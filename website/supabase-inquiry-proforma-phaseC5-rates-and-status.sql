-- Phase C5. Persisted specification pricing settings, and the approved status
-- vocabulary.
--
-- What this file does:
--   1. Adds public.inquiry_proforma_groups, which stores a specification's BASE
--      rate and SURCHARGE percentage against the revision they were saved with.
--   2. Moves the proforma status vocabulary to the approved words: Draft, Sent,
--      Approved, Issued and Cancelled. Legacy 'Void' rows become 'Cancelled'.
--   3. Allocates numbers the way the desktop allocator does: the first free
--      number above the highest one used under the prefix, compared
--      case-insensitively, plus the case-insensitive uniqueness that rule needs.
--   4. Adds save_inquiry_proforma_v2, which writes the lines and the settings in
--      one transaction and one revision. v1 is left in place and unchanged, so a
--      deployment that has not applied this file keeps working.
--
-- What it deliberately does not do: no Step 6 charges, no discount, no VAT, no
-- issuance action, no PDF, no publication, no numbering change. A status is only
-- written by a function that performs the corresponding operation, and this file
-- adds no such operation.
--
-- Idempotent: safe to apply twice. Nothing is dropped except the status check this
-- file replaces, and no row is deleted.

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Specification pricing settings, pinned to the revision that saved them.
--
-- The website stores a rate per line, because that is what the write function
-- prices from. The specification's BASE rate and SURCHARGE percentage are the
-- inputs a person works with, and until now they vanished on reload. They are
-- stored here per revision, so an older revision keeps the figures it was saved
-- with and a new revision cannot silently inherit a different one.
--
-- The key is (proforma_id, revision, group_no). Two proformas never share a row,
-- and a deleted proforma takes its rows with it, matching how lines and saves
-- already behave.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.inquiry_proforma_groups (
  proforma_id        uuid    NOT NULL REFERENCES public.inquiry_proformas(id) ON DELETE CASCADE,
  revision           integer NOT NULL CHECK (revision >= 1),
  group_no           integer NOT NULL CHECK (group_no BETWEEN 1 AND 50),
  base_rate          numeric(18,2) NOT NULL CHECK (base_rate >= 0 AND base_rate <= 1000000),
  surcharge_percent  numeric(9,2)  NOT NULL CHECK (surcharge_percent >= 0 AND surcharge_percent <= 1000),
  created_at         timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (proforma_id, revision, group_no)
);

COMMENT ON TABLE public.inquiry_proforma_groups IS
  'BASE rate and SURCHARGE percentage per specification, pinned to the proforma revision that saved them. Written only by save_inquiry_proforma_v2.';
COMMENT ON COLUMN public.inquiry_proforma_groups.base_rate IS
  'Specification base rate per square metre, in the proforma currency.';
COMMENT ON COLUMN public.inquiry_proforma_groups.surcharge_percent IS
  'Specification surcharge percentage applied at or above the large-panel threshold.';

ALTER TABLE public.inquiry_proforma_groups ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON public.inquiry_proforma_groups FROM PUBLIC, anon;
REVOKE INSERT, UPDATE, DELETE ON public.inquiry_proforma_groups FROM authenticated;
GRANT SELECT ON public.inquiry_proforma_groups TO authenticated;

DROP POLICY IF EXISTS "Operators read proforma groups" ON public.inquiry_proforma_groups;
CREATE POLICY "Operators read proforma groups" ON public.inquiry_proforma_groups
  FOR SELECT TO authenticated
  USING (EXISTS (SELECT 1 FROM public.inquiry_proformas p WHERE p.id = proforma_id));

-- ---------------------------------------------------------------------------
-- 2. Approved status vocabulary.
--
-- Draft     the PI is being prepared
-- Sent      it has been sent to the customer
-- Approved  the customer accepted it through the existing business process
-- Issued    reserved for the verified issuance workflow
-- Cancelled replaces 'Void', which is not a business status
--
-- No transition is added here, and nothing in this file marks a document Sent,
-- Approved or Issued. Those words exist so the record can hold them once the
-- operations that produce them exist.
--
-- Legacy rows: a 'Void' row becomes 'Cancelled'. Nothing is deleted. The update is
-- recorded here rather than left to a manual step, and it is the only data change
-- in the file.
-- ---------------------------------------------------------------------------

ALTER TABLE public.inquiry_proformas
  DROP CONSTRAINT IF EXISTS inquiry_proformas_status_check;

UPDATE public.inquiry_proformas
   SET status = 'Cancelled'
 WHERE status = 'Void';

ALTER TABLE public.inquiry_proformas
  ADD CONSTRAINT inquiry_proformas_status_check
  CHECK (status IN ('Draft', 'Sent', 'Approved', 'Issued', 'Cancelled'));

-- ---------------------------------------------------------------------------
-- 3. Proforma number allocation, following the desktop allocator.
--
-- The desktop allocator (Services/ProformaInvoice/ProformaInvoiceRepository,
-- SuggestNextIdAsync) keeps no counter. It reads the number out of the id in
-- front of it, walks upwards, and returns the first candidate that is not
-- already taken, comparing ids case-insensitively. The prefix is everything
-- before the trailing digit run and the padding is as wide as that run, so
-- "PI-2026-001" is followed by "PI-2026-002" and "INVOICE-42" by "INVOICE-43".
--
-- This function applies that rule to the website's own prefix. The website
-- prefix is "PI-WEB-YYYY-" and its width is fixed at six digits by the Phase C1
-- column check, so the shape stays "PI-WEB-2026-000123".
--
-- Two consequences that matter here:
--
--   * A desktop row that happens to carry a "PI-WEB-" number is skipped rather
--     than colliding, exactly as a taken id is skipped on the desktop. That is
--     how records from the two origins coexist without duplicates.
--   * The advisory lock serialises allocation per prefix, so two operators
--     saving at the same moment cannot both be handed the same candidate.
--
-- One deliberate difference: the existing sequence is kept as a floor. The
-- desktop reuses the number of a deleted row because it has nowhere else to
-- remember it; a customer-facing document number that has already been printed
-- must not quietly reappear on a different inquiry, so a number the website
-- handed out is never handed out again.
-- ---------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.inquiry_next_proforma_number(
  p_prefix    text,
  p_pad_width integer DEFAULT 6
)
RETURNS text
LANGUAGE plpgsql
VOLATILE
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_highest   bigint  := 0;
  v_seq       bigint  := 0;
  v_current   bigint;
  v_width     integer;
  v_candidate text;
  i           integer;
BEGIN
  IF p_prefix IS NULL OR p_prefix = '' THEN
    RAISE EXCEPTION 'A proforma number prefix is required.' USING ERRCODE = '22023';
  END IF;

  v_width := coalesce(p_pad_width, 6);
  IF v_width < 1 OR v_width > 9 THEN
    RAISE EXCEPTION 'A proforma number width must be between 1 and 9, not %.', p_pad_width
      USING ERRCODE = '22023';
  END IF;

  -- One allocator at a time per prefix.
  PERFORM pg_advisory_xact_lock(hashtext(upper(p_prefix)));

  -- The highest number already used under this prefix. A digit run longer than
  -- nine digits contributes nothing instead of overflowing, mirroring the
  -- desktop's TryParse degradation (AUDIT V-1).
  SELECT coalesce(max(CASE WHEN length(x.suffix) <= 9 THEN x.suffix::bigint ELSE 0 END), 0)
    INTO v_highest
    FROM (
      SELECT (regexp_match(upper(proforma_number), '([0-9]+)$'))[1] AS suffix
        FROM public.inquiry_proformas
       WHERE left(upper(proforma_number), length(p_prefix)) = upper(p_prefix)
    ) AS x
   WHERE x.suffix IS NOT NULL;

  -- Numbers already handed out by the previous nextval allocator stay spent.
  SELECT CASE WHEN s.is_called THEN s.last_value ELSE 0 END
    INTO v_seq
    FROM public.inquiry_proforma_number_seq AS s;

  v_current := greatest(v_highest, coalesce(v_seq, 0));

  FOR i IN 1..100000 LOOP
    v_candidate := p_prefix || lpad((v_current + i)::text, v_width, '0');

    IF NOT EXISTS (
      SELECT 1
        FROM public.inquiry_proformas
       WHERE upper(proforma_number) = upper(v_candidate)
    ) THEN
      RETURN v_candidate;
    END IF;
  END LOOP;

  -- The desktop falls back to a GUID suffix at this point. The website column
  -- check admits only the six digit website shape, so a GUID could not be
  -- stored; failing loudly is the honest outcome.
  RAISE EXCEPTION 'No free proforma number was found under prefix % after 100000 attempts.', p_prefix
    USING ERRCODE = '55000';
END;
$$;

COMMENT ON FUNCTION public.inquiry_next_proforma_number(text, integer) IS
  'The first free number above the highest one used under the prefix, compared case-insensitively. Mirrors the desktop SuggestNextIdAsync rule for the website PI-WEB-YYYY- prefix.';

REVOKE ALL ON FUNCTION public.inquiry_next_proforma_number(text, integer) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.inquiry_next_proforma_number(text, integer) TO authenticated;

-- The desktop compares ids case-insensitively, so two numbers that differ only
-- by case are the same number. The Phase 15 UNIQUE constraint is case sensitive
-- and would let both exist. Refuse to add this index over data that already
-- contains such a pair, rather than failing halfway through the file.
DO $$
DECLARE v_duplicates text;
BEGIN
  SELECT string_agg(d.duplicated, ', ')
    INTO v_duplicates
    FROM (
      SELECT upper(proforma_number) AS duplicated
        FROM public.inquiry_proformas
       GROUP BY upper(proforma_number)
      HAVING count(*) > 1
    ) AS d;

  IF v_duplicates IS NOT NULL THEN
    RAISE EXCEPTION 'Proforma numbers that differ only by case already exist: %. Resolve them, then apply this file again.', v_duplicates;
  END IF;
END
$$;

CREATE UNIQUE INDEX IF NOT EXISTS inquiry_proformas_number_ci
  ON public.inquiry_proformas (upper(proforma_number));

COMMENT ON INDEX public.inquiry_proformas_number_ci IS
  'Proforma numbers are unique regardless of case, matching the desktop allocator case-insensitive comparison.';

-- ---------------------------------------------------------------------------
-- 4. One save path that writes lines and specification settings together.
--
-- Identical to v1 in every check, every computation and every stored money figure.
-- The only additions are the p_groups argument and the settings rows it writes for
-- the revision this call produced. v1 stays in place untouched, so an older client
-- keeps working and the two can coexist during a rollout.
-- ---------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.save_inquiry_proforma_v2(
  p_inquiry_id        uuid,
  p_idempotency_key   text,
  p_expected_revision integer,
  p_currency          text,
  p_lines             jsonb,
  p_groups            jsonb
) RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
  v_inquiry    public.inquiries%ROWTYPE;
  v_proforma   public.inquiry_proformas%ROWTYPE;
  v_save       public.inquiry_proforma_saves%ROWTYPE;
  v_line       record;
  v_id         uuid;
  v_number     text;
  v_currency   text := upper(coalesce(p_currency, ''));
  v_sqm        numeric(12,4);
  v_total_sqm  numeric(14,4);
  v_line_value numeric(18,2);
  v_sum_sqm    numeric(14,4) := 0;
  v_sum_value  numeric(18,2) := 0;
  v_count      integer := 0;
  v_revision   integer;
BEGIN
  IF NOT public.has_inquiry_role(ARRAY['inquiry_admin']) THEN
    RAISE EXCEPTION 'An enabled inquiry administrator role is required to save a proforma.'
      USING ERRCODE = '42501';
  END IF;

  IF p_inquiry_id IS NULL THEN
    RAISE EXCEPTION 'An inquiry is required.' USING ERRCODE = '22023';
  END IF;
  IF p_idempotency_key IS NULL OR p_idempotency_key !~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN
    RAISE EXCEPTION 'A canonical idempotency key is required.' USING ERRCODE = '22023';
  END IF;
  IF p_expected_revision IS NULL OR p_expected_revision < 0 THEN
    RAISE EXCEPTION 'An expected revision of zero or more is required.' USING ERRCODE = '22023';
  END IF;
  IF v_currency !~ '^[A-Z]{3}$' THEN
    RAISE EXCEPTION 'A three letter currency code is required.' USING ERRCODE = '22023';
  END IF;
  IF p_lines IS NULL OR jsonb_typeof(p_lines) <> 'array' THEN
    RAISE EXCEPTION 'Proforma lines must be an array.' USING ERRCODE = '22023';
  END IF;
  IF jsonb_array_length(p_lines) < 1 OR jsonb_array_length(p_lines) > 200 THEN
    RAISE EXCEPTION 'A proforma needs between 1 and 200 lines.' USING ERRCODE = '22023';
  END IF;

  SELECT * INTO v_inquiry FROM public.inquiries WHERE id = p_inquiry_id;
  IF v_inquiry.id IS NULL THEN
    RAISE EXCEPTION 'Inquiry not found.' USING ERRCODE = 'P0002';
  END IF;
  IF v_inquiry.availability <> 'available' THEN
    RAISE EXCEPTION 'This inquiry is no longer available in the publication.'
      USING ERRCODE = '22023';
  END IF;

  -- A retry of an accepted save returns the record it already produced.
  SELECT * INTO v_save FROM public.inquiry_proforma_saves
   WHERE idempotency_key = p_idempotency_key::uuid;
  IF v_save.id IS NOT NULL THEN
    SELECT * INTO v_proforma FROM public.inquiry_proformas WHERE id = v_save.proforma_id;
    IF v_proforma.inquiry_id IS DISTINCT FROM p_inquiry_id THEN
      RAISE EXCEPTION 'That idempotency key belongs to another inquiry.' USING ERRCODE = '22023';
    END IF;
    RETURN v_proforma.id;
  END IF;

  SELECT * INTO v_proforma FROM public.inquiry_proformas
   WHERE inquiry_id = p_inquiry_id AND source_system = 'website' AND status = 'Draft'
   FOR UPDATE;

  IF v_proforma.id IS NULL THEN
    IF p_expected_revision <> 0 THEN
      RAISE EXCEPTION 'This inquiry has no proforma draft at revision %.', p_expected_revision
        USING ERRCODE = '40001';
    END IF;
    -- Desktop-style allocation: the first free number under this year's prefix.
    v_number := public.inquiry_next_proforma_number(
                  'PI-WEB-' || to_char(now(), 'YYYY') || '-', 6);
    INSERT INTO public.inquiry_proformas
      (inquiry_id, proforma_number, source_system, inquiry_number, customer_name, currency, created_by)
    VALUES
      (p_inquiry_id, v_number, 'website', v_inquiry.inquiry_number, v_inquiry.customer_name, v_currency, auth.uid())
    RETURNING id INTO v_id;
    v_proforma.id := v_id;
    v_proforma.revision := 0;
  ELSE
    IF v_proforma.revision <> p_expected_revision THEN
      RAISE EXCEPTION 'The proforma changed since it was loaded.'
        USING ERRCODE = '40001';
    END IF;
    v_id := v_proforma.id;
    UPDATE public.inquiry_proformas
       SET currency = v_currency, revision = revision + 1, updated_at = now()
     WHERE id = v_id;
    DELETE FROM public.inquiry_proforma_lines WHERE proforma_id = v_id;
  END IF;

  FOR v_line IN
    SELECT * FROM jsonb_to_recordset(p_lines) AS x(
      group_no integer, line_no integer, glass_ref text,
      w1 integer, h1 integer, w2 integer, h2 integer, qty integer, rate numeric
    )
    ORDER BY group_no, line_no
  LOOP
    IF v_line.group_no IS NULL OR v_line.group_no NOT BETWEEN 1 AND 50 THEN
      RAISE EXCEPTION 'A specification group number between 1 and 50 is required.' USING ERRCODE = '22023';
    END IF;
    IF v_line.line_no IS NULL OR v_line.line_no NOT BETWEEN 1 AND 500 THEN
      RAISE EXCEPTION 'A line number between 1 and 500 is required.' USING ERRCODE = '22023';
    END IF;
    IF coalesce(v_line.w1, 0) NOT BETWEEN 1 AND 20000
       OR coalesce(v_line.h1, 0) NOT BETWEEN 1 AND 20000
       OR coalesce(v_line.w2, 0) NOT BETWEEN 0 AND 20000
       OR coalesce(v_line.h2, 0) NOT BETWEEN 0 AND 20000 THEN
      RAISE EXCEPTION 'Line % of specification % has a panel size outside 1 to 20000 mm.', v_line.line_no, v_line.group_no
        USING ERRCODE = '22023';
    END IF;
    IF coalesce(v_line.qty, 0) NOT BETWEEN 1 AND 100000 THEN
      RAISE EXCEPTION 'Line % of specification % needs a quantity of at least one.', v_line.line_no, v_line.group_no
        USING ERRCODE = '22023';
    END IF;
    IF coalesce(v_line.rate, 0) < 0 OR coalesce(v_line.rate, 0) > 1000000 THEN
      RAISE EXCEPTION 'Line % of specification % has an approved rate outside the accepted range.', v_line.line_no, v_line.group_no
        USING ERRCODE = '22023';
    END IF;

    v_sqm := round(greatest((v_line.w1::numeric / 1000) * (v_line.h1::numeric / 1000), 0.5), 4);
    v_total_sqm := round(v_sqm * v_line.qty, 4);
    v_line_value := round(v_total_sqm * coalesce(v_line.rate, 0), 2);

    INSERT INTO public.inquiry_proforma_lines
      (proforma_id, group_no, line_no, glass_ref, w1, h1, w2, h2, qty, approved_rate, sqm, total_sqm, line_value)
    VALUES
      (v_id, v_line.group_no, v_line.line_no, left(coalesce(v_line.glass_ref, ''), 200),
       v_line.w1, v_line.h1, coalesce(v_line.w2, 0), coalesce(v_line.h2, 0),
       v_line.qty, round(coalesce(v_line.rate, 0), 2), v_sqm, v_total_sqm, v_line_value);

    v_count := v_count + 1;
    v_sum_sqm := v_sum_sqm + v_total_sqm;
    v_sum_value := v_sum_value + v_line_value;
  END LOOP;

  UPDATE public.inquiry_proformas
     SET line_count = v_count,
         total_sqm = v_sum_sqm,
         total_value = v_sum_value,
         updated_at = now()
   WHERE id = v_id
   RETURNING revision INTO v_proforma.revision;

  v_revision := v_proforma.revision;

  INSERT INTO public.inquiry_proforma_saves
    (proforma_id, revision, idempotency_key, line_count, total_sqm, total_value, saved_by)
  VALUES
    (v_id, v_proforma.revision, p_idempotency_key::uuid, v_count, v_sum_sqm, v_sum_value, auth.uid());

  -- Specification pricing settings for exactly this revision.
  --
  -- A row is written only when the caller supplies settings, and it is keyed by the
  -- revision it belongs to. An older revision therefore keeps the figures it was
  -- saved with, and a new revision never inherits them silently: the caller either
  -- sends the settings it is working with or sends none.
  IF p_groups IS NOT NULL AND jsonb_typeof(p_groups) = 'array' THEN
    DELETE FROM public.inquiry_proforma_groups
     WHERE proforma_id = v_id AND revision = v_revision;

    INSERT INTO public.inquiry_proforma_groups
      (proforma_id, revision, group_no, base_rate, surcharge_percent)
    SELECT v_id, v_revision, g.group_no, g.base_rate, g.surcharge_percent
      FROM jsonb_to_recordset(p_groups) AS g(
             group_no integer, base_rate numeric, surcharge_percent numeric)
     WHERE g.group_no BETWEEN 1 AND 50;
  END IF;

  RETURN v_id;
END;
$$;
REVOKE ALL ON FUNCTION public.save_inquiry_proforma_v2(uuid, text, integer, text, jsonb, jsonb) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.save_inquiry_proforma_v2(uuid, text, integer, text, jsonb, jsonb) TO authenticated;

COMMIT;
