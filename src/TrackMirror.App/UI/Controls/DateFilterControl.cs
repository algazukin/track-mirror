// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class DateFilterControl : UserControl
{
    private readonly ActionButton _choose = new(CalendarText.AllDates + CalendarText.ExpandSuffix)
    {
        Dock = DockStyle.Fill,
        Margin = Padding.Empty,
        AccessibleName = CalendarText.DateFilter
    };
    private readonly ActionButton _reset = new(CalendarText.Reset)
    {
        Dock = DockStyle.Fill,
        Margin = new Padding(6, 0, 0, 0),
        AccessibleName = CalendarText.ResetDate,
        Visible = false
    };
    private readonly CaptureCalendar _calendar = new();
    private readonly AnchoredDropDown _popup;
    private readonly ToolStripControlHost _host;
    private readonly ColumnStyle _resetColumn = new(SizeType.Absolute, 0);
    private HashSet<DateTime> dates = [];
    private readonly bool _singleDay;

    internal DateRange? SelectedRange { get; private set; }

    internal event Action? RangeChanged;

    internal DateFilterControl(bool singleDay = false)
    {
        _singleDay = singleDay;

        Dock = DockStyle.Fill;

        Margin = new Padding(0, 0, 0, 12);

        BackColor = UiTheme.Surface;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.ColumnStyles.Add(_resetColumn);

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(_choose, 0, 0);

        layout.Controls.Add(_reset, 1, 0);

        Controls.Add(layout);

        _host = new ToolStripControlHost(_calendar)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _popup = new AnchoredDropDown();

        _popup.Items.Add(_host);

        _choose.Click += (_, _) => ShowCalendar();

        _reset.Click += (_, _) => SetRange(null);

        _calendar.RangeSelected += range =>
        {
            CloseCalendar();

            SetRange(range);
        };

        if (singleDay)
        {
            _choose.AccessibleName = ActivityText.Day;

            SetRange(new DateRange(DateTime.Today, DateTime.Today));
        }
    }

    internal void SetAvailableDates(IEnumerable<DateTime> values)
    {
        dates = values.Select(value => value.Date).ToHashSet();

        if (_popup.Visible)
        {
            _calendar.UpdateAvailableDates(dates);
        }
    }

    internal void SetRange(DateRange? range)
    {
        if (_singleDay)
        {
            var date = range?.Start.Date ?? DateTime.Today;

            range = new DateRange(date, date);
        }

        DateRange? value = null;

        if (range != null)
        {
            var start = range.Start.Date;

            var end = range.End.Date;

            value = start <= end ? new DateRange(start, end) : new DateRange(end, start);
        }

        if (SelectedRange == value)
            return;

        SelectedRange = value;

        UpdateCaption();

        RangeChanged?.Invoke();
    }

    private void UpdateCaption()
    {
        var text = CalendarText.AllDates;

        if (SelectedRange != null)
        {
            text = SelectedRange.Start == SelectedRange.End
                ? SelectedRange.Start.ToString(DateFormats.CalendarDate)
                : string.Format(CalendarText.RangeFormat, SelectedRange.Start, SelectedRange.End);
        }

        _choose.Text = text + CalendarText.ExpandSuffix;

        _choose.ForeColor = SelectedRange != null ? UiTheme.AccentText : UiTheme.Text;

        var showReset = !_singleDay && SelectedRange != null;

        _reset.Visible = showReset;

        _resetColumn.Width = showReset ? (int)(44 * DeviceDpi / 96f) : 0;
    }

    private void ShowCalendar()
    {
        if (_popup.Visible)
        {
            CloseCalendar();

            return;
        }

        _calendar.Prepare(dates, SelectedRange, DeviceDpi, _singleDay);

        _host.Size = _calendar.Size;

        _popup.ShowAnchored(_choose, new Point(0, _choose.Height + (int)(6 * DeviceDpi / 96f)));
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);

        UpdateCaption();
    }

    internal void CloseCalendar()
    {
        if (_popup.Visible)
        {
            _popup.Close();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _popup.Dispose();
        }

        base.Dispose(disposing);
    }
}
