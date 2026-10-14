// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Hubstaff;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class HubstaffControlPanel : SettingsSection
{
    private readonly ThemedCheckBox _enabled = new()
    {
        Text = HubstaffControlText.Enabled, Dock = DockStyle.Fill, ForeColor = UiTheme.Text,
        BackColor = UiTheme.Surface, Cursor = Cursors.Hand, Margin = Padding.Empty
    };
    private readonly OptionSelector _mode = new(HubstaffControlText.Mode)
    {
        Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 6)
    };
    private readonly NumericUpDown _idle = Number(HubstaffControlText.IdleSeconds);
    private readonly NumericUpDown _restart = Number(HubstaffControlText.RestartSeconds);
    private readonly TextBox _path = new()
    {
        Dock = DockStyle.Fill, BackColor = UiTheme.Raised, ForeColor = UiTheme.Text,
        BorderStyle = BorderStyle.FixedSingle, PlaceholderText = HubstaffControlText.AutomaticPath,
        AccessibleName = HubstaffControlText.CliPath, Margin = new Padding(0, 6, 12, 0)
    };
    private readonly Label _hint = UiTheme.Label(string.Empty, 9, muted: true, wrap: true);
    private readonly Label _status = UiTheme.Label(HubstaffControlText.Disabled, 9, muted: true, wrap: true);
    private readonly ActionButton _retry = new(HubstaffControlText.Retry) { Width = 140 };
    private readonly TableLayoutPanel _restartRow;
    private HubstaffControlOptions current;
    private HubstaffControlState state;

    internal event Action<HubstaffControlOptions>? Changed;

    internal event Action? RetryRequested;

    internal HubstaffControlPanel(HubstaffControlOptions options)
    {
        current = options;

        BackColor = UiTheme.Surface;

        _enabled.Checked = options.Enabled;

        _idle.Value = options.IdleSeconds;

        _restart.Value = options.RestartSeconds;

        _path.Text = options.CliPath;

        _mode.SetChoices([HubstaffControlText.PauseMode, HubstaffControlText.RestartMode]);

        _mode.SelectValue(options.Mode == HubstaffControlMode.PauseUntilActive
            ? HubstaffControlText.PauseMode : HubstaffControlText.RestartMode);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, RowCount = 11, Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var height in new[] { 36, 30, 0, 42, 36, 36, 24, 46, 0, 0, 40 })
        {
            layout.RowStyles.Add(new RowStyle(height == 0 ? SizeType.AutoSize : SizeType.Absolute, height));
        }

        layout.Controls.Add(UiTheme.Label(HubstaffControlText.Heading, 15, true), 0, 0);

        layout.Controls.Add(_enabled, 0, 1);

        layout.Controls.Add(_hint, 0, 2);

        layout.Controls.Add(Row(HubstaffControlText.Mode, _mode), 0, 3);

        layout.Controls.Add(Row(HubstaffControlText.IdleSeconds, _idle), 0, 4);

        _restartRow = Row(HubstaffControlText.RestartSeconds, _restart);

        layout.Controls.Add(_restartRow, 0, 5);

        layout.Controls.Add(UiTheme.Label(HubstaffControlText.CliPath, 9, muted: true), 0, 6);

        layout.Controls.Add(PathRow(), 0, 7);

        layout.Controls.Add(UiTheme.Label(HubstaffControlText.PermissionHint, 9, muted: true, wrap: true), 0, 8);

        layout.Controls.Add(_status, 0, 9);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty };

        actions.Controls.Add(_retry);

        layout.Controls.Add(actions, 0, 10);

        Controls.Add(layout);

        UpdateMode();

        _enabled.CheckedChanged += (_, _) => Publish();

        _mode.SelectionChanged += Publish;

        _idle.ValueChanged += (_, _) => Publish();

        _restart.ValueChanged += (_, _) => Publish();

        _path.Validated += (_, _) => Publish();

        _path.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Enter)
            {
                Publish();

                args.SuppressKeyPress = true;
            }
        };

        _retry.Click += (_, _) => RetryRequested?.Invoke();

        VisibleChanged += (_, _) =>
        {
            if (!Visible)
            {
                _mode.ClosePopup();
            }
        };
    }

    private TableLayoutPanel PathRow()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 114));

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 154));

        var browse = new ActionButton(HubstaffControlText.Browse)
        {
            Dock = DockStyle.Fill, Margin = new Padding(0, 2, 10, 4)
        };

        var detect = new ActionButton(HubstaffControlText.Detect)
        {
            Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 4)
        };

        browse.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = HubstaffControlText.CliPath, Filter = HubstaffControlText.FileFilter,
                CheckFileExists = true, RestoreDirectory = true
            };

            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _path.Text = dialog.FileName;

                Publish();
            }
        };

        detect.Click += (_, _) =>
        {
            var found = HubstaffCliLocator.Find();

            if (found == null)
            {
                SetStatus(new(HubstaffControlState.Error, FailureCode: HubstaffControlNames.MissingCli));

                return;
            }

            _path.Text = found;

            Publish();
        };

        row.Controls.Add(_path, 0, 0);

        row.Controls.Add(browse, 1, 0);

        row.Controls.Add(detect, 2, 0);

        return row;
    }

    private static TableLayoutPanel Row(string text, Control control)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var label = UiTheme.Label(text, 10);

        label.AutoSize = true;

        label.Margin = new Padding(0, 0, 16, 0);

        row.Controls.Add(label, 0, 0);

        row.Controls.Add(control, 1, 0);

        return row;
    }

    private static NumericUpDown Number(string caption) => new()
    {
        Minimum = 1, Maximum = HubstaffControlNames.MaximumSeconds, Width = 100,
        MinimumSize = new Size(100, 0),
        BackColor = UiTheme.Raised, ForeColor = UiTheme.Text, BorderStyle = BorderStyle.FixedSingle,
        AccessibleName = caption, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 12, 0)
    };

    private void UpdateMode()
    {
        var restart = _mode.SelectedValue == HubstaffControlText.RestartMode;

        _restartRow.Enabled = restart;

        _hint.Text = restart ? HubstaffControlText.RestartHint : HubstaffControlText.PauseHint;

        _retry.Enabled = _enabled.Checked && state == HubstaffControlState.Error;
    }

    private void Publish()
    {
        UpdateMode();

        var value = current with
        {
            Enabled = _enabled.Checked,
            Mode = _mode.SelectedValue == HubstaffControlText.RestartMode
                ? HubstaffControlMode.RestartWhileIdle : HubstaffControlMode.PauseUntilActive,
            IdleSeconds = (int)_idle.Value, RestartSeconds = (int)_restart.Value, CliPath = _path.Text
        };

        if (!value.IsValid)
        {
            if (!_enabled.Checked && current.Enabled)
            {
                current = current with { Enabled = false };

                Changed?.Invoke(current);
            }

            _status.Text = HubstaffControlText.InvalidSettings;

            _status.ForeColor = UiTheme.Error;

            return;
        }

        current = value;

        Changed?.Invoke(value);
    }

    internal void SetStatus(HubstaffControlStatus value)
    {
        state = value.State;

        _retry.Enabled = _enabled.Checked && state == HubstaffControlState.Error;

        _status.Text = HubstaffControlText.Status(value);

        _status.ForeColor = value.State == HubstaffControlState.Error ? UiTheme.Error : UiTheme.Muted;
    }
}
