import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const read = (path) => readFile(new URL(`../${path}`, import.meta.url), "utf8");

const api = await read("src/lib/proforma-api.ts");
const route = await read("src/app/api/inquiries/[id]/proforma/route.ts");
const draft = await read("src/lib/proforma-draft.ts");
const workspace = await read("src/components/inquiries/ProformaWorkspace.tsx");
const builder = await read("src/components/inquiries/proforma/ProformaBuilder.tsx");
const specification = await read("src/components/inquiries/proforma/ProformaSpecification.tsx");
const tables = await read("supabase-inquiry-proforma-phase15.sql");
const writeFunction = await read("supabase-inquiry-proforma-phase15-write.sql");
const inbox = await read("src/lib/inquiries-api.ts");

test("the proforma API authorizes inside the data layer, not the page", () => {
  assert.match(api, /client\.auth\.getUser\(token\)/);
  assert.match(api, /from\("inquiry_operator_roles"\)/);
  assert.match(api, /data\?\.enabled !== true/);
  assert.match(api, /role === WRITE_ROLE/);
  assert.match(api, /READ_ROLES\.includes/);
  assert.match(api, /WRITE_ROLE = "inquiry_admin"/);
});

test("the route refuses an unauthenticated or keyless save", () => {
  assert.match(route, /export const dynamic = "force-dynamic"/);
  assert.match(route, /idempotency-key/i);
  assert.match(route, /private, no-store/);
  assert.match(route, /status === 409|code === "CONFLICT"/);
});

test("a save is a revision with an idempotency key", () => {
  assert.match(api, /rpc\("save_inquiry_proforma_v1"/);
  assert.match(api, /p_idempotency_key/);
  assert.match(api, /p_expected_revision/);
  assert.match(api, /isUuid\(input\.idempotency_key\)/);
  assert.match(workspace, /"Idempotency-Key": key/);
  assert.match(workspace, /expected_revision: revision/);
});

test("the write function is definer, pinned and granted only to authenticated", () => {
  assert.match(writeFunction, /SECURITY DEFINER/);
  assert.match(writeFunction, /SET search_path = public, pg_temp/);
  assert.match(writeFunction, /REVOKE ALL ON FUNCTION[\s\S]*FROM PUBLIC, anon/);
  assert.match(writeFunction, /GRANT EXECUTE ON FUNCTION[\s\S]*TO authenticated/);
  assert.match(writeFunction, /has_inquiry_role\(ARRAY\['inquiry_admin'\]\)/);
  assert.match(writeFunction, /p_expected_revision <> 0/);
  assert.match(writeFunction, /ERRCODE = '40001'/);
  assert.match(writeFunction, /ERRCODE = '42501'/);
  assert.doesNotMatch(writeFunction, /SECURITY INVOKER/);
});

test("the tables keep RLS on, deny anon and take reads from the operator roles", () => {
  assert.match(tables, /ALTER TABLE public\.inquiry_proformas ENABLE ROW LEVEL SECURITY/);
  assert.match(tables, /ALTER TABLE public\.inquiry_proforma_lines ENABLE ROW LEVEL SECURITY/);
  assert.match(tables, /ALTER TABLE public\.inquiry_proforma_saves ENABLE ROW LEVEL SECURITY/);
  assert.match(tables, /REVOKE ALL ON public\.inquiry_proformas FROM PUBLIC, anon/);
  assert.match(tables, /REVOKE INSERT, UPDATE, DELETE ON public\.inquiry_proformas FROM authenticated/);
  assert.match(tables, /FOR SELECT TO authenticated\s+USING \(public\.has_inquiry_role\(ARRAY\['inquiry_admin', 'inquiry_auditor'\]\)\)/);
  assert.match(tables, /CREATE UNIQUE INDEX IF NOT EXISTS inquiry_proformas_open_draft/);
  assert.match(tables, /idempotency_key uuid NOT NULL UNIQUE/);
  assert.doesNotMatch(tables, /TO anon/);
});

test("the money is recomputed by the database, not trusted from the client", () => {
  assert.match(writeFunction, /v_sqm := round\(greatest\(/);
  assert.match(writeFunction, /v_line_value := round\(v_total_sqm \* coalesce\(v_line\.rate, 0\), 2\)/);
  assert.match(writeFunction, /approved_rate/);
  assert.match(draft, /export function lineValue/);
  assert.match(draft, /Math\.round\(\(sqmUnits \* rateUnits\) \/ 10000\) \/ 100/);
});

test("no internal cost, margin, profit or optimiser figure reaches the proforma workspace", () => {
  for (const source of [draft, workspace, builder, specification]) {
    assert.doesNotMatch(source, /ProfitLossSurface/);
    assert.doesNotMatch(source, /buildRateAdders/);
    assert.doesNotMatch(source, /outerSheetRates/);
    assert.doesNotMatch(source, /step4RatePerSqm/);
    assert.doesNotMatch(source, /sheetForRef/);
    assert.doesNotMatch(source, /optimizerLimits/);
  }
  // The builder may read the option lists; nothing else touches the rules file.
  assert.doesNotMatch(workspace, /pricingRules/);
  assert.doesNotMatch(specification, /pricingRules/);
  assert.match(builder, /import \{ buildOptions, glassBuilds, type GlassBuildCode \} from "@\/data\/pricingRules"/);
  // The draft may import the customer safe rules and nothing else.
  assert.equal(
    draft.match(/from "@\/data\/pricingRules"/g)?.length,
    1,
  );
  assert.match(draft, /import \{ billablePanelSqm, isFloored, moneyRate, physicalPanelSqm \} from "@\/data\/pricingRules"/);
});

test("the inbox reports a website draft without inventing an estimator", () => {
  assert.match(inbox, /from\("inquiry_proformas"\)/);
  assert.match(inbox, /proforma_draft/);
  assert.match(inbox, /const VIEW_ROLES = \["inquiry_auditor", "inquiry_admin"\]/);
  assert.doesNotMatch(inbox, /inquiry_viewer/);
});

test("no service role key, public bucket or signed url is used for proformas", () => {
  for (const source of [api, route, workspace]) {
    assert.doesNotMatch(source, /service_role/);
    assert.doesNotMatch(source, /getPublicUrl|createSignedUrl|publicUrl/);
  }
});
