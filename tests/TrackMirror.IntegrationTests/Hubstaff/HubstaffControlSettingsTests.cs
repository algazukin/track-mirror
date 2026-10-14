// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Hubstaff;
using TrackMirror.Models;
using TrackMirror.Storage;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Hubstaff;

public sealed class HubstaffControlSettingsTests
{
    [Fact]
    public void Configurable_intervals_mode_and_cli_path_survive_settings_reload()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var options = new HubstaffControlOptions
        {
            Enabled = true, Mode = HubstaffControlMode.RestartWhileIdle,
            IdleSeconds = 123, RestartSeconds = 45, CliPath = Path.Combine(workspace.Root, "custom CLI.exe")
        };

        var store = new SettingsStore(workspace.Root);

        // Act
        store.Save(new AppSettings { HubstaffControl = options });

        // Assert
        Assert.Equal(options, new SettingsStore(workspace.Root).Current.HubstaffControl);
    }

    [Fact]
    public void Invalid_saved_control_is_disabled_without_discarding_unrelated_settings()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        workspace.Json("settings.json", new AppSettings
        {
            NotificationsEnabled = false, PrivacyGames = true,
            HubstaffControl = new HubstaffControlOptions { Enabled = true, IdleSeconds = 0 }
        });

        // Act
        var store = new SettingsStore(workspace.Root);

        // Assert
        Assert.False(store.Current.HubstaffControl.Enabled);

        Assert.False(store.Current.NotificationsEnabled);

        Assert.True(store.Current.PrivacyGames);

        Assert.NotNull(store.InitialWarning);
    }
}
