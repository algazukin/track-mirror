// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.Models;
using TrackMirror.Storage;
using Xunit;

namespace TrackMirror.IntegrationTests.Updates;

public sealed class UpdateChannelSettingsTests
{
    [Fact]
    public void New_and_legacy_profiles_default_to_the_pre_release_channel()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        workspace.Json("settings.json", new { NotificationsEnabled = false });

        // Act
        var defaults = new AppSettings();
        var legacy = new SettingsStore(workspace.Root);

        // Assert
        Assert.Equal(UpdateChannel.PreRelease, defaults.UpdateChannel);
        Assert.Equal(UpdateChannel.PreRelease, legacy.Current.UpdateChannel);
        Assert.False(legacy.Current.NotificationsEnabled);
    }

    [Theory]
    [InlineData(UpdateChannel.Stable)]
    [InlineData(UpdateChannel.PreRelease)]
    public void The_selected_channel_survives_saving(UpdateChannel channel)
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var store = new SettingsStore(workspace.Root);

        // Act
        store.Save(store.Current with { UpdateChannel = channel, PrivacyGames = true });
        var restored = new SettingsStore(workspace.Root);

        // Assert
        Assert.Equal(channel, restored.Current.UpdateChannel);
        Assert.True(restored.Current.PrivacyGames);
    }

    [Fact]
    public void Invalid_saved_channels_are_reset_without_losing_other_preferences()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        workspace.Json("settings.json", new { UpdateChannel = 99, NotificationsEnabled = false });

        // Act
        var restored = new SettingsStore(workspace.Root);

        // Assert
        Assert.Equal(UpdateChannel.PreRelease, restored.Current.UpdateChannel);
        Assert.False(restored.Current.NotificationsEnabled);
        Assert.NotNull(restored.InitialWarning);
    }
}
