// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class DatabaseSql
{
    internal const string Wal = "PRAGMA journal_mode=WAL;";
    internal const string ReadVersion = "PRAGMA user_version;";
    internal const string SetVersion = "PRAGMA user_version=";
    internal const string StampVersion =
        "INSERT INTO schema_migrations(version, applied_utc) VALUES($version, $updated);";
    internal const string ImportComplete = "SELECT EXISTS(SELECT 1 FROM legacy_import WHERE id=1);";
    internal const string ImportWarning = "SELECT skipped_rows FROM legacy_import WHERE id=1;";
    internal const string CompleteImport =
        "INSERT INTO legacy_import(id, completed_utc, skipped_rows) VALUES(1, $updated, $skipped);";
}
