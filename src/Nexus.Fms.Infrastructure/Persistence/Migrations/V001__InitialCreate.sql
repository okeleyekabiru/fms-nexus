-- ============================================================
-- V001__InitialCreate.sql
-- FMS schema — run once against the target PostgreSQL database.
-- Equivalent to `dotnet ef database update` for the initial snapshot.
-- ============================================================

-- ---- fraud_rules ----------------------------------------
CREATE TABLE IF NOT EXISTS fraud_rules (
    rule_id           UUID          NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    code              VARCHAR(20)   NOT NULL,
    name              VARCHAR(200)  NOT NULL,
    description       TEXT          NOT NULL DEFAULT '',
    category          VARCHAR(50)   NOT NULL,
    conditions_json   JSONB         NOT NULL DEFAULT '{}',
    score             INTEGER       NOT NULL DEFAULT 0,
    mode              VARCHAR(20)   NOT NULL DEFAULT 'Disabled',
    is_synchronous    BOOLEAN       NOT NULL DEFAULT TRUE,
    cannot_be_offset  BOOLEAN       NOT NULL DEFAULT FALSE,
    approval_status   VARCHAR(30)   NOT NULL DEFAULT 'PendingApproval',
    rejected_by       TEXT,
    rejection_reason  TEXT,
    created_by        TEXT,
    approved_by       TEXT,
    created_at        TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_fraud_rules_code ON fraud_rules (code);

-- ---- fraud_alerts ---------------------------------------
CREATE TABLE IF NOT EXISTS fraud_alerts (
    alert_id                UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    transaction_ref         TEXT        NOT NULL,
    customer_id             UUID        NOT NULL,
    triggered_rules_json    JSONB       NOT NULL DEFAULT '[]',
    composite_risk_score    INTEGER     NOT NULL DEFAULT 0,
    risk_level              VARCHAR(10) NOT NULL DEFAULT 'Clean',
    verdict                 VARCHAR(20) NOT NULL DEFAULT 'Allow',
    nibss_lookup_result_json JSONB,
    shadow_only             BOOLEAN     NOT NULL DEFAULT FALSE,
    sender_account          TEXT        NOT NULL DEFAULT '',
    sender_bvn              TEXT,
    amount                  NUMERIC(18,2) NOT NULL DEFAULT 0,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_fraud_alerts_transaction_ref ON fraud_alerts (transaction_ref);
CREATE INDEX IF NOT EXISTS ix_fraud_alerts_customer_id     ON fraud_alerts (customer_id);

-- ---- fraud_cases ----------------------------------------
CREATE TABLE IF NOT EXISTS fraud_cases (
    case_id           UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    alert_id          UUID        NOT NULL,
    status            VARCHAR(30) NOT NULL DEFAULT 'New',
    assigned_to       TEXT,
    resolution        VARCHAR(30),
    notes             TEXT        NOT NULL DEFAULT '',
    sar_reference     TEXT,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    resolved_at       TIMESTAMPTZ,
    last_escalated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_fraud_cases_alert_id ON fraud_cases (alert_id);
CREATE INDEX IF NOT EXISTS ix_fraud_cases_status   ON fraud_cases (status);

ALTER TABLE fraud_cases
    ADD CONSTRAINT fk_fraud_cases_alert_id
    FOREIGN KEY (alert_id) REFERENCES fraud_alerts (alert_id) ON DELETE RESTRICT;

-- ---- fraud_list_entries ---------------------------------
CREATE TABLE IF NOT EXISTS fraud_list_entries (
    entry_id       UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    bvn            TEXT,
    account_number TEXT,
    list_type      VARCHAR(20) NOT NULL,
    source         VARCHAR(20) NOT NULL DEFAULT 'Internal',
    reason         TEXT        NOT NULL DEFAULT '',
    created_by     TEXT,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_fraud_list_entries_bvn            ON fraud_list_entries (bvn);
CREATE INDEX IF NOT EXISTS ix_fraud_list_entries_account_number ON fraud_list_entries (account_number);

-- ---- fraud_async_evaluations ----------------------------
CREATE TABLE IF NOT EXISTS fraud_async_evaluations (
    id                      UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    alert_id                UUID        NOT NULL,
    transaction_context_json JSONB      NOT NULL DEFAULT '{}',
    status                  VARCHAR(20) NOT NULL DEFAULT 'Pending',
    error                   TEXT,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processed_at            TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_fraud_async_evaluations_status   ON fraud_async_evaluations (status);
CREATE INDEX IF NOT EXISTS ix_fraud_async_evaluations_alert_id ON fraud_async_evaluations (alert_id);

-- ---- fraud_audit_logs -----------------------------------
CREATE TABLE IF NOT EXISTS fraud_audit_logs (
    id           UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    action       TEXT        NOT NULL,
    entity_type  TEXT        NOT NULL,
    entity_id    UUID,
    old_values   JSONB,
    new_values   JSONB,
    performed_by TEXT        NOT NULL,
    timestamp    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_fraud_audit_logs_entity_type ON fraud_audit_logs (entity_type);
CREATE INDEX IF NOT EXISTS ix_frau