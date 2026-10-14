// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.ComponentModel;
using System.Drawing.Drawing2D;
using TrackMirror.Constants;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class ActionButton : Button
{
    private bool primary;
    private bool hovered;
    private bool pressed;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool IsPrimary
    {
        get => primary;
        set
        {
            primary = value;

            Invalidate();
        }
    }

    internal ActionButton(string text, bool primary = false)
    {
        this.primary = primary;

        Text = text;

        Size = new Size(140, 40);

        Margin = new Padding(0, 0, 8, 0);

        Font = new Font(UiText.FontFamily, 9.5f);

        ForeColor = UiTheme.Text;

        BackColor = primary ? UiTheme.Accent : UiTheme.Raised;

        FlatStyle = FlatStyle.Flat;

        FlatAppearance.BorderSize = 0;

        Cursor = Cursors.Hand;

        SetStyle(
            ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
            true
        );
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2)
            return;

        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Surface);

        var color = UiTheme.Raised;

        if (Enabled)
        {
            color = primary
                ? (hovered ? UiTheme.AccentHover : UiTheme.Accent)
                : (hovered ? UiTheme.Border : UiTheme.Raised);
        }

        if (pressed && Enabled)
        {
            color = UiTheme.Selected;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var shape = UiTheme.RoundedRectangle(
            new Rectangle(0, 0, Width - 1, Height - 1),
            (int)(8 * DeviceDpi / 96f)
        );

        using var fill = new SolidBrush(color);

        e.Graphics.FillPath(fill, shape);

        if (!primary)
        {
            using var edge = new Pen(UiTheme.Border);

            e.Graphics.DrawPath(edge, shape);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            Enabled ? ForeColor : UiTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
        );

        if (Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(
                e.Graphics,
                Rectangle.Inflate(ClientRectangle, -6, -6),
                UiTheme.AccentText,
                color
            );
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovered = true;

        Invalidate();

        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = false;

        pressed = false;

        Invalidate();

        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        pressed = e.Button == MouseButtons.Left;

        Invalidate();

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        pressed = false;

        Invalidate();

        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            pressed = true;

            Invalidate();
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        pressed = false;

        Invalidate();

        base.OnKeyUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();

        base.OnEnabledChanged(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        pressed = false;

        Invalidate();

        base.OnLostFocus(e);
    }
}
