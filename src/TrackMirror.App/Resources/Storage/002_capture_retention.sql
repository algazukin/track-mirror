CREATE TABLE capture_deletions (
    capture_key TEXT PRIMARY KEY,
    source_key TEXT NOT NULL,
    id TEXT NOT NULL,
    account TEXT NOT NULL,
    file_name TEXT NOT NULL,
    removed_utc INTEGER NOT NULL,
    pending_delete INTEGER NOT NULL CHECK (pending_delete IN (0, 1))
);
CREATE INDEX deletions_by_source ON capture_deletions(source_key);
CREATE INDEX deletions_pending ON capture_deletions(pending_delete);
