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

    private ScreenshotCountdownOptions options = new();

    private string caption = CountdownText.Waiting;
    private string countdown = "--:--";
    private int? remainingSeconds;

    private Point? dragOffset;

    internal event Action<ScreenshotCountdownOptions>? OptionsChanged;

    internal event Action? SettingsRequested;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000 | 0x00000080 | 0x00080000; // NOACTIVATE | TOOLWINDOW | LAYERED

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
        ClientSize = new Size(144, 70);

        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Text;
        DoubleBuffered = true;
        Cursor = Cursors.SizeAll;

        _menu.Items.Add(CountdownText.TopLeft, null, (_, _) => MoveToCorner(false, false));
        _menu.Items.Add(CountdownText.TopRight, null, (_, _) => MoveToCorner(true, false));
        _menu.Items.Add(CountdownText.BottomLeft, null, (_, _) => MoveToCorner(false, true));
        _menu.Items.Add(CountdownText.BottomRight, null, (_, _) => MoveToCorner(true, true));
        _menu.Items.Add(CountdownText.Reset, null, (_, _) =>
            OptionsChanged?.Invoke(options with { Left = null, Top = null }));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(SettingsText.SettingsTab, null, (_, _) => SettingsRequested?.Invoke());
        _menu.Items.Add(CountdownText.Hide, null, (_, _) => OptionsChanged?.Invoke(options with { Enabled = false }));
        ContextMenuStrip = _menu;

        ResumeLayout(false);
    }

    internal void ApplyOptions(ScreenshotCountdownOptions value)
    {
        options = value;

        _ = Handle;
        ResizeForDisplay();

        var primary = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
        var requested = value.Left is { } x && value.Top is { } y
            ? new Point(x, y) : new Point(primary.Right - Width - 16, primary.Top + 64);
        var area = Screen.AllScreens.FirstOrDefault(screen => screen.WorkingArea.IntersectsWith(new Rectangle(requested, Size)))
            ?.WorkingArea ?? primary;
        Location = ConstrainLocation(requested, Size, area);

        if (!value.Enabled)
            Hide();

        RefreshSurface();
    }

    internal void UpdateState(ScreenshotScheduleSnapshot snapshot)
    {
        var scheduled = snapshot.Status == ScreenshotScheduleStatus.Scheduled && snapshot.RemainingSeconds > 0;

        caption = scheduled ? CountdownText.Scheduled : snapshot.Status switch
        {
            ScreenshotScheduleStatus.Unavailable => CountdownText.Unavailable,
            ScreenshotScheduleStatus.Stopped => CountdownText.Stopped,
            ScreenshotScheduleStatus.Disabled => CountdownText.Disabled,
            _ => CountdownText.Waiting
        };
        countdown = scheduled ? TimeSpan.FromSeconds(snapshot.RemainingSeconds!.Value).ToString(@"mm\:ss") : "--:--";
        remainingSeconds = scheduled ? snapshot.RemainingSeconds : null;

        var description = caption + ": " + countdown;

        if (AccessibleDescription == description)
            return;

        AccessibleDescription = description;

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
    }

    private void ResizeForDisplay()
    {
        var scale = DeviceDpi / 96f * options.ScalePercent / 100f;

        ClientSize = new Size((int)Math.Ceiling(144 * scale), (int)Math.Ceiling(70 * scale));
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

        if (e.Button == MouseButtons.Left)
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

        using var captionFont = new Font(Font.FontFamily, 11.33f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var timeFont = new Font("Consolas", 33.33f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        using var captionBrush = new SolidBrush(UiTheme.Muted);
        using var timeBrush = new SolidBrush(highlighted ? UiTheme.Amber : UiTheme.AccentText);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter,
            LineAlignment = StringAlignment.Center
        };

        var inset = 10 * scale;

        graphics.DrawString(caption, captionFont, captionBrush,
            new RectangleF(inset, 5 * scale, Width - inset * 2, 18 * scale), format);
        graphics.DrawString(countdown, timeFont, timeBrush,
            new RectangleF(inset, 24 * scale, Width - inset * 2, Height - 28 * scale), format);

        return surface;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _menu.Dispose();

        base.Dispose(disposing);
    }
}
