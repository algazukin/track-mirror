// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Hubstaff;

internal sealed record HubstaffControlStatus(HubstaffControlState State, int Seconds = 0, string FailureCode = "");
