import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const api = await readFile(new URL("../src/lib/inquiries-api.ts", import.meta.url), "utf8");
const download = await readFile(new URL("../src/app/api/inquiries/[id]/documents/[documentId]/route.ts", import.meta.url), "utf8");

// UUID validation prevents PostgREST filters and object lookup from receiving
// arbitrary path text before authorization.
test("opaque inquiry/document IDs are constrained to versioned canonical UUIDs", () => {
  assert.match(api, /\[1-5\]\[0-9a-f\]\{3\}/);
  assert.match(api, /\[89ab\]\[0-9a-f\]\{3\}/);
  assert.match(api, /if \(!isUuid\(inquiryId\) \|\| !isUuid\(documentId\)\)/);
});

test("server data layer verifies Auth user and enabled database-owned roles", () => {
  assert.match(api, /client\.auth\.getUser\(token\)/);
  assert.match(api, /inquiry_operator_roles/);
  assert.match(api, /data\.enabled !== true/);
  assert.match(api, /VIEW_ROLES\.includes/);
});

test("private downloads verify ownership, authorize by RPC, and hash bytes", () => {
  assert.match(api, /eq\("inquiry_id", inquiryId\)/);
  assert.match(api, /authorize_inquiry_document_download/);
  assert.match(api, /createHash\("sha256"\)/);
  assert.match(api, /bytes\.byteLength !== expectedSize/);
  assert.doesNotMatch(api, /createSignedUrl|publicUrl|getPublicUrl/);
});

test("download response is non-inline, non-cacheable, no-sniff and sandboxed", () => {
  assert.match(download, /Content-Disposition.*attachment/);
  assert.match(download, /filename\*=UTF-8/);
  assert.match(download, /private, no-store/);
  assert.match(download, /X-Content-Type-Options.*nosniff/);
  assert.match(download, /Content-Security-Policy.*sandbox/);
});
