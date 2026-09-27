-- ═══════════════════════════════════════════════════════════════════════
-- 0006_refresh_token_absolute_expiry.sql — ProGlass SaaS (Phase 4)
--
-- Refresh tokens rotate on every use with a 30-day SLIDING window. Sliding
-- alone would let an active session live forever, so AuthOptions also
-- defines an ABSOLUTE lifetime (60 days) after which the user must sign
-- in again. Enforcing that requires knowing when the session's lineage
-- STARTED — a per-token created_at cannot express it (rotation creates a
-- fresh row), so the absolute deadline is stored per token and INHERITED
-- unchanged by each rotation (AuthService never extends it).
--
-- There is deliberately NO column default: PostgreSQL cannot reference
-- created_at in a DEFAULT expression, and the application always supplies
-- the value explicitly (RefreshTokenStore.CreateAsync) — a missing value
-- is a bug we want to hear about (NOT NULL), not silently paper over.
--
-- Also indexes token_hash: every refresh call looks a token up by hash
-- (previously only (user_id)/(tenant_id) indexes existed → sequential
-- scans per refresh).
-- ═══════════════════════════════════════════════════════════════════════

ALTER TABLE saas.refresh_tokens
  ADD COLUMN absolute_expires_at_utc timestamptz;

-- Backfill (no rows exist yet in any environment, but stay safe).
UPDATE saas.refresh_tokens
   SET absolute_expires_at_utc = created_at + interval '60 days'
 WHERE absolute_expires_at_utc IS NULL;

ALTER TABLE saas.refresh_tokens
  ALTER COLUMN absolute_expires_at_utc SET NOT NULL;

COMMENT ON COLUMN saas.refresh_tokens.absolute_expires_at_utc IS
  'Hard deadline for the session lineage; rotations inherit it unchanged (ADR-011).';

CREATE INDEX ix_refresh_tokens_token_hash ON saas.refresh_tokens (token_hash);
