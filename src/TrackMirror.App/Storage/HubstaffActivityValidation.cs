// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Models;

namespace TrackMirror.Storage;

internal static class HubstaffActivityValidation
{
    internal static bool Valid(HubstaffActivity? entry) => entry != null && !string.IsNullOrEmpty(entry.Account)
        && entry.TrackedSeconds is > 0 and <= 600 && entry.OverallSeconds >= 0
        && entry.OverallSeconds <= entry.TrackedSeconds && entry.MouseSeconds >= 0 && entry.KeyboardSeconds >= 0
        && entry.MouseSeconds <= entry.OverallSeconds && entry.KeyboardSeconds <= entry.OverallSeconds
        && entry.OverallSeconds <= entry.MouseSeconds + entry.KeyboardSeconds
        && entry.StartedUtc.Kind == DateTimeKind.Utc && entry.ObservedUtc.Kind == DateTimeKind.Utc
        && entry.StartedUtc <= DateTime.MaxValue.AddSeconds(-600);
}
