// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Diagnostics;

namespace TrackMirror.App;

internal static class ChildProcessLaunch
{
    internal static readonly object Gate = new();

    internal static Process? Start(ProcessStartInfo options)
    {
        // Coordinate ordinary Process.Start with the CLI's temporary inheritable pipe handles.
        lock (Gate)
        {
            return Process.Start(options);
        }
    }
}
