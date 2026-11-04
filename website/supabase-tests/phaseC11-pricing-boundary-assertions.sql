-- PHASE C11 PRICING-BOUNDARY DETERMINISTIC ASSERTIONS
--
-- Run only after phaseC11-pricing-boundary-harness.sql and the C11 migration.
-- Every role test uses PostgREST-style authenticated role plus JWT claims.

-- 1. Customer direct table reads return no priced PI, lines or share metadata.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000001')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE p integer; l integer; s integer; saves integer; groups integer;
BEGIN
  SELECT count(*) INTO p FROM public.inquiry_proformas;
  SELECT count(*) INTO l FROM public.inquiry_proforma_lines;
  SELECT count(*) INTO s FROM public.inquiry_proforma_shares;
  SELECT count(*) INTO saves FROM public.inquiry_proforma_saves;
  SELECT count(*) INTO groups FROM public.inquiry_proforma_groups;
  IF p <> 0 OR l <> 0 OR s <> 0 OR saves <> 0 OR groups <> 0 THEN
    RAISE EXCEPTION 'case 1: customer saw proformas %, lines %, shares %, saves %, groups %', p, l, s, saves, groups;
  END IF;
END $$;
ROLLBACK;
SELECT 'case 1 ok: customer direct SELECT returns no PI, rate, value, share, save or group rows' AS result;

-- 2. Authorized administrator retains priced PI and internal history reads.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000002')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE p integer; l integer; s integer; rate numeric; value numeric; saves integer; groups integer;
BEGIN
  SELECT count(*) INTO p FROM public.inquiry_proformas;
  SELECT count(*), min(approved_rate), min(line_value) INTO l, rate, value FROM public.inquiry_proforma_lines;
  SELECT count(*) INTO s FROM public.inquiry_proforma_shares;
  SELECT count(*) INTO saves FROM public.inquiry_proforma_saves;
  SELECT count(*) INTO groups FROM public.inquiry_proforma_groups;
  IF p <> 1 OR l <> 1 OR s <> 1 OR saves <> 1 OR groups <> 1
     OR rate <> 125.50 OR value <> 500.25 THEN
    RAISE EXCEPTION 'case 2: admin read changed (p %, l %, s %, saves %, groups %, rate %, value %)', p, l, s, saves, groups, rate, value;
  END IF;
  IF NOT has_function_privilege('authenticated', 'public.save_inquiry_proforma_v2(uuid,text,integer,text,jsonb,jsonb)', 'EXECUTE') THEN
    RAISE EXCEPTION 'case 2: administrator save RPC execute grant was removed';
  END IF;
  PERFORM public.save_inquiry_proforma_v2(
    'cccccccc-0000-4000-8000-000000000001', 'test-key', 0, 'AED', '[]'::jsonb, '[]'::jsonb
  );
END $$;
ROLLBACK;
SELECT 'case 2 ok: authorized admin retains PI reads and save RPC access' AS result;

-- 3. Authorized auditor retains read-only PI and internal audit access.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000003')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE p integer; l integer; s integer; saves integer; groups integer;
BEGIN
  SELECT count(*) INTO p FROM public.inquiry_proformas;
  SELECT count(*) INTO l FROM public.inquiry_proforma_lines;
  SELECT count(*) INTO s FROM public.inquiry_proforma_shares;
  SELECT count(*) INTO saves FROM public.inquiry_proforma_saves;
  SELECT count(*) INTO groups FROM public.inquiry_proforma_groups;
  IF p <> 1 OR l <> 1 OR s <> 1 OR saves <> 1 OR groups <> 1 THEN
    RAISE EXCEPTION 'case 3: auditor read changed (p %, l %, s %, saves %, groups %)', p, l, s, saves, groups;
  END IF;
  BEGIN
    PERFORM public.save_inquiry_proforma_v2(
      'cccccccc-0000-4000-8000-000000000001', 'test-key', 0, 'AED', '[]'::jsonb, '[]'::jsonb
    );
    RAISE EXCEPTION 'case 3: auditor was allowed to save';
  EXCEPTION WHEN insufficient_privilege THEN NULL;
  END;
END $$;
ROLLBACK;
SELECT 'case 3 ok: auditor reads remain; staff PI save remains administrator-only' AS result;

-- 4. A signed-in customer cannot create a share or use the admin-only RPC.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000001')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  BEGIN
    PERFORM public.share_inquiry_proforma('bbbbbbbb-0000-4000-8000-000000000001');
    RAISE EXCEPTION 'case 4: customer share RPC unexpectedly succeeded';
  EXCEPTION WHEN insufficient_privilege THEN NULL;
  END;
  BEGIN
    PERFORM public.save_inquiry_proforma_v2(
      'cccccccc-0000-4000-8000-000000000001', 'customer-key', 4, 'AED', '[]'::jsonb, '[]'::jsonb
    );
    RAISE EXCEPTION 'case 4: customer save RPC unexpectedly succeeded';
  EXCEPTION WHEN insufficient_privilege THEN NULL;
  END;
  BEGIN
    UPDATE public.inquiry_proformas SET status = 'Draft'
     WHERE id = 'bbbbbbbb-0000-4000-8000-000000000001';
    RAISE EXCEPTION 'case 4: customer direct mutation unexpectedly succeeded';
  EXCEPTION WHEN insufficient_privilege THEN NULL;
  END;
END $$;
ROLLBACK;
SELECT 'case 4 ok: customer cannot share, invoke staff save, or directly mutate priced PI' AS result;

-- 5. An administrator cannot create a new customer-priced PI share either.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000002')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  BEGIN
    PERFORM public.share_inquiry_proforma('bbbbbbbb-0000-4000-8000-000000000001');
    RAISE EXCEPTION 'case 5: retired sharing RPC unexpectedly succeeded';
  EXCEPTION WHEN SQLSTATE '0A000' THEN NULL;
  END;
END $$;
ROLLBACK;
SELECT 'case 5 ok: database fails closed on new customer pricing shares' AS result;

-- 6. Existing shares are recorded as migration-withdrawn and never deleted.
BEGIN;
RESET ROLE;
DO $$
DECLARE n integer; withdrawn timestamptz; actor uuid;
BEGIN
  SELECT count(*) INTO n FROM public.inquiry_proforma_shares
   WHERE proforma_id = 'bbbbbbbb-0000-4000-8000-000000000001';
  SELECT revoked_at, revoked_by INTO withdrawn, actor FROM public.inquiry_proforma_shares
   WHERE proforma_id = 'bbbbbbbb-0000-4000-8000-000000000001';
  IF n <> 1 OR withdrawn IS NULL OR actor IS NOT NULL THEN
    RAISE EXCEPTION 'case 6: legacy audit row missing/malformed (n %, revoked %, actor %)', n, withdrawn, actor;
  END IF;
END $$;
ROLLBACK;
SELECT 'case 6 ok: legacy share audit row retained and marked withdrawn by migration' AS result;

-- 7. An authenticated account with no inquiry role sees no PI or internal pricing records.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000004')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE p integer; l integer; s integer; saves integer; groups integer;
BEGIN
  SELECT count(*) INTO p FROM public.inquiry_proformas;
  SELECT count(*) INTO l FROM public.inquiry_proforma_lines;
  SELECT count(*) INTO s FROM public.inquiry_proforma_shares;
  SELECT count(*) INTO saves FROM public.inquiry_proforma_saves;
  SELECT count(*) INTO groups FROM public.inquiry_proforma_groups;
  IF p <> 0 OR l <> 0 OR s <> 0 OR saves <> 0 OR groups <> 0 THEN
    RAISE EXCEPTION 'case 7: no-role account saw p %, l %, s %, saves %, groups %', p, l, s, saves, groups;
  END IF;
END $$;
ROLLBACK;
SELECT 'case 7 ok: no-role account cannot read PI, share, save or pricing group records' AS result;

-- 8. RLS is enabled and direct table mutation remains unavailable to authenticated users.
DO $$
BEGIN
  IF EXISTS (
    SELECT 1
      FROM (VALUES
        ('public.inquiry_proformas'),
        ('public.inquiry_proforma_lines'),
        ('public.inquiry_proforma_shares'),
        ('public.inquiry_proforma_saves'),
        ('public.inquiry_proforma_groups')
      ) AS tables(table_name)
     WHERE has_table_privilege('authenticated', table_name, 'INSERT')
        OR has_table_privilege('authenticated', table_name, 'UPDATE')
        OR has_table_privilege('authenticated', table_name, 'DELETE')
        OR has_table_privilege('anon', table_name, 'SELECT')
  ) THEN
    RAISE EXCEPTION 'case 8: direct table mutation or anonymous SELECT grant remains';
  END IF;
  IF EXISTS (
    SELECT 1 FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'public'
      AND c.relname IN ('inquiry_proformas', 'inquiry_proforma_lines', 'inquiry_proforma_shares', 'inquiry_proforma_saves', 'inquiry_proforma_groups')
      AND NOT c.relrowsecurity
  ) THEN
    RAISE EXCEPTION 'case 8: row-level security is disabled on a PI table';
  END IF;
END $$;
SELECT 'case 8 ok: all five PI tables have RLS; no direct authenticated DML or anon SELECT' AS result;
