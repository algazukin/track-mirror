// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Globalization;
using Microsoft.Data.Sqlite;
using TrackMirror.Constants;

namespace TrackMirror.Storage.Sqlite;

internal static class SchemaMigrator
{
    internal static void Apply(SqliteConnection connection, IReadOnlyList<SchemaMigration> migrations)
    {
        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText = DatabaseSql.ReadVersion;

        var current = Convert.ToInt32(command.ExecuteScalar());

        if (current > migrations.Max(migration => migration.Version))
            throw new InvalidDataException(DatabaseText.NewerSchema);

        foreach (var migration in migrations.OrderBy(migration => migration.Version))
        {
            if (migration.Version <= current)
                continue;

            if (migration.Version != current + 1)
                throw new InvalidDataException(DatabaseText.MigrationOrder);

            command.CommandText = migration.Sql;

            command.ExecuteNonQuery();

            command.CommandText = DatabaseSql.StampVersion;

            command.Parameters.AddWithValue(DatabaseNames.Version, migration.Version);

            command.Parameters.AddWithValue(DatabaseNames.Updated, DateTime.UtcNow.Ticks);

            command.ExecuteNonQuery();

            command.Parameters.Clear();

            command.CommandText = DatabaseSql.SetVersion + migration.Version.ToString(CultureInfo.InvariantCulture);

            command.ExecuteNonQuery();

            current = migration.Version;
        }

        transaction.Commit();
    }
}
