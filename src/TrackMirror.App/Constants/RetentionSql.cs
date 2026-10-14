// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class RetentionSql
{
    internal const string Candidates = """
        SELECT c.id,c.account,c.file_name,c.captured_utc,c.saved_utc,c.viewed,c.flagged FROM captures c
        LEFT JOIN privacy_scans p ON p.capture_key=c.lookup_key
        WHERE c.captured_utc<$cutoff AND ($keepFlagged=0 OR c.flagged=0)
        AND ($keepUnread=0 OR c.viewed=1) AND (p.state IS NULL OR p.state NOT IN (0,1));
        """;
    internal const string Intent = """
        INSERT INTO capture_deletions(capture_key,source_key,id,account,file_name,removed_utc,pending_delete)
        VALUES($key,$source,$id,$account,$file,$updated,1);
        DELETE FROM captures WHERE lookup_key=$key;
        """;
    internal const string Pending = "SELECT id,account,file_name FROM capture_deletions WHERE pending_delete=1;";
    internal const string Completed = "UPDATE capture_deletions SET pending_delete=0 WHERE capture_key=$key;";
    internal const string Deleted = "SELECT id,account,file_name FROM capture_deletions;";
    internal const string PendingCount = "SELECT COUNT(*) FROM capture_deletions WHERE pending_delete=1;";
}
