// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class CountdownText
{
    internal static string Heading => UiLocalization.Get(nameof(CountdownText), nameof(Heading));

    internal static string Enabled => UiLocalization.Get(nameof(CountdownText), nameof(Enabled));

    internal static string MiddleDragUnavailable => UiLocalization.Get(nameof(CountdownText), nameof(MiddleDragUnavailable));

    internal static string Mode => UiLocalization.Get(nameof(CountdownText), nameof(Mode));

    internal static string Default => UiLocalization.Get(nameof(CountdownText), nameof(Default));

    internal static string Detailed => UiLocalization.Get(nameof(CountdownText), nameof(Detailed));

    internal static string BeforeScreenshot => UiLocalization.Get(nameof(CountdownText), nameof(BeforeScreenshot));

    internal static string ShowBefore => UiLocalization.Get(nameof(CountdownText), nameof(ShowBefore));

    internal static string HideBefore => UiLocalization.Get(nameof(CountdownText), nameof(HideBefore));

    internal static string HideSeconds => UiLocalization.Get(nameof(CountdownText), nameof(HideSeconds));

    internal static string MiddleDrag => UiLocalization.Get(nameof(CountdownText), nameof(MiddleDrag));

    internal static string InteractionHint => UiLocalization.Get(nameof(CountdownText), nameof(InteractionHint));

    internal static string Hint => UiLocalization.Get(nameof(CountdownText), nameof(Hint));

    internal static string Highlight => UiLocalization.Get(nameof(CountdownText), nameof(Highlight));

    internal static string Scale => UiLocalization.Get(nameof(CountdownText), nameof(Scale));

    internal static string BackgroundOpacity => UiLocalization.Get(nameof(CountdownText), nameof(BackgroundOpacity));

    internal static string Limitation => UiLocalization.Get(nameof(CountdownText), nameof(Limitation));

    internal static string Scheduled => UiLocalization.Get(nameof(CountdownText), nameof(Scheduled));

    internal static string Waiting => UiLocalization.Get(nameof(CountdownText), nameof(Waiting));

    internal static string Unavailable => UiLocalization.Get(nameof(CountdownText), nameof(Unavailable));

    internal static string Stopped => UiLocalization.Get(nameof(CountdownText), nameof(Stopped));

    internal static string Disabled => UiLocalization.Get(nameof(CountdownText), nameof(Disabled));

    internal static string Hide => UiLocalization.Get(nameof(CountdownText), nameof(Hide));

    internal static string Reset => UiLocalization.Get(nameof(CountdownText), nameof(Reset));

    internal static string TopLeft => UiLocalization.Get(nameof(CountdownText), nameof(TopLeft));

    internal static string TopRight => UiLocalization.Get(nameof(CountdownText), nameof(TopRight));

    internal static string BottomLeft => UiLocalization.Get(nameof(CountdownText), nameof(BottomLeft));

    internal static string BottomRight => UiLocalization.Get(nameof(CountdownText), nameof(BottomRight));

    internal static string InvalidSettings => UiLocalization.Get(nameof(CountdownText), nameof(InvalidSettings));
}
