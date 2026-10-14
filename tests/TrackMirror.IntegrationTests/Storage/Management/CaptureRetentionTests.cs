// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Models;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Storage;
using TrackMirror.Storage;
using TrackMirror.Storage.Management;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Storage.Management;

public sealed class CaptureRetentionTests
{
    [Fact]
    public async Task Cleanup_protects_marks_unread_and_queued_jobs_and_removes_only_expired_captures()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var now = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

        var expired = workspace.Capture("expired.jpg", viewed: true, flagged: false)
            with { CapturedUtc = now.AddDays(-31) };

        var flagged = workspace.Capture("flagged.jpg", viewed: true)
            with { CapturedUtc = now.AddDays(-31) };

        var unread = workspace.Capture("unread.jpg", flagged: false)
            with { CapturedUtc = now.AddDays(-31) };

        var boundary = workspace.Capture("boundary.jpg", viewed: true, flagged: false)
            with { CapturedUtc = now.AddDays(-30) };

        var queued = workspace.Capture("queued.jpg", viewed: true, flagged: false)
            with { CapturedUtc = now.AddDays(-31) };

        var recent = workspace.Capture("recent.jpg", viewed: true, flagged: false)
            with { CapturedUtc = now.AddDays(-1) };

        workspace.Json("index.json", new[] { expired, flagged, unread, boundary, queued, recent });

        _ = new ScreenshotStore(workspace.Root);

        var privacy = new PrivacyResultStore(workspace.Root, new HashSet<string> { expired.Id, queued.Id });

        privacy.Put([new PrivacyScanRecord { CaptureId = expired.Id, RequestId = "done", Profile = "fixture",
            State = PrivacyScanState.Completed }, new PrivacyScanRecord { CaptureId = queued.Id,
            RequestId = "queued", Profile = "fixture", State = PrivacyScanState.Queued }]);

        var settings = new AppSettings { RetentionDays = 30 };

        var retention = new CaptureRetention(workspace.Root);

        // Act
        // Assert
        Assert.Equal(expired, Assert.Single(retention.Candidates(settings, now)));

        // Act
        // Assert
        Assert.Equal(new RetentionResult(1, 0), await retention.PurgeAsync(settings, now, default));

        var reopened = new ScreenshotStore(workspace.Root);

        Assert.Equal(5, reopened.Snapshot().Count);

        Assert.True(reopened.Contains(expired.Account + "/" + expired.FileName));

        Assert.False(File.Exists(reopened.CopyPath(expired)));

        Assert.Single(new PrivacyResultStore(workspace.Root, new HashSet<string> { queued.Id }).Snapshot());

        Assert.Equal(2, retention.Candidates(settings with
        {
            RetentionKeepFlagged = false, RetentionKeepUnread = false
        }, now).Count);
    }

    [Fact]
    public async Task Locked_file_keeps_a_durable_deletion_intent_that_retries_after_restart()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var now = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

        var capture = workspace.Capture("expired.jpg", viewed: true, flagged: false)
            with { CapturedUtc = now.AddDays(-31) };

        workspace.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(workspace.Root);

        var path = store.CopyPath(capture);

        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            // Act
            var result = await new CaptureRetention(workspace.Root).PurgeAsync(new AppSettings(), now, default);

            // Assert
            Assert.Equal(new RetentionResult(1, 1), result);

            Assert.Empty(new ScreenshotStore(workspace.Root).Snapshot());

            Assert.True(File.Exists(path));
        }

        // Act
        // Assert
        Assert.Empty(new ScreenshotStore(workspace.Root).Snapshot());

        Assert.False(File.Exists(path));
    }
}
