// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text;
using TrackMirror.Constants;
using TrackMirror.Localization;
using TrackMirror.Models;
using TrackMirror.Privacy;
using TrackMirror.Privacy.Installation;
using TrackMirror.Storage;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Localization;

[Collection(nameof(LocalizationCollection))]
public sealed class LocalizationTests
{
    [Fact]
    public void English_is_the_default_for_new_and_existing_settings_without_a_language()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        // Act
        // Assert
        Assert.Equal(LocalizationNames.English, new AppSettings().Language);

        // Act
        workspace.Json("settings.json", new { RetentionDays = 90, PrivacyGames = true });

        var settings = new SettingsStore(workspace.Root).Current;

        // Assert
        Assert.Equal(LocalizationNames.English, settings.Language);

        Assert.Equal(90, settings.RetentionDays);

        Assert.True(settings.PrivacyGames);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xx")]
    public void Unsupported_saved_languages_fall_back_to_English_and_keep_other_preferences(string? language)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        workspace.Json("settings.json", new { Language = language, RetentionKeepUnread = false });

        // Act
        var store = new SettingsStore(workspace.Root);

        // Assert
        Assert.Equal(LocalizationNames.English, store.Current.Language);

        Assert.False(store.Current.RetentionKeepUnread);

        Assert.NotNull(store.InitialWarning);
    }

    [Fact]
    public void Language_choice_round_trips_and_does_not_invalidate_AI_coverage()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var settings = new AppSettings { Language = LocalizationNames.Russian, PrivacyGames = true };

        // Act
        new SettingsStore(workspace.Root).Save(settings);

        // Assert
        Assert.Equal(settings, new SettingsStore(workspace.Root).Current);

        Assert.Equal(PrivacyScanService.Profile(settings),
            PrivacyScanService.Profile(settings with { Language = LocalizationNames.English }));
    }

    [Fact]
    public void Both_resource_sets_have_every_key_and_valid_matching_format_arguments()
    {
        // Arrange
        var resources = new ResourceManager(LocalizationNames.ResourceBaseName, typeof(UiLocalization).Assembly);

        // Act
        using var english = resources.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, false)!;

        using var russian = resources.GetResourceSet(CultureInfo.GetCultureInfo("ru"), true, false)!;

        // Assert
        Assert.NotNull(english);

        Assert.NotNull(russian);

        var keys = english.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).Order().ToList();

        Assert.Equal(keys, russian.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).Order().ToList());

        foreach (var key in keys)
        {
            var first = english.GetString(key)!;

            var second = russian.GetString(key)!;

            Assert.False(string.IsNullOrWhiteSpace(first));

            Assert.False(string.IsNullOrWhiteSpace(second));

            Assert.Equal(CompositeFormat.Parse(first).MinimumArgumentCount,
                CompositeFormat.Parse(second).MinimumArgumentCount);

            if (key != "LanguageText.Russian")
            {
                Assert.DoesNotMatch("[А-Яа-яЁё]", first);
            }
        }
    }

    [Fact]
    public void Model_captions_and_retained_worker_messages_use_the_selected_application_language()
    {
        // Arrange
        var previous = CultureInfo.CurrentUICulture;

        var previousDefault = CultureInfo.DefaultThreadCurrentUICulture;

        try
        {
            // Act
            CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture =
                CultureInfo.GetCultureInfo("en-GB");

            var model = PrivacyModelCatalog.Get(PrivacyNames.Model);

            // Assert
            Assert.Contains("GB", model.Caption);

            Assert.Equal("History", SettingsText.HistoryTab);

            // Act
            CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture =
                CultureInfo.GetCultureInfo("ru-RU");

            // Assert
            Assert.Contains("ГБ", model.Caption);

            Assert.Equal("История", SettingsText.HistoryTab);

            // Existing worker contexts must still use the selected application language.
            // Act
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-GB");

            // Assert
            Assert.Equal("История", SettingsText.HistoryTab);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentUICulture = previousDefault;

            CultureInfo.CurrentUICulture = previous;
        }
    }
}
