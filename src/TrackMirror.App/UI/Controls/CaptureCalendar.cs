// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Globalization;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class CaptureCalendar : UserControl
{
    private readonly Label _heading = UiTheme.Label(string.Empty, 11, true);
    private readonly Label _prompt = UiTheme.Label(CalendarText.SelectStart, 9, muted: true);
    private readonly ActionButton _previous = new(CalendarText.Previous);
    private readonly ActionButton _next = new(CalendarText.Next);
    private readonly TableLayoutPanel _days = new()
    {
        Dock = DockStyle.Fill,
        ColumnCount = 7,
        RowCount = 6,
        Margin = Padding.Empty
    };
    private readonly Font _normalFont = new(UiText.FontFamily, 9.5f);
    private readonly Font _captureFont = new(UiText.FontFamily, 9.5f, FontStyle.Bold);
    private readonly ActionButton _today = new(CalendarText.Today)
    {
        Dock = DockStyle.Fill
    };
    private readonly TableLayoutPanel _layout;
    private HashSet<DateTime> captureDates = [];
    private DateTime month;
    private DateRange? selectedRange;
    private DateTime? anchor;
    private int inset = 3;
    private bool singleDaySelection;

    internal event Action<DateRange>? RangeSelected;

    internal CaptureCalendar()
    {
        AutoScaleMode = AutoScaleMode.None;

        BackColor = UiTheme.Surface;

        ForeColor = UiTheme.Text;

        AccessibleName = CalendarText.DateFilter;

        _previous.AccessibleName = CalendarText.PreviousMonth;

        _next.AccessibleName = CalendarText.NextMonth;

        _heading.TextAlign = ContentAlignment.MiddleCenter;

        _prompt.TextAlign = ContentAlignment.MiddleCenter;

        for (var i = 0; i < 7; i++)
        {
            _days.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
        }

        for (var i = 0; i < 6; i++)
        {
            _days.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6));
        }

        _previous.Click += (_, _) => ChangeMonth(-1);

        _next.Click += (_, _) => ChangeMonth(1);

        _today.Click += (_, _) => RangeSelected?.Invoke(new DateRange(DateTime.Today, DateTime.Today));

        _layout = CreateLayout();

        Controls.Add(_layout);
    }

    internal void Prepare(HashSet<DateTime> dates, DateRange? selection, int dpi, bool singleDay = false)
    {
        captureDates = dates;

        selectedRange = selection;

        anchor = null;

        singleDaySelection = singleDay;

        _prompt.Text = singleDay ? ActivityText.SelectDay : CalendarText.SelectStart;

        var initial = selection?.Start ?? (dates.Count > 0 ? dates.Max() : DateTime.Today);

        month = new DateTime(initial.Year, initial.Month, 1);

        var scale = dpi / 96f;

        inset = Math.Max(1, (int)(3 * scale));

        Size = new Size((int)(304 * scale), (int)(368 * scale));

        _layout.Padding = new Padding((int)(12 * scale));

        _today.Margin = new Padding(0, (int)(8 * scale), 0, 0);

        RenderMonth();
    }

    private TableLayoutPanel CreateLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty
        };

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 13));

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 8));

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 56));

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 10));

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 13));

        var navigation = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty
        };

        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));

        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76));

        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));

        navigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _previous.Dock = _next.Dock = DockStyle.Fill;

        _previous.Margin = _next.Margin = Padding.Empty;

        navigation.Controls.Add(_previous, 0, 0);

        navigation.Controls.Add(_heading, 1, 0);

        navigation.Controls.Add(_next, 2, 0);

        var weekdays = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1,
            Margin = Padding.Empty
        };

        var format = CultureInfo.CurrentCulture.DateTimeFormat;

        for (var i = 0; i < 7; i++)
        {
            weekdays.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));

            var day = ((int)format.FirstDayOfWeek + i) % 7;

            var label = UiTheme.Label(format.AbbreviatedDayNames[day], 8.5f, muted: true);

            label.TextAlign = ContentAlignment.MiddleCenter;

            weekdays.Controls.Add(label, i, 0);
        }

        weekdays.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(navigation, 0, 0);

        root.Controls.Add(weekdays, 0, 1);

        root.Controls.Add(_days, 0, 2);

        root.Controls.Add(_prompt, 0, 3);

        root.Controls.Add(_today, 0, 4);

        return root;
    }

    internal void UpdateAvailableDates(HashSet<DateTime> dates)
    {
        captureDates = dates;

        foreach (Button button in _days.Controls)
        {
            if (button.Tag is DateTime date)
            {
                button.Font = dates.Contains(date) ? _captureFont : _normalFont;
            }
        }
    }

    private void ChangeMonth(int offset)
    {
        month = month.AddMonths(offset);

        RenderMonth();
    }

    private void RenderMonth()
    {
        _heading.Text = month.ToString(DateFormats.CalendarMonth);

        _previous.Enabled = month.Year > 1 || month.Month > 1;

        _next.Enabled = month.Year < 9999 || month.Month < 12;

        _days.SuspendLayout();

        foreach (Control control in _days.Controls.Cast<Control>().ToArray())
        {
            control.Dispose();
        }

        var firstDay = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

        var offset = ((int)month.DayOfWeek - firstDay + 7) % 7;

        var firstNumber = DateOnly.FromDateTime(month).DayNumber - offset;

        for (var i = 0; i < 42; i++)
        {
            var dayNumber = firstNumber + i;

            if (dayNumber < DateOnly.MinValue.DayNumber || dayNumber > DateOnly.MaxValue.DayNumber)
                continue;

            var date = DateOnly.FromDayNumber(dayNumber).ToDateTime(TimeOnly.MinValue);

            var hasCaptures = captureDates.Contains(date);

            var selected = selectedRange != null && date >= selectedRange.Start && date <= selectedRange.End;

            var button = new ActionButton(date.Day.ToString(CultureInfo.CurrentCulture), selected)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(inset),
                Font = hasCaptures ? _captureFont : _normalFont,
                ForeColor = selected || date.Month == month.Month ? UiTheme.Text : UiTheme.Muted,
                AccessibleName = date.ToString(DateFormats.AccessibleCalendarDate),
                Tag = date
            };

            button.Click += (_, _) => SelectDate(date);

            _days.Controls.Add(button, i % 7, i / 7);
        }

        _days.ResumeLayout(true);
    }

    private void SelectDate(DateTime date)
    {
        if (singleDaySelection)
        {
            RangeSelected?.Invoke(new DateRange(date, date));

            return;
        }

        if (anchor.HasValue)
        {
            var range = date >= anchor.Value
                ? new DateRange(anchor.Value, date)
                : new DateRange(date, anchor.Value);

            RangeSelected?.Invoke(range);

            return;
        }

        anchor = date;

        selectedRange = new DateRange(date, date);

        _prompt.Text = CalendarText.SelectEnd;

        foreach (ActionButton button in _days.Controls)
        {
            if (button.Tag is DateTime value)
            {
                button.IsPrimary = value == date;

                button.ForeColor = button.IsPrimary || value.Month == month.Month ? UiTheme.Text : UiTheme.Muted;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _normalFont.Dispose();

            _captureFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
