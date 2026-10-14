// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Drawing2D;
using TrackMirror.Constants;

namespace TrackMirror.UI.Theme;

internal static class UiTheme
{
    internal static readonly Color Background = Color.FromArgb(17, 20, 27);
    internal static readonly Color Surface = Color.FromArgb(25, 30, 40);
    internal static readonly Color Raised = Color.FromArgb(35, 42, 56);
    internal static readonly Color Border = Color.FromArgb(46, 55, 72);
    internal static readonly Color Text = Color.FromArgb(231, 237, 247);
    internal static readonly Color Muted = Color.FromArgb(153, 166, 189);
    internal static readonly Color Accent = Color.FromArgb(66, 105, 225);
    internal static readonly Color AccentHover = Color.FromArgb(80, 121, 243);
    internal static readonly Color Selected = Color.FromArgb(35, 54, 84);
    internal static readonly Color AccentText = Color.FromArgb(172, 199, 255);
    internal static readonly Color Amber = Color.FromArgb(255, 213, 140);
    internal static readonly Color Error = Color.FromArgb(255, 163, 177);
    internal static readonly Color Viewer = Color.FromArgb(11, 14, 20);
    internal static readonly Color ScrollThumb = Color.FromArgb(77, 91, 114);
    internal static readonly Color ActivityMouse = Color.FromArgb(96, 218, 206);
    internal static Label Label(string text, float size = 9.5f, bool bold = false,
        bool muted = false, bool wrap = false)
    {
        return new Label
        {
            Text = text,
            AutoSize = wrap,
            AutoEllipsis = !wrap,
            Dock = DockStyle.Fill,
            Margin = wrap ? new Padding(0, 4, 0, 8) : Padding.Empty,
            ForeColor = muted ? Muted : Text,
            BackColor = Color.Transparent,
            Font = new Font(UiText.FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular),
            TextAlign = wrap ? ContentAlignment.TopLeft : ContentAlignment.MiddleLeft
        };
    }

    internal static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();

        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));

        if (diameter < 2)
        {
            path.AddRectangle(bounds);

            return path;
        }

        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);

        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);

        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);

        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);

        path.CloseFigure();

        return path;
    }
}
