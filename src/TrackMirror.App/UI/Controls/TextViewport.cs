// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Runtime.InteropServices;
using TrackMirror.Constants;

namespace TrackMirror.UI.Controls;

internal sealed class TextViewport : TextBox
{
    private int wheelRemainder;

    internal event Action? ViewportChanged;

    internal int TopLine => IsHandleCreated
        ? (int)SendMessage(Handle, NativeWindowConstants.TextFirstVisibleLine, IntPtr.Zero, IntPtr.Zero) : 0;

    internal int LineCount => IsHandleCreated
        ? (int)SendMessage(Handle, NativeWindowConstants.TextLineCount, IntPtr.Zero, IntPtr.Zero) : 1;

    internal int PageLines => Math.Max(1, (ClientSize.Height - (int)(4 * DeviceDpi / 96f)) / Font.Height);

    internal TextViewport()
    {
        Multiline = true;

        ReadOnly = true;

        BorderStyle = BorderStyle.None;

        ScrollBars = ScrollBars.None;

        Dock = DockStyle.Fill;

        TabStop = false;
    }

    internal void ScrollTo(int line)
    {
        if (!IsHandleCreated)
            return;

        var target = Math.Clamp(line, 0, Math.Max(0, LineCount - PageLines));

        _ = SendMessage(Handle, NativeWindowConstants.TextLineScroll, IntPtr.Zero, new IntPtr(target - TopLine));

        ViewportChanged?.Invoke();
    }

    internal void ScrollWheel(int delta)
    {
        wheelRemainder += delta;

        var notches = wheelRemainder / 120;

        wheelRemainder %= 120;

        var lines = SystemInformation.MouseWheelScrollLines;

        ScrollTo(TopLine - notches * (lines < 0 ? PageLines : lines));
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollWheel(e.Delta);

        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }

        base.OnMouseWheel(e);
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);

        if (message.Msg is NativeWindowConstants.KeyDownMessage or NativeWindowConstants.CharacterMessage
            or NativeWindowConstants.SizeMessage or NativeWindowConstants.TimerMessage
            or NativeWindowConstants.LeftButtonUpMessage or NativeWindowConstants.TextSetSelection
            or NativeWindowConstants.TextLineScroll or NativeWindowConstants.TextScrollCaret
            or NativeWindowConstants.SetTextMessage)
        {
            ViewportChanged?.Invoke();
        }
    }

    [DllImport(NativeWindowConstants.User32)]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
