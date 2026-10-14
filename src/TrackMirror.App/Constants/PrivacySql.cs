// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class PrivacySql
{
    internal const string SelectScans = """
        SELECT capture_key,capture_id,request_id,profile,image_hash,state,disposition,failure_code,
        updated_utc,attempts,duration_seconds FROM privacy_scans;
        """;

    internal const string SelectCategories = """
        SELECT capture_key,category,version,state,failure_code,updated_utc FROM privacy_categories ORDER BY position;
        """;

    internal const string SelectRequested =
        "SELECT capture_key,category FROM privacy_requested_categories ORDER BY position;";

    internal const string SelectFindings = """
        SELECT capture_key,scan_category,category,severity,reason,regions_json FROM privacy_findings ORDER BY position;
        """;

    internal const string UpsertScan = """
        INSERT INTO privacy_scans(capture_key,capture_id,request_id,profile,image_hash,state,disposition,
        failure_code,updated_utc,attempts,duration_seconds)
        VALUES($key,$id,$request,$profile,$hash,$state,$disposition,$failure,$updated,$attempts,$duration)
        ON CONFLICT(capture_key) DO UPDATE SET capture_id=excluded.capture_id,request_id=excluded.request_id,
        profile=excluded.profile,image_hash=excluded.image_hash,state=excluded.state,disposition=excluded.disposition,
        failure_code=excluded.failure_code,updated_utc=excluded.updated_utc,
        attempts=excluded.attempts,duration_seconds=excluded.duration_seconds;
        """;

    internal const string ClearChildren = """
        DELETE FROM privacy_categories WHERE capture_key=$key;
        DELETE FROM privacy_requested_categories WHERE capture_key=$key;
        DELETE FROM privacy_findings WHERE capture_key=$key;
        """;

    internal const string InsertCategory = """
        INSERT INTO privacy_categories(capture_key,category,position,version,state,failure_code,updated_utc)
        VALUES($key,$category,$position,$version,$state,$failure,$updated);
        """;

    internal const string InsertRequested = """
        INSERT INTO privacy_requested_categories(capture_key,category,position) VALUES($key,$category,$position);
        """;

    internal const string InsertFinding = """
        INSERT INTO privacy_findings(capture_key,scan_category,position,category,severity,reason,regions_json)
        VALUES($key,$scope,$position,$category,$severity,$reason,$regions);
        """;
}
