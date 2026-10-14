// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Drawing2D;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy.Models;
using TrackMirror.Storage;
using TrackMirror.UI.Imaging;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class CaptureGallery : Control
{
    private readonly ThumbnailLoader _loader;
    private readonly CaptureScrollBar _scrollBar = new();
    private readonly Dictionary<string, Bitmap?> _thumbnails = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _cacheOrder = new();
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private readonly Font _titleFont = new(UiText.FontFamily, 9.5f, FontStyle.Bold);
    private readonly Font _badgeFont = new(UiText.FontFamily, 8.5f);
    private readonly ToolTip _tooltips = new();
    private List<CaptureEntry> entries = [];
    private Dictionary<string, PrivacyScanRecord> privacy = new(StringComparer.OrdinalIgnoreCase);

    internal void SetPrivacyResults(Dictionary<string, PrivacyScanRecord> results)
    {
        privacy = results;

        Invalidate();
    }
    private int columns = 1;
    private int tileWidth = 240;
    private int rowHeight = 220;
    private int gap = 16;
    private int scrollOffset;
    private int contentHeight;
    private int focusedIndex = -1;
    private int hoveredIndex = -1;
    private int generation;
    private int wheelRemainder;
    private string emptyTitle = UiText.NoCaptures;
    private string emptyHint = UiText.NewCapturesPlaceholder;
    private volatile bool disposed;

    internal event Action<string>? EntryOpened;

    internal event Action? SelectionChanged;

    internal List<CaptureEntry> SelectedEntries => entries.Where(entry => _selected.Contains(entry.Id)).ToList();

    internal bool IsSelected(int index) => index >= 0 && index < entries.Count && _selected.Contains(entries[index].Id);

    internal void SetSelected(int index, bool value)
    {
        if (index < 0 || index >= entries.Count)
            return;

        var changed = value ? _selected.Add(entries[index].Id) : _selected.Remove(entries[index].Id);

        if (changed)
        {
            SelectionChanged?.Invoke();

            AccessibilityNotifyClients(AccessibleEvents.Selection, index + 1);

            Invalidate();
        }
    }

    internal void SelectAll(bool value)
    {
        _selected.Clear();

        if (value)
        {
            _selected.UnionWith(entries.Select(entry => entry.Id));
        }

        SelectionChanged?.Invoke();

        AccessibilityNotifyClients(AccessibleEvents.Selection, -1);

        Invalidate();
    }

    internal IReadOnlyList<CaptureEntry> Entries => entries;

    internal int FocusedIndex => focusedIndex;

    internal CaptureGallery(ScreenshotStore store)
    {
        _loader = new ThumbnailLoader(store);

        _loader.Loaded += OnThumbnailLoaded;

        Dock = DockStyle.Fill;

        Margin = Padding.Empty;

        BackColor = UiTheme.Surface;

        TabStop = true;

        AccessibleName = UiText.Gallery;

        AccessibleDescription = GalleryText.SelectHint;

        AccessibleRole = AccessibleRole.List;

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        _scrollBar.ScrollRequested += value =>
        {
            Focus();

            SetScrollOffset(value);
        };

        _scrollBar.WheelScrolled += ScrollWheel;

        Controls.Add(_scrollBar);
    }

    internal void SetEntries(List<CaptureEntry> values, bool resetScroll)
    {
        var anchorIndex = entries.Count == 0 ? -1 : Math.Min(entries.Count - 1, scrollOffset / rowHeight * columns);

        var anchorId = anchorIndex >= 0 ? entries[anchorIndex].Id : null;

        var withinRow = scrollOffset % rowHeight;

        var focusedId = focusedIndex >= 0 && focusedIndex < entries.Count ? entries[focusedIndex].Id : null;

        entries = values;

        var previousCount = _selected.Count;

        _selected.IntersectWith(entries.Select(entry => entry.Id));

        var selectionChanged = previousCount != _selected.Count;

        focusedIndex = focusedId == null ? -1 : entries.FindIndex(entry => entry.Id == focusedId);

        hoveredIndex = -1;

        if (resetScroll)
        {
            scrollOffset = 0;
        }
        else if (scrollOffset > 0 && anchorId != null)
        {
            var index = entries.FindIndex(entry => entry.Id == anchorId);

            scrollOffset = index < 0 ? 0 : index / columns * rowHeight + withinRow;
        }

        StartGeneration();

        RecalculateLayout();

        AccessibilityNotifyClients(AccessibleEvents.Reorder, -1);

        Invalidate();

        if (selectionChanged)
        {
            SelectionChanged?.Invoke();
        }
    }

    internal void SetEmptyState(string title, string hint)
    {
        emptyTitle = title;

        emptyHint = hint;

        Invalidate();
    }

    private void StartGeneration()
    {
        var failed = _thumbnails.Where(pair => pair.Value == null).Select(pair => pair.Key).ToArray();

        foreach (var id in failed)
        {
            _thumbnails.Remove(id);
        }

        if (failed.Length > 0)
        {
            var retained = _cacheOrder.Where(_thumbnails.ContainsKey).ToArray();

            _cacheOrder.Clear();

            foreach (var id in retained)
            {
                _cacheOrder.Enqueue(id);
            }
        }

        generation++;

        _pending.Clear();

        _loader.SetGeneration(generation);
    }

    private void RecalculateLayout()
    {
        var scale = DeviceDpi / 96f;

        gap = Math.Max(1, (int)(16 * scale));

        var width = Math.Max(1, ClientSize.Width - (int)(18 * scale));

        columns = Math.Max(1, (width + gap) / ((int)(240 * scale) + gap));

        tileWidth = Math.Max(1, (width - (columns - 1) * gap) / columns);

        rowHeight = Math.Max(1, (int)((tileWidth - 24 * scale) * 9 / 16 + 78 * scale) + gap);

        contentHeight = Math.Max(0, (entries.Count + columns - 1) / columns * rowHeight - gap);

        scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, contentHeight - ClientSize.Height));

        _scrollBar.SetViewport(contentHeight, ClientSize.Height, scrollOffset);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (_loader != null)
        {
            StartGeneration();

            RecalculateLayout();
        }
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);

        StartGeneration();

        RecalculateLayout();

        Invalidate();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (Visible && _loader != null && !disposed)
        {
            StartGeneration();

            Invalidate();
        }
    }

    internal Rectangle TileBounds(int index) => new(
        index % columns * (tileWidth + gap),
        index / columns * rowHeight - scrollOffset,
        tileWidth,
        rowHeight - gap
    );

    internal Rectangle SelectionBounds(int index)
    {
        var tile = TileBounds(index);

        var inset = (int)(18 * DeviceDpi / 96f);

        var size = (int)(26 * DeviceDpi / 96f);

        return new Rectangle(tile.X + inset, tile.Y + inset, size, size);
    }

    internal int IndexAt(Point point)
    {
        if (!ClientRectangle.Contains(point))
            return -1;

        var column = point.X / (tileWidth + gap);

        var row = (point.Y + scrollOffset) / rowHeight;

        var index = row * columns + column;

        return column < columns && index < entries.Count && TileBounds(index).Contains(point) ? index : -1;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);

        if (entries.Count == 0)
        {
            var title = new Rectangle(12, Math.Max(0, Height / 2 - 40), Math.Max(1, Width - 24), 40);

            TextRenderer.DrawText(e.Graphics, emptyTitle, _titleFont, title, UiTheme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            var hint = new Rectangle(12, title.Bottom, Math.Max(1, Width - 24), 40);

            TextRenderer.DrawText(e.Graphics, emptyHint, Font, hint, UiTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var start = scrollOffset / rowHeight * columns;

        var end = Math.Min(entries.Count, ((scrollOffset + Height) / rowHeight + 1) * columns);

        for (var i = start; i < end; i++)
        {
            DrawTile(e.Graphics, i);
        }
    }

    private void DrawTile(Graphics graphics, int index)
    {
        var bounds = TileBounds(index);

        if (bounds.Width < 24 || bounds.Height < 24)
            return;

        var entry = entries[index];

        var scale = DeviceDpi / 96f;

        var inset = (int)(12 * scale);

        using var shape = UiTheme.RoundedRectangle(Rectangle.Inflate(bounds, -1, -1), (int)(10 * scale));

        using var fill = new SolidBrush(IsSelected(index) ? UiTheme.Selected
            : index == hoveredIndex ? UiTheme.Raised : UiTheme.Background);

        using var edge = new Pen(IsSelected(index) || index == focusedIndex && Focused
            ? UiTheme.Accent : UiTheme.Border, IsSelected(index) ? 2 * scale : 1);

        graphics.FillPath(fill, shape);

        graphics.DrawPath(edge, shape);

        var imageBounds = new Rectangle(bounds.X + inset, bounds.Y + inset,
            Math.Max(1, bounds.Width - inset * 2), Math.Max(1, (int)((bounds.Width - inset * 2) * 9d / 16)));

        using var imageFill = new SolidBrush(UiTheme.Viewer);

        graphics.FillRectangle(imageFill, imageBounds);

        if (_thumbnails.TryGetValue(entry.Id, out var image) && image != null)
        {
            var ratio = Math.Min(imageBounds.Width / (double)image.Width, imageBounds.Height / (double)image.Height);

            var size = new Size(Math.Max(1, (int)(image.Width * ratio)), Math.Max(1, (int)(image.Height * ratio)));

            graphics.DrawImage(image, new Rectangle(imageBounds.X + (imageBounds.Width - size.Width) / 2,
                imageBounds.Y + (imageBounds.Height - size.Height) / 2, size.Width, size.Height));
        }
        else
        {
            var loaded = _thumbnails.ContainsKey(entry.Id);

            TextRenderer.DrawText(graphics, loaded ? Messages.ImageUnavailable : UiText.LoadingThumbnail, Font,
                imageBounds, UiTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (!loaded && _pending.Add(entry.Id))
            {
                _loader.Request(entry, generation);
            }
        }

        var title = new Rectangle(imageBounds.X, imageBounds.Bottom + (int)(8 * scale),
            imageBounds.Width, (int)(24 * scale));

        TextRenderer.DrawText(graphics, entry.CapturedUtc.ToLocalTime().ToString(DateFormats.HistoryRow),
            _titleFont, title, UiTheme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var badge = new Rectangle(title.X, title.Bottom + (int)(3 * scale),
            Math.Min(imageBounds.Width, (int)(115 * scale)), (int)(22 * scale));

        using var badgeShape = UiTheme.RoundedRectangle(badge, (int)(5 * scale));

        using var badgeFill = new SolidBrush(UiTheme.Raised);

        graphics.FillPath(badgeFill, badgeShape);

        var text = (entry.Flagged ? UiText.FlagPrefix : string.Empty) + (entry.Viewed ? UiText.Reviewed : UiText.New);

        TextRenderer.DrawText(graphics, text, _badgeFont, badge,
            entry.Flagged ? UiTheme.Amber : entry.Viewed ? UiTheme.Muted : UiTheme.AccentText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var aiBadge = new Rectangle(badge.Right + (int)(6 * scale), badge.Top,
            Math.Max(1, imageBounds.Right - badge.Right - (int)(6 * scale)), badge.Height);

        var result = privacy.TryGetValue(entry.Id, out var found) ? found : null;

        var aiColor = result is { Findings.Count: > 0, Disposition: PrivacyDisposition.Pending }
            ? UiTheme.Amber : UiTheme.Muted;

        var caption = result is { NeedsCheck: true, State: not (PrivacyScanState.Queued or PrivacyScanState.Scanning) }
            ? PrivacyText.StaleBadge : PrivacyText.State(result);

        TextRenderer.DrawText(graphics, caption, _badgeFont, aiBadge, aiColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (index == hoveredIndex || index == focusedIndex && Focused || _selected.Count > 0)
        {
            var checkbox = SelectionBounds(index);

            using var checkboxShape = UiTheme.RoundedRectangle(checkbox, (int)(5 * scale));

            using var checkboxFill = new SolidBrush(IsSelected(index) ? UiTheme.Accent : UiTheme.Raised);

            using var checkboxEdge = new Pen(UiTheme.AccentText, scale);

            graphics.FillPath(checkboxFill, checkboxShape);

            graphics.DrawPath(checkboxEdge, checkboxShape);

            if (IsSelected(index))
            {
                using var check = new Pen(UiTheme.Text, 2 * scale);

                graphics.DrawLines(check, [new PointF(checkbox.X + 6 * scale, checkbox.Y + 13 * scale),
                    new PointF(checkbox.X + 11 * scale, checkbox.Y + 18 * scale),
                    new PointF(checkbox.X + 20 * scale, checkbox.Y + 8 * scale)]);
            }
        }
    }

    private void OnThumbnailLoaded(CaptureEntry entry, int value, Bitmap? image)
    {
        if (disposed || !IsHandleCreated)
        {
            image?.Dispose();

            return;
        }

        try
        {
            BeginInvoke(new Action(() =>
            {
                if (disposed || value != generation || _thumbnails.ContainsKey(entry.Id))
                {
                    image?.Dispose();

                    return;
                }

                _pending.Remove(entry.Id);

                _thumbnails[entry.Id] = image;

                _cacheOrder.Enqueue(entry.Id);

                var capacity = Math.Max(96, columns * (Height / rowHeight + 3));

                while (_thumbnails.Count > capacity)
                {
                    var oldest = _cacheOrder.Dequeue();

                    if (_thumbnails.Remove(oldest, out var removed))
                    {
                        removed?.Dispose();
                    }
                }

                Invalidate();
            }));
        }
        catch (InvalidOperationException)
        {
            image?.Dispose();
        }
    }

    private void SetScrollOffset(int value)
    {
        var offset = Math.Clamp(value, 0, Math.Max(0, contentHeight - ClientSize.Height));

        if (offset == scrollOffset)
            return;

        scrollOffset = offset;

        StartGeneration();

        _scrollBar.SetViewport(contentHeight, ClientSize.Height, scrollOffset);

        Invalidate();
    }

    private void ScrollWheel(int delta)
    {
        wheelRemainder += delta;

        var notches = wheelRemainder / 120;

        wheelRemainder %= 120;

        var lines = SystemInformation.MouseWheelScrollLines;

        var step = lines < 0 ? Height : (int)(40 * DeviceDpi / 96f) * lines;

        SetScrollOffset(scrollOffset - notches * step);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollWheel(e.Delta);

        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }

        base.OnMouseWheel(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var index = IndexAt(e.Location);

        if (index == hoveredIndex)
            return;

        hoveredIndex = index;

        Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;

        _tooltips.SetToolTip(this, index < 0 ? string.Empty
            : SelectionBounds(index).Contains(e.Location) ? GalleryText.SelectCapture
            : entries[index].Account + Environment.NewLine + entries[index].FileName);

        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        hoveredIndex = -1;

        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        if (e.Button == MouseButtons.Left)
        {
            Focus();

            var index = IndexAt(e.Location);

            if (index >= 0 && SelectionBounds(index).Contains(e.Location))
            {
                focusedIndex = index;

                SetSelected(index, !IsSelected(index));
            }
            else
            {
                OpenIndex(index);
            }
        }
    }

    internal void OpenIndex(int index)
    {
        if (index < 0 || index >= entries.Count)
            return;

        focusedIndex = index;

        EntryOpened?.Invoke(entries[index].Id);
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down
            or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (entries.Count == 0)
            return;

        if (e.Control && e.KeyCode == Keys.A || e.KeyCode == Keys.Escape)
        {
            SelectAll(e.KeyCode != Keys.Escape);

            e.SuppressKeyPress = true;

            return;
        }

        var index = Math.Max(0, focusedIndex);

        switch (e.KeyCode)
        {
            case Keys.Left:
                index--;

                break;
            case Keys.Right:
                index = focusedIndex < 0 ? 0 : index + 1;

                break;
            case Keys.Up:
                index -= columns;

                break;
            case Keys.Down:
                index = focusedIndex < 0 ? 0 : index + columns;

                break;
            case Keys.Home:
                index = 0;

                break;
            case Keys.End:
                index = entries.Count - 1;

                break;
            case Keys.PageUp:
                index -= Math.Max(1, Height / rowHeight) * columns;

                break;
            case Keys.PageDown:
                index += Math.Max(1, Height / rowHeight) * columns;

                break;
            case Keys.Enter:
                OpenIndex(index);

                e.SuppressKeyPress = true;

                return;
            case Keys.Space:
                focusedIndex = index;

                SetSelected(index, !IsSelected(index));

                e.SuppressKeyPress = true;

                return;
            default:
                return;
        }

        focusedIndex = Math.Clamp(index, 0, entries.Count - 1);

        var bounds = TileBounds(focusedIndex);

        if (bounds.Top < 0)
        {
            SetScrollOffset(scrollOffset + bounds.Top);
        }
        else if (bounds.Bottom > Height)
        {
            SetScrollOffset(scrollOffset + bounds.Bottom - Height);
        }

        AccessibilityNotifyClients(AccessibleEvents.Focus, focusedIndex + 1);

        Invalidate();

        e.SuppressKeyPress = true;
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new CaptureGalleryAccessibleObject(this);

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;

            _loader.Dispose();

            foreach (var image in _thumbnails.Values)
            {
                image?.Dispose();
            }

            _thumbnails.Clear();

            _titleFont.Dispose();

            _badgeFont.Dispose();

            _tooltips.Dispose();
        }

        base.Dispose(disposing);
    }
}
