// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.Models;
using TrackMirror.Storage;
using Xunit;

namespace TrackMirror.IntegrationTests.Storage;

public sealed class SettingsStoreTests
{
    [Fact]
    public void Temporarily_unreadable_settings_are_preserved_and_recover_after_the_lock_is_released()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var settings = new AppSettings { NotificationsEnabled = false, PrivacyGames = true };

        var path = Path.Combine(workspace.Root, "settings.json");

        workspace.Json("settings.json", settings);

        var bytes = File.ReadAllBytes(path);

        // Act
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var unavailable = new SettingsStore(workspace.Root);

            // Assert
            Assert.Equal(new AppSettings(), unavailable.Current);

            Assert.NotNull(unavailable.InitialWarning);
        }

        // Act
        var recovered = new SettingsStore(workspace.Root);

        // Assert
        Assert.Equal(settings, recovered.Current);

        Assert.Null(recovered.InitialWarning);

        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("{\"NotificationsEnabled\":")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"NotificationTimeoutSeconds\":0}")]
    public void Invalid_settings_use_defaults_and_leave_the_original_file_available_for_recovery(string json)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var path = Path.Combine(workspace.Root, "settings.json");

        File.WriteAllText(path, json);

        // Act
        var store = new SettingsStore(workspace.Root);

        // Assert
        Assert.Equal(new AppSettings(), store.Current);

        Assert.NotNull(store.InitialWarning);

        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void Failed_atomic_save_keeps_the_previous_file_and_current_settings_then_allows_retry()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var store = new SettingsStore(workspace.Root);

        var original = new AppSettings { NotificationsEnabled = false, RetentionDays = 90 };

        store.Save(original);

        var path = Path.Combine(workspace.Root, "settings.json");

        var bytes = File.ReadAllBytes(path);

        var changed = original with { NotificationsEnabled = true, PrivacyGames = true };

        // Act
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            // Assert
            Assert.Throws<IOException>(() => store.Save(changed));

            Assert.Equal(original, store.Current);

            Assert.Equal(bytes, File.ReadAllBytes(path));

            Assert.Equal(original, new SettingsStore(workspace.Root).Current);
        }

        // Act
        store.Save(changed);

        // Assert
        Assert.Equal(changed, store.Current);

        Assert.Equal(changed, new SettingsStore(workspace.Root).Current);

        Assert.False(File.Exists(path + ".tmp"));
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("retention")]
    [InlineData("language")]
    [InlineData("model")]
    [InlineData("control")]
    public void Invalid_save_is_rejected_before_existing_settings_are_changed(string invalid)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var original = new AppSettings { NotificationsEnabled = false, PrivacyGames = true };

        var store = new SettingsStore(workspace.Root);

        store.Save(original);

        var bytes = File.ReadAllBytes(Path.Combine(workspace.Root, "settings.json"));

        var settings = invalid switch
        {
            "timeout" => original with { NotificationTimeoutSeconds = 0 },
            "retention" => original with { RetentionDays = 0 },
            "language" => original with { Language = "unsupported" },
            "model" => original with { PrivacyModel = "unknown-model" },
            "control" => original with { HubstaffControl = null! },
            _ => throw new ArgumentException(nameof(invalid))
        };

        // Act
        // Assert
        Assert.ThrowsAny<ArgumentException>(() => store.Save(settings));

        Assert.Equal(original, store.Current);

        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(workspace.Root, "settings.json")));

        Assert.Equal(original, new SettingsStore(workspace.Root).Current);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(DataManagementNames.MaximumDays + 1)]
    public void Invalid_retention_is_disabled_while_unrelated_preferences_survive(int days)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var saved = new AppSettings
        {
            RetentionEnabled = true, RetentionDays = days, RetentionKeepUnread = false,
            NotificationsEnabled = false, PrivacyGames = true, StartWithHubstaff = true
        };

        workspace.Json("settings.json", saved);

        // Act
        var store = new SettingsStore(workspace.Root);

        // Assert
        Assert.Equal(saved with { RetentionEnabled = false, RetentionDays = DataManagementNames.DefaultDays },
            store.Current);

        Assert.NotNull(store.InitialWarning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Legacy_personal_activity_setting_maps_both_categories_without_overriding_explicit_values(bool enabled)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        workspace.Json("settings.json", new { PrivacyPersonalActivity = enabled, PrivacyGames = !enabled });

        // Act
        var store = new SettingsStore(workspace.Root);

        // Assert
        Assert.Equal(!enabled, store.Current.PrivacyGames);

        Assert.Equal(enabled, store.Current.PrivacyPersonalChats);

        Assert.True(store.Current.PrivacyConfidentialData);

        Assert.Null(store.InitialWarning);

        // Act
        store.Save(store.Current);

        // Assert
        Assert.Equal(store.Current, new SettingsStore(workspace.Root).Current);
    }
}
