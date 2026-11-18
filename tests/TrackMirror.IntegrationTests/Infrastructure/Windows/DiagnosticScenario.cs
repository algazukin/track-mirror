// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Runtime.CompilerServices;
using TrackMirror.Diagnostics;

namespace TrackMirror.IntegrationTests.Infrastructure.Windows;

internal static class DiagnosticScenario
{
    internal static int Run(string root, string scenario)
    {
        // Arrange
        var directory = Path.Combine(root, "diagnostics");
        AppDiagnostics.Initialize(directory);
        using var log = AppDiagnostics.Current;
        var uiFailures = 0;
        using var handlers = new DiagnosticExceptionHandlers(log, () =>
        {
            if (scenario == "diagnostics-ui-shutdown" && ++uiFailures == 1)
                FailShutdownAsync();
            else
                Application.Exit();
        });

        // Act
        switch (scenario)
        {
            case "diagnostics-ui":
            case "diagnostics-ui-shutdown":
                using (var form = new QuietForm())
                using (var deadline = new System.Windows.Forms.Timer { Interval = 2000 })
                {
                    deadline.Tick += (_, _) => Environment.Exit(3);
                    deadline.Start();
                    form.Shown += (_, _) => form.BeginInvoke(() => throw new InvalidOperationException("private-crash-canary"));
                    Application.Run(form);
                }
                break;
            case "diagnostics-background":
                new Thread(() => throw new IOException("private-crash-canary")) { IsBackground = true }.Start();
                Thread.Sleep(Timeout.Infinite);
                break;
            case "diagnostics-task":
                LoseFaultedTask();

                for (var attempt = 0; attempt < 20; attempt++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(10);
                }
                break;
            case "diagnostics-write":
                using (var shared = new DiagnosticLog(directory, new DiagnosticLogOptions { MaximumFileBytes = 4096, MaximumFiles = 3 }))
                    for (var index = 0; index < 100; index++)
                        shared.Write(DiagnosticComponent.Application, DiagnosticEvent.Started);
                break;
        }

        // Assert: the parent test checks the exit code and safe records after this process exits.
        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LoseFaultedTask()
    {
        var task = Task.Run(() => throw new InvalidOperationException("private-crash-canary"));
        ((IAsyncResult)task).AsyncWaitHandle.WaitOne();
    }

    private static async void FailShutdownAsync()
    {
        await Task.Yield();
        throw new IOException("private-crash-canary");
    }

    private sealed class QuietForm : Form
    {
        protected override bool ShowWithoutActivation => true;
        internal QuietForm() { Opacity = 0; ShowInTaskbar = false; }
    }
}
