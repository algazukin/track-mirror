// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0/.

using TrackMirror.Constants;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Storage;
using TrackMirror.Storage;
using TrackMirror.Storage.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Privacy.Storage;

public sealed class PrivacyDiagnosticStorageTests
{
    [Fact]
    public void Schema_two_upgrade_keeps_capture_flags_and_roundtrips_safe_request_metrics()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        using (var connection = new SqliteConnection(
            "Pooling=False;Data Source=" + Path.Combine(workspace.Root, DatabaseNames.FileName)))
        {
            connection.Open();

            SchemaMigrator.Apply(connection,
            [
                new SchemaMigration(1, Schema(DatabaseNames.InitialSchema)),
                new SchemaMigration(2, Schema(DatabaseNames.RetentionSchema))
            ]);

            using var command = connection.CreateCommand();

            command.CommandText = "INSERT INTO captures VALUES('FIXTURE/PHOTO.JPG','fixture/photo.jpg'," +
                "'fixture','photo.jpg',1,1,1,1);";

            command.ExecuteNonQuery();
        }

        // Act
        var database = new ViewerDatabase(workspace.Root);

        // Arrange
        var diagnostic = new PrivacyRequestDiagnostic
        {
            RequestBytes = 100, OcrBlocks = 2, OcrCharacters = 12, PromptTokens = 37,
            ReplyTokens = null, DurationMilliseconds = 2.5, FailureCode = PrivacyNames.ModelContext
        };

        var record = new PrivacyScanRecord
        {
            CaptureId = "fixture/photo.jpg", RequestId = "request", Profile = "profile",
            State = PrivacyScanState.Partial,
            Categories = [new PrivacyCategoryResult
            {
                Category = PrivacyScanCategory.ConfidentialData, State = PrivacyScanState.Partial,
                Diagnostics = [diagnostic]
            }]
        };

        var store = new PrivacyResultStore(workspace.Root, new HashSet<string> { record.CaptureId });

        // Act
        store.Put([record]);

        var loaded = new PrivacyResultStore(workspace.Root, new HashSet<string> { record.CaptureId });

        // Assert
        Assert.Equal(diagnostic, Assert.Single(Assert.Single(loaded.Snapshot()[record.CaptureId].Categories).Diagnostics));

        using var read = database.Open();

        using var query = read.CreateCommand();

        query.CommandText = "PRAGMA user_version;";

        Assert.Equal(3L, query.ExecuteScalar());

        query.CommandText = "SELECT viewed+flagged FROM captures;";

        Assert.Equal(2L, query.ExecuteScalar());

        // Arrange
        query.CommandText = "DELETE FROM captures; SELECT COUNT(*) FROM privacy_request_diagnostics;";

        // Act
        // Assert
        Assert.Equal(0L, query.ExecuteScalar());
    }

    private static string Schema(string name)
    {
        using var resource = typeof(ScreenshotStore).Assembly.GetManifestResourceStream(name)!;

        using var reader = new StreamReader(resource);

        return reader.ReadToEnd();
    }
}
