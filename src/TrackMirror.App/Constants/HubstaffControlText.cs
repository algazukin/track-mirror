// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Hubstaff;
using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class HubstaffControlText
{
    internal static string Heading => UiLocalization.Get(nameof(HubstaffControlText), nameof(Heading));
    internal static string Enabled => UiLocalization.Get(nameof(HubstaffControlText), nameof(Enabled));
    internal static string Mode => UiLocalization.Get(nameof(HubstaffControlText), nameof(Mode));
    internal static string PauseMode => UiLocalization.Get(nameof(HubstaffControlText), nameof(PauseMode));
    internal static string RestartMode => UiLocalization.Get(nameof(HubstaffControlText), nameof(RestartMode));
    internal static string PauseHint => UiLocalization.Get(nameof(HubstaffControlText), nameof(PauseHint));
    internal static string RestartHint => UiLocalization.Get(nameof(HubstaffControlText), nameof(RestartHint));
    internal static string IdleSeconds => UiLocalization.Get(nameof(HubstaffControlText), nameof(IdleSeconds));
    internal static string RestartSeconds => UiLocalization.Get(nameof(HubstaffControlText), nameof(RestartSeconds));
    internal static string CliPath => UiLocalization.Get(nameof(HubstaffControlText), nameof(CliPath));
    internal static string AutomaticPath => UiLocalization.Get(nameof(HubstaffControlText), nameof(AutomaticPath));
    internal static string Browse => UiLocalization.Get(nameof(HubstaffControlText), nameof(Browse));
    internal static string Detect => UiLocalization.Get(nameof(HubstaffControlText), nameof(Detect));
    internal static string FileFilter => UiLocalization.Get(nameof(HubstaffControlText), nameof(FileFilter));
    internal static string PermissionHint => UiLocalization.Get(nameof(HubstaffControlText), nameof(PermissionHint));
    internal static string Retry => UiLocalization.Get(nameof(HubstaffControlText), nameof(Retry));
    internal static string Disabled => UiLocalization.Get(nameof(HubstaffControlText), nameof(Disabled));
    internal static string WaitingForIdle => UiLocalization.Get(nameof(HubstaffControlText), nameof(WaitingForIdle));
    internal static string WaitingForTracking =>
        UiLocalization.Get(nameof(HubstaffControlText), nameof(WaitingForTracking));
    internal static string WaitingForRestart =>
        UiLocalization.Get(nameof(HubstaffControlText), nameof(WaitingForRestart));
    internal static string Paused => UiLocalization.Get(nameof(HubstaffControlText), nameof(Paused));
    internal static string Working => UiLocalization.Get(nameof(HubstaffControlText), nameof(Working));
    internal static string InvalidSettings => UiLocalization.Get(nameof(HubstaffControlText), nameof(InvalidSettings));
    internal static string MissingCli => UiLocalization.Get(nameof(HubstaffControlText), nameof(MissingCli));
    internal static string InvalidStatus => UiLocalization.Get(nameof(HubstaffControlText), nameof(InvalidStatus));
    internal static string CommandFailed => UiLocalization.Get(nameof(HubstaffControlText), nameof(CommandFailed));
    internal static string TimedOut => UiLocalization.Get(nameof(HubstaffControlText), nameof(TimedOut));
    internal static string InputUnavailable =>
        UiLocalization.Get(nameof(HubstaffControlText), nameof(InputUnavailable));
    internal static string SelectionChanged =>
        UiLocalization.Get(nameof(HubstaffControlText), nameof(SelectionChanged));
    internal static string StopUnconfirmed => UiLocalization.Get(nameof(HubstaffControlText), nameof(StopUnconfirmed));
    internal static string ResumeUnconfirmed =>
        UiLocalization.Get(nameof(HubstaffControlText), nameof(ResumeUnconfirmed));

    internal static string Status(HubstaffControlStatus status) => status.State switch
    {
        HubstaffControlState.Disabled => Disabled,
        HubstaffControlState.WaitingForIdle => string.Format(WaitingForIdle, status.Seconds),
        HubstaffControlState.WaitingForTracking => WaitingForTracking,
        HubstaffControlState.WaitingForRestart => string.Format(WaitingForRestart, status.Seconds),
        HubstaffControlState.Paused => Paused,
        HubstaffControlState.Working => Working,
        _ => Failure(status.FailureCode)
    };

    private static string Failure(string code) => code switch
    {
        HubstaffControlNames.MissingCli => MissingCli,
        HubstaffControlNames.InvalidStatus => InvalidStatus,
        HubstaffControlNames.TimedOut => TimedOut,
        HubstaffControlNames.InputUnavailable => InputUnavailable,
        HubstaffControlNames.SelectionChanged => SelectionChanged,
        HubstaffControlNames.StopUnconfirmed => StopUnconfirmed,
        HubstaffControlNames.ResumeUnconfirmed => ResumeUnconfirmed,
        _ => CommandFailed
    };
}
