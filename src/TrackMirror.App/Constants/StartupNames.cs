// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class StartupNames
{
    internal const string ShortcutPrefix = "TrackMirror-";
    internal const string ShortcutExtension = ".lnk";
    internal const string ShellProgId = "WScript.Shell";
    internal const string WatcherMutexPrefix = @"Local\TrackMirror-Watcher-";
    internal const string WatchOption = "--watch-hubstaff";
    internal const string LaunchOption = "--hubstaff-start";
    internal const string HubstaffProcessName = "HubstaffClient";
    internal const string ExecutableName = "TrackMirror.exe";
    internal const int PollMilliseconds = 1000;
    internal const int RetrySeconds = 10;
    internal const string ArgumentSeparator = " ";
    internal const string Quote = "\"";
    internal const string DirectoryMarker = ".";
}
