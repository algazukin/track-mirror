// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.


using TrackMirror.Constants;
using TrackMirror.Hubstaff;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class TimerNotesSettingsPanel : SettingsSection
{
    private readonly ThemedCheckBox _enabled = new()
    {
        Name = "timerNotesEnabled", Text = TimerNoteText.Enabled, AutoSize = true, Dock = DockStyle.Top
    };
    private readonly ThemedCheckBox _prompt = new()
    {
        Name = "timerNotesShowPrompt", Text = TimerNoteText.ShowPrompt, AutoSize = true, Dock = DockStyle.Top
    };
    private readonly NumericUpDown _seconds = new()
    {
        Minimum = 5, Maximum = 60, Width = 100, MinimumSize = new Size(100, 0),
        ForeColor = UiTheme.Text, BackColor = UiTheme.Raised, BorderStyle = BorderStyle.FixedSingle
    };
    private readonly ScrollableTextField _presetsField = new()
    {
        Dock = DockStyle.Top, MaximumLines = 6, Padding = new Padding(6),
        BackColor = UiTheme.Raised, ForeColor = UiTheme.Text, BorderStyle = BorderStyle.FixedSingle
    };
    private readonly TextViewport _presets;
    private readonly Label _error = UiTheme.Label(string.Empty, 9, wrap: true);
    private TimerNotesOptions current;

    internal event Action<TimerNotesOptions>? Changed;

    internal TimerNotesSettingsPanel(TimerNotesOptions options)
    {
        _presets = _presetsField.Editor;
        _presets.Name = "timerNotesPresets";
        _presets.ReadOnly = false;
        _presets.TabStop = true;
        _presets.AcceptsReturn = true;
        _presets.MaxLength = 2000;

        current = options;
        _enabled.Checked = options.Enabled;
        _prompt.Checked = options.ShowPrompt;
        _seconds.Value = options.PromptSeconds;
        _presets.Lines = options.Reasons.SequenceEqual(new TimerNotesOptions().Reasons)
            ? TimerNoteText.DefaultReasons.Split('|') : options.Reasons;
        current = options with { Reasons = _presets.Lines };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Control[] rows =
        [
            UiTheme.Label(TimerNoteText.Heading, 14, true, wrap: true),
            UiTheme.Label(TimerNoteText.LocalHint, 9, muted: true, wrap: true), _enabled, _prompt,
            UiTheme.Label(TimerNoteText.PromptSeconds, 10, wrap: true), _seconds,
            UiTheme.Label(TimerNoteText.Reasons, 10, wrap: true), _presetsField,
            UiTheme.Label(TimerNoteText.ReasonsHint, 9, muted: true, wrap: true)
        ];

        foreach (var row in rows)
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(row, 0, root.RowCount++);
            row.Margin = new Padding(0, 4, 0, 6);
        }

        var save = new ActionButton(TimerNoteText.Save)
        {
            Name = "timerNotesSavePresets", Margin = new Padding(0, 4, 0, 4)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(save, 0, root.RowCount++);
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(_error, 0, root.RowCount++);
        Controls.Add(root);

        _enabled.CheckedChanged += (_, _) => Publish(false);
        _prompt.CheckedChanged += (_, _) => Publish(false);
        _seconds.ValueChanged += (_, _) => Publish(false);
        save.Click += (_, _) => Publish(true);
    }

    private void Publish(bool savePresets)
    {
        var reasons = savePresets
            ? _presets.Lines.Where(reason => !string.IsNullOrWhiteSpace(reason)).Distinct(StringComparer.Ordinal).ToArray()
            : current.Reasons;
        var next = current with
        {
            Enabled = _enabled.Checked, ShowPrompt = _prompt.Checked,
            PromptSeconds = (int)_seconds.Value, Reasons = reasons
        };

        if (!next.IsValid)
        {
            _error.Text = TimerNoteText.InvalidReasons;
            _error.ForeColor = UiTheme.Error;
            return;
        }

        _error.Text = string.Empty;
        current = next;
        Changed?.Invoke(next);
    }
}
