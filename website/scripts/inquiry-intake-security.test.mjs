import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const sql = readFileSync(new URL("../supabase-inquiry-intake.sql", import.meta.url), "utf8");
const fieldsPatch = readFileSync(new URL("../supabase-inquiry-intake-phase8a-fields.sql", import.meta.url), "utf8");
const limitsPatch = readFileSync(new URL("../supabase-inquiry-intake-phase8a-limits.sql", import.meta.url), "utf8");
const api = readFileSync(new URL("../src/lib/inquiry-submissions-api.ts", import.meta.url), "utf8");
const route = readFileSync(new URL("../src/app/api/inquiry-submissions/route.ts", import.meta.url), "utf8");
const portal = readFileSync(new URL("../src/components/inquiries/CustomerInquiryPortal.tsx", import.meta.url), "utf8");
const nextConfig = readFileSync(new URL("../next.config.ts", import.meta.url), "utf8");

test("Next middleware preserves the full bounded customer multipart request", () => {
  assert.match(nextConfig, /proxyClientMaxBodySize:\s*["']451mb["']/);
  assert.match(api, /CUSTOMER_INQUIRY_MAX_REQUEST_BYTES\s*=\s*CUSTOMER_INQUIRY_MAX_FILES\s*\*\s*CUSTOMER_INQUIRY_MAX_FILE_BYTES/);
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
