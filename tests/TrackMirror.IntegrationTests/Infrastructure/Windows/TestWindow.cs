// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Runtime.InteropServices;

namespace TrackMirror.IntegrationTests.Infrastructure.Windows;

internal static class TestWindow
{
    private const int ExtendedStyle = -20;

    internal static void Show(Form form)
    {
        const int noActivate = 0x08000000;

        SetWindowLong(form.Handle, ExtendedStyle, GetWindowLong(form.Handle, ExtendedStyle) | noActivate);

        form.Show();
    }

    internal static bool IsTopmost(Form form)
    {
        const int topmost = 0x00000008;

        return (GetWindowLong(form.Handle, ExtendedStyle) & topmost) != 0;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr window, int index, int value);
}
