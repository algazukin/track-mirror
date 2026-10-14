// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Imaging;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Storage;
using TrackMirror.Storage;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.IntegrationTests.Infrastructure.TestDoubles;

namespace TrackMirror.IntegrationTests.Privacy.Scanning;

public sealed class SelectiveScanTests
{
    [Fact]
    public async Task Manual_categories_are_retained_for_active_and_queued_captures()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var entries = new[] { workspace.Capture(), workspace.Capture("2026-10-13T103100-0.jpg") };

        foreach (var entry in entries)
        {
            using var bitmap = new Bitmap(100, 60);

            bitmap.Save(Path.Combine(workspace.Root, "captures", entry.Account, entry.FileName),
                TestImages.JpegEncoder, null);
        }

        workspace.Json("index.json", entries);

        var analyzer = new CancelOnceAnalyzer();

        await using var service = new PrivacyScanService(new ScreenshotStore(workspace.Root),
            new AppSettings(), analyzer);

        // Act
        service.Enqueue([entries[0]], categories: [PrivacyScanCategory.Game]);

        await analyzer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        service.Enqueue([entries[1]], categories: [PrivacyScanCategory.Game]);

        service.Enqueue(entries, categories: [PrivacyScanCategory.PersonalChat]);

        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (service.Snapshot().Values.Any(result =>
            result.State is PrivacyScanState.Queued or PrivacyScanState.Scanning))
        {
            Assert.True(DateTime.UtcNow < deadline, "The expanded scan did not complete.");

            await Task.Delay(20);
        }

        // Assert
        foreach (var result in service.Snapshot().Values)
        {
            Assert.Equal(2, result.Categories.Count);

            Assert.Contains(result.Categories, category => category.Category == PrivacyScanCategory.Game);

            Assert.Contains(result.Categories, category => category.Category == PrivacyScanCategory.PersonalChat);

            Assert.All(result.Categories, category => Assert.Equal(PrivacyScanState.Completed, category.State));
        }
    }

    [Fact]
    public async Task Resumed_manual_scan_preserves_categories_when_the_first_image_hash_is_created()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture();

        using (var bitmap = new Bitmap(100, 60))
        {
            bitmap.Save(Path.Combine(workspace.Root, "captures", capture.Account, capture.FileName),
                TestImages.JpegEncoder, null);
        }

        workspace.Json("index.json", new[] { capture });

        var settings = new AppSettings { PrivacyConfidentialData = false };

        var captures = new ScreenshotStore(workspace.Root);

        var results = new PrivacyResultStore(workspace.Root, new HashSet<string> { capture.Id });

        results.Put([new PrivacyScanRecord
        {
            CaptureId = capture.Id, RequestId = "manual", Profile = PrivacyScanService.Profile(settings),
            State = PrivacyScanState.Queued, RequestedCategories = [PrivacyScanCategory.Game]
        }]);

        var analyzer = new RecordingAnalyzer();

        // Act
        await using var service = new PrivacyScanService(captures, settings, analyzer);

        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (service.Snapshot()[capture.Id].State is PrivacyScanState.Queued or PrivacyScanState.Scanning)
        {
            Assert.True(DateTime.UtcNow < deadline, "The queued scan did not complete.");

            await Task.Delay(20);
        }

        // Assert
        var call = Assert.Single(analyzer.Calls);

        Assert.True(call.Games);

        Assert.False(call.PersonalChats || call.ConfidentialData);

        Assert.Equal(PrivacyScanCategory.Game, Assert.Single(service.Snapshot()[capture.Id].Categories).Category);

        Assert.False(settings.HasPrivacyCategories);
    }

    [Fact]
    public void Manual_findings_and_uncertainty_remain_visible_when_the_category_is_off_in_settings()
    {
        // Arrange
        var settings = new AppSettings { PrivacyConfidentialData = false };

        var record = new PrivacyScanRecord
        {
            CaptureId = "fixture", RequestId = "manual", Profile = PrivacyScanService.Profile(settings),
            ImageHash = "fixture", State = PrivacyScanState.Partial,
            Categories = [new PrivacyCategoryResult
            {
                Category = PrivacyScanCategory.Game,
                Version = PrivacyCoverage.Version(PrivacyScanCategory.Game, settings.PrivacyModel),
                State = PrivacyScanState.Partial,
                FailureCode = PrivacyNames.ModelUncertain,
                Findings = [new PrivacyFinding(PrivacyRules.Game, PrivacyRules.Medium, PrivacyRules.ContextReason, [])]
            }]
        };

        // Act
        var projected = PrivacyCoverage.Project(record, settings);

        // Assert
        Assert.Single(projected.Findings);

        Assert.True(projected.NeedsCheck);

        Assert.Equal(PrivacyScanState.Partial, projected.State);

        Assert.Equal(PrivacyNames.ModelUncertain, projected.FailureCode);
    }
}
