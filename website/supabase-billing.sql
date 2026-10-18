-- =====================================================================
-- PROGLASS AUTOMATION — LICENCE ORDERS (Phase 1 billing intake)
-- Project: afpbdyamedpanwijxghk
-- =====================================================================
--
-- What this installs:
--   * public.license_orders — one row per paid online order, the queue
--     the vendor's .NET License Generator works from (see
--     docs/MONETIZATION_PLAN.md: key issuance v1 is semi-manual on
--     purpose; the website records money, it never mints licence keys).
--   * public.record_license_order(...) — the ONLY write path, a
--     SECURITY DEFINER RPC in the same house pattern as
--     publish_inquiry_snapshot etc. The table itself has RLS on and no
--     policies: anon/authenticated can neither read nor write it
--     directly. The vendor reads the queue via the Supabase dashboard.
--
-- Dormant until used: creating these objects changes nothing about the
-- running site. /api/webhooks/billing additionally refuses to run
-- without BILLING_WEBHOOK_SECRET.
--
-- plan_days is whitelisted to LicensePlanCatalog.cs durations
-- (7 / 30 / 182 / 365 / 730) in BOTH the CHECK and the function —
-- an order for a term the licensor cannot issue must not record.
-- =====================================================================

CREATE TABLE IF NOT EXISTS public.license_orders (
  id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  created_at        timestamptz NOT NULL DEFAULT now(),

  email             text NOT NULL,
  edition           text,  -- editions.ts id: trial|basic|business|factory|enterprise
  plan_days         int  NOT NULL CHECK (plan_days IN (7, 30, 182, 365, 730)),
  amount            numeric,
  currency          text,

  provider          text NOT NULL DEFAULT 'manual',
  provider_event_id text UNIQUE,  -- webhook idempotency: retries collide here

  status            text NOT NULL DEFAULT 'paid'
                    CHECK (status IN ('paid', 'key_issued', 'refunded', 'cancelled')),
  issued_at         timestamptz,
  license_note      text  -- vendor-side note (e.g. key fingerprint); never the key itself
);

CREATE INDEX IF NOT EXISTS idx_license_orders_queue
  ON public.license_orders (status, created_at);

ALTER TABLE public.license_orders ENABLE ROW LEVEL SECURITY;
-- No policies. Deliberate: see header.

CREATE OR REPLACE FUNCTION public.record_license_order(
  p_email             text,
  p_edition           text,
  p_plan_days         int,
  p_amount            numeric,
  p_currency          text,
  p_provider          text,
  p_provider_event_id text
) RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_id uuid;
BEGIN
  IF p_email IS NULL OR p_email !~ '^[^@\s]+@[^@\s]+\.[^@\s]{2,}$' THEN
    RAISE EXCEPTION 'valid buyer email required';
  END IF;
  IF p_plan_days IS NULL OR p_plan_days NOT IN (7, 30, 182, 365, 730) THEN
    RAISE EXCEPTION 'plan_days must be a catalogue duration (7,30,182,365,730)';
  END IF;
  IF p_edition IS NOT NULL
     AND p_edition NOT IN ('trial','basic','business','factory','enterprise') THEN
    RAISE EXCEPTION 'unknown edition';
  END IF;

  INSERT INTO public.license_orders
    (email, edition, plan_days, amount, currency, provider, provider_event_id)
  VALUES
    (lower(trim(p_email)), p_edition, p_plan_days, p_amount,
     upper(nullif(trim(coalesce(p_currency,'')),'')),
     coalesce(nullif(trim(p_provider),''), 'manual'),
     nullif(trim(coalesce(p_provider_event_id,'')),''))
  ON CONFLICT (provider_event_id) DO NOTHING
  RETURNING id INTO v_id;

  -- Webhook retry with the same event id: return the original order.
  IF v_id IS NULL AND p_provider_event_id IS NOT NULL THEN
    SELECT id INTO v_id FROM public.license_orders
     WHERE provider_event_id = p_provider_event_id;
  END IF;

  RETURN v_id;
END;
$$;

GRANT EXECUTE ON FUNCTION public.record_license_order(text, text, int, numeric, text, text, text)
  TO anon, authenticated, service_role;

-- Vendor queue (run in dashboard):
--   SELECT id, created_at, email, edition, plan_days, amount, currency, provider
--     FROM public.license_orders WHERE status='paid' ORDER BY created_at;
-- After issuing a key in the License Generator:
--   UPDATE public.license_orders
--      SET status='key_issued', issued_at=now(), license_note='<key fingerprint>'
--    WHERE id='<order id>';
