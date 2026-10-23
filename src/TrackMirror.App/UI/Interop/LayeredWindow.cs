// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.ComponentModel;
using System.Runtime.InteropServices;
using TrackMirror.Constants;

namespace TrackMirror.UI.Interop;

internal static class LayeredWindow
{
    internal static void Update(IntPtr window, Bitmap surface)
    {
        var screen = GetDC(IntPtr.Zero);

        if (screen == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        var memory = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previous = IntPtr.Zero;

        try
        {
            memory = CreateCompatibleDC(screen);

            if (memory == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            bitmap = surface.GetHbitmap(Color.FromArgb(0));
            previous = SelectObject(memory, bitmap);

            if (previous == IntPtr.Zero || previous == new IntPtr(-1))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var size = surface.Size;
            var origin = Point.Empty;
            var blend = new BlendFunction { SourceConstantAlpha = 255, AlphaFormat = 1 }; // AC_SRC_ALPHA

            if (!UpdateLayeredWindow(window, screen, IntPtr.Zero, ref size, memory, ref origin, 0, ref blend, 2))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            if (previous != IntPtr.Zero && previous != new IntPtr(-1))
                _ = SelectObject(memory, previous);

            if (bitmap != IntPtr.Zero)
                _ = DeleteObject(bitmap);

            if (memory != IntPtr.Zero)
                _ = DeleteDC(memory);

            _ = ReleaseDC(IntPtr.Zero, screen);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        internal byte BlendOp;
        internal byte BlendFlags;
        internal byte SourceConstantAlpha;
        internal byte AlphaFormat;
    }

    [DllImport(NativeWindowConstants.User32, SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport(NativeWindowConstants.User32)]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr item);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr item);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport(NativeWindowConstants.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDC, IntPtr destinationPoint,
        ref Size size, IntPtr sourceDC, ref Point sourcePoint, uint colorKey, ref BlendFunction blend, uint flags);
}
