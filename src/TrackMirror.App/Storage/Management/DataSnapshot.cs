// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using TrackMirror.Constants;
using TrackMirror.Privacy.Storage;
using TrackMirror.Storage.Sqlite;

namespace TrackMirror.Storage.Management;

internal static class DataSnapshot
{
    internal static readonly IReadOnlySet<string> MetadataFiles = new HashSet<string>(StringComparer.Ordinal)
    {
        DatabaseNames.FileName, StorageNames.SettingsFile, StorageNames.IndexFile,
        HubstaffActivityNames.HistoryFile, HubstaffActivityNames.CacheFile, PrivacyNames.ResultsFile
    };

    internal static async Task<List<DataBackupFile>> CreateAsync(string source, string target,
        CancellationToken token, IProgress<DataOperationProgress>? progress)
    {
        DataFolderRules.CheckLocal(source);

        token.ThrowIfCancellationRequested();

        progress?.Report(new DataOperationProgress(DataManagementText.Preparing));

        var database = new ViewerDatabase(source);

        var destination = DataFolderRules.OwnedFile(target, DatabaseNames.FileName);

        {
            using var input = database.Open();

            using var output = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = destination, Pooling = false
            }.ToString());

            output.Open();

            input.BackupDatabase(output);
        }

        ValidateDatabase(target);

        ValidateRecords(new ViewerDatabase(target));

        var files = new List<DataBackupFile>();

        await using (var stream = File.OpenRead(destination))
        {
            var hash = await SHA256.HashDataAsync(stream, token).ConfigureAwait(false);

            files.Add(new DataBackupFile(DatabaseNames.FileName, stream.Length, Convert.ToHexString(hash)));
        }

        var paths = MetadataFiles.Where(name => name != DatabaseNames.FileName)
            .Select(name => Path.Combine(source, name)).Where(File.Exists).ToList();

        var captures = Path.Combine(source, StorageNames.CapturesDirectory);

        var deleted = new CaptureRetention(database).Deleted().Select(entry =>
            DataFolderRules.CapturePath(source, entry)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(captures))
        {
            DataFolderRules.CheckLocal(captures);

            foreach (var account in Directory.EnumerateDirectories(captures))
            {
                DataFolderRules.CheckLocal(account);

                paths.AddRange(Directory.EnumerateFiles(account, StorageNames.ScreenshotPattern)
                    .Where(path => !deleted.Contains(path)));
            }
        }

        var total = files[0].Length + paths.Sum(path => new FileInfo(path).Length);

        var completed = files[0].Length;

        foreach (var path in paths)
        {
            var relative = Path.GetRelativePath(source, path).Replace(Path.DirectorySeparatorChar, '/');

            var ownedSource = DataFolderRules.OwnedFile(source, relative);

            var ownedTarget = DataFolderRules.OwnedFile(target, relative);

            await using var stream = new FileStream(ownedSource, FileMode.Open, FileAccess.Read, FileShare.Read,
                DataManagementNames.CopyBuffer, useAsync: true);

            files.Add(await DataFileCopy.CopyAsync(stream, ownedTarget, relative, token, read =>
            {
                completed += read;

                progress?.Report(new DataOperationProgress(DataManagementText.Copying, completed, total));
            }).ConfigureAwait(false));
        }

        token.ThrowIfCancellationRequested();

        progress?.Report(new DataOperationProgress(DataManagementText.Checking, completed, total));

        await VerifyFilesAsync(target, files, token).ConfigureAwait(false);

        ValidateDatabase(target);

        return files;
    }

    internal static async Task VerifyFilesAsync(string root, IReadOnlyList<DataBackupFile> files,
        CancellationToken token)
    {
        foreach (var file in files)
        {
            var path = DataFolderRules.OwnedFile(root, file.Path);

            await using var input = File.OpenRead(path);

            if (input.Length != file.Length)
                throw new IOException(DataManagementText.InvalidArchive);

            var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, token).ConfigureAwait(false));

            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException(DataManagementText.InvalidArchive);
        }
    }

    internal static bool AllowedFile(string? relative)
    {
        if (string.IsNullOrEmpty(relative))
            return false;

        if (MetadataFiles.Contains(relative))
            return true;

        var parts = relative.Split('/');

        return parts.Length == 3 && parts[0] == StorageNames.CapturesDirectory
            && DataFolderRules.SafeSegment(parts[1]) && DataFolderRules.SafeSegment(parts[2])
            && parts[2].EndsWith(StorageNames.ScreenshotExtension, StringComparison.OrdinalIgnoreCase);
    }

    internal static void ValidateRecords(ViewerDatabase database)
    {
        foreach (var capture in CaptureRecords.Read(database))
        {
            if (string.IsNullOrEmpty(capture.Id))
                throw new IOException(DataManagementText.InvalidDatabase);

            DataFolderRules.CapturePath(database.DataRoot, capture);
        }

        if (ActivityRecords.Read(database).Any(activity => !HubstaffActivityValidation.Valid(activity))
            || PrivacyRecords.Read(database).Any(record => !PrivacyRecordValidation.Valid(record)))
            throw new IOException(DataManagementText.InvalidDatabase);

        foreach (var deleted in new CaptureRetention(database).Deleted())
        {
            DataFolderRules.CapturePath(database.DataRoot, deleted);
        }
    }

    internal static void ValidateDatabase(string root)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DataFolderRules.OwnedFile(root, DatabaseNames.FileName),
            Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = DataManagementSql.Integrity;

        if (!Equals(command.ExecuteScalar(), DataManagementSql.IntegrityOk))
            throw new IOException(DataManagementText.InvalidDatabase);

        command.CommandText = DataManagementSql.ForeignKeys;

        if (Convert.ToInt32(command.ExecuteScalar()) != 0)
            throw new IOException(DataManagementText.InvalidDatabase);

        command.CommandText = DatabaseSql.ReadVersion;

        var version = Convert.ToInt32(command.ExecuteScalar());

        if (version is < 1 or > DatabaseNames.CurrentVersion)
            throw new IOException(DataManagementText.InvalidDatabase);

        command.CommandText = DataManagementSql.RequiredTables;

        if (Convert.ToInt32(command.ExecuteScalar()) != 8)
            throw new IOException(DataManagementText.InvalidDatabase);

        if (version >= 2)
        {
            command.CommandText = RetentionSql.PendingCount;

            command.ExecuteScalar();
        }
    }
}
