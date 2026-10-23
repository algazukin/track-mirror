// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.ComponentModel;
using System.Security;
using TrackMirror.App;

namespace TrackMirror.Diagnostics;

internal static class AppDiagnostics
{
    internal static DiagnosticLog Current { get; private set; } = DiagnosticLog.Disabled;

    internal static void Initialize(string? directory = null)
    {
        try
        {
            Current = new DiagnosticLog(directory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrackMirror", "installations",
                SingleInstance.Identifier(AppContext.BaseDirectory), "diagnostics"));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException
            or ArgumentException or Win32Exception)
        {
            Current = DiagnosticLog.Disabled;
        }
    }

    internal static void Write(DiagnosticComponent component, DiagnosticEvent diagnosticEvent, Exception? error = null,
        DiagnosticMetrics? metrics = null, DiagnosticLevel? level = null) =>
        Current.Write(component, diagnosticEvent, error, metrics, level);
}
