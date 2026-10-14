// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Storage.Management;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class StorageSettingsPanel : SettingsSection
{
    private readonly Label _status = UiTheme.Label(string.Empty, 9, muted: true, wrap: true);
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Top, Height = 8, Visible = false };
    private readonly ActionButton _cancel = new(DataManagementText.Cancel) { Width = 120, Visible = false };
    private readonly FlowLayoutPanel _actions = new()
    {
        Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = true,
        Margin = Padding.Empty
    };
    private readonly ThemedCheckBox _retention = Option(DataManagementText.Retention);
    private readonly ThemedCheckBox _keepFlagged = Option(DataManagementText.KeepFlagged);
    private readonly ThemedCheckBox _keepUnread = Option(DataManagementText.KeepUnread);
    private readonly NumericUpDown _days = new()
    {
        Minimum = 1,
        Maximum = DataManagementNames.MaximumDays,
        Width = 86,
        MinimumSize = new Size(86, 0),
        BackColor = UiTheme.Raised,
        ForeColor = UiTheme.Text,
        BorderStyle = BorderStyle.FixedSingle,
        AccessibleName = DataManagementText.Days,
        Margin = new Padding(0, 5, 12, 0)
    };
    private readonly ActionButton _cleanup = new(DataManagementText.Cleanup) { Width = 164 };

    internal event Action<bool, int, bool, bool>? RetentionChanged;
    internal event Action<DataOperationKind>? OperationRequested;
    internal event Action? CancelRequested;

    internal StorageSettingsPanel(AppSettings settings, string dataRoot)
    {
        BackColor = UiTheme.Surface;

        _retention.Checked = settings.RetentionEnabled;

        _days.Value = settings.RetentionDays;

        _keepFlagged.Checked = settings.RetentionKeepFlagged;

        _keepUnread.Checked = settings.RetentionKeepUnread;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 12,
            Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _status.Visible = false;

        foreach (var height in new[] { 36, 24, 36, 0, 0, 0, 0, 32, 0, 30, 30, 0 })
        {
            layout.RowStyles.Add(new RowStyle(height == 0 ? SizeType.AutoSize : SizeType.Absolute, height));
        }

        layout.Controls.Add(UiTheme.Label(DataManagementText.Heading, 15, true), 0, 0);

        layout.Controls.Add(UiTheme.Label(DataManagementText.Folder, 9, muted: true), 0, 1);

        var path = new TextBox
        {
            Text = dataRoot,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Raised,
            ForeColor = UiTheme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 3, 0, 10),
            AccessibleName = DataManagementText.Folder
        };

        layout.Controls.Add(path, 0, 2);

        foreach (var (text, operation) in new[]
        {
            (DataManagementText.Move, DataOperationKind.Move),
            (DataManagementText.Backup, DataOperationKind.Backup),
            (DataManagementText.Restore, DataOperationKind.Restore)
        })
        {
            var button = new ActionButton(text) { Width = 178, Margin = new Padding(0, 0, 10, 8) };

            button.Click += (_, _) => OperationRequested?.Invoke(operation);

            _actions.Controls.Add(button);
        }

        _cancel.Margin = new Padding(0, 0, 0, 8);

        _cancel.Click += (_, _) => CancelRequested?.Invoke();

        _actions.Controls.Add(_cancel);

        layout.Controls.Add(_actions, 0, 3);

        layout.Controls.Add(UiTheme.Label(DataManagementText.BackupHint, 9, muted: true, wrap: true), 0, 4);

        layout.Controls.Add(_status, 0, 5);

        layout.Controls.Add(_progress, 0, 6);

        layout.Controls.Add(_retention, 0, 7);

        var age = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true, Margin = Padding.Empty
        };

        var label = UiTheme.Label(DataManagementText.Days, 10);

        label.AutoSize = true;

        label.Dock = DockStyle.None;

        label.Margin = new Padding(0, 8, 12, 0);

        var unit = UiTheme.Label(DataManagementText.DaysUnit, 10);

        unit.AutoSize = true;

        unit.Dock = DockStyle.None;

        unit.Margin = new Padding(0, 8, 28, 0);

        age.Controls.AddRange([label, _days, unit, _cleanup]);

        _cleanup.Click += (_, _) => OperationRequested?.Invoke(DataOperationKind.Cleanup);

        layout.Controls.Add(age, 0, 8);

        layout.Controls.Add(_keepFlagged, 0, 9);

        layout.Controls.Add(_keepUnread, 0, 10);

        layout.Controls.Add(UiTheme.Label(DataManagementText.RetentionHint, 9, muted: true, wrap: true), 0, 11);

        Controls.Add(layout);

        _retention.CheckedChanged += (_, _) => Publish();

        _days.ValueChanged += (_, _) => Publish();

        _keepFlagged.CheckedChanged += (_, _) => Publish();

        _keepUnread.CheckedChanged += (_, _) => Publish();
    }

    private static ThemedCheckBox Option(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        ForeColor = UiTheme.Text,
        BackColor = UiTheme.Surface,
        Cursor = Cursors.Hand,
        Margin = Padding.Empty
    };

    private void Publish() => RetentionChanged?.Invoke(_retention.Checked, (int)_days.Value,
        _keepFlagged.Checked, _keepUnread.Checked);

    internal void SetBusy(bool busy)
    {
        foreach (Control control in _actions.Controls)
        {
            control.Enabled = !busy || control == _cancel;
        }

        _cancel.Visible = busy;

        _retention.Enabled = !busy;

        _days.Enabled = !busy;

        _keepFlagged.Enabled = !busy;

        _keepUnread.Enabled = !busy;

        _cleanup.Enabled = !busy;

        _progress.Visible = busy;

        if (busy)
        {
            SetProgress(new DataOperationProgress(DataManagementText.Preparing));
        }
    }

    internal void SetProgress(DataOperationProgress value)
    {
        _status.Visible = true;

        _status.Text = value.TotalBytes > 0
            ? string.Format(DataManagementText.ProgressFormat, value.Stage,
                value.CompletedBytes / 1048576d, value.TotalBytes / 1048576d)
            : value.Stage;

        _status.ForeColor = UiTheme.Muted;

        _progress.Style = value.TotalBytes > 0 ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;

        _progress.Value = value.TotalBytes > 0
            ? (int)Math.Clamp(100d * value.CompletedBytes / value.TotalBytes, 0, 100) : 0;
    }

    internal void SetStatus(string text, bool error)
    {
        _status.Visible = text.Length > 0;

        _status.Text = text;

        _status.ForeColor = error ? UiTheme.Error : UiTheme.AccentText;
    }
}
