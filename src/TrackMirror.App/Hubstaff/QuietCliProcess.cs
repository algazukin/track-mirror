// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using TrackMirror.App;

namespace TrackMirror.Hubstaff;

internal sealed class QuietCliProcess : IDisposable
{
    internal Process Process { get; }
    internal StreamReader StandardOutput { get; }
    internal StreamReader StandardError { get; }

    private QuietCliProcess(Process process, AnonymousPipeServerStream output, AnonymousPipeServerStream error)
    {
        Process = process;
        StandardOutput = new StreamReader(output, Encoding.UTF8);
        StandardError = new StreamReader(error, Encoding.UTF8);
    }

    internal static QuietCliProcess Start(string path, string command)
    {
        lock (ChildProcessLaunch.Gate)
        {
            return StartCore(path, command);
        }
    }

    private static QuietCliProcess StartCore(string path, string command)
    {
        AnonymousPipeServerStream? output = null;
        AnonymousPipeServerStream? error = null;
        Process? process = null;
        var information = new ProcessInformation();
        var attributes = IntPtr.Zero;
        var handles = IntPtr.Zero;
        var initialized = false;

        try
        {
            output = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            error = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            using var input = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);

            nuint size = 0;
            _ = InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            attributes = Marshal.AllocHGlobal(checked((int)size));

            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            initialized = true;
            handles = Marshal.AllocHGlobal(IntPtr.Size * 3);
            Marshal.WriteIntPtr(handles, 0, input.ClientSafePipeHandle.DangerousGetHandle());
            Marshal.WriteIntPtr(handles, IntPtr.Size, output.ClientSafePipeHandle.DangerousGetHandle());
            Marshal.WriteIntPtr(handles, IntPtr.Size * 2, error.ClientSafePipeHandle.DangerousGetHandle());

            // Inherit only this request's pipe ends, including when requests run concurrently.
            if (!UpdateProcThreadAttribute(attributes, 0, 0x20002, handles, (nuint)(IntPtr.Size * 3), IntPtr.Zero, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var startup = new StartupInfoEx
            {
                Info = new StartupInfo
                {
                    Size = (uint)Marshal.SizeOf<StartupInfoEx>(),
                    Flags = 0x1 | 0x80 | 0x100, // USESHOWWINDOW | FORCEOFFFEEDBACK | USESTDHANDLES
                    Input = input.ClientSafePipeHandle.DangerousGetHandle(),
                    Output = output.ClientSafePipeHandle.DangerousGetHandle(),
                    Error = error.ClientSafePipeHandle.DangerousGetHandle()
                },
                Attributes = attributes
            };

            // Start suspended so even a fast CLI cannot exit before Process owns its handle.
            var arguments = new StringBuilder($"\"{path}\" {command}");

            if (!CreateProcess(path, arguments, IntPtr.Zero, IntPtr.Zero, true,
                0x08000000 | 0x00080000 | 0x4, // NO_WINDOW | EXTENDED_STARTUPINFO_PRESENT | SUSPENDED
                IntPtr.Zero, null, ref startup, out information))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            process = Process.GetProcessById((int)information.ProcessId);
            _ = process.Handle;
            process.EnableRaisingEvents = true;

            output.DisposeLocalCopyOfClientHandle();
            error.DisposeLocalCopyOfClientHandle();
            input.DisposeLocalCopyOfClientHandle();

            if (ResumeThread(information.Thread) == uint.MaxValue)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return new QuietCliProcess(process, output, error);
        }
        catch
        {
            if (information.Process != IntPtr.Zero)
            {
                _ = TerminateProcess(information.Process, 1);
            }

            process?.Dispose();
            output?.Dispose();
            error?.Dispose();
            throw;
        }
        finally
        {
            if (information.Thread != IntPtr.Zero)
            {
                _ = CloseHandle(information.Thread);
            }

            if (information.Process != IntPtr.Zero)
            {
                _ = CloseHandle(information.Process);
            }

            if (initialized)
            {
                DeleteProcThreadAttributeList(attributes);
            }

            Marshal.FreeHGlobal(attributes);
            Marshal.FreeHGlobal(handles);
        }
    }

    internal void Terminate()
    {
        try
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or AggregateException)
        {
            // Cancellation callbacks must not throw, including partial process-tree termination failures.
        }
    }

    public void Dispose()
    {
        StandardOutput.Dispose();
        StandardError.Dispose();
        Process.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        internal uint Size;
        internal IntPtr Reserved, Desktop, Title;
        internal uint X, Y, Width, Height, Columns, Rows, Fill, Flags;
        internal ushort ShowWindow, ReservedSize;
        internal IntPtr ReservedBytes, Input, Output, Error;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        internal StartupInfo Info;
        internal IntPtr Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        internal IntPtr Process, Thread;
        internal uint ProcessId, ThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref nuint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, nuint attribute,
        IntPtr value, nuint size, IntPtr previous, IntPtr returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes,
        IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, IntPtr environment,
        string? directory, ref StartupInfoEx startup, out ProcessInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
