CREATE TABLE schema_migrations (
    version INTEGER PRIMARY KEY,
    applied_utc INTEGER NOT NULL
);

CREATE TABLE legacy_import (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    completed_utc INTEGER NOT NULL,
    skipped_rows INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE captures (
    lookup_key TEXT PRIMARY KEY,
    id TEXT NOT NULL,
    account TEXT NOT NULL,
    file_name TEXT NOT NULL,
    captured_utc INTEGER NOT NULL,
    saved_utc INTEGER NOT NULL,
    viewed INTEGER NOT NULL CHECK (viewed IN (0, 1)),
    flagged INTEGER NOT NULL CHECK (flagged IN (0, 1))
);
CREATE INDEX captures_by_time ON captures(captured_utc DESC, saved_utc DESC);
CREATE INDEX captures_by_account_time ON captures(account, captured_utc DESC);

CREATE TABLE activity_intervals (
    account_key TEXT NOT NULL,
    account TEXT NOT NULL,
    started_utc INTEGER NOT NULL,
    tracked_seconds INTEGER NOT NULL CHECK (tracked_seconds BETWEEN 1 AND 600),
    mouse_seconds INTEGER NOT NULL CHECK (mouse_seconds >= 0),
    keyboard_seconds INTEGER NOT NULL CHECK (keyboard_seconds >= 0),
    overall_seconds INTEGER NOT NULL CHECK (overall_seconds BETWEEN 0 AND tracked_seconds),
    observed_utc INTEGER NOT NULL,
    PRIMARY KEY (account_key, started_utc),
    CHECK (mouse_seconds <= overall_seconds AND keyboard_seconds <= overall_seconds),
    CHECK (overall_seconds <= mouse_seconds + keyboard_seconds)
);
CREATE INDEX activity_by_time ON activity_intervals(started_utc);

CREATE TABLE privacy_scans (
    capture_key TEXT PRIMARY KEY REFERENCES captures(lookup_key) ON DELETE CASCADE,
    capture_id TEXT NOT NULL,
    request_id TEXT NOT NULL,
    profile TEXT NOT NULL,
    image_hash TEXT NOT NULL,
    state INTEGER NOT NULL CHECK (state BETWEEN 0 AND 5),
    disposition INTEGER NOT NULL CHECK (disposition BETWEEN 0 AND 2),
    failure_code TEXT NOT NULL,
    updated_utc INTEGER NOT NULL,
    attempts INTEGER NOT NULL,
    duration_seconds REAL NOT NULL
);
CREATE INDEX privacy_by_state ON privacy_scans(state, updated_utc);

CREATE TABLE privacy_categories (
    capture_key TEXT NOT NULL REFERENCES privacy_scans(capture_key) ON DELETE CASCADE,
    category INTEGER NOT NULL CHECK (category BETWEEN 0 AND 2),
    position INTEGER NOT NULL,
    version TEXT NOT NULL,
    state INTEGER NOT NULL CHECK (state BETWEEN 0 AND 5),
    failure_code TEXT NOT NULL,
    updated_utc INTEGER NOT NULL,
    PRIMARY KEY (capture_key, category)
);

CREATE TABLE privacy_requested_categories (
    capture_key TEXT NOT NULL REFERENCES privacy_scans(capture_key) ON DELETE CASCADE,
    category INTEGER NOT NULL CHECK (category BETWEEN 0 AND 2),
    position INTEGER NOT NULL,
    PRIMARY KEY (capture_key, category)
);

CREATE TABLE privacy_findings (
    id INTEGER PRIMARY KEY,
    capture_key TEXT NOT NULL REFERENCES privacy_scans(capture_key) ON DELETE CASCADE,
    scan_category INTEGER CHECK (scan_category BETWEEN 0 AND 2),
    position INTEGER NOT NULL,
    category TEXT NOT NULL,
    severity TEXT NOT NULL,
    reason TEXT NOT NULL,
    regions_json TEXT NOT NULL
);
CREATE INDEX findings_by_capture ON privacy_findings(capture_key, scan_category, position);
