// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.


using TrackMirror.Models;

namespace TrackMirror.Hubstaff;

internal sealed class TimerNoteTracker
{
    private bool enabled;
    private HubstaffTrackingStatus? previous;

    internal void SetEnabled(bool value)
    {
        if (enabled != value)
        {
            enabled = value;
            previous = null;
        }
    }

    internal TimerStopNote? Observe(HubstaffTrackingStatus status, DateTime utcNow)
    {
        if (!enabled)
        {
            return null;
        }

        var active = previous;
        previous = status;

        if (active is not { Tracking: true } || status.Tracking)
        {
            return null;
        }

        return new TimerStopNote(Guid.NewGuid().ToString("N"), utcNow, active.ProjectId, active.TaskId);
    }

    internal TimerStopNote? IdleStopped(HubstaffTrackingStatus status, int seconds, DateTime utcNow)
    {
        if (!enabled)
        {
            return null;
        }

        previous = status with { Tracking = false };

        return new TimerStopNote(Guid.NewGuid().ToString("N"), utcNow, status.ProjectId, status.TaskId, seconds);
    }

    internal void IgnoreStop(HubstaffTrackingStatus status)
    {
        previous = enabled ? status with { Tracking = false } : null;
    }
}
