// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Monitoring;

namespace TrackMirror.UI;

internal static class CountdownDisplay
{
    internal static bool ShouldShow(ScreenshotCountdownOptions options, ScreenshotScheduleSnapshot snapshot)
    {
        if (!options.Enabled || options.Mode == ScreenshotCountdownMode.Default
            || snapshot.Status is ScreenshotScheduleStatus.Stopped or ScreenshotScheduleStatus.Disabled)
            return false;

        if (snapshot.Status != ScreenshotScheduleStatus.Scheduled || snapshot.RemainingSeconds is not > 0)
            return options.Mode == ScreenshotCountdownMode.Detailed && !options.HideBeforeScreenshot;

        if (options.HideBeforeScreenshot && snapshot.RemainingSeconds <= options.HideBeforeSeconds)
            return false;

        return options.Mode == ScreenshotCountdownMode.Detailed || snapshot.RemainingSeconds <= options.ShowBeforeSeconds;
    }

    internal static string TimeText(ScreenshotScheduleSnapshot snapshot) =>
        snapshot.Status == ScreenshotScheduleStatus.Scheduled && snapshot.RemainingSeconds is > 0
            ? TimeSpan.FromSeconds(snapshot.RemainingSeconds.Value).ToString(@"mm\:ss") : "--:--";

    internal static string StatusText(ScreenshotScheduleSnapshot snapshot) => snapshot.Status switch
    {
        ScreenshotScheduleStatus.Scheduled when snapshot.RemainingSeconds is > 0 => CountdownText.Scheduled + ": " + TimeText(snapshot),
        ScreenshotScheduleStatus.Stopped => CountdownText.Stopped,
        ScreenshotScheduleStatus.Disabled => CountdownText.Disabled,
        ScreenshotScheduleStatus.Unavailable => CountdownText.Unavailable,
        _ => CountdownText.Waiting
    };
}
