ALTER TABLE signup_pending
    ADD COLUMN auto_approval_requested TINYINT(1) NOT NULL DEFAULT 0,
    ADD COLUMN approval_email_pending TINYINT(1) NOT NULL DEFAULT 0,
    ADD COLUMN approval_email_retry_after DATETIME(6) NULL;
