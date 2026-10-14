// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class ActivitySql
{
    internal const string SelectAll = """
        SELECT account,started_utc,tracked_seconds,mouse_seconds,keyboard_seconds,overall_seconds,observed_utc
        FROM activity_intervals ORDER BY started_utc;
        """;

    internal const string Upsert = """
        INSERT INTO activity_intervals(account_key,account,started_utc,tracked_seconds,mouse_seconds,
        keyboard_seconds,overall_seconds,observed_utc)
        VALUES($key,$account,$started,$tracked,$mouse,$keyboard,$overall,$observed)
        ON CONFLICT(account_key,started_utc) DO UPDATE SET account=excluded.account,
        tracked_seconds=excluded.tracked_seconds,mouse_seconds=excluded.mouse_seconds,
        keyboard_seconds=excluded.keyboard_seconds,overall_seconds=excluded.overall_seconds,
        observed_utc=excluded.observed_utc
        WHERE excluded.tracked_seconds>=activity_intervals.tracked_seconds
        AND excluded.observed_utc>=activity_intervals.observed_utc
        AND (excluded.account!=activity_intervals.account
        OR excluded.tracked_seconds!=activity_intervals.tracked_seconds
        OR excluded.mouse_seconds!=activity_intervals.mouse_seconds
        OR excluded.keyboard_seconds!=activity_intervals.keyboard_seconds
        OR excluded.overall_seconds!=activity_intervals.overall_seconds);
        """;
}
