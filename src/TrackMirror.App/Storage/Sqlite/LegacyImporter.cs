// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using Microsoft.Data.Sqlite;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Storage;

namespace TrackMirror.Storage.Sqlite;

internal static class LegacyImporter
{
    internal static void Import(ViewerDatabase database, SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText = DatabaseSql.ImportComplete;

        if (Convert.ToInt32(command.ExecuteScalar()) != 0)
            return;

        var skipped = 0;

        var captures = ReadCaptures(database.DataRoot, ref skipped);

        var activities = LegacyJson.Read<HubstaffActivity>(database.DataRoot, HubstaffActivityNames.HistoryFile)
            .Concat(LegacyJson.Read<HubstaffActivity>(database.DataRoot, HubstaffActivityNames.CacheFile)).ToList();

        var validActivities = activities.Where(HubstaffActivityValidation.Valid).ToList();

        skipped += activities.Count - validActivities.Count;

        var scans = LegacyJson.Read<PrivacyScanRecord>(database.DataRoot, PrivacyNames.ResultsFile, 32 * 1024 * 1024);

        var validScans = scans.Where(record => PrivacyRecordValidation.Valid(record)
            && captures.ContainsKey(record.CaptureId)).Select(PrivacyCoverage.ImportLegacy).ToList();

        skipped += scans.Count - validScans.Count;

        CaptureRecords.Write(connection, transaction, captures.Values);

        ActivityRecords.Write(connection, transaction, validActivities);

        PrivacyRecords.Write(connection, transaction, validScans);

        command.CommandText = DatabaseSql.CompleteImport;

        command.Parameters.AddWithValue(DatabaseNames.Updated, DateTime.UtcNow.Ticks);

        command.Parameters.AddWithValue(DatabaseNames.Skipped, skipped);

        command.ExecuteNonQuery();

        transaction.Commit();
    }

    private static Dictionary<string, CaptureEntry> ReadCaptures(string root, ref int skipped)
    {
        var records = new Dictionary<string, CaptureEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in LegacyJson.Read<CaptureEntry>(root, StorageNames.IndexFile))
        {
            if (!CaptureFileMetadata.Valid(entry))
            {
                skipped++;

                continue;
            }

            var id = string.IsNullOrEmpty(entry.Id)
                ? entry.Account + StorageNames.EntrySeparator + entry.FileName : entry.Id;

            records[id] = entry with { Id = id };
        }

        var directory = Path.Combine(root, StorageNames.CapturesDirectory);

        var sources = records.Values.Select(entry => entry.Account + StorageNames.EntrySeparator + entry.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(directory))
            return records;

        foreach (var account in Directory.EnumerateDirectories(directory))
        {
            foreach (var path in Directory.EnumerateFiles(account, StorageNames.ScreenshotPattern))
            {
                var name = Path.GetFileName(path);

                var owner = Path.GetFileName(account);

                var id = owner + StorageNames.EntrySeparator + name;

                if (sources.Contains(id))
                    continue;

                var time = File.GetLastWriteTimeUtc(path);

                records[id] = new CaptureEntry
                {
                    Id = id, Account = owner, FileName = name,
                    CapturedUtc = CaptureFileMetadata.CaptureTime(name, time),
                    SavedUtc = time, Viewed = true
                };
            }
        }

        return records;
    }
}
