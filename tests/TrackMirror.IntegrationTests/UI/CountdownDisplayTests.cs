// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Models;
using TrackMirror.Monitoring;
using TrackMirror.UI;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

public sealed class CountdownDisplayTests
{
    [Theory]
    [InlineData(ScreenshotCountdownMode.Default, 30, false)]
    [InlineData(ScreenshotCountdownMode.Detailed, 31, true)]
    [InlineData(ScreenshotCountdownMode.BeforeScreenshot, 31, false)]
    [InlineData(ScreenshotCountdownMode.BeforeScreenshot, 30, true)]
    [InlineData(ScreenshotCountdownMode.BeforeScreenshot, 4, true)]
    [InlineData(ScreenshotCountdownMode.BeforeScreenshot, 3, false)]
    [InlineData(ScreenshotCountdownMode.Detailed, 3, false)]
    [InlineData(ScreenshotCountdownMode.BeforeScreenshot, 0, false)]
    public void Display_modes_respect_both_threshold_boundaries(ScreenshotCountdownMode mode, int seconds, bool expected)
    {
        // Arrange
        var options = new ScreenshotCountdownOptions
        {
            Enabled = true, Mode = mode, ShowBeforeSeconds = 30,
            HideBeforeScreenshot = true, HideBeforeSeconds = 3
        };
        var snapshot = new ScreenshotScheduleSnapshot(ScreenshotScheduleStatus.Scheduled,
            DateTimeOffset.UtcNow.AddSeconds(seconds), seconds);

        // Act
        var visible = CountdownDisplay.ShouldShow(options, snapshot);

        // Assert
        Assert.Equal(expected, visible);
    }

    [Theory]
    [InlineData((int)ScreenshotScheduleStatus.Waiting, false, true)]
    [InlineData((int)ScreenshotScheduleStatus.Unavailable, false, true)]
    [InlineData((int)ScreenshotScheduleStatus.Waiting, true, false)]
    [InlineData((int)ScreenshotScheduleStatus.Unavailable, true, false)]
    [InlineData((int)ScreenshotScheduleStatus.Stopped, false, false)]
    [InlineData((int)ScreenshotScheduleStatus.Disabled, false, false)]
    public void Unknown_schedules_do_not_reappear_during_scheduled_hiding(int status, bool hide, bool expected)
    {
        // Arrange
        var options = new ScreenshotCountdownOptions
        {
            Enabled = true, Mode = ScreenshotCountdownMode.Detailed, HideBeforeScreenshot = hide
        };
        var snapshot = new ScreenshotScheduleSnapshot((ScreenshotScheduleStatus)status);

        // Act
        var visible = CountdownDisplay.ShouldShow(options, snapshot);
        var timed = CountdownDisplay.ShouldShow(options with { Mode = ScreenshotCountdownMode.BeforeScreenshot }, snapshot);

        // Assert
        Assert.Equal(expected, visible);
        Assert.False(timed);
        Assert.Equal("--:--", CountdownDisplay.TimeText(snapshot));
    }

    [Fact]
    public void Disabled_timers_never_show_and_unhidden_timers_include_the_final_second()
    {
        // Arrange
        var snapshot = new ScreenshotScheduleSnapshot(ScreenshotScheduleStatus.Scheduled,
            DateTimeOffset.UtcNow.AddSeconds(1), 1);
        var options = new ScreenshotCountdownOptions { Enabled = true, Mode = ScreenshotCountdownMode.BeforeScreenshot };

        // Act
        var enabled = CountdownDisplay.ShouldShow(options, snapshot);
        var disabled = CountdownDisplay.ShouldShow(options with { Enabled = false }, snapshot);

        // Assert
        Assert.True(enabled);
        Assert.False(disabled);
        Assert.Equal("00:01", CountdownDisplay.TimeText(snapshot));
    }
}
