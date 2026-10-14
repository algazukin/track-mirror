// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Hubstaff;

internal interface IHubstaffCli
{
    Task<HubstaffTrackingStatus> StatusAsync(string path, CancellationToken token);

    Task ExecuteAsync(string path, string command, CancellationToken token);
}
