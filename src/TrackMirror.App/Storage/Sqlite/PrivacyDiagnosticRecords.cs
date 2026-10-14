// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0/.

using Microsoft.Data.Sqlite;
using TrackMirror.Constants;
using TrackMirror.Privacy.Models;

namespace TrackMirror.Storage.Sqlite;

internal static class PrivacyDiagnosticRecords
{
    internal static void Read(SqliteCommand command, Dictionary<string, PrivacyScanRecord> records)
    {
        command.CommandText = PrivacyDiagnosticsSql.Select;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var category = records[reader.GetString(0)].Categories
                .First(value => (int)value.Category == reader.GetInt32(1));

            category.Diagnostics.Add(new PrivacyRequestDiagnostic
            {
                RequestBytes = reader.GetInt32(2), OcrBlocks = reader.GetInt32(3), OcrCharacters = reader.GetInt32(4),
                PromptTokens = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                ReplyTokens = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                DurationMilliseconds = reader.GetDouble(7), FailureCode = reader.GetString(8),
                RequestId = Guid.Parse(reader.GetString(9))
            });
        }
    }

    internal static void Write(SqliteCommand command, string capture, PrivacyCategoryResult category)
    {
        for (var position = 0; position < category.Diagnostics.Count; position++)
        {
            var diagnostic = category.Diagnostics[position];

            command.CommandText = PrivacyDiagnosticsSql.Insert;

            command.Parameters.Clear();

            command.Parameters.AddWithValue(DatabaseNames.Key, PersistentIdentity.Key(capture));
            command.Parameters.AddWithValue(DatabaseNames.Category, (int)category.Category);
            command.Parameters.AddWithValue(DatabaseNames.Position, position);
            command.Parameters.AddWithValue(PrivacyDiagnosticsSql.RequestBytes, diagnostic.RequestBytes);
            command.Parameters.AddWithValue(PrivacyDiagnosticsSql.Blocks, diagnostic.OcrBlocks);
            command.Parameters.AddWithValue(PrivacyDiagnosticsSql.Characters, diagnostic.OcrCharacters);
            command.Parameters.AddWithValue(PrivacyDiagnosticsSql.PromptTokens,
                (object?)diagnostic.PromptTokens ?? DBNull.Value);
            command.Parameters.AddWithValue(PrivacyDiagnosticsSql.ReplyTokens,
                (object?)diagnostic.ReplyTokens ?? DBNull.Value);
            command.Parameters.AddWithValue(DatabaseNames.Duration, diagnostic.DurationMilliseconds);
            command.Parameters.AddWithValue(DatabaseNames.Failure, diagnostic.FailureCode);
            command.Parameters.AddWithValue(DatabaseNames.Request, diagnostic.RequestId.ToString());

            command.ExecuteNonQuery();
        }
    }
}
