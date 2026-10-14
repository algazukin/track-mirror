// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Reflection;
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
    [Theory]
    [InlineData(HubstaffControlMode.PauseUntilActive)]
    [InlineData(HubstaffControlMode.RestartWhileIdle)]
    public void Seconds_remain_editable_after_initial_layout_and_resizing(HubstaffControlMode mode)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        StaThread.Run(() =>
        {
            var store = new ScreenshotStore(workspace.Root);

            using var form = new HistoryForm(store, new AppSettings
            {
                HubstaffControl = new HubstaffControlOptions { Mode = mode }
            }) { Opacity = 0, ShowInTaskbar = false };

            TestWindow.Show(form);

            form.ShowSettings();

            var flags = BindingFlags.Instance | BindingFlags.NonPublic;

            var settings = (SettingsPanel)typeof(HistoryForm).GetField("_settingsPage", flags)!.GetValue(form)!;

            var panel = (HubstaffControlPanel)typeof(SettingsPanel).GetField("_hubstaff", flags)!.GetValue(settings)!;

            var saved = new SettingsStore(workspace.Root);

            form.SettingsChanged += saved.Save;

            foreach (var width in new[] { 1100, 1700, 1200 })
            {
                // Act
                form.ClientSize = new Size(width, 800);

                form.PerformLayout();

                foreach (var field in new[] { "_idle", "_restart" })
                {
                    var input = (NumericUpDown)typeof(HubstaffControlPanel).GetField(field, flags)!.GetValue(panel)!;

                    var editor = input.Controls.OfType<TextBox>().Single();

                    // Assert
                    Assert.True(editor.Width >= input.Font.Height * 3, "The seconds editor has collapsed.");

                    Assert.True(input.Parent!.ClientRectangle.Contains(input.Bounds));

                    if (field == "_restart" && mode == HubstaffControlMode.PauseUntilActive)
                    {
                        Assert.False(input.Enabled);

                        continue;
                    }

                    Assert.True(input.Enabled);

                    // Act
                    editor.Text = field == "_idle" ? "125" : "45";

                    // Assert
                    Assert.Equal(field == "_idle" ? 125 : 45, input.Value);
                }
            }

            var reloaded = new SettingsStore(workspace.Root).Current.HubstaffControl;

            // Assert
            Assert.Equal(125, reloaded.IdleSeconds);

            Assert.Equal(mode == HubstaffControlMode.RestartWhileIdle ? 45 : 60, reloaded.RestartSeconds);
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
