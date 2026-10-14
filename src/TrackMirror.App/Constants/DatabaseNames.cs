// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class DatabaseNames
{
    internal const string FileName = "viewer.db";
    internal const string InitialSchema = "TrackMirror.Resources.Storage.001_initial.sql";
    internal const string RetentionSchema = "TrackMirror.Resources.Storage.002_capture_retention.sql";
    internal const string PrivacyDiagnosticsSchema =
        "TrackMirror.Resources.Storage.003_privacy_request_diagnostics.sql";
    internal const int CurrentVersion = 3;
    internal const string Source = "$source";
    internal const string Cutoff = "$cutoff";
    internal const string KeepFlagged = "$keepFlagged";
    internal const string KeepUnread = "$keepUnread";
    internal const string Key = "$key";
    internal const string Id = "$id";
    internal const string Account = "$account";
    internal const string File = "$file";
    internal const string Captured = "$captured";
    internal const string Saved = "$saved";
    internal const string Viewed = "$viewed";
    internal const string Flagged = "$flagged";
    internal const string Started = "$started";
    internal const string Tracked = "$tracked";
    internal const string Mouse = "$mouse";
    internal const string Keyboard = "$keyboard";
    internal const string Overall = "$overall";
    internal const string Observed = "$observed";
    internal const string Request = "$request";
    internal const string Profile = "$profile";
    internal const string Hash = "$hash";
    internal const string State = "$state";
    internal const string Disposition = "$disposition";
    internal const string Failure = "$failure";
    internal const string Updated = "$updated";
    internal const string Attempts = "$attempts";
    internal const string Duration = "$duration";
    internal const string Category = "$category";
    internal const string Version = "$version";
    internal const string Position = "$position";
    internal const string Scope = "$scope";
    internal const string Severity = "$severity";
    internal const string Reason = "$reason";
    internal const string Regions = "$regions";
    internal const string Skipped = "$skipped";
}
