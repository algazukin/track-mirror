// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using Microsoft.Data.Sqlite;
using TrackMirror.Constants;
using TrackMirror.Models;

namespace TrackMirror.Storage.Sqlite;

internal static class CaptureRecords
{
    internal static List<CaptureEntry> Read(ViewerDatabase database)
    {
        using var connection = database.Open();

        using var command = connection.CreateCommand();

        command.CommandText = CaptureSql.SelectAll;

        using var reader = command.ExecuteReader();

        var entries = new List<CaptureEntry>();

        while (reader.Read())
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    internal static CaptureEntry ReadEntry(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0), Account = reader.GetString(1), FileName = reader.GetString(2),
        CapturedUtc = new DateTime(reader.GetInt64(3), DateTimeKind.Utc),
        SavedUtc = new DateTime(reader.GetInt64(4), DateTimeKind.Utc),
        Viewed = reader.GetBoolean(5), Flagged = reader.GetBoolean(6)
    };

    internal static void Write(SqliteConnection connection, SqliteTransaction transaction,
        IEnumerable<CaptureEntry> entries)
    {
        using var command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText = CaptureSql.Upsert;

        foreach (var entry in entries)
        {
            command.Parameters.Clear();

            command.Parameters.AddWithValue(DatabaseNames.Key, PersistentIdentity.Key(entry.Id));

            command.Parameters.AddWithValue(DatabaseNames.Id, entry.Id);

            command.Parameters.AddWithValue(DatabaseNames.Account, entry.Account);

            command.Parameters.AddWithValue(DatabaseNames.File, entry.FileName);

            command.Parameters.AddWithValue(DatabaseNames.Captured, entry.CapturedUtc.Ticks);

            command.Parameters.AddWithValue(DatabaseNames.Saved, entry.SavedUtc.Ticks);

            command.Parameters.AddWithValue(DatabaseNames.Viewed, entry.Viewed);

            command.Parameters.AddWithValue(DatabaseNames.Flagged, entry.Flagged);

            command.ExecuteNonQuery();
        }
    }
}
