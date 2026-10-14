// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.IntegrationTests.Infrastructure.Windows;
using TrackMirror.IntegrationTests.Localization;
using TrackMirror.Models;
using TrackMirror.Privacy.Models;
using TrackMirror.UI.Controls;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

[Collection(nameof(LocalizationCollection))]
public sealed class ScanOptionsTests
{
    [Fact]
    public void Scan_options_publish_one_off_categories_and_force_without_changing_saved_settings()
    {
        // Arrange
        StaThread.Run(() =>
        {
            var settings = new AppSettings();

            using var panel = new ScanOptionsPanel();

            using var host = new Form { Opacity = 0, ShowInTaskbar = false, ClientSize = new Size(540, 800) };

            host.Controls.Add(panel);

            panel.Prepare(12, settings, true);

            TestWindow.Show(host);

            IReadOnlyList<PrivacyScanCategory>? requested = null;

            var force = false;

            panel.ScanRequested += (categories, recheck) =>
            {
                requested = categories;

                force = recheck;
            };

            UiControls.Field<CheckBox>(panel, "_games").Checked = true;

            UiControls.Field<CheckBox>(panel, "_confidential").Checked = false;

            UiControls.Field<CheckBox>(panel, "_recheck").Checked = true;

            // Act
            UiControls.Field<Button>(panel, "_start").PerformClick();

            // Assert
            Assert.Equal(new[] { PrivacyScanCategory.Game }, requested);

            Assert.True(force);

            Assert.False(settings.PrivacyGames);

            Assert.True(settings.PrivacyConfidentialData);

            // Act
            panel.Prepare(12, settings, true);

            // Assert
            Assert.False(UiControls.Field<CheckBox>(panel, "_recheck").Checked);

            Assert.Equal(new[] { PrivacyScanCategory.ConfidentialData }, panel.SelectedCategories());
        });
    }

    [Fact]
    public void Missing_model_offers_settings_and_no_selected_categories_disables_start()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var panel = new ScanOptionsPanel();

            using var host = new Form { Opacity = 0, ShowInTaskbar = false, ClientSize = new Size(540, 800) };

            host.Controls.Add(panel);

            TestWindow.Show(host);

            var opened = false;

            panel.SettingsRequested += () => opened = true;

            // Act
            panel.Prepare(12, new AppSettings(), false);

            UiControls.Field<Button>(panel, "_settings").PerformClick();

            // Assert
            Assert.True(opened);

            Assert.False(UiControls.Field<Button>(panel, "_start").Visible);

            // Act
            panel.Prepare(12, new AppSettings { PrivacyConfidentialData = false }, true);

            // Assert
            Assert.False(UiControls.Field<Button>(panel, "_settings").Visible);

            Assert.False(UiControls.Field<Button>(panel, "_start").Enabled);
        });
    }
}
