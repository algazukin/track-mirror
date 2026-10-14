// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.IntegrationTests.Infrastructure.Windows;
using TrackMirror.IntegrationTests.Localization;
using TrackMirror.Models;
using TrackMirror.UI.Controls;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

[Collection(nameof(LocalizationCollection))]
public sealed class CalendarTests
{
    [Fact]
    public void A_partial_range_keeps_the_existing_filter_until_the_second_day_is_chosen()
    {
        // Arrange
        StaThread.Run(() =>
        {
            var day = new DateTime(2026, 10, 13);

            using var calendar = new CaptureCalendar();

            var previous = new DateRange(day, day.AddDays(2));

            DateRange? selected = null;

            calendar.RangeSelected += value => selected = value;

            calendar.Prepare([day], previous, 96, false);

            // Act
            UiControls.Invoke(calendar, "SelectDate", day.AddDays(3));

            // Assert
            Assert.Null(selected);

            // Act
            UiControls.Invoke(calendar, "SelectDate", day.AddDays(1));

            // Assert
            Assert.Equal(new DateRange(day.AddDays(1), day.AddDays(3)), selected);
        });
    }

    [Fact]
    public void Reopening_the_calendar_discards_a_partial_range_and_same_day_selects_one_day()
    {
        // Arrange
        StaThread.Run(() =>
        {
            var day = new DateTime(2026, 10, 13);

            using var calendar = new CaptureCalendar();

            DateRange? selected = null;

            calendar.RangeSelected += value => selected = value;

            calendar.Prepare([day], null, 96, false);

            UiControls.Invoke(calendar, "SelectDate", day.AddDays(5));

            // Act
            calendar.Prepare([day], null, 96, false);

            UiControls.Invoke(calendar, "SelectDate", day);

            UiControls.Invoke(calendar, "SelectDate", day);

            // Assert
            Assert.Equal(new DateRange(day, day), selected);
        });
    }

    [Fact]
    public void Normalized_ranges_raise_one_change_and_reset_restores_all_dates()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var filter = new DateFilterControl();

            var day = new DateTime(2026, 10, 13);

            var changes = 0;

            filter.RangeChanged += () => changes++;

            // Act
            filter.SetRange(new DateRange(day.AddDays(1).AddHours(23), day.AddHours(12)));

            filter.SetRange(new DateRange(day, day.AddDays(1)));

            // Assert
            Assert.Equal(new DateRange(day, day.AddDays(1)), filter.SelectedRange);

            Assert.Equal(1, changes);

            // Act
            filter.SetRange(null);

            // Assert
            Assert.Null(filter.SelectedRange);

            Assert.Equal(2, changes);
        });
    }
}
