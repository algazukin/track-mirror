// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class HubstaffActivityNames
{
    internal const string SourceFile = "TrackedActivity.xml";
    internal const string CacheFile = "hubstaff-activity.json";
    internal const string HistoryFile = "hubstaff-activity-history.json";
    internal const string DamagedHistorySuffix = ".corrupt-";
    internal const string JsonExtension = ".json";
    internal const string Items = "items";
    internal const string Item = "item";
    internal const string Start = "t.start";
    internal const string Reading = "t.reading";
    internal const string BreakId = "t.break_id";
    internal const string DateContainer = "ptime_date";
    internal const string Date = "date";
    internal const string TimeContainer = "ptime_time_duration";
    internal const string Hours = "time_duration_hours";
    internal const string Minutes = "time_duration_minutes";
    internal const string Seconds = "time_duration_seconds";
    internal const string Tracked = "tracked";
    internal const string Mouse = "mouse";
    internal const string Keyboard = "keyboard";
    internal const string Overall = "overall";
    internal const string DateFormat = "yyyyMMdd";
}
