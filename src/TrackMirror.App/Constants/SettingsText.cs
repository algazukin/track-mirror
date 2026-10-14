// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class SettingsText
{
    internal static string HistoryTab => UiLocalization.Get(nameof(SettingsText), nameof(HistoryTab));
    internal static string SettingsTab => UiLocalization.Get(nameof(SettingsText), nameof(SettingsTab));
    internal static string Heading => UiLocalization.Get(nameof(SettingsText), nameof(Heading));
    internal static string Description => UiLocalization.Get(nameof(SettingsText), nameof(Description));
    internal static string StartWithHubstaff => UiLocalization.Get(nameof(SettingsText), nameof(StartWithHubstaff));
    internal static string StartupHint => UiLocalization.Get(nameof(SettingsText), nameof(StartupHint));
    internal static string StartupFailedPrefix => UiLocalization.Get(nameof(SettingsText), nameof(StartupFailedPrefix));
    internal static string NotificationsEnabled =>
        UiLocalization.Get(nameof(SettingsText), nameof(NotificationsEnabled));
    internal static string NotificationsHint => UiLocalization.Get(nameof(SettingsText), nameof(NotificationsHint));
    internal static string Timeout => UiLocalization.Get(nameof(SettingsText), nameof(Timeout));
    internal static string Seconds => UiLocalization.Get(nameof(SettingsText), nameof(Seconds));
    internal static string TimeoutHint => UiLocalization.Get(nameof(SettingsText), nameof(TimeoutHint));
    internal static string Saved => UiLocalization.Get(nameof(SettingsText), nameof(Saved));
    internal static string SessionDisabled => UiLocalization.Get(nameof(SettingsText), nameof(SessionDisabled));
    internal static string LoadFailed => UiLocalization.Get(nameof(SettingsText), nameof(LoadFailed));
    internal static string SaveFailedPrefix => UiLocalization.Get(nameof(SettingsText), nameof(SaveFailedPrefix));
}
