// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Models;

public sealed record HubstaffActivity(
    string Account,
    DateTime StartedUtc,
    int TrackedSeconds,
    int MouseSeconds,
    int KeyboardSeconds,
    int OverallSeconds,
    DateTime ObservedUtc);
