-- Phase C1. PI identity contract and additive schema.
--
-- What this file does, and nothing else:
--   1. Keeps the originating website submission on the published inquiry.
--   2. Gives a desktop-authored proforma invoice a stable canonical identity
--      that the authoring application generates while offline.
--   3. Lets a desktop-origin proforma row carry the published inquiry it
--      belongs to, without changing the website-origin rule.
--   4. Stores the customer-facing proforma number verbatim for desktop origin,
--      so no second number and no mandatory PI-DSK prefix is introduced.
--
-- What this file deliberately does not do: no publishing function, no number
-- allocation, no status or lifecycle change, no pricing or rounding change, no
-- issuance or document record, no policy, grant, revoke or role change, and no
-- change to any existing row. Those are C2 or later, or are not approved.
--
-- Idempotent: safe to apply twice. Every step is either IF NOT EXISTS, a
-- drop-and-recreate of a constraint this file owns, or a COMMENT.
-- Rollback: docs/PHASE_C1_IMPLEMENTATION_REPORT.md, section "Rollback".

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Published inquiry to originating website submission.
--
-- public.inquiries is the desktop-published mirror (INQ- numbers), not the
-- intake table. The intake row lives in inquiry_customer_submissions (WEB-
-- numbers). Until now the two could not be joined in the database at all.
--
-- Nullable on purpose: every historical inquiry predates the column, desktop
-- only inquiries have no submission, and a relationship is only written when
-- the publisher can prove it. No backfill, no inference from names or numbers.
-- ---------------------------------------------------------------------------

ALTER TABLE public.inquiries
  ADD COLUMN IF NOT EXISTS web_submission_id uuid;

ALTER TABLE public.inquiries
  DROP CONSTRAINT IF EXISTS inquiries_web_submission_id_fkey;

ALTER TABLE public.inquiries
  ADD CONSTRAINT inquiries_web_submission_id_fkey
  FOREIGN KEY (web_submission_id)
  REFERENCES public.inquiry_customer_submissions(id)
  ON DELETE RESTRICT;

-- One live published inquiry per submission. A tombstoned inquiry is the
-- publisher's record of a source row that disappeared, so it must not block a
-- later re-ingest of the same submission. The predicate mirrors the existing
-- availability model ('available', 'source_missing', 'tombstoned').
CREATE UNIQUE INDEX IF NOT EXISTS inquiries_web_submission_live
  ON public.inquiries (web_submission_id)
  WHERE web_submission_id IS NOT NULL
    AND availability <> 'tombstoned';

COMMENT ON COLUMN public.inquiries.web_submission_id IS
  'Originating website submission (public.inquiry_customer_submissions.id) when the publisher can prove it. Null for historical, unlinked or desktop-only inquiries.';

-- ---------------------------------------------------------------------------
-- 2. Canonical desktop proforma identity.
--
-- The desktop mints this UUID when the invoice is created, so the identity
-- exists before any network call and survives offline work. It is the anchor a
-- later publication uses for idempotency. Null for a website-origin draft,
-- which the server already identifies by its own row id.
-- ---------------------------------------------------------------------------

ALTER TABLE public.inquiry_proformas
  ADD COLUMN IF NOT EXISTS client_public_id uuid;

CREATE UNIQUE INDEX IF NOT EXISTS inquiry_proformas_client_public_id
  ON public.inquiry_proformas (client_public_id)
  WHERE client_public_id IS NOT NULL;

ALTER TABLE public.inquiry_proformas
  DROP CONSTRAINT IF EXISTS inquiry_proformas_client_identity;

ALTER TABLE public.inquiry_proformas
  ADD CONSTRAINT inquiry_proformas_client_identity
  CHECK (client_public_id IS NULL OR source_system = 'desktop');

COMMENT ON COLUMN public.inquiry_proformas.client_public_id IS
  'Desktop-minted canonical identity for a desktop-origin proforma. Null for website-origin drafts. Unique across both origins.';

-- ---------------------------------------------------------------------------
-- 3. Origin identity.
--
-- The Phase 15 rule required a desktop row to have no inquiry_id, which made a
-- published desktop invoice unable to point at the published inquiry it came
-- from. The website branch is unchanged, character for character. The desktop
-- branch keeps its existing requirements (a non-empty source number) and drops
-- only the inquiry_id prohibition.
--
-- Numbering rules are untouched here; the next block owns the number format.
-- ---------------------------------------------------------------------------

ALTER TABLE public.inquiry_proformas
  DROP CONSTRAINT IF EXISTS inquiry_proformas_source_identity;

ALTER TABLE public.inquiry_proformas
  ADD CONSTRAINT inquiry_proformas_source_identity
  CHECK (
    (source_system = 'website'
       AND inquiry_id IS NOT NULL
       AND source_proforma_number = '')
    OR
    (source_system = 'desktop'
       AND source_proforma_number <> '')
  );

COMMENT ON CONSTRAINT inquiry_proformas_source_identity ON public.inquiry_proformas IS
  'Website rows: inquiry required, no source number. Desktop rows: source number required, inquiry optional so a published desktop invoice can link to its published inquiry.';

-- ---------------------------------------------------------------------------
-- 4. Customer-facing number, per origin.
--
-- The approved direction is one number per document: the number the desktop
-- assigned, stored verbatim, with no PI-DSK prefix. The Phase 15 column check
-- admitted only PI-WEB or PI-DSK, so it has to become origin aware. The check
-- was declared inline, so its name is whatever PostgreSQL generated; it is
-- found by definition rather than assumed by name, and the block raises if a
-- format check is still present afterwards, so a silent no-op is impossible.
--
-- The desktop pattern accepts the real allocator output and the custom ids the
-- desktop allows (PI-2026-001, INV-42, ABC-001, and the hex fallback), bounded
-- to the desktop's 50 character ProformaId.
-- ---------------------------------------------------------------------------

ALTER TABLE public.inquiry_proformas
  DROP CONSTRAINT IF EXISTS inquiry_proformas_proforma_number_origin;

DO $$
DECLARE v_name text;
BEGIN
  FOR v_name IN
    SELECT c.conname
      FROM pg_constraint c
     WHERE c.conrelid = 'public.inquiry_proformas'::regclass
       AND c.contype = 'c'
       AND c.conname <> 'inquiry_proformas_proforma_number_origin'
       AND pg_get_constraintdef(c.oid) ~ 'proforma_number[[:space:]]*~'
  LOOP
    EXECUTE format('ALTER TABLE public.inquiry_proformas DROP CONSTRAINT %I', v_name);
  END LOOP;

  IF EXISTS (
    SELECT 1
      FROM pg_constraint c
     WHERE c.conrelid = 'public.inquiry_proformas'::regclass
       AND c.contype = 'c'
       AND c.conname <> 'inquiry_proformas_proforma_number_origin'
       AND pg_get_constraintdef(c.oid) ~ 'proforma_number[[:space:]]*~'
  ) THEN
    RAISE EXCEPTION 'An unrecognised proforma_number format check remains on public.inquiry_proformas.';
  END IF;
END
$$;

ALTER TABLE public.inquiry_proformas
  ADD CONSTRAINT inquiry_proformas_proforma_number_origin
  CHECK (
    (source_system = 'website'
       AND proforma_number ~ '^PI-WEB-[0-9]{4}-[0-9]{6}$')
    OR
    (source_system = 'desktop'
       AND proforma_number ~ '^[A-Za-z0-9][A-Za-z0-9._-]{2,49}$')
  );

COMMENT ON CONSTRAINT inquiry_proformas_proforma_number_origin ON public.inquiry_proformas IS
  'Website origin keeps the PI-WEB-YYYY-NNNNNN server format. Desktop origin stores the desktop number verbatim, bounded to the desktop ProformaId width.';

COMMIT;
