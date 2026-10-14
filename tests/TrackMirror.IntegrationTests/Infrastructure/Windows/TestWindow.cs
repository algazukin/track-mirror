// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Runtime.InteropServices;

namespace TrackMirror.IntegrationTests.Infrastructure.Windows;

internal static class TestWindow
{
    internal static void Show(Form form)
    {
        const int extendedStyle = -20;

        const int noActivate = 0x08000000;

        SetWindowLong(form.Handle, extendedStyle, GetWindowLong(form.Handle, extendedStyle) | noActivate);

        form.Show();
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr window, int index, int value);
}
