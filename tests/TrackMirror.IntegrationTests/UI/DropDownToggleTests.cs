// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.IntegrationTests.Infrastructure.Windows;
using TrackMirror.IntegrationTests.Localization;
using TrackMirror.UI.Controls;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

[Collection(nameof(LocalizationCollection))]
public sealed class DropDownToggleTests
{
    [Theory]
    [InlineData("scan")]
    [InlineData("options")]
    [InlineData("calendar")]
    public void Clicking_the_open_dropdown_anchor_closes_without_reopening(string kind)
    {
        // Arrange
        StaThread.Run(() =>
        {
            using Control control = kind switch
            {
                "scan" => new GalleryActionBar(),
                "options" => new OptionSelector(),
                _ => new DateFilterControl()
            };

            if (control is GalleryActionBar bar)
            {
                bar.UpdateSelection(12, 12, true);
            }

            if (control is OptionSelector selector)
            {
                selector.SetChoices(["First", "Second"]);
            }

            using var form = new Form
            {
                Opacity = 0, ShowInTaskbar = false, ClientSize = new Size(1000, 700),
                StartPosition = FormStartPosition.Manual
            };

            form.Controls.Add(control);

            TestWindow.Show(form);

            var anchor = kind == "options" ? control
                : UiControls.Field<Button>(control, kind == "scan" ? "_scan" : "_choose");

            // Keep the real pointer untouched; place the invisible anchor underneath it.
            var origin = anchor.PointToScreen(Point.Empty);

            var pointer = Control.MousePosition;

            form.Location = new Point(form.Left + pointer.X - origin.X - anchor.Width / 2,
                form.Top + pointer.Y - origin.Y - anchor.Height / 2);

            var popup = UiControls.Field<ToolStripDropDown>(control, "_popup");

            popup.Opacity = 0;

            var openings = 0;

            popup.Opened += (_, _) => openings++;

            Action click = anchor is Button button ? button.PerformClick
                : () => UiControls.Invoke(control, "OnClick", EventArgs.Empty);

            click();

            Assert.True(popup.Visible);

            // Act
            // WinForms dismisses the popup before dispatching the anchor's Click event.
            popup.Close(ToolStripDropDownCloseReason.AppClicked);

            click();

            // Assert
            Assert.False(popup.Visible);

            Assert.Equal(1, openings);

            // Act
            click();

            popup.Close(ToolStripDropDownCloseReason.Keyboard);

            // Assert
            Assert.False(popup.Visible);

            Assert.Equal(2, openings);

            // Act
            click();

            form.Left += 2000;

            popup.Close(ToolStripDropDownCloseReason.AppClicked);

            // Assert
            Assert.False(popup.Visible);
        });
    }
}
