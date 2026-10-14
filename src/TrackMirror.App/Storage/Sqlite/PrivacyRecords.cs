// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Text.Json;
using Microsoft.Data.Sqlite;
using TrackMirror.Constants;
using TrackMirror.Privacy.Models;

namespace TrackMirror.Storage.Sqlite;

internal static class PrivacyRecords
{
    internal static List<PrivacyScanRecord> Read(ViewerDatabase database)
    {
        using var connection = database.Open();

        using var transaction = connection.BeginTransaction(deferred: true);

        using var command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText = PrivacySql.SelectScans;

        var records = new Dictionary<string, PrivacyScanRecord>();

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                records.Add(reader.GetString(0), new PrivacyScanRecord
                {
                    CaptureId = reader.GetString(1), RequestId = reader.GetString(2), Profile = reader.GetString(3),
                    ImageHash = reader.GetString(4), State = (PrivacyScanState)reader.GetInt32(5),
                    Disposition = (PrivacyDisposition)reader.GetInt32(6), FailureCode = reader.GetString(7),
                    UpdatedUtc = new DateTime(reader.GetInt64(8), DateTimeKind.Utc), Attempts = reader.GetInt32(9),
                    DurationSeconds = reader.GetDouble(10)
                });
            }
        }

        command.CommandText = PrivacySql.SelectCategories;

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                records[reader.GetString(0)].Categories.Add(new PrivacyCategoryResult
                {
                    Category = (PrivacyScanCategory)reader.GetInt32(1), Version = reader.GetString(2),
                    State = (PrivacyScanState)reader.GetInt32(3), FailureCode = reader.GetString(4),
                    UpdatedUtc = new DateTime(reader.GetInt64(5), DateTimeKind.Utc)
                });
            }
        }

        command.CommandText = PrivacySql.SelectRequested;

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                records[reader.GetString(0)].RequestedCategories.Add((PrivacyScanCategory)reader.GetInt32(1));
            }
        }

        command.CommandText = PrivacySql.SelectFindings;

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var record = records[reader.GetString(0)];

                var finding = new PrivacyFinding(reader.GetString(2), reader.GetString(3), reader.GetString(4),
                    JsonSerializer.Deserialize<List<PrivacyRegion>>(reader.GetString(5))!);

                if (reader.IsDBNull(1))
                {
                    record.Findings.Add(finding);
                }
                else
                {
                    record.Categories.First(category => (int)category.Category == reader.GetInt32(1))
                        .Findings.Add(finding);
                }
            }
        }

        PrivacyDiagnosticRecords.Read(command, records);

        transaction.Commit();

        return records.Values.ToList();
    }

    internal static void Write(SqliteConnection connection, SqliteTransaction transaction,
        IEnumerable<PrivacyScanRecord> records)
    {
        using var command = connection.CreateCommand();

        command.Transaction = transaction;

        foreach (var record in records)
        {
            command.CommandText = PrivacySql.UpsertScan;

            command.Parameters.Clear();

            command.Parameters.AddWithValue(DatabaseNames.Key, PersistentIdentity.Key(record.CaptureId));

            command.Parameters.AddWithValue(DatabaseNames.Id, record.CaptureId);

            command.Parameters.AddWithValue(DatabaseNames.Request, record.RequestId);

            command.Parameters.AddWithValue(DatabaseNames.Profile, record.Profile);

            command.Parameters.AddWithValue(DatabaseNames.Hash, record.ImageHash);

            command.Parameters.AddWithValue(DatabaseNames.State, (int)record.State);

            command.Parameters.AddWithValue(DatabaseNames.Disposition, (int)record.Disposition);

            command.Parameters.AddWithValue(DatabaseNames.Failure, record.FailureCode);

            command.Parameters.AddWithValue(DatabaseNames.Updated, record.UpdatedUtc.Ticks);

            command.Parameters.AddWithValue(DatabaseNames.Attempts, record.Attempts);

            command.Parameters.AddWithValue(DatabaseNames.Duration, record.DurationSeconds);

            command.ExecuteNonQuery();

            command.CommandText = PrivacySql.ClearChildren;

            command.ExecuteNonQuery();

            WriteCategories(command, record);

            WriteFindings(command, record.CaptureId, null, record.Findings);

            foreach (var category in record.Categories)
            {
                WriteFindings(command, record.CaptureId, category.Category, category.Findings);

                PrivacyDiagnosticRecords.Write(command, record.CaptureId, category);
            }
        }
    }

    private static void WriteCategories(SqliteCommand command, PrivacyScanRecord record)
    {
        for (var position = 0; position < record.Categories.Count; position++)
        {
            var category = record.Categories[position];

            command.CommandText = PrivacySql.InsertCategory;

            command.Parameters.Clear();

            command.Parameters.AddWithValue(DatabaseNames.Key, PersistentIdentity.Key(record.CaptureId));

            command.Parameters.AddWithValue(DatabaseNames.Category, (int)category.Category);

            command.Parameters.AddWithValue(DatabaseNames.Position, position);

            command.Parameters.AddWithValue(DatabaseNames.Version, category.Version);

            command.Parameters.AddWithValue(DatabaseNames.State, (int)category.State);

            command.Parameters.AddWithValue(DatabaseNames.Failure, category.FailureCode);

            command.Parameters.AddWithValue(DatabaseNames.Updated, category.UpdatedUtc.Ticks);

            command.ExecuteNonQuery();
        }

        for (var position = 0; position < record.RequestedCategories.Count; position++)
        {
            command.CommandText = PrivacySql.InsertRequested;

            command.Parameters.Clear();

            command.Parameters.AddWithValue(DatabaseNames.Key, PersistentIdentity.Key(record.CaptureId));

            command.Parameters.AddWithValue(DatabaseNames.Category, (int)record.RequestedCategories[position]);

            command.Parameters.AddWithValue(DatabaseNames.Position, position);

            command.ExecuteNonQuery();
        }
    }

    private static void WriteFindings(SqliteCommand command, string capture, PrivacyScanCategory? scope,
        IReadOnlyList<PrivacyFinding> findings)
    {
        for (var position = 0; position < findings.Count; position++)
        {
            var finding = findings[position];

            command.CommandText = PrivacySql.InsertFinding;

            command.Parameters.Clear();

            command.Parameters.AddWithValue(DatabaseNames.Key, PersistentIdentity.Key(capture));

            command.Parameters.AddWithValue(DatabaseNames.Scope,
                scope.HasValue ? (object)(int)scope.Value : DBNull.Value);

            command.Parameters.AddWithValue(DatabaseNames.Position, position);

            command.Parameters.AddWithValue(DatabaseNames.Category, finding.Category);

            command.Parameters.AddWithValue(DatabaseNames.Severity, finding.Severity);

            command.Parameters.AddWithValue(DatabaseNames.Reason, finding.Reason);

            command.Parameters.AddWithValue(DatabaseNames.Regions, JsonSerializer.Serialize(finding.Regions));

            command.ExecuteNonQuery();
        }
    }
}
