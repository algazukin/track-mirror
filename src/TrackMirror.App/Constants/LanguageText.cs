// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class LanguageText
{
    internal static string Language => UiLocalization.Get(nameof(LanguageText), nameof(Language));
    internal static string Choose => UiLocalization.Get(nameof(LanguageText), nameof(Choose));
    internal static string Restart => UiLocalization.Get(nameof(LanguageText), nameof(Restart));
    internal static string English => UiLocalization.Get(nameof(LanguageText), nameof(English));
    internal static string Russian => UiLocalization.Get(nameof(LanguageText), nameof(Russian));
    internal static string Scroll => UiLocalization.Get(nameof(LanguageText), nameof(Scroll));
    internal static string InvalidSaved => UiLocalization.Get(nameof(LanguageText), nameof(InvalidSaved));
}
