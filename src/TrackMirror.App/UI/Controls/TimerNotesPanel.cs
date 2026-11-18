// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.


using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class TimerNotesPanel : UserControl
{
    private readonly Label _status = UiTheme.Label(string.Empty, 9, muted: true, wrap: true);
    private readonly DataGridView _grid = new()
    {
        Name = "timerNotesHistory", Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = false,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
        RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
        BackgroundColor = UiTheme.Surface, BorderStyle = BorderStyle.None, GridColor = UiTheme.Border,
        EnableHeadersVisualStyles = false, Margin = new Padding(0, 8, 0, 8)
    };
    private readonly ScrollableTextField _reasonField = new()
    {
        Dock = DockStyle.Top, MaximumLines = 3, Padding = new Padding(6),
        BackColor = UiTheme.Raised, ForeColor = UiTheme.Text, BorderStyle = BorderStyle.FixedSingle
    };
    private readonly TextViewport _reason;
    private readonly ActionButton _save = new(TimerNoteText.Save);
    private readonly ActionButton _delete = new(TimerNoteText.Delete);
    private List<TimerStopNote> entries = [];
    private bool refreshing;

    internal event Action<string, string>? ReasonChanged;
    internal event Action<string>? DeleteRequested;

    internal TimerNotesPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(24);
        _reason = _reasonField.Editor;
        _reason.Name = "timerNoteReason";
        _reason.ReadOnly = false;
        _reason.TabStop = true;
        _reason.AcceptsReturn = true;
        _reason.MaxLength = 1000;

        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = UiTheme.Surface, ForeColor = UiTheme.Text, SelectionBackColor = UiTheme.Selected,
            SelectionForeColor = UiTheme.Text, WrapMode = DataGridViewTriState.True, Padding = new Padding(6)
        };
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = UiTheme.Raised, ForeColor = UiTheme.Text, Padding = new Padding(6)
        };
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = TimerNoteText.TimeColumn, Width = 170, SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = TimerNoteText.NoteColumn, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 44, 0, -1, 0, 0, 48, 0 })
        {
            root.RowStyles.Add(new RowStyle(height < 0 ? SizeType.Percent
                : height == 0 ? SizeType.AutoSize : SizeType.Absolute, height < 0 ? 100 : height));
        }

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };

        foreach (var button in new[] { _save, _delete })
        {
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(140, 40);
            button.Padding = new Padding(16, 0, 16, 0);
        }

        actions.Controls.AddRange([_save, _delete]);
        root.Controls.Add(UiTheme.Label(TimerNoteText.Heading, 22, true), 0, 0);
        root.Controls.Add(UiTheme.Label(TimerNoteText.Description, 10, muted: true, wrap: true), 0, 1);
        root.Controls.Add(_grid, 0, 2);
        root.Controls.Add(UiTheme.Label(TimerNoteText.EditHint, 9, muted: true, wrap: true), 0, 3);
        root.Controls.Add(_reasonField, 0, 4);
        root.Controls.Add(actions, 0, 5);
        root.Controls.Add(_status, 0, 6);
        Controls.Add(root);

        _grid.SelectionChanged += (_, _) => SelectEntry();
        _grid.FontChanged += (_, _) => _grid.AutoResizeColumnHeadersHeight();
        _grid.DpiChangedAfterParent += (_, _) => _grid.AutoResizeColumnHeadersHeight();
        _save.Click += (_, _) =>
        {
            if (Selected() is { } note)
            {
                ReasonChanged?.Invoke(note.Id, _reason.Text);
            }
        };
        _delete.Click += (_, _) =>
        {
            if (Selected() is { } note)
            {
                DeleteRequested?.Invoke(note.Id);
            }
        };
        SelectEntry();
    }

    internal void SetEntries(List<TimerStopNote> notes)
    {
        var previous = Selected();
        var draft = _reason.Text;
        var hasDraft = previous != null && draft != TimerNoteText.Reason(previous);
        refreshing = true;

        try
        {
            entries = notes;
            _grid.Rows.Clear();

            foreach (var note in notes)
            {
                var index = _grid.Rows.Add(note.StoppedUtc.ToLocalTime().ToString("g"), TimerNoteText.Reason(note));
                _grid.Rows[index].Tag = note.Id;
            }

            var selected = _grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row => Equals(row.Tag, previous?.Id));

            if (selected != null)
            {
                _grid.CurrentCell = selected.Cells[0];
            }
        }
        finally
        {
            refreshing = false;
        }

        SelectEntry();

        if (hasDraft && Selected()?.Id == previous?.Id)
        {
            _reason.Text = draft;
        }
    }

    internal void SetStatus(string text, bool error = false)
    {
        _status.Text = text;
        _status.ForeColor = error ? UiTheme.Error : UiTheme.Muted;
    }

    private TimerStopNote? Selected() => _grid.CurrentRow?.Tag is string id
        ? entries.FirstOrDefault(note => note.Id == id) : null;

    private void SelectEntry()
    {
        if (refreshing)
        {
            return;
        }

        var note = Selected();
        _reason.Text = note == null ? string.Empty : TimerNoteText.Reason(note);
        _reason.Enabled = _save.Enabled = _delete.Enabled = note != null;
    }
}
