// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Models;
using TrackMirror.Monitoring;
using TrackMirror.Constants;
using TrackMirror.UI.Theme;
using TrackMirror.UI.Interop;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace TrackMirror.UI.Forms;

internal sealed class ScreenshotCountdownForm : Form
{
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolTip _tooltips = new();
    private readonly object _dragGate = new();

    private ScreenshotCountdownOptions options = new();

    private string countdown = "--:--";
    private int? remainingSeconds;

    private Point? dragOffset;
    private MiddleButtonDragListener? middleListener;
    private bool middleDragFailed;
    private bool middleDragging;
    private (Point Position, bool Completed)? pendingDrag;
    private bool dragPosted;
    private int dragGeneration;

    internal event Action<ScreenshotCountdownOptions>? OptionsChanged;

    internal event Action? SettingsRequested;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000 | 0x00000080 | 0x00080000; // NOACTIVATE | TOOLWINDOW | LAYERED

            if (options.MiddleButtonDrag && !middleDragFailed)
                parameters.ExStyle |= 0x00000020; // TRANSPARENT: clicks reach windows in other processes too.

            return parameters;
        }
    }

    internal ScreenshotCountdownForm()
    {
        SuspendLayout();

        Text = CountdownText.Heading;
        AccessibleName = CountdownText.Heading;
        Icon = ApplicationIcon.Main;
        Font = new Font(UiText.FontFamily, 9);

        // The overlay combines monitor DPI and its independent user scale explicitly.
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(124, 46);

        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Text;
        DoubleBuffered = true;
        Cursor = Cursors.SizeAll;

        AddMenuItems(_menu.Items);
        ContextMenuStrip = _menu;

        ResumeLayout(false);
    }

    internal void AddMenuItems(ToolStripItemCollection items)
    {
        items.Add(CountdownText.TopLeft, null, (_, _) => MoveToCorner(false, false));
        items.Add(CountdownText.TopRight, null, (_, _) => MoveToCorner(true, false));
        items.Add(CountdownText.BottomLeft, null, (_, _) => MoveToCorner(false, true));
        items.Add(CountdownText.BottomRight, null, (_, _) => MoveToCorner(true, true));
        items.Add(CountdownText.Reset, null, (_, _) =>
            OptionsChanged?.Invoke(options with { Left = null, Top = null }));
        items.Add(new ToolStripSeparator());
        items.Add(SettingsText.SettingsTab, null, (_, _) => SettingsRequested?.Invoke());
        items.Add(CountdownText.Hide, null, (_, _) => OptionsChanged?.Invoke(options with { Enabled = false }));
    }

    internal ScreenshotCountdownOptions ApplyOptions(ScreenshotCountdownOptions value)
    {
        var keepPosition = value.Left == options.Left && value.Top == options.Top;
        // Finish the current gesture before changing options or visibility. This keeps its
        // final position in the caller's save and prevents nested settings saves from Hide().
        var finalPosition = StopMiddleDrag(publish: false);

        if (keepPosition && finalPosition.HasValue)
            value = value with { Left = Left, Top = Top };

        options = value;

        if (!value.MiddleButtonDrag)
            middleDragFailed = false;

        Cursor = value.MiddleButtonDrag && !middleDragFailed ? Cursors.Default : Cursors.SizeAll;

        _ = Handle;
        UpdateStyles();
        ResizeForDisplay();

        var primary = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
        var requested = value.Left is { } x && value.Top is { } y
            ? new Point(x, y) : new Point(primary.Right - Width - 16, primary.Top + 64);
        var area = Screen.AllScreens.FirstOrDefault(screen => screen.WorkingArea.IntersectsWith(new Rectangle(requested, Size)))
            ?.WorkingArea ?? primary;
        Location = ConstrainLocation(requested, Size, area);

        if (!value.Enabled || value.Mode == ScreenshotCountdownMode.Default)
            Hide();

        UpdateMiddleDrag();

        RefreshSurface();

        return options;
    }

    internal void UpdateState(ScreenshotScheduleSnapshot snapshot)
    {
        var scheduled = snapshot.Status == ScreenshotScheduleStatus.Scheduled && snapshot.RemainingSeconds > 0;

        countdown = CountdownDisplay.TimeText(snapshot);
        remainingSeconds = scheduled ? snapshot.RemainingSeconds : null;

        var description = CountdownDisplay.StatusText(snapshot);

        if (middleDragFailed)
            description += Environment.NewLine + CountdownText.MiddleDragUnavailable;

        if (AccessibleDescription == description)
            return;

        AccessibleDescription = description;
        _tooltips.SetToolTip(this, description);

        RefreshSurface();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var surface = RenderSurface();
        e.Graphics.CompositingMode = CompositingMode.SourceCopy;
        e.Graphics.DrawImageUnscaled(surface, Point.Empty);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        ResizeForDisplay();

        RefreshSurface();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (Visible)
            RefreshSurface();

        UpdateMiddleDrag();
    }

    private void ResizeForDisplay()
    {
        var scale = DeviceDpi / 96f * options.ScalePercent / 100f;

        ClientSize = new Size((int)Math.Ceiling(124 * scale), (int)Math.Ceiling(46 * scale));
    }

    private void RefreshSurface()
    {
        if (!IsHandleCreated || IsDisposed)
            return;

        using var surface = RenderSurface();
        LayeredWindow.Update(Handle, surface);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0021) // WM_MOUSEACTIVATE
        {
            message.Result = new IntPtr(3); // MA_NOACTIVATE

            return;
        }

        base.WndProc(ref message);

        if (message.Msg == 0x02E0) // WM_DPICHANGED: apply user scale after WinForms finishes the DPI transition.
        {
            ResizeForDisplay();
            Location = ConstrainLocation(Location, Size, Screen.FromRectangle(Bounds).WorkingArea);

            RefreshSurface();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Left && (!options.MiddleButtonDrag || middleDragFailed))
        {
            dragOffset = e.Location;
            Capture = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (dragOffset is { } delta)
        {
            var position = new Point(MousePosition.X - delta.X, MousePosition.Y - delta.Y);
            Location = ConstrainLocation(position, Size, Screen.FromPoint(MousePosition).WorkingArea);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Left && dragOffset.HasValue)
        {
            dragOffset = null;
            Capture = false;

            OptionsChanged?.Invoke(options with { Left = Left, Top = Top });
        }
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        if (!Capture)
            dragOffset = null;

        base.OnMouseCaptureChanged(e);
    }

    private void MoveToCorner(bool right, bool bottom)
    {
        var area = Screen.FromRectangle(Bounds).WorkingArea;
        var position = new Point(right ? area.Right - Width - 16 : area.Left + 16,
            bottom ? area.Bottom - Height - 16 : area.Top + 64);
        position = ConstrainLocation(position, Size, area);

        OptionsChanged?.Invoke(options with { Left = position.X, Top = position.Y });
    }

    internal static Point ConstrainLocation(Point location, Size size, Rectangle area) => new(
        Math.Clamp(location.X, area.Left, Math.Max(area.Left, area.Right - size.Width)),
        Math.Clamp(location.Y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);

        middleListener?.SetTarget(Bounds);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);

        middleListener?.SetTarget(Bounds);
    }

    private void UpdateMiddleDrag()
    {
        if (!Visible || !options.Enabled || !options.MiddleButtonDrag)
        {
            StopMiddleDrag();

            return;
        }

        if (middleListener != null)
        {
            middleListener.SetTarget(Bounds);

            return;
        }

        middleListener = MiddleButtonDragListener.Start(Bounds, QueueMiddleDrag);
        middleDragFailed = middleListener == null;
        Cursor = middleDragFailed ? Cursors.SizeAll : Cursors.Default;
        UpdateStyles();

        if (middleDragFailed)
        {
            AccessibleDescription = CountdownText.MiddleDragUnavailable;
            _tooltips.SetToolTip(this, AccessibleDescription);
        }
    }

    private void QueueMiddleDrag(Point position, bool completed)
    {
        lock (_dragGate)
        {
            pendingDrag = (position, completed || pendingDrag?.Completed == true);

            if (dragPosted)
                return;

            dragPosted = true;
            var generation = dragGeneration;

            try
            {
                BeginInvoke(() => ApplyMiddleDrag(generation));
            }
            catch (InvalidOperationException)
            {
                dragPosted = false;
                pendingDrag = null;
            }
        }
    }

    private void ApplyMiddleDrag(int generation)
    {
        (Point Position, bool Completed)? update;

        lock (_dragGate)
        {
            if (generation != dragGeneration)
                return;

            update = pendingDrag;
            pendingDrag = null;
            dragPosted = false;
        }

        if (update is not { } drag || !Visible || IsDisposed || middleListener == null)
            return;

        middleDragging = !drag.Completed;
        Location = ConstrainLocation(drag.Position, Size, Screen.FromRectangle(new Rectangle(drag.Position, Size)).WorkingArea);

        if (drag.Completed)
            OptionsChanged?.Invoke(options with { Left = Left, Top = Top });
    }

    private Point? StopMiddleDrag(bool publish = true)
    {
        middleListener?.Dispose();
        middleListener = null;

        (Point Position, bool Completed)? final;

        lock (_dragGate)
        {
            dragGeneration++;
            final = pendingDrag;
            pendingDrag = null;
            dragPosted = false;
        }

        if (final is { } drag)
            Location = ConstrainLocation(drag.Position, Size, Screen.FromRectangle(new Rectangle(drag.Position, Size)).WorkingArea);

        if (middleDragging || final != null)
        {
            middleDragging = false;

            if (publish)
                OptionsChanged?.Invoke(options with { Left = Left, Top = Top });

            return Location;
        }

        return null;
    }

    internal Bitmap RenderSurface()
    {
        var surface = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(surface);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var scale = DeviceDpi / 96f * options.ScalePercent / 100f;
        var alpha = (int)Math.Round(options.BackgroundOpacityPercent * 255 / 100f);
        var highlighted = remainingSeconds is > 0 && remainingSeconds <= options.HighlightSeconds;

        using var shape = UiTheme.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), (int)(10 * scale));
        using var background = new SolidBrush(Color.FromArgb(alpha, UiTheme.Surface));
        using var border = new Pen(Color.FromArgb(alpha, highlighted ? UiTheme.Amber : UiTheme.Border));

        graphics.FillPath(background, shape);
        graphics.DrawPath(border, shape);

        using var timeFont = new Font("Consolas", 33.33f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        using var timeBrush = new SolidBrush(highlighted ? UiTheme.Amber : UiTheme.AccentText);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter,
            LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center
        };

        graphics.DrawString(countdown, timeFont, timeBrush,
            new RectangleF(4 * scale, 0, Width - 8 * scale, Height), format);

        return surface;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopMiddleDrag(publish: false);
            _tooltips.Dispose();
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }
}
