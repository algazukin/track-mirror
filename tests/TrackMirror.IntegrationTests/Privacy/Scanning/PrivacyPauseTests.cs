// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Imaging;
using TrackMirror.Models;
using TrackMirror.Privacy;
using TrackMirror.Privacy.Models;
using TrackMirror.Storage;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.IntegrationTests.Infrastructure.TestDoubles;

namespace TrackMirror.IntegrationTests.Privacy.Scanning;

public sealed class PrivacyPauseTests
{
    [Fact]
    public async Task Storage_pause_preserves_the_request_and_does_not_consume_a_retry()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture();

        workspace.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(workspace.Root);

        using (var image = new Bitmap(320, 180))
        {
            image.Save(store.CopyPath(capture), TestImages.JpegEncoder, null);
        }

        var analyzer = new CancelOnceAnalyzer();

        await using var service = new PrivacyScanService(store,
            new AppSettings { PrivacyGames = true, PrivacyConfidentialData = false }, analyzer);

        // Act
        service.Enqueue([capture]);

        await analyzer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var request = service.Snapshot()[capture.Id].RequestId;

        await service.PauseAsync(default).WaitAsync(TimeSpan.FromSeconds(5));

        var paused = service.Snapshot()[capture.Id];

        // Assert
        Assert.Equal(PrivacyScanState.Queued, paused.State);

        Assert.Equal(0, paused.Attempts);

        Assert.Equal(request, paused.RequestId);

        Assert.Equal([PrivacyScanCategory.Game], paused.RequestedCategories);

        // Act
        service.ReloadResults();

        // Assert
        Assert.Equal(request, service.Snapshot()[capture.Id].RequestId);

        // Act
        service.Resume();

        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (service.Snapshot()[capture.Id].State != PrivacyScanState.Completed && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        var completed = service.Snapshot()[capture.Id];

        // Assert
        Assert.Equal(PrivacyScanState.Completed, completed.State);

        Assert.Equal(1, completed.Attempts);

        Assert.Equal(request, completed.RequestId);
    }
}
