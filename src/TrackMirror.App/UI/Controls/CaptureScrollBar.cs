// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Drawing2D;
using System.ComponentModel;
using TrackMirror.Constants;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class CaptureScrollBar : Control
{
    private readonly CaptureListBox? _list;
    private int maximum = -1;
    private int pageRows = 1;
    private int position;
    private int totalUnits;
    private int dragOffset;
    private bool dragging;
    private bool hovered;

    internal event Action<int>? ScrollRequested;
    internal event Action<int>? WheelScrolled;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Orientation Axis { get; init; } = Orientation.Vertical;

    internal CaptureScrollBar()
    {
        Dock = DockStyle.Right;

        Width = 14;

        Visible = false;

        Margin = Padding.Empty;

        BackColor = UiTheme.Surface;

        Cursor = Cursors.Hand;

        TabStop = false;

        AccessibleRole = AccessibleRole.ScrollBar;

        AccessibleName = UiText.CaptureScrollBar;

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    internal CaptureScrollBar(CaptureListBox list) : this()
    {
        _list = list;

        ScrollRequested += value =>
        {
            list.Focus();

            list.TopIndex = value;
        };

        WheelScrolled += list.ScrollWheel;

        list.ViewportChanged += RefreshViewport;

        list.SizeChanged += (_, _) => RefreshViewport();

        list.HandleCreated += (_, _) => RefreshViewport();

        list.DpiChangedAfterParent += (_, _) => RefreshViewport();
    }

    internal void RefreshViewport()
    {
        if (_list == null || _list.IsDisposed || IsDisposed)
            return;

        var nextPageRows = Math.Max(1, _list.ClientSize.Height / _list.ItemHeight);

        SetViewport(_list.Items.Count, nextPageRows, _list.TopIndex);
    }

    internal void SetViewport(int total, int page, int offset)
    {
        var nextPageRows = Math.Max(1, page);

        var nextMaximum = Math.Max(0, total - nextPageRows);

        var nextPosition = Math.Clamp(offset, 0, nextMaximum);

        if (pageRows == nextPageRows && maximum == nextMaximum && position == nextPosition && totalUnits == total)
            return;

        pageRows = nextPageRows;

        maximum = nextMaximum;

        position = nextPosition;

        totalUnits = total;

        if (maximum == 0)
        {
            dragging = false;

            Capture = false;
        }

        Visible = maximum > 0;

        Invalidate();
    }

    private Rectangle TrackBounds => Axis == Orientation.Vertical
        ? new Rectangle((int)(3 * DeviceDpi / 96f), (int)(4 * DeviceDpi / 96f),
            Math.Max(1, Width - (int)(6 * DeviceDpi / 96f)), Math.Max(1, Height - (int)(8 * DeviceDpi / 96f)))
        : new Rectangle((int)(4 * DeviceDpi / 96f), (int)(3 * DeviceDpi / 96f),
            Math.Max(1, Width - (int)(8 * DeviceDpi / 96f)), Math.Max(1, Height - (int)(6 * DeviceDpi / 96f)));

    private Rectangle ThumbBounds
    {
        get
        {
            var track = TrackBounds;

            var length = Axis == Orientation.Vertical ? track.Height : track.Width;

            var minimum = Math.Min(length, (int)(32 * DeviceDpi / 96f));

            var size = Math.Clamp((int)(length * (double)pageRows / Math.Max(1, totalUnits)), minimum, length);

            var offset = maximum == 0 ? 0 : (int)Math.Round((length - size) * (double)position / maximum);

            return Axis == Orientation.Vertical
                ? new Rectangle(track.X, track.Y + offset, track.Width, size)
                : new Rectangle(track.X + offset, track.Y, size, track.Height);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (maximum == 0 || Width < 2 || Height < 2)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var track = TrackBounds;

        var railWidth = Math.Max(2, (int)(2 * DeviceDpi / 96f));

        var rail = Axis == Orientation.Vertical
            ? new Rectangle(track.X + (track.Width - railWidth) / 2, track.Y, railWidth, track.Height)
            : new Rectangle(track.X, track.Y + (track.Height - railWidth) / 2, track.Width, railWidth);

        using var railShape = UiTheme.RoundedRectangle(rail, railWidth / 2);

        using var railFill = new SolidBrush(UiTheme.Raised);

        e.Graphics.FillPath(railFill, railShape);

        var thumb = ThumbBounds;

        using var thumbShape = UiTheme.RoundedRectangle(thumb, Math.Min(thumb.Width, thumb.Height) / 2);

        using var thumbFill = new SolidBrush(dragging || hovered ? UiTheme.AccentText : UiTheme.ScrollThumb);

        e.Graphics.FillPath(thumbFill, thumbShape);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left || maximum == 0)
            return;

        var thumb = ThumbBounds;

        if (thumb.Contains(e.Location))
        {
            dragging = true;

            dragOffset = Axis == Orientation.Vertical ? e.Y - thumb.Y : e.X - thumb.X;

            Capture = true;
        }
        else
        {
            var before = Axis == Orientation.Vertical ? e.Y < thumb.Top : e.X < thumb.Left;

            var step = before ? -pageRows : pageRows;

            ScrollRequested?.Invoke(Math.Clamp(position + step, 0, maximum));
        }

        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!dragging)
            return;

        var track = TrackBounds;

        var travel = Axis == Orientation.Vertical
            ? track.Height - ThumbBounds.Height : track.Width - ThumbBounds.Width;

        if (travel <= 0)
            return;

        var offset = Axis == Orientation.Vertical ? e.Y - dragOffset - track.Y : e.X - dragOffset - track.X;

        var value = (int)Math.Round(offset * (double)maximum / travel);

        ScrollRequested?.Invoke(Math.Clamp(value, 0, maximum));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left)
            return;

        dragging = false;

        Capture = false;

        Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);

        if (!Capture)
        {
            dragging = false;

            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);

        hovered = true;

        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        hovered = false;

        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        WheelScrolled?.Invoke(e.Delta);

        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }

        base.OnMouseWheel(e);
    }
}
