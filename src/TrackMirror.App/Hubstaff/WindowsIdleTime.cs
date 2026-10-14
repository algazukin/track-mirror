// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Runtime.InteropServices;
using TrackMirror.Constants;

namespace TrackMirror.Hubstaff;

internal static class WindowsIdleTime
{
    internal static IdleObservation? Read()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };

        return GetLastInputInfo(ref info)
            ? new IdleObservation(TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time)),
                info.Time) : null;
    }

    [DllImport(NativeWindowConstants.User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);
}
