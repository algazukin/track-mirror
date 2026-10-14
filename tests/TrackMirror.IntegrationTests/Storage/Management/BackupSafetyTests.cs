// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using TrackMirror.Constants;
using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.Storage;
using TrackMirror.Storage.Management;
using Microsoft.Data.Sqlite;
using Xunit;

namespace TrackMirror.IntegrationTests.Storage.Management;

public sealed class BackupSafetyTests
{
    [Fact]
    public async Task Matching_archive_hash_does_not_allow_a_corrupt_database_to_replace_active_data()
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        var capture = source.ImageCapture();

        source.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(source.Root);

        var archive = Path.Combine(output.Root, "corrupt-database.zip");

        await DataArchive.CreateBackupAsync(source.Root, archive, default);

        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        {
            var header = zip.GetEntry(DataManagementNames.ManifestFile)!;

            DataBackupManifest manifest;

            using (var stream = header.Open())
            {
                manifest = JsonSerializer.Deserialize<DataBackupManifest>(stream)!;
            }

            var original = manifest.Files.Single(file => file.Path == "viewer.db");

            var corrupt = new byte[checked((int)original.Length)];

            var replacement = original with { Sha256 = Convert.ToHexString(SHA256.HashData(corrupt)) };

            zip.GetEntry("viewer.db")!.Delete();

            using (var content = zip.CreateEntry("viewer.db").Open())
            {
                content.Write(corrupt);
            }

            header.Delete();

            using (var stream = zip.CreateEntry(DataManagementNames.ManifestFile).Open())
            {
                JsonSerializer.Serialize(stream, manifest with
                {
                    Files = manifest.Files.Select(file => file.Path == "viewer.db" ? replacement : file).ToList()
                });
            }
        }

        var target = Path.Combine(output.Root, "restore");

        // Act
        var error = await Record.ExceptionAsync(() => DataArchive.RestoreAsync(source.Root, archive, target, default));

        // Assert
        Assert.IsType<SqliteException>(error);

        Assert.Equal(source.Root, DataLocationStore.ResolveRedirects(source.Root));

        Assert.Equal(capture, Assert.Single(new ScreenshotStore(source.Root).Snapshot()));

        Assert.True(File.Exists(store.CopyPath(capture)));

        Assert.False(Directory.Exists(target));

        Assert.Empty(Directory.EnumerateDirectories(output.Root));
    }

    [Theory]
    [InlineData("preparing")]
    [InlineData("copying")]
    [InlineData("checking")]
    [InlineData("packing")]
    public async Task Cancelled_backup_preserves_the_previous_archive_and_cleans_only_its_owned_files(string phase)
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        var capture = source.ImageCapture();

        source.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(source.Root);

        var archive = Path.Combine(output.Root, "backup.zip");

        await DataArchive.CreateBackupAsync(source.Root, archive, default);

        var original = File.ReadAllBytes(archive);

        store.MarkViewed(capture.Id);

        using var cancellation = new CancellationTokenSource();

        var stage = phase switch
        {
            "preparing" => DataManagementText.Preparing,
            "copying" => DataManagementText.Copying,
            "checking" => DataManagementText.Checking,
            "packing" => DataManagementText.Packing,
            _ => throw new ArgumentException(nameof(phase))
        };

        var progress = new ImmediateProgress(value =>
        {
            if (value.Stage == stage)
            {
                cancellation.Cancel();
            }
        });

        // Act
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DataArchive.CreateBackupAsync(source.Root, archive, cancellation.Token, progress));

        Assert.True(cancellation.IsCancellationRequested);

        Assert.Equal(original, File.ReadAllBytes(archive));

        Assert.Equal(archive, Assert.Single(Directory.EnumerateFileSystemEntries(output.Root)));

        Assert.True(Assert.Single(new ScreenshotStore(source.Root).Snapshot()).Viewed);

        Assert.True(File.Exists(store.CopyPath(capture)));

        Assert.Equal(source.Root, DataLocationStore.ResolveRedirects(source.Root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelled_restore_never_switches_the_active_folder_or_leaves_a_partial_destination(bool checking)
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        var capture = source.ImageCapture();

        source.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(source.Root);

        var archive = Path.Combine(output.Root, "backup.zip");

        await DataArchive.CreateBackupAsync(source.Root, archive, default);

        store.MarkViewed(capture.Id);

        var target = Path.Combine(output.Root, "restore");

        using var cancellation = new CancellationTokenSource();

        var progress = new ImmediateProgress(value =>
        {
            if (value.Stage == (checking ? DataManagementText.Checking : DataManagementText.Restoring))
            {
                cancellation.Cancel();
            }
        });

        // Act
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DataArchive.RestoreAsync(source.Root, archive, target, cancellation.Token, progress));

        Assert.True(cancellation.IsCancellationRequested);

        Assert.False(Directory.Exists(target));

        Assert.Empty(Directory.EnumerateDirectories(output.Root));

        Assert.True(Assert.Single(new ScreenshotStore(source.Root).Snapshot()).Viewed);

        Assert.Equal(source.Root, DataLocationStore.ResolveRedirects(source.Root));

        // Act
        await DataArchive.RestoreAsync(source.Root, archive, target, default);

        // Assert
        Assert.Equal(capture, Assert.Single(new ScreenshotStore(target).Snapshot()));

        Assert.True(Assert.Single(new ScreenshotStore(source.Root).Snapshot()).Viewed);
    }

    [Theory]
    [InlineData("missing-image")]
    [InlineData("extra-entry")]
    [InlineData("duplicate-entry")]
    [InlineData("duplicate-catalog")]
    [InlineData("symlink")]
    public async Task Invalid_archive_structure_is_rejected_without_changing_the_active_data(string fault)
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        var capture = source.ImageCapture();

        source.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(source.Root);

        var archive = Path.Combine(output.Root, "invalid.zip");

        await DataArchive.CreateBackupAsync(source.Root, archive, default);

        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        {
            var image = zip.GetEntry($"captures/{capture.Account}/{capture.FileName}")!;

            switch (fault)
            {
                case "missing-image":
                    image.Delete();
                    break;
                case "extra-entry":
                    zip.CreateEntry("unlisted.txt");
                    break;
                case "duplicate-entry":
                    zip.CreateEntry(image.FullName);
                    break;
                case "duplicate-catalog":
                    var header = zip.GetEntry(DataManagementNames.ManifestFile)!;

                    DataBackupManifest manifest;

                    using (var input = header.Open())
                    {
                        manifest = JsonSerializer.Deserialize<DataBackupManifest>(input)!;
                    }

                    header.Delete();

                    zip.CreateEntry("unlisted.txt");

                    using (var replacement = zip.CreateEntry(DataManagementNames.ManifestFile).Open())
                    {
                        JsonSerializer.Serialize(replacement, manifest with
                        {
                            Files = [.. manifest.Files, manifest.Files[0]]
                        });
                    }

                    break;
                case "symlink":
                    image.ExternalAttributes = unchecked((int)0xa0000000);
                    break;
                default:
                    throw new ArgumentException(nameof(fault));
            }
        }

        var target = Path.Combine(output.Root, "restore");

        // Act
        // Assert
        await Assert.ThrowsAsync<IOException>(() => DataArchive.RestoreAsync(source.Root, archive, target, default));

        Assert.False(Directory.Exists(target));

        Assert.Empty(Directory.EnumerateDirectories(output.Root));

        Assert.Equal(capture, Assert.Single(new ScreenshotStore(source.Root).Snapshot()));

        Assert.True(File.Exists(store.CopyPath(capture)));

        Assert.Equal(source.Root, DataLocationStore.ResolveRedirects(source.Root));
    }
}
