// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Globalization;
using System.Text.RegularExpressions;

namespace TrackMirror.Monitoring;

internal enum ScreenshotScheduleStatus { Waiting, Scheduled, Stopped, Unavailable, Disabled }

internal sealed record ScreenshotScheduleSnapshot(ScreenshotScheduleStatus Status, DateTimeOffset? NextUtc = null,
    int? RemainingSeconds = null);

internal sealed class ScreenshotSchedule(Func<long, string, long?> resolveProject)
{
    private static readonly Regex LogLine = new(
        @"^(?<date>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{1,7})(?<zone>[+-]\d{4}) \[[A-Z]+\] \[(?<thread>.+)\] (?<source>[A-Za-z0-9_.]+:\d+) +(?<message>.*)$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static readonly Regex Initial = new(
        @"^Screen Frequency: (?<frequency>\d+) Screen Slot: (?<slot>[^Z]+Z) Screen Next: (?<next>.+)$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static readonly Regex Started = new(
        @"^\(START_TRACKING\) Tracking Started: \[(?<project>.*):(?<task>\d+)\] by \[",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static readonly Regex FrequencyChanged = new(
        @"^Project update changed screen_frequency from \d+ to (?<frequency>\d+) while tracking$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private readonly Dictionary<(long Task, string Project), (DateTimeOffset Slot, long Order)> _occupied = [];
    private long captureOrder;

    private DateTimeOffset? slot;
    private DateTimeOffset? next;
    private DateTimeOffset lastEvent;
    private (DateTimeOffset? Slot, DateTimeOffset? Next, int Frequency, DateTimeOffset At)? pending;

    private long task;
    private string project = string.Empty;
    private int frequency;
    private bool? tracking;

    internal void Read(string line)
    {
        if (line.Length > 4096)
            return;

        var record = LogLine.Match(line);

        if (!record.Success)
            return;

        var zone = record.Groups["zone"].Value;
        var timestamp = record.Groups["date"].Value + zone[..3] + ":" + zone[3..];

        if (!DateTimeOffset.TryParseExact(timestamp, "yyyy-MM-dd HH:mm:ss.FFFFFFFzzz",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) || at < lastEvent)
            return;

        var message = record.Groups["message"].Value;
        var source = record.Groups["source"].Value;

        if (source.StartsWith("ScreenshotExtension.h:", StringComparison.Ordinal))
        {
            lastEvent = at;

            var initial = Initial.Match(message);

            if (initial.Success)
            {
                pending = (ParseTime(initial.Groups["slot"].Value), ParseTime(initial.Groups["next"].Value),
                    int.TryParse(initial.Groups["frequency"].Value, out var count) ? count : 0, at);
            }
            else if (message.StartsWith("Screen Slot: ", StringComparison.Ordinal))
            {
                // step schedules the old slot before logging the following slot, including duplicates.
                if (tracking == true && next.HasValue && at >= next && slot <= at)
                    _occupied[(task, project)] = (slot!.Value, ++captureOrder);

                slot = ParseTime(message[13..]);
                next = null;
            }
            else if (message.StartsWith("Screen Next: ", StringComparison.Ordinal))
            {
                next = ParseTime(message[13..]);
            }
            else if (message.StartsWith("Screen shots are disabled", StringComparison.Ordinal)
                || message.StartsWith("Screenshots are disabled", StringComparison.Ordinal))
            {
                pending = (null, null, 0, at);
                next = null;
                frequency = 0;
            }
            else if (message.StartsWith("Project update changed screen_frequency", StringComparison.Ordinal))
            {
                // pre_step can silently replace the deadline. Wait for its next logged schedule.
                next = null;

                var changed = FrequencyChanged.Match(message);
                frequency = changed.Success && int.TryParse(changed.Groups["frequency"].Value, out var count)
                    ? count : -1;
            }

            return;
        }

        if (!source.StartsWith("ActivityTracker.cpp:", StringComparison.Ordinal))
            return;

        var started = Started.Match(message);

        if (started.Success)
        {
            lastEvent = at;
            tracking = true;

            task = long.TryParse(started.Groups["task"].Value, out var id) ? id : 0;
            project = started.Groups["project"].Value;

            var fresh = pending.HasValue && at - pending.Value.At < TimeSpan.FromSeconds(5);
            slot = fresh ? pending!.Value.Slot : null;
            next = fresh ? pending!.Value.Next : null;
            frequency = fresh ? pending!.Value.Frequency : -1;
            pending = null;
        }
        else if (message.StartsWith("(STOP_TRACKING)", StringComparison.Ordinal))
        {
            // Session completion uses the same per-project cache as a scheduled step.
            if (tracking == true && slot.HasValue && next.HasValue && slot < at && at < next)
                _occupied[(task, project)] = (slot.Value, ++captureOrder);

            lastEvent = at;
            tracking = false;
            slot = next = null;
            pending = null;
        }
    }

    internal ScreenshotScheduleSnapshot Snapshot(DateTimeOffset now)
    {
        if (tracking == false)
            return new(ScreenshotScheduleStatus.Stopped);

        if (tracking == true && frequency == 0)
            return new(ScreenshotScheduleStatus.Disabled);

        if (tracking != true || !slot.HasValue || !next.HasValue || next <= now || next < slot
            || next - slot > TimeSpan.FromMinutes(10) || lastEvent > now.AddSeconds(5)
            || resolveProject(task, project) is not { } key)
            return new(ScreenshotScheduleStatus.Waiting);

        // Keep task identities until lookup: Task.xml can arrive after a session has already stopped.
        (DateTimeOffset Slot, long Order)? occupied = null;

        foreach (var pair in _occupied)
        {
            var captureProject = resolveProject(pair.Key.Task, pair.Key.Project);

            if (captureProject is null && pair.Value.Slot == slot)
                return new(ScreenshotScheduleStatus.Waiting);

            if (captureProject == key
                && (!occupied.HasValue || pair.Value.Order > occupied.Value.Order))
                occupied = pair.Value;
        }

        if (occupied?.Slot == slot)
            return new(ScreenshotScheduleStatus.Waiting);

        return new(ScreenshotScheduleStatus.Scheduled, next, (int)Math.Ceiling((next.Value - now).TotalSeconds));
    }

    private static DateTimeOffset? ParseTime(string value) => DateTimeOffset.TryParseExact(value,
        "yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result) ? result : null;
}
