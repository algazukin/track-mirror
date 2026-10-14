// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Drawing2D;
using TrackMirror.Constants;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class OptionSelector : Control
{
    private readonly Panel _options = new() { BackColor = UiTheme.Surface };
    private readonly CaptureScrollBar _scrollBar = new() { AccessibleName = ActivityText.AccountScroll };
    private readonly ToolTip _tooltips = new();
    private readonly string _emptyText;
    private readonly ToolStripControlHost _host;
    private readonly AnchoredDropDown _popup;
    private List<string> choices = [];
    private int firstIndex;
    private int focusedIndex;
    private int pageCount = 7;
    private int wheelRemainder;
    private bool hovered;

    internal string? SelectedValue { get; private set; }

    internal event Action? SelectionChanged;

    internal OptionSelector(string? caption = null, string? emptyText = null, string? scrollCaption = null)
    {
        _emptyText = emptyText ?? ActivityText.NoValue;

        _scrollBar.AccessibleName = scrollCaption ?? ActivityText.AccountScroll;

        Size = new Size(280, 38);

        BackColor = UiTheme.Raised;

        ForeColor = UiTheme.Text;

        Cursor = Cursors.Hand;

        TabStop = true;

        AccessibleName = caption ?? ActivityText.Account;

        AccessibleRole = AccessibleRole.ComboBox;

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        _options.Controls.Add(_scrollBar);

        _options.MouseWheel += HandleWheel;

        _scrollBar.ScrollRequested += SetFirstIndex;

        _scrollBar.WheelScrolled += ScrollWheel;

        _host = new ToolStripControlHost(_options)
        {
            AutoSize = false,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        _popup = new AnchoredDropDown();

        _popup.Items.Add(_host);

        _popup.Closed += (_, _) => Invalidate();

        Enabled = false;
    }

    internal void SetChoices(IEnumerable<string> values)
    {
        var next = values.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();

        if (choices.SequenceEqual(next, StringComparer.OrdinalIgnoreCase))
            return;

        var focused = focusedIndex < choices.Count ? choices[focusedIndex] : SelectedValue;

        var previous = SelectedValue;

        choices = next;

        SelectedValue = choices.FirstOrDefault(value => string.Equals(value, previous,
            StringComparison.OrdinalIgnoreCase)) ?? choices.FirstOrDefault();

        focusedIndex = Math.Max(0, choices.FindIndex(value => value == focused));

        Enabled = choices.Count > 0;

        AccessibleDescription = SelectedValue;

        _tooltips.SetToolTip(this, SelectedValue);

        if (choices.Count == 0)
        {
            ClosePopup();
        }
        else if (_popup.Visible)
        {
            ConfigurePopup();

            MoveFocus(focusedIndex);
        }

        Invalidate();

        if (!string.Equals(previous, SelectedValue, StringComparison.OrdinalIgnoreCase))
        {
            SelectionChanged?.Invoke();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2)
            return;

        var scale = DeviceDpi / 96f;

        var color = _popup.Visible ? UiTheme.Selected : hovered && Enabled ? UiTheme.Border : UiTheme.Raised;

        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Background);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var shape = UiTheme.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), (int)(8 * scale));

        using var fill = new SolidBrush(color);

        using var edge = new Pen(UiTheme.Border);

        e.Graphics.FillPath(fill, shape);

        e.Graphics.DrawPath(edge, shape);

        var textBounds = new Rectangle((int)(12 * scale), 0,
            Math.Max(1, Width - (int)(48 * scale)), Height);

        TextRenderer.DrawText(e.Graphics, SelectedValue ?? _emptyText, Font, textBounds,
            Enabled ? ForeColor : UiTheme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        using var arrow = new Pen(Enabled ? UiTheme.AccentText : UiTheme.Muted, 1.5f * scale);

        var x = Width - 21 * scale;

        var y = Height / 2f;

        e.Graphics.DrawLines(arrow, [new PointF(x - 4 * scale, y - 2 * scale),
            new PointF(x, y + 2 * scale), new PointF(x + 4 * scale, y - 2 * scale)]);

        if (Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -6, -6),
                UiTheme.AccentText, color);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);

        Focus();

        if (_popup.Visible)
        {
            ClosePopup();
        }
        else
        {
            ShowPopup();
        }
    }

    private void ConfigurePopup()
    {
        var scale = DeviceDpi / 96f;

        var available = Screen.FromControl(this).WorkingArea;

        pageCount = Math.Min(choices.Count, Math.Min(7, Math.Max(1, (available.Height - 24) / (int)(44 * scale))));

        var textWidth = choices.Max(value => TextRenderer.MeasureText(value, Font).Width);

        var width = Math.Min(available.Width - (int)(24 * scale),
            Math.Max(Width, Math.Max((int)(310 * scale), textWidth + (int)(50 * scale))));

        _options.Size = new Size(width, (int)((pageCount * 44 + 12) * scale));

        _host.Size = _options.Size;

        _scrollBar.Width = (int)(18 * scale);

        firstIndex = Math.Clamp(firstIndex, 0, Math.Max(0, choices.Count - pageCount));

        RenderOptions();
    }

    private void RenderOptions()
    {
        var scale = DeviceDpi / 96f;

        foreach (var button in _options.Controls.OfType<Button>().ToArray())
        {
            button.Dispose();
        }

        for (var row = 0; row < pageCount; row++)
        {
            var index = firstIndex + row;

            var value = choices[index];

            var button = new ActionButton(value, value == SelectedValue)
            {
                Location = new Point((int)(6 * scale), (int)((6 + row * 44) * scale)),
                Size = new Size(_options.Width - (int)((choices.Count > pageCount ? 30 : 12) * scale),
                    (int)(40 * scale)),
                Font = Font,
                Tag = value,
                AccessibleName = value,
                Margin = Padding.Empty
            };

            button.Click += (_, _) => SelectChoice(index);

            button.MouseWheel += HandleWheel;

            button.PreviewKeyDown += (_, e) =>
            {
                if (e.KeyCode is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown
                    or Keys.Enter or Keys.Space or Keys.Escape)
                {
                    e.IsInputKey = true;
                }
            };

            button.KeyDown += (_, e) =>
            {
                var target = e.KeyCode switch
                {
                    Keys.Up => index - 1,
                    Keys.Down => index + 1,
                    Keys.Home => 0,
                    Keys.End => choices.Count - 1,
                    Keys.PageUp => index - pageCount,
                    Keys.PageDown => index + pageCount,
                    _ => -1
                };

                if (e.KeyCode is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown)
                {
                    MoveFocus(target);

                    e.Handled = e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    ClosePopup();

                    Focus();

                    e.Handled = e.SuppressKeyPress = true;
                }
                else if (e.KeyCode is Keys.Enter or Keys.Space)
                {
                    SelectChoice(index);

                    e.Handled = e.SuppressKeyPress = true;
                }
            };

            _options.Controls.Add(button);
        }

        _scrollBar.SetViewport(choices.Count, pageCount, firstIndex);
    }

    private void ShowPopup()
    {
        if (!Enabled || !Visible || choices.Count == 0)
            return;

        focusedIndex = Math.Max(0, choices.IndexOf(SelectedValue!));

        firstIndex = focusedIndex;

        wheelRemainder = 0;

        ConfigurePopup();

        _popup.ShowAnchored(this, new Point(0, Height + (int)(6 * DeviceDpi / 96f)));

        MoveFocus(focusedIndex);

        Invalidate();
    }

    private void MoveFocus(int index)
    {
        focusedIndex = Math.Clamp(index, 0, choices.Count - 1);

        if (focusedIndex < firstIndex || focusedIndex >= firstIndex + pageCount)
        {
            firstIndex = Math.Clamp(focusedIndex < firstIndex ? focusedIndex : focusedIndex - pageCount + 1,
                0, Math.Max(0, choices.Count - pageCount));

            RenderOptions();
        }

        var value = choices[focusedIndex];

        _options.Controls.OfType<Button>().FirstOrDefault(button => (string?)button.Tag == value)?.Focus();
    }

    private void SetFirstIndex(int index)
    {
        var value = Math.Clamp(index, 0, Math.Max(0, choices.Count - pageCount));

        if (value == firstIndex)
            return;

        firstIndex = value;

        focusedIndex = Math.Clamp(focusedIndex, firstIndex, firstIndex + pageCount - 1);

        RenderOptions();

        MoveFocus(focusedIndex);
    }

    private void HandleWheel(object? sender, MouseEventArgs e)
    {
        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }

        ScrollWheel(e.Delta);
    }

    private void ScrollWheel(int delta)
    {
        wheelRemainder += delta;

        var steps = wheelRemainder / 120;

        wheelRemainder %= 120;

        var lines = SystemInformation.MouseWheelScrollLines;

        if (steps == 0 || lines == 0)
            return;

        SetFirstIndex(firstIndex - steps * (lines < 0 ? pageCount : lines));
    }

    internal void SelectValue(string value)
    {
        var index = choices.IndexOf(value);

        if (index >= 0)
        {
            SelectChoice(index);
        }
    }

    private void SelectChoice(int index)
    {
        var changed = SelectedValue != choices[index];

        SelectedValue = choices[index];

        AccessibleDescription = SelectedValue;

        _tooltips.SetToolTip(this, SelectedValue);

        ClosePopup();

        Focus();

        Invalidate();

        if (changed)
        {
            SelectionChanged?.Invoke();
        }
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End
            || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!Enabled)
            return;

        if (e.KeyCode is Keys.Enter or Keys.Space || e.KeyCode == Keys.F4 && !e.Alt || e.Alt && e.KeyCode == Keys.Down)
        {
            ShowPopup();
        }
        else if (e.KeyCode is Keys.Up or Keys.Down or Keys.Home or Keys.End)
        {
            var index = choices.IndexOf(SelectedValue!);

            index = e.KeyCode switch
            {
                Keys.Up => index - 1,
                Keys.Down => index + 1,
                Keys.Home => 0,
                _ => choices.Count - 1
            };

            SelectChoice(Math.Clamp(index, 0, choices.Count - 1));
        }
        else
        {
            return;
        }

        e.Handled = e.SuppressKeyPress = true;
    }

    internal void ClosePopup()
    {
        if (_popup.Visible)
        {
            _popup.Close();
        }
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

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);

        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);

        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);

        ClosePopup();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _popup.Dispose();

            _tooltips.Dispose();
        }

        base.Dispose(disposing);
    }
}
