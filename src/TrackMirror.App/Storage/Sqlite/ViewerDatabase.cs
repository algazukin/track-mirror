// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using Microsoft.Data.Sqlite;
using TrackMirror.Constants;

namespace TrackMirror.Storage.Sqlite;

internal sealed class ViewerDatabase
{
    private readonly string _connectionString;

    internal string DataRoot { get; }

    internal string? InitialWarning { get; }

    internal ViewerDatabase(string dataRoot)
    {
        DataRoot = Path.GetFullPath(dataRoot);

        Directory.CreateDirectory(DataRoot);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(DataRoot, DatabaseNames.FileName),
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString();

        using var connection = Open();

        try
        {
            using var command = connection.CreateCommand();

            command.CommandText = DatabaseSql.Wal;

            command.ExecuteNonQuery();

            SchemaMigrator.Apply(connection,
            [
                new SchemaMigration(1, ReadSchema(DatabaseNames.InitialSchema)),
                new SchemaMigration(2, ReadSchema(DatabaseNames.RetentionSchema)),
                new SchemaMigration(3, ReadSchema(DatabaseNames.PrivacyDiagnosticsSchema))
            ]);

            LegacyImporter.Import(this, connection);

            command.CommandText = DatabaseSql.ImportWarning;

            var skipped = Convert.ToInt32(command.ExecuteScalar());

            InitialWarning = skipped > 0 ? string.Format(DatabaseText.SkippedFormat, skipped) : null;
        }
        catch (SqliteException ex)
        {
            throw new IOException(DatabaseText.Unavailable, ex);
        }
    }

    private static string ReadSchema(string name)
    {
        using var resource = typeof(ViewerDatabase).Assembly.GetManifestResourceStream(name)!;

        using var reader = new StreamReader(resource);

        return reader.ReadToEnd();
    }

    internal SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);

        try
        {
            connection.Open();

            return connection;
        }
        catch (SqliteException ex)
        {
            connection.Dispose();

            throw new IOException(DatabaseText.Unavailable, ex);
        }
    }

    internal void Write(Action<SqliteConnection, SqliteTransaction> action)
    {
        using var connection = Open();

        try
        {
            using var transaction = connection.BeginTransaction();

            action(connection, transaction);

            transaction.Commit();
        }
        catch (SqliteException ex)
        {
            throw new IOException(DatabaseText.WriteFailed, ex);
        }
    }
}
