CREATE TABLE privacy_request_diagnostics (
    capture_key TEXT NOT NULL,
    category INTEGER NOT NULL,
    position INTEGER NOT NULL CHECK (position BETWEEN 0 AND 255),
    request_bytes INTEGER NOT NULL CHECK (request_bytes >= 0),
    ocr_blocks INTEGER NOT NULL CHECK (ocr_blocks >= 0),
    ocr_characters INTEGER NOT NULL CHECK (ocr_characters >= 0),
    prompt_tokens INTEGER CHECK (prompt_tokens >= 0),
    reply_tokens INTEGER CHECK (reply_tokens >= 0),
    duration_milliseconds REAL NOT NULL CHECK (duration_milliseconds >= 0),
    failure_code TEXT NOT NULL,
    request_id TEXT NOT NULL CHECK (length(request_id) = 36),
    PRIMARY KEY (capture_key, category, position),
    FOREIGN KEY (capture_key, category) REFERENCES privacy_categories(capture_key, category) ON DELETE CASCADE
);
