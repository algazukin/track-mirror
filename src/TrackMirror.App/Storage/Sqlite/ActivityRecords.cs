// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using Microsoft.Data.Sqlite;
using TrackMirror.Constants;
using TrackMirror.Models;

namespace TrackMirror.Storage.Sqlite;

internal static class ActivityRecords
{
    internal static List<HubstaffActivity> Read(ViewerDatabase database)
    {
        using var connection = database.Open();

        using var command = connection.CreateCommand();

        command.CommandText = ActivitySql.SelectAll;

        using var reader = command.ExecuteReader();

        var entries = new List<HubstaffActivity>();

        while (reader.Read())
        {
            entries.Add(new HubstaffActivity(reader.GetString(0), new DateTime(reader.GetInt64(1), DateTimeKind.Utc),
                reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5),
                new DateTime(reader.GetInt64(6), DateTimeKind.Utc)));
        }

        return entries;
    }

    internal static void Write(SqliteConnection connection, SqliteTransaction transaction,
        IEnumerable<HubstaffActivity> entries)
    {
        using var command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText = ActivitySql.Upsert;

        foreach (var entry in entries)
        {
            command.Parameters.Clear();

            command.Parameters.AddWithValue(DatabaseNames.Key, PersistentIdentity.Key(entry.Account));

            command.Parameters.AddWithValue(DatabaseNames.Account, entry.Account);

            command.Parameters.AddWithValue(DatabaseNames.Started, entry.StartedUtc.Ticks);

            command.Parameters.AddWithValue(DatabaseNames.Tracked, entry.TrackedSeconds);

            command.Parameters.AddWithValue(DatabaseNames.Mouse, entry.MouseSeconds);

            command.Parameters.AddWithValue(DatabaseNames.Keyboard, entry.KeyboardSeconds);

            command.Parameters.AddWithValue(DatabaseNames.Overall, entry.OverallSeconds);

            command.Parameters.AddWithValue(DatabaseNames.Observed, entry.ObservedUtc.Ticks);

            command.ExecuteNonQuery();
        }
    }
}
