-- Phase C7. Customer access to their own proforma invoice.
--
-- What this file does:
--   1. Adds public.inquiry_proforma_shares, one row per proforma, naming the
--      customer account the document was shared with and the revision that was
--      shared.
--   2. Lets that customer read exactly that proforma and its lines, and nothing
--      else: no drafts, no other customer's document, no unshared document, and
--      no access to a revision that was never shared.
--   3. Adds share_inquiry_proforma and revoke_inquiry_proforma_share, the two
--      operations an administrator can perform.
--
-- Apply after phase15, phase15-write, phaseC1 and phaseC5. It reads
-- public.inquiries.web_submission_id, which C1 adds.
--
-- What it deliberately does not do: it does not publish a proforma on a public
-- page, does not list customers, does not email anything, and does not invent a
-- customer account. If the inquiry is not linked to a submission, sharing is
-- refused with a message that says so.
--
-- Idempotent: safe to apply twice.

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. The share ledger.
--
-- One row per proforma: a proforma belongs to one inquiry, an inquiry belongs to
-- one customer, so there is nothing to fan out. Replacing the row is how a later
-- revision is shared, and the row keeps the revision it granted, which is what
-- stops a customer from seeing a revision nobody sent them.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.inquiry_proforma_shares (
  proforma_id      uuid PRIMARY KEY REFERENCES public.inquiry_proformas(id) ON DELETE CASCADE,
  customer_user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  revision         integer NOT NULL CHECK (revision >= 1),
  shared_by        uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
  shared_at        timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS inquiry_proforma_shares_customer
  ON public.inquiry_proforma_shares (customer_user_id);

ALTER TABLE public.inquiry_proforma_shares ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON public.inquiry_proforma_shares FROM PUBLIC, anon, authenticated;
GRANT SELECT ON public.inquiry_proforma_shares TO authenticated;

DROP POLICY IF EXISTS "Owners and operators read proforma shares" ON public.inquiry_proforma_shares;
CREATE POLICY "Owners and operators read proforma shares" ON public.inquiry_proforma_shares
  FOR SELECT TO authenticated
  USING (
    customer_user_id = auth.uid()
    OR public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor'])
  );

-- ---------------------------------------------------------------------------
-- 2. What a customer may read.
--
-- A proforma is readable by its customer only when all of these hold:
--   * a share row names that customer,
--   * the share was granted for the revision the proforma is on now, so saving a
--     newer revision withdraws the customer's sight of it until it is shared
--     again, and
--   * the document is no longer a Draft.
--
-- The third condition is why sharing moves Draft to Sent. The operation that
-- makes the document available to the customer is the operation that marks it
-- sent; nothing else writes that status.
-- ---------------------------------------------------------------------------

DROP POLICY IF EXISTS "Customers read their shared proforma" ON public.inquiry_proformas;
CREATE POLICY "Customers read their shared proforma" ON public.inquiry_proformas
  FOR SELECT TO authenticated
  USING (
    status <> 'Draft'
    AND EXISTS (
      SELECT 1
        FROM public.inquiry_proforma_shares s
       WHERE s.proforma_id = inquiry_proformas.id
         AND s.customer_user_id = auth.uid()
         AND s.revision = inquiry_proformas.revision
    )
  );

-- Lines follow the proforma policy, because this predicate is itself filtered by
-- the proforma policies for the same caller: the customer sees the lines of the
-- document they were shared and no others. The policy is restated here so the
-- dependency is visible rather than implied by RLS on the subquery.
DROP POLICY IF EXISTS "Operators read proforma lines" ON public.inquiry_proforma_lines;
CREATE POLICY "Operators read proforma lines" ON public.inquiry_proforma_lines
  FOR SELECT TO authenticated
  USING (
    EXISTS (SELECT 1 FROM public.inquiry_proformas p WHERE p.id = proforma_id)
  );

-- The save ledger and the specification settings are internal working records:
-- who saved which revision, and the base rate behind each specification. They
-- stay with the operator roles even though the document itself is now visible to
-- its customer.
DROP POLICY IF EXISTS "Operators read proforma saves" ON public.inquiry_proforma_saves;
CREATE POLICY "Operators read proforma saves" ON public.inquiry_proforma_saves
  FOR SELECT TO authenticated
  USING (public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor']));

DROP POLICY IF EXISTS "Operators read proforma groups" ON public.inquiry_proforma_groups;
CREATE POLICY "Operators read proforma groups" ON public.inquiry_proforma_groups
  FOR SELECT TO authenticated
  USING (public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor']));

-- ---------------------------------------------------------------------------
-- 3. Sharing and withdrawing a share.
--
-- Both are administrator operations, checked here in the database rather than in
-- a page. The customer is resolved from the inquiry's linked submission, never
-- from a request body, so a caller cannot address a document to an arbitrary
-- account.
-- ---------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.share_inquiry_proforma(p_proforma_id uuid)
RETURNS integer
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
  v_proforma public.inquiry_proformas;
  v_customer uuid;
BEGIN
  IF auth.uid() IS NULL THEN
    RAISE EXCEPTION 'Sign in to share a proforma.' USING ERRCODE = '42501';
  END IF;
  IF NOT public.has_inquiry_role(ARRAY['inquiry_admin']) THEN
    RAISE EXCEPTION 'Sharing a proforma needs an enabled inquiry administrator role.' USING ERRCODE = '42501';
  END IF;

  SELECT * INTO v_proforma FROM public.inquiry_proformas WHERE id = p_proforma_id FOR UPDATE;
  IF v_proforma.id IS NULL THEN
    RAISE EXCEPTION 'Proforma not found.' USING ERRCODE = 'P0002';
  END IF;
  IF v_proforma.source_system <> 'website' THEN
    RAISE EXCEPTION 'Only a website proforma can be shared from the website.' USING ERRCODE = '22023';
  END IF;
  IF v_proforma.status NOT IN ('Draft', 'Sent') THEN
    RAISE EXCEPTION 'A % proforma cannot be shared.', lower(v_proforma.status) USING ERRCODE = '22023';
  END IF;
  IF v_proforma.revision < 1 THEN
    RAISE EXCEPTION 'Save the proforma before sharing it.' USING ERRCODE = '22023';
  END IF;

  SELECT s.customer_user_id
    INTO v_customer
    FROM public.inquiries i
    JOIN public.inquiry_customer_submissions s ON s.id = i.web_submission_id
   WHERE i.id = v_proforma.inquiry_id;

  IF v_customer IS NULL THEN
    RAISE EXCEPTION 'This inquiry is not linked to a customer account, so there is nobody to share it with.'
      USING ERRCODE = 'P0002';
  END IF;

  INSERT INTO public.inquiry_proforma_shares AS existing
    (proforma_id, customer_user_id, revision, shared_by, shared_at)
  VALUES
    (p_proforma_id, v_customer, v_proforma.revision, auth.uid(), now())
  ON CONFLICT (proforma_id) DO UPDATE
    SET customer_user_id = excluded.customer_user_id,
        revision         = excluded.revision,
        shared_by        = excluded.shared_by,
        shared_at        = excluded.shared_at;

  UPDATE public.inquiry_proformas
     SET status = CASE WHEN status = 'Draft' THEN 'Sent' ELSE status END,
         updated_at = now()
   WHERE id = p_proforma_id;

  RETURN v_proforma.revision;
END;
$$;

COMMENT ON FUNCTION public.share_inquiry_proforma(uuid) IS
  'Shares the current revision with the customer account that owns the inquiry, withdraws nothing, and moves Draft to Sent because the document is now in the customer account.';

CREATE OR REPLACE FUNCTION public.revoke_inquiry_proforma_share(p_proforma_id uuid)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
BEGIN
  IF auth.uid() IS NULL THEN
    RAISE EXCEPTION 'Sign in to withdraw a shared proforma.' USING ERRCODE = '42501';
  END IF;
  IF NOT public.has_inquiry_role(ARRAY['inquiry_admin']) THEN
    RAISE EXCEPTION 'Withdrawing a shared proforma needs an enabled inquiry administrator role.' USING ERRCODE = '42501';
  END IF;

  DELETE FROM public.inquiry_proforma_shares WHERE proforma_id = p_proforma_id;
END;
$$;

REVOKE ALL ON FUNCTION public.share_inquiry_proforma(uuid) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.revoke_inquiry_proforma_share(uuid) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.share_inquiry_proforma(uuid) TO authenticated;
GRANT EXECUTE ON FUNCTION public.revoke_inquiry_proforma_share(uuid) TO authenticated;

COMMIT;
