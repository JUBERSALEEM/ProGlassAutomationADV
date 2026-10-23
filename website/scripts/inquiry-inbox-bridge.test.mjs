import test from "node:test";import assert from "node:assert/strict";import {readFileSync} from "node:fs";
const script=readFileSync(new URL("./inquiry-inbox-bridge.mjs",import.meta.url),"utf8");
const sql=readFileSync(new URL("../supabase-inquiry-intake-phase8c-inbox.sql",import.meta.url),"utf8");
const repair=readFileSync(new URL("../supabase-inquiry-intake-phase8c-event-repair.sql",import.meta.url),"utf8");
test("inbox migration is bridge-only, accepted-only, idempotent and audited",()=>{assert.match(sql,/has_inquiry_role\(ARRAY\['inquiry_bridge'\]\)/);assert.match(sql,/upload_state='complete' AND s\.status='accepted'/);assert.match(sql,/PRIMARY KEY REFERENCES public\.inquiry_customer_submissions/);assert.match(sql,/Conflicting acknowledgement/);assert.match(sql,/Document count changed/);assert.match(sql,/event_type,details,customer_visible/);assert.match(sql,/CHECK \(event_type IN[\s\S]*'inbox_delivered'/);assert.match(repair,/DROP CONSTRAINT IF EXISTS inquiry_customer_events_event_type_check/);assert.match(repair,/CHECK \(event_type IN[\s\S]*'inbox_delivered'/);assert.match(sql,/status='downloaded'/);assert.doesNotMatch(sql,/service_role|public\s*=\s*true/i);});
test("dry-run is a genuine remote plan with no local or remote mutation",()=>{assert.match(script,/remote-plan-dry-run/);assert.match(script,/plan_customer_inquiry_deliveries_v2/);assert.match(script,/if\(!apply\)process\.exit\(0\)/);assert.ok(script.indexOf("if(!apply)process.exit(0)")<script.indexOf("await mkdir(stageRoot"));assert.ok(script.indexOf("if(!apply)process.exit(0)")<script.indexOf("acknowledge_customer_inquiry_delivery"));});
test("apply uses isolated staging, integrity checks, atomic rename and acknowledgement",()=>{assert.match(script,/Inbox must be isolated/);assert.match(script,/\.staging/);assert.match(script,/endsWith\("\.partial"\)/);assert.match(script,/Removed stale interrupted staging folder/);assert.ok(script.indexOf('endsWith(".partial")')<script.indexOf("for(const item of plan)"));assert.match(script,/bytes\.length!==Number\(document\.byte_size\)\|\|hash\(bytes\)!==document\.content_sha256/);assert.match(script,/await rename\(stage,finalPath\)/);assert.ok(script.indexOf("await rename(stage,finalPath)")<script.indexOf('client.rpc("acknowledge_customer_inquiry_delivery_v2"'));assert.match(script,/Existing inbox item conflicts/);assert.match(script,/READY/);assert.doesNotMatch(script,/service.role|SERVICE_ROLE/i);});

const bridgeSql = readFileSync(new URL("../supabase-inquiry-inbox-bridge-phase12.sql", import.meta.url), "utf8");
const bridgeCode = bridgeSql.replace(/--[^\n]*/g, "");

test("the desktop write-back is additive, bridge-only and customer invisible", () => {
  // Four nullable columns, added without a default, so existing rows are untouched.
  for (const column of ["pipeline_status", "pipeline_note", "pipeline_updated_at", "pipeline_updated_by"]) {
    assert.match(bridgeSql, new RegExp(`ADD COLUMN IF NOT EXISTS ${column}`), `${column} is declared`);
  }
  // The status list is the desktop's vocabulary, enforced inside the function
  // rather than by widening the submission CHECK constraint.
  assert.match(bridgeSql, /p_status NOT IN \('prepared', 'sent', 'confirmed', 'hold', 'cancelled'\)/);
  assert.doesNotMatch(bridgeCode, /DROP CONSTRAINT|ALTER COLUMN .* SET NOT NULL|ALTER POLICY|DROP POLICY/);
  assert.doesNotMatch(bridgeCode, /UPDATE public\.inquiry_customer_submissions\s+SET status/);
  // The audit event is written for operators only.
  assert.match(bridgeSql, /'status_changed'/);
  assert.match(bridgeSql, /'source', 'desktop_inbox'/);
  assert.match(bridgeSql, /false\);/);
});

test("the desktop chat pull is read-only and cannot post", () => {
  assert.match(bridgeSql, /CREATE OR REPLACE FUNCTION public\.list_customer_inquiry_communications_v1\(p_submission_id uuid\)/);
  assert.match(bridgeSql, /LANGUAGE plpgsql STABLE SECURITY DEFINER/);
  assert.match(bridgeSql, /LEFT JOIN public\.user_profiles p ON p\.user_id = c\.author_user_id/);
  // No write of any kind on the communications table anywhere in the contract.
  assert.doesNotMatch(bridgeCode, /INSERT INTO public\.inquiry_communications|UPDATE public\.inquiry_communications|DELETE FROM public\.inquiry_communications/);
  assert.doesNotMatch(bridgeCode, /FUNCTION public\.(post|send|create)_customer_inquiry_communication/);
  assert.doesNotMatch(bridgeCode, /global_chat/);
});

test("both bridge contracts gate on the role and close the anon door", () => {
  for (const [fn, signature] of [
    ["update_customer_inquiry_status_v1", "uuid, text, text"],
    ["list_customer_inquiry_communications_v1", "uuid"],
  ]) {
    assert.match(bridgeSql, new RegExp(`FUNCTION public\\.${fn}`), `${fn} is defined`);
    assert.match(
      bridgeSql,
      new RegExp(`REVOKE ALL ON FUNCTION public\\.${fn}\\(${signature}\\) FROM PUBLIC, anon;`),
      `${fn} is closed to anon`,
    );
    assert.match(
      bridgeSql,
      new RegExp(`GRANT EXECUTE ON FUNCTION public\\.${fn}\\(${signature}\\) TO authenticated;`),
      `${fn} is open to signed-in callers`,
    );
  }
  const gates = bridgeSql.match(/has_inquiry_role\(ARRAY\['inquiry_bridge'\]\)/g) ?? [];
  assert.equal(gates.length, 2, "each function checks the role in its own body");
  assert.match(bridgeSql, /RAISE EXCEPTION 'Inquiry bridge role required\.' USING ERRCODE = '42501'/);
});
