// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using TrackMirror.Constants;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class ZoomImageView : UserControl
{
    internal const double MaximumZoom = 4;
    internal const double ZoomStep = 1.25;
    private readonly Bitmap _image;
    private readonly CaptureScrollBar _vertical = new() { AccessibleName = UiText.VerticalScroll };
    private readonly CaptureScrollBar _horizontal = new()
    {
        Axis = Orientation.Horizontal, Dock = DockStyle.Bottom, Height = 14,
        AccessibleName = UiText.HorizontalScroll
    };
    private Point offset;
    private Size page;
    private Point? dragStart;
    private Point dragOffset;
    private int verticalWheel;
    private int horizontalWheel;
    private int zoomWheel;
    private bool updating;

    internal event Action? ViewChanged;

    internal double ZoomFactor { get; private set; } = 1;

    internal bool IsFit { get; private set; } = true;

    internal double MinimumZoom => Math.Min(0.05, FitScale);

    internal Size ImageSize => _image.Size;

    internal Point ViewCenter => new(page.Width / 2, page.Height / 2);

    internal Rectangle ImageBounds => new(
        Math.Max(0, (page.Width - ScaledSize.Width) / 2) - offset.X,
        Math.Max(0, (page.Height - ScaledSize.Height) / 2) - offset.Y,
        ScaledSize.Width, ScaledSize.Height);

    private Size ScaledSize => new(Math.Max(1, (int)Math.Round(_image.Width * ZoomFactor)),
        Math.Max(1, (int)Math.Round(_image.Height * ZoomFactor)));

    private double FitScale => Math.Min(1, Math.Min(Math.Max(1, ClientSize.Width) / (double)_image.Width,
        Math.Max(1, ClientSize.Height) / (double)_image.Height));

    internal ZoomImageView(Bitmap image)
    {
        _image = image;

        Dock = DockStyle.Fill;

        BackColor = UiTheme.Viewer;

        AccessibleRole = AccessibleRole.Graphic;

        AccessibleName = ZoomText.Image;

        TabStop = true;

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        Controls.Add(_vertical);

        Controls.Add(_horizontal);

        _vertical.ScrollRequested += value => SetOffset(offset.X, value);

        _horizontal.ScrollRequested += value => SetOffset(value, offset.Y);

        _vertical.WheelScrolled += delta => ScrollWheel(delta);

        _horizontal.WheelScrolled += delta => ScrollWheel(delta, true);

        Reflow();
    }

    internal void Fit()
    {
        IsFit = true;

        Reflow();
    }

    internal void ZoomAt(double factor, Point anchor)
    {
        var bounds = ImageBounds;

        var imageX = (anchor.X - bounds.Left) / ZoomFactor;

        var imageY = (anchor.Y - bounds.Top) / ZoomFactor;

        IsFit = false;

        ZoomFactor = Math.Clamp(factor, MinimumZoom, MaximumZoom);

        Reflow();

        SetOffset((int)Math.Round(imageX * ZoomFactor - anchor.X
            + Math.Max(0, (page.Width - ScaledSize.Width) / 2)),
            (int)Math.Round(imageY * ZoomFactor - anchor.Y + Math.Max(0, (page.Height - ScaledSize.Height) / 2)));
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);

        Reflow();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);

        _vertical.Width = _horizontal.Height = Math.Max(14, (int)(14 * DeviceDpi / 96f));

        Reflow();
    }

    private void Reflow()
    {
        if (_image == null || updating || IsDisposed || Disposing)
            return;

        updating = true;

        try
        {
            if (IsFit)
            {
                ZoomFactor = FitScale;
            }

            var size = ScaledSize;

            var needsVertical = size.Height > ClientSize.Height;

            var needsHorizontal = size.Width > ClientSize.Width;

            for (var attempt = 0; attempt < 2; attempt++)
            {
                needsVertical |= size.Height > ClientSize.Height - (needsHorizontal ? _horizontal.Height : 0);

                needsHorizontal |= size.Width > ClientSize.Width - (needsVertical ? _vertical.Width : 0);
            }

            page = new Size(Math.Max(1, ClientSize.Width - (needsVertical ? _vertical.Width : 0)),
                Math.Max(1, ClientSize.Height - (needsHorizontal ? _horizontal.Height : 0)));

            SetOffset(offset.X, offset.Y);

            Cursor = needsVertical || needsHorizontal ? Cursors.Hand : Cursors.Default;

            Invalidate();
        }
        finally
        {
            updating = false;
        }

        ViewChanged?.Invoke();
    }

    private void SetOffset(int x, int y)
    {
        var size = ScaledSize;

        offset = new Point(Math.Clamp(x, 0, Math.Max(0, size.Width - page.Width)),
            Math.Clamp(y, 0, Math.Max(0, size.Height - page.Height)));

        _vertical.SetViewport(size.Height, page.Height, offset.Y);

        _horizontal.SetViewport(size.Width, page.Width, offset.X);

        Invalidate();
    }

    private void ScrollWheel(int delta, bool across = false)
    {
        var useHorizontal = across || _horizontal.Visible && !_vertical.Visible;

        var pending = (useHorizontal ? horizontalWheel : verticalWheel) + delta;

        var notches = pending / 120;

        if (useHorizontal)
        {
            horizontalWheel = pending % 120;
        }
        else
        {
            verticalWheel = pending % 120;
        }

        var lines = SystemInformation.MouseWheelScrollLines;

        var step = lines < 0 ? (useHorizontal ? page.Width : page.Height)
            : lines * Math.Max(1, (int)(18 * DeviceDpi / 96f));

        SetOffset(useHorizontal ? offset.X - notches * step : offset.X,
            useHorizontal ? offset.Y : offset.Y - notches * step);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var state = e.Graphics.Save();

        e.Graphics.SetClip(new Rectangle(Point.Empty, page), CombineMode.Intersect);

        e.Graphics.InterpolationMode = ZoomFactor >= 1
            ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;

        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

        using var attributes = new ImageAttributes();

        attributes.SetWrapMode(WrapMode.TileFlipXY);

        e.Graphics.DrawImage(_image, ImageBounds, 0, 0, _image.Width, _image.Height, GraphicsUnit.Pixel, attributes);

        e.Graphics.Restore(state);
    }

    internal void HandleWheel(int delta, Point location, Keys modifiers)
    {
        if (modifiers.HasFlag(Keys.Control))
        {
            zoomWheel += delta;

            var notches = zoomWheel / 120;

            zoomWheel %= 120;

            if (notches != 0)
            {
                ZoomAt(ZoomFactor * Math.Pow(ZoomStep, notches), location);
            }
        }
        else
        {
            ScrollWheel(delta, modifiers.HasFlag(Keys.Shift));
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        HandleWheel(e.Delta, e.Location, ModifierKeys);

        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }

        base.OnMouseWheel(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        Focus();

        if (e.Button != MouseButtons.Left || !new Rectangle(Point.Empty, page).Contains(e.Location)
            || ScaledSize.Width <= page.Width && ScaledSize.Height <= page.Height)
            return;

        dragStart = e.Location;

        dragOffset = offset;

        Capture = true;

        Cursor = Cursors.SizeAll;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (dragStart is not { } origin)
            return;

        SetOffset(dragOffset.X + origin.X - e.X, dragOffset.Y + origin.Y - e.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left)
            return;

        Capture = false;
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);

        if (!Capture)
        {
            dragStart = null;

            Cursor = ScaledSize.Width > page.Width || ScaledSize.Height > page.Height ? Cursors.Hand : Cursors.Default;
        }
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        var line = Math.Max(1, (int)(18 * DeviceDpi / 96f));

        var next = keyData switch
        {
            Keys.Left => new Point(offset.X - line, offset.Y),
            Keys.Right => new Point(offset.X + line, offset.Y),
            Keys.Up => new Point(offset.X, offset.Y - line),
            Keys.Down => new Point(offset.X, offset.Y + line),
            Keys.PageUp => new Point(offset.X, offset.Y - page.Height),
            Keys.PageDown => new Point(offset.X, offset.Y + page.Height),
            Keys.Home => Point.Empty,
            Keys.End => new Point(ScaledSize.Width, ScaledSize.Height),
            _ => (Point?)null
        };

        if (next == null)
            return base.ProcessCmdKey(ref message, keyData);

        SetOffset(next.Value.X, next.Value.Y);

        return true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _image.Dispose();
        }
    }
}
