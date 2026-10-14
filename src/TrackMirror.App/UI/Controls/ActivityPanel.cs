// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class ActivityPanel : UserControl
{
    private readonly DateFilterControl _date = new(true) { Width = 230, Height = 38, Margin = new Padding(0, 0, 8, 0) };
    private readonly ActionButton _previous = new(CalendarText.Previous) { Width = 38, Height = 38 };
    private readonly ActionButton _next = new(CalendarText.Next) { Width = 38, Height = 38 };
    private readonly OptionSelector _account = new();
    private readonly ActivityChart _chart = new();
    private readonly Label _tracked = UiTheme.Label(ActivityText.NoValue, 14, true);
    private readonly Label _overall = UiTheme.Label(ActivityText.NoValue, 22, true);
    private readonly Label _mouse = UiTheme.Label(ActivityText.NoValue, 22, true);
    private readonly Label _keyboard = UiTheme.Label(ActivityText.NoValue, 22, true);
    private readonly Label _received = UiTheme.Label(ActivityText.Waiting, 9, muted: true);
    private readonly Dictionary<string, SortedDictionary<DateTime, HubstaffActivity>> _accounts =
        new(StringComparer.OrdinalIgnoreCase);
    private bool refreshing;

    internal ActivityPanel()
    {
        Dock = DockStyle.Fill;

        BackColor = UiTheme.Background;

        Padding = new Padding(24);

        Controls.Add(CreateLayout());

        _previous.AccessibleName = ActivityText.PreviousDay;

        _next.AccessibleName = ActivityText.NextDay;

        _previous.Click += (_, _) => ChangeDate(-1);

        _next.Click += (_, _) => ChangeDate(1);

        _date.RangeChanged += RefreshDay;

        _account.SelectionChanged += () =>
        {
            if (!refreshing)
            {
                RefreshDay();
            }
        };

        RefreshDay();
    }

    private TableLayoutPanel CreateLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 6,
            ColumnCount = 1,
            Margin = Padding.Empty
        };

        foreach (var height in new[] { 44, 30, 58, 98 })
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        }

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = Padding.Empty
        };

        filters.Controls.AddRange([_previous, _date, _next]);

        var caption = UiTheme.Label(ActivityText.Account, 10, muted: true);

        caption.Dock = DockStyle.None;

        caption.Size = new Size(76, 38);

        caption.Margin = new Padding(24, 0, 8, 0);

        filters.Controls.Add(caption);

        _account.Margin = Padding.Empty;

        filters.Controls.Add(_account);

        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = Padding.Empty
        };

        var labels = new[] { ActivityText.Tracked, ActivityText.Overall, ActivityText.Mouse, ActivityText.Keyboard };

        var values = new[] { _tracked, _overall, _mouse, _keyboard };

        var colors = new[] { UiTheme.Text, UiTheme.AccentText, UiTheme.ActivityMouse, UiTheme.Amber };

        for (var i = 0; i < values.Length; i++)
        {
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            values[i].ForeColor = colors[i];

            var card = new SurfacePanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 10, 12, 10),
                Margin = new Padding(0, 0, i == 3 ? 0 : 12, 12)
            };

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Margin = Padding.Empty
            };

            content.RowStyles.Add(new RowStyle(SizeType.Percent, 35));

            content.RowStyles.Add(new RowStyle(SizeType.Percent, 65));

            content.Controls.Add(UiTheme.Label(labels[i], 9, muted: true), 0, 0);

            content.Controls.Add(values[i], 0, 1);

            card.Controls.Add(content);

            cards.Controls.Add(card, i, 0);
        }

        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var chartCard = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            Margin = Padding.Empty
        };

        chartCard.Controls.Add(_chart);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 8, 0, 0)
        };

        for (var i = 0; i < 3; i++)
        {
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3));
        }

        footer.Controls.Add(_received, 0, 0);

        footer.Controls.Add(UiTheme.Label(ActivityText.Hint, 9, muted: true), 0, 1);

        footer.Controls.Add(UiTheme.Label(ActivityText.ArchiveHint, 9, muted: true), 0, 2);

        root.Controls.Add(UiTheme.Label(ActivityText.Heading, 22, true), 0, 0);

        root.Controls.Add(UiTheme.Label(ActivityText.Description, 10, muted: true), 0, 1);

        root.Controls.Add(filters, 0, 2);

        root.Controls.Add(cards, 0, 3);

        root.Controls.Add(chartCard, 0, 4);

        root.Controls.Add(footer, 0, 5);

        return root;
    }

    internal void AddEntries(IReadOnlyList<HubstaffActivity> entries)
    {
        foreach (var entry in entries)
        {
            if (!_accounts.TryGetValue(entry.Account, out var records))
            {
                records = [];

                _accounts.Add(entry.Account, records);
            }

            records[entry.StartedUtc] = entry;
        }

        refreshing = true;

        _account.SetChoices(_accounts.Keys);

        refreshing = false;

        RefreshDay();
    }

    private void ChangeDate(int offset)
    {
        var current = _date.SelectedRange!.Start;

        _date.SetRange(new DateRange(current.AddDays(offset), current.AddDays(offset)));
    }

    private void RefreshDay()
    {
        var current = _date.SelectedRange!.Start;

        var records = _account.SelectedValue is string selected && _accounts.TryGetValue(selected, out var values)
            ? values.Values.ToList() : [];

        _date.SetAvailableDates(records.Select(entry => entry.StartedUtc.ToLocalTime()));

        _previous.Enabled = current > DateTime.MinValue.Date;

        _next.Enabled = current < DateTime.MaxValue.Date;

        var day = new HubstaffActivityDay(current, records);

        _chart.SetDay(day);

        var duration = TimeSpan.FromSeconds(day.TrackedSeconds);

        _tracked.Text = day.Entries.Count == 0 ? ActivityText.NoValue
            : string.Format(ActivityText.DurationFormat, (int)duration.TotalHours, duration.Minutes, duration.Seconds);

        _overall.Text = day.Entries.Count == 0 ? ActivityText.NoValue
            : string.Format(ActivityText.PercentFormat, day.OverallPercent);

        _mouse.Text = day.Entries.Count == 0 ? ActivityText.NoValue
            : string.Format(ActivityText.PercentFormat, day.MousePercent);

        _keyboard.Text = day.Entries.Count == 0 ? ActivityText.NoValue
            : string.Format(ActivityText.PercentFormat, day.KeyboardPercent);

        _received.Text = day.Entries.Count == 0 ? (records.Count == 0 ? ActivityText.Waiting : ActivityText.Empty)
            : string.Format(ActivityText.RecordsFormat, day.Entries.Count,
                day.Entries.Max(entry => entry.ObservedUtc).ToLocalTime());
    }

    internal void CloseCalendar()
    {
        _date.CloseCalendar();

        _account.ClosePopup();
    }
}
