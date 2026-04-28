CREATE TABLE IF NOT EXISTS daily_balance (
    merchant_id       UUID NOT NULL,
    date              DATE NOT NULL,
    total_credits     NUMERIC(18,4) NOT NULL DEFAULT 0,
    total_debits      NUMERIC(18,4) NOT NULL DEFAULT 0,
    balance           NUMERIC(18,4) NOT NULL DEFAULT 0,
    transaction_count INTEGER NOT NULL DEFAULT 0,
    last_updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_daily_balance PRIMARY KEY (merchant_id, date)
);

CREATE TABLE IF NOT EXISTS processed_events (
    event_id     UUID NOT NULL,
    event_type   VARCHAR(100) NOT NULL,
    occurred_at  TIMESTAMPTZ NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_processed_events PRIMARY KEY (event_id)
);

CREATE INDEX IF NOT EXISTS ix_daily_balance_merchant ON daily_balance (merchant_id, date DESC);
CREATE INDEX IF NOT EXISTS ix_processed_events_occurred ON processed_events (occurred_at);
