-- Phase 15, part 1. A proforma invoice a website operator can start from an
-- inquiry.
--
-- The inquiry register stays desktop-owned and read-only. This file adds a
-- website-side proforma record with enough identity to meet the desktop one
-- day: a revision counter, an idempotency key per save, and a source system.
-- Nothing here reads or writes the desktop proforma, the demonstration ledger
-- behind /reports, or any internal cost, margin or optimisation figure.
--
-- Line money is recomputed by the write function in part 2 from the panel
-- dimensions and the approved rate. The client never decides a stored value.

BEGIN;

CREATE SEQUENCE IF NOT EXISTS public.inquiry_proforma_number_seq;

CREATE TABLE IF NOT EXISTS public.inquiry_proformas (
  id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  inquiry_id             uuid REFERENCES public.inquiries(id) ON DELETE RESTRICT,
  proforma_number        text NOT NULL UNIQUE
                           CHECK (proforma_number ~ '^PI-(WEB|DSK)-[0-9]{4}-[0-9]{6}$'),
  source_system          text NOT NULL CHECK (source_system IN ('website', 'desktop')),
  source_proforma_number text NOT NULL DEFAULT '',
  inquiry_number         text NOT NULL DEFAULT '',
  customer_name          text NOT NULL DEFAULT '',
  status                 text NOT NULL DEFAULT 'Draft'
                           CHECK (status IN ('Draft', 'Issued', 'Void')),
  currency               text NOT NULL DEFAULT 'AED' CHECK (currency ~ '^[A-Z]{3}$'),
  revision               integer NOT NULL DEFAULT 1 CHECK (revision >= 1),
  line_count             integer NOT NULL DEFAULT 0 CHECK (line_count >= 0),
  total_sqm              numeric(14,4) NOT NULL DEFAULT 0 CHECK (total_sqm >= 0),
  total_value            numeric(18,2) NOT NULL DEFAULT 0 CHECK (total_value >= 0),
  created_by             uuid NOT NULL DEFAULT auth.uid()
                           REFERENCES auth.users(id) ON DELETE RESTRICT,
  created_at             timestamptz NOT NULL DEFAULT now(),
  updated_at             timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT inquiry_proformas_source_identity CHECK (
    (source_system = 'website' AND inquiry_id IS NOT NULL AND source_proforma_number = '')
    OR (source_system = 'desktop' AND inquiry_id IS NULL AND source_proforma_number <> '')
  )
);

-- One number per origin. A desktop publication that arrives with a number the
-- website already holds collides here rather than creating a second record.
CREATE UNIQUE INDEX IF NOT EXISTS inquiry_proformas_source_number
  ON public.inquiry_proformas (source_system, source_proforma_number)
  WHERE source_proforma_number <> '';

-- One open website draft per inquiry. An issued or void proforma does not
-- block a new draft.
CREATE UNIQUE INDEX IF NOT EXISTS inquiry_proformas_open_draft
  ON public.inquiry_proformas (inquiry_id)
  WHERE source_system = 'website' AND status = 'Draft';

CREATE TABLE IF NOT EXISTS public.inquiry_proforma_lines (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  proforma_id   uuid NOT NULL REFERENCES public.inquiry_proformas(id) ON DELETE CASCADE,
  group_no      integer NOT NULL CHECK (group_no BETWEEN 1 AND 50),
  line_no       integer NOT NULL CHECK (line_no BETWEEN 1 AND 500),
  glass_ref     text NOT NULL DEFAULT '' CHECK (char_length(glass_ref) <= 200),
  w1            integer NOT NULL CHECK (w1 BETWEEN 0 AND 20000),
  h1            integer NOT NULL CHECK (h1 BETWEEN 0 AND 20000),
  w2            integer NOT NULL DEFAULT 0 CHECK (w2 BETWEEN 0 AND 20000),
  h2            integer NOT NULL DEFAULT 0 CHECK (h2 BETWEEN 0 AND 20000),
  qty           integer NOT NULL DEFAULT 0 CHECK (qty BETWEEN 0 AND 100000),
  approved_rate numeric(12,2) NOT NULL DEFAULT 0 CHECK (approved_rate BETWEEN 0 AND 1000000),
  sqm           numeric(12,4) NOT NULL DEFAULT 0 CHECK (sqm >= 0),
  total_sqm     numeric(14,4) NOT NULL DEFAULT 0 CHECK (total_sqm >= 0),
  line_value    numeric(18,2) NOT NULL DEFAULT 0 CHECK (line_value >= 0),
  created_at    timestamptz NOT NULL DEFAULT now(),
  UNIQUE (proforma_id, group_no, line_no)
);

CREATE INDEX IF NOT EXISTS inquiry_proforma_lines_order
  ON public.inquiry_proforma_lines (proforma_id, group_no, line_no);

-- The save ledger. Every accepted save records the revision it produced and
-- the key the client sent, which is what makes a retry a no-op instead of a
-- second revision. It is also the cursor a desktop consumer would read.
CREATE TABLE IF NOT EXISTS public.inquiry_proforma_saves (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  proforma_id     uuid NOT NULL REFERENCES public.inquiry_proformas(id) ON DELETE CASCADE,
  revision        integer NOT NULL CHECK (revision >= 1),
  idempotency_key uuid NOT NULL UNIQUE,
  line_count      integer NOT NULL CHECK (line_count >= 0),
  total_sqm       numeric(14,4) NOT NULL CHECK (total_sqm >= 0),
  total_value     numeric(18,2) NOT NULL CHECK (total_value >= 0),
  saved_by        uuid NOT NULL DEFAULT auth.uid() REFERENCES auth.users(id) ON DELETE RESTRICT,
  saved_at        timestamptz NOT NULL DEFAULT now(),
  UNIQUE (proforma_id, revision)
);

ALTER TABLE public.inquiry_proformas ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_proforma_lines ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_proforma_saves ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON public.inquiry_proformas FROM PUBLIC, anon;
REVOKE ALL ON public.inquiry_proforma_lines FROM PUBLIC, anon;
REVOKE ALL ON public.inquiry_proforma_saves FROM PUBLIC, anon;
REVOKE INSERT, UPDATE, DELETE ON public.inquiry_proformas FROM authenticated;
REVOKE INSERT, UPDATE, DELETE ON public.inquiry_proforma_lines FROM authenticated;
REVOKE INSERT, UPDATE, DELETE ON public.inquiry_proforma_saves FROM authenticated;
GRANT SELECT ON public.inquiry_proformas TO authenticated;
GRANT SELECT ON public.inquiry_proforma_lines TO authenticated;
GRANT SELECT ON public.inquiry_proforma_saves TO authenticated;
REVOKE ALL ON SEQUENCE public.inquiry_proforma_number_seq FROM PUBLIC, anon, authenticated;

-- Read access follows the inbox: an enabled administrator or auditor, held in
-- the database and checked by the policy, never by the page.
DROP POLICY IF EXISTS "Operators read proformas" ON public.inquiry_proformas;
CREATE POLICY "Operators read proformas" ON public.inquiry_proformas
  FOR SELECT TO authenticated
  USING (public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor']));

DROP POLICY IF EXISTS "Operators read proforma lines" ON public.inquiry_proforma_lines;
CREATE POLICY "Operators read proforma lines" ON public.inquiry_proforma_lines
  FOR SELECT TO authenticated
  USING (EXISTS (SELECT 1 FROM public.inquiry_proformas p WHERE p.id = proforma_id));

DROP POLICY IF EXISTS "Operators read proforma saves" ON public.inquiry_proforma_saves;
CREATE POLICY "Operators read proforma saves" ON public.inquiry_proforma_saves
  FOR SELECT TO authenticated
  USING (EXISTS (SELECT 1 FROM public.inquiry_proformas p WHERE p.id = proforma_id));

COMMIT;
