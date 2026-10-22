// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.Monitoring;
using Xunit;

namespace TrackMirror.IntegrationTests.Hubstaff;

public sealed class ScreenshotScheduleMonitorTests
{
    private const string Plan = "2026-10-22 15:00:40.100000+0300 [DEBUG] [fixture:main] ScreenshotExtension.h:47  Screen Frequency: 2 Screen Slot: 2026-10-22 12:00:00Z Screen Next: 2026-10-22 12:02:51Z\n";
    private const string Started = "2026-10-22 15:00:40.100000+0300 [AUDIT] [fixture:main] ActivityTracker.cpp:2406  (START_TRACKING) Tracking Started: [Example:11] by [USER]\n";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-22T12:01:00Z");

    [Fact]
    public async Task Partial_appends_are_completed_once_and_a_replaced_log_discards_the_old_deadline()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var path = CreateSource(workspace);
        File.WriteAllText(path, Plan + Started[..70]);

        await using var monitor = new ScreenshotScheduleMonitor(workspace.Root, path, () => true);

        // Act
        monitor.Refresh();
        var partial = monitor.Snapshot(Now);

        File.AppendAllText(path, Started[70..]);
        monitor.Refresh();
        var complete = monitor.Snapshot(Now);

        File.Move(path, path + ".1");
        File.WriteAllText(path, Plan.Replace("12:02:51Z", "12:04:00Z") + Started);
        monitor.Refresh();
        var replaced = monitor.Snapshot(Now);

        // Assert
        Assert.Equal(ScreenshotScheduleStatus.Waiting, partial.Status);

        Assert.Equal(DateTimeOffset.Parse("2026-10-22T12:02:51Z"), complete.NextUtc);

        Assert.Equal(DateTimeOffset.Parse("2026-10-22T12:04:00Z"), replaced.NextUtc);
    }

    [Fact]
    public async Task Locked_missing_or_stopped_sources_never_keep_a_stale_timer_and_can_recover()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var path = CreateSource(workspace);
        File.WriteAllText(path, Plan + Started);

        var running = true;

        await using var monitor = new ScreenshotScheduleMonitor(workspace.Root, path, () => running);
        monitor.Refresh();

        // Act
        ScreenshotScheduleSnapshot locked;

        using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            monitor.Refresh();
            locked = monitor.Snapshot(Now);
        }

        monitor.Refresh();
        var recovered = monitor.Snapshot(Now);

        File.Delete(path);
        monitor.Refresh();
        var missing = monitor.Snapshot(Now);

        File.WriteAllText(path, Plan + Started);
        monitor.Refresh();
        var recreated = monitor.Snapshot(Now);

        running = false;
        monitor.Refresh();
        var stopped = monitor.Snapshot(Now);

        // Assert
        Assert.Equal(ScreenshotScheduleStatus.Unavailable, locked.Status);
        Assert.Null(locked.NextUtc);

        Assert.Equal(ScreenshotScheduleStatus.Scheduled, recovered.Status);

        Assert.Equal(ScreenshotScheduleStatus.Unavailable, missing.Status);
        Assert.Null(missing.NextUtc);

        Assert.Equal(ScreenshotScheduleStatus.Scheduled, recreated.Status);

        Assert.Equal(ScreenshotScheduleStatus.Stopped, stopped.Status);
        Assert.Null(stopped.NextUtc);
    }

    [Fact]
    public async Task A_project_name_split_inside_a_UTF8_character_is_reassembled_before_lookup()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var path = CreateSource(workspace);
        File.WriteAllText(Path.Combine(workspace.Root, "account", "Project.xml"),
            "<root><item><id>1</id><name>Projet été</name></item></root>");

        var bytes = System.Text.Encoding.UTF8.GetBytes(Plan + Started.Replace("Example:11", "Projet été:0"));
        var split = Array.IndexOf(bytes, (byte)0xc3) + 1;
        File.WriteAllBytes(path, bytes[..split]);

        await using var monitor = new ScreenshotScheduleMonitor(workspace.Root, path, () => true);

        // Act
        monitor.Refresh();
        var partial = monitor.Snapshot(Now);

        using (var stream = new FileStream(path, FileMode.Append))
            stream.Write(bytes[split..]);

        monitor.Refresh();
        var completed = monitor.Snapshot(Now);

        // Assert
        Assert.Equal(ScreenshotScheduleStatus.Waiting, partial.Status);

        Assert.Equal(ScreenshotScheduleStatus.Scheduled, completed.Status);
        Assert.Equal(DateTimeOffset.Parse("2026-10-22T12:02:51Z"), completed.NextUtc);
    }

    [Fact]
    public async Task The_previous_rotated_log_restores_the_session_and_the_current_log_supplies_its_next_slot()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var path = CreateSource(workspace);
        var archive = Path.ChangeExtension(path, ".1");
        File.WriteAllText(archive, Plan + Started);
        File.WriteAllText(path,
            "2026-10-22 15:02:51.100000+0300 [DEBUG] [fixture:work[Tracking]] ScreenshotExtension.h:149  Screen Slot: 2026-10-22 12:05:00Z\n" +
            "2026-10-22 15:02:51.100001+0300 [DEBUG] [fixture:work[Tracking]] ScreenshotExtension.h:150  Screen Next: 2026-10-22 12:06:56Z\n");

        await using var monitor = new ScreenshotScheduleMonitor(workspace.Root, path, () => true);

        // Act
        monitor.Refresh();
        var restored = monitor.Snapshot(Now.AddMinutes(2));

        File.Move(path, archive, overwrite: true);
        File.WriteAllText(path,
            "2026-10-22 15:06:56.100000+0300 [DEBUG] [fixture:work[Tracking]] ScreenshotExtension.h:149  Screen Slot: 2026-10-22 12:10:00Z\n" +
            "2026-10-22 15:06:56.100001+0300 [DEBUG] [fixture:work[Tracking]] ScreenshotExtension.h:150  Screen Next: 2026-10-22 12:12:42Z\n");
        monitor.Refresh();
        var rotated = monitor.Snapshot(Now.AddMinutes(7));

        // Assert
        Assert.Equal(ScreenshotScheduleStatus.Scheduled, restored.Status);
        Assert.Equal(DateTimeOffset.Parse("2026-10-22T12:06:56Z"), restored.NextUtc);

        Assert.Equal(ScreenshotScheduleStatus.Scheduled, rotated.Status);
        Assert.Equal(DateTimeOffset.Parse("2026-10-22T12:12:42Z"), rotated.NextUtc);
    }

    [Fact]
    public async Task An_unread_stop_at_the_end_of_a_rotated_log_is_consumed_before_the_new_file()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var path = CreateSource(workspace);
        File.WriteAllText(path, Plan + Started);

        await using var monitor = new ScreenshotScheduleMonitor(workspace.Root, path, () => true);
        monitor.Refresh();

        // Act
        File.AppendAllText(path,
            "2026-10-22 15:01:01.100000+0300 [AUDIT] [fixture:main] ActivityTracker.cpp:2490  (STOP_TRACKING) Tracking Stopped: [Example:11] by [USER]\n");
        File.Move(path, Path.ChangeExtension(path, ".1"));
        File.WriteAllText(path, "2026-10-22 15:01:02.100000+0300 [DEBUG] [fixture:main] Other.cpp:1  Rotated\n");

        monitor.Refresh();
        var value = monitor.Snapshot(Now.AddSeconds(3));

        // Assert
        Assert.Equal(ScreenshotScheduleStatus.Stopped, value.Status);
        Assert.Null(value.NextUtc);
    }

    private static string CreateSource(TestWorkspace workspace)
    {
        var account = Path.Combine(workspace.Root, "account");
        Directory.CreateDirectory(account);

        File.WriteAllText(Path.Combine(account, "Task.xml"), "<root><item><id>11</id><project>1</project></item></root>");
        File.WriteAllText(Path.Combine(account, "Project.xml"), "<root><item><id>1</id><name>Example</name></item></root>");

        return Path.Combine(workspace.Root, "hubstaff.log");
    }
}
