-- =====================================================================
-- PROGLASS AUTOMATION — CONFIRMED PI → PRODUCTION JOB ORDER TRACKING
--
-- Run after website/supabase-dashboard.sql in the Supabase SQL Editor.
-- This creates a website-side production release ledger. It does not write
-- back into Excel Sync's public.records table or the desktop SQLite database.
--
-- Source gate: only active public.records rows with status = Confirmed may
-- be released. The release stores an immutable source snapshot and the
-- operator-reviewed stage plan so later Excel edits cannot rewrite history.
-- =====================================================================

CREATE SEQUENCE IF NOT EXISTS public.production_job_order_number_seq;

CREATE TABLE IF NOT EXISTS public.production_job_orders (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  job_order_number      text NOT NULL UNIQUE,
  source_pi_number      text NOT NULL,
  source_file           text NOT NULL DEFAULT '',
  source_record_ids     text[] NOT NULL,
  source_snapshot       jsonb NOT NULL,
  snapshot_fingerprint  text NOT NULL,
  company               text NOT NULL DEFAULT '',
  customer_ref          text NOT NULL DEFAULT '',
  salesman              text NOT NULL DEFAULT '',
  type_of_work          text NOT NULL DEFAULT '',
  ordered_qty           numeric(18,3) NOT NULL DEFAULT 0 CHECK (ordered_qty >= 0),
  ordered_sqm           numeric(18,3) NOT NULL DEFAULT 0 CHECK (ordered_sqm >= 0),
  route_source          text NOT NULL DEFAULT 'records_summary'
                        CHECK (route_source = 'records_summary'),
  status                text NOT NULL DEFAULT 'Released'
                        CHECK (status IN ('Released', 'In Production', 'Completed')),
  route_confirmed_at    timestamptz NOT NULL DEFAULT now(),
  route_confirmed_by    uuid,
  released_at           timestamptz NOT NULL DEFAULT now(),
  released_by           uuid,
  created_at            timestamptz NOT NULL DEFAULT now(),
  updated_at            timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT production_job_orders_source_unique UNIQUE (source_pi_number, source_file)
);

CREATE INDEX IF NOT EXISTS idx_production_job_orders_status_updated
  ON public.production_job_orders (status, updated_at DESC);
CREATE INDEX IF NOT EXISTS idx_production_job_orders_source_pi
  ON public.production_job_orders (source_pi_number);

CREATE TABLE IF NOT EXISTS public.production_job_order_operations (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  job_order_id    uuid NOT NULL REFERENCES public.production_job_orders(id) ON DELETE RESTRICT,
  stage_code      text NOT NULL CHECK (stage_code IN (
                    'cutting', 'holes_cutouts', 'polish', 'edge_deletion',
                    'mitring', 'tempering', 'pairing', 'doubling_lamination'
                  )),
  stage_label     text NOT NULL,
  stage_order     smallint NOT NULL CHECK (stage_order BETWEEN 1 AND 8),
  quantity_unit   text NOT NULL CHECK (quantity_unit IN ('panes', 'pieces', 'units', 'linear metres')),
  planned_qty     numeric(18,3) NOT NULL CHECK (planned_qty > 0),
  completed_qty   numeric(18,3) NOT NULL DEFAULT 0
                  CHECK (completed_qty >= 0 AND completed_qty <= planned_qty),
  status          text NOT NULL DEFAULT 'Pending'
                  CHECK (status IN ('Pending', 'In Progress', 'Completed')),
  started_at      timestamptz,
  completed_at    timestamptz,
  updated_by      uuid,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT production_job_order_operations_whole_count CHECK (
    quantity_unit = 'linear metres'
    OR (planned_qty = trunc(planned_qty) AND completed_qty = trunc(completed_qty))
  ),
  CONSTRAINT production_job_order_operations_stage_unique UNIQUE (job_order_id, stage_code)
);

CREATE INDEX IF NOT EXISTS idx_production_job_order_operations_job_order
  ON public.production_job_order_operations (job_order_id, stage_order);
CREATE INDEX IF NOT EXISTS idx_production_job_order_operations_stage_status
  ON public.production_job_order_operations (stage_code, status);

CREATE TABLE IF NOT EXISTS public.production_job_order_events (
  id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  job_order_id    uuid NOT NULL REFERENCES public.production_job_orders(id) ON DELETE RESTRICT,
  operation_id   uuid REFERENCES public.production_job_order_operations(id) ON DELETE RESTRICT,
  event_type      text NOT NULL CHECK (event_type IN ('Released', 'ProgressUpdated')),
  stage_code      text,
  previous_qty    numeric(18,3),
  new_qty         numeric(18,3),
  actor_id        uuid,
  details         jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_production_job_order_events_job_created
  ON public.production_job_order_events (job_order_id, created_at DESC);

-- Authenticated operators may read the queue and progress. Writes are only
-- through the two validated RPC functions below; anon/public has no access.
ALTER TABLE public.production_job_orders ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.production_job_order_operations ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.production_job_order_events ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "Authenticated operators read production job orders" ON public.production_job_orders;
CREATE POLICY "Authenticated operators read production job orders"
  ON public.production_job_orders FOR SELECT TO authenticated USING (true);

DROP POLICY IF EXISTS "Authenticated operators read production operations" ON public.production_job_order_operations;
CREATE POLICY "Authenticated operators read production operations"
  ON public.production_job_order_operations FOR SELECT TO authenticated USING (true);

REVOKE ALL ON TABLE public.production_job_orders FROM PUBLIC, anon;
REVOKE ALL ON TABLE public.production_job_order_operations FROM PUBLIC, anon;
REVOKE ALL ON TABLE public.production_job_order_events FROM PUBLIC, anon;
REVOKE INSERT, UPDATE, DELETE ON TABLE public.production_job_orders FROM authenticated;
REVOKE INSERT, UPDATE, DELETE ON TABLE public.production_job_order_operations FROM authenticated;
REVOKE ALL ON TABLE public.production_job_order_events FROM authenticated;
GRANT SELECT ON TABLE public.production_job_orders TO authenticated;
GRANT SELECT ON TABLE public.production_job_order_operations TO authenticated;
GRANT ALL ON TABLE public.production_job_orders TO service_role;
GRANT ALL ON TABLE public.production_job_order_operations TO service_role;
GRANT ALL ON TABLE public.production_job_order_events TO service_role;
GRANT USAGE, SELECT ON SEQUENCE public.production_job_order_number_seq TO service_role;
GRANT USAGE, SELECT ON SEQUENCE public.production_job_order_events_id_seq TO service_role;

CREATE OR REPLACE FUNCTION public.release_confirmed_pi_to_production(
  p_pi_number text,
  p_source_file text,
  p_expected_record_ids text[],
  p_operations jsonb,
  p_route_confirmed boolean
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
  v_actor uuid := auth.uid();
  v_pi text := upper(btrim(COALESCE(p_pi_number, '')));
  v_source text := btrim(COALESCE(p_source_file, ''));
  v_ids text[];
  v_expected_ids text[];
  v_snapshot jsonb;
  v_fingerprint text;
  v_count integer;
  v_qty numeric(18,3);
  v_sqm numeric(18,3);
  v_company text;
  v_customer_ref text;
  v_salesman text;
  v_type_of_work text;
  v_job_id uuid;
  v_job_number text;
  v_job_status text;
  v_operation jsonb;
  v_stage text;
  v_label text;
  v_order smallint;
  v_unit text;
  v_planned numeric(18,3);
  v_operation_count integer := 0;
  v_has_cutting boolean := false;
  v_operations jsonb;
BEGIN
  IF v_actor IS NULL THEN
    RAISE EXCEPTION 'Authenticated operator required.' USING ERRCODE = '42501';
  END IF;
  IF v_pi = '' THEN
    RAISE EXCEPTION 'Proforma number is required.' USING ERRCODE = '22023';
  END IF;
  IF p_route_confirmed IS DISTINCT FROM true THEN
    RAISE EXCEPTION 'The operator must review and confirm the production route before release.' USING ERRCODE = '22023';
  END IF;
  IF jsonb_typeof(p_operations) IS DISTINCT FROM 'array'
     OR jsonb_array_length(p_operations) < 1
     OR jsonb_array_length(p_operations) > 8 THEN
    RAISE EXCEPTION 'A production route must contain between one and eight stages.' USING ERRCODE = '22023';
  END IF;

  -- Serialize simultaneous releases for the same PI/workbook so a racing
  -- retry observes the first transaction's committed Job Order.
  PERFORM pg_advisory_xact_lock(hashtext(v_pi), hashtext(v_source));

  -- Idempotent retry: a PI/workbook pair can have only one website release.
  SELECT id, job_order_number, status
    INTO v_job_id, v_job_number, v_job_status
  FROM public.production_job_orders
  WHERE source_pi_number = v_pi AND source_file = v_source;
  IF FOUND THEN
    SELECT COALESCE(jsonb_agg(to_jsonb(o) ORDER BY o.stage_order), '[]'::jsonb)
      INTO v_operations
    FROM public.production_job_order_operations o
    WHERE o.job_order_id = v_job_id;
    RETURN jsonb_build_object(
      'created', false,
      'job_order_id', v_job_id,
      'job_order_number', v_job_number,
      'status', v_job_status,
      'operations', v_operations
    );
  END IF;

  -- Re-read and validate the exact source rows at release time. The browser
  -- cannot release a PI that became inactive, changed status, or changed lines
  -- after the queue was loaded.
  SELECT
    array_agg(r.id::text ORDER BY r.id::text),
    jsonb_agg(jsonb_build_object(
      'id', r.id::text,
      'date', r.date,
      'pi_number', r.pi_number,
      'customer_ref', r.customer_ref,
      'company', r.company,
      'salesman', r.salesman,
      'prepared_by', r.prepared_by,
      'type_of_work', r.type_of_work,
      'qty', r.qty,
      'sqm', r.sqm,
      'status', r.status,
      'pi_status', r.pi_status,
      'source_file', r.source_file
    ) ORDER BY r.id::text),
    count(*)::integer,
    COALESCE(sum(NULLIF(regexp_replace(COALESCE(r.qty::text, '0'), ',', '', 'g'), '')::numeric), 0),
    COALESCE(sum(NULLIF(regexp_replace(COALESCE(r.sqm::text, '0'), ',', '', 'g'), '')::numeric), 0),
    COALESCE(max(NULLIF(btrim(r.company::text), '')), ''),
    COALESCE(max(NULLIF(btrim(r.customer_ref::text), '')), ''),
    COALESCE(max(NULLIF(btrim(r.salesman::text), '')), ''),
    COALESCE(string_agg(DISTINCT NULLIF(btrim(r.type_of_work::text), ''), ', ' ORDER BY NULLIF(btrim(r.type_of_work::text), '')), '')
  INTO v_ids, v_snapshot, v_count, v_qty, v_sqm, v_company, v_customer_ref, v_salesman, v_type_of_work
  FROM public.records r
  WHERE upper(btrim(COALESCE(r.pi_number::text, ''))) = v_pi
    AND r.status::text = 'Confirmed'
    AND r.is_active IS TRUE
    AND btrim(COALESCE(r.source_file::text, '')) = v_source;

  IF v_count = 0 THEN
    RAISE EXCEPTION 'No active Confirmed source rows remain for PI % in workbook %.', v_pi, v_source USING ERRCODE = 'P0002';
  END IF;
  SELECT COALESCE(array_agg(expected_id ORDER BY expected_id), ARRAY[]::text[])
    INTO v_expected_ids
  FROM unnest(COALESCE(p_expected_record_ids, ARRAY[]::text[])) AS expected_id;
  IF v_ids IS DISTINCT FROM v_expected_ids THEN
    RAISE EXCEPTION 'The PI source lines changed after the queue loaded. Refresh the page and review the route again.' USING ERRCODE = '40001';
  END IF;

  v_snapshot := COALESCE(v_snapshot, '[]'::jsonb);
  v_fingerprint := md5(v_snapshot::text);

  -- Validate and canonicalize stage labels/order/units on the server.
  IF EXISTS (
    SELECT 1
    FROM jsonb_array_elements(p_operations) AS item(value)
    GROUP BY item.value->>'stage_code'
    HAVING count(*) > 1
  ) THEN
    RAISE EXCEPTION 'A stage may appear only once in the route.' USING ERRCODE = '22023';
  END IF;

  FOR v_operation IN SELECT value FROM jsonb_array_elements(p_operations)
  LOOP
    v_stage := COALESCE(v_operation->>'stage_code', '');
    v_planned := COALESCE(NULLIF(v_operation->>'planned_qty', '')::numeric, 0);
    IF v_planned <= 0 THEN
      RAISE EXCEPTION 'Every production stage must have a positive planned quantity.' USING ERRCODE = '22023';
    END IF;

    CASE v_stage
      WHEN 'cutting' THEN v_label := 'Cutting'; v_order := 1; v_unit := 'panes'; v_has_cutting := true;
      WHEN 'holes_cutouts' THEN v_label := 'Holes & Cutouts'; v_order := 2; v_unit := 'pieces';
      WHEN 'polish' THEN v_label := 'Polish'; v_order := 3; v_unit := 'linear metres';
      WHEN 'edge_deletion' THEN v_label := 'Edge Deletion'; v_order := 4; v_unit := 'linear metres';
      WHEN 'mitring' THEN v_label := 'Mitring'; v_order := 5; v_unit := 'linear metres';
      WHEN 'tempering' THEN v_label := 'Tempering'; v_order := 6; v_unit := 'panes';
      WHEN 'pairing' THEN v_label := 'Pairing'; v_order := 7; v_unit := 'units';
      WHEN 'doubling_lamination' THEN v_label := 'Doubling / Lamination'; v_order := 8; v_unit := 'units';
      ELSE RAISE EXCEPTION 'Unsupported production stage: %', v_stage USING ERRCODE = '22023';
    END CASE;

    IF COALESCE(v_operation->>'quantity_unit', v_unit) <> v_unit THEN
      RAISE EXCEPTION 'Stage % must use the % quantity unit.', v_label, v_unit USING ERRCODE = '22023';
    END IF;
    IF v_unit <> 'linear metres' AND v_planned <> trunc(v_planned) THEN
      RAISE EXCEPTION 'Stage % must use a whole-number pane, piece or finished-unit quantity.', v_label USING ERRCODE = '22023';
    END IF;
    v_operation_count := v_operation_count + 1;
  END LOOP;
  IF NOT v_has_cutting THEN
    RAISE EXCEPTION 'Cutting is required for every production release.' USING ERRCODE = '22023';
  END IF;
  IF upper(v_type_of_work) ~ '(DGU|DOUBLE[[:space:]]*GLAZ|LAM|HYBRID)'
     AND (
       NOT (p_operations @> '[{"stage_code":"pairing"}]'::jsonb)
       OR NOT (p_operations @> '[{"stage_code":"doubling_lamination"}]'::jsonb)
     ) THEN
    RAISE EXCEPTION 'DGU, LAM and DGU+LAM routes require Pairing and Doubling / Lamination.' USING ERRCODE = '22023';
  END IF;

  v_job_number := 'JO-' || to_char(clock_timestamp(), 'YYYY') || '-' ||
    lpad(nextval('public.production_job_order_number_seq')::text, 6, '0');
  INSERT INTO public.production_job_orders (
    job_order_number, source_pi_number, source_file, source_record_ids,
    source_snapshot, snapshot_fingerprint, company, customer_ref, salesman,
    type_of_work, ordered_qty, ordered_sqm, route_source, route_confirmed_by, released_by
  ) VALUES (
    v_job_number, v_pi, v_source, v_ids, v_snapshot, v_fingerprint,
    v_company, v_customer_ref, v_salesman, v_type_of_work, v_qty, v_sqm,
    'records_summary', v_actor, v_actor
  )
  RETURNING id INTO v_job_id;

  FOR v_operation IN SELECT value FROM jsonb_array_elements(p_operations)
  LOOP
    v_stage := v_operation->>'stage_code';
    v_planned := (v_operation->>'planned_qty')::numeric;
    CASE v_stage
      WHEN 'cutting' THEN v_label := 'Cutting'; v_order := 1; v_unit := 'panes';
      WHEN 'holes_cutouts' THEN v_label := 'Holes & Cutouts'; v_order := 2; v_unit := 'pieces';
      WHEN 'polish' THEN v_label := 'Polish'; v_order := 3; v_unit := 'linear metres';
      WHEN 'edge_deletion' THEN v_label := 'Edge Deletion'; v_order := 4; v_unit := 'linear metres';
      WHEN 'mitring' THEN v_label := 'Mitring'; v_order := 5; v_unit := 'linear metres';
      WHEN 'tempering' THEN v_label := 'Tempering'; v_order := 6; v_unit := 'panes';
      WHEN 'pairing' THEN v_label := 'Pairing'; v_order := 7; v_unit := 'units';
      WHEN 'doubling_lamination' THEN v_label := 'Doubling / Lamination'; v_order := 8; v_unit := 'units';
    END CASE;
    INSERT INTO public.production_job_order_operations (
      job_order_id, stage_code, stage_label, stage_order, quantity_unit, planned_qty, updated_by
    ) VALUES (v_job_id, v_stage, v_label, v_order, v_unit, v_planned, v_actor);
  END LOOP;

  INSERT INTO public.production_job_order_events (
    job_order_id, event_type, actor_id, details
  ) VALUES (
    v_job_id,
    'Released',
    v_actor,
    jsonb_build_object('source_pi_number', v_pi, 'source_file', v_source, 'source_fingerprint', v_fingerprint, 'stage_count', v_operation_count)
  );

  SELECT COALESCE(jsonb_agg(to_jsonb(o) ORDER BY o.stage_order), '[]'::jsonb)
    INTO v_operations
  FROM public.production_job_order_operations o
  WHERE o.job_order_id = v_job_id;

  RETURN jsonb_build_object(
    'created', true,
    'job_order_id', v_job_id,
    'job_order_number', v_job_number,
    'status', 'Released',
    'operations', v_operations
  );
END;
$$;

CREATE OR REPLACE FUNCTION public.update_production_job_order_progress(
  p_job_order_id uuid,
  p_operation_id uuid,
  p_completed_qty numeric
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
  v_actor uuid := auth.uid();
  v_operation public.production_job_order_operations%ROWTYPE;
  v_previous_qty numeric(18,3);
  v_status text;
  v_job_status text;
BEGIN
  IF v_actor IS NULL THEN
    RAISE EXCEPTION 'Authenticated operator required.' USING ERRCODE = '42501';
  END IF;
  IF p_completed_qty IS NULL OR p_completed_qty < 0 THEN
    RAISE EXCEPTION 'Completed quantity must be zero or greater.' USING ERRCODE = '22023';
  END IF;

  -- Parent-first row locking serializes progress saves for a single job.
  PERFORM 1 FROM public.production_job_orders WHERE id = p_job_order_id FOR UPDATE;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'Production Job Order was not found.' USING ERRCODE = 'P0002';
  END IF;
  SELECT * INTO v_operation
  FROM public.production_job_order_operations
  WHERE id = p_operation_id AND job_order_id = p_job_order_id
  FOR UPDATE;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'Production stage was not found for this Job Order.' USING ERRCODE = 'P0002';
  END IF;
  IF p_completed_qty > v_operation.planned_qty THEN
    RAISE EXCEPTION 'Completed quantity cannot exceed the planned quantity (%).', v_operation.planned_qty USING ERRCODE = '22023';
  END IF;
  IF v_operation.quantity_unit <> 'linear metres' AND p_completed_qty <> trunc(p_completed_qty) THEN
    RAISE EXCEPTION 'Pane, piece and finished-unit progress must be a whole number.' USING ERRCODE = '22023';
  END IF;
  v_previous_qty := v_operation.completed_qty;

  v_status := CASE
    WHEN p_completed_qty = 0 THEN 'Pending'
    WHEN p_completed_qty >= v_operation.planned_qty THEN 'Completed'
    ELSE 'In Progress'
  END;

  UPDATE public.production_job_order_operations
  SET completed_qty = p_completed_qty,
      status = v_status,
      started_at = CASE WHEN p_completed_qty > 0 THEN COALESCE(started_at, now()) ELSE NULL END,
      completed_at = CASE WHEN p_completed_qty >= planned_qty THEN now() ELSE NULL END,
      updated_by = v_actor,
      updated_at = now()
  WHERE id = p_operation_id
  RETURNING * INTO v_operation;

  INSERT INTO public.production_job_order_events (
    job_order_id, operation_id, event_type, stage_code,
    previous_qty, new_qty, actor_id
  ) VALUES (
    p_job_order_id, p_operation_id, 'ProgressUpdated', v_operation.stage_code,
    v_previous_qty,
    p_completed_qty, v_actor
  );

  SELECT CASE
    WHEN bool_and(completed_qty >= planned_qty) THEN 'Completed'
    WHEN bool_or(completed_qty > 0) THEN 'In Production'
    ELSE 'Released'
  END
  INTO v_job_status
  FROM public.production_job_order_operations
  WHERE job_order_id = p_job_order_id;

  UPDATE public.production_job_orders
  SET status = COALESCE(v_job_status, 'Released'), updated_at = now()
  WHERE id = p_job_order_id;

  RETURN jsonb_build_object(
    'job_order_id', p_job_order_id,
    'operation', to_jsonb(v_operation),
    'job_status', COALESCE(v_job_status, 'Released')
  );
END;
$$;

REVOKE ALL ON FUNCTION public.release_confirmed_pi_to_production(text, text, text[], jsonb, boolean) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.update_production_job_order_progress(uuid, uuid, numeric) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.release_confirmed_pi_to_production(text, text, text[], jsonb, boolean) TO authenticated;
GRANT EXECUTE ON FUNCTION public.update_production_job_order_progress(uuid, uuid, numeric) TO authenticated;

NOTIFY pgrst, 'reload schema';
