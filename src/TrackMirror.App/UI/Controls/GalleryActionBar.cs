// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Providers;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class GalleryActionBar : SettingsSection
{
    private readonly ThemedCheckBox _all = new()
    {
        AutoCheck = false, ThreeState = true, AutoEllipsis = true,
        ForeColor = UiTheme.Text, Cursor = Cursors.Hand, AccessibleName = GalleryText.SelectCapture
    };
    private readonly Panel _header = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly ActionButton _read = new(GalleryText.MarkRead);
    private readonly ActionButton _scan = new(GalleryText.Scan + GalleryText.ExpandSuffix);
    private readonly ActionButton _clear = new(GalleryText.Clear);
    private readonly ActionButton _more = new(GalleryText.MoreGlyph) { AccessibleName = GalleryText.More };
    private readonly Label _message = UiTheme.Label(string.Empty, 9, muted: true);
    private readonly ActionButton _undo = new(GalleryText.Undo) { Width = 88, Height = 30, Visible = false };
    private readonly ActionButton _stop = new(PrivacyText.Cancel) { Width = 136, Height = 30, Visible = false };
    private readonly FlowLayoutPanel _feedback = new()
    {
        Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = true, Margin = Padding.Empty, Visible = false
    };
    private readonly System.Windows.Forms.Timer _undoTimer = new() { Interval = 10000 };
    private readonly ContextMenuStrip _overflow = new() { BackColor = UiTheme.Surface, ForeColor = UiTheme.Text };
    private readonly ScanOptionsPanel _scanOptions = new();
    private readonly ToolStripControlHost _host;
    private readonly AnchoredDropDown _popup;
    private AppSettings settings = new();
    private string queueText = string.Empty;
    private int selectedCount;
    private int undoCount;
    private bool arranging;

    internal event Action<bool>? SelectAllRequested;
    internal event Action? ReadRequested;
    internal event Action? ClearRequested;
    internal event Action? UndoRequested;
    internal event Action? CancelScanRequested;
    internal event Action? SettingsRequested;
    internal event Action<IReadOnlyList<PrivacyScanCategory>, bool>? ScanRequested;

    internal GalleryActionBar()
    {
        Dock = DockStyle.Fill;

        BackColor = UiTheme.Background;

        Margin = new Padding(0, 0, 0, 12);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, RowCount = 2, Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _header.Controls.AddRange([_all, _read, _scan, _clear, _more]);

        _message.AutoSize = true;

        _message.Dock = DockStyle.None;

        _message.Margin = new Padding(0, 8, 12, 8);

        _feedback.Controls.AddRange([_message, _undo, _stop]);

        layout.Controls.Add(_header, 0, 0);

        layout.Controls.Add(_feedback, 0, 1);

        Controls.Add(layout);

        _host = new ToolStripControlHost(new ScrollViewport(_scanOptions) { BackColor = UiTheme.Surface })
        {
            AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty
        };

        _popup = new AnchoredDropDown();

        _popup.Items.Add(_host);

        _all.Click += (_, _) => SelectAllRequested?.Invoke(_all.CheckState != CheckState.Checked);

        _read.Click += (_, _) => ReadRequested?.Invoke();

        _scan.Click += (_, _) => ShowScanOptions(_scan);

        _clear.Click += (_, _) => ClearRequested?.Invoke();

        _more.Click += (_, _) => ShowOverflow();

        _undo.Click += (_, _) => UndoRequested?.Invoke();

        _stop.Click += (_, _) => CancelScanRequested?.Invoke();

        _scanOptions.ScanRequested += (categories, force) =>
        {
            ClosePopups();

            ScanRequested?.Invoke(categories, force);
        };

        _scanOptions.SettingsRequested += () =>
        {
            ClosePopups();

            SettingsRequested?.Invoke();
        };

        _scanOptions.CancelRequested += ClosePopups;

        _header.SizeChanged += (_, _) => ArrangeActions();

        _undoTimer.Tick += (_, _) => ClearUndo();

        VisibleChanged += (_, _) =>
        {
            if (!Visible)
            {
                ClosePopups();
            }
        };
    }

    internal void UpdateSelection(int total, int count, bool hasUnread)
    {
        selectedCount = count;

        _all.Enabled = total > 0;

        _all.Text = count == 0 ? string.Format(GalleryText.SelectAllFormat, total)
            : string.Format(GalleryText.SelectedFormat, count, total);

        _all.AccessibleName = _all.Text;

        _all.CheckState = count == 0 ? CheckState.Unchecked
            : count == total ? CheckState.Checked : CheckState.Indeterminate;

        _read.Enabled = hasUnread;

        ArrangeActions();

        if (count == 0)
        {
            ClosePopups();
        }
    }

    internal void SetSettings(AppSettings value) => settings = value;

    private void ArrangeActions()
    {
        if (arranging)
            return;

        arranging = true;

        try
        {
            var scale = DeviceDpi / 96f;

            var gap = (int)(10 * scale);

            var buttons = new[] { _read, _scan, _clear };

            var widths = new[] { (int)(148 * scale), (int)(128 * scale), (int)(92 * scale) };

            var show = new[] { selectedCount > 0, selectedCount > 0, selectedCount > 0 };

            var available = Math.Max(0, _header.Width - (int)(190 * scale));

            for (var i = 2; i >= 0; i--)
            {
                var required = Enumerable.Range(0, 3).Where(index => show[index]).Sum(index => widths[index] + gap)
                    + (show.Any(value => !value) && selectedCount > 0 ? (int)(40 * scale) + gap : 0);

                if (required <= available)
                    break;

                show[i] = false;
            }

            var more = selectedCount > 0 && show.Any(value => !value);

            _more.Visible = more;

            var x = _header.Width;

            if (more)
            {
                x -= (int)(40 * scale);

                _more.SetBounds(x, 0, (int)(40 * scale), _header.Height);

                x -= gap;
            }

            for (var i = 2; i >= 0; i--)
            {
                buttons[i].Visible = show[i];

                if (!show[i])
                    continue;

                x -= widths[i];

                buttons[i].SetBounds(x, 0, widths[i], _header.Height);

                x -= gap;
            }

            _all.SetBounds(0, 0, Math.Max(1, x), _header.Height);
        }
        finally
        {
            arranging = false;
        }
    }

    private void ShowOverflow()
    {
        _overflow.Items.Clear();

        foreach (var (button, action) in new (ActionButton, Action)[]
        {
            (_read, () => ReadRequested?.Invoke()),
            (_scan, () => BeginInvoke(new Action(() => ShowScanOptions(_more)))),
            (_clear, () => ClearRequested?.Invoke())
        })
        {
            if (button.Visible)
                continue;

            var item = _overflow.Items.Add(button.Text, null, (_, _) => action());

            item.Enabled = button.Enabled;

            item.BackColor = UiTheme.Surface;

            item.ForeColor = UiTheme.Text;
        }

        _overflow.Show(_more, new Point(0, _more.Height));
    }

    private void ShowScanOptions(Control anchor)
    {
        if (selectedCount == 0 || !Visible || Disposing || IsDisposed)
            return;

        if (_popup.Visible)
        {
            ClosePopups();

            return;
        }

        _scanOptions.Prepare(selectedCount, settings, LocalModelRuntime.IsInstalled(settings.PrivacyModel));

        _ = _scanOptions.Handle;

        var area = Screen.FromControl(anchor).WorkingArea;

        var width = Math.Min((int)(360 * DeviceDpi / 96f), Math.Max(1, area.Width - 32));

        var maximumHeight = Math.Max(1, area.Height - 48);

        // Measure with the available viewport first; a temporary scrollbar changes text wrapping.
        _host.Size = new Size(width, maximumHeight);

        _scanOptions.Size = _scanOptions.GetPreferredSize(new Size(width, 0));

        _host.Height = Math.Min(_scanOptions.Height, maximumHeight);

        _popup.ShowAnchored(anchor, new Point(0, anchor.Height + (int)(6 * DeviceDpi / 96f)));
    }

    internal void SetQueue(int queued, bool scanning, string? error)
    {
        queueText = error ?? (queued > 0 || scanning
            ? string.Format(PrivacyText.QueueFormat, queued, scanning ? PrivacyText.Scanning : PrivacyText.Completed)
            : string.Empty);

        _stop.Visible = queued > 0 || scanning;

        UpdateFeedback();
    }

    internal void ShowUndo(int count)
    {
        undoCount = count;

        _undoTimer.Stop();

        if (count > 0)
        {
            _undoTimer.Start();
        }

        UpdateFeedback();
    }

    internal void ClearUndo()
    {
        undoCount = 0;

        _undoTimer.Stop();

        UpdateFeedback();
    }

    private void UpdateFeedback()
    {
        _undo.Visible = undoCount > 0;

        _message.Text = undoCount > 0 ? string.Format(GalleryText.ReadFormat, undoCount) : queueText;

        _feedback.Visible = _message.Text.Length > 0 || _stop.Visible;
    }

    internal void ClosePopups()
    {
        _popup.Close();

        _overflow.Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _undoTimer.Dispose();

            _overflow.Dispose();

            _popup.Dispose();
        }

        base.Dispose(disposing);
    }
}
