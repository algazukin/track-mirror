// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class CalendarText
{
    internal static string DateFilter => UiLocalization.Get(nameof(CalendarText), nameof(DateFilter));
    internal static string AllDates => UiLocalization.Get(nameof(CalendarText), nameof(AllDates));
    internal const string ExpandSuffix = "  ▾";
    internal const string Reset = "×";
    internal static string ResetDate => UiLocalization.Get(nameof(CalendarText), nameof(ResetDate));
    internal const string Previous = "‹";
    internal const string Next = "›";
    internal static string PreviousMonth => UiLocalization.Get(nameof(CalendarText), nameof(PreviousMonth));
    internal static string NextMonth => UiLocalization.Get(nameof(CalendarText), nameof(NextMonth));
    internal static string Today => UiLocalization.Get(nameof(CalendarText), nameof(Today));
    internal static string SelectStart => UiLocalization.Get(nameof(CalendarText), nameof(SelectStart));
    internal static string SelectEnd => UiLocalization.Get(nameof(CalendarText), nameof(SelectEnd));
    internal static string RangeFormat => UiLocalization.Get(nameof(CalendarText), nameof(RangeFormat));
    internal static string NoRangeMatchesFormat =>
        UiLocalization.Get(nameof(CalendarText), nameof(NoRangeMatchesFormat));
    internal static string NoMatchesFormat => UiLocalization.Get(nameof(CalendarText), nameof(NoMatchesFormat));
    internal static string NoMatchesHint => UiLocalization.Get(nameof(CalendarText), nameof(NoMatchesHint));
}
