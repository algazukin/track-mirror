// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.IntegrationTests.Infrastructure.Windows;
using TrackMirror.IntegrationTests.Localization;
using TrackMirror.Localization;
using TrackMirror.Models;
using TrackMirror.Monitoring;
using TrackMirror.UI.Forms;
using TrackMirror.UI.Controls;
using TrackMirror.Constants;
using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.Storage;
using System.Runtime.InteropServices;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

[Collection(nameof(LocalizationCollection))]
public sealed class ScreenshotCountdownTests
{
    [Fact]
    public async Task Disabling_or_changing_mode_during_drag_persists_the_final_position_through_the_application()
    {
        // Arrange
        // The child process owns a synthetic profile; gestures are queued without injecting mouse input.

        // Act
        var failure = await Record.ExceptionAsync(() => UiScenarioProcess.RunAsync("countdown-settings"));

        // Assert
        Assert.Null(failure);
    }

    [Fact]
    public void Middle_button_mode_updates_native_click_through_without_listening_while_hidden()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var form = new ScreenshotCountdownForm();
            var options = new ScreenshotCountdownOptions { Enabled = true, Mode = ScreenshotCountdownMode.Detailed };

            // Act
            form.ApplyOptions(options with { MiddleButtonDrag = true });
            var transparent = GetWindowLong(form.Handle, -20);
            var hiddenListener = UiControls.Field<object?>(form, "middleListener");
            form.ApplyOptions(options);
            var interactive = GetWindowLong(form.Handle, -20);

            // Assert
            Assert.NotEqual(0, transparent & 0x20);
            Assert.Equal(0, interactive & 0x20);
            Assert.Null(hiddenListener);
            Assert.False(form.Visible);
            Assert.Equal(Cursors.SizeAll, form.Cursor);
        });
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void In_app_status_stays_beside_navigation_and_can_be_cleared(string language)
    {
        // Arrange
        StaThread.Run(() =>
        {
            UiLocalization.Initialize(language);
            using var workspace = new TestWorkspace();
            var store = new ScreenshotStore(workspace.Root);
            using var form = new HistoryForm(store);
            _ = form.Handle;
            form.ClientSize = new Size(1000 * form.DeviceDpi / 96, 700 * form.DeviceDpi / 96);

            // Act
            form.SetCountdownStatus(CountdownText.Scheduled + ": 00:30");
            form.PerformLayout();
            var status = UiControls.Field<Label>(form, "_countdownStatus");
            var settingsTab = UiControls.Field<Button>(form, "_settingsTab");

            // Assert
            Assert.Contains("00:30", status.Text);
            Assert.True(status.Bounds.Width > 0);
            Assert.True(status.Left >= settingsTab.Parent!.Left + settingsTab.Right);

            // Act
            form.MinimumSize = Size.Empty;
            form.ClientSize = new Size(620 * form.DeviceDpi / 96, 600 * form.DeviceDpi / 96);
            form.PerformLayout();

            // Assert
            Assert.True(settingsTab.Parent.ClientRectangle.Contains(settingsTab.Bounds));
            Assert.True(settingsTab.Parent.Parent!.ClientRectangle.Contains(settingsTab.Parent.Bounds));

            // Act
            form.SetCountdownStatus(null);

            // Assert
            Assert.Empty(status.Text);
        });
    }

    [Fact]
    public void Disabling_the_widget_retains_the_latest_queued_drag_position()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var form = new ScreenshotCountdownForm();
            var options = new ScreenshotCountdownOptions
            {
                Enabled = true, Mode = ScreenshotCountdownMode.Detailed, MiddleButtonDrag = true, Left = 80, Top = 80
            };
            form.ApplyOptions(options);
            UiControls.Invoke(form, "QueueMiddleDrag", new Point(320, 200), false);

            // Act
            var saved = form.ApplyOptions(options with { Enabled = false });
            Application.DoEvents();

            // Assert
            Assert.Equal(320, saved.Left);
            Assert.Equal(200, saved.Top);
            Assert.Equal(new Point(320, 200), form.Location);
            Assert.False(form.Visible);
        });
    }

    [Fact]
    public void Display_settings_enable_only_applicable_controls_and_keep_hiding_below_appearance()
    {
        // Arrange
        StaThread.Run(() =>
        {
            UiLocalization.Initialize("en");
            using var panel = new ScreenshotCountdownPanel(new ScreenshotCountdownOptions { Enabled = true });
            ScreenshotCountdownOptions? saved = null;
            panel.Changed += value => saved = value;
            var mode = UiControls.Descendants<OptionSelector>(panel).Single(control => control.AccessibleName == CountdownText.Mode);
            var show = UiControls.Descendants<NumericUpDown>(panel).Single(control => control.AccessibleName == CountdownText.ShowBefore);
            var hide = UiControls.Descendants<NumericUpDown>(panel).Single(control => control.AccessibleName == CountdownText.HideSeconds);
            var hiding = UiControls.Descendants<CheckBox>(panel).Single(control => control.AccessibleName == CountdownText.HideBefore);
            var middle = UiControls.Descendants<CheckBox>(panel).Single(control => control.AccessibleName == CountdownText.MiddleDrag);

            // Assert
            Assert.False(show.Enabled);
            Assert.False(hiding.Enabled);
            Assert.False(middle.Enabled);

            // Act
            mode.SelectValue(CountdownText.BeforeScreenshot);
            hiding.Checked = true;
            middle.Checked = true;
            show.Value = 2;

            // Assert
            Assert.True(show.Enabled);
            Assert.True(hide.Enabled);
            Assert.NotNull(saved);
            Assert.Equal(ScreenshotCountdownMode.BeforeScreenshot, saved.Mode);
            Assert.True(saved.MiddleButtonDrag);
            Assert.Equal(1, saved.HideBeforeSeconds);
            Assert.True(saved.IsValid);

            // Act
            mode.SelectValue(CountdownText.Detailed);

            // Assert
            Assert.False(show.Enabled);
            Assert.True(hide.Enabled);

            // Act
            mode.SelectValue(CountdownText.BeforeScreenshot);
            show.Value = 1;

            // Assert
            Assert.False(hiding.Checked);
            Assert.False(hiding.Enabled);
            Assert.True(saved!.IsValid);
        });
    }

    [Fact]
    public void Settings_enable_the_timer_without_enabling_previews_or_losing_its_saved_position()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var panel = new SettingsPanel(new AppSettings
            {
                NotificationsEnabled = false,
                ScreenshotCountdown = new ScreenshotCountdownOptions { Left = 320, Top = 100 }
            }, false);

            AppSettings? saved = null;
            panel.Changed += value => saved = value;

            // Act
            var toggle = UiControls.Descendants<CheckBox>(panel)
                .SingleOrDefault(control => control.AccessibleName == CountdownText.Enabled);

            // Assert
            Assert.NotNull(toggle);

            // Act
            toggle.Checked = true;

            var highlight = UiControls.Descendants<NumericUpDown>(panel)
                .Single(control => control.AccessibleName == CountdownText.Highlight);
            highlight.Value = 25;

            // Assert
            Assert.NotNull(saved);
            Assert.True(saved.ScreenshotCountdown.Enabled);
            Assert.Equal(25, saved.ScreenshotCountdown.HighlightSeconds);
            Assert.Equal(320, saved.ScreenshotCountdown.Left);
            Assert.False(saved.NotificationsEnabled);
        });
    }

    [Fact]
    public void The_overlay_renders_the_countdown_and_replaces_expired_data_with_a_waiting_state()
    {
        // Arrange
        StaThread.Run(() =>
        {
            UiLocalization.Initialize("en");

            using var form = new ScreenshotCountdownForm();
            form.ApplyOptions(new ScreenshotCountdownOptions { Enabled = true });

            // Act
            form.UpdateState(new ScreenshotScheduleSnapshot(ScreenshotScheduleStatus.Scheduled,
                DateTimeOffset.Parse("2026-10-22T12:02:51Z"), 111));
            var scheduled = form.AccessibleDescription;

            form.UpdateState(new ScreenshotScheduleSnapshot(ScreenshotScheduleStatus.Waiting));

            // Assert
            Assert.Contains("01:51", scheduled);

            Assert.DoesNotContain("01:51", form.AccessibleDescription);
            Assert.Contains("Waiting", form.AccessibleDescription);

            Assert.False(form.ShowInTaskbar);
        });
    }

    [Fact]
    public void The_digits_fit_inside_a_compact_caption_free_widget_at_the_current_display_scale()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var form = new ScreenshotCountdownForm();

            // Act
            form.ApplyOptions(new ScreenshotCountdownOptions { Enabled = true });
            _ = form.Handle;

            // Assert
            Assert.InRange(form.ClientSize.Height, 42 * form.DeviceDpi / 96, 50 * form.DeviceDpi / 96);

            Assert.True(Screen.PrimaryScreen!.WorkingArea.Contains(form.Bounds),
                $"The initial timer position is offscreen after scaling: {form.Bounds}.");
        });
    }

    [Fact]
    public void The_timer_is_compact_and_user_scaling_does_not_accumulate()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var form = new ScreenshotCountdownForm();
            var options = new ScreenshotCountdownOptions { Enabled = true };
            form.ApplyOptions(options);
            _ = form.Handle;
            var normal = form.ClientSize;

            // Act
            form.ApplyOptions(options with { ScalePercent = 200 });
            var large = form.ClientSize;

            form.ApplyOptions(options with { ScalePercent = 50 });
            var small = form.ClientSize;

            form.ApplyOptions(options);

            // Assert
            Assert.True(normal.Width <= 160 * form.DeviceDpi / 96, $"The timer is too wide: {normal}.");

            Assert.Equal(normal.Width * 2, large.Width);
            Assert.InRange(small.Width, normal.Width / 2, normal.Width / 2 + 1);
            Assert.Equal(normal, form.ClientSize);

            Assert.True(Screen.PrimaryScreen!.WorkingArea.Contains(form.Bounds));
        });
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(25, 64)]
    [InlineData(100, 255)]
    public void Only_the_background_fades_and_the_countdown_stays_opaque(int opacity, int alpha)
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var form = new ScreenshotCountdownForm();
            form.ApplyOptions(new ScreenshotCountdownOptions { Enabled = true, BackgroundOpacityPercent = opacity });

            form.UpdateState(new ScreenshotScheduleSnapshot(ScreenshotScheduleStatus.Scheduled,
                DateTimeOffset.Parse("2026-10-22T12:02:51Z"), 111));

            // Act
            using var surface = form.RenderSurface();
            var background = surface.GetPixel(surface.Width - 4, surface.Height / 2);

            var opaqueTextPixels = 0;

            for (var y = surface.Height / 2; y < surface.Height - 5; y++)
                for (var x = 5; x < surface.Width - 5; x++)
                {
                    var pixel = surface.GetPixel(x, y);

                    if (pixel.A == 255 && pixel.B > pixel.R + 30)
                        opaqueTextPixels++;
                }

            // Assert
            Assert.Equal(alpha, background.A);

            Assert.True(opaqueTextPixels > 20, "The countdown must remain opaque when its background fades.");
        });
    }

    [Fact]
    public void Appearance_controls_apply_immediately_and_preserve_the_saved_position()
    {
        // Arrange
        StaThread.Run(() =>
        {
            UiLocalization.Initialize("en");
            using var panel = new ScreenshotCountdownPanel(new ScreenshotCountdownOptions
            {
                Enabled = true, Mode = ScreenshotCountdownMode.Detailed, Left = 320, Top = 100
            });

            ScreenshotCountdownOptions? saved = null;
            panel.Changed += value => saved = value;

            // Act
            var scale = UiControls.Descendants<NumericUpDown>(panel)
                .SingleOrDefault(control => control.AccessibleName == "Timer scale");
            var opacity = UiControls.Descendants<NumericUpDown>(panel)
                .SingleOrDefault(control => control.AccessibleName == "Background opacity");

            // Assert
            Assert.NotNull(scale);
            Assert.NotNull(opacity);

            // Act
            scale.Value = 75;
            opacity.Value = 25;

            // Assert
            Assert.NotNull(saved);
            Assert.Equal(75, saved.ScalePercent);
            Assert.Equal(25, saved.BackgroundOpacityPercent);

            Assert.Equal(320, saved.Left);
            Assert.Equal(100, saved.Top);
            Assert.Equal(10, saved.HighlightSeconds);
        });
    }

    [Fact]
    public void Rounded_corners_are_transparent_while_the_middle_of_the_background_remains_visible()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var form = new ScreenshotCountdownForm();
            form.ApplyOptions(new ScreenshotCountdownOptions { BackgroundOpacityPercent = 80 });

            // Act
            using var surface = form.RenderSurface();

            // Assert
            Assert.Equal(0, surface.GetPixel(0, 0).A);
            Assert.Equal(0, surface.GetPixel(surface.Width - 1, surface.Height - 1).A);

            Assert.Equal(204, surface.GetPixel(surface.Width / 2, surface.Height - 4).A);
        });
    }

    [Theory]
    [InlineData(-3000, -1000, -1920, 0)]
    [InlineData(3000, 3000, -200, 930)]
    public void A_saved_position_is_clamped_to_the_available_monitor(int x, int y, int expectedX, int expectedY)
    {
        // Arrange
        var area = new Rectangle(-1920, 0, 1920, 1000);

        // Act
        var position = ScreenshotCountdownForm.ConstrainLocation(new Point(x, y), new Size(200, 70), area);

        // Assert
        Assert.Equal(new Point(expectedX, expectedY), position);
    }
}
