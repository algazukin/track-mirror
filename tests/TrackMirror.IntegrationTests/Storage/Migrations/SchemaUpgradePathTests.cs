// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Storage;
using TrackMirror.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace TrackMirror.IntegrationTests.Storage.Migrations;

public sealed class SchemaUpgradePathTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Every_released_schema_upgrades_once_and_preserves_capture_marks_and_queued_categories(int version)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture(viewed: true);

        var schemaNames = new[]
        {
            DatabaseNames.InitialSchema, DatabaseNames.RetentionSchema, DatabaseNames.PrivacyDiagnosticsSchema
        };

        using var connection = new SqliteConnection(
            "Pooling=False;Data Source=" + Path.Combine(workspace.Root, "viewer.db"));

        connection.Open();

        using var command = connection.CreateCommand();

        for (var index = 0; index < version; index++)
        {
            using var resource = typeof(ScreenshotStore).Assembly.GetManifestResourceStream(schemaNames[index])!;

            using var reader = new StreamReader(resource);

            command.CommandText = reader.ReadToEnd() +
                $" INSERT INTO schema_migrations VALUES({index + 1},1); PRAGMA user_version={index + 1};";

            command.ExecuteNonQuery();
        }

        command.CommandText = """
            INSERT INTO legacy_import VALUES(1,1,0);
            INSERT INTO captures VALUES('STABLE-ID','stable-id','fixture','2026-10-13T103000-0.jpg',1,2,1,1);
            INSERT INTO privacy_scans VALUES('STABLE-ID','stable-id','keep-job','fixture','hash',0,0,'',1,0,0);
            INSERT INTO privacy_requested_categories VALUES('STABLE-ID',2,0);
            """;

        command.ExecuteNonQuery();

        // Act
        _ = new ScreenshotStore(workspace.Root);

        var reopened = new ScreenshotStore(workspace.Root);

        var scan = new PrivacyResultStore(workspace.Root, new HashSet<string> { "stable-id" }).Snapshot()["stable-id"];

        // Assert
        var current = Assert.Single(reopened.Snapshot());

        Assert.Equal("stable-id", current.Id);

        Assert.Equal(capture.FileName, current.FileName);

        Assert.True(current.Viewed);

        Assert.True(current.Flagged);

        Assert.Equal("keep-job", scan.RequestId);

        Assert.Equal(PrivacyScanState.Queued, scan.State);

        Assert.Equal(PrivacyScanCategory.ConfidentialData, Assert.Single(scan.RequestedCategories));

        command.CommandText = "PRAGMA user_version;";

        Assert.Equal(3L, command.ExecuteScalar());

        command.CommandText = "SELECT COUNT(*) FROM schema_migrations;";

        Assert.Equal(3L, command.ExecuteScalar());

        command.CommandText = "PRAGMA foreign_key_check;";

        Assert.Null(command.ExecuteScalar());
    }
}
