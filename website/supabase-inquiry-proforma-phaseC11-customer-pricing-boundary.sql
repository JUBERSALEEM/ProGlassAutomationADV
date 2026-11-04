-- Phase C11: customer inquiry / internal priced PI security boundary.
--
-- Additive retirement of Phase C7 customer access. Staff PI reads/writes and the
-- existing proforma, line, save, group and share-ledger records are retained.
-- Existing share rows become internal audit records; no row or PI is deleted.
-- Apply after phases 15, C1, C5, C7 and C8, before deploying the matching app.
--
-- Customer inquiries and technical-only attachments remain unaffected. Customer
-- accounts must not read priced proformas, line rates/values, or share metadata.
-- WPF remains the authoritative staff workflow for issuing priced documents.

BEGIN;

-- Keep the historical share record, but record withdrawal rather than deleting
-- it. Existing customer grants become ineffective as soon as the policies below
-- are replaced, regardless of the stored share row/revision/status.
ALTER TABLE public.inquiry_proforma_shares
  ADD COLUMN IF NOT EXISTS revoked_at timestamptz,
  ADD COLUMN IF NOT EXISTS revoked_by uuid REFERENCES auth.users(id) ON DELETE RESTRICT;

COMMENT ON COLUMN public.inquiry_proforma_shares.revoked_at IS
  'Internal audit timestamp: when legacy customer access was withdrawn or staff revoked it.';
COMMENT ON COLUMN public.inquiry_proforma_shares.revoked_by IS
  'Internal audit identity for a staff withdrawal; null when access was withdrawn by the Phase C11 boundary migration.';

-- Retain each historical share row and record this boundary change as its
-- withdrawal; null revoked_by identifies the system migration, not a staff user.
UPDATE public.inquiry_proforma_shares
   SET revoked_at = coalesce(revoked_at, now())
 WHERE revoked_at IS NULL;

-- Reassert RLS and reduce every direct table grant to authenticated SELECT.
-- Staff mutations remain through the role-checked SECURITY DEFINER RPCs; no
-- account can bypass those functions with direct table DML.
ALTER TABLE public.inquiry_proformas ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_proforma_lines ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_proforma_saves ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_proforma_groups ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inquiry_proforma_shares ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON public.inquiry_proformas, public.inquiry_proforma_lines,
  public.inquiry_proforma_saves, public.inquiry_proforma_groups,
  public.inquiry_proforma_shares FROM PUBLIC, anon, authenticated;
GRANT SELECT ON public.inquiry_proformas, public.inquiry_proforma_lines,
  public.inquiry_proforma_saves, public.inquiry_proforma_groups,
  public.inquiry_proforma_shares TO authenticated;

DROP POLICY IF EXISTS "Customers read their shared proforma" ON public.inquiry_proformas;
DROP POLICY IF EXISTS "Operators read proformas" ON public.inquiry_proformas;
CREATE POLICY "Operators read proformas" ON public.inquiry_proformas
  FOR SELECT TO authenticated
  USING (public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor']));

DROP POLICY IF EXISTS "Operators read proforma lines" ON public.inquiry_proforma_lines;
CREATE POLICY "Operators read proforma lines" ON public.inquiry_proforma_lines
  FOR SELECT TO authenticated
  USING (public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor']));

DROP POLICY IF EXISTS "Owners and operators read proforma shares" ON public.inquiry_proforma_shares;
DROP POLICY IF EXISTS "Operators read proforma shares" ON public.inquiry_proforma_shares;
CREATE POLICY "Operators read proforma shares" ON public.inquiry_proforma_shares
  FOR SELECT TO authenticated
  USING (public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor']));

DROP POLICY IF EXISTS "Operators read proforma saves" ON public.inquiry_proforma_saves;
CREATE POLICY "Operators read proforma saves" ON public.inquiry_proforma_saves
  FOR SELECT TO authenticated
  USING (public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor']));

DROP POLICY IF EXISTS "Operators read proforma groups" ON public.inquiry_proforma_groups;
CREATE POLICY "Operators read proforma groups" ON public.inquiry_proforma_groups
  FOR SELECT TO authenticated
  USING (public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor']));

-- Fail closed in the database even if an old API or direct RPC caller remains.
-- Preserve authentication and administrator checks so this does not become an
-- endpoint oracle. Staff can still withdraw an old share; the ledger is retained.
CREATE OR REPLACE FUNCTION public.share_inquiry_proforma(p_proforma_id uuid)
RETURNS integer
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
BEGIN
  IF auth.uid() IS NULL THEN
    RAISE EXCEPTION 'Sign in to continue.' USING ERRCODE = '42501';
  END IF;
  IF NOT public.has_inquiry_role(ARRAY['inquiry_admin']) THEN
    RAISE EXCEPTION 'An enabled inquiry administrator role is required.' USING ERRCODE = '42501';
  END IF;

  RAISE EXCEPTION 'Customer access to priced proformas is disabled. Continue PI preparation and release in the authorized WPF workflow.'
    USING ERRCODE = '0A000';
END;
$$;

COMMENT ON FUNCTION public.share_inquiry_proforma(uuid) IS
  'Retired by Phase C11: priced customer PI sharing is disabled. Authorized staff PI preparation remains available; WPF is the authoritative release workflow.';

CREATE OR REPLACE FUNCTION public.revoke_inquiry_proforma_share(p_proforma_id uuid)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
BEGIN
  IF auth.uid() IS NULL THEN
    RAISE EXCEPTION 'Sign in to withdraw a legacy customer share.' USING ERRCODE = '42501';
  END IF;
  IF NOT public.has_inquiry_role(ARRAY['inquiry_admin']) THEN
    RAISE EXCEPTION 'Withdrawing a legacy customer share needs an enabled inquiry administrator role.' USING ERRCODE = '42501';
  END IF;

  UPDATE public.inquiry_proforma_shares
     SET revoked_at = coalesce(revoked_at, now()),
         revoked_by = coalesce(revoked_by, auth.uid())
   WHERE proforma_id = p_proforma_id
     AND revoked_at IS NULL;
END;
$$;

COMMENT ON FUNCTION public.revoke_inquiry_proforma_share(uuid) IS
  'Records an internal withdrawal of a legacy customer share without deleting its audit row.';

REVOKE ALL ON FUNCTION public.share_inquiry_proforma(uuid) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.revoke_inquiry_proforma_share(uuid) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.share_inquiry_proforma(uuid) TO authenticated;
GRANT EXECUTE ON FUNCTION public.revoke_inquiry_proforma_share(uuid) TO authenticated;

COMMIT;
