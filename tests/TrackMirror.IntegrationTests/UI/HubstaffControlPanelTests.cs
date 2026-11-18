// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Reflection;
using TrackMirror.Constants;
using TrackMirror.Hubstaff;
using TrackMirror.Models;
using TrackMirror.Storage;
using TrackMirror.UI.Controls;
using TrackMirror.UI.Forms;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.IntegrationTests.Infrastructure.Windows;
using TrackMirror.IntegrationTests.Localization;

namespace TrackMirror.IntegrationTests.UI;

[Collection(nameof(LocalizationCollection))]
public sealed class HubstaffControlPanelTests
{
    [Fact]
    public void Only_idle_pause_is_offered_and_its_threshold_remains_editable_after_resizing()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        StaThread.Run(() =>
        {
            var store = new ScreenshotStore(workspace.Root);

            using var form = new HistoryForm(store, new AppSettings()) { Opacity = 0, ShowInTaskbar = false };

            TestWindow.Show(form);

            form.ShowSettings();

            var flags = BindingFlags.Instance | BindingFlags.NonPublic;

            var settings = (SettingsPanel)typeof(HistoryForm).GetField("_settingsPage", flags)!.GetValue(form)!;

            var panel = (HubstaffControlPanel)typeof(SettingsPanel).GetField("_hubstaff", flags)!.GetValue(settings)!;

            var saved = new SettingsStore(workspace.Root);

            form.SettingsChanged += saved.Save;

            // Assert
            Assert.Empty(UiControls.Descendants<OptionSelector>(panel));
            var enabled = Assert.Single(UiControls.Descendants<CheckBox>(panel));
            Assert.Equal(HubstaffControlText.PauseMode, enabled.Text);
            var input = Assert.Single(UiControls.Descendants<NumericUpDown>(panel));
            var editor = input.Controls.OfType<TextBox>().Single();

            // Act
            enabled.Checked = true;

            foreach (var width in new[] { 1100, 1700, 1200 })
            {
                // Act
                form.ClientSize = new Size(width, 800);

                form.PerformLayout();

                // Assert
                Assert.True(editor.Width >= input.Font.Height * 3, "The seconds editor has collapsed.");
                Assert.True(input.Parent!.ClientRectangle.Contains(input.Bounds));
                Assert.True(input.Enabled);

                // Act
                editor.Text = "125";

                // Assert
                Assert.Equal(125, input.Value);
            }

            var reloaded = new SettingsStore(workspace.Root).Current.HubstaffControl;

            // Assert
            Assert.True(reloaded.Enabled);
            Assert.Equal(125, reloaded.IdleSeconds);
            Assert.Equal(HubstaffControlMode.PauseUntilActive, reloaded.Mode);
        });
    }

    [Fact]
    public void Disabling_publishes_valid_opt_out_despite_invalid_draft_path()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var panel = new HubstaffControlPanel(new HubstaffControlOptions { Enabled = true });

            HubstaffControlOptions? change = null;

            panel.Changed += value => change = value;

            var flags = BindingFlags.Instance | BindingFlags.NonPublic;

            var path = (TextBox)typeof(HubstaffControlPanel).GetField("_path", flags)!.GetValue(panel)!;

            var enabled = (CheckBox)typeof(HubstaffControlPanel).GetField("_enabled", flags)!.GetValue(panel)!;

            // Act
            path.Text = "invalid-relative-path";

            enabled.Checked = false;

            // Assert
            Assert.NotNull(change);

            Assert.False(change.Enabled);

            Assert.True(change.IsValid);
        });
    }
}
