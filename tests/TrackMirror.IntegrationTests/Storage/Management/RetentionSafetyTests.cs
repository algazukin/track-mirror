// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.Models;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Storage;
using TrackMirror.Storage;
using TrackMirror.Storage.Management;
using Microsoft.Data.Sqlite;
using Xunit;

namespace TrackMirror.IntegrationTests.Storage.Management;

public sealed class RetentionSafetyTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Flagged_and_unread_protections_are_independent_and_cleanup_is_idempotent(
        bool keepFlagged, bool keepUnread)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

        var entries = (from viewed in new[] { false, true }
            from flagged in new[] { false, true }
            select workspace.Capture($"viewed-{viewed}-flagged-{flagged}.jpg", viewed, flagged)
                with { CapturedUtc = now.AddDays(-31) }).ToArray();

        workspace.Json("index.json", entries);

        var store = new ScreenshotStore(workspace.Root);

        var settings = new AppSettings { RetentionKeepFlagged = keepFlagged, RetentionKeepUnread = keepUnread };

        var protectedEntries = entries.Where(entry => keepFlagged && entry.Flagged || keepUnread && !entry.Viewed)
            .OrderBy(entry => entry.Id).ToArray();

        var retention = new CaptureRetention(workspace.Root);

        // Act
        var result = await retention.PurgeAsync(settings, now, default);

        var repeated = await retention.PurgeAsync(settings, now, default);

        // Assert
        Assert.Equal(new RetentionResult(entries.Length - protectedEntries.Length, 0), result);

        Assert.Equal(new RetentionResult(0, 0), repeated);

        Assert.Equal(protectedEntries, new ScreenshotStore(workspace.Root).Snapshot().OrderBy(entry => entry.Id));

        foreach (var entry in entries)
        {
            Assert.Equal(protectedEntries.Contains(entry), File.Exists(store.CopyPath(entry)));
        }
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(5, false)]
    public async Task Only_queued_and_running_AI_jobs_protect_expired_captures(int state, bool keep)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

        var capture = workspace.Capture(viewed: true, flagged: false) with { CapturedUtc = now.AddDays(-31) };

        workspace.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(workspace.Root);

        new PrivacyResultStore(workspace.Root, new HashSet<string> { capture.Id }).Put([
            new PrivacyScanRecord
            {
                CaptureId = capture.Id, RequestId = "retention-job", Profile = "fixture",
                State = (PrivacyScanState)state
            }
        ]);

        // Act
        var result = await new CaptureRetention(workspace.Root).PurgeAsync(new AppSettings(), now, default);

        // Assert
        Assert.Equal(new RetentionResult(keep ? 0 : 1, 0), result);

        Assert.Equal(keep, File.Exists(store.CopyPath(capture)));

        Assert.Equal(keep ? 1 : 0, new ScreenshotStore(workspace.Root).Snapshot().Count);

        using var connection = new SqliteConnection(
            "Pooling=False;Data Source=" + Path.Combine(workspace.Root, "viewer.db"));

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM privacy_scans;";

        Assert.Equal(keep ? 1L : 0L, command.ExecuteScalar());
    }

    [Fact]
    public async Task Failure_after_the_first_deletion_intent_rolls_back_the_entire_batch_and_can_be_retried()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

        var captures = new[]
        {
            workspace.Capture("first.jpg", true, false), workspace.Capture("second.jpg", true, false)
        }
            .Select(entry => entry with { CapturedUtc = now.AddDays(-31) }).ToArray();

        workspace.Json("index.json", captures);

        var store = new ScreenshotStore(workspace.Root);

        using var connection = new SqliteConnection(
            "Pooling=False;Data Source=" + Path.Combine(workspace.Root, "viewer.db"));

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            CREATE TRIGGER reject_second_intent BEFORE INSERT ON capture_deletions
            WHEN (SELECT COUNT(*) FROM capture_deletions)>0
            BEGIN SELECT RAISE(ABORT, 'synthetic retention failure'); END;
            """;

        command.ExecuteNonQuery();

        var retention = new CaptureRetention(workspace.Root);

        // Act
        // Assert
        await Assert.ThrowsAsync<IOException>(() => retention.PurgeAsync(new AppSettings(), now, default));

        Assert.Equal(2, new ScreenshotStore(workspace.Root).Snapshot().Count);

        Assert.All(captures, capture => Assert.True(File.Exists(store.CopyPath(capture))));

        Assert.Empty(retention.Deleted());

        // Act
        command.CommandText = "DROP TRIGGER reject_second_intent;";

        command.ExecuteNonQuery();

        var result = await retention.PurgeAsync(new AppSettings(), now, default);

        // Assert
        Assert.Equal(new RetentionResult(2, 0), result);

        Assert.Empty(new ScreenshotStore(workspace.Root).Snapshot());

        Assert.All(captures, capture => Assert.False(File.Exists(store.CopyPath(capture))));
    }

    [Fact]
    public async Task Cancellation_before_cleanup_changes_neither_catalog_nor_files()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture(viewed: true, flagged: false);

        workspace.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(workspace.Root);

        var retention = new CaptureRetention(workspace.Root);

        using var cancellation = new CancellationTokenSource();

        cancellation.Cancel();

        // Act
        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            retention.PurgeAsync(new AppSettings(), capture.CapturedUtc.AddDays(60), cancellation.Token));

        Assert.Equal(capture, Assert.Single(new ScreenshotStore(workspace.Root).Snapshot()));

        Assert.True(File.Exists(store.CopyPath(capture)));

        Assert.Empty(retention.Deleted());
    }
}
