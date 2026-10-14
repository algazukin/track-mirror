// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.UI.Theme;
using System.Diagnostics.CodeAnalysis;

namespace TrackMirror.UI.Controls;

internal sealed class ScrollableTextField : UserControl
{
    private readonly TextViewport _text = new();
    private readonly CaptureScrollBar _scrollBar = new() { AccessibleName = UiText.VerticalScroll };
    private bool updating;

    [AllowNull]
    public override string Text
    {
        get => _text?.Text ?? string.Empty;
        set => _text.Text = value;
    }

    internal ScrollableTextField()
    {
        Dock = DockStyle.Fill;

        TabStop = false;

        BackColor = UiTheme.Surface;

        ForeColor = UiTheme.Muted;

        Controls.Add(_text);

        Controls.Add(_scrollBar);

        _text.ViewportChanged += RefreshViewport;

        _text.TextChanged += (_, _) =>
        {
            RefreshLayout();

            OnTextChanged(EventArgs.Empty);
        };

        _text.HandleCreated += (_, _) => RefreshLayout();

        _text.SizeChanged += (_, _) => RefreshViewport();

        _text.FontChanged += (_, _) => RefreshLayout();

        SizeChanged += (_, _) => RefreshLayout();

        _scrollBar.ScrollRequested += line =>
        {
            _text.Focus();

            _text.ScrollTo(line);
        };

        _scrollBar.WheelScrolled += _text.ScrollWheel;

        DpiChangedAfterParent += (_, _) =>
        {
            _scrollBar.Width = Math.Max(14, (int)(14 * DeviceDpi / 96f));

            RefreshLayout();
        };
    }

    private void RefreshViewport()
    {
        if (!updating && !IsDisposed && !Disposing && !_text.IsDisposed)
        {
            _scrollBar.SetViewport(_text.LineCount, _text.PageLines, _text.TopLine);
        }
    }

    private void RefreshLayout()
    {
        if (updating || IsDisposed || Disposing || !_text.IsHandleCreated)
            return;

        var firstCharacter = Math.Max(0, _text.GetFirstCharIndexFromLine(_text.TopLine));

        updating = true;

        SuspendLayout();

        try
        {
            // Measure wrapping at full width before deciding whether the scrollbar is necessary.
            _text.Dock = DockStyle.None;

            _text.SetBounds(0, 0, ClientSize.Width, ClientSize.Height);

            _scrollBar.SetViewport(_text.LineCount, _text.PageLines, _text.TopLine);
        }
        finally
        {
            _text.Dock = DockStyle.Fill;

            ResumeLayout(true);

            updating = false;
        }

        _text.ScrollTo(_text.GetLineFromCharIndex(firstCharacter));

        RefreshViewport();
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);

        if (_text != null)
        {
            _text.BackColor = _scrollBar.BackColor = BackColor;
        }
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);

        if (_text != null)
        {
            _text.ForeColor = ForeColor;
        }
    }
}
