// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Diagnostics;

internal sealed class DiagnosticExceptionHandlers : IDisposable
{
    private readonly DiagnosticLog _log;
    private readonly Action _exitUI;

    internal DiagnosticExceptionHandlers(DiagnosticLog log, Action exitUI)
    {
        _log = log;
        _exitUI = exitUI;

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += OnUI;
        AppDomain.CurrentDomain.UnhandledException += OnBackground;
        TaskScheduler.UnobservedTaskException += OnTask;
    }

    private void OnUI(object sender, ThreadExceptionEventArgs args)
    {
        _log.Write(DiagnosticComponent.UI, DiagnosticEvent.UnhandledUI, args.Exception, level: DiagnosticLevel.Fatal);

        _exitUI();
    }

    private void OnBackground(object sender, UnhandledExceptionEventArgs args) =>
        _log.Write(DiagnosticComponent.Application, DiagnosticEvent.UnhandledBackground,
            args.ExceptionObject as Exception, level: DiagnosticLevel.Fatal);

    private void OnTask(object? sender, UnobservedTaskExceptionEventArgs args) =>
        _log.Write(DiagnosticComponent.Application, DiagnosticEvent.UnobservedTask, args.Exception, level: DiagnosticLevel.Error);

    public void Dispose()
    {
        Application.ThreadException -= OnUI;
        AppDomain.CurrentDomain.UnhandledException -= OnBackground;
        TaskScheduler.UnobservedTaskException -= OnTask;
    }
}
