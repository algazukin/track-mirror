// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Imaging;
using System.Security.Cryptography;
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

public sealed class PrivacyQueueTests
{
    [Fact]
    public async Task Cancelling_preserves_partial_findings_and_metrics_after_sqlite_reload()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture();

        using (var image = new Bitmap(320, 180))
        {
            image.Save(Path.Combine(workspace.Root, "captures", capture.Account, capture.FileName),
                TestImages.JpegEncoder, null);
        }

        workspace.Json("index.json", new[] { capture });

        var analyzer = new CheckpointingAnalyzer();

        await using var service = new PrivacyScanService(new ScreenshotStore(workspace.Root), new AppSettings
        {
            PrivacyGames = true, PrivacyPersonalChats = false, PrivacyConfidentialData = false
        }, analyzer);

        // Act
        service.Enqueue([capture]);

        await analyzer.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));

        service.Cancel();

        await service.PauseAsync(CancellationToken.None);

        var store = new PrivacyResultStore(workspace.Root, new HashSet<string> { capture.Id });

        var result = store.Snapshot()[capture.Id];

        // Assert
        Assert.Equal(PrivacyScanState.Cancelled, result.State);

        Assert.Single(result.Findings);

        var category = Assert.Single(result.Categories);

        Assert.Equal(PrivacyScanState.Partial, category.State);

        Assert.Equal(2, category.Diagnostics.Count);

        Assert.Equal(PrivacyNames.Cancelled, category.Diagnostics[^1].FailureCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migrated_queue_resumes_only_missing_category_and_retains_completed_checkpoint(bool differentCase)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture("Проверка-0.jpg");

        var path = Path.Combine(workspace.Root, "captures", capture.Account, capture.FileName);

        using (var image = new Bitmap(320, 180))
        {
            image.Save(path, TestImages.JpegEncoder, null);
        }

        workspace.Json("index.json", new[] { capture });

        var settings = new AppSettings
        {
            PrivacyGames = true,
            PrivacyPersonalChats = true,
            PrivacyConfidentialData = false
        };

        var finding = new PrivacyFinding(PrivacyRules.Game, PrivacyRules.Medium, PrivacyRules.ContextReason, []);

        var checkpoint = new PrivacyCategoryResult
        {
            Category = PrivacyScanCategory.Game,
            Version = PrivacyNames.Version,
            State = PrivacyScanState.Completed,
            Findings = [finding]
        };

        var queued = new PrivacyScanRecord
        {
            CaptureId = differentCase ? capture.Id.ToUpperInvariant() : capture.Id,
            RequestId = "durable-job",
            Profile = PrivacyScanService.Profile(settings),
            ImageHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            State = PrivacyScanState.Queued,
            RequestedCategories = [PrivacyScanCategory.PersonalChat],
            Categories = [checkpoint],
            Findings = [finding],
            Disposition = PrivacyDisposition.FalsePositive
        };

        workspace.Json("privacy-results.json", new[] { queued });

        var captures = new ScreenshotStore(workspace.Root);

        var analyzer = new RecordingAnalyzer();

        // Act
        await using (var service = new PrivacyScanService(captures, settings, analyzer))
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);

            while (service.Snapshot()[capture.Id].State != PrivacyScanState.Completed && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            var result = service.Snapshot()[capture.Id];

            // Assert
            Assert.Equal(PrivacyScanState.Completed, result.State);

            Assert.Equal("durable-job", result.RequestId);

            Assert.Equal(PrivacyDisposition.FalsePositive, result.Disposition);

            Assert.Equal(
                System.Text.Json.JsonSerializer.Serialize(finding),
                System.Text.Json.JsonSerializer.Serialize(Assert.Single(result.Findings))
            );
        }

        var input = Assert.Single(analyzer.Calls);

        Assert.False(input.Games);

        Assert.True(input.PersonalChats);

        Assert.False(input.ConfidentialData);

        // Act
        var reopened = new PrivacyResultStore(workspace.Root, new HashSet<string> { capture.Id });

        var durable = reopened.Snapshot()[capture.Id];

        // Assert
        Assert.Equal(PrivacyScanState.Completed, durable.State);

        Assert.Equal(2, durable.Categories.Count);

        Assert.Empty(durable.RequestedCategories);

        Assert.Equal(queued.ImageHash, durable.ImageHash);

        Assert.Equal(
            PrivacyScanState.Queued,
            System.Text.Json.JsonSerializer.Deserialize<List<PrivacyScanRecord>>(
                File.ReadAllText(Path.Combine(workspace.Root, "privacy-results.json"))
            )![0].State
        );
    }
}
