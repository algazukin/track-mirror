// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.Models;
using TrackMirror.Storage;
using Xunit;

namespace TrackMirror.IntegrationTests.Hubstaff;

public sealed class ScreenshotCountdownSettingsTests
{
    [Fact]
    public void Invalid_countdown_settings_are_disabled_without_resetting_other_preferences()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        workspace.Json("settings.json", new AppSettings
        {
            PrivacyGames = true, NotificationsEnabled = false,
            ScreenshotCountdown = new ScreenshotCountdownOptions { Enabled = true, HighlightSeconds = -1 }
        });

        // Act
        var store = new SettingsStore(workspace.Root);

        // Assert
        Assert.False(store.Current.ScreenshotCountdown.Enabled);

        Assert.True(store.Current.PrivacyGames);
        Assert.False(store.Current.NotificationsEnabled);
        Assert.NotNull(store.InitialWarning);
    }

    [Fact]
    public void Countdown_position_and_highlight_survive_saving_and_old_settings_leave_it_disabled()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var options = new ScreenshotCountdownOptions
        {
            Enabled = true, HighlightSeconds = 25, Left = -500, Top = 150,
            ScalePercent = 125, BackgroundOpacityPercent = 35, Mode = ScreenshotCountdownMode.Default,
            ShowBeforeSeconds = 45, HideBeforeScreenshot = true, HideBeforeSeconds = 5, MiddleButtonDrag = true
        };

        workspace.Json("settings.json", new { NotificationsEnabled = false });

        // Act
        var legacy = new SettingsStore(workspace.Root);
        var wasEnabled = legacy.Current.ScreenshotCountdown.Enabled;

        legacy.Save(legacy.Current with { ScreenshotCountdown = options });

        var restored = new SettingsStore(workspace.Root);

        // Assert
        Assert.False(wasEnabled);

        Assert.Equal(options, restored.Current.ScreenshotCountdown);

        Assert.False(restored.Current.NotificationsEnabled);
    }

    [Fact]
    public void Existing_enabled_timers_keep_their_position_and_receive_default_appearance_settings()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        workspace.Json("settings.json", new
        {
            ScreenshotCountdown = new { Enabled = true, HighlightSeconds = 25, Left = -500, Top = 150 }
        });

        // Act
        var restored = new SettingsStore(workspace.Root);

        // Assert
        Assert.True(restored.Current.ScreenshotCountdown.Enabled);
        Assert.Equal(ScreenshotCountdownMode.Detailed, restored.Current.ScreenshotCountdown.Mode);
        Assert.Equal(100, restored.Current.ScreenshotCountdown.ScalePercent);
        Assert.Equal(80, restored.Current.ScreenshotCountdown.BackgroundOpacityPercent);

        Assert.Equal(-500, restored.Current.ScreenshotCountdown.Left);
        Assert.Equal(150, restored.Current.ScreenshotCountdown.Top);
        Assert.Null(restored.InitialWarning);
    }

    [Theory]
    [InlineData(0, 3, false, true)]
    [InlineData(3601, 3, false, true)]
    [InlineData(30, 0, true, false)]
    [InlineData(30, 3601, true, false)]
    [InlineData(30, 30, true, false)]
    public void Invalid_thresholds_are_rejected_without_replacing_saved_preferences(int show, int hide, bool hiding, bool detailed)
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var store = new SettingsStore(workspace.Root);
        var options = new ScreenshotCountdownOptions
        {
            Enabled = true, Mode = detailed ? ScreenshotCountdownMode.Detailed : ScreenshotCountdownMode.BeforeScreenshot,
            ShowBeforeSeconds = show, HideBeforeScreenshot = hiding, HideBeforeSeconds = hide
        };

        // Act
        var exception = Record.Exception(() => store.Save(new AppSettings { ScreenshotCountdown = options }));

        // Assert
        Assert.IsType<ArgumentException>(exception);
        Assert.False(store.Current.ScreenshotCountdown.Enabled);
    }

    [Theory]
    [InlineData(49, 80)]
    [InlineData(201, 80)]
    [InlineData(100, -1)]
    [InlineData(100, 101)]
    public void Invalid_appearance_settings_disable_the_timer_and_preserve_other_preferences(int scale, int opacity)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        workspace.Json("settings.json", new AppSettings
        {
            NotificationsEnabled = false,
            ScreenshotCountdown = new ScreenshotCountdownOptions
            {
                Enabled = true, ScalePercent = scale, BackgroundOpacityPercent = opacity
            }
        });

        // Act
        var restored = new SettingsStore(workspace.Root);

        // Assert
        Assert.False(restored.Current.ScreenshotCountdown.Enabled);
        Assert.Equal(100, restored.Current.ScreenshotCountdown.ScalePercent);
        Assert.Equal(80, restored.Current.ScreenshotCountdown.BackgroundOpacityPercent);

        Assert.False(restored.Current.NotificationsEnabled);
        Assert.NotNull(restored.InitialWarning);
    }
}
