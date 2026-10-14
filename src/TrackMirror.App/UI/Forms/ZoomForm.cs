// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.UI.Controls;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Forms;

internal sealed class ZoomForm : Form
{
    private readonly ZoomImageView _view;
    private readonly FlowLayoutPanel _toolbar = new()
    {
        Dock = DockStyle.Top, Height = 56, Padding = new Padding(12, 10, 12, 10),
        BackColor = UiTheme.Surface, WrapContents = false
    };
    private readonly ActionButton _fit = new(ZoomText.Fit) { Width = 88, Height = 36 };
    private readonly ActionButton _actualSize = new(ZoomText.ActualSize) { Width = 68, Height = 36 };
    private readonly ActionButton _zoomOut = new(ZoomText.ZoomOut)
    {
        Width = 36, Height = 36, AccessibleName = ZoomText.ZoomOutName
    };
    private readonly ActionButton _zoomIn = new(ZoomText.ZoomIn)
    {
        Width = 36, Height = 36, AccessibleName = ZoomText.ZoomInName
    };
    private readonly Label _scale = UiTheme.Label(string.Empty, 9.5f, true);
    private readonly ToolTip _tooltips = new();

    internal string CaptureId { get; }

    internal ZoomForm(string captureId, Bitmap image, DateTime capturedUtc)
    {
        SuspendLayout();

        CaptureId = captureId;

        Text = capturedUtc.ToLocalTime().ToString(DateFormats.FullDateTime) + UiText.FullSizeTitleSuffix;

        Icon = ApplicationIcon.Main;

        Font = new Font(UiText.FontFamily, 10);

        AutoScaleMode = AutoScaleMode.Dpi;

        AutoScaleDimensions = new SizeF(96, 96);

        StartPosition = FormStartPosition.Manual;

        ClientSize = new Size(640, 440);

        BackColor = UiTheme.Background;

        _view = new ZoomImageView(image);

        _scale.Dock = DockStyle.None;

        _scale.Size = new Size(68, 36);

        _scale.TextAlign = ContentAlignment.MiddleCenter;

        _scale.Margin = new Padding(0, 0, 8, 0);

        var hint = UiTheme.Label(ZoomText.Hint, 8.5f, muted: true);

        hint.Dock = DockStyle.None;

        hint.Size = new Size(260, 36);

        _toolbar.Controls.AddRange([_fit, _actualSize, _zoomOut, _scale, _zoomIn, hint]);

        Controls.Add(_view);

        Controls.Add(_toolbar);

        _fit.Click += (_, _) => _view.Fit();

        _actualSize.Click += (_, _) => _view.ZoomAt(1, _view.ViewCenter);

        _zoomIn.Click += (_, _) => _view.ZoomAt(_view.ZoomFactor * ZoomImageView.ZoomStep, _view.ViewCenter);

        _zoomOut.Click += (_, _) => _view.ZoomAt(_view.ZoomFactor / ZoomImageView.ZoomStep, _view.ViewCenter);

        _view.ViewChanged += UpdateScale;

        _tooltips.SetToolTip(_fit, ZoomText.FitHint);

        _tooltips.SetToolTip(_actualSize, ZoomText.ActualSizeHint);

        _tooltips.SetToolTip(_zoomIn, ZoomText.ZoomInHint);

        _tooltips.SetToolTip(_zoomOut, ZoomText.ZoomOutHint);

        _tooltips.SetToolTip(_view, ZoomText.Hint);

        _tooltips.SetToolTip(hint, ZoomText.Hint);

        ResumeLayout(true);

        UpdateScale();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        var area = Screen.FromControl(Owner ?? this).WorkingArea;

        Location = new Point(area.Left + 24, area.Top + 24);

        var dpi = DeviceDpi / 96f;

        var margin = (int)(24 * dpi);

        var maximum = new Size(Math.Max(1, area.Width - margin * 2), Math.Max(1, area.Height - margin * 2));

        MinimumSize = new Size(Math.Min((int)(640 * dpi), maximum.Width),
            Math.Min((int)(380 * dpi), maximum.Height));

        var frame = new Size(Width - ClientSize.Width, Height - ClientSize.Height);

        var width = Math.Clamp(_view.ImageSize.Width + frame.Width, MinimumSize.Width, maximum.Width);

        var height = Math.Clamp(_view.ImageSize.Height + _toolbar.Height + frame.Height,
            MinimumSize.Height, maximum.Height);

        var center = Owner?.Bounds ?? area;

        Bounds = new Rectangle(
            Math.Clamp(center.Left + (center.Width - width) / 2, area.Left + margin, area.Right - width - margin),
            Math.Clamp(center.Top + (center.Height - height) / 2, area.Top + margin, area.Bottom - height - margin),
            width, height);

        _view.Fit();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        _view.Focus();
    }

    private void UpdateScale()
    {
        _scale.Text = string.Format(ZoomText.ScaleFormat, _view.ZoomFactor * 100);

        _fit.IsPrimary = _view.IsFit;

        _zoomIn.Enabled = _view.ZoomFactor < ZoomImageView.MaximumZoom;

        _zoomOut.Enabled = _view.ZoomFactor > _view.MinimumZoom;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (_view.Bounds.Contains(e.Location))
        {
            _view.HandleWheel(e.Delta, _view.PointToClient(PointToScreen(e.Location)), ModifierKeys);

            if (e is HandledMouseEventArgs handled)
            {
                handled.Handled = true;
            }
        }

        base.OnMouseWheel(e);
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.Add:
            case Keys.Control | Keys.Oemplus:
            case Keys.Control | Keys.Shift | Keys.Oemplus:
                _view.ZoomAt(_view.ZoomFactor * ZoomImageView.ZoomStep, _view.ViewCenter);
                return true;
            case Keys.Control | Keys.Subtract:
            case Keys.Control | Keys.OemMinus:
                _view.ZoomAt(_view.ZoomFactor / ZoomImageView.ZoomStep, _view.ViewCenter);
                return true;
            case Keys.Control | Keys.D0:
            case Keys.Control | Keys.NumPad0:
                _view.Fit();
                return true;
            case Keys.Control | Keys.D1:
            case Keys.Control | Keys.NumPad1:
                _view.ZoomAt(1, _view.ViewCenter);
                return true;
            case Keys.Escape:
                Close();
                return true;
            default:
                return base.ProcessCmdKey(ref message, keyData);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tooltips.Dispose();
        }

        base.Dispose(disposing);
    }
}
