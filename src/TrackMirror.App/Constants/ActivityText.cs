// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class ActivityText
{
    internal static string Tab => UiLocalization.Get(nameof(ActivityText), nameof(Tab));
    internal static string Heading => UiLocalization.Get(nameof(ActivityText), nameof(Heading));
    internal static string Description => UiLocalization.Get(nameof(ActivityText), nameof(Description));
    internal static string Account => UiLocalization.Get(nameof(ActivityText), nameof(Account));
    internal static string AccountScroll => UiLocalization.Get(nameof(ActivityText), nameof(AccountScroll));
    internal static string Day => UiLocalization.Get(nameof(ActivityText), nameof(Day));
    internal static string SelectDay => UiLocalization.Get(nameof(ActivityText), nameof(SelectDay));
    internal static string PreviousDay => UiLocalization.Get(nameof(ActivityText), nameof(PreviousDay));
    internal static string NextDay => UiLocalization.Get(nameof(ActivityText), nameof(NextDay));
    internal static string Chart => UiLocalization.Get(nameof(ActivityText), nameof(Chart));
    internal static string Overall => UiLocalization.Get(nameof(ActivityText), nameof(Overall));
    internal static string Mouse => UiLocalization.Get(nameof(ActivityText), nameof(Mouse));
    internal static string Keyboard => UiLocalization.Get(nameof(ActivityText), nameof(Keyboard));
    internal static string Tracked => UiLocalization.Get(nameof(ActivityText), nameof(Tracked));
    internal const string PercentFormat = "{0:0}%";
    internal const string HoursFormat = "{0:00}:00";
    internal static string DurationFormat => UiLocalization.Get(nameof(ActivityText), nameof(DurationFormat));
    internal static string SummaryFormat => UiLocalization.Get(nameof(ActivityText), nameof(SummaryFormat));
    internal static string IntervalFormat => UiLocalization.Get(nameof(ActivityText), nameof(IntervalFormat));
    internal static string RecordsFormat => UiLocalization.Get(nameof(ActivityText), nameof(RecordsFormat));
    internal static string Empty => UiLocalization.Get(nameof(ActivityText), nameof(Empty));
    internal static string Waiting => UiLocalization.Get(nameof(ActivityText), nameof(Waiting));
    internal const string NoValue = "—";
    internal static string Hint => UiLocalization.Get(nameof(ActivityText), nameof(Hint));
    internal static string ArchiveHint => UiLocalization.Get(nameof(ActivityText), nameof(ArchiveHint));
}
