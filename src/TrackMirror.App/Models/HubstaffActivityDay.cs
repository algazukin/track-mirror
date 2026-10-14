// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Models;

internal sealed class HubstaffActivityDay
{
    internal DateTime Date { get; }

    internal List<HubstaffActivity> Entries { get; }

    internal long TrackedSeconds { get; }

    internal double OverallPercent { get; }

    internal double MousePercent { get; }

    internal double KeyboardPercent { get; }

    internal HubstaffActivityDay(DateTime date, IEnumerable<HubstaffActivity> entries)
    {
        Date = date.Date;

        Entries = entries.Where(entry => entry.StartedUtc.ToLocalTime().Date == Date)
            .OrderBy(entry => entry.StartedUtc).ToList();

        TrackedSeconds = Entries.Sum(entry => (long)entry.TrackedSeconds);

        if (TrackedSeconds == 0)
            return;

        OverallPercent = Entries.Sum(entry => (long)entry.OverallSeconds) * 100d / TrackedSeconds;

        MousePercent = Entries.Sum(entry => (long)entry.MouseSeconds) * 100d / TrackedSeconds;

        KeyboardPercent = Entries.Sum(entry => (long)entry.KeyboardSeconds) * 100d / TrackedSeconds;
    }
}
