import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const sql = readFileSync(new URL("../supabase-inquiry-intake.sql", import.meta.url), "utf8");
const fieldsPatch = readFileSync(new URL("../supabase-inquiry-intake-phase8a-fields.sql", import.meta.url), "utf8");
const limitsPatch = readFileSync(new URL("../supabase-inquiry-intake-phase8a-limits.sql", import.meta.url), "utf8");
const stagedPatch = readFileSync(new URL("../supabase-inquiry-intake-phase8a-staged-uploads.sql", import.meta.url), "utf8");
const projectsPatch = readFileSync(new URL("../supabase-inquiry-intake-phase8a-customer-projects.sql", import.meta.url), "utf8");
const api = readFileSync(new URL("../src/lib/inquiry-submissions-api.ts", import.meta.url), "utf8");
const route = readFileSync(new URL("../src/app/api/inquiry-submissions/route.ts", import.meta.url), "utf8");
const draftRoute = readFileSync(new URL("../src/app/api/inquiry-submissions/drafts/route.ts", import.meta.url), "utf8");
const uploadRoute = readFileSync(new URL("../src/app/api/inquiry-submissions/[id]/documents/route.ts", import.meta.url), "utf8");
const finalizeRoute = readFileSync(new URL("../src/app/api/inquiry-submissions/[id]/finalize/route.ts", import.meta.url), "utf8");
const profileRoute = readFileSync(new URL("../src/app/api/inquiry-submissions/profile/route.ts", import.meta.url), "utf8");
const projectsRoute = readFileSync(new URL("../src/app/api/inquiry-submissions/projects/route.ts", import.meta.url), "utf8");
const portal = readFileSync(new URL("../src/components/inquiries/CustomerInquiryPortal.tsx", import.meta.url), "utf8");
const nextConfig = readFileSync(new URL("../next.config.ts", import.meta.url), "utf8");

test("staged upload patch is additive, owner-bound, bounded, and explicitly finalized", () => {
  assert.match(stagedPatch, /ADD COLUMN IF NOT EXISTS upload_state/);
  assert.match(stagedPatch, /expected_document_count BETWEEN 0 AND 15/);
  assert.match(stagedPatch, /CREATE OR REPLACE FUNCTION public\.create_customer_inquiry_draft/);
  assert.match(stagedPatch, /CREATE OR REPLACE FUNCTION public\.finalize_customer_inquiry_submission/);
  assert.match(stagedPatch, /customer_user_id=auth\.uid\(\)/);
  assert.match(stagedPatch, /v_count<>v_submission\.expected_document_count/);
  assert.match(stagedPatch, /lower\(original_filename\)=lower\(btrim\(p_original_filename\)\)/);
  assert.match(stagedPatch, /p_byte_size NOT BETWEEN 1 AND 31457280/);
  assert.match(stagedPatch, /upload_state='draft'/);
  assert.match(stagedPatch, /s\.upload_state='complete' AND public\.has_inquiry_role/);
  assert.doesNotMatch(stagedPatch, /FOR (UPDATE|DELETE) TO authenticated|service_role|public\s*=\s*true/i);
});

test("customer profiles and projects are owner-bound and written only through constrained RPCs", () => {
  for (const table of ["inquiry_customer_profiles", "inquiry_customer_projects"]) {
    assert.match(projectsPatch, new RegExp(`CREATE TABLE IF NOT EXISTS public\\.${table}`));
    assert.match(projectsPatch, new RegExp(`ALTER TABLE public\\.${table} ENABLE ROW LEVEL SECURITY`));
  }
  assert.match(projectsPatch, /customer_user_id=auth\.uid\(\)/);
  assert.match(projectsPatch, /REVOKE INSERT,UPDATE,DELETE ON TABLE/);
  assert.match(projectsPatch, /save_customer_inquiry_profile/);
  assert.match(projectsPatch, /save_customer_inquiry_project/);
  assert.match(projectsPatch, /create_customer_inquiry_draft_v2/);
  assert.match(projectsPatch, /p_project_id IS NOT NULL/);
  assert.match(projectsPatch, /project_id uuid REFERENCES public\.inquiry_customer_projects/);
  assert.match(projectsPatch, /inquiry_type IN \('quotation','drawing_review','technical','order_handover','other'\)/);
  assert.doesNotMatch(projectsPatch, /service_role|public\s*=\s*true/i);
});

test("profile and project APIs prefill, persist, associate, and filter customer inquiries", () => {
  assert.match(profileRoute, /getCustomerInquirySetup/);
  assert.match(profileRoute, /saveCustomerInquiryProfile/);
  assert.match(projectsRoute, /saveCustomerInquiryProject/);
  assert.match(api, /create_customer_inquiry_draft_v2/);
  assert.match(api, /p_project_id: projectId \|\| null/);
  assert.match(portal, /Loading your saved details and projects/);
  assert.match(portal, /New project — save from fields below/);
  assert.match(portal, /Filter by project/);
  assert.match(portal, /Filter by inquiry type/);
  assert.match(portal, /Filter by status/);
  for (const writeRoute of [profileRoute,projectsRoute]) assert.match(writeRoute,/sec-fetch-site/);
});

test("staged API uploads one bounded file, finalizes explicitly, and exposes real progress", () => {
  assert.match(draftRoute, /createCustomerInquiryDraft/);
  assert.match(uploadRoute, /CUSTOMER_INQUIRY_MAX_FILE_BYTES \+ 128_000/);
  assert.match(uploadRoute, /uploadCustomerInquiryDocument/);
  assert.match(finalizeRoute, /finalizeCustomerInquiry/);
  for (const stagedRoute of [draftRoute, uploadRoute, finalizeRoute]) {
    assert.match(stagedRoute, /sec-fetch-site/);
    assert.match(stagedRoute, /Bearer/);
  }
  assert.match(api, /files\.length !== 1/);
  assert.match(api, /\.eq\("upload_state", "complete"\)/);
  assert.match(portal, /new XMLHttpRequest\(\)/);
  assert.match(portal, /request\.upload\.onprogress/);
  assert.match(portal, /status: "verifying"/);
  assert.match(portal, /Overall inquiry upload progress/);
  assert.match(portal, /beforeunload/);
});

test("Next proxy accepts one bounded staged document, not the obsolete aggregate payload", () => {
  assert.match(nextConfig, /proxyClientMaxBodySize:\s*["']31mb["']/);
  assert.doesNotMatch(nextConfig, /proxyClientMaxBodySize:\s*["']451mb["']/);
  assert.match(uploadRoute, /CUSTOMER_INQUIRY_MAX_FILE_BYTES \+ 128_000/);
});

for (const table of ["inquiry_customer_submissions", "inquiry_customer_documents", "inquiry_customer_events"]) {
  test(`${table} is additive and protected by RLS`, () => {
    assert.match(sql, new RegExp(`CREATE TABLE IF NOT EXISTS public\\.${table}`));
    assert.match(sql, new RegExp(`ALTER TABLE public\\.${table} ENABLE ROW LEVEL SECURITY`));
  });
}

test("attention, VAT, salesman and handover fields are bounded in fresh and already-applied schemas", () => {
  for (const source of [sql, fieldsPatch]) {
    assert.match(source, /attention_name\s+text NOT NULL DEFAULT '' CHECK \(char_length\(attention_name\) <= 120\)/);
    assert.match(source, /vat_trn_number\s+text NOT NULL DEFAULT '' CHECK \(char_length\(vat_trn_number\) <= 50\)/);
    assert.match(source, /salesman_reference\s+text NOT NULL DEFAULT '' CHECK \(char_length\(salesman_reference\) <= 120\)/);
    assert.match(source, /handover_estimator_name\s+text NOT NULL DEFAULT '' CHECK \(char_length\(handover_estimator_name\) <= 120\)/);
    assert.match(source, /p_attention_name text, p_vat_trn_number text, p_salesman_reference text, p_handover_estimator_name text/);
  }
  assert.match(fieldsPatch, /DROP FUNCTION public\.create_customer_inquiry_submission\(text,text,text,text,text,text,text\)/);
});

test("customer rows and documents are ownership-bound while operators retain review access", () => {
  assert.match(sql, /customer_user_id=auth\.uid\(\)/);
  assert.match(sql, /has_inquiry_role\(ARRAY\['inquiry_viewer','inquiry_auditor','inquiry_admin'\]\)/);
  assert.match(sql, /Customers read own inquiry submissions/);
  assert.match(sql, /Customers read own inquiry documents/);
});

test("intake bucket is private, bounded and immutable", () => {
  assert.match(sql, /VALUES\('inquiry-intake-documents','inquiry-intake-documents',false,31457280/);
  assert.match(limitsPatch, /file_size_limit=31457280/);
  assert.match(limitsPatch, /CHECK \(byte_size BETWEEN 1 AND 31457280\)/);
  assert.match(sql, /Customers upload own inquiry intake documents/);
  assert.match(sql, /storage\.foldername\(name\)\)\[1\]=auth\.uid\(\)::text/);
  assert.doesNotMatch(sql, /CREATE POLICY[^;]+FOR (?:UPDATE|DELETE)/is);
});

test("submission writes use constrained security-definer RPCs rather than table grants", () => {
  assert.match(sql, /create_customer_inquiry_submission[\s\S]+SECURITY DEFINER/);
  assert.match(sql, /register_customer_inquiry_document[\s\S]+SECURITY DEFINER/);
  assert.match(sql, /REVOKE INSERT, UPDATE, DELETE ON TABLE/);
  assert.match(sql, /auth\.uid\(\) IS NULL/);
});

test("server submission handler validates bounded files and never uses service role", () => {
  assert.match(api, /CUSTOMER_INQUIRY_MAX_FILES = 15/);
  assert.match(api, /CUSTOMER_INQUIRY_MAX_FILE_BYTES = 30 \* 1024 \* 1024/);
  assert.match(api, /maxByteSize: CUSTOMER_INQUIRY_MAX_FILE_BYTES/);
  assert.match(api, /validateInquiryDocumentCandidate/);
  assert.match(api, /sha256Hex/);
  assert.match(api, /unzipSync/);
  assert.match(api, /supabaseServerWithToken/);
  assert.doesNotMatch(api, /service[_-]?role/i);
});

test("multipart route requires bearer auth, same-origin submission and no-store responses", () => {
  assert.match(route, /authorization/);
  assert.match(route, /multipart\/form-data/);
  assert.match(route, /crossSite\(request\)/);
  assert.match(route, /sec-fetch-site/);
  assert.match(route, /x-forwarded-host/);
  assert.match(route, /request\.headers\.get\("host"\)/);
  assert.doesNotMatch(route, /request\.nextUrl\.host/);
  assert.match(route, /Cache-Control[^\n]+private, no-store/);
  assert.match(route, /X-Robots-Tag[^\n]+noindex, nofollow/);
});

test("customer form exposes accessible responsive controls and visible dark-mode focus", () => {
  assert.match(portal, /htmlFor=\{id\}/);
  assert.match(portal, /aria-describedby="inquiry-documents-help"/);
  assert.match(portal, /aria-live="polite"/);
  assert.match(portal, /focus-visible:ring-2/);
  assert.match(portal, /caret-brand-300/);
  assert.match(portal, /text-base[^"]+sm:text-sm/);
  assert.match(portal, /sm:grid-cols-2/);
  assert.match(portal, /Add more documents/);
  assert.match(portal, /Remove all/);
  assert.match(portal, /Replace/);
});

test("customer document downloads are ownership-authorized, audited and integrity checked", () => {
  assert.match(sql, /authorize_customer_inquiry_document_download/);
  assert.match(sql, /download_authorized/);
  assert.match(api, /authorize_customer_inquiry_document_download/);
  assert.match(api, /createHash\("sha256"\)/);
  assert.match(api, /bytes\.byteLength !== Number\(metadata\.byte_size\)/);
});
