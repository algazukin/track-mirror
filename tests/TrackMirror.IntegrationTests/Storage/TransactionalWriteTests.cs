// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Text.Json;
using Microsoft.Data.Sqlite;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Storage;
using TrackMirror.Storage;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Storage;

public sealed class TransactionalWriteTests
{
    [Fact]
    public void Storage_operations_refuse_pending_marks_until_the_database_can_save_them()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture(flagged: false);

        workspace.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(workspace.Root);

        using var connection = new SqliteConnection(
            "Pooling=False;Data Source=" + Path.Combine(workspace.Root, "viewer.db"));

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            CREATE TRIGGER reject_marks BEFORE UPDATE ON captures
            BEGIN SELECT RAISE(ABORT, 'synthetic storage failure'); END;
            """;

        command.ExecuteNonQuery();

        // Act
        store.ToggleFlag(capture.Id);

        // Assert
        Assert.Throws<IOException>(store.FlushPending);

        Assert.True(Assert.Single(store.Snapshot()).Flagged);

        Assert.False(Assert.Single(new ScreenshotStore(workspace.Root).Snapshot()).Flagged);

        // Act
        command.CommandText = "DROP TRIGGER reject_marks;";

        command.ExecuteNonQuery();

        store.FlushPending();

        // Assert
        Assert.True(Assert.Single(new ScreenshotStore(workspace.Root).Snapshot()).Flagged);
    }

    [Fact]
    public void Locked_database_reports_failure_and_saves_pending_flags_after_lock_release()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture();

        workspace.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(workspace.Root);

        string? warning = null;

        store.Warning += value => warning = value;

        using (var connection = new SqliteConnection(
            "Pooling=False;Data Source=" + Path.Combine(workspace.Root, "viewer.db")
        ))
        {
            connection.Open();

            using var transaction = connection.BeginTransaction();

            // Act
            store.ToggleFlag(capture.Id);

            // Assert
            Assert.NotNull(warning);

            using var command = connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText = "SELECT flagged FROM captures;";

            Assert.Equal(1L, command.ExecuteScalar());
        }

        // Act
        store.MarkViewed(capture.Id);

        // Assert
        var reopened = Assert.Single(new ScreenshotStore(workspace.Root).Snapshot());

        Assert.True(reopened.Viewed);

        Assert.False(reopened.Flagged);
    }

    [Fact]
    public void Failed_AI_checkpoint_rolls_back_parent_categories_and_findings_then_retries()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture();

        workspace.Json("index.json", new[] { capture });

        _ = new ScreenshotStore(workspace.Root);

        var store = new PrivacyResultStore(workspace.Root, new HashSet<string> { capture.Id });

        var record = new PrivacyScanRecord
        {
            CaptureId = capture.Id,
            RequestId = "checkpoint",
            Profile = "profile",
            ImageHash = "hash",
            State = PrivacyScanState.Completed,
            Categories = [new PrivacyCategoryResult
            {
                Category = PrivacyScanCategory.Game,
                State = PrivacyScanState.Completed,
                Version = "v1"
            }

            ]
        };

        store.Put([record]);

        using var connection = new SqliteConnection(
            "Pooling=False;Data Source=" + Path.Combine(workspace.Root, "viewer.db")
        );

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            CREATE TRIGGER reject_checkpoint BEFORE INSERT ON privacy_categories
            BEGIN SELECT RAISE(ABORT, 'synthetic storage failure'); END;
            """;

        command.ExecuteNonQuery();

        var changed = record with
        {
            State = PrivacyScanState.Scanning,
            Categories = [new PrivacyCategoryResult
            {
                Category = PrivacyScanCategory.PersonalChat,
                State = PrivacyScanState.Completed,
                Version = "v2"
            }

            ]
        };

        // Act
        // Assert
        Assert.Throws<IOException>(() => store.Put([changed]));

        Assert.Equal(JsonSerializer.Serialize(record), JsonSerializer.Serialize(store.Snapshot()[capture.Id]));

        Assert.Equal(
            JsonSerializer.Serialize(record),
            JsonSerializer.Serialize(
                new PrivacyResultStore(workspace.Root, new HashSet<string> { capture.Id }).Snapshot()[capture.Id]
            )
        );

        // Act
        command.CommandText = "DROP TRIGGER reject_checkpoint;";

        command.ExecuteNonQuery();

        store.Put([changed with { State = PrivacyScanState.Completed }]);

        // Assert
        Assert.Equal(
            PrivacyScanCategory.PersonalChat,
            Assert.Single(
                new PrivacyResultStore(workspace.Root, new HashSet<string> { capture.Id })
                    .Snapshot()[capture.Id].Categories
            ).Category
        );
    }

    [Fact]
    public void Failed_flag_write_is_reported_and_retried_without_losing_another_change()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture();

        workspace.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(workspace.Root);

        string? warning = null;

        store.Warning += value => warning = value;

        using var connection = new SqliteConnection(
            "Pooling=False;Data Source=" + Path.Combine(workspace.Root, "viewer.db")
        );

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            CREATE TRIGGER reject_capture BEFORE UPDATE ON captures
            BEGIN SELECT RAISE(ABORT, 'synthetic storage failure'); END;
            """;

        command.ExecuteNonQuery();

        // Act
        store.ToggleFlag(capture.Id);

        // Assert
        Assert.NotNull(warning);

        Assert.True(Assert.Single(new ScreenshotStore(workspace.Root).Snapshot()).Flagged);

        // Act
        command.CommandText = "DROP TRIGGER reject_capture;";

        command.ExecuteNonQuery();

        store.MarkViewed(capture.Id);

        // Assert
        var reopened = Assert.Single(new ScreenshotStore(workspace.Root).Snapshot());

        Assert.False(reopened.Flagged);

        Assert.True(reopened.Viewed);
    }

    [Fact]
    public async Task Concurrent_capture_activity_and_AI_writers_preserve_all_committed_records()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture();

        workspace.Json("index.json", new[] { capture });

        var captures = new ScreenshotStore(workspace.Root);

        var activity = new HubstaffActivityStore(workspace.Root);

        var privacy = new PrivacyResultStore(workspace.Root, new HashSet<string> { capture.Id });

        var start = capture.CapturedUtc;

        // Act
        await Task.WhenAll(Task.Run(() =>
        {
            for (var index = 0; index < 20; index++)
            {
                captures.ToggleFlag(capture.Id);
            }

            captures.MarkViewed(capture.Id);
        }), Task.Run(() =>
        {
            for (var index = 0; index < 20; index++)
            {
                activity.Merge(
                    [new HubstaffActivity(
                        "fixture",
                        start.AddMinutes(index * 10),
                        300,
                        100,
                        100,
                        150,
                        start.AddMinutes(index * 10
                        + 5)
                    )]
                );

                activity.Save();
            }
        }), Task.Run(() =>
        {
            for (var index = 0; index < 20; index++)
            {
                privacy.Put(
                    [new PrivacyScanRecord { CaptureId = capture.Id, RequestId = "job-"
                        + index, Profile = "profile", State = PrivacyScanState.Completed, ImageHash = "hash" }]
                );
            }
        }));

        // Assert
        var current = Assert.Single(new ScreenshotStore(workspace.Root).Snapshot());

        Assert.True(current.Viewed);

        Assert.True(current.Flagged);

        Assert.Equal(20, new HubstaffActivityStore(workspace.Root).Snapshot().Count);

        Assert.Equal(
            "job-19",
            Assert.Single(
                new PrivacyResultStore(workspace.Root, new HashSet<string> { capture.Id }).Snapshot()
            ).Value.RequestId
        );
    }
}
