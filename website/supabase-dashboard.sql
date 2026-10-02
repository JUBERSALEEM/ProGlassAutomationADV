-- =====================================================================
-- PROGLASS AUTOMATION — HIGH-SCALE RECORDS DASHBOARD MIGRATION
-- Project: afpbdyamedpanwijxghk
-- Table: public.records
-- =====================================================================
--
-- This script provides:
-- 1. Native PostgreSQL server-side aggregation function for millions of records
-- 2. Performance indexes tailored to ERP operations and dashboard filtering
-- 3. Strict RLS preservation using SECURITY INVOKER (no permission bypass)
--
-- Run this in the Supabase SQL Editor:
-- https://supabase.com/dashboard/project/afpbdyamedpanwijxghk/sql
-- =====================================================================

-- ---------------------------------------------------------------------
-- 1. NATIVE SERVER-SIDE AGGREGATION RPC
-- ---------------------------------------------------------------------
-- Computes Total Entries, Total Qty, Total SQM, Average SQM, Active
-- Companies, Active Salesmen, and all 4 breakdowns (Prepared By, Salesman,
-- Company, Type of Work) directly inside PostgreSQL.
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
  p_zoho_status text DEFAULT NULL
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

  -- 1. Summary aggregates across the complete filtered dataset
  WITH filtered AS (
    SELECT
      id,
      date,
      company,
      salesman,
      prepared_by,
      status,
      pi_status,
      type_of_work,
      zoho_status,
      qty,
      sqm
    FROM public.records
    WHERE
      (p_date_from IS NULL OR p_date_from = '' OR date >= p_date_from)
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
      COALESCE(SUM(qty), 0)::bigint AS total_qty,
      ROUND(COALESCE(SUM(sqm), 0)::numeric, 2) AS total_sqm,
      CASE 
        WHEN COALESCE(SUM(qty), 0) > 0 
        THEN ROUND((COALESCE(SUM(sqm), 0) / SUM(qty))::numeric, 2)
        ELSE 0 
      END AS average_sqm,
      COUNT(DISTINCT company)::integer AS active_companies_count,
      COUNT(DISTINCT salesman)::integer AS active_salesmen_count
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
    SELECT prepared_by, qty, sqm
    FROM public.records
    WHERE
      (p_date_from IS NULL OR p_date_from = '' OR date >= p_date_from)
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
      COALESCE(SUM(qty), 0)::bigint AS total_qty,
      ROUND(COALESCE(SUM(sqm), 0)::numeric, 2) AS total_sqm
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
    SELECT salesman, qty, sqm
    FROM public.records
    WHERE
      (p_date_from IS NULL OR p_date_from = '' OR date >= p_date_from)
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
      COALESCE(SUM(qty), 0)::bigint AS total_qty,
      ROUND(COALESCE(SUM(sqm), 0)::numeric, 2) AS total_sqm
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
    SELECT company, qty, sqm
    FROM public.records
    WHERE
      (p_date_from IS NULL OR p_date_from = '' OR date >= p_date_from)
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
      COALESCE(SUM(qty), 0)::bigint AS total_qty,
      ROUND(COALESCE(SUM(sqm), 0)::numeric, 2) AS total_sqm
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
    SELECT type_of_work, qty, sqm
    FROM public.records
    WHERE
      (p_date_from IS NULL OR p_date_from = '' OR date >= p_date_from)
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
      COALESCE(SUM(qty), 0)::bigint AS total_qty,
      ROUND(COALESCE(SUM(sqm), 0)::numeric, 2) AS total_sqm
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

  -- 6. Distinct filter options directly from PostgreSQL
  SELECT json_build_object(
    'companies', (SELECT COALESCE(json_agg(company), '[]'::json) FROM (SELECT DISTINCT company FROM public.records WHERE company IS NOT NULL AND company <> '' ORDER BY company) c),
    'salesmen', (SELECT COALESCE(json_agg(salesman), '[]'::json) FROM (SELECT DISTINCT salesman FROM public.records WHERE salesman IS NOT NULL AND salesman <> '' ORDER BY salesman) s),
    'preparedBy', (SELECT COALESCE(json_agg(prepared_by), '[]'::json) FROM (SELECT DISTINCT prepared_by FROM public.records WHERE prepared_by IS NOT NULL AND prepared_by <> '' ORDER BY prepared_by) p),
    'statuses', (SELECT COALESCE(json_agg(status), '[]'::json) FROM (SELECT DISTINCT status FROM public.records WHERE status IS NOT NULL AND status <> '' ORDER BY status) st),
    'piStatuses', (SELECT COALESCE(json_agg(pi_status), '[]'::json) FROM (SELECT DISTINCT pi_status FROM public.records WHERE pi_status IS NOT NULL AND pi_status <> '' ORDER BY pi_status) pis),
    'typesOfWork', (SELECT COALESCE(json_agg(type_of_work), '[]'::json) FROM (SELECT DISTINCT type_of_work FROM public.records WHERE type_of_work IS NOT NULL AND type_of_work <> '' ORDER BY type_of_work) tw),
    'zohoStatuses', (SELECT COALESCE(json_agg(zoho_status), '[]'::json) FROM (SELECT DISTINCT zoho_status FROM public.records WHERE zoho_status IS NOT NULL AND zoho_status <> '' ORDER BY zoho_status) z)
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
-- 2. RECOMMENDED INDEXES FOR LARGE SCALE (MILLIONS OF RECORDS)
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

-- Index 4: Optional Trigram Substring Search Index (GIN)
-- Accelerates: ILIKE '%search%' across PI number and customer reference
-- Requires pg_trgm extension.
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX IF NOT EXISTS idx_records_pi_number_trgm 
ON public.records USING gin (pi_number gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_records_customer_ref_trgm 
ON public.records USING gin (customer_ref gin_trgm_ops);
