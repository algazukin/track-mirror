// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy.Models;
using TrackMirror.UI.Controls;
using TrackMirror.UI.Forms;
using Xunit;

namespace TrackMirror.IntegrationTests.Infrastructure.Windows;

internal static class UiLayoutAssertions
{
    internal static void Check(HistoryForm history)
    {
        // Arrange
        var gallery = UiControls.Field<CaptureGallery>(history, "_gallery");

        var bar = UiControls.Field<GalleryActionBar>(history, "_galleryActions");

        gallery.SelectAll(true);

        foreach (var logicalWidth in new[] { 960, 1160, 1600 })
        {
            // Act
            var scale = history.DeviceDpi / 96f;

            history.ClientSize = new Size((int)(logicalWidth * scale), (int)(720 * scale));

            history.PerformLayout();

            Application.DoEvents();

            // Assert
            Assert.True(gallery.Height > 100 * scale);

            var buttons = new[] { "_read", "_scan", "_clear", "_more" }
                .Select(name => UiControls.Field<Button>(bar, name)).Where(button => button.Visible).ToArray();

            Assert.All(buttons, button => Assert.True(button.Parent!.ClientRectangle.Contains(button.Bounds)));

            foreach (var pair in buttons.Zip(buttons.Skip(1)))
            {
                Assert.False(pair.First.Bounds.IntersectsWith(pair.Second.Bounds));
            }
        }

        CheckScanPopup(bar);

        // Arrange
        using var result = new PrivacyResultForm("fixture", "synthetic layout") { Opacity = 0 };

        var finding = new PrivacyFinding(PrivacyRules.Game, PrivacyRules.Medium, PrivacyRules.ContextReason, []);

        result.SetResult(new PrivacyScanRecord
        {
            CaptureId = "fixture", RequestId = "fixture", Profile = "fixture", Findings = [finding],
            State = PrivacyScanState.Completed
        }, new AppSettings());

        TestWindow.Show(result);

        // Act
        result.Size = result.MinimumSize;

        result.PerformLayout();

        Application.DoEvents();

        // Assert
        var details = UiControls.Field<PrivacyDetailPanel>(result, "_details");

        var actions = UiControls.Field<FlowLayoutPanel>(details, "_actions");

        var visible = actions.Controls.OfType<Button>().Where(button => button.Visible).ToArray();

        Assert.True(visible.Length >= 3);

        Assert.All(visible, button =>
        {
            Assert.True(actions.ClientRectangle.Contains(button.Bounds));

            Assert.True(button.Height >= button.MinimumSize.Height);

            Assert.True(button.Margin.Right >= 10);
        });

        foreach (var first in visible)
        {
            foreach (var second in visible.Where(button => button != first))
            {
                Assert.False(first.Bounds.IntersectsWith(second.Bounds));
            }
        }

        // Arrange
        using var options = new ScanOptionsPanel();

        using var host = new Form { ClientSize = new Size(540, 800), Opacity = 0, ShowInTaskbar = false };

        host.Controls.Add(options);

        options.Prepare(12, new AppSettings { PrivacyGames = true }, true);

        TestWindow.Show(host);

        // Act
        host.PerformLayout();

        Application.DoEvents();

        // Assert
        foreach (var name in new[] { "_start", "_cancel" })
        {
            var button = UiControls.Field<Button>(options, name);

            Assert.True(button.Parent!.ClientRectangle.Contains(button.Bounds));

            Assert.True(button.Width >= TextRenderer.MeasureText(button.Text, button.Font).Width);
        }
    }

    private static void CheckScanPopup(GalleryActionBar bar)
    {
        // Arrange
        var popup = UiControls.Field<ToolStripDropDown>(bar, "_popup");

        popup.Opacity = 0;

        var button = UiControls.Field<Button>(bar, "_scan");

        var host = UiControls.Field<ToolStripControlHost>(bar, "_host");

        var panel = UiControls.Field<ScanOptionsPanel>(bar, "_scanOptions");

        // Act
        button.PerformClick();

        Application.DoEvents();

        var first = popup.Size;

        bar.ClosePopups();

        button.PerformClick();

        Application.DoEvents();

        // Assert
        Assert.Equal(first, popup.Size);

        Assert.True(host.Height <= panel.Height + 2, "The scan popup has empty space below its content.");

        bar.ClosePopups();
    }
}
