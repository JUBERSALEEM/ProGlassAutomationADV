import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const sql = readFileSync(new URL("../supabase-inquiry-intake.sql", import.meta.url), "utf8");
const api = readFileSync(new URL("../src/lib/inquiry-submissions-api.ts", import.meta.url), "utf8");
const route = readFileSync(new URL("../src/app/api/inquiry-submissions/route.ts", import.meta.url), "utf8");

for (const table of ["inquiry_customer_submissions", "inquiry_customer_documents", "inquiry_customer_events"]) {
  test(`${table} is additive and protected by RLS`, () => {
    assert.match(sql, new RegExp(`CREATE TABLE IF NOT EXISTS public\\.${table}`));
    assert.match(sql, new RegExp(`ALTER TABLE public\\.${table} ENABLE ROW LEVEL SECURITY`));
  });
}

test("customer rows and documents are ownership-bound while operators retain review access", () => {
  assert.match(sql, /customer_user_id=auth\.uid\(\)/);
  assert.match(sql, /has_inquiry_role\(ARRAY\['inquiry_viewer','inquiry_auditor','inquiry_admin'\]\)/);
  assert.match(sql, /Customers read own inquiry submissions/);
  assert.match(sql, /Customers read own inquiry documents/);
});

test("intake bucket is private, bounded and immutable", () => {
  assert.match(sql, /VALUES\('inquiry-intake-documents','inquiry-intake-documents',false,10485760/);
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
  assert.match(api, /CUSTOMER_INQUIRY_MAX_FILES = 5/);
  assert.match(api, /INQUIRY_DOCUMENT_MAX_BYTES/);
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

test("customer document downloads are ownership-authorized, audited and integrity checked", () => {
  assert.match(sql, /authorize_customer_inquiry_document_download/);
  assert.match(sql, /download_authorized/);
  assert.match(api, /authorize_customer_inquiry_document_download/);
  assert.match(api, /createHash\("sha256"\)/);
  assert.match(api, /bytes\.byteLength !== Number\(metadata\.byte_size\)/);
});
