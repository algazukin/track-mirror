CREATE TABLE timer_stop_notes (
    id TEXT PRIMARY KEY NOT NULL,
    stopped_utc INTEGER NOT NULL,
    project_id TEXT,
    task_id TEXT,
    idle_seconds INTEGER NOT NULL DEFAULT 0 CHECK (idle_seconds >= 0),
    reason TEXT NOT NULL DEFAULT '' CHECK (length(reason) <= 1000)
);
CREATE INDEX timer_stop_notes_stopped ON timer_stop_notes(stopped_utc);
