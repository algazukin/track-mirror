// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Models;
using TrackMirror.Constants;
using TrackMirror.Storage.Sqlite;

namespace TrackMirror.Storage.Management;

internal sealed class CaptureRetention
{
    private readonly ViewerDatabase _database;

    internal CaptureRetention(string dataRoot)
        : this(new ViewerDatabase(dataRoot))
    {
    }

    internal CaptureRetention(ViewerDatabase database)
    {
        _database = database;
    }

    internal List<CaptureEntry> Candidates(AppSettings settings, DateTime utcNow)
    {
        if (settings.RetentionDays is < 1 or > DataManagementNames.MaximumDays)
            throw new ArgumentOutOfRangeException(nameof(settings));

        using var connection = _database.Open();

        using var command = connection.CreateCommand();

        command.CommandText = RetentionSql.Candidates;

        command.Parameters.AddWithValue(DatabaseNames.Cutoff, utcNow.AddDays(-settings.RetentionDays).Ticks);

        command.Parameters.AddWithValue(DatabaseNames.KeepFlagged, settings.RetentionKeepFlagged);

        command.Parameters.AddWithValue(DatabaseNames.KeepUnread, settings.RetentionKeepUnread);

        using var reader = command.ExecuteReader();

        var entries = new List<CaptureEntry>();

        while (reader.Read())
        {
            entries.Add(CaptureRecords.ReadEntry(reader));
        }

        return entries;
    }

    internal Task<RetentionResult> PurgeAsync(AppSettings settings, DateTime utcNow, CancellationToken token) =>
        Task.Run(() =>
        {
            var candidates = Candidates(settings, utcNow);

            foreach (var entry in candidates)
            {
                token.ThrowIfCancellationRequested();

                DataFolderRules.CapturePath(_database.DataRoot, entry);
            }

            _database.Write((connection, transaction) =>
            {
                using var command = connection.CreateCommand();

                command.Transaction = transaction;

                command.CommandText = RetentionSql.Intent;

                foreach (var entry in candidates)
                {
                    token.ThrowIfCancellationRequested();

                    command.Parameters.Clear();

                    command.Parameters.AddWithValue(DatabaseNames.Key, PersistentIdentity.Key(entry.Id));

                    command.Parameters.AddWithValue(DatabaseNames.Source,
                        PersistentIdentity.Key(entry.Account + StorageNames.EntrySeparator + entry.FileName));

                    command.Parameters.AddWithValue(DatabaseNames.Id, entry.Id);

                    command.Parameters.AddWithValue(DatabaseNames.Account, entry.Account);

                    command.Parameters.AddWithValue(DatabaseNames.File, entry.FileName);

                    command.Parameters.AddWithValue(DatabaseNames.Updated, utcNow.Ticks);

                    command.ExecuteNonQuery();
                }
            });

            RecoverPending(token);

            return new RetentionResult(candidates.Count, PendingFiles());
        }, token);

    internal int PendingFiles()
    {
        using var connection = _database.Open();

        using var command = connection.CreateCommand();

        command.CommandText = RetentionSql.PendingCount;

        return Convert.ToInt32(command.ExecuteScalar());
    }

    internal List<CaptureEntry> Deleted(bool pendingOnly = false)
    {
        using var connection = _database.Open();

        using var command = connection.CreateCommand();

        command.CommandText = pendingOnly ? RetentionSql.Pending : RetentionSql.Deleted;

        using var reader = command.ExecuteReader();

        var entries = new List<CaptureEntry>();

        while (reader.Read())
        {
            entries.Add(new CaptureEntry
            {
                Id = reader.GetString(0), Account = reader.GetString(1), FileName = reader.GetString(2)
            });
        }

        return entries;
    }

    internal void RecoverPending(CancellationToken token)
    {
        foreach (var entry in Deleted(pendingOnly: true))
        {
            token.ThrowIfCancellationRequested();

            try
            {
                File.Delete(DataFolderRules.CapturePath(_database.DataRoot, entry));

                _database.Write((connection, transaction) =>
                {
                    using var command = connection.CreateCommand();

                    command.Transaction = transaction;

                    command.CommandText = RetentionSql.Completed;

                    command.Parameters.AddWithValue(DatabaseNames.Key, PersistentIdentity.Key(entry.Id));

                    command.ExecuteNonQuery();
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The durable intent remains pending and is retried without recovering the image into history.
            }
        }
    }
}
