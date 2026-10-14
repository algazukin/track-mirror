// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Models;
using TrackMirror.Constants;
using TrackMirror.Monitoring;
using TrackMirror.Storage;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Monitoring;

public sealed class MonitorPauseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Captures_arriving_during_pause_remain_unread_and_notify_after_resume(bool switchFolder)
    {
        // Arrange
        using var source = new TestWorkspace();

        using var data = new TestWorkspace();

        using var destination = new TestWorkspace();

        using var arriving = new TestWorkspace();

        var preparedAccount = Path.Combine(arriving.Root, "account");

        Directory.CreateDirectory(Path.Combine(preparedAccount, "screens"));

        File.WriteAllBytes(Path.Combine(preparedAccount, "screens", "new-capture.jpg"), TestImages.Jpeg(320, 180));

        var screens = Path.Combine(source.Root, "fixture", "screens");

        Directory.CreateDirectory(screens);

        var original = new ScreenshotStore(data.Root);

        await using var monitor = new ScreenshotMonitor(source.Root, original);

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        monitor.StatusChanged += _ => ready.TrySetResult();

        monitor.Start();

        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var queued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var savedWhilePaused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        monitor.StatusChanged += status =>
        {
            if (status == string.Format(Messages.MonitoringAccountsFormat, 2))
            {
                queued.TrySetResult();
            }
        };

        monitor.CaptureSaved += (_, _) => savedWhilePaused.TrySetResult();

        // Act
        await monitor.PauseAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Directory.Move(preparedAccount, Path.Combine(source.Root, "arrivals"));

        await queued.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Task.WhenAny(savedWhilePaused.Task, Task.Delay(100));

        // Assert
        Assert.False(savedWhilePaused.Task.IsCompleted);

        Assert.Empty(original.Snapshot());

        var selected = switchFolder ? new ScreenshotStore(destination.Root) : original;

        monitor.Retarget(selected);

        var saved = new TaskCompletionSource<(CaptureEntry Entry, bool Notify)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        monitor.CaptureSaved += (entry, notify) => saved.TrySetResult((entry, notify));

        // Act
        monitor.Resume();

        var result = await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(result.Notify);

        Assert.False(result.Entry.Viewed);

        Assert.Single(selected.Snapshot());

        if (switchFolder)
        {
            Assert.Empty(original.Snapshot());
        }
    }

    [Fact]
    public async Task Disposing_a_paused_monitor_cancels_its_blocked_writer()
    {
        // Arrange
        using var source = new TestWorkspace();

        using var data = new TestWorkspace();

        var monitor = new ScreenshotMonitor(source.Root, new ScreenshotStore(data.Root));

        await monitor.PauseAsync().WaitAsync(TimeSpan.FromSeconds(5));

        monitor.Start();

        var screens = Path.Combine(source.Root, "fixture", "screens");

        Directory.CreateDirectory(screens);

        File.WriteAllBytes(Path.Combine(screens, "new.jpg"), TestImages.Jpeg(320, 180));

        await Task.Delay(200);

        // Act
        // Assert
        await monitor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
