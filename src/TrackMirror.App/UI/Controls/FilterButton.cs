// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Drawing2D;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class FilterButton : RadioButton
{
    private bool hovered;

    internal FilterButton(string text)
    {
        Text = text;

        Dock = DockStyle.Fill;

        Appearance = Appearance.Button;

        FlatStyle = FlatStyle.Flat;

        BackColor = UiTheme.Surface;

        ForeColor = UiTheme.Muted;

        Cursor = Cursors.Hand;

        Margin = Padding.Empty;

        AccessibleRole = AccessibleRole.RadioButton;

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

        using var shape = UiTheme.RoundedRectangle(bounds, (int)(6 * DeviceDpi / 96f));

        var fillColor = Checked ? UiTheme.Selected : hovered && Enabled ? UiTheme.Border : UiTheme.Raised;

        using var fill = new SolidBrush(fillColor);

        using var edge = new Pen(Checked ? UiTheme.Accent : UiTheme.Border);

        e.Graphics.FillPath(fill, shape);

        e.Graphics.DrawPath(edge, shape);

        var inset = (int)(4 * DeviceDpi / 96f);

        var textBounds = Rectangle.Inflate(bounds, -inset, 0);

        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, Enabled ? ForeColor : UiTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        if (Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -inset, -inset),
                ForeColor, fillColor);
        }
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        ForeColor = Checked ? UiTheme.AccentText : UiTheme.Muted;

        Invalidate();

        base.OnCheckedChanged(e);
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
}
