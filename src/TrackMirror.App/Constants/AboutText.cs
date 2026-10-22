// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class AboutText
{
    internal static string Heading => UiLocalization.Get(nameof(AboutText), nameof(Heading));

    internal static string Version => UiLocalization.Get(nameof(AboutText), nameof(Version));

    internal static string Credits => UiLocalization.Get(nameof(AboutText), nameof(Credits));

    internal static string UpdateChannel => UiLocalization.Get(nameof(AboutText), nameof(UpdateChannel));

    internal static string Stable => UiLocalization.Get(nameof(AboutText), nameof(Stable));

    internal static string PreRelease => UiLocalization.Get(nameof(AboutText), nameof(PreRelease));

    internal static string ChannelHint => UiLocalization.Get(nameof(AboutText), nameof(ChannelHint));

    internal static string CheckForUpdates => UiLocalization.Get(nameof(AboutText), nameof(CheckForUpdates));

    internal static string UpdateStatus => UiLocalization.Get(nameof(AboutText), nameof(UpdateStatus));

    internal static string ManualCheck => UiLocalization.Get(nameof(AboutText), nameof(ManualCheck));

    internal static string Checking => UiLocalization.Get(nameof(AboutText), nameof(Checking));

    internal static string Available => UiLocalization.Get(nameof(AboutText), nameof(Available));

    internal static string UpToDate => UiLocalization.Get(nameof(AboutText), nameof(UpToDate));

    internal static string NoReleases => UiLocalization.Get(nameof(AboutText), nameof(NoReleases));

    internal static string TimedOut => UiLocalization.Get(nameof(AboutText), nameof(TimedOut));

    internal static string RateLimited => UiLocalization.Get(nameof(AboutText), nameof(RateLimited));

    internal static string Failed => UiLocalization.Get(nameof(AboutText), nameof(Failed));

    internal static string BrowserFailed => UiLocalization.Get(nameof(AboutText), nameof(BrowserFailed));

    internal static string InvalidChannel => UiLocalization.Get(nameof(AboutText), nameof(InvalidChannel));
}
