// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using Microsoft.Data.Sqlite;
using TrackMirror.Storage;
using TrackMirror.Storage.Sqlite;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Storage.Migrations;

public sealed class SchemaMigrationTests
{
    [Fact]
    public void Failed_schema_upgrade_is_atomic_and_successful_retry_is_versioned()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        _ = new ScreenshotStore(workspace.Root);

        using var connection = new SqliteConnection(
            "Pooling=False;Data Source=" + Path.Combine(workspace.Root, "viewer.db")
        );

        connection.Open();

        var original = new SchemaMigration(1, string.Empty);

        // Act
        // Assert
        Assert.Throws<SqliteException>(
            () => SchemaMigrator.Apply(
                connection,
                [original, new SchemaMigration(4,
                    "CREATE TABLE upgrade_probe(id INTEGER); INSERT INTO absent VALUES(1);")]
            )
        );

        using var command = connection.CreateCommand();

        command.CommandText = "PRAGMA user_version;";

        Assert.Equal(3L, command.ExecuteScalar());

        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name='upgrade_probe';";

        Assert.Equal(0L, command.ExecuteScalar());

        // Act
        SchemaMigrator.Apply(connection, [original, new SchemaMigration(4, "CREATE TABLE upgrade_probe(id INTEGER);")]);

        command.CommandText = "SELECT version FROM schema_migrations ORDER BY version;";

        using var reader = command.ExecuteReader();

        // Assert
        Assert.True(reader.Read());

        Assert.Equal(1, reader.GetInt32(0));

        Assert.True(reader.Read());

        Assert.Equal(2, reader.GetInt32(0));

        Assert.True(reader.Read());

        Assert.Equal(3, reader.GetInt32(0));

        Assert.True(reader.Read());

        Assert.Equal(4, reader.GetInt32(0));
    }

    [Fact]
    public void Newer_database_is_rejected_without_resetting_its_version_or_data()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture();

        workspace.Json("index.json", new[] { capture });

        _ = new ScreenshotStore(workspace.Root);

        using var connection = new SqliteConnection(
            "Pooling=False;Data Source=" + Path.Combine(workspace.Root, "viewer.db")
        );

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = "PRAGMA user_version=99;";

        command.ExecuteNonQuery();

        // Act
        // Assert
        Assert.Throws<InvalidDataException>(() => new ScreenshotStore(workspace.Root));

        command.CommandText = "SELECT id FROM captures;";

        Assert.Equal(capture.Id, command.ExecuteScalar());

        command.CommandText = "PRAGMA user_version;";

        Assert.Equal(99L, command.ExecuteScalar());
    }

    [Fact]
    public void Corrupt_database_is_reported_and_never_replaced_with_empty_history()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var path = Path.Combine(workspace.Root, "viewer.db");

        var original = new byte[]
        {
            1,
            2,
            3,
            4,
            5
        };

        File.WriteAllBytes(path, original);

        // Act
        // Assert
        Assert.Throws<IOException>(() => new ScreenshotStore(workspace.Root));

        Assert.Equal(original, File.ReadAllBytes(path));
    }
}
