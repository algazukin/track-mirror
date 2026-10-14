// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.IO.Compression;
using System.Text.Json;
using TrackMirror.Constants;
using TrackMirror.Storage.Sqlite;

namespace TrackMirror.Storage.Management;

internal static class DataArchive
{
    internal static async Task CreateBackupAsync(string source, string archive, CancellationToken token,
        IProgress<DataOperationProgress>? progress = null)
    {
        source = DataFolderRules.Canonical(source);

        archive = Path.GetFullPath(archive);

        if (DataFolderRules.Overlaps(source, archive))
            throw new IOException(DataManagementText.Overlap);

        DataFolderRules.CheckLocal(Path.GetDirectoryName(archive)!);

        using var lease = new FileStream(Path.Combine(source, DataManagementNames.LockFile), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);

        using var staging = new OwnedDataStaging(Path.GetDirectoryName(archive)!);

        var files = await DataSnapshot.CreateAsync(source, staging.Root, token, progress).ConfigureAwait(false);

        var temporary = archive + Guid.NewGuid().ToString(Formats.CompactGuid) + StorageNames.PartialSuffix;

        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                DataManagementNames.CopyBuffer, useAsync: true))
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var total = files.Sum(file => file.Length);

                    long completed = 0;

                    foreach (var file in files)
                    {
                        token.ThrowIfCancellationRequested();

                        var entry = zip.CreateEntry(file.Path, CompressionLevel.Fastest);

                        await using var output = entry.Open();

                        await using var input = File.OpenRead(DataFolderRules.OwnedFile(staging.Root, file.Path));

                        progress?.Report(new DataOperationProgress(DataManagementText.Packing, completed, total));

                        await input.CopyToAsync(output, token).ConfigureAwait(false);

                        completed += file.Length;
                    }

                    var manifest = new DataBackupManifest(DataManagementNames.BackupSignature,
                        DataManagementNames.ArchiveVersion, files);

                    await using var header = zip.CreateEntry(DataManagementNames.ManifestFile).Open();

                    await JsonSerializer.SerializeAsync(header, manifest, cancellationToken: token)
                        .ConfigureAwait(false);
                }

                await stream.FlushAsync(token).ConfigureAwait(false);
            }

            token.ThrowIfCancellationRequested();

            File.Move(temporary, archive, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    internal static async Task<string> RestoreAsync(string source, string archive, string target,
        CancellationToken token,
        IProgress<DataOperationProgress>? progress = null)
    {
        source = DataFolderRules.Canonical(source);

        target = DataFolderRules.Canonical(target);

        DataFolderRules.EmptyTarget(source, target);

        using var lease = new FileStream(Path.Combine(source, DataManagementNames.LockFile), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);

        using var staging = new OwnedDataStaging(Path.GetDirectoryName(target)!);

        await ExtractAsync(archive, staging.Root, token, progress).ConfigureAwait(false);

        DataSnapshot.ValidateDatabase(staging.Root);

        var database = new ViewerDatabase(staging.Root);

        DataSnapshot.ValidateRecords(database);

        token.ThrowIfCancellationRequested();

        staging.Promote(source, target);

        token.ThrowIfCancellationRequested();

        try
        {
            AtomicJson.Write(Path.Combine(source, DataManagementNames.RedirectFile),
                new DataLocationRecord(target, staging.TransferId));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException(DataManagementText.UncommittedCopy, ex);
        }

        return target;
    }

    private static async Task ExtractAsync(string archive, string target, CancellationToken token,
        IProgress<DataOperationProgress>? progress)
    {
        using var zip = ZipFile.OpenRead(archive);

        var header = zip.GetEntry(DataManagementNames.ManifestFile);

        if (header == null || header.Length > DataManagementNames.MaximumManifestBytes)
            throw new IOException(DataManagementText.InvalidArchive);

        await using var input = header.Open();

        var manifest = await JsonSerializer.DeserializeAsync<DataBackupManifest>(input, cancellationToken: token)
            .ConfigureAwait(false);

        if (manifest == null || manifest.Signature != DataManagementNames.BackupSignature
            || manifest.Version != DataManagementNames.ArchiveVersion || manifest.Files == null
            || manifest.Files.Count > 1000000 || zip.Entries.Count != manifest.Files.Count + 1)
            throw new IOException(DataManagementText.InvalidArchive);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        long total = 0;

        foreach (var file in manifest.Files)
        {
            if (file == null || !DataSnapshot.AllowedFile(file.Path) || !names.Add(file.Path)
                || file.Length < 0 || file.Length > DataManagementNames.MaximumBackupBytes
                || file.Sha256 is not { Length: 64 } || !file.Sha256.All(char.IsAsciiHexDigit))
                throw new IOException(DataManagementText.InvalidArchive);

            total = checked(total + file.Length);

            if (total > DataManagementNames.MaximumBackupBytes)
                throw new IOException(DataManagementText.InvalidArchive);

            DataFolderRules.OwnedFile(target, file.Path);
        }

        if (!names.Contains(DatabaseNames.FileName))
            throw new IOException(DataManagementText.InvalidArchive);

        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);

        foreach (var entry in zip.Entries)
        {
            if (!entries.TryAdd(entry.FullName, entry))
                throw new IOException(DataManagementText.InvalidArchive);
        }

        long completed = 0;

        foreach (var file in manifest.Files)
        {
            if (!entries.TryGetValue(file.Path, out var entry) || entry.Length != file.Length
                || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000
                || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new IOException(DataManagementText.InvalidArchive);

            await using var content = entry.Open();

            var copied = await DataFileCopy.CopyAsync(content, DataFolderRules.OwnedFile(target, file.Path),
                file.Path, token, read =>
                {
                    completed += read;

                    progress?.Report(new DataOperationProgress(DataManagementText.Restoring, completed, total));
                }, file.Length).ConfigureAwait(false);

            if (copied.Length != file.Length
                || !string.Equals(copied.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException(DataManagementText.InvalidArchive);
        }

        progress?.Report(new DataOperationProgress(DataManagementText.Checking, completed, total));

        await DataSnapshot.VerifyFilesAsync(target, manifest.Files, token).ConfigureAwait(false);
    }
}
