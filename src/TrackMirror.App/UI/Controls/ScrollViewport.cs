// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class ScrollViewport : UserControl
{
    private readonly Panel _viewport = new();
    private readonly CaptureScrollBar _vertical = new()
    {
        Dock = DockStyle.None, AccessibleName = UiText.VerticalScroll
    };
    private readonly CaptureScrollBar _horizontal = new()
    {
        Axis = Orientation.Horizontal,
        Dock = DockStyle.None,
        Height = 14,
        AccessibleName = UiText.HorizontalScroll
    };
    private readonly Control _content;
    private readonly bool _fitWidth;
    private readonly bool _fillHeight;
    private Control? focusedControl;
    private Point offset;
    private Size pageSize;
    private int verticalWheel;
    private int horizontalWheel;
    private bool updating;

    internal ScrollViewport(Control content, bool fitWidth = true, bool fillHeight = false)
    {
        _content = content;

        _fitWidth = fitWidth;

        _fillHeight = fillHeight;

        Dock = DockStyle.Fill;

        TabStop = !fitWidth;

        BackColor = UiTheme.Background;

        content.Dock = DockStyle.None;

        content.Anchor = fitWidth ? AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            : AnchorStyles.Top | AnchorStyles.Left;

        _viewport.Controls.Add(content);

        Controls.Add(_viewport);

        Controls.Add(_vertical);

        Controls.Add(_horizontal);

        _vertical.ScrollRequested += value => SetOffset(offset.X, value);

        _horizontal.ScrollRequested += value => SetOffset(value, offset.Y);

        _vertical.WheelScrolled += delta => ScrollWheel(delta);

        _horizontal.WheelScrolled += delta => ScrollWheel(delta, true);

        SizeChanged += (_, _) => RefreshViewport();

        content.SizeChanged += (_, _) => RefreshViewport();

        HandleCreated += (_, _) => RefreshViewport();

        DpiChangedAfterParent += (_, _) =>
        {
            _vertical.Width = _horizontal.Height = Math.Max(14, (int)(14 * DeviceDpi / 96f));

            RefreshViewport();
        };

        Watch(content, true);

        RefreshViewport();
    }

    internal void RefreshViewport()
    {
        if (updating || IsDisposed || Disposing || _content.IsDisposed)
            return;

        updating = true;

        try
        {
            var needsVertical = false;

            var needsHorizontal = false;

            // Wrapping may change content height after a scrollbar reduces the available width.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                pageSize = new Size(Math.Max(1, ClientSize.Width - (needsVertical ? _vertical.Width : 0)),
                    Math.Max(1, ClientSize.Height - (needsHorizontal ? _horizontal.Height : 0)));

                _viewport.Bounds = new Rectangle(Point.Empty, pageSize);

                if (_fitWidth)
                {
                    _content.Width = pageSize.Width;
                }

                if (_fillHeight)
                {
                    _content.Height = Math.Max(pageSize.Height, _content.MinimumSize.Height);
                }

                var nextVertical = _content.Height > pageSize.Height;

                var nextHorizontal = !_fitWidth && _content.Width > pageSize.Width;

                if (nextVertical == needsVertical && nextHorizontal == needsHorizontal)
                    break;

                needsVertical = nextVertical;

                needsHorizontal = nextHorizontal;
            }

            _vertical.Visible = needsVertical;

            _horizontal.Visible = needsHorizontal;

            _vertical.SetBounds(pageSize.Width, 0, _vertical.Width, pageSize.Height);

            _horizontal.SetBounds(0, pageSize.Height, pageSize.Width, _horizontal.Height);

            SetOffset(offset.X, offset.Y);

            PerformLayout();
        }
        finally
        {
            updating = false;
        }
    }

    private void SetOffset(int x, int y)
    {
        offset = new Point(_fitWidth ? 0 : Math.Clamp(x, 0, Math.Max(0, _content.Width - pageSize.Width)),
            Math.Clamp(y, 0, Math.Max(0, _content.Height - pageSize.Height)));

        _content.Location = new Point(-offset.X, -offset.Y);

        _vertical.SetViewport(_content.Height, pageSize.Height, offset.Y);

        _horizontal.SetViewport(_fitWidth ? 0 : _content.Width, pageSize.Width, offset.X);
    }

    internal void ScrollWheel(int delta, bool across = false)
    {
        var useHorizontal = !_fitWidth && (across || _horizontal.Visible && !_vertical.Visible);

        var notches = 0;

        if (useHorizontal)
        {
            horizontalWheel += delta;

            notches = horizontalWheel / 120;

            horizontalWheel %= 120;
        }
        else
        {
            verticalWheel += delta;

            notches = verticalWheel / 120;

            verticalWheel %= 120;
        }

        var page = useHorizontal ? pageSize.Width : pageSize.Height;

        var lines = SystemInformation.MouseWheelScrollLines;

        var step = lines < 0 ? page : lines * Math.Max(1, (int)(18 * DeviceDpi / 96f));

        SetOffset(useHorizontal ? offset.X - notches * step : offset.X,
            useHorizontal ? offset.Y : offset.Y - notches * step);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollWheel(e.Delta, ModifierKeys.HasFlag(Keys.Shift));

        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }

        base.OnMouseWheel(e);
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (focusedControl is TextBoxBase { ContainsFocus: true } or UpDownBase { ContainsFocus: true })
            return base.ProcessCmdKey(ref message, keyData);

        var line = Math.Max(1, (int)(18 * DeviceDpi / 96f));

        var next = keyData switch
        {
            Keys.PageUp => new Point(offset.X, offset.Y - _viewport.ClientSize.Height),
            Keys.PageDown => new Point(offset.X, offset.Y + _viewport.ClientSize.Height),
            Keys.Home => Point.Empty,
            Keys.End => new Point(offset.X, _content.Height),
            Keys.Up when Focused => new Point(offset.X, offset.Y - line),
            Keys.Down when Focused => new Point(offset.X, offset.Y + line),
            Keys.Left when Focused => new Point(offset.X - line, offset.Y),
            Keys.Right when Focused => new Point(offset.X + line, offset.Y),
            _ => (Point?)null
        };

        if (next == null)
            return base.ProcessCmdKey(ref message, keyData);

        SetOffset(next.Value.X, next.Value.Y);

        return true;
    }

    private void Watch(Control control, bool subscribe)
    {
        if (subscribe)
        {
            control.GotFocus += ChildFocused;

            control.ControlAdded += ChildAdded;

            control.ControlRemoved += ChildRemoved;

            control.MouseDown += ChildMouseDown;
        }
        else
        {
            control.GotFocus -= ChildFocused;

            control.ControlAdded -= ChildAdded;

            control.ControlRemoved -= ChildRemoved;

            control.MouseDown -= ChildMouseDown;
        }

        foreach (Control child in control.Controls)
        {
            Watch(child, subscribe);
        }
    }

    private void ChildAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control != null)
        {
            Watch(e.Control, true);
        }
    }

    private void ChildRemoved(object? sender, ControlEventArgs e)
    {
        if (e.Control != null)
        {
            Watch(e.Control, false);
        }
    }

    private void ChildMouseDown(object? sender, MouseEventArgs e)
    {
        if (!_fitWidth)
        {
            Focus();
        }
    }

    private void ChildFocused(object? sender, EventArgs e)
    {
        if (sender is not Control child || child.IsDisposed)
            return;

        focusedControl = child;

        var origin = _content.PointToClient(child.PointToScreen(Point.Empty));

        var y = origin.Y < offset.Y ? origin.Y
            : origin.Y + child.Height > offset.Y + _viewport.Height
                ? origin.Y + child.Height - _viewport.Height : offset.Y;

        SetOffset(offset.X, y);
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);

        if (_viewport != null)
        {
            _viewport.BackColor = _vertical.BackColor = _horizontal.BackColor = BackColor;
        }
    }
}
