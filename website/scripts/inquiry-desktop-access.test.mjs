import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const read = (path) => readFileSync(new URL(path, import.meta.url), "utf8");

const desktopSql = read("../supabase-inquiry-desktop-access-phase14.sql");
const desktopCode = desktopSql.replace(/--[^\n]*/g, "");
const assertions = read("../supabase-tests/phase14-desktop-access-assertions.sql");
const harness = read("../supabase-tests/phase13-role-matrix-harness.sql");
const matrixSql = read("../supabase-inquiry-role-matrix-phase13.sql");
const bridgeSql = read("../supabase-inquiry-inbox-bridge-phase12.sql");

/** Escapes a signature so it can sit inside a RegExp. */
const esc = (value) => value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

/** The body of one CREATE OR REPLACE FUNCTION, up to its closing tag. */
const bodyOf = (sql, name) => {
  const start = sql.indexOf(`CREATE OR REPLACE FUNCTION public.${name}(`);
  assert.notEqual(start, -1, `${name} is defined`);
  const end = sql.indexOf("$function$;", start);
  return sql.slice(start, end === -1 ? undefined : end);
};

const DELIVERY_FUNCTIONS = [
  ["plan_customer_inquiry_deliveries_v2", "integer"],
  ["plan_customer_inquiry_delivery_documents", "uuid"],
  ["acknowledge_customer_inquiry_delivery_v2", "uuid, text, integer, text, jsonb"],
];

test("all three delivery calls admit the bridge and an enabled admin", () => {
  for (const [name] of DELIVERY_FUNCTIONS) {
    const body = bodyOf(desktopCode, name);
    assert.match(
      body,
      /has_inquiry_role\(ARRAY\['inquiry_bridge', 'inquiry_admin'\]\)/,
      `${name} gates on bridge and admin`,
    );
    assert.match(body, /ERRCODE\s*=\s*'42501'|ERRCODE='42501'/, `${name} keeps the 403 SQLSTATE`);
  }
});

test("no person role other than admin reaches the delivery loop", () => {
  for (const [name] of DELIVERY_FUNCTIONS) {
    const gate = bodyOf(desktopCode, name).match(/has_inquiry_role\(ARRAY\[[^\]]*\]\)/)[0];
    assert.doesNotMatch(gate, /inquiry_estimator/);
    assert.doesNotMatch(gate, /inquiry_auditor/);
    assert.doesNotMatch(gate, /inquiry_viewer/);
  }
});

test("the planning queries are replaced whole, so a stale return shape cannot survive", () => {
  for (const [name, args] of DELIVERY_FUNCTIONS.slice(0, 2)) {
    assert.match(
      desktopCode,
      new RegExp(`DROP FUNCTION IF EXISTS public\\.${name}\\(${esc(args)}\\);`),
      `${name} is dropped before it is recreated`,
    );
    const dropAt = desktopCode.indexOf(`DROP FUNCTION IF EXISTS public.${name}`);
    const createAt = desktopCode.indexOf(`CREATE OR REPLACE FUNCTION public.${name}`);
    assert.ok(dropAt < createAt, `${name} is dropped before it is created`);
  }
  assert.match(desktopCode, /^BEGIN;$/m);
  assert.match(desktopCode, /^COMMIT;$/m);
});

test("every touched function stays definer, path-pinned, closed to anon and open to authenticated", () => {
  const functions = [
    ["inquiry_capabilities_for_roles", "text[]"],
    ["plan_customer_inquiry_deliveries_v2", "integer"],
    ["plan_customer_inquiry_delivery_documents", "uuid"],
    ["acknowledge_customer_inquiry_delivery_v2", "uuid, text, integer, text, jsonb"],
  ];
  for (const [name, args] of functions) {
    const body = bodyOf(desktopSql, name);
    // The capability table is a pure mapping, so it is immutable and not
    // definer. The three delivery calls read tables, so they are definer.
    if (name !== "inquiry_capabilities_for_roles") {
      assert.match(body, /SECURITY DEFINER/, `${name} is definer`);
    } else {
      assert.match(body, /IMMUTABLE/, `${name} stays immutable`);
    }
    assert.match(body, /SET search_path = public, pg_temp|SET search_path TO 'public', 'pg_temp'/, `${name} pins the path`);
    assert.match(
      desktopSql,
      new RegExp(`REVOKE ALL ON FUNCTION public\\.${name}\\(${esc(args)}\\) FROM PUBLIC, anon;`),
      `${name} is revoked from PUBLIC and anon`,
    );
    assert.match(
      desktopSql,
      new RegExp(`GRANT EXECUTE ON FUNCTION public\\.${name}\\(${esc(args)}\\) TO authenticated;`),
      `${name} is granted to authenticated`,
    );
  }
});

test("an admin reports the two delivery capabilities and the bridge list is unchanged", () => {
  const caps = bodyOf(desktopCode, "inquiry_capabilities_for_roles");
  assert.match(caps, /\('inquiry_admin',\s*'inbox\.plan\.bridge'\)/);
  assert.match(caps, /\('inquiry_admin',\s*'inbox\.acknowledge'\)/);
  const bridgeRows = caps.match(/\('inquiry_bridge',\s*'[^']+'\)/g) ?? [];
  assert.equal(bridgeRows.length, 5, "the bridge keeps exactly five capabilities");
  const original = bodyOf(matrixSql, "inquiry_capabilities_for_roles");
  const originalBridgeRows = original.match(/\('inquiry_bridge',\s*'[^']*'\)/g) ?? [];
  assert.deepEqual(
    [...bridgeRows].sort(),
    [...originalBridgeRows].sort(),
    "the bridge capability list did not move",
  );
  const viewerRows = caps.match(/\('inquiry_viewer'/g) ?? [];
  assert.equal(viewerRows.length, 0, "a viewer gains nothing");
});

test("the migration keeps the privacy promises it inherited", () => {
  assert.doesNotMatch(desktopCode, /service_role/);
  assert.doesNotMatch(desktopCode, /DISABLE ROW LEVEL SECURITY/i);
  assert.doesNotMatch(desktopCode, /storage\.buckets/);
  assert.doesNotMatch(desktopCode, /ALTER TABLE[^;]*status/i);
  assert.doesNotMatch(desktopCode, /authenticated:.*anon/i);
  assert.doesNotMatch(desktopCode, /\bauth\.users\b/);
});

test("the delivery guards that protect customers are restated intact", () => {
  const ack = bodyOf(desktopCode, "acknowledge_customer_inquiry_delivery_v2");
  assert.match(desktopSql, /Idempotency first/, "the replay check still runs before the queue");
  assert.ok(
    ack.indexOf("inquiry_customer_deliveries") < ack.indexOf("Document count changed"),
    "the replay check is decided before the document count is compared",
  );
  assert.match(ack, /Document count changed/);
  assert.match(ack, /Estimator assignment changed/);
  assert.match(ack, /Invalid routing key/);
  assert.match(ack, /customer_inquiry_assignment_snapshot\(p_submission_id\)/, "the route comes from the database");
  const plan = bodyOf(desktopCode, "plan_customer_inquiry_deliveries_v2");
  assert.match(plan, /s\.status='accepted'/);
  assert.match(plan, /s\.upload_state='complete'/);
  assert.match(plan, /x\.submission_id IS NULL/, "a delivered submission is never planned twice");
});

test("the scratch harness mirrors the live plan row, not an older short one", () => {
  const plan = bodyOf(harness, "plan_customer_inquiry_deliveries_v2");
  for (const column of [
    "submission_id", "submission_reference", "submitted_at", "status", "contact_name",
    "company_name", "phone", "customer_reference", "attention_name", "vat_trn_number",
    "salesman_reference", "handover_estimator_name", "project_name", "project_location",
    "inquiry_type", "description", "document_count", "primary_estimator_code",
    "assignment_snapshot", "routing_key",
  ]) {
    assert.match(plan, new RegExp(`\\b${column}\\b`), `the harness exposes ${column}`);
  }
});

test("the assertions cover the widening, the refusals and the untouched layers", () => {
  const cases = assertions.match(/case \d+ ok:/g) ?? [];
  assert.equal(new Set(cases).size, 13, "thirteen cases");
  for (const expected of [
    "the bridge still plans",
    "an admin plans",
    "an auditor is refused",
    "a viewer is refused",
    "an assigned estimator is refused",
    "a disabled bridge role is refused",
    "an account with no role is refused",
    "anon cannot execute",
    "inbox scopes are unchanged",
    "replay returns quietly",
    "attributed to the admin",
  ]) {
    assert.match(assertions, new RegExp(expected.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")), `covers: ${expected}`);
  }
});

test("the website side of the desk is documented with the account decision", () => {
  const doc = read("../docs/PHASE14_DESKTOP_OPERATOR_ACCESS.md");
  assert.match(doc, /inquiry_admin/);
  assert.match(doc, /inquiry_bridge/);
  assert.match(doc, /42501/);
  assert.match(doc, /Rollback/);
  assert.doesNotMatch(doc, /—/, "no em dashes");
});

test("the two Phase 12 contracts are untouched, because Phase 13 already widened them", () => {
  assert.doesNotMatch(desktopSql, /CREATE OR REPLACE FUNCTION public\.update_customer_inquiry_status_v1/);
  assert.doesNotMatch(desktopSql, /CREATE OR REPLACE FUNCTION public\.list_customer_inquiry_communications_v1/);
  assert.match(bridgeSql, /CREATE OR REPLACE FUNCTION public\.update_customer_inquiry_status_v1/);
  assert.match(bridgeSql, /CREATE OR REPLACE FUNCTION public\.list_customer_inquiry_communications_v1/);
});
