import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const read = (path) => readFile(new URL(`../${path}`, import.meta.url), "utf8");

const [boundarySql, c7Sql, listRoute, detailRoute, retiredService, retiredComponent, inquiryWelcome, accountWorkspace, sharePanel, shareRoute, staffRoute, attachment] = await Promise.all([
  read("supabase-inquiry-proforma-phaseC11-customer-pricing-boundary.sql"),
  read("supabase-inquiry-proforma-phaseC7-customer-share.sql"),
  read("src/app/api/account/proformas/route.ts"),
  read("src/app/api/account/proformas/[id]/route.ts"),
  read("src/lib/customer-proforma-api.ts"),
  read("src/components/account/CustomerProformas.tsx"),
  read("src/components/inquiries/InquiryWelcome.tsx"),
  read("src/components/account/CustomerAccountWorkspace.tsx"),
  read("src/components/inquiries/proforma/ProformaSharePanel.tsx"),
  read("src/app/api/inquiries/[id]/proforma/share/route.ts"),
  read("src/app/api/inquiries/[id]/proforma/route.ts"),
  read("src/lib/customer-pi-attachments.tsx"),
]);

test("C11 removes customer RLS and replaces it with staff-only PI policies", () => {
  assert.match(boundarySql, /DROP POLICY IF EXISTS "Customers read their shared proforma"/);
  assert.match(boundarySql, /CREATE POLICY "Operators read proformas"[\s\S]*?has_inquiry_role\(ARRAY\['inquiry_admin', 'inquiry_auditor'\]\)/);
  assert.match(boundarySql, /CREATE POLICY "Operators read proforma lines"[\s\S]*?has_inquiry_role\(ARRAY\['inquiry_admin', 'inquiry_auditor'\]\)/);
  assert.match(boundarySql, /DROP POLICY IF EXISTS "Operators read proforma shares" ON public\.inquiry_proforma_shares/);
  assert.match(boundarySql, /CREATE POLICY "Operators read proforma shares"[\s\S]*?has_inquiry_role\(ARRAY\['inquiry_admin', 'inquiry_auditor'\]\)/);
  assert.doesNotMatch(boundarySql, /customer_user_id\s*=\s*auth\.uid\(\)/);
  assert.match(boundarySql, /ALTER TABLE public\.inquiry_proformas ENABLE ROW LEVEL SECURITY/);
  assert.match(boundarySql, /ALTER TABLE public\.inquiry_proforma_lines ENABLE ROW LEVEL SECURITY/);
  assert.match(boundarySql, /ALTER TABLE public\.inquiry_proforma_saves ENABLE ROW LEVEL SECURITY/);
  assert.match(boundarySql, /ALTER TABLE public\.inquiry_proforma_groups ENABLE ROW LEVEL SECURITY/);
  assert.match(boundarySql, /ALTER TABLE public\.inquiry_proforma_shares ENABLE ROW LEVEL SECURITY/);
  assert.match(boundarySql, /REVOKE ALL ON public\.inquiry_proformas, public\.inquiry_proforma_lines,[\s\S]*?FROM PUBLIC, anon, authenticated/);
  assert.match(boundarySql, /GRANT SELECT ON public\.inquiry_proformas, public\.inquiry_proforma_lines,[\s\S]*?TO authenticated/);
});

test("C11 preserves and marks the legacy share audit instead of deleting it", () => {
  assert.match(boundarySql, /ADD COLUMN IF NOT EXISTS revoked_at timestamptz/);
  assert.match(boundarySql, /UPDATE public\.inquiry_proforma_shares[\s\S]*?SET revoked_at = coalesce\(revoked_at, now\(\)\)/);
  assert.match(boundarySql, /revoked_by = coalesce\(revoked_by, auth\.uid\(\)\)/);
  assert.doesNotMatch(boundarySql, /DELETE\s+FROM\s+public\.inquiry_proforma_shares/i);
  assert.match(boundarySql, /share_inquiry_proforma[\s\S]*?USING ERRCODE = '0A000'/);
  assert.match(boundarySql, /CREATE OR REPLACE FUNCTION public\.revoke_inquiry_proforma_share/);
  assert.match(boundarySql, /AND revoked_at IS NULL/);
  assert.match(c7Sql, /CREATE POLICY "Customers read their shared proforma"/);
});

test("both legacy customer endpoints are no-store 410 tombstones with no priced DTO", () => {
  for (const source of [listRoute, detailRoute]) {
    assert.match(source, /status: 410/);
    assert.match(source, /CUSTOMER_PRICING_DISABLED/);
    assert.doesNotMatch(source, /supabase|approved_rate|line_value|total_value|customer-proforma-api/);
  }
  assert.doesNotMatch(retiredService, /supabaseServerWithToken|approved_rate|line_value|total_value|ProformaLine|ProformaRecord/);
  assert.match(retiredService, /throw new CustomerProformaFailure\(\)/);
  assert.doesNotMatch(retiredComponent, /ProformaDocument|CustomerProformaSummary|approved_rate|line_value|total_value|Print or save as PDF/);
  assert.match(retiredComponent, /Priced proformas are not available in customer accounts/);
});

test("customer navigation and staff share UI no longer advertise priced web PIs", () => {
  assert.doesNotMatch(inquiryWelcome, /\/account\/proformas|View shared proformas/);
  assert.match(inquiryWelcome, /unpriced technical PI/);
  assert.match(inquiryWelcome, /WPF workflow/);
  assert.doesNotMatch(accountWorkspace, /\/account\/proformas|proformas shared with your account/);
  assert.doesNotMatch(sharePanel, /Share revision|Share with the customer/);
  assert.match(sharePanel, /Customer access to priced proformas is disabled/);
});

test("staff PI API remains distinct from the retired customer projection", () => {
  assert.match(staffRoute, /fetchProformaWorkspace/);
  assert.match(staffRoute, /saveProforma/);
  assert.match(shareRoute, /readProformaShare/);
  assert.match(shareRoute, /CUSTOMER_PRICING_DISABLED/);
  assert.match(shareRoute, /status: 410/);
  assert.match(shareRoute, /revokeProformaShare/);
});

test("customer PI requests generate one technical-only unpriced Inquiry PDF", () => {
  assert.match(attachment, /UNPRICED INQUIRY — NO RATES, COSTS, MARGINS, PROFIT OR PI VALUE/);
  assert.match(attachment, /export async function generateCustomerPiInquiryPdf/);
  assert.match(attachment, /return new File\(\[pdfBlob\], "ProGlass_PI_Technical_Inquiry\.pdf"/);
  assert.match(attachment, /Glass Specifications/);
  assert.match(attachment, /Cutting List · Dimensions in mm/);
  assert.doesNotMatch(attachment, /createCsv|createXlsx|csvSheet|spreadsheet\/|\.xlsx|\.csv/);
  assert.doesNotMatch(attachment, /approved_rate|line_value|total_value|margin_amount|profit_amount/);
});
