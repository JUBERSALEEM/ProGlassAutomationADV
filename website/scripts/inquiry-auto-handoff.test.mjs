import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const read = (p) => readFileSync(new URL(p, import.meta.url), "utf8");

const sql = read("../supabase-inquiry-intake-phaseC9-auto-handoff.sql");
const page = read("../src/app/[locale]/inquiries/page.tsx");
const registerPage = read("../src/app/[locale]/inquiries/register/page.tsx");
const welcome = read("../src/components/inquiries/InquiryWelcome.tsx");
const portal = read("../src/components/inquiries/CustomerInquiryPortal.tsx");
const proformaPage = read("../src/app/[locale]/inquiries/submit/proforma/page.tsx");
const types = read("../src/types/inquiry-submissions.ts");
const landing = read("../src/components/inquiries/InquiryLanding.tsx");
const dashboard = read("../src/components/inquiries/InquiriesDashboard.tsx");
const operationsTabs = read("../src/components/operations/OperationsTabs.tsx");

test("the auto-handoff migration is additive, transactional and rewrites no row", () => {
  assert.match(sql, /^BEGIN;/m);
  assert.match(sql, /^COMMIT;/m);
  // Nothing destructive, nothing widened to the public roles.
  assert.doesNotMatch(sql, /^\s*(?:DELETE\s+FROM|TRUNCATE|DROP\s+TABLE|DROP\s+COLUMN|ALTER\s+COLUMN[^\n]*TYPE)/im);
  assert.doesNotMatch(sql, /GRANT[^\n]*TO\s+(?:PUBLIC|anon)/i);
  assert.doesNotMatch(sql, /service_role/i);
  // No outside-the-function write: every UPDATE lives inside a function body.
  const withoutComments = sql.replace(/^\s*--.*$/gm, "");
  const outsideFunctions = withoutComments.split(/CREATE OR REPLACE FUNCTION/)[0];
  assert.doesNotMatch(outsideFunctions, /\bUPDATE\b/i);
  // The rollback is documented and restores the accepted-only rule.
  assert.match(sql, /ROLLBACK/);
  assert.match(sql, /status='accepted'/);
});

test("a complete submission is deliverable without an acceptance step", () => {
  // The gate, the document plan and the two acknowledgement checks.
  const submittedOrAccepted = sql.match(/status IN \('submitted','accepted'\)/g) ?? [];
  assert.ok(submittedOrAccepted.length >= 4, `expected at least 4 widened predicates, saw ${submittedOrAccepted.length}`);
  assert.match(sql, /WHERE s\.upload_state='complete' AND s\.status IN \('submitted','accepted'\) AND x\.submission_id IS NULL/);
  // The acknowledgement contract itself is untouched.
  assert.match(sql, /\^\[0-9a-f\]\{64\}\$/);
  assert.match(sql, /p_document_count NOT BETWEEN 0 AND 15/);
  assert.match(sql, /Conflicting acknowledgement\.[\s\S]{0,80}23505/);
  assert.match(sql, /v_current <> p_assignment_snapshot/);
  assert.match(sql, /p_routing_key IS DISTINCT FROM v_route/);
});

test("a proforma invoice request waits for a coordinator", () => {
  assert.match(types, /"proforma_invoice"/);
  assert.match(sql, /inquiry_type IN \('quotation','drawing_review','technical','order_handover','proforma_invoice','other'\)/);
  assert.match(sql, /default_inquiry_type IN \('quotation','drawing_review','technical','order_handover','proforma_invoice','other'\)/);
  // Finalize parks only that type, and says so in the customer-visible event.
  assert.match(sql, /status=CASE WHEN v_submission\.inquiry_type='proforma_invoice'\s*\n\s*THEN 'under_review' ELSE 'submitted' END/);
  assert.match(sql, /'desktop_handoff',CASE WHEN v_submission\.inquiry_type='proforma_invoice'/);
  assert.match(sql, /THEN 'held_for_coordinator' ELSE 'ready' END/);
  // The portal offers the type and the dedicated route preselects it.
  assert.match(portal, /CUSTOMER_INQUIRY_TYPES\.map/);
  assert.match(portal, /defaultValue=\{intent\}/);
  assert.match(proformaPage, /intent="proforma_invoice"/);
});

test("the bridge may read the rows and objects of a deliverable submission", () => {
  assert.match(sql, /DROP POLICY IF EXISTS "Inquiry bridge reads accepted submissions"/);
  assert.match(sql, /CREATE POLICY "Inquiry bridge reads deliverable submissions"[\s\S]{0,400}status IN \('submitted','accepted'\)/);
  assert.match(sql, /DROP POLICY IF EXISTS "Inquiry bridge reads accepted documents"/);
  assert.match(sql, /CREATE POLICY "Inquiry bridge reads deliverable documents"[\s\S]{0,400}status IN \('submitted','accepted'\)/);
  assert.match(sql, /DROP POLICY IF EXISTS "Inquiry bridge reads accepted intake objects"/);
  assert.match(sql, /CREATE POLICY "Inquiry bridge reads deliverable intake objects"[\s\S]{0,500}status IN \('submitted','accepted'\)/);
  // The customers and the operators keep their own policies untouched.
  assert.doesNotMatch(sql, /Customers read own inquiry submissions/);
  assert.doesNotMatch(sql, /Customers and operators download inquiry intake documents/);
});

test("the inquiry page is public, and the register has its own address", () => {
  assert.match(page, /InquiryWelcome/);
  assert.doesNotMatch(page, /InquiryLanding|InquiriesDashboard/);
  assert.match(registerPage, /InquiryLanding/);
  // Welcome screen: both ways to submit, sign in, and a staff door.
  assert.match(welcome, /href="\/inquiries\/submit"/);
  assert.match(welcome, /href="\/inquiries\/submit\/proforma"/);
  assert.match(welcome, /href="\/inquiries\/chat"/);
  assert.match(welcome, /href="\/reports\/login\?redirect=%2Finquiries%2Fsubmit"/);
  assert.match(welcome, /href="\/reports\/login\?mode=signup&redirect=%2Finquiries%2Fsubmit"/);
  assert.match(welcome, /href="\/inquiries\/register"/);
  // The register still routes by role and still fails closed.
  assert.match(landing, /classifyInquirySurface\(access\.role, access\.enabled\)/);
  assert.match(landing, /if \(surface === "register"\) return <InquiriesDashboard \/>;/);
  // Inquiries are not the reports chain any more.
  assert.doesNotMatch(dashboard, /OperationsTabs/);
  assert.match(operationsTabs, /href: "\/inquiries\/register"/);
});
