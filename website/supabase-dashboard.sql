-- =====================================================================
-- PROGLASS AUTOMATION — HIGH-SCALE RECORDS DASHBOARD & EXCEL SYNC
-- Project: afpbdyamedpanwijxghk
-- Table: public.records
-- =====================================================================
--
-- This script provides:
-- 0. Schema migration: Adds is_active boolean flag for non-destructive
--    soft-delete reconciliation with the active Excel worksheet.
-- 1. Native PostgreSQL server-side aggregation function for millions of records
--    filtering strictly on active records (is_active = true).
-- 2. Scoped reconciliation RPC function for Excel Sync / Windowfilehost.
-- 3. High-performance indexes tailored to ERP operations and dashboard filtering.
--
-- Run this in the Supabase SQL Editor:
-- https://supabase.com/dashboard/project/afpbdyamedpanwijxghk/sql
-- =====================================================================

-- ---------------------------------------------------------------------
-- 0. SCHEMA MIGRATION: SOFT-DELETE / ACTIVE RECONCILIATION
-- ---------------------------------------------------------------------
-- Adds is_active flag to public.records.
-- Default is true so existing historical records remain intact.
-- Excel row currently present  -> is_active = true
-- Excel row deleted/missing   -> is_active = false (retained for history)
-- Excel row re-added later     -> is_active = true
-- ---------------------------------------------------------------------

ALTER TABLE public.records 
ADD COLUMN IF NOT EXISTS is_active boolean NOT NULL DEFAULT true;

-- High-speed index for active records filtering
CREATE INDEX IF NOT EXISTS idx_records_is_active 
ON public.records(is_active);

-- Scoped composite partial index for source reconciliation and active dashboard queries
CREATE INDEX IF NOT EXISTS idx_records_active_source 
ON public.records(source_file, source_worksheet) 
WHERE is_active = true;


-- ---------------------------------------------------------------------
-- 1. NATIVE SERVER-SIDE AGGREGATION RPC
-- ---------------------------------------------------------------------
-- Computes Total Entries, Total Qty, Total SQM, Average SQM, Active
-- Companies, Active Salesmen, and all 4 breakdowns (Prepared By, Salesman,
-- Company, Type of Work) directly inside PostgreSQL.
--
-- Filters strictly on active records: (is_active IS NOT FALSE).
--
-- Security model:
-- - SECURITY INVOKER: Runs with caller privileges, strictly enforcing
--   Row Level Security (RLS) on public.records.
-- - STABLE: Allows the query optimizer to cache results within transactions.
-- - SET search_path = public, pg_temp: Prevents search-path hijacking.
-- ---------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.get_records_dashboard_aggregates(
  p_search text DEFAULT NULL,
  p_date_from text DEFAULT NULL,
  p_date_to text DEFAULT NULL,
  p_company text DEFAULT NULL,
  p_salesman text DEFAULT NULL,
  p_prepared_by text DEFAULT NULL,
  p_status text DEFAULT NULL,
  p_pi_status text DEFAULT NULL,
  p_type_of_work text DEFAULT NULL,
  p_zoho_status text DEFAULT NULL,
  p_source_file text DEFAULT NULL
)
RETURNS json
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
SET search_path = public, pg_temp
AS $$
DECLARE
  v_summary json;
  v_prepared_by json;
  v_salesman json;
  v_company json;
  v_type_of_work json;
  v_filter_options json;
  v_search text;
BEGIN
  -- Normalize substring search pattern
  IF p_search IS NOT NULL AND trim(p_search) <> '' THEN
    v_search := '%' || trim(p_search) || '%';
  ELSE
    v_search := NULL;
  END IF;

  -- 1. Summary aggregates across the complete filtered active dataset
  WITH filtered AS (
    SELECT
      company,
      salesman,
      NULLIF(TRIM(qty), '')::numeric AS num_qty,
      NULLIF(TRIM(sqm), '')::numeric AS num_sqm
    FROM public.records
    WHERE
      (is_active IS NOT FALSE)
      AND (p_source_file IS NULL OR p_source_file = '' OR p_source_file = 'all' OR source_file = p_source_file)
      AND (p_date_from IS NULL OR p_date_from = '' OR date >= p_date_from)
      AND (p_date_to IS NULL OR p_date_to = '' OR date <= p_date_to)
      AND (p_company IS NULL OR p_company = '' OR company = p_company)
      AND (p_salesman IS NULL OR p_salesman = '' OR salesman = p_salesman)
      AND (p_prepared_by IS NULL OR p_prepared_by = '' OR prepared_by = p_prepared_by)
      AND (p_status IS NULL OR p_status = '' OR status = p_status)
      AND (p_pi_status IS NULL OR p_pi_status = '' OR pi_status = p_pi_status)
      AND (p_type_of_work IS NULL OR p_type_of_work = '' OR type_of_work = p_type_of_work)
      AND (p_zoho_status IS NULL OR p_zoho_status = '' OR zoho_status = p_zoho_status)
      AND (
        v_search IS NULL OR
        pi_number ILIKE v_search OR
        customer_ref ILIKE v_search OR
        company ILIKE v_search
      )
  ),
  summary_calc AS (
    SELECT
      COUNT(*)::bigint AS total_entries,
      COALESCE(SUM(num_qty), 0)::bigint AS total_qty,
      ROUND(COALESCE(SUM(num_sqm), 0), 2) AS total_sqm,
      CASE 
        WHEN COUNT(*) > 0 
        THEN ROUND((COALESCE(SUM(num_sqm), 0) / COUNT(*))::numeric, 2)
        ELSE 0 
      END AS average_sqm,
      COUNT(DISTINCT NULLIF(TRIM(company), ''))::integer AS active_companies_count,
      COUNT(DISTINCT NULLIF(TRIM(salesman), ''))::integer AS active_salesmen_count
    FROM filtered
  )
  SELECT json_build_object(
    'totalEntries', total_entries,
    'totalQty', total_qty,
    'totalSqm', total_sqm,
    'averageSqm', average_sqm,
    'activeCompaniesCount', active_companies_count,
    'activeSalesmenCount', active_salesmen_count
  ) INTO v_summary
  FROM summary_calc;

  -- 2. Prepared By breakdown (GROUP BY prepared_by)
  WITH filtered AS (
    SELECT
      prepared_by,
      NULLIF(TRIM(qty), '')::numeric AS num_qty,
      NULLIF(TRIM(sqm), '')::numeric AS num_sqm
    FROM public.records
    WHERE
      (is_active IS NOT FALSE)
      AND (p_source_file IS NULL OR p_source_file = '' OR p_source_file = 'all' OR source_file = p_source_file)
      AND (p_date_from IS NULL OR p_date_from = '' OR date >= p_date_from)
      AND (p_date_to IS NULL OR p_date_to = '' OR date <= p_date_to)
      AND (p_company IS NULL OR p_company = '' OR company = p_company)
      AND (p_salesman IS NULL OR p_salesman = '' OR salesman = p_salesman)
      AND (p_prepared_by IS NULL OR p_prepared_by = '' OR prepared_by = p_prepared_by)
      AND (p_status IS NULL OR p_status = '' OR status = p_status)
      AND (p_pi_status IS NULL OR p_pi_status = '' OR pi_status = p_pi_status)
      AND (p_type_of_work IS NULL OR p_type_of_work = '' OR type_of_work = p_type_of_work)
      AND (p_zoho_status IS NULL OR p_zoho_status = '' OR zoho_status = p_zoho_status)
      AND (
        v_search IS NULL OR
        pi_number ILIKE v_search OR
        customer_ref ILIKE v_search OR
        company ILIKE v_search
      )
  ),
  tot AS (
    SELECT COUNT(*)::bigint AS total_count FROM filtered
  ),
  grouped AS (
    SELECT
      COALESCE(NULLIF(trim(prepared_by), ''), '(Unassigned)') AS name,
      COUNT(*)::bigint AS count,
      COALESCE(SUM(num_qty), 0)::bigint AS total_qty,
      ROUND(COALESCE(SUM(num_sqm), 0), 2) AS total_sqm
    FROM filtered
    GROUP BY COALESCE(NULLIF(trim(prepared_by), ''), '(Unassigned)')
    ORDER BY count DESC
    LIMIT 20
  )
  SELECT COALESCE(json_agg(
    json_build_object(
      'name', g.name,
      'count', g.count,
      'totalQty', g.total_qty,
      'totalSqm', g.total_sqm,
      'percentage', CASE WHEN t.total_count > 0 THEN ROUND((g.count * 100.0 / t.total_count)::numeric, 1) ELSE 0 END
    )
  ), '[]'::json) INTO v_prepared_by
  FROM grouped g, tot t;

  -- 3. Salesman breakdown (GROUP BY salesman)
  WITH filtered AS (
    SELECT
      salesman,
      NULLIF(TRIM(qty), '')::numeric AS num_qty,
      NULLIF(TRIM(sqm), '')::numeric AS num_sqm
    FROM public.records
    WHERE
      (is_active IS NOT FALSE)
      AND (p_source_file IS NULL OR p_source_file = '' OR p_source_file = 'all' OR source_file = p_source_file)
      AND (p_date_from IS NULL OR p_date_from = '' OR date >= p_date_from)
      AND (p_date_to IS NULL OR p_date_to = '' OR date <= p_date_to)
      AND (p_company IS NULL OR p_company = '' OR company = p_company)
      AND (p_salesman IS NULL OR p_salesman = '' OR salesman = p_salesman)
      AND (p_prepared_by IS NULL OR p_prepared_by = '' OR prepared_by = p_prepared_by)
      AND (p_status IS NULL OR p_status = '' OR status = p_status)
      AND (p_pi_status IS NULL OR p_pi_status = '' OR pi_status = p_pi_status)
      AND (p_type_of_work IS NULL OR p_type_of_work = '' OR type_of_work = p_type_of_work)
      AND (p_zoho_status IS NULL OR p_zoho_status = '' OR zoho_status = p_zoho_status)
      AND (
        v_search IS NULL OR
        pi_number ILIKE v_search OR
        customer_ref ILIKE v_search OR
        company ILIKE v_search
      )
  ),
  tot AS (
    SELECT COUNT(*)::bigint AS total_count FROM filtered
  ),
  grouped AS (
    SELECT
      COALESCE(NULLIF(trim(salesman), ''), '(Unassigned)') AS name,
      COUNT(*)::bigint AS count,
      COALESCE(SUM(num_qty), 0)::bigint AS total_qty,
      ROUND(COALESCE(SUM(num_sqm), 0), 2) AS total_sqm
    FROM filtered
    GROUP BY COALESCE(NULLIF(trim(salesman), ''), '(Unassigned)')
    ORDER BY count DESC
    LIMIT 20
  )
  SELECT COALESCE(json_agg(
    json_build_object(
      'name', g.name,
      'count', g.count,
      'totalQty', g.total_qty,
      'totalSqm', g.total_sqm,
      'percentage', CASE WHEN t.total_count > 0 THEN ROUND((g.count * 100.0 / t.total_count)::numeric, 1) ELSE 0 END
    )
  ), '[]'::json) INTO v_salesman
  FROM grouped g, tot t;

  -- 4. Company breakdown (GROUP BY company)
  WITH filtered AS (
    SELECT
      company,
      NULLIF(TRIM(qty), '')::numeric AS num_qty,
      NULLIF(TRIM(sqm), '')::numeric AS num_sqm
    FROM public.records
    WHERE
      (is_active IS NOT FALSE)
      AND (p_source_file IS NULL OR p_source_file = '' OR p_source_file = 'all' OR source_file = p_source_file)
      AND (p_date_from IS NULL OR p_date_from = '' OR date >= p_date_from)
      AND (p_date_to IS NULL OR p_date_to = '' OR date <= p_date_to)
      AND (p_company IS NULL OR p_company = '' OR company = p_company)
      AND (p_salesman IS NULL OR p_salesman = '' OR salesman = p_salesman)
      AND (p_prepared_by IS NULL OR p_prepared_by = '' OR prepared_by = p_prepared_by)
      AND (p_status IS NULL OR p_status = '' OR status = p_status)
      AND (p_pi_status IS NULL OR p_pi_status = '' OR pi_status = p_pi_status)
      AND (p_type_of_work IS NULL OR p_type_of_work = '' OR type_of_work = p_type_of_work)
      AND (p_zoho_status IS NULL OR p_zoho_status = '' OR zoho_status = p_zoho_status)
      AND (
        v_search IS NULL OR
        pi_number ILIKE v_search OR
        customer_ref ILIKE v_search OR
        company ILIKE v_search
      )
  ),
  tot AS (
    SELECT COUNT(*)::bigint AS total_count FROM filtered
  ),
  grouped AS (
    SELECT
      COALESCE(NULLIF(trim(company), ''), '(Unassigned)') AS name,
      COUNT(*)::bigint AS count,
      COALESCE(SUM(num_qty), 0)::bigint AS total_qty,
      ROUND(COALESCE(SUM(num_sqm), 0), 2) AS total_sqm
    FROM filtered
    GROUP BY COALESCE(NULLIF(trim(company), ''), '(Unassigned)')
    ORDER BY count DESC
    LIMIT 20
  )
  SELECT COALESCE(json_agg(
    json_build_object(
      'name', g.name,
      'count', g.count,
      'totalQty', g.total_qty,
      'totalSqm', g.total_sqm,
      'percentage', CASE WHEN t.total_count > 0 THEN ROUND((g.count * 100.0 / t.total_count)::numeric, 1) ELSE 0 END
    )
  ), '[]'::json) INTO v_company
  FROM grouped g, tot t;

  -- 5. Type of Work breakdown (GROUP BY type_of_work)
  WITH filtered AS (
    SELECT
      type_of_work,
      NULLIF(TRIM(qty), '')::numeric AS num_qty,
      NULLIF(TRIM(sqm), '')::numeric AS num_sqm
    FROM public.records
    WHERE
      (is_active IS NOT FALSE)
      AND (p_source_file IS NULL OR p_source_file = '' OR p_source_file = 'all' OR source_file = p_source_file)
      AND (p_date_from IS NULL OR p_date_from = '' OR date >= p_date_from)
      AND (p_date_to IS NULL OR p_date_to = '' OR date <= p_date_to)
      AND (p_company IS NULL OR p_company = '' OR company = p_company)
      AND (p_salesman IS NULL OR p_salesman = '' OR salesman = p_salesman)
      AND (p_prepared_by IS NULL OR p_prepared_by = '' OR prepared_by = p_prepared_by)
      AND (p_status IS NULL OR p_status = '' OR status = p_status)
      AND (p_pi_status IS NULL OR p_pi_status = '' OR pi_status = p_pi_status)
      AND (p_type_of_work IS NULL OR p_type_of_work = '' OR type_of_work = p_type_of_work)
      AND (p_zoho_status IS NULL OR p_zoho_status = '' OR zoho_status = p_zoho_status)
      AND (
        v_search IS NULL OR
        pi_number ILIKE v_search OR
        customer_ref ILIKE v_search OR
        company ILIKE v_search
      )
  ),
  tot AS (
    SELECT COUNT(*)::bigint AS total_count FROM filtered
  ),
  grouped AS (
    SELECT
      COALESCE(NULLIF(trim(type_of_work), ''), '(Unassigned)') AS name,
      COUNT(*)::bigint AS count,
      COALESCE(SUM(num_qty), 0)::bigint AS total_qty,
      ROUND(COALESCE(SUM(num_sqm), 0), 2) AS total_sqm
    FROM filtered
    GROUP BY COALESCE(NULLIF(trim(type_of_work), ''), '(Unassigned)')
    ORDER BY count DESC
    LIMIT 20
  )
  SELECT COALESCE(json_agg(
    json_build_object(
      'name', g.name,
      'count', g.count,
      'totalQty', g.total_qty,
      'totalSqm', g.total_sqm,
      'percentage', CASE WHEN t.total_count > 0 THEN ROUND((g.count * 100.0 / t.total_count)::numeric, 1) ELSE 0 END
    )
  ), '[]'::json) INTO v_type_of_work
  FROM grouped g, tot t;

  -- 6. Distinct filter options directly from PostgreSQL (Active records scoped to source)
  SELECT json_build_object(
    'companies', (SELECT COALESCE(json_agg(company), '[]'::json) FROM (SELECT DISTINCT company FROM public.records WHERE is_active IS NOT FALSE AND (p_source_file IS NULL OR p_source_file = '' OR p_source_file = 'all' OR source_file = p_source_file) AND company IS NOT NULL AND trim(company) <> '' ORDER BY company) c),
    'salesmen', (SELECT COALESCE(json_agg(salesman), '[]'::json) FROM (SELECT DISTINCT salesman FROM public.records WHERE is_active IS NOT FALSE AND (p_source_file IS NULL OR p_source_file = '' OR p_source_file = 'all' OR source_file = p_source_file) AND salesman IS NOT NULL AND trim(salesman) <> '' ORDER BY salesman) s),
    'preparedBy', (SELECT COALESCE(json_agg(prepared_by), '[]'::json) FROM (SELECT DISTINCT prepared_by FROM public.records WHERE is_active IS NOT FALSE AND (p_source_file IS NULL OR p_source_file = '' OR p_source_file = 'all' OR source_file = p_source_file) AND prepared_by IS NOT NULL AND trim(prepared_by) <> '' ORDER BY prepared_by) p),
    'statuses', (SELECT COALESCE(json_agg(status), '[]'::json) FROM (SELECT DISTINCT status FROM public.records WHERE is_active IS NOT FALSE AND (p_source_file IS NULL OR p_source_file = '' OR p_source_file = 'all' OR source_file = p_source_file) AND status IS NOT NULL AND trim(status) <> '' ORDER BY status) st),
    'piStatuses', (SELECT COALESCE(json_agg(pi_status), '[]'::json) FROM (SELECT DISTINCT pi_status FROM public.records WHERE is_active IS NOT FALSE AND (p_source_file IS NULL OR p_source_file = '' OR p_source_file = 'all' OR source_file = p_source_file) AND pi_status IS NOT NULL AND trim(pi_status) <> '' ORDER BY pi_status) pis),
    'typesOfWork', (SELECT COALESCE(json_agg(type_of_work), '[]'::json) FROM (SELECT DISTINCT type_of_work FROM public.records WHERE is_active IS NOT FALSE AND (p_source_file IS NULL OR p_source_file = '' OR p_source_file = 'all' OR source_file = p_source_file) AND type_of_work IS NOT NULL AND trim(type_of_work) <> '' ORDER BY type_of_work) tw),
    'zohoStatuses', (SELECT COALESCE(json_agg(zoho_status), '[]'::json) FROM (SELECT DISTINCT zoho_status FROM public.records WHERE is_active IS NOT FALSE AND (p_source_file IS NULL OR p_source_file = '' OR p_source_file = 'all' OR source_file = p_source_file) AND zoho_status IS NOT NULL AND trim(zoho_status) <> '' ORDER BY zoho_status) z),
    'sourceFiles', (SELECT COALESCE(json_agg(sf), '[]'::json) FROM (SELECT DISTINCT source_file FROM public.records WHERE is_active IS NOT FALSE AND source_file IS NOT NULL AND trim(source_file) <> '' ORDER BY source_file) sf),
    'currentSourceFile', (SELECT source_file FROM public.records WHERE is_active IS NOT FALSE AND source_file IS NOT NULL AND trim(source_file) <> '' ORDER BY COALESCE(updated_at, created_at) DESC NULLS LAST, created_at DESC NULLS LAST, id DESC LIMIT 1)
  ) INTO v_filter_options;

  RETURN json_build_object(
    'summary', v_summary,
    'breakdowns', json_build_object(
      'preparedBy', v_prepared_by,
      'salesman', v_salesman,
      'company', v_company,
      'typeOfWork', v_type_of_work
    ),
    'filterOptions', v_filter_options
  );
END;
$$;

-- Explicit permission grants
GRANT EXECUTE ON FUNCTION public.get_records_dashboard_aggregates TO anon, authenticated, service_role;


-- ---------------------------------------------------------------------
-- 2. SCOPED RECONCILIATION RPC (FOR EXCEL SYNC / WINDOWFILEHOST)
-- ---------------------------------------------------------------------
-- Safely reconciles the Supabase records against the current Excel worksheet:
-- - Sets is_active = false for records previously synced from this exact
--   source file and sheet but no longer present in current Excel workbook.
-- - Sets is_active = true for records present in current Excel workbook.
-- - Strictly scoped: NEVER modifies rows from other files or worksheets.
-- - Atomic: Executes within a single database transaction.
-- - Safety guarded: Rejects empty or null active PI array to prevent
--   accidental bulk deactivation during file locks or temporary save states.
-- ---------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.reconcile_records_active_state(
  p_source_file text,
  p_source_worksheet text,
  p_active_pi_numbers text[]
)
RETURNS json
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = public, pg_temp
AS $$
DECLARE
  v_deactivated_count integer := 0;
  v_reactivated_count integer := 0;
  v_total_active integer := 0;
BEGIN
  -- Strict validation of source scope parameters
  IF p_source_file IS NULL OR trim(p_source_file) = '' THEN
    RAISE EXCEPTION 'p_source_file cannot be empty';
  END IF;
  IF p_source_worksheet IS NULL OR trim(p_source_worksheet) = '' THEN
    RAISE EXCEPTION 'p_source_worksheet cannot be empty';
  END IF;

  -- CRITICAL EMPTY-WORKBOOK SAFETY:
  -- Never allow an empty or null array to deactivate all remote records.
  -- A corrupted file or Excel rewrite lock must not mark valid records inactive.
  IF p_active_pi_numbers IS NULL OR array_length(p_active_pi_numbers, 1) IS NULL OR array_length(p_active_pi_numbers, 1) = 0 THEN
    RETURN json_build_object(
      'ok', false,
      'error', 'Safety trigger: Empty active PI numbers list rejected to prevent accidental bulk deactivation'
    );
  END IF;

  -- 1. Soft-deactivate records missing from current Excel sheet
  -- (Scoped strictly to p_source_file and p_source_worksheet)
  UPDATE public.records
  SET is_active = false, updated_at = NOW()
  WHERE source_file = p_source_file
    AND source_worksheet = p_source_worksheet
    AND is_active = true
    AND NOT (pi_number = ANY(p_active_pi_numbers));
  GET DIAGNOSTICS v_deactivated_count = ROW_COUNT;

  -- 2. Reactivate any returning records present in Excel
  UPDATE public.records
  SET is_active = true, updated_at = NOW()
  WHERE source_file = p_source_file
    AND source_worksheet = p_source_worksheet
    AND is_active = false
    AND pi_number = ANY(p_active_pi_numbers);
  GET DIAGNOSTICS v_reactivated_count = ROW_COUNT;

  -- Count remaining active records for this source
  SELECT COUNT(*) INTO v_total_active
  FROM public.records
  WHERE source_file = p_source_file
    AND source_worksheet = p_source_worksheet
    AND is_active = true;

  RETURN json_build_object(
    'ok', true,
    'sourceFile', p_source_file,
    'sourceWorksheet', p_source_worksheet,
    'activeCount', v_total_active,
    'deactivated', v_deactivated_count,
    'reactivated', v_reactivated_count
  );
END;
$$;

-- Explicit permission grants
GRANT EXECUTE ON FUNCTION public.reconcile_records_active_state TO anon, authenticated, service_role;


-- ---------------------------------------------------------------------
-- 3. CURRENT EXCEL SOURCE RESOLUTION & WORKBOOK ACTIVATION RPCs
-- ---------------------------------------------------------------------

-- Resolves the current active source file name (latest sync timestamp or highest ID)
CREATE OR REPLACE FUNCTION public.get_current_excel_source()
RETURNS text
LANGUAGE sql
STABLE
SECURITY INVOKER
AS $$
  SELECT source_file
  FROM public.records
  WHERE is_active IS NOT FALSE
    AND source_file IS NOT NULL
    AND trim(source_file) <> ''
  ORDER BY 
    COALESCE(updated_at, created_at) DESC NULLS LAST,
    created_at DESC NULLS LAST,
    id DESC
  LIMIT 1;
$$;

GRANT EXECUTE ON FUNCTION public.get_current_excel_source TO anon, authenticated, service_role;

-- ---------------------------------------------------------------------
-- 3B. AUTOMATIC TIMESTAMP & ACTIVE STATE TRIGGER
-- ---------------------------------------------------------------------
-- Guarantees that newly inserted or synced rows from Excel always have
-- created_at, updated_at, and is_active populated automatically even
-- if ProGlassExcelSync does not explicitly pass them in the payload.
-- ---------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.trg_records_set_timestamps()
RETURNS TRIGGER AS $$
BEGIN
  IF TG_OP = 'INSERT' THEN
    NEW.created_at = COALESCE(NEW.created_at, NOW());
    NEW.updated_at = COALESCE(NEW.updated_at, NOW());
    NEW.is_active = COALESCE(NEW.is_active, true);
  ELSIF TG_OP = 'UPDATE' THEN
    NEW.updated_at = NOW();
  END IF;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_records_timestamps ON public.records;
CREATE TRIGGER trg_records_timestamps
BEFORE INSERT OR UPDATE ON public.records
FOR EACH ROW
EXECUTE FUNCTION public.trg_records_set_timestamps();

-- Designates a specific workbook as the active Excel dataset
CREATE OR REPLACE FUNCTION public.set_current_excel_file(
  p_source_file text,
  p_deactivate_others boolean DEFAULT false
)
RETURNS json
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = public, pg_temp
AS $$
DECLARE
  v_activated integer := 0;
  v_deactivated integer := 0;
BEGIN
  IF p_source_file IS NULL OR trim(p_source_file) = '' THEN
    RAISE EXCEPTION 'p_source_file cannot be empty';
  END IF;

  -- 1. Ensure records for this source file are active and touch updated_at
  UPDATE public.records
  SET is_active = true, updated_at = NOW()
  WHERE source_file = p_source_file;
  GET DIAGNOSTICS v_activated = ROW_COUNT;

  -- 2. Optionally deactivate older superseded workbooks to guarantee single-source view
  IF p_deactivate_others THEN
    UPDATE public.records
    SET is_active = false, updated_at = NOW()
    WHERE source_file <> p_source_file AND is_active = true;
    GET DIAGNOSTICS v_deactivated = ROW_COUNT;
  END IF;

  RETURN json_build_object(
    'ok', true,
    'currentSourceFile', p_source_file,
    'activated', v_activated,
    'deactivated', v_deactivated
  );
END;
$$;

GRANT EXECUTE ON FUNCTION public.set_current_excel_file TO anon, authenticated, service_role;


-- ---------------------------------------------------------------------
-- 4. RECOMMENDED INDEXES FOR LARGE SCALE (MILLIONS OF RECORDS)
-- ---------------------------------------------------------------------
-- Evaluate before applying: each index accelerates specific read queries
-- while adding modest write overhead on Excel Sync bulk INSERTs/UPDATEs.
-- ---------------------------------------------------------------------

-- Index 1: Accelerated Date Sorting & Range Filtering
-- Accelerates: WHERE date >= '...' AND date <= '...' ORDER BY date DESC
CREATE INDEX IF NOT EXISTS idx_records_date_desc 
ON public.records(date DESC);

-- Index 2: Categorical Equality Filters
-- Accelerates: WHERE company = '...' / salesman = '...' / prepared_by = '...'
CREATE INDEX IF NOT EXISTS idx_records_company 
ON public.records(company);

CREATE INDEX IF NOT EXISTS idx_records_salesman 
ON public.records(salesman);

CREATE INDEX IF NOT EXISTS idx_records_prepared_by 
ON public.records(prepared_by);

CREATE INDEX IF NOT EXISTS idx_records_status 
ON public.records(status);

-- Index 3: Composite Index for Combined Date + Company Drilldown
-- Accelerates: WHERE company = '...' AND date >= '...' ORDER BY date DESC
CREATE INDEX IF NOT EXISTS idx_records_company_date 
ON public.records(company, date DESC);

-- Index 4: Composite Index for Attached Excel Scoping and Active Filtering
-- Accelerates: WHERE source_file = '...' AND is_active = true ORDER BY updated_at DESC
CREATE INDEX IF NOT EXISTS idx_records_source_active_updated 
ON public.records(source_file, is_active, updated_at DESC);

-- Index 5: Trigram Substring Search Index (GIN)
-- Accelerates: ILIKE '%search%' across PI number and customer reference
-- Requires pg_trgm extension.
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX IF NOT EXISTS idx_records_pi_number_trgm 
ON public.records USING gin (pi_number gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_records_customer_ref_trgm 
ON public.records USING gin (customer_ref gin_trgm_ops);


-- ---------------------------------------------------------------------
-- 5. ROW LEVEL SECURITY (RLS) POLICIES FOR OPERATIONAL REPORTS
-- ---------------------------------------------------------------------
-- Operational records must NOT be publicly readable anonymously.
-- Anonymous visitors cannot view operational business data.
-- Only authenticated registered users can SELECT active records.
-- Service role retains full management access for external sync.
-- ---------------------------------------------------------------------

-- Enable Row Level Security
ALTER TABLE public.records ENABLE ROW LEVEL SECURITY;

-- 1. Remove anonymous read access (if previously granted)
DROP POLICY IF EXISTS "Allow anon read" ON public.records;

-- 2. Allow authenticated registered users to view active records
DROP POLICY IF EXISTS "Allow authenticated read" ON public.records;
CREATE POLICY "Allow authenticated read" 
ON public.records 
FOR SELECT 
TO authenticated 
USING (is_active IS NOT FALSE);

-- 3. Service role retained for Excel Sync / Windowfilehost backend
DROP POLICY IF EXISTS "Allow service role all" ON public.records;
CREATE POLICY "Allow service role all" 
ON public.records 
FOR ALL 
TO service_role 
USING (true) 
WITH CHECK (true);
