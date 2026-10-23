// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Monitoring;
using Xunit;

namespace TrackMirror.IntegrationTests.Hubstaff;

public sealed class ScreenshotScheduleTests
{
    [Fact]
    public void A_logged_UTC_schedule_is_counted_down_without_guessing_an_interval()
    {
        // Arrange
        var schedule = NewSchedule();

        // Act
        Start(schedule, "15:00:40", "12:00:00", "12:02:51", 11);
        var value = schedule.Snapshot(At("12:01:00"));

        // Assert
        Assert.Equal(ScreenshotScheduleStatus.Scheduled, value.Status);
        Assert.Equal(At("12:02:51"), value.NextUtc);
        Assert.Equal(111, value.RemainingSeconds);
    }

    [Fact]
    public void Switching_tasks_in_the_same_project_consumes_the_slot_and_waits_for_a_new_one()
    {
        // Arrange
        var schedule = NewSchedule();
        Start(schedule, "14:58:46", "11:55:00", "11:59:33", 11);
        schedule.Read(Line("14:59:33", "ScreenshotExtension.h:149", "Screen Slot: 2026-10-22 12:00:00Z"));
        schedule.Read(Line("14:59:33", "ScreenshotExtension.h:150", "Screen Next: 2026-10-22 12:03:32Z"));

        // Act
        schedule.Read(Line("15:00:40", "ActivityTracker.cpp:2490", "(STOP_TRACKING) Tracking Stopped: [Example:11] by [USER]"));
        Start(schedule, "15:00:40", "12:00:00", "12:02:51", 12);
        var occupied = schedule.Snapshot(At("12:01:00"));

        schedule.Read(Line("15:02:51", "ScreenshotExtension.h:149", "Screen Slot: 2026-10-22 12:05:00Z"));
        schedule.Read(Line("15:02:51", "ScreenshotExtension.h:150", "Screen Next: 2026-10-22 12:06:56Z"));
        var following = schedule.Snapshot(At("12:03:00"));

        // Assert
        Assert.Equal(ScreenshotScheduleStatus.Waiting, occupied.Status);
        Assert.Null(occupied.NextUtc);

        Assert.Equal(ScreenshotScheduleStatus.Scheduled, following.Status);
        Assert.Equal(At("12:06:56"), following.NextUtc);
    }

    [Fact]
    public void A_different_project_can_schedule_a_capture_in_the_same_time_slot()
    {
        // Arrange
        var schedule = NewSchedule();
        Start(schedule, "15:00:01", "12:00:00", "12:03:32", 11);

        // Act
        schedule.Read(Line("15:00:40", "ActivityTracker.cpp:2490", "(STOP_TRACKING) Tracking Stopped: [Example:11] by [USER]"));
        Start(schedule, "15:00:41", "12:00:00", "12:02:51", 22);
        var value = schedule.Snapshot(At("12:01:00"));

        // Assert
        Assert.Equal(ScreenshotScheduleStatus.Scheduled, value.Status);
        Assert.Equal(At("12:02:51"), value.NextUtc);
    }

    [Fact]
    public void A_consumed_slot_survives_late_task_metadata_and_switching_away_and_back()
    {
        // Arrange
        var metadataAvailable = false;
        var schedule = new ScreenshotSchedule((task, _) =>
            task == 11 && !metadataAvailable ? null : task == 22 ? 2 : 1);
        Start(schedule, "15:00:01", "12:00:00", "12:03:32", 11);

        // Act
        schedule.Read(Line("15:00:40", "ActivityTracker.cpp:2490", "(STOP_TRACKING) Tracking Stopped: [Example:11] by [USER]"));
        Start(schedule, "15:00:41", "12:00:00", "12:02:51", 12);
        var unresolved = schedule.Snapshot(At("12:00:42"));

        metadataAvailable = true;
        var resolved = schedule.Snapshot(At("12:01:00"));

        schedule.Read(Line("15:01:01", "ActivityTracker.cpp:2490", "(STOP_TRACKING) Tracking Stopped: [Example:12] by [USER]"));
        Start(schedule, "15:01:02", "12:00:00", "12:03:00", 22);
        var otherProject = schedule.Snapshot(At("12:01:03"));

        schedule.Read(Line("15:01:04", "ActivityTracker.cpp:2490", "(STOP_TRACKING) Tracking Stopped: [Example:22] by [USER]"));
        Start(schedule, "15:01:05", "12:00:00", "12:04:00", 11);
        var returned = schedule.Snapshot(At("12:01:06"));

        // Assert
        Assert.Equal(ScreenshotScheduleStatus.Waiting, unresolved.Status);
        Assert.Null(unresolved.NextUtc);

        Assert.Equal(ScreenshotScheduleStatus.Waiting, resolved.Status);
        Assert.Null(resolved.NextUtc);

        Assert.Equal(ScreenshotScheduleStatus.Scheduled, otherProject.Status);

        Assert.Equal(ScreenshotScheduleStatus.Waiting, returned.Status);
        Assert.Null(returned.NextUtc);
    }

    [Theory]
    [InlineData("Project update changed screen_frequency from 2 to 0 while tracking")]
    [InlineData("Screen shots are disabled")]
    [InlineData("Screenshots are disabled")]
    public void Disabling_captures_during_tracking_hides_the_timer(string message)
    {
        // Arrange
        var schedule = NewSchedule();
        Start(schedule, "15:00:40", "12:00:00", "12:02:51", 11);

        // Act
        schedule.Read(Line("15:01:00", "ScreenshotExtension.h:82", message));
        var value = schedule.Snapshot(At("12:01:01"));

        // Assert
        Assert.Equal(ScreenshotScheduleStatus.Disabled, value.Status);
        Assert.Null(value.NextUtc);
    }

    [Fact]
    public void An_expired_schedule_waits_and_a_stopped_session_never_keeps_counting()
    {
        // Arrange
        var schedule = NewSchedule();
        Start(schedule, "15:00:40", "12:00:00", "12:02:51", 11);

        // Act
        var expired = schedule.Snapshot(At("12:02:52"));

        schedule.Read(Line("15:03:00", "ActivityTracker.cpp:2490", "(STOP_TRACKING) Tracking Stopped: [Example:11] by [USER]"));
        var stopped = schedule.Snapshot(At("12:03:01"));

        // Assert
        Assert.Equal(ScreenshotScheduleStatus.Waiting, expired.Status);
        Assert.Null(expired.NextUtc);

        Assert.Equal(ScreenshotScheduleStatus.Stopped, stopped.Status);
        Assert.Null(stopped.NextUtc);
    }

    [Fact]
    public void A_malformed_schedule_invalidates_the_old_deadline_instead_of_reusing_it()
    {
        // Arrange
        var schedule = NewSchedule();
        Start(schedule, "15:00:40", "12:00:00", "12:02:51", 11);

        // Act
        schedule.Read(Line("15:01:00", "ScreenshotExtension.h:150", "Screen Next: not-a-date-time"));
        var value = schedule.Snapshot(At("12:01:01"));

        // Assert
        Assert.Equal(ScreenshotScheduleStatus.Waiting, value.Status);
        Assert.Null(value.NextUtc);
    }

    [Fact]
    public void Unresolved_project_identity_and_a_clock_moving_backwards_do_not_produce_a_false_countdown()
    {
        // Arrange
        var unknown = new ScreenshotSchedule((_, _) => null);
        var schedule = NewSchedule();

        // Act
        Start(unknown, "15:00:40", "12:00:00", "12:02:51", 11);
        Start(schedule, "15:00:40", "12:00:00", "12:02:51", 11);

        var unidentified = unknown.Snapshot(At("12:01:00"));
        var clockChanged = schedule.Snapshot(At("11:00:40"));

        // Assert
        Assert.Equal(ScreenshotScheduleStatus.Waiting, unidentified.Status);
        Assert.Equal(ScreenshotScheduleStatus.Waiting, clockChanged.Status);

        Assert.Null(unidentified.NextUtc);
        Assert.Null(clockChanged.NextUtc);
    }

    private static ScreenshotSchedule NewSchedule() => new((task, _) => task == 22 ? 2 : 1);

    private static void Start(ScreenshotSchedule schedule, string local, string slot, string next, int task)
    {
        schedule.Read(Line(local, "ScreenshotExtension.h:47",
            $"Screen Frequency: 2 Screen Slot: 2026-10-22 {slot}Z Screen Next: 2026-10-22 {next}Z"));
        schedule.Read(Line(local, "ActivityTracker.cpp:2406", $"(START_TRACKING) Tracking Started: [Example:{task}] by [USER]"));
    }

    private static string Line(string local, string source, string message) =>
        $"2026-10-22 {local}.100000+0300 [DEBUG] [fixture:work[Tracking]] {source}  {message}";

    private static DateTimeOffset At(string time) => DateTimeOffset.Parse($"2026-10-22T{time}Z");
}
