// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class DateFormats
{
    internal const string Time = "HH:mm:ss";
    internal static string HistoryRow => UiLocalization.Get(nameof(DateFormats), nameof(HistoryRow));
    internal static string HistoryDetails => UiLocalization.Get(nameof(DateFormats), nameof(HistoryDetails));
    internal static string FullDateTime => UiLocalization.Get(nameof(DateFormats), nameof(FullDateTime));
    internal const string CaptureFileName = "yyyy-MM-dd'T'HHmmss";
    internal static string CalendarDate => UiLocalization.Get(nameof(DateFormats), nameof(CalendarDate));
    internal const string CalendarMonth = "MMMM yyyy";
    internal const string AccessibleCalendarDate = "D";
    internal const string BackupTimestamp = "yyyyMMddHHmmssfff";
}
