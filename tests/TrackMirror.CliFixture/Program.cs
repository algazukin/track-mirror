// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Runtime.InteropServices;
using System.Text;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        GetStartupInfo(out var startup);
        var mode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "behavior.txt"));

        if (mode == "hold")
        {
            await Task.Delay(TimeSpan.FromSeconds(15));
            return 0;
        }

        if ((startup.Flags & 0x80) == 0 || (startup.Flags & 1) == 0 || startup.ShowWindow != 0)
        {
            return 31;
        }

        if (mode == "hang")
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "pid.txt"), Environment.ProcessId.ToString());
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }

        if (mode == "nonzero")
        {
            return 23;
        }

        if (mode == "invalid" || mode == "large" || mode == "error")
        {
            Console.Write(mode == "invalid" ? "synthetic invalid status"
                : mode == "large" ? new string('x', 70000) : "{\"error\":\"synthetic error\"}");
            return 0;
        }

        if (mode == "stderr")
        {
            Console.Error.Write(new string('e', 32000));
        }

        Console.Write(args.Single() == "status"
            ? "{\"tracking\":true,\"active_project\":{\"id\":\"проект-demo\"},\"active_task\":{\"id\":7}}"
            : "{\"ok\":true}");
        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public uint Size;
        public IntPtr Reserved, Desktop, Title;
        public uint X, Y, Width, Height, Columns, Rows, Fill, Flags;
        public ushort ShowWindow, ReservedSize;
        public IntPtr ReservedBytes, Input, Output, Error;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetStartupInfoW")]
    private static extern void GetStartupInfo(out StartupInfo startup);
}
