// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.IO.Compression;
using System.Text.Json;
using TrackMirror.Constants;
using TrackMirror.Storage;
using TrackMirror.Storage.Management;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Storage.Management;

public sealed class DataOperationFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_before_copy_or_after_verification_keeps_source_active_and_allows_retry(bool checking)
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        var capture = source.ImageCapture(viewed: true, flagged: true);

        source.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(source.Root);

        using var cancellation = new CancellationTokenSource();

        var progress = new ImmediateProgress(value =>
        {
            if (value.Stage == (checking ? DataManagementText.Checking : DataManagementText.Preparing))
            {
                cancellation.Cancel();
            }
        });

        var target = Path.Combine(output.Root, "cancelled");

        // Act
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DataFolderTransfer.MoveAsync(source.Root, target, cancellation.Token, progress));

        Assert.True(cancellation.IsCancellationRequested);

        Assert.Empty(Directory.EnumerateFileSystemEntries(output.Root));

        Assert.Equal(capture, Assert.Single(new ScreenshotStore(source.Root).Snapshot()));

        Assert.True(File.Exists(store.CopyPath(capture)));

        Assert.Equal(source.Root, DataLocationStore.ResolveRedirects(source.Root));

        // Act
        await DataFolderTransfer.MoveAsync(source.Root, target, default);

        // Assert
        Assert.Equal(target, DataLocationStore.ResolveRedirects(source.Root));

        Assert.Equal(capture, Assert.Single(new ScreenshotStore(target).Snapshot()));
    }

    [Fact]
    public async Task Cancellation_during_copy_keeps_source_active_and_removes_owned_staging()
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        var capture = source.Capture();

        source.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(source.Root);

        using var cancellation = new CancellationTokenSource();

        var progress = new ImmediateProgress(value =>
        {
            if (value.Stage == DataManagementText.Copying)
            {
                cancellation.Cancel();
            }
        });

        var target = Path.Combine(output.Root, "cancelled");

        // Act
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DataFolderTransfer.MoveAsync(source.Root, target, cancellation.Token, progress));

        Assert.False(Directory.Exists(target));

        Assert.Equal(source.Root, DataLocationStore.ResolveRedirects(source.Root));

        Assert.True(File.Exists(store.CopyPath(capture)));

        Assert.Empty(Directory.EnumerateDirectories(output.Root));
    }

    [Fact]
    public async Task Locked_source_file_leaves_no_destination_or_redirect()
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        var capture = source.Capture();

        source.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(source.Root);

        using var locked = new FileStream(store.CopyPath(capture), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var target = Path.Combine(output.Root, "failed");

        // Act
        // Assert
        await Assert.ThrowsAsync<IOException>(() => DataFolderTransfer.MoveAsync(source.Root, target, default));

        Assert.False(Directory.Exists(target));

        Assert.Equal(source.Root, DataLocationStore.ResolveRedirects(source.Root));

        Assert.Empty(Directory.EnumerateDirectories(output.Root));
    }

    [Theory]
    [InlineData("../escape.jpg")]
    [InlineData("captures/account/CON.jpg")]
    [InlineData("captures/account/image.jpg.")]
    [InlineData("captures/account/image.jpg ")]
    [InlineData(null)]
    public async Task Unsafe_archive_catalog_cannot_switch_or_write_outside_staging(string? unsafePath)
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        _ = new ScreenshotStore(source.Root);

        var archive = Path.Combine(output.Root, "unsafe.zip");

        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            var manifest = new DataBackupManifest(DataManagementNames.BackupSignature, 1,
                [new DataBackupFile(unsafePath!, 0, new string('0', 64))]);

            using (var header = zip.CreateEntry(DataManagementNames.ManifestFile).Open())
            {
                JsonSerializer.Serialize(header, manifest);
            }

            zip.CreateEntry("unrelated");
        }

        var target = Path.Combine(output.Root, "restored");

        // Act
        // Assert
        await Assert.ThrowsAsync<IOException>(() => DataArchive.RestoreAsync(source.Root, archive, target, default));

        Assert.False(Directory.Exists(target));

        Assert.Equal(source.Root, DataLocationStore.ResolveRedirects(source.Root));

        Assert.Empty(Directory.EnumerateDirectories(output.Root));
    }

    [Fact]
    public async Task Tampered_image_is_rejected_before_the_active_folder_changes()
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        var capture = source.Capture();

        source.Json("index.json", new[] { capture });

        _ = new ScreenshotStore(source.Root);

        var archive = Path.Combine(output.Root, "tampered.zip");

        await DataArchive.CreateBackupAsync(source.Root, archive, default);

        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        {
            var name = "captures/" + capture.Account + "/" + capture.FileName;

            zip.GetEntry(name)!.Delete();

            using var content = zip.CreateEntry(name).Open();

            content.Write([0, 0, 0, 0]);
        }

        var target = Path.Combine(output.Root, "restored");

        // Act
        // Assert
        await Assert.ThrowsAsync<IOException>(() => DataArchive.RestoreAsync(source.Root, archive, target, default));

        Assert.False(Directory.Exists(target));

        Assert.Equal(source.Root, DataLocationStore.ResolveRedirects(source.Root));
    }

    [Fact]
    public void Overlap_and_nonempty_destinations_are_rejected_without_modification()
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        // Act
        // Assert
        Assert.Throws<IOException>(() => DataFolderRules.EmptyTarget(source.Root, source.Root));

        Assert.Throws<IOException>(() => DataFolderRules.EmptyTarget(source.Root, Path.Combine(source.Root, "child")));

        Assert.Throws<IOException>(() => DataFolderRules.EmptyTarget(source.Root, Path.GetDirectoryName(source.Root)!));

        // Arrange
        var sentinel = Path.Combine(output.Root, "keep.txt");

        File.WriteAllText(sentinel, "keep");

        // Act
        // Assert
        Assert.Throws<IOException>(() => DataFolderRules.EmptyTarget(source.Root, output.Root));

        Assert.Equal("keep", File.ReadAllText(sentinel));
    }

    [Fact]
    public void Invalid_redirect_marker_and_cycles_never_open_an_empty_replacement()
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        var id = Guid.NewGuid();

        AtomicJson.Write(Path.Combine(source.Root, DataManagementNames.RedirectFile),
            new DataLocationRecord(output.Root, id));

        // Act
        // Assert
        Assert.Throws<IOException>(() => DataLocationStore.ResolveRedirects(source.Root));

        // Arrange
        _ = new ScreenshotStore(output.Root);

        AtomicJson.Write(Path.Combine(output.Root, DataManagementNames.MarkerFile), new DataFolderMarker(id));

        AtomicJson.Write(Path.Combine(source.Root, DataManagementNames.MarkerFile), new DataFolderMarker(id));

        _ = new ScreenshotStore(source.Root);

        AtomicJson.Write(Path.Combine(output.Root, DataManagementNames.RedirectFile),
            new DataLocationRecord(source.Root, id));

        // Act
        // Assert
        Assert.Throws<IOException>(() => DataLocationStore.ResolveRedirects(source.Root));
    }
}
