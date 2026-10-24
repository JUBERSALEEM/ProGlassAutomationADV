import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const read = (path) => readFileSync(new URL(path, import.meta.url), "utf8");

const matrixSql = read("../supabase-inquiry-role-matrix-phase13.sql");
const matrixCode = matrixSql.replace(/--[^\n]*/g, "");
const selfAccess = read("../supabase-tests/phase13-role-matrix-assertions.sql");
const operatorApi = read("../src/lib/inquiry-submissions-api.ts");
const register = read("../src/components/inquiries/CustomerSubmissionRegister.tsx");
const review = read("../src/components/inquiries/CustomerSubmissionReview.tsx");
const pipeline = read("../src/lib/inquiry-pipeline.ts");
const handoffSql = read("../supabase-inquiry-estimator-phase9e-handoff.sql");

/** Escapes a signature so it can sit inside a RegExp. */
const esc = (value) => value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

const DEFINER = /SECURITY DEFINER\n\s*SET search_path = public, (auth, )?pg_temp/;

test("every new function is definer, path-pinned, closed to anon and open to authenticated", () => {
  const functions = [
    ["inquiry_capabilities_for_roles", "text[]", false],
    ["get_my_inquiry_access_v1", "", false],
    ["inquiry_can_read_submission", "uuid", false],
    ["list_customer_inquiry_inbox_v1", "integer", false],
    ["authorize_customer_inquiry_document_download", "uuid, uuid", false],
    ["update_customer_inquiry_status_v1", "uuid, text, text", false],
    ["list_customer_inquiry_communications_v1", "uuid", false],
    ["acknowledge_customer_inquiry_delivery_v2", "uuid, text, integer, text, jsonb", false],
    ["log_inquiry_operator_role_change", "", true],
  ];
  for (const [name, args, internal] of functions) {
    const signature = esc(args);
    assert.match(matrixSql, new RegExp(`CREATE OR REPLACE FUNCTION public\\.${name}\\(`), `${name} is defined`);
    assert.match(
      matrixSql,
      new RegExp(`REVOKE ALL ON FUNCTION public\\.${name}\\(${signature}\\) FROM PUBLIC, anon${internal ? ", authenticated" : ""};`),
      `${name} is revoked`,
    );
    if (!internal) {
      assert.match(
        matrixSql,
        new RegExp(`GRANT EXECUTE ON FUNCTION public\\.${name}\\(${signature}\\) TO authenticated;`),
        `${name} is granted`,
      );
    }
  }
  // Only the two functions that read across tables carry SECURITY DEFINER with
  // an auth schema on the path; the rest keep the tighter public path.
  assert.match(matrixSql, DEFINER);
  assert.match(matrixSql, /SET search_path = public, auth, pg_temp/);
  assert.doesNotMatch(matrixCode, /SECURITY INVOKER/);
  assert.doesNotMatch(matrixCode, /GRANT[^;]*TO (anon|public)\b/i);
  assert.doesNotMatch(matrixCode, /service_role/i);
});

test("self access reports the caller and nothing about anyone else", () => {
  const body = matrixSql.slice(
    matrixSql.indexOf("CREATE OR REPLACE FUNCTION public.get_my_inquiry_access_v1"),
    matrixSql.indexOf("REVOKE ALL ON FUNCTION public.get_my_inquiry_access_v1"),
  );
  // Identity comes from the JWT and nowhere else.
  assert.match(body, /v_uid\s+uuid := auth\.uid\(\)/);
  assert.match(body, /FROM auth\.users u WHERE u\.id = v_uid/);
  assert.match(body, /FROM public\.inquiry_operator_roles r\s+WHERE r\.user_id = v_uid AND r\.enabled/);
  // No directory: no unfiltered read of users or role rows, no email parameter.
  assert.doesNotMatch(body, /p_email|p_user_id|p_uid/);
  assert.doesNotMatch(body, /FROM auth\.users\s*(;|$)/);
  // A denied caller gets a sentence, not an empty row.
  assert.match(body, /has no enabled inquiry role/);
  assert.match(body, /grants no inbox capability/);
  assert.match(body, /No Supabase Auth session reached the database/);
});

test("the inbox scope is decided by the database, not by the caller", () => {
  const body = matrixSql.slice(
    matrixSql.indexOf("CREATE OR REPLACE FUNCTION public.list_customer_inquiry_inbox_v1"),
    matrixSql.indexOf("REVOKE ALL ON FUNCTION public.list_customer_inquiry_inbox_v1"),
  );
  // The only parameter is a limit.
  assert.match(matrixSql, /list_customer_inquiry_inbox_v1\(p_limit integer DEFAULT 100\)/);
  assert.doesNotMatch(body, /p_scope|p_role|p_user/);
  assert.match(body, /has_inquiry_role\(ARRAY\['inquiry_admin', 'inquiry_auditor'\]\)/);
  assert.match(body, /v_scope := 'assigned'/);
  assert.match(body, /public\.is_active_inquiry_estimator_assignment\(s\.id\)/);
  assert.match(body, /Inquiry inbox access is not enabled for this account/);
  // An estimator can only ever be in the assigned branch.
  const assignedIndex = body.indexOf("ELSIF public.has_inquiry_role(ARRAY['inquiry_estimator'])");
  const allIndex = body.indexOf("v_scope := 'all'");
  assert.ok(allIndex < assignedIndex, "the global branch is checked first, then the assigned one");
  // The bridge keeps its own plan RPCs; this listing is for people.
  assert.doesNotMatch(body, /inquiry_bridge/);
});

test("inquiry_viewer leaves the intake surfaces", () => {
  for (const policy of [
    "Customers read own inquiry submissions",
    "Customers read own inquiry documents",
    "Customers and operators download inquiry intake documents",
  ]) {
    const start = matrixSql.indexOf(`CREATE POLICY "${policy}"`);
    assert.ok(start > 0, `${policy} is recreated`);
    const statement = matrixSql.slice(start, matrixSql.indexOf(";", matrixSql.indexOf("USING", start)));
    assert.match(statement, /has_inquiry_role\(ARRAY\['inquiry_auditor', 'inquiry_admin'\]\)/, `${policy} drops viewer`);
    assert.doesNotMatch(statement, /inquiry_viewer/, `${policy} keeps no viewer branch`);
    // The customer branch survives in every one of them.
    assert.match(statement, /customer_user_id = auth\.uid\(\)|s\.customer_user_id = auth\.uid\(\)/, `${policy} keeps the owner`);
  }
  const download = matrixSql.slice(
    matrixSql.indexOf("CREATE OR REPLACE FUNCTION public.authorize_customer_inquiry_document_download"),
    matrixSql.indexOf("REVOKE ALL ON FUNCTION public.authorize_customer_inquiry_document_download"),
  );
  assert.doesNotMatch(download, /inquiry_viewer/);
  assert.match(download, /is_active_inquiry_estimator_assignment/);
  // The website gate was narrowed with it, so the page and the database agree.
  assert.match(operatorApi, /\["inquiry_auditor", "inquiry_admin"\]/);
  assert.doesNotMatch(operatorApi, /\["inquiry_viewer", "inquiry_auditor", "inquiry_admin"\]/);
});

test("statements about other people stay out of the client", () => {
  // The estimator and bridge policies are left alone, not rebuilt wider.
  assert.doesNotMatch(matrixCode, /DROP POLICY IF EXISTS "Assigned estimators read complete submissions"/);
  assert.doesNotMatch(matrixCode, /DROP POLICY IF EXISTS "Inquiry bridge reads accepted submissions"/);
  assert.doesNotMatch(matrixCode, /ALTER TABLE public\.inquiry_customer_submissions DISABLE ROW LEVEL SECURITY/);
  assert.doesNotMatch(matrixCode, /DROP POLICY IF EXISTS "Admins auditors and assigned estimators read assignments"/);
  // No bucket is made public and no anon grant appears.
  assert.doesNotMatch(matrixCode, /public\s*=\s*true/i);
  assert.doesNotMatch(matrixCode, /storage\.buckets/i);
});

test("the customer status vocabulary and the pipeline columns keep their separation", () => {
  // The closed status check is never touched and never gains a desktop state.
  assert.doesNotMatch(matrixCode, /inquiry_customer_submissions_status_check/);
  assert.doesNotMatch(matrixCode, /ALTER TABLE public\.inquiry_customer_submissions\s+ADD COLUMN IF NOT EXISTS status/);
  const statusLists = matrixSql.match(/NOT IN \([^)]*\)/g) ?? [];
  for (const list of statusLists) {
    const isPipelineList = list.includes("prepared") || list.includes("sent");
    const isReviewList = list.includes("rejected") || list.includes("closed");
    assert.ok(isPipelineList || isReviewList, `unexpected status list ${list}`);
  }
  // Pipeline writing stays additive and keeps its own event, sourced by caller.
  assert.match(matrixSql, /'source', CASE WHEN public\.has_inquiry_role\(ARRAY\['inquiry_bridge'\]\)/);
  assert.match(matrixSql, /pipeline_updated_by = auth\.uid\(\)::text/);
});

test("acknowledgement checks the ledger before it checks the queue", () => {
  const body = matrixSql.slice(
    matrixSql.indexOf("CREATE OR REPLACE FUNCTION public.acknowledge_customer_inquiry_delivery_v2"),
    matrixSql.indexOf("REVOKE ALL ON FUNCTION public.acknowledge_customer_inquiry_delivery_v2"),
  );
  const ledger = body.indexOf("FROM public.inquiry_customer_deliveries WHERE submission_id = p_submission_id");
  const count = body.indexOf("SELECT count(*) INTO v_expected");
  assert.ok(ledger > 0 && count > 0, "both checks exist");
  assert.ok(ledger < count, "the replay check runs before the expected-count check");
  // Same signature and the same stored columns as the bridge contract.
  assert.match(matrixSql, /acknowledge_customer_inquiry_delivery_v2\(\n\s+p_submission_id uuid,\n\s+p_manifest_sha256 text,\n\s+p_document_count integer,\n\s+p_routing_key text,\n\s+p_assignment_snapshot jsonb\n\)/);
  assert.match(handoffSql, /acknowledge_customer_inquiry_delivery_v2/);
  assert.match(body, /'Conflicting acknowledgement\.'/);
  assert.match(body, /'Document count changed\.'/);
  // A person still cannot acknowledge.
  assert.match(body, /IF NOT public\.has_inquiry_role\(ARRAY\['inquiry_bridge'\]\) THEN[\s\S]*42501/);
});

test("role grants, revocations and handoffs are attributable", () => {
  assert.match(matrixSql, /CREATE TABLE IF NOT EXISTS public\.inquiry_operator_role_audit/);
  assert.match(matrixSql, /action text NOT NULL CHECK \(action IN \('granted', 'updated', 'revoked'\)\)/);
  assert.match(matrixSql, /actor_user_id uuid/);
  assert.match(matrixSql, /actor_role text NOT NULL DEFAULT current_user/);
  assert.match(matrixSql, /ALTER TABLE public\.inquiry_operator_role_audit ENABLE ROW LEVEL SECURITY/);
  assert.match(matrixSql, /CREATE TRIGGER trg_inquiry_operator_role_audit\s+AFTER INSERT OR UPDATE OR DELETE ON public\.inquiry_operator_roles/);
  assert.match(matrixSql, /"Admins and auditors read role audit"/);
  // Handoffs stay attributable through the existing delivery and event rows.
  assert.match(handoffSql, /bridge_user_id/);
  assert.match(handoffSql, /'inbox_delivered'/);
});

test("the operator pages report the handoff state", () => {
  assert.match(pipeline, /export function inquiryHandoffLabel/);
  assert.match(pipeline, /export function inquiryHandoffSummary/);
  assert.match(pipeline, /const HANDOFF_DONE = "Handed to desktop"/);
  assert.match(pipeline, /const HANDOFF_READY = "Ready for desktop"/);
  assert.match(register, /inquiryHandoffLabel\(r\)/);
  assert.match(review, /<b>Desktop handoff:<\/b>/);
  // Time is always the shop's time, whatever the viewer's machine believes.
  assert.match(pipeline, /timeZone = "Asia\/Dubai"/);
});

test("the deterministic database assertions cover the matrix", () => {
  const cases = (selfAccess.match(/case \d+[a-z]? ok:/g) ?? []).length;
  assert.ok(cases >= 30, `expected at least 30 assertion cases, found ${cases}`);
  for (const requirement of [
    "no session reports itself and nothing else",
    "estimator is assigned-scope only",
    "viewer is refused with a reason",
    "disabled bridge row grants nothing",
    "estimator table reads stay assignment-scoped",
    "viewer no longer reads intake rows or objects",
    "customer isolation holds",
    "unassigned estimator cannot download",
    "acknowledgement is idempotent and fails closed",
    "acknowledgement stays closed to an estimator",
    "unassigned estimator cannot write",
    "the role audit is admin and auditor only",
    "anon closed, authenticated open",
    "the customer status check is unchanged",
    "bridge plan RPC unchanged",
  ]) {
    assert.ok(selfAccess.includes(requirement), `missing assertion: ${requirement}`);
  }
  assert.match(selfAccess, /SET LOCAL ROLE authenticated/);
  assert.match(selfAccess, /set_config\('request\.jwt\.claims'/);
});
