ALTER TABLE subscriptions
    MODIFY COLUMN status ENUM(
        'pending_approval',
        'pending_activation',
        'active',
        'suspended',
        'cancelled',
        'expired'
    ) NOT NULL DEFAULT 'pending_approval';
