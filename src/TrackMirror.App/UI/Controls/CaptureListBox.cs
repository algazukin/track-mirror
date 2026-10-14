// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Drawing2D;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class CaptureListBox : ListBox
{
    private readonly Font _rowTitleFont = new(UiText.FontFamily, 10, FontStyle.Bold);
    private readonly Font _rowDetailFont = new(UiText.FontFamily, 8.5f);
    private readonly ToolTip _tooltips = new()
    {
        InitialDelay = 400,
        ReshowDelay = 150,
        AutoPopDelay = 10000
    };
    private int wheelRemainder;

    internal event Action? ViewportChanged;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;

            parameters.Style &= ~NativeWindowConstants.VerticalScrollStyle;

            return parameters;
        }
    }

    internal CaptureListBox()
    {
        Dock = DockStyle.Fill;

        DrawMode = DrawMode.OwnerDrawFixed;

        ItemHeight = 76;

        BorderStyle = BorderStyle.None;

        IntegralHeight = false;

        FormattingEnabled = true;

        BackColor = UiTheme.Surface;

        ForeColor = UiTheme.Text;

        Margin = Padding.Empty;

        AccessibleName = UiText.HistoryHeading;

        HandleCreated += (_, _) => ItemHeight = Math.Min(255, (int)(76 * DeviceDpi / 96f));

        DpiChangedAfterParent += (_, _) => ItemHeight = Math.Min(255, (int)(76 * DeviceDpi / 96f));

        Format += (_, e) =>
        {
            if (e.ListItem is CaptureEntry capture)
            {
                var timestamp = capture.CapturedUtc.ToLocalTime().ToString(DateFormats.HistoryRow);

                e.Value = timestamp
                    + UiText.Separator
                    + (capture.Viewed ? UiText.Reviewed : UiText.New)
                    + (capture.Flagged ? UiText.FlaggedSuffix : string.Empty)
                    + UiText.Separator
                    + capture.FileName
                    + UiText.Separator
                    + capture.Account;
            }
        };

        MouseMove += (_, e) =>
        {
            var index = IndexFromPoint(e.Location);

            var text = index >= 0
                && index < Items.Count
                && Items[index] is CaptureEntry capture ? capture.Account
                + Environment.NewLine
                + capture.FileName : string.Empty;

            if (_tooltips.GetToolTip(this) != text)
            {
                _tooltips.SetToolTip(this, text);
            }
        };

        DrawItem += DrawCaptureRow;
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);

        if (message.Msg is NativeWindowConstants.KeyDownMessage
            or NativeWindowConstants.CharacterMessage
            or NativeWindowConstants.SizeMessage
            or NativeWindowConstants.TimerMessage
            or NativeWindowConstants.MouseMoveMessage
            or NativeWindowConstants.LeftButtonDownMessage
            or NativeWindowConstants.LeftButtonUpMessage
            or NativeWindowConstants.VerticalScrollMessage
            or NativeWindowConstants.MouseWheelMessage
            or NativeWindowConstants.ListSetSelectionMessage
            or NativeWindowConstants.ListSetTopIndexMessage
            or NativeWindowConstants.ListSetItemHeightMessage)
        {
            ViewportChanged?.Invoke();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollWheel(e.Delta);

        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }

        base.OnMouseWheel(e);
    }

    internal void ScrollWheel(int delta)
    {
        wheelRemainder += delta;

        var notches = wheelRemainder / 120;

        wheelRemainder %= 120;

        var pageRows = Math.Max(1, ClientSize.Height / ItemHeight);

        var lines = SystemInformation.MouseWheelScrollLines;

        var step = lines < 0 ? pageRows : lines;

        TopIndex = Math.Clamp(TopIndex - notches * step, 0, Math.Max(0, Items.Count - pageRows));
    }

    private void DrawCaptureRow(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= this.Items.Count)
            return;

        var entry = (CaptureEntry)this.Items[e.Index];

        var scale = Math.Min(this.DeviceDpi / 96f, this.ItemHeight / 76f);

        var inset = (int)(10 * scale);

        var row = new Rectangle(
            e.Bounds.X,
            e.Bounds.Y + (int)(3 * scale),
            e.Bounds.Width - (int)(4 * scale),
            e.Bounds.Height - (int)(6 * scale)
        );

        if (row.Width < 2 || row.Height < 2)
            return;

        using var background = new SolidBrush(UiTheme.Surface);

        e.Graphics.FillRectangle(background, e.Bounds);

        if ((e.State & DrawItemState.Selected) != 0)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var shape = UiTheme.RoundedRectangle(row, (int)(8 * scale));

            using var selectedFill = new SolidBrush(UiTheme.Selected);

            e.Graphics.FillPath(selectedFill, shape);
        }

        var titleBounds = new Rectangle(
            row.X + inset,
            row.Y + (int)(8 * scale),
            row.Width - inset * 2,
            (int)(24 * scale)
        );

        TextRenderer.DrawText(
            e.Graphics,
            entry.CapturedUtc.ToLocalTime().ToString(DateFormats.HistoryRow),
            _rowTitleFont,
            titleBounds,
            UiTheme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
        );

        var badgeBounds = new Rectangle(
            row.X + inset,
            row.Y + (int)(39 * scale),
            (int)(109 * scale),
            (int)(23 * scale)
        );

        var badgeColor = entry.Flagged ? Color.FromArgb(65, 53, 36) : entry.Viewed ? UiTheme.Raised : UiTheme.Selected;

        using var badgeShape = UiTheme.RoundedRectangle(badgeBounds, (int)(6 * scale));

        using var badgeFill = new SolidBrush(badgeColor);

        e.Graphics.FillPath(badgeFill, badgeShape);

        TextRenderer.DrawText(
            e.Graphics,
            (entry.Flagged ? UiText.FlagPrefix : string.Empty) + (entry.Viewed ? UiText.Reviewed : UiText.New),
            _rowDetailFont,
            badgeBounds,
            entry.Flagged ? UiTheme.Amber : entry.Viewed ? UiTheme.Muted : UiTheme.AccentText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
        );

        if ((e.State & DrawItemState.Focus) != 0)
        {
            e.DrawFocusRectangle();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rowTitleFont.Dispose();

            _rowDetailFont.Dispose();

            _tooltips.Dispose();
        }

        base.Dispose(disposing);
    }
}
