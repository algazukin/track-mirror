// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class CaptureSql
{
    internal const string SelectAll = """
        SELECT id, account, file_name, captured_utc, saved_utc, viewed, flagged FROM captures;
        """;

    internal const string Upsert = """
        INSERT INTO captures(lookup_key,id,account,file_name,captured_utc,saved_utc,viewed,flagged)
        VALUES($key,$id,$account,$file,$captured,$saved,$viewed,$flagged)
        ON CONFLICT(lookup_key) DO UPDATE SET
        id=excluded.id,account=excluded.account,file_name=excluded.file_name,captured_utc=excluded.captured_utc,
        saved_utc=excluded.saved_utc,viewed=excluded.viewed,flagged=excluded.flagged;
        """;
}
