CREATE TABLE IF NOT EXISTS request_events (
    id CHAR(36) NOT NULL PRIMARY KEY,
    request_type VARCHAR(32) NOT NULL,
    request_id CHAR(36) NOT NULL,
    actor_user_id CHAR(36) NULL,
    event_type VARCHAR(64) NOT NULL,
    old_status VARCHAR(40) NULL,
    new_status VARCHAR(40) NULL,
    correlation_id VARCHAR(128) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    KEY ix_request_events_request (request_type, request_id, created_at),
    CONSTRAINT fk_request_events_actor
        FOREIGN KEY (actor_user_id) REFERENCES portal_users (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS request_internal_notes (
    id CHAR(36) NOT NULL PRIMARY KEY,
    request_type VARCHAR(32) NOT NULL,
    request_id CHAR(36) NOT NULL,
    author_user_id CHAR(36) NOT NULL,
    note_text TEXT NOT NULL,
    created_at DATETIME(6) NOT NULL,
    KEY ix_request_internal_notes_request (
        request_type,
        request_id,
        created_at
    ),
    CONSTRAINT fk_request_internal_notes_author
        FOREIGN KEY (author_user_id) REFERENCES portal_users (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

CREATE TABLE IF NOT EXISTS request_public_messages (
    id CHAR(36) NOT NULL PRIMARY KEY,
    request_type VARCHAR(32) NOT NULL,
    request_id CHAR(36) NOT NULL,
    author_user_id CHAR(36) NOT NULL,
    message_text TEXT NOT NULL,
    created_at DATETIME(6) NOT NULL,
    KEY ix_request_public_messages_request (
        request_type,
        request_id,
        created_at
    ),
    CONSTRAINT fk_request_public_messages_author
        FOREIGN KEY (author_user_id) REFERENCES portal_users (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
-- statement-break

INSERT INTO request_events (
    id, request_type, request_id, actor_user_id, event_type, old_status,
    new_status, correlation_id, created_at
)
SELECT
    UUID(), 'support', sr.id, sr.created_by_user_id, 'created', NULL,
    sr.status, CONCAT('migration-004-support-', sr.id), sr.created_at
FROM support_requests sr
WHERE NOT EXISTS (
    SELECT 1
    FROM request_events event
    WHERE event.request_type = 'support'
      AND event.request_id = sr.id
      AND event.event_type = 'created'
);
-- statement-break

INSERT INTO request_events (
    id, request_type, request_id, actor_user_id, event_type, old_status,
    new_status, correlation_id, created_at
)
SELECT
    UUID(), 'service', request.id, request.created_by_user_id, 'created', NULL,
    request.status, CONCAT('migration-004-service-', request.id),
    request.created_at
FROM service_requests request
WHERE NOT EXISTS (
    SELECT 1
    FROM request_events event
    WHERE event.request_type = 'service'
      AND event.request_id = request.id
      AND event.event_type = 'created'
);

