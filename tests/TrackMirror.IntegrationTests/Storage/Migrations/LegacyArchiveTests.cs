// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Text.Json;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Storage;
using TrackMirror.Storage;
using Microsoft.Data.Sqlite;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Storage.Migrations;

public sealed class LegacyArchiveTests
{
    [Fact]
    public void Observation_only_cache_update_keeps_original_receive_time_and_accepts_intermediate_changes()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var started = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);

        var original = new HubstaffActivity("fixture", started, 300, 100, 100, 150, started.AddMinutes(5));

        workspace.Json("hubstaff-activity-history.json", new[] { original });

        workspace.Json("hubstaff-activity.json", new[] { original with { ObservedUtc = started.AddMinutes(8) } });

        // Act
        var store = new HubstaffActivityStore(workspace.Root);

        // Assert
        Assert.Equal(original, Assert.Single(store.Snapshot()));

        var changed = original with
        {
            MouseSeconds = 110,
            OverallSeconds = 160,
            ObservedUtc = started.AddMinutes(6)
        };

        // Act
        // Assert
        Assert.Equal(changed, Assert.Single(store.Merge([changed])));

        // Act
        store.Save();

        // Assert
        Assert.Equal(changed, Assert.Single(new HubstaffActivityStore(workspace.Root).Snapshot()));
    }

    [Fact]
    public void Import_preserves_activity_AI_categories_findings_decisions_and_identifiers()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture("Проверка-0.jpg");

        workspace.Json("index.json", new[] { capture });

        var activity = new HubstaffActivity("fixture", capture.CapturedUtc, 300, 180, 90, 240, capture.SavedUtc);

        workspace.Json("hubstaff-activity-history.json", new[] { activity });

        workspace.Json("hubstaff-activity.json", new[] { activity with { TrackedSeconds = 100 } });

        var finding = new PrivacyFinding(PrivacyRules.Game, PrivacyRules.Medium, PrivacyRules.ContextReason, []);

        var record = new PrivacyScanRecord
        {
            CaptureId = capture.Id,
            RequestId = "original-request",
            Profile = "original-profile",
            ImageHash = "original-hash",
            State = PrivacyScanState.Partial,
            Disposition = PrivacyDisposition.FalsePositive,
            Attempts = 1,
            DurationSeconds = 1.25,
            UpdatedUtc = capture.SavedUtc,
            FailureCode = PrivacyNames.ModelTimeout,
            Findings = [finding],
            RequestedCategories = [PrivacyScanCategory.PersonalChat],
            Categories = [new PrivacyCategoryResult
            {
                Category = PrivacyScanCategory.Game,
                Version = "original-version",
                State = PrivacyScanState.Completed,
                Findings = [finding],
                UpdatedUtc = capture.SavedUtc
            }, new PrivacyCategoryResult
            {
                Category = PrivacyScanCategory.ConfidentialData,
                Version = "other-version",
                State = PrivacyScanState.Partial,
                FailureCode = PrivacyNames.ModelTimeout,
                UpdatedUtc = capture.SavedUtc
            }

            ]
        };

        workspace.Json("privacy-results.json", new[] { record });

        var hashes = Directory.GetFiles(
            workspace.Root,
            "*.json"
        ).ToDictionary(
            path => Path.GetFileName(path)!,
            path => File.ReadAllBytes(path)
        );

        // Act
        var captures = new ScreenshotStore(workspace.Root);

        // Assert
        Assert.Equal(capture, Assert.Single(captures.Snapshot()));

        Assert.Equal(activity, Assert.Single(new HubstaffActivityStore(workspace.Root).Snapshot()));

        var privacy = new PrivacyResultStore(workspace.Root, new HashSet<string> { capture.Id });

        Assert.Equal(
            JsonSerializer.Serialize(record),
            JsonSerializer.Serialize(Assert.Single(privacy.Snapshot()).Value)
        );

        // Act
        privacy.Put([record with { Disposition = PrivacyDisposition.Acknowledged }]);

        var reopened = new PrivacyResultStore(workspace.Root, new HashSet<string> { capture.Id });

        // Assert
        Assert.Equal(PrivacyDisposition.Acknowledged, reopened.Snapshot()[capture.Id].Disposition);

        foreach (var original in hashes)
        {
            Assert.Equal(original.Value, File.ReadAllBytes(Path.Combine(workspace.Root, original.Key!)));
        }
    }

    [Fact]
    public void Failed_import_rolls_back_all_rows_and_retries_after_source_repair()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture();

        workspace.Json("index.json", new[] { capture });

        File.WriteAllText(Path.Combine(workspace.Root, "privacy-results.json"), "{incomplete");

        // Act
        // Assert
        var error = Assert.Throws<IOException>(() => new ScreenshotStore(workspace.Root));

        Assert.Contains("privacy-results.json", error.Message);

        using (var connection = new SqliteConnection(
            "Pooling=False;Data Source=" + Path.Combine(workspace.Root, "viewer.db")
        ))
        {
            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = "SELECT (SELECT COUNT(*) FROM captures) + (SELECT COUNT(*) FROM legacy_import);";

            Assert.Equal(0L, command.ExecuteScalar());
        }

        // Act
        workspace.Json("privacy-results.json", Array.Empty<PrivacyScanRecord>());

        // Act
        // Assert
        Assert.Equal(capture, Assert.Single(new ScreenshotStore(workspace.Root).Snapshot()));
    }

    [Fact]
    public void Completed_import_does_not_overwrite_database_from_edited_old_JSON()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture();

        workspace.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(workspace.Root);

        // Act
        store.MarkViewed(capture.Id);

        store.ToggleFlag(capture.Id);

        workspace.Json("index.json", new[] { capture });

        // Assert
        var current = Assert.Single(new ScreenshotStore(workspace.Root).Snapshot());

        Assert.True(current.Viewed);

        Assert.False(current.Flagged);

        Assert.Equal(capture.Id, current.Id);
    }

    [Fact]
    public void Missing_copy_keeps_flags_for_later_recovery()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture();

        workspace.Json("index.json", new[] { capture });

        var path = Path.Combine(workspace.Root, "captures", capture.Account, capture.FileName);

        File.Delete(path);

        // Act
        // Assert
        Assert.Empty(new ScreenshotStore(workspace.Root).Snapshot());

        // Act
        File.WriteAllBytes(path, [0xff, 0xd8, 0xff, 0xd9]);

        // Act
        // Assert
        Assert.Equal(capture, Assert.Single(new ScreenshotStore(workspace.Root).Snapshot()));
    }

    [Fact]
    public void Explicit_identifier_is_not_replaced_by_recovered_filename_identity()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture()with
        {
            Id = "stable-external-identifier"
        };

        workspace.Json("index.json", new[] { capture });

        // Act
        var store = new ScreenshotStore(workspace.Root);

        // Assert
        Assert.Equal(capture, Assert.Single(store.Snapshot()));

        Assert.True(store.Contains(capture.Account + "/" + capture.FileName));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 4)]
    public void Interrupted_AI_job_retains_checkpoint_and_bounded_retry(int attempts, int expected)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture();

        workspace.Json("index.json", new[] { capture });

        var record = new PrivacyScanRecord
        {
            CaptureId = capture.Id,
            RequestId = "resume-me",
            Profile = "profile",
            ImageHash = "hash",
            State = PrivacyScanState.Scanning,
            Attempts = attempts,
            RequestedCategories = [PrivacyScanCategory.PersonalChat],
            Categories = [new PrivacyCategoryResult
            {
                Category = PrivacyScanCategory.Game,
                State = PrivacyScanState.Completed,
                Version = "checkpoint"
            }

            ]
        };

        workspace.Json("privacy-results.json", new[] { record });

        // Act
        _ = new ScreenshotStore(workspace.Root);

        var store = new PrivacyResultStore(workspace.Root, new HashSet<string> { capture.Id });

        var recovered = store.Snapshot()[capture.Id];

        // Assert
        Assert.Equal((PrivacyScanState)expected, recovered.State);

        Assert.Equal("resume-me", recovered.RequestId);

        Assert.Equal(attempts, recovered.Attempts);

        Assert.Equal("checkpoint", Assert.Single(recovered.Categories).Version);

        Assert.Equal(PrivacyNames.Interrupted, recovered.FailureCode);

        Assert.Equal(
            JsonSerializer.Serialize(recovered),
            JsonSerializer.Serialize(
                new PrivacyResultStore(workspace.Root, new HashSet<string> { capture.Id }).Snapshot()[capture.Id]
            )
        );
    }
}
