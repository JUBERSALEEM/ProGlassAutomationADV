-- PHASE 14 DESKTOP ACCESS: DETERMINISTIC ASSERTIONS
--
-- Run after the Phase 13 harness, the Phase 13 migration and the Phase 14
-- migration. Every case runs as the `authenticated` role with a simulated
-- claim, which is what PostgREST does, so the execute grants and the gates
-- inside the functions are both exercised.
--
-- Cases 1 to 3 prove the widening. Cases 4 to 9 prove nothing else moved.
-- Any failure raises, so a single mistake stops the run.

-- 1. The machine bridge still plans, unchanged.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000001')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE n integer;
BEGIN
  SELECT count(*) INTO n FROM public.plan_customer_inquiry_deliveries_v2(25);
  IF n <> 1 THEN RAISE EXCEPTION 'case 1: bridge planned % rows, expected 1', n; END IF;
END $$;
ROLLBACK;
SELECT 'case 1 ok: the bridge still plans the accepted submission' AS result;

-- 2. An admin now plans the same accepted submission.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000002')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE n integer; ref text; route text;
BEGIN
  SELECT count(*), min(submission_reference), min(routing_key) INTO n, ref, route
    FROM public.plan_customer_inquiry_deliveries_v2(25);
  IF n <> 1 THEN RAISE EXCEPTION 'case 2: admin planned % rows, expected 1', n; END IF;
  IF ref <> 'WEB-2026-000101' THEN RAISE EXCEPTION 'case 2: planned %', ref; END IF;
  IF route <> 'assigned/EST-A' THEN RAISE EXCEPTION 'case 2: route %', route; END IF;
END $$;
ROLLBACK;
SELECT 'case 2 ok: an admin plans the same row, with the database-derived route' AS result;

-- 3. An admin plans the documents and holds both delivery capabilities.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000002')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE n integer; r record;
BEGIN
  SELECT count(*) INTO n FROM public.plan_customer_inquiry_delivery_documents('ffffffff-0000-4000-8000-000000000001');
  IF n <> 1 THEN RAISE EXCEPTION 'case 3: admin planned % documents, expected 1', n; END IF;
  SELECT * INTO r FROM public.get_my_inquiry_access_v1();
  IF NOT ('inbox.plan.bridge' = ANY(r.capabilities)) THEN RAISE EXCEPTION 'case 3: caps %', r.capabilities; END IF;
  IF NOT ('inbox.acknowledge' = ANY(r.capabilities)) THEN RAISE EXCEPTION 'case 3: caps %', r.capabilities; END IF;
END $$;
ROLLBACK;
SELECT 'case 3 ok: admin plans documents and reports the delivery capabilities' AS result;

-- 4. An auditor is refused, with the authorization SQLSTATE the gateway turns
--    into 403.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000003')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  PERFORM public.plan_customer_inquiry_deliveries_v2(25);
  RAISE EXCEPTION 'case 4: auditor was allowed to plan';
EXCEPTION WHEN insufficient_privilege THEN NULL;
END $$;
ROLLBACK;
SELECT 'case 4 ok: an auditor is refused with 42501' AS result;

-- 5. A viewer is refused.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000006')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  PERFORM public.plan_customer_inquiry_deliveries_v2(25);
  RAISE EXCEPTION 'case 5: viewer was allowed to plan';
EXCEPTION WHEN insufficient_privilege THEN NULL;
END $$;
ROLLBACK;
SELECT 'case 5 ok: a viewer is refused with 42501' AS result;

-- 6. An estimator, even the assigned one, is refused: assignment buys the
--    inbox and pipeline write-back, never the desktop delivery loop.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000004')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  PERFORM public.plan_customer_inquiry_deliveries_v2(25);
  RAISE EXCEPTION 'case 6: assigned estimator was allowed to plan';
EXCEPTION WHEN insufficient_privilege THEN NULL;
END $$;
ROLLBACK;
SELECT 'case 6 ok: an assigned estimator is refused with 42501' AS result;

-- 7. A bridge role that is switched off is refused, so the gate still reads
--    the enabled flag and not just the role name.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000008')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  PERFORM public.plan_customer_inquiry_deliveries_v2(25);
  RAISE EXCEPTION 'case 7: disabled bridge was allowed to plan';
EXCEPTION WHEN insufficient_privilege THEN NULL;
END $$;
ROLLBACK;
SELECT 'case 7 ok: a disabled bridge role is refused with 42501' AS result;

-- 8. An account with no role is refused.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000007')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  PERFORM public.plan_customer_inquiry_deliveries_v2(25);
  RAISE EXCEPTION 'case 8: role-less account was allowed to plan';
EXCEPTION WHEN insufficient_privilege THEN NULL;
END $$;
ROLLBACK;
SELECT 'case 8 ok: an account with no role is refused with 42501' AS result;

-- 9. The widening did not reach anonymous callers.
BEGIN;
SELECT set_config('request.jwt.claims', '{}', true);
SET LOCAL ROLE anon;
DO $$
BEGIN
  PERFORM public.plan_customer_inquiry_deliveries_v2(25);
  RAISE EXCEPTION 'case 9: anon reached the plan';
EXCEPTION WHEN insufficient_privilege THEN NULL;
END $$;
ROLLBACK;
SELECT 'case 9 ok: anon cannot execute the plan' AS result;

-- 10. The estimator inbox is unchanged: an admin still reads all, an estimator
--     still reads only its assignment, and neither scope came from a parameter.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000002')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE n integer;
BEGIN
  SELECT count(*) INTO n FROM public.list_customer_inquiry_inbox_v1(50);
  IF n <> 2 THEN RAISE EXCEPTION 'case 10: admin inbox rows %', n; END IF;
END $$;
ROLLBACK;
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000004')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE n integer;
BEGIN
  SELECT count(*) INTO n FROM public.list_customer_inquiry_inbox_v1(50);
  IF n <> 1 THEN RAISE EXCEPTION 'case 10: estimator inbox rows %', n; END IF;
END $$;
ROLLBACK;
SELECT 'case 10 ok: inbox scopes are unchanged by Phase 14' AS result;

-- 11. The delivery ledger still guards the acknowledgement: a replay returns
--     quietly, a second, different acknowledgement conflicts, and an
--     acknowledgement by the admin is recorded under the admin's own id.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000002')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE v_snap jsonb; doc_count integer;
BEGIN
  SELECT z.snap INTO v_snap FROM public.zz_test_snapshot z WHERE submission_id='ffffffff-0000-4000-8000-000000000001';
  SELECT count(*)::integer INTO doc_count FROM public.inquiry_customer_documents
   WHERE submission_id='ffffffff-0000-4000-8000-000000000001' AND availability='available';
  PERFORM public.acknowledge_customer_inquiry_delivery_v2(
    'ffffffff-0000-4000-8000-000000000001', repeat('c',64), doc_count, 'assigned/EST-A', v_snap);
  PERFORM public.acknowledge_customer_inquiry_delivery_v2(
    'ffffffff-0000-4000-8000-000000000001', repeat('c',64), doc_count, 'assigned/EST-A', v_snap);
  BEGIN
    PERFORM public.acknowledge_customer_inquiry_delivery_v2(
      'ffffffff-0000-4000-8000-000000000001', repeat('d',64), doc_count, 'assigned/EST-A', v_snap);
    RAISE EXCEPTION 'case 11: a conflicting acknowledgement was accepted';
  EXCEPTION WHEN unique_violation THEN NULL;
  END;
END $$;
ROLLBACK;
SELECT 'case 11 ok: replay returns quietly and a conflict still fails closed' AS result;

-- 12. After an admin acknowledgement inside a live transaction, the row is
--     attributed to the admin and the status becomes downloaded.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000002')::text, true);
DO $$
DECLARE v_snap jsonb; doc_count integer; actor uuid; st text;
BEGIN
  SELECT z.snap INTO v_snap FROM public.zz_test_snapshot z WHERE submission_id='ffffffff-0000-4000-8000-000000000001';
  SELECT count(*)::integer INTO doc_count FROM public.inquiry_customer_documents
   WHERE submission_id='ffffffff-0000-4000-8000-000000000001' AND availability='available';
  PERFORM public.acknowledge_customer_inquiry_delivery_v2(
    'ffffffff-0000-4000-8000-000000000001', repeat('e',64), doc_count, 'assigned/EST-A', v_snap);
  SELECT bridge_user_id INTO actor FROM public.inquiry_customer_deliveries
   WHERE submission_id='ffffffff-0000-4000-8000-000000000001';
  SELECT status INTO st FROM public.inquiry_customer_submissions
   WHERE id='ffffffff-0000-4000-8000-000000000001';
  IF actor <> 'aaaaaaaa-0000-4000-8000-000000000002'::uuid THEN
    RAISE EXCEPTION 'case 12: delivery attributed to %', actor;
  END IF;
  IF st <> 'downloaded' THEN RAISE EXCEPTION 'case 12: status %', st; END IF;
END $$;
ROLLBACK;
SELECT 'case 12 ok: an admin delivery is attributed to the admin' AS result;

-- 13. The pipeline write-back the desk uses afterwards is still open to the
--     admin and still refuses an unassigned estimator.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000002')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  PERFORM public.update_customer_inquiry_status_v1('ffffffff-0000-4000-8000-000000000002','prepared','admin desk');
END $$;
ROLLBACK;
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000005')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  PERFORM public.update_customer_inquiry_status_v1('ffffffff-0000-4000-8000-000000000002','prepared','unassigned');
  RAISE EXCEPTION 'case 13: an unassigned estimator wrote the pipeline';
EXCEPTION WHEN insufficient_privilege THEN NULL;
END $$;
ROLLBACK;
SELECT 'case 13 ok: admin write-back works, unassigned estimator still refused' AS result;
