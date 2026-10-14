// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Drawing2D;
using TrackMirror.Privacy.Models;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class InstallationProgressBar : Control
{
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 50 };
    private PrivacyInstallProgress progress = new(PrivacyInstallStage.Idle);
    private float position;

    internal InstallationProgressBar()
    {
        Height = 10;

        Dock = DockStyle.Fill;

        Margin = new Padding(0, 6, 0, 6);

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        _animation.Tick += (_, _) =>
        {
            position = (position + 0.04f) % 1.3f;

            Invalidate();
        };
    }

    internal void SetProgress(PrivacyInstallProgress value)
    {
        progress = value;

        _animation.Enabled = progress.Busy && progress.Total <= 0 && Visible;

        Invalidate();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        _animation.Enabled = progress.Busy && progress.Total <= 0 && Visible;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2)
            return;

        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Surface);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var shape = UiTheme.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2);

        using var background = new SolidBrush(UiTheme.Raised);

        using var fill = new SolidBrush(UiTheme.AccentText);

        e.Graphics.FillPath(background, shape);

        e.Graphics.SetClip(shape);

        if (progress.Busy && progress.Total <= 0)
        {
            e.Graphics.FillRectangle(fill, (position - 0.3f) * Width, 0, Width * 0.3f, Height);
        }
        else if (progress.Total > 0)
        {
            var fraction = Math.Clamp((double)progress.Completed / progress.Total, 0, 1);

            e.Graphics.FillRectangle(fill, 0, 0, (float)(Width * fraction), Height);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animation.Dispose();
        }

        base.Dispose(disposing);
    }
}
