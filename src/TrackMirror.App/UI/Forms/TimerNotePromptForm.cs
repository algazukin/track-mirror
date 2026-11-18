// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.


using TrackMirror.Constants;
using TrackMirror.Hubstaff;
using TrackMirror.Models;
using TrackMirror.UI.Controls;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Forms;

internal sealed class TimerNotePromptForm : Form
{
    private readonly System.Windows.Forms.Timer _dismiss = new();
    private readonly ToolTip _tooltips = new();
    private readonly TableLayoutPanel _layout;
    private readonly FlowLayoutPanel _reasons = new() { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
    private readonly Label _caption = UiTheme.Label(string.Empty, 9, muted: true, wrap: true);
    private readonly TextBox _custom = new()
    {
        Name = "timerNoteCustom", Dock = DockStyle.Top, Multiline = true, Height = 65, MaxLength = 1000,
        BackColor = UiTheme.Raised, ForeColor = UiTheme.Text, Visible = false, ScrollBars = ScrollBars.Vertical
    };
    private readonly ActionButton _save = new(TimerNoteText.Save) { Visible = false };
    private TimerStopNote? entry;

    internal event Func<string, string, bool>? ReasonSelected;
    internal event Func<string, bool>? Skipped;

    protected override bool ShowWithoutActivation => true;

    internal TimerNotePromptForm()
    {
        Text = TimerNoteText.StoppedHeading;
        Font = new Font(UiText.FontFamily, 9.5f);
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Text;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScroll = true;

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, Padding = new Padding(16)
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        var skip = new ActionButton(TimerNoteText.Skip) { Name = "timerNoteSkip" };
        actions.Controls.AddRange([_save, skip]);

        foreach (var control in new Control[] { _caption, _reasons, _custom, actions })
        {
            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Margin = new Padding(0, 4, 0, 8);
            _layout.Controls.Add(control, 0, _layout.RowCount++);
        }

        Controls.Add(_layout);
        _dismiss.Tick += (_, _) => Hide();
        _save.Click += (_, _) => Choose(_custom.Text);
        skip.Click += (_, _) =>
        {
            if (entry != null && Skipped?.Invoke(entry.Id) == true)
            {
                Hide();
            }
        };
        FormClosing += (_, args) =>
        {
            if (args.CloseReason == CloseReason.UserClosing)
            {
                args.Cancel = true;
                Hide();
            }
        };
        VisibleChanged += (_, _) =>
        {
            if (!Visible)
            {
                _dismiss.Stop();
            }
        };
    }

    internal void Present(TimerStopNote note, TimerNotesOptions options)
    {
        Hide();
        _tooltips.RemoveAll();
        entry = note;
        _caption.Text = TimerNoteText.StoppedHeading + " · " + note.StoppedUtc.ToLocalTime().ToString("t");
        _custom.Clear();
        _custom.Visible = _save.Visible = false;

        foreach (Control old in _reasons.Controls.Cast<Control>().ToList())
        {
            old.Dispose();
        }

        foreach (var reason in options.Reasons)
        {
            var button = new ActionButton(reason) { Margin = new Padding(0, 0, 8, 6) };
            button.Click += (_, _) => Choose(reason);
            _tooltips.SetToolTip(button, reason);
            _reasons.Controls.Add(button);
        }

        var other = new ActionButton(TimerNoteText.Other) { Name = "timerNoteOther" };
        other.Click += (_, _) =>
        {
            _dismiss.Stop();
            other.Enabled = false;
            _custom.Visible = _save.Visible = true;
            FitToContent();
            _custom.Focus();
        };
        _reasons.Controls.Add(other);

        var scale = DeviceDpi / 96f;
        ClientSize = new Size((int)(420 * scale), (int)(180 * scale));
        Place();
        _dismiss.Interval = options.PromptSeconds * 1000;
        Show();
        FitToContent();
        _dismiss.Start();
    }

    private void FitToContent()
    {
        var available = ClientSize.Width - _layout.Padding.Horizontal;
        _reasons.MaximumSize = new Size(available, 0);

        foreach (Button button in _reasons.Controls)
        {
            var text = TextRenderer.MeasureText(button.Text, button.Font);
            button.Size = new Size(Math.Min(available - button.Margin.Horizontal,
                Math.Max(100, text.Width + 32)), Math.Max(40, text.Height + 20));
        }

        PerformLayout();
        ClientSize = new Size(ClientSize.Width, _layout.GetPreferredSize(new Size(ClientSize.Width, 0)).Height);
        Place();
    }

    private void Place()
    {
        var bounds = Screen.FromPoint(Cursor.Position).WorkingArea;
        Height = Math.Min(Height, bounds.Height - 32);
        Location = new Point(Math.Max(bounds.Left, bounds.Right - Width - 16),
            Math.Max(bounds.Top, bounds.Bottom - Height - 16));
    }

    private void Choose(string reason)
    {
        if (entry == null || string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        if (ReasonSelected?.Invoke(entry.Id, reason) == true)
        {
            Hide();
        }
        else
        {
            _caption.Text = TimerNoteText.SaveFailed;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dismiss.Dispose();
            _tooltips.Dispose();
        }

        base.Dispose(disposing);
    }
}
