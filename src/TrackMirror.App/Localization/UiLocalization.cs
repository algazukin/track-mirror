// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Globalization;
using System.Resources;
using TrackMirror.Constants;

namespace TrackMirror.Localization;

internal static class UiLocalization
{
    private static readonly ResourceManager resources = new(LocalizationNames.ResourceBaseName,
        typeof(UiLocalization).Assembly);

    private static CultureInfo DisplayCulture =>
        CultureInfo.DefaultThreadCurrentUICulture ?? CultureInfo.CurrentUICulture;

    internal static string ActiveLanguage => DisplayCulture.TwoLetterISOLanguageName
        == LocalizationNames.Russian ? LocalizationNames.Russian : LocalizationNames.English;

    internal static bool Supported(string? language) =>
        language is LocalizationNames.English or LocalizationNames.Russian;

    internal static void Initialize(string? language)
    {
        var culture = CultureInfo.GetCultureInfo(language == LocalizationNames.Russian
            ? LocalizationNames.RussianCulture : LocalizationNames.EnglishCulture);

        CultureInfo.DefaultThreadCurrentCulture = culture;

        CultureInfo.DefaultThreadCurrentUICulture = culture;

        CultureInfo.CurrentCulture = culture;

        CultureInfo.CurrentUICulture = culture;
    }

    internal static string Get(string scope, string name)
    {
        var key = scope + LocalizationNames.KeySeparator + name;

        // Retained workers can carry an earlier ExecutionContext across a data-folder switch.
        return resources.GetString(key, DisplayCulture)
            ?? throw new MissingManifestResourceException(LocalizationNames.MissingResource + key);
    }
}
