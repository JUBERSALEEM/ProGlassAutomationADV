-- Phase 15, part 2. The one write path for a website proforma.
--
-- Called through /api/inquiries/[id]/proforma after the API layer has already
-- verified the caller's token. It verifies the role again, because the database
-- must not depend on a page or a route for authorization.
--
-- Money is derived here, in numeric arithmetic, from the panel dimensions and
-- the approved rate. The client proposes lines; it does not propose values.

CREATE OR REPLACE FUNCTION public.save_inquiry_proforma_v1(
  p_inquiry_id        uuid,
  p_idempotency_key   text,
  p_expected_revision integer,
  p_currency          text,
  p_lines             jsonb
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
    v_number := 'PI-WEB-' || to_char(now(), 'YYYY') || '-' ||
                lpad(nextval('public.inquiry_proforma_number_seq')::text, 6, '0');
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

  INSERT INTO public.inquiry_proforma_saves
    (proforma_id, revision, idempotency_key, line_count, total_sqm, total_value, saved_by)
  VALUES
    (v_id, v_proforma.revision, p_idempotency_key::uuid, v_count, v_sum_sqm, v_sum_value, auth.uid());

  RETURN v_id;
END;
$$;

REVOKE ALL ON FUNCTION public.save_inquiry_proforma_v1(uuid, text, integer, text, jsonb) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.save_inquiry_proforma_v1(uuid, text, integer, text, jsonb) TO authenticated;
