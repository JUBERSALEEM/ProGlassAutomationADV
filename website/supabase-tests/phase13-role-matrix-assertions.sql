-- PHASE 13 ROLE MATRIX: DETERMINISTIC ASSERTIONS
--
-- Every case runs as the `authenticated` role with a simulated JWT claim, which
-- is what PostgREST does, so RLS and the execute grants are both exercised.
-- One line is printed per case. Any failure stops the run with a non-zero exit.
-- Cases 22, 23 and 24 step back out of the role because the objects they assert
-- on are deliberately unreadable to `authenticated`.

-- Every case runs as the `authenticated` role with a simulated JWT claim, which
-- is what PostgREST does, so RLS and the execute grants are both exercised.

-- 1. An unsigned caller is told there is no session, and is told nothing else.
BEGIN;
SELECT set_config('request.jwt.claims', '{}', true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE r record;
BEGIN
  SELECT * INTO r FROM public.get_my_inquiry_access_v1();
  IF r.authenticated THEN RAISE EXCEPTION 'case 1: expected unauthenticated'; END IF;
  IF r.user_id IS NOT NULL OR r.email IS NOT NULL OR cardinality(r.roles) <> 0
    THEN RAISE EXCEPTION 'case 1: leaked identity'; END IF;
  IF r.denial_reason NOT LIKE '%No Supabase Auth session%'
    THEN RAISE EXCEPTION 'case 1: reason %', r.denial_reason; END IF;
END $$;
ROLLBACK;
SELECT 'case 1 ok: no session reports itself and nothing else' AS result;

-- 2. The bridge sees its own identity and bridge capabilities.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000001')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE r record;
BEGIN
  SELECT * INTO r FROM public.get_my_inquiry_access_v1();
  IF NOT r.authenticated THEN RAISE EXCEPTION 'case 2: not authenticated'; END IF;
  IF r.email <> 'bridge@example.test' THEN RAISE EXCEPTION 'case 2: email %', r.email; END IF;
  IF r.roles <> ARRAY['inquiry_bridge'] THEN RAISE EXCEPTION 'case 2: roles %', r.roles; END IF;
  IF NOT ('inbox.plan.bridge' = ANY(r.capabilities)) THEN RAISE EXCEPTION 'case 2: caps %', r.capabilities; END IF;
  IF NOT ('inbox.acknowledge' = ANY(r.capabilities)) THEN RAISE EXCEPTION 'case 2: caps %', r.capabilities; END IF;
  IF r.denial_reason IS NOT NULL THEN RAISE EXCEPTION 'case 2: unexpected denial'; END IF;
END $$;
ROLLBACK;
SELECT 'case 2 ok: bridge identity and capabilities' AS result;

-- 3. Admin: all-submission scope plus review and audit.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000002')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE r record;
BEGIN
  SELECT * INTO r FROM public.get_my_inquiry_access_v1();
  IF NOT ('inbox.read.all' = ANY(r.capabilities)) THEN RAISE EXCEPTION 'case 3: caps %', r.capabilities; END IF;
  IF NOT ('audit.read' = ANY(r.capabilities)) THEN RAISE EXCEPTION 'case 3: caps %', r.capabilities; END IF;
  IF NOT ('inbox.review' = ANY(r.capabilities)) THEN RAISE EXCEPTION 'case 3: caps %', r.capabilities; END IF;
END $$;
ROLLBACK;
SELECT 'case 3 ok: admin capabilities' AS result;

-- 4. Estimator gets assigned scope only, never the global read.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000004')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE r record;
BEGIN
  SELECT * INTO r FROM public.get_my_inquiry_access_v1();
  IF NOT ('inbox.read.assigned' = ANY(r.capabilities)) THEN RAISE EXCEPTION 'case 4: caps %', r.capabilities; END IF;
  IF 'inbox.read.all' = ANY(r.capabilities) THEN RAISE EXCEPTION 'case 4: estimator got global read'; END IF;
  IF 'audit.read' = ANY(r.capabilities) THEN RAISE EXCEPTION 'case 4: estimator got audit'; END IF;
END $$;
ROLLBACK;
SELECT 'case 4 ok: estimator is assigned-scope only' AS result;

-- 5. Viewer holds a role but no capability, and is told why.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000006')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE r record;
BEGIN
  SELECT * INTO r FROM public.get_my_inquiry_access_v1();
  IF r.roles <> ARRAY['inquiry_viewer'] THEN RAISE EXCEPTION 'case 5: roles %', r.roles; END IF;
  IF cardinality(r.capabilities) <> 0 THEN RAISE EXCEPTION 'case 5: caps %', r.capabilities; END IF;
  IF r.denial_reason NOT LIKE '%grants no inbox capability%' THEN RAISE EXCEPTION 'case 5: %', r.denial_reason; END IF;
END $$;
ROLLBACK;
SELECT 'case 5 ok: viewer is refused with a reason' AS result;

-- 6. A disabled role is not a role.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000008')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE r record;
BEGIN
  SELECT * INTO r FROM public.get_my_inquiry_access_v1();
  IF cardinality(r.roles) <> 0 THEN RAISE EXCEPTION 'case 6: roles %', r.roles; END IF;
  IF r.denial_reason NOT LIKE '%no enabled inquiry role%' THEN RAISE EXCEPTION 'case 6: %', r.denial_reason; END IF;
END $$;
ROLLBACK;
SELECT 'case 6 ok: disabled bridge row grants nothing' AS result;

-- 7. Self-access is exactly one row: the caller's.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000004')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE n integer; r record;
BEGIN
  SELECT count(*) INTO n FROM public.get_my_inquiry_access_v1();
  IF n <> 1 THEN RAISE EXCEPTION 'case 7: % rows', n; END IF;
  SELECT * INTO r FROM public.get_my_inquiry_access_v1();
  IF r.user_id <> 'aaaaaaaa-0000-4000-8000-000000000004'::uuid THEN RAISE EXCEPTION 'case 7: wrong user'; END IF;
END $$;
ROLLBACK;
SELECT 'case 7 ok: one row, the caller only' AS result;

-- 8. Admin inbox lists both complete submissions, excludes the draft.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000002')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE n integer; s text;
BEGIN
  SELECT count(*), min(inbox_scope) INTO n, s FROM public.list_customer_inquiry_inbox_v1(100);
  IF n <> 2 THEN RAISE EXCEPTION 'case 8: % rows', n; END IF;
  IF s <> 'all' THEN RAISE EXCEPTION 'case 8: scope %', s; END IF;
  IF EXISTS (SELECT 1 FROM public.list_customer_inquiry_inbox_v1(100) WHERE submission_reference = 'WEB-2026-000103')
    THEN RAISE EXCEPTION 'case 8: draft leaked into the inbox'; END IF;
END $$;
ROLLBACK;
SELECT 'case 8 ok: admin inbox is complete-and-only' AS result;

-- 9. The assigned estimator sees the assigned submission and nothing else.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000004')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE n integer; ref text; s text;
BEGIN
  SELECT count(*), min(submission_reference), min(inbox_scope) INTO n, ref, s FROM public.list_customer_inquiry_inbox_v1(100);
  IF n <> 1 THEN RAISE EXCEPTION 'case 9: % rows', n; END IF;
  IF ref <> 'WEB-2026-000101' THEN RAISE EXCEPTION 'case 9: %', ref; END IF;
  IF s <> 'assigned' THEN RAISE EXCEPTION 'case 9: scope %', s; END IF;
END $$;
ROLLBACK;
SELECT 'case 9 ok: estimator inbox is assigned-scope' AS result;

-- 10. An estimator with no assignment sees an empty inbox, not everybody's.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000005')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE n integer;
BEGIN
  SELECT count(*) INTO n FROM public.list_customer_inquiry_inbox_v1(100);
  IF n <> 0 THEN RAISE EXCEPTION 'case 10: unassigned estimator saw % rows', n; END IF;
END $$;
ROLLBACK;
SELECT 'case 10 ok: unrelated estimator sees nothing' AS result;

-- 11-13. Viewer, no-role and unsigned callers are refused by the inbox.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000006')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  BEGIN
    PERFORM * FROM public.list_customer_inquiry_inbox_v1(10);
    RAISE EXCEPTION 'case 11: viewer was allowed';
  EXCEPTION WHEN insufficient_privilege THEN NULL;
  END;
END $$;
ROLLBACK;
SELECT 'case 11 ok: viewer refused by the inbox' AS result;

BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000007')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  BEGIN
    PERFORM * FROM public.list_customer_inquiry_inbox_v1(10);
    RAISE EXCEPTION 'case 12: role-less account was allowed';
  EXCEPTION WHEN insufficient_privilege THEN NULL;
  END;
END $$;
ROLLBACK;
SELECT 'case 12 ok: account with no role refused' AS result;

BEGIN;
SELECT set_config('request.jwt.claims', '{}', true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  BEGIN
    PERFORM * FROM public.list_customer_inquiry_inbox_v1(10);
    RAISE EXCEPTION 'case 13: anonymous was allowed';
  EXCEPTION WHEN insufficient_privilege THEN NULL;
  END;
END $$;
ROLLBACK;
SELECT 'case 13 ok: anonymous refused by the inbox' AS result;

-- 14. RLS still scopes the tables themselves, not just the RPC.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000004')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE subs integer; docs integer;
BEGIN
  SELECT count(*) INTO subs FROM public.inquiry_customer_submissions;
  IF subs <> 1 THEN RAISE EXCEPTION 'case 14: estimator read % submissions', subs; END IF;
  SELECT count(*) INTO docs FROM public.inquiry_customer_documents;
  IF docs <> 1 THEN RAISE EXCEPTION 'case 14: estimator read % documents', docs; END IF;
END $$;
ROLLBACK;
SELECT 'case 14 ok: estimator table reads stay assignment-scoped' AS result;

-- 15. The viewer lost the intake surfaces; the admin kept them.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000006')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE subs integer; docs integer; objs integer;
BEGIN
  SELECT count(*) INTO subs FROM public.inquiry_customer_submissions;
  SELECT count(*) INTO docs FROM public.inquiry_customer_documents;
  SELECT count(*) INTO objs FROM storage.objects;
  IF subs <> 0 OR docs <> 0 OR objs <> 0
    THEN RAISE EXCEPTION 'case 15: viewer saw %/%/%', subs, docs, objs; END IF;
END $$;
ROLLBACK;
SELECT 'case 15 ok: viewer no longer reads intake rows or objects' AS result;

BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000002')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE subs integer; objs integer;
BEGIN
  SELECT count(*) INTO subs FROM public.inquiry_customer_submissions;
  SELECT count(*) INTO objs FROM storage.objects;
  IF subs <> 2 THEN RAISE EXCEPTION 'case 15b: admin read % submissions', subs; END IF;
  IF objs <> 2 THEN RAISE EXCEPTION 'case 15b: admin read % objects', objs; END IF;
END $$;
ROLLBACK;
SELECT 'case 15b ok: admin keeps full read' AS result;

-- 16. A customer is still limited to their own records, draft included.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','11111111-1111-4111-8111-111111111111')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE subs integer; docs integer; foreign_docs integer;
BEGIN
  SELECT count(*) INTO subs FROM public.inquiry_customer_submissions;
  IF subs <> 2 THEN RAISE EXCEPTION 'case 16: customer read % submissions', subs; END IF;
  SELECT count(*) INTO docs FROM public.inquiry_customer_documents;
  IF docs <> 1 THEN RAISE EXCEPTION 'case 16: customer read % documents', docs; END IF;
  SELECT count(*) INTO foreign_docs FROM public.inquiry_customer_documents
   WHERE submission_id = 'ffffffff-0000-4000-8000-000000000002';
  IF foreign_docs <> 0 THEN RAISE EXCEPTION 'case 16: customer read another customer''s document'; END IF;
END $$;
ROLLBACK;
SELECT 'case 16 ok: customer isolation holds' AS result;

-- 17. Download authorization follows the matrix.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000006')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  BEGIN
    PERFORM * FROM public.authorize_customer_inquiry_document_download(
      'ffffffff-0000-4000-8000-000000000001','dddddddd-0000-4000-8000-000000000001');
    RAISE EXCEPTION 'case 17: viewer downloaded';
  EXCEPTION WHEN no_data_found THEN NULL;
  END;
END $$;
ROLLBACK;
SELECT 'case 17 ok: viewer cannot authorize a download' AS result;

BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000005')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  BEGIN
    PERFORM * FROM public.authorize_customer_inquiry_document_download(
      'ffffffff-0000-4000-8000-000000000001','dddddddd-0000-4000-8000-000000000001');
    RAISE EXCEPTION 'case 17b: unassigned estimator downloaded';
  EXCEPTION WHEN no_data_found THEN NULL;
  END;
END $$;
ROLLBACK;
SELECT 'case 17b ok: unassigned estimator cannot download' AS result;

BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000004')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE f text;
BEGIN
  SELECT original_filename INTO f FROM public.authorize_customer_inquiry_document_download(
    'ffffffff-0000-4000-8000-000000000001','dddddddd-0000-4000-8000-000000000001');
  IF f <> 'alpha.pdf' THEN RAISE EXCEPTION 'case 17c: %', f; END IF;
END $$;
ROLLBACK;
SELECT 'case 17c ok: assigned estimator downloads permitted documents' AS result;

BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000003')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE f text;
BEGIN
  SELECT original_filename INTO f FROM public.authorize_customer_inquiry_document_download(
    'ffffffff-0000-4000-8000-000000000002','dddddddd-0000-4000-8000-000000000002');
  IF f <> 'beta.pdf' THEN RAISE EXCEPTION 'case 17d: %', f; END IF;
END $$;
ROLLBACK;
SELECT 'case 17d ok: auditor downloads for reading' AS result;

-- 18. Acknowledgement: happy path, quiet retry, conflict fails closed.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000001')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE v_snap jsonb; rows integer; events integer; st text;
BEGIN
  -- The same shape the plan RPC hands the desktop; the helper itself is not
  -- granted to authenticated, so a caller cannot ask for it directly.
  SELECT t.snap INTO v_snap FROM public.zz_test_snapshot t
   WHERE t.submission_id = 'ffffffff-0000-4000-8000-000000000001';
  PERFORM public.acknowledge_customer_inquiry_delivery_v2(
    'ffffffff-0000-4000-8000-000000000001', repeat('c',64), 1, 'assigned/EST-A', v_snap);
  -- The retry used to raise "Document count changed." here.
  PERFORM public.acknowledge_customer_inquiry_delivery_v2(
    'ffffffff-0000-4000-8000-000000000001', repeat('c',64), 1, 'assigned/EST-A', v_snap);
  -- The ledger is deliberately unreadable to authenticated, so the assertions
  -- about what was written have to step back out of the role.
  EXECUTE 'RESET ROLE';
  SELECT count(*) INTO rows FROM public.inquiry_customer_deliveries;
  SELECT count(*) INTO events FROM public.inquiry_customer_events WHERE event_type = 'inbox_delivered';
  SELECT status INTO st FROM public.inquiry_customer_submissions WHERE id = 'ffffffff-0000-4000-8000-000000000001';
  IF rows <> 1 THEN RAISE EXCEPTION 'case 18: % delivery rows', rows; END IF;
  IF events <> 1 THEN RAISE EXCEPTION 'case 18: % delivered events', events; END IF;
  IF st <> 'downloaded' THEN RAISE EXCEPTION 'case 18: status %', st; END IF;
  BEGIN
    PERFORM public.acknowledge_customer_inquiry_delivery_v2(
      'ffffffff-0000-4000-8000-000000000001', repeat('d',64), 1, 'assigned/EST-A', v_snap);
    RAISE EXCEPTION 'case 18: conflicting acknowledgement accepted';
  EXCEPTION WHEN unique_violation THEN NULL;
  END;
  BEGIN
    PERFORM public.acknowledge_customer_inquiry_delivery_v2(
      'ffffffff-0000-4000-8000-000000000001', repeat('c',64), 2, 'assigned/EST-A', v_snap);
    RAISE EXCEPTION 'case 18: wrong document count accepted';
  EXCEPTION WHEN unique_violation THEN NULL;
  END;
END $$;
ROLLBACK;
SELECT 'case 18 ok: acknowledgement is idempotent and fails closed' AS result;

-- 19. Acknowledgement stays closed to an estimator. Phase 14 widened this
-- to the admin as well, which the Phase 14 assertions cover; the estimator
-- boundary is the one this case keeps guarding.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000004')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  BEGIN
    PERFORM public.acknowledge_customer_inquiry_delivery_v2(
      'ffffffff-0000-4000-8000-000000000001', repeat('c',64), 1, 'assigned/EST-A', '[]'::jsonb);
    RAISE EXCEPTION 'case 19: estimator acknowledged';
  EXCEPTION WHEN insufficient_privilege THEN NULL;
  END;
END $$;
ROLLBACK;
SELECT 'case 19 ok: acknowledgement stays closed to an estimator' AS result;

-- 20. Pipeline write-back follows the matrix and keeps attribution.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000004')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE st text; src text;
BEGIN
  PERFORM public.update_customer_inquiry_status_v1('ffffffff-0000-4000-8000-000000000001','prepared','estimator note');
  SELECT pipeline_status, pipeline_updated_by INTO st, src FROM public.inquiry_customer_submissions
   WHERE id = 'ffffffff-0000-4000-8000-000000000001';
  IF st <> 'prepared' THEN RAISE EXCEPTION 'case 20: %', st; END IF;
  IF src <> 'aaaaaaaa-0000-4000-8000-000000000004' THEN RAISE EXCEPTION 'case 20: attribution %', src; END IF;
END $$;
ROLLBACK;
SELECT 'case 20 ok: assigned estimator writes pipeline status' AS result;

BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000005')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  BEGIN
    PERFORM public.update_customer_inquiry_status_v1('ffffffff-0000-4000-8000-000000000001','prepared','nope');
    RAISE EXCEPTION 'case 20b: unassigned estimator wrote';
  EXCEPTION WHEN insufficient_privilege THEN NULL;
  END;
END $$;
ROLLBACK;
SELECT 'case 20b ok: unassigned estimator cannot write' AS result;

BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000002')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE src text;
BEGIN
  PERFORM public.update_customer_inquiry_status_v1('ffffffff-0000-4000-8000-000000000002','sent','admin');
  SELECT details->>'source' INTO src FROM public.inquiry_customer_events
   WHERE submission_id = 'ffffffff-0000-4000-8000-000000000002' AND event_type = 'status_changed' ORDER BY id DESC LIMIT 1;
  IF src <> 'website_operator' THEN RAISE EXCEPTION 'case 20c: source %', src; END IF;
END $$;
ROLLBACK;
SELECT 'case 20c ok: admin write-back is labelled website_operator' AS result;

-- 21. The chat pull follows the same scope.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000004')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE n integer;
BEGIN
  SELECT count(*) INTO n FROM public.list_customer_inquiry_communications_v1('ffffffff-0000-4000-8000-000000000001');
  IF n <> 1 THEN RAISE EXCEPTION 'case 21: % messages', n; END IF;
END $$;
ROLLBACK;
SELECT 'case 21 ok: assigned estimator reads the thread' AS result;

BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000005')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  BEGIN
    PERFORM * FROM public.list_customer_inquiry_communications_v1('ffffffff-0000-4000-8000-000000000001');
    RAISE EXCEPTION 'case 21b: unassigned estimator read the thread';
  EXCEPTION WHEN insufficient_privilege THEN NULL;
  END;
END $$;
ROLLBACK;
SELECT 'case 21b ok: unassigned estimator cannot read the thread' AS result;

-- 22. Role changes are audited, and only admins and auditors read the log.
BEGIN;
UPDATE public.inquiry_operator_roles SET enabled = false WHERE user_id = 'aaaaaaaa-0000-4000-8000-000000000005';
DELETE FROM public.inquiry_operator_roles WHERE user_id = 'aaaaaaaa-0000-4000-8000-000000000006';
INSERT INTO public.inquiry_operator_roles(user_id, role, enabled) VALUES ('aaaaaaaa-0000-4000-8000-000000000007','inquiry_viewer',true);
DO $$
DECLARE revoked integer; granted integer; deleted integer;
BEGIN
  SELECT count(*) INTO revoked FROM public.inquiry_operator_role_audit WHERE action = 'revoked' AND enabled = false;
  SELECT count(*) INTO granted FROM public.inquiry_operator_role_audit WHERE action = 'granted';
  SELECT count(*) INTO deleted FROM public.inquiry_operator_role_audit WHERE action = 'revoked' AND user_id = 'aaaaaaaa-0000-4000-8000-000000000006';
  IF revoked < 1 THEN RAISE EXCEPTION 'case 22: no revocation row'; END IF;
  IF granted < 1 THEN RAISE EXCEPTION 'case 22: no grant row'; END IF;
  IF deleted < 1 THEN RAISE EXCEPTION 'case 22: no delete row'; END IF;
END $$;
ROLLBACK;
SELECT 'case 22 ok: role changes are audited' AS result;

BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000004')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE n integer;
BEGIN
  SELECT count(*) INTO n FROM public.inquiry_operator_role_audit;
  IF n <> 0 THEN RAISE EXCEPTION 'case 22b: estimator read % audit rows', n; END IF;
END $$;
ROLLBACK;
SELECT 'case 22b ok: the role audit is admin and auditor only' AS result;

-- 23. Nothing here is reachable anonymously.
DO $$
DECLARE fn text;
BEGIN
  FOREACH fn IN ARRAY ARRAY[
    'public.get_my_inquiry_access_v1()',
    'public.list_customer_inquiry_inbox_v1(integer)',
    'public.inquiry_can_read_submission(uuid)',
    'public.inquiry_capabilities_for_roles(text[])',
    'public.update_customer_inquiry_status_v1(uuid,text,text)',
    'public.list_customer_inquiry_communications_v1(uuid)',
    'public.acknowledge_customer_inquiry_delivery_v2(uuid,text,integer,text,jsonb)',
    'public.authorize_customer_inquiry_document_download(uuid,uuid)'
  ] LOOP
    IF has_function_privilege('anon', fn, 'EXECUTE') THEN RAISE EXCEPTION 'case 23: anon can execute %', fn; END IF;
    IF NOT has_function_privilege('authenticated', fn, 'EXECUTE') THEN RAISE EXCEPTION 'case 23: authenticated cannot execute %', fn; END IF;
  END LOOP;
END $$;
SELECT 'case 23 ok: anon closed, authenticated open' AS result;

-- 24. The customer status vocabulary did not gain a desktop state.
DO $$
BEGIN
  BEGIN
    UPDATE public.inquiry_customer_submissions SET status = 'prepared' WHERE id = 'ffffffff-0000-4000-8000-000000000001';
    RAISE EXCEPTION 'case 24: prepared entered the customer status vocabulary';
  EXCEPTION WHEN check_violation THEN NULL;
  END;
END $$;
SELECT 'case 24 ok: the customer status check is unchanged' AS result;

-- 25. The bridge flow is exactly as it was: plan the accepted, undelivered one.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000001')::text, true);
SET LOCAL ROLE authenticated;
DO $$
DECLARE n integer; ref text; route text;
BEGIN
  SELECT count(*), min(submission_reference), min(routing_key) INTO n, ref, route
    FROM public.plan_customer_inquiry_deliveries_v2(25);
  IF n <> 1 THEN RAISE EXCEPTION 'case 25: bridge planned % rows', n; END IF;
  IF ref <> 'WEB-2026-000101' THEN RAISE EXCEPTION 'case 25: %', ref; END IF;
  IF route <> 'assigned/EST-A' THEN RAISE EXCEPTION 'case 25: route %', route; END IF;
END $$;
ROLLBACK;
SELECT 'case 25 ok: bridge plan RPC unchanged' AS result;

-- 26. An estimator still cannot call the bridge plan RPC; the inbox RPC is the
--     path that serves people.
BEGIN;
SELECT set_config('request.jwt.claims', json_build_object('sub','aaaaaaaa-0000-4000-8000-000000000004')::text, true);
SET LOCAL ROLE authenticated;
DO $$
BEGIN
  BEGIN
    PERFORM * FROM public.plan_customer_inquiry_deliveries_v2(25);
    RAISE EXCEPTION 'case 26: estimator reached the bridge plan RPC';
  EXCEPTION WHEN insufficient_privilege THEN NULL;
  END;
END $$;
ROLLBACK;
SELECT 'case 26 ok: the bridge plan RPC stays machine-only' AS result;
