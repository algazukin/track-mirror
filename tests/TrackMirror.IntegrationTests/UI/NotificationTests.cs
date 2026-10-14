// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.IntegrationTests.Infrastructure.Windows;
using TrackMirror.IntegrationTests.Localization;
using TrackMirror.Models;
using TrackMirror.Storage;
using TrackMirror.UI.Controls;
using TrackMirror.UI.Forms;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

[Collection(nameof(LocalizationCollection))]
public sealed class NotificationTests
{
    [Fact]
    public void Notifications_and_timeout_settings_are_linked_and_preserve_the_selected_timeout()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var settings = new SettingsPanel(new AppSettings(), false);

            var enabled = UiControls.Field<CheckBox>(settings, "_notifications");

            var timeout = UiControls.Field<NumericUpDown>(settings, "_timeout");

            AppSettings? saved = null;

            settings.Changed += value => saved = value;

            timeout.Value = 17;

            // Act
            enabled.Checked = false;

            // Assert
            Assert.False(timeout.Enabled);

            Assert.NotNull(saved);

            Assert.False(saved.NotificationsEnabled);

            Assert.Equal(17, saved.NotificationTimeoutSeconds);

            // Act
            enabled.Checked = true;

            // Assert
            Assert.True(timeout.Enabled);

            Assert.True(saved.NotificationsEnabled);

            Assert.Equal(17, saved.NotificationTimeoutSeconds);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3601)]
    public void Invalid_preview_timeouts_are_rejected_without_changing_the_active_timer(int seconds)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        StaThread.Run(() =>
        {
            using var preview = new PreviewForm(new ScreenshotStore(workspace.Root));

            var timer = UiControls.Field<System.Windows.Forms.Timer>(preview, "_dismissTimer");

            var previous = timer.Interval;

            // Act
            var error = Record.Exception(() => preview.SetNotificationTimeout(seconds));

            // Assert
            Assert.IsType<ArgumentOutOfRangeException>(error);

            Assert.Equal(previous, timer.Interval);
        });
    }

    [Fact]
    public void A_preview_can_be_shown_again_after_hiding_with_its_configured_timeout_and_unread_state()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var first = workspace.ImageCapture("a.jpg");

        var second = workspace.ImageCapture("b.jpg");

        workspace.Json("index.json", new[] { first, second });

        StaThread.Run(() =>
        {
            var store = new ScreenshotStore(workspace.Root);

            using var preview = new PreviewForm(store) { Opacity = 0 };

            preview.SetNotificationTimeout(17);

            preview.ShowCapture(first);

            preview.Hide();

            // Act
            preview.ShowCapture(second);

            // Assert
            var timer = UiControls.Field<System.Windows.Forms.Timer>(preview, "_dismissTimer");

            Assert.Equal(17000, timer.Interval);

            Assert.True(timer.Enabled);

            Assert.Equal(second.Id, UiControls.Field<CaptureEntry>(preview, "entry").Id);

            Assert.All(store.Snapshot(), entry => Assert.False(entry.Viewed));

            // Act
            preview.Close();

            // Assert
            Assert.False(preview.Visible);

            Assert.False(preview.IsDisposed);

            Assert.False(timer.Enabled);
        });
    }

    [Fact]
    public void Reading_and_flagging_use_the_current_preview_and_update_its_buttons()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var entry = workspace.ImageCapture();

        workspace.Json("index.json", new[] { entry });

        StaThread.Run(() =>
        {
            var store = new ScreenshotStore(workspace.Root);

            using var preview = new PreviewForm(store) { Opacity = 0 };

            preview.FlagRequested += id => store.ToggleFlag(id);

            preview.ReviewRequested += store.MarkViewed;

            preview.ShowCapture(entry);

            // Act
            UiControls.Field<Button>(preview, "_flag").PerformClick();

            preview.UpdateCaption();

            // Assert
            Assert.True(Assert.Single(store.Snapshot()).Flagged);

            Assert.Equal(UiText.Unflag, UiControls.Field<Button>(preview, "_flag").Text);

            // Act
            UiControls.Field<Button>(preview, "_reviewed").PerformClick();

            preview.UpdateCaption();

            // Assert
            Assert.True(Assert.Single(store.Snapshot()).Viewed);

            Assert.False(preview.Visible);

            Assert.False(UiControls.Field<Button>(preview, "_reviewed").Enabled);
        });
    }
}
