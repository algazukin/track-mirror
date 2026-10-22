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
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

[Collection(nameof(LocalizationCollection))]
public sealed class ScreenshotCountdownTests
{
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
    public void The_overlay_reserves_its_full_logical_height_at_the_current_display_scale()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var form = new ScreenshotCountdownForm();

            // Act
            form.ApplyOptions(new ScreenshotCountdownOptions { Enabled = true });
            _ = form.Handle;

            // Assert
            Assert.True(form.ClientSize.Height >= 70 * form.DeviceDpi / 96,
                $"The timer is clipped at {form.DeviceDpi} DPI: {form.ClientSize}.");

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
                Enabled = true, Left = 320, Top = 100
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
