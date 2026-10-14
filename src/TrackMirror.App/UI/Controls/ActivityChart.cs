// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Drawing2D;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class ActivityChart : Control
{
    private readonly ToolTip _tooltips = new() { InitialDelay = 150, AutoPopDelay = 15000 };
    private readonly Font _chartFont = new(UiText.FontFamily, 9);
    private HubstaffActivityDay day = new(DateTime.Today, []);
    private int hoveredIndex = -1;

    internal ActivityChart()
    {
        Dock = DockStyle.Fill;

        BackColor = UiTheme.Surface;

        ForeColor = UiTheme.Text;

        Font = _chartFont;

        AccessibleName = ActivityText.Chart;

        AccessibleRole = AccessibleRole.Graphic;

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    internal void SetDay(HubstaffActivityDay value)
    {
        day = value;

        hoveredIndex = -1;

        _tooltips.SetToolTip(this, string.Empty);

        AccessibleDescription = day.Entries.Count == 0 ? ActivityText.Empty
            : string.Join(Environment.NewLine, day.Entries.Select(Describe));

        Invalidate();
    }

    private Rectangle PlotBounds()
    {
        var scale = DeviceDpi / 96f;

        return new Rectangle((int)(50 * scale), (int)(44 * scale),
            Math.Max(1, Width - (int)(72 * scale)), Math.Max(1, Height - (int)(78 * scale)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;

        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var scale = DeviceDpi / 96f;

        var plot = PlotBounds();

        TextRenderer.DrawText(graphics, ActivityText.Chart, Font,
            new Rectangle(0, 0, Width, (int)(28 * scale)), ForeColor, TextFormatFlags.Left);

        var colors = new[] { UiTheme.AccentText, UiTheme.ActivityMouse, UiTheme.Amber };

        var labels = new[] { ActivityText.Overall, ActivityText.Mouse, ActivityText.Keyboard };

        for (var i = 0; i < labels.Length; i++)
        {
            var x = Width - (int)((310 - i * 105) * scale);

            using var color = new SolidBrush(colors[i]);

            graphics.FillEllipse(color, x, 5 * scale, 7 * scale, 7 * scale);

            TextRenderer.DrawText(graphics, labels[i], Font,
                new Rectangle(x + (int)(12 * scale), 0, (int)(94 * scale), (int)(28 * scale)),
                UiTheme.Muted, TextFormatFlags.Left);
        }

        using var grid = new Pen(UiTheme.Border);

        for (var percent = 0; percent <= 100; percent += 25)
        {
            var y = plot.Bottom - plot.Height * percent / 100;

            graphics.DrawLine(grid, plot.Left, y, plot.Right, y);

            TextRenderer.DrawText(graphics, string.Format(ActivityText.PercentFormat, percent), Font,
                new Rectangle(0, y - (int)(9 * scale), plot.Left - (int)(8 * scale), (int)(22 * scale)),
                UiTheme.Muted, TextFormatFlags.Right);
        }

        var step = plot.Width < 600 * scale ? 4 : 3;

        for (var hour = 0; hour <= 24; hour += step)
        {
            var x = plot.Left + plot.Width * hour / 24;

            graphics.DrawLine(grid, x, plot.Top, x, plot.Bottom);

            TextRenderer.DrawText(graphics, string.Format(ActivityText.HoursFormat, hour), Font,
                new Rectangle(x - (int)(25 * scale), plot.Bottom + (int)(10 * scale),
                    (int)(50 * scale), (int)(22 * scale)), UiTheme.Muted, TextFormatFlags.HorizontalCenter);
        }

        using var fill = new SolidBrush(Color.FromArgb(40, UiTheme.Accent));

        using var overall = new Pen(UiTheme.AccentText, 2 * scale);

        using var mouse = new Pen(UiTheme.ActivityMouse, 2 * scale);

        using var keyboard = new Pen(UiTheme.Amber, 2 * scale) { DashStyle = DashStyle.Dash };

        foreach (var entry in day.Entries)
        {
            var span = IntervalBounds(entry, plot);

            var y = plot.Bottom - (float)entry.OverallSeconds / entry.TrackedSeconds * plot.Height;

            graphics.FillRectangle(fill, span.Left, y, span.Width, plot.Bottom - y);

            DrawActivity(graphics, plot, span, overall, entry.OverallSeconds, entry.TrackedSeconds);

            DrawActivity(graphics, plot, span, mouse, entry.MouseSeconds, entry.TrackedSeconds);

            DrawActivity(graphics, plot, span, keyboard, entry.KeyboardSeconds, entry.TrackedSeconds);
        }

        if (day.Entries.Count == 0)
        {
            TextRenderer.DrawText(graphics, ActivityText.Empty, Font, plot, UiTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private RectangleF IntervalBounds(HubstaffActivity entry, Rectangle plot)
    {
        var start = entry.StartedUtc.ToLocalTime();

        var end = entry.StartedUtc.AddSeconds(entry.TrackedSeconds).ToLocalTime();

        var left = plot.Left + (float)(start.TimeOfDay.TotalHours / 24) * plot.Width;

        var endHours = end.Date > day.Date ? 24 : end.TimeOfDay.TotalHours;

        var right = Math.Min(plot.Right, plot.Left + (float)(endHours / 24) * plot.Width);

        return new RectangleF(left, plot.Top, Math.Max(1, right - left), plot.Height);
    }

    private static void DrawActivity(Graphics graphics, Rectangle plot, RectangleF span,
        Pen pen, int seconds, int tracked)
    {
        var y = plot.Bottom - (float)seconds / tracked * plot.Height;

        graphics.DrawLine(pen, span.Left, y, span.Right, y);
    }

    private static string Describe(HubstaffActivity entry)
    {
        return string.Format(ActivityText.IntervalFormat,
            entry.StartedUtc.ToLocalTime(), entry.StartedUtc.AddSeconds(entry.TrackedSeconds).ToLocalTime(),
            entry.TrackedSeconds, entry.OverallSeconds * 100d / entry.TrackedSeconds,
            entry.MouseSeconds * 100d / entry.TrackedSeconds, entry.KeyboardSeconds * 100d / entry.TrackedSeconds);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var plot = PlotBounds();

        var index = -1;

        if (plot.Contains(e.Location))
        {
            for (var i = day.Entries.Count - 1; i >= 0; i--)
            {
                var span = IntervalBounds(day.Entries[i], plot);

                if (e.X >= span.Left && e.X <= span.Right)
                {
                    index = i;

                    break;
                }
            }
        }

        if (index == hoveredIndex)
            return;

        hoveredIndex = index;

        _tooltips.SetToolTip(this, index < 0 ? string.Empty : Describe(day.Entries[index]));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        hoveredIndex = -1;

        _tooltips.SetToolTip(this, string.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tooltips.Dispose();

            _chartFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
