// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Security;
using TrackMirror.Constants;
using TrackMirror.Diagnostics;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class DiagnosticsSettingsPanel : SettingsSection
{
    private readonly ActionButton _export = new(DiagnosticsText.Export)
    {
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(220, 40), Padding = new Padding(14, 0, 14, 0),
        Margin = new Padding(0, 4, 0, 4), Anchor = AnchorStyles.Left,
        AccessibleName = DiagnosticsText.Export
    };
    private readonly Label _status = UiTheme.Label(string.Empty, 9, muted: true, wrap: true);
    private readonly Func<IWin32Window, string?> _choose;
    private readonly Func<string, Task> _write;
    private bool exporting;

    internal DiagnosticsSettingsPanel() : this(ChooseDestination,
        path => Task.Run(() => AppDiagnostics.Current.Export(path)), AppDiagnostics.Current.Available) { }

    internal DiagnosticsSettingsPanel(Func<IWin32Window, string?> choose, Func<string, Task> write, bool available)
    {
        _choose = choose;
        _write = write;
        BackColor = UiTheme.Surface;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, RowCount = 4, Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        for (var row = 1; row < 4; row++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(UiTheme.Label(DiagnosticsText.Heading, 15, true), 0, 0);
        layout.Controls.Add(UiTheme.Label(DiagnosticsText.Hint, 9, muted: true, wrap: true), 0, 1);
        layout.Controls.Add(_export, 0, 2);
        layout.Controls.Add(_status, 0, 3);
        _export.Enabled = available;
        _status.Text = available ? string.Empty : DiagnosticsText.Unavailable;
        _status.AccessibleName = DiagnosticsText.Heading;
        Controls.Add(layout);

        _export.Click += async (_, _) => await ExportAsync();
    }

    internal async Task ExportAsync()
    {
        if (exporting || !_export.Enabled || IsDisposed || Disposing)
            return;

        exporting = true;

        try
        {
            var destination = _choose(this);

            if (destination == null)
                return;

            _export.Enabled = false;
            _status.Text = DiagnosticsText.Exporting;
            _status.ForeColor = UiTheme.Muted;
            AppDiagnostics.Write(DiagnosticComponent.Diagnostics, DiagnosticEvent.ExportStarted);

            await _write(destination);

            AppDiagnostics.Write(DiagnosticComponent.Diagnostics, DiagnosticEvent.ExportCompleted);

            if (!IsDisposed && !Disposing)
                _status.Text = DiagnosticsText.Completed;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException
            or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            AppDiagnostics.Write(DiagnosticComponent.Diagnostics, DiagnosticEvent.ExportFailed, error);

            if (!IsDisposed && !Disposing)
            {
                _status.Text = DiagnosticsText.Failed;
                _status.ForeColor = UiTheme.Error;
            }
        }
        finally
        {
            exporting = false;

            if (!IsDisposed && !Disposing)
                _export.Enabled = true;
        }
    }

    private static string? ChooseDestination(IWin32Window owner)
    {
        using var dialog = new SaveFileDialog
        {
            Title = DiagnosticsText.Export, Filter = "ZIP (*.zip)|*.zip", DefaultExt = "zip", AddExtension = true,
            FileName = $"TrackMirror-diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip", OverwritePrompt = true
        };

        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : null;
    }
}
