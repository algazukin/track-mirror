// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Runtime.InteropServices;

namespace TrackMirror.UI.Interop;

// A layered click-through window cannot receive middle presses itself. Keep this hook on
// its own message-loop thread; its callback only updates the gesture and posts a UI update.
internal sealed class MiddleButtonDragListener : IDisposable
{
    private readonly object _gate = new();
    private readonly MiddleButtonDragGesture _gesture = new();
    private readonly ManualResetEventSlim _ready = new();
    private readonly Action<Point, bool> _changed;
    private readonly HookCallback _callback;
    private readonly Thread _thread;
    private IntPtr hook;
    private uint threadId;
    private int disposed;

    private MiddleButtonDragListener(Rectangle target, Action<Point, bool> changed)
    {
        _gesture.Target = target;
        _changed = changed;
        _callback = OnMouse;
        _thread = new Thread(Run) { IsBackground = true, Name = "Countdown middle-button drag" };
        _thread.Start();
    }

    internal static MiddleButtonDragListener? Start(Rectangle target, Action<Point, bool> changed)
    {
        var listener = new MiddleButtonDragListener(target, changed);

        if (listener._ready.Wait(TimeSpan.FromSeconds(2)) && listener.hook != IntPtr.Zero)
            return listener;

        listener.Dispose();

        return null;
    }

    internal void SetTarget(Rectangle target)
    {
        lock (_gate)
            _gesture.Target = target;
    }

    private void Run()
    {
        try
        {
            threadId = GetCurrentThreadId();
            _ = PeekMessage(out _, IntPtr.Zero, 0, 0, 0); // Create the thread's message queue before signaling readiness.
            hook = SetWindowsHookEx(14, _callback, GetModuleHandle(null), 0); // WH_MOUSE_LL
            _ready.Set();

            if (hook != IntPtr.Zero && Volatile.Read(ref disposed) == 0)
            {
                while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
            }
        }
        finally
        {
            if (hook != IntPtr.Zero)
                _ = UnhookWindowsHookEx(hook);

            _ready.Set();
        }
    }

    private IntPtr OnMouse(int code, IntPtr message, IntPtr data)
    {
        if (code < 0 || Volatile.Read(ref disposed) != 0)
            return CallNextHookEx(IntPtr.Zero, code, message, data);

        var pointer = Marshal.PtrToStructure<MouseData>(data).Position;
        bool consume;
        Point? position;
        bool completed;

        lock (_gate)
            consume = _gesture.Process((int)message, pointer, out position, out completed);

        if (position is { } next)
            _changed(next, completed);

        return consume ? new IntPtr(1) : CallNextHookEx(IntPtr.Zero, code, message, data);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        SetTarget(Rectangle.Empty);

        if (threadId != 0)
            _ = PostThreadMessage(threadId, 0x0012, IntPtr.Zero, IntPtr.Zero); // WM_QUIT

        if (_thread.Join(TimeSpan.FromSeconds(1)))
            _ready.Dispose();
    }

    private delegate IntPtr HookCallback(int code, IntPtr message, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseData
    {
        internal Point Position;
        internal uint Data;
        internal uint Flags;
        internal uint Time;
        internal UIntPtr Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        internal IntPtr Window;
        internal uint Message;
        internal UIntPtr WParam;
        internal IntPtr LParam;
        internal uint Time;
        internal Point Position;
        internal uint Private;
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int type, HookCallback callback, IntPtr module, uint thread);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll", EntryPoint = "GetMessageW")]
    private static extern int GetMessage(out NativeMessage message, IntPtr window, uint first, uint last);

    [DllImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint first, uint last, uint remove);

    [DllImport("user32.dll", EntryPoint = "PostThreadMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint thread, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? name);
}
