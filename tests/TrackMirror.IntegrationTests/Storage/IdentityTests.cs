// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Models;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Storage;
using TrackMirror.Storage;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Storage;

public sealed class IdentityTests
{
    [Fact]
    public void Distinct_ordinal_identifiers_keep_separate_captures_activity_and_AI_results()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var first = workspace.Capture("first.jpg")with
        {
            Id = "fixture/ſ.jpg"
        };

        var second = workspace.Capture("second.jpg", viewed: true, flagged: false)with
        {
            Id = "fixture/S.jpg"
        };

        Assert.False(StringComparer.OrdinalIgnoreCase.Equals(first.Id, second.Id));

        workspace.Json("index.json", new[] { first, second });

        var started = first.CapturedUtc;

        var activities = new[]
        {
            new HubstaffActivity("ſ", started, 300, 100, 50, 120, started),
            new HubstaffActivity("S", started, 300, 200, 50, 220, started)
        };

        workspace.Json("hubstaff-activity-history.json", activities);

        var scans = new[]
        {
            new PrivacyScanRecord
            {
                CaptureId = first.Id,
                RequestId = "first",
                Profile = "fixture",
                State = PrivacyScanState.Completed
            },
            new PrivacyScanRecord
            {
                CaptureId = second.Id,
                RequestId = "second",
                Profile = "fixture",
                State = PrivacyScanState.Completed
            }
        };

        workspace.Json("privacy-results.json", scans);

        // Act
        var captures = new ScreenshotStore(workspace.Root).Snapshot();

        // Assert
        Assert.Equal(2, captures.Count);

        Assert.Contains(first, captures);

        Assert.Contains(second, captures);

        // Act
        var activityStore = new HubstaffActivityStore(workspace.Root);

        // Assert
        Assert.Equal(2, activityStore.Snapshot().Count);

        // Act
        activityStore.Merge(
            activities.Select(entry => entry with { TrackedSeconds = 400, ObservedUtc = started.AddMinutes(10) })
        );

        activityStore.Save();

        // Assert
        Assert.All(
            new HubstaffActivityStore(workspace.Root).Snapshot(),
            entry => Assert.Equal(400, entry.TrackedSeconds)
        );

        // Act
        var results = new PrivacyResultStore(workspace.Root, new HashSet<string> { first.Id, second.Id }).Snapshot();

        // Assert
        Assert.Equal(2, results.Count);

        Assert.Equal("first", results[first.Id].RequestId);

        Assert.Equal("second", results[second.Id].RequestId);
    }
}
