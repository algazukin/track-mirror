// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class DataManagementSql
{
    internal const string Integrity = "PRAGMA integrity_check;";
    internal const string IntegrityOk = "ok";
    internal const string ForeignKeys = "SELECT COUNT(*) FROM pragma_foreign_key_check;";
    internal const string RequiredTables = """
        SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('schema_migrations','legacy_import',
        'captures','activity_intervals','privacy_scans','privacy_categories',
        'privacy_requested_categories','privacy_findings');
        """;
}
