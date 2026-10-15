import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const sql = await readFile(new URL("../supabase-inquiries.sql", import.meta.url), "utf8");

const tables = [
  "inquiry_operator_roles", "inquiry_sources", "inquiry_sync_runs", "inquiries",
  "inquiry_documents", "inquiry_document_versions", "inquiry_document_events",
];

test("every inquiry table enables RLS and anonymous table access is revoked", () => {
  for (const table of tables) {
    assert.match(sql, new RegExp(`ALTER TABLE public\\.${table} ENABLE ROW LEVEL SECURITY;`), table);
  }
  assert.match(sql, /REVOKE ALL ON TABLE[\s\S]+FROM PUBLIC, anon;/);
});

test("authenticated users receive no direct mutation grants", () => {
  assert.match(sql, /REVOKE INSERT, UPDATE, DELETE ON TABLE[\s\S]+FROM authenticated;/);
  assert.doesNotMatch(sql, /GRANT\s+(?:INSERT|UPDATE|DELETE|ALL)\s+ON TABLE[\s\S]+TO authenticated/i);
});

test("bridge publication functions require machine role and assigned source", () => {
  assert.match(sql, /has_inquiry_role\(ARRAY\['inquiry_bridge'\]\)/);
  assert.match(sql, /bridge_user_id = auth\.uid\(\)/);
  assert.match(sql, /p_source_revision <= v_source\.last_source_revision/);
  assert.match(sql, /public\.inquiries\.source_revision < excluded\.source_revision/);
});

test("private bucket is bounded and has no update/delete policy", () => {
  assert.match(sql, /'inquiry-documents', 'inquiry-documents', false, 10485760/);
  assert.match(sql, /Deliberately no storage UPDATE or DELETE policy/);
  assert.doesNotMatch(sql, /CREATE POLICY[^;]+ON storage\.objects\s+FOR (?:UPDATE|DELETE)/i);
});

test("object metadata enforces opaque UUID paths, SHA-256, and 10 MB maximum", () => {
  assert.match(sql, /content_sha256\s+text NOT NULL CHECK \(content_sha256 ~ '\^\[a-f0-9\]\{64\}\$'\)/);
  assert.match(sql, /byte_size\s+bigint NOT NULL CHECK \(byte_size BETWEEN 1 AND 10485760\)/);
  assert.match(sql, /storage_object_key ~ '\^inquiry-documents\//);
});

test("complete-set reconciliation is disabled by any warning", () => {
  assert.match(sql, /IF p_warnings_count = 0 THEN/);
  assert.match(sql, /availability='tombstoned'/);
  assert.match(sql, /availability='source_missing'/);
  assert.match(sql, /complete_scan=\(p_warnings_count=0\)/);
  assert.match(sql, /Observed identity sets do not match sync counts/);
});

test("download authorization is role-gated, availability-gated and audited without exposing a public URL", () => {
  assert.match(sql, /authorize_inquiry_document_download/);
  assert.match(sql, /inquiry_viewer','inquiry_auditor','inquiry_admin/);
  assert.match(sql, /document_access_authorized/);
  assert.doesNotMatch(sql, /createSignedUrl|signed[_ ]url/i);
});

test("migration contains no service-role credential or public bucket", () => {
  assert.doesNotMatch(sql, /service[_ ]role/i);
  assert.doesNotMatch(sql, /'inquiry-documents'\s*,\s*'inquiry-documents'\s*,\s*true/i);
});
