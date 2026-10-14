// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class HubstaffControlNames
{
    internal const int DefaultIdleSeconds = 60;
    internal const int DefaultRestartSeconds = 60;
    internal const int MaximumSeconds = 86400;
    internal const int CommandTimeoutSeconds = 15;
    internal const int PollMilliseconds = 1000;
    internal const int MaximumOutputCharacters = 65536;
    internal const string CliExecutable = "HubstaffCLI.exe";
    internal const string InstallDirectory = "Hubstaff";
    internal const string Status = "status";
    internal const string Stop = "stop";
    internal const string Resume = "resume";
    internal const string Tracking = "tracking";
    internal const string ActiveProject = "active_project";
    internal const string ActiveTask = "active_task";
    internal const string TaskId = "task_id";
    internal const string Id = "id";
    internal const string NoSelectionId = "0";
    internal const string Error = "error";
    internal const string MissingCli = "cli_missing";
    internal const string InvalidStatus = "invalid_status";
    internal const string CommandFailed = "command_failed";
    internal const string TimedOut = "command_timeout";
    internal const string InputUnavailable = "input_unavailable";
    internal const string SelectionChanged = "selection_changed";
    internal const string StopUnconfirmed = "stop_unconfirmed";
    internal const string ResumeUnconfirmed = "resume_unconfirmed";
}
