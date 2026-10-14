// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Drawing2D;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class SurfacePanel : Panel
{
    internal SurfacePanel()
    {
        BackColor = UiTheme.Surface;

        SetStyle(
            ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
            true
        );
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Background);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var shape = UiTheme.RoundedRectangle(
            new Rectangle(0, 0, Width - 1, Height - 1),
            (int)(12 * DeviceDpi / 96f)
        );

        using var fill = new SolidBrush(BackColor);

        using var edge = new Pen(UiTheme.Border);

        e.Graphics.FillPath(fill, shape);

        e.Graphics.DrawPath(edge, shape);

        base.OnPaint(e);
    }
}
