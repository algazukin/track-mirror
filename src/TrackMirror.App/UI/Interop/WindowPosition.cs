// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Runtime.InteropServices;
using TrackMirror.Constants;

namespace TrackMirror.UI.Interop;

internal static class WindowPosition
{
    internal static void KeepAboveWithoutActivation(IntPtr handle)
    {
        _ = SetWindowPos(handle, new IntPtr(NativeWindowConstants.TopMost), 0, 0, 0, 0,
            NativeWindowConstants.NoSize | NativeWindowConstants.NoMove | NativeWindowConstants.NoActivate);
    }

    [DllImport(NativeWindowConstants.User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
