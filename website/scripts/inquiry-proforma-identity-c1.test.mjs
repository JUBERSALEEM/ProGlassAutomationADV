import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const read = (path) => readFile(new URL(`../${path}`, import.meta.url), "utf8");

const migration = await read("supabase-inquiry-proforma-phaseC1.sql");
const phase15 = await read("supabase-inquiry-proforma-phase15.sql");
const intake = await read("supabase-inquiry-intake.sql");
const inquiries = await read("supabase-inquiries.sql");
const report = await read("docs/PHASE_C1_IMPLEMENTATION_REPORT.md");

/**
 * Executable SQL only. The migration's comments explain what it refuses to do,
 * so scanning the raw text would match words that never run.
 */
function stripComments(value) {
  return value
    .replace(/\/\*[\s\S]*?\*\//g, " ")
    .replace(/--[^\n]*/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

const sql = stripComments(migration);

function tableBlock(source, name) {
  const start = source.search(new RegExp(`CREATE TABLE IF NOT EXISTS public\\.${name} \\(`));
  assert.notEqual(start, -1, `${name} table definition is missing`);
  const end = source.indexOf(");", start);
  assert.notEqual(end, -1, `${name} table definition is unterminated`);
  return source.slice(start, end);
}

function constraintBody(name) {
  const marker = `ADD CONSTRAINT ${name}`;
  const start = sql.indexOf(marker);
  assert.notEqual(start, -1, `${name} is not added by the migration`);
  const end = sql.indexOf(";", start);
  return sql.slice(start, end);
}

test("the migration is one transaction and starts with it", () => {
  assert.match(sql, /^BEGIN;/);
  assert.equal(sql.match(/(^|\s)BEGIN;/g)?.length, 1);
  assert.equal(sql.match(/(^|\s)COMMIT;/g)?.length, 1);
  assert.match(sql, /COMMIT;$/);
});

test("the migration changes no data and destroys no object", () => {
  // ON DELETE RESTRICT is a referential action, not a data statement.
  assert.doesNotMatch(sql, /\bDELETE\s+FROM\b/i);
  assert.doesNotMatch(sql, /\bINSERT\s+INTO\b/i);
  assert.doesNotMatch(sql, /\bUPDATE\s+[a-z_."]+\s+SET\b/i);
  assert.doesNotMatch(sql, /\bTRUNCATE\b/i);
  assert.doesNotMatch(sql, /DROP\s+(TABLE|COLUMN|SCHEMA|INDEX|POLICY|FUNCTION|TRIGGER|TYPE|SEQUENCE|VIEW)\b/i);
  assert.doesNotMatch(sql, /\bALTER\s+TYPE\b/i);
});

test("the migration adds no policy, grant, revoke or role decision", () => {
  assert.doesNotMatch(sql, /\bCREATE\s+POLICY\b|\bALTER\s+POLICY\b|\bDROP\s+POLICY\b/i);
  assert.doesNotMatch(sql, /\bGRANT\b|\bREVOKE\b/i);
  assert.doesNotMatch(sql, /ROW\s+LEVEL\s+SECURITY/i);
  assert.doesNotMatch(sql, /\b(CREATE|ALTER|DROP)\s+ROLE\b/i);
});

test("the submission link is an optional, restricted uuid with one live inquiry per submission", () => {
  assert.match(sql, /ALTER TABLE public\.inquiries ADD COLUMN IF NOT EXISTS web_submission_id uuid;/);
  assert.match(
    sql,
    /ADD CONSTRAINT inquiries_web_submission_id_fkey FOREIGN KEY \(web_submission_id\) REFERENCES public\.inquiry_customer_submissions\(id\) ON DELETE RESTRICT;/,
  );
  assert.match(
    sql,
    /CREATE UNIQUE INDEX IF NOT EXISTS inquiries_web_submission_live ON public\.inquiries \(web_submission_id\) WHERE web_submission_id IS NOT NULL AND availability <> 'tombstoned';/,
  );
  assert.doesNotMatch(sql, /web_submission_id uuid NOT NULL/i);
  assert.doesNotMatch(sql, /web_submission_id[^;]*DEFAULT/i);
});

test("the linked tables really key on uuid, so the foreign key is type correct", () => {
  assert.match(tableBlock(inquiries, "inquiries"), /id\s+uuid PRIMARY KEY/);
  assert.match(tableBlock(intake, "inquiry_customer_submissions"), /id\s+uuid PRIMARY KEY/);
  assert.match(tableBlock(intake, "inquiry_customer_submissions"), /submission_reference\s+text NOT NULL UNIQUE/);
});

test("the canonical desktop identity is a unique uuid bound to desktop origin", () => {
  assert.match(sql, /ALTER TABLE public\.inquiry_proformas ADD COLUMN IF NOT EXISTS client_public_id uuid;/);
  assert.match(
    sql,
    /CREATE UNIQUE INDEX IF NOT EXISTS inquiry_proformas_client_public_id ON public\.inquiry_proformas \(client_public_id\) WHERE client_public_id IS NOT NULL;/,
  );
  const identity = constraintBody("inquiry_proformas_client_identity");
  assert.match(identity, /client_public_id IS NULL OR source_system = 'desktop'/);
  assert.doesNotMatch(sql, /client_public_id uuid NOT NULL/i);
});

test("a desktop proforma may carry its published inquiry while the website rule is unchanged", () => {
  const origin = constraintBody("inquiry_proformas_source_identity");

  assert.match(origin, /source_system = 'website' AND inquiry_id IS NOT NULL AND source_proforma_number = ''/);
  assert.match(origin, /source_system = 'desktop' AND source_proforma_number <> ''/);
  assert.doesNotMatch(origin, /desktop' AND inquiry_id IS NULL/);

  // The Phase 15 rule did forbid it, so this is the one deliberate relaxation.
  assert.match(phase15, /source_system = 'desktop' AND inquiry_id IS NULL AND source_proforma_number <> ''/);
});

test("the number rule stores the desktop number verbatim and keeps the website format", () => {
  const rule = constraintBody("inquiry_proformas_proforma_number_origin");

  const patterns = [...rule.matchAll(/proforma_number ~ '([^']+)'/g)].map((match) => match[1]);
  assert.equal(patterns.length, 2, "expected exactly two number patterns, one per origin");

  const web = new RegExp(patterns.find((pattern) => pattern.includes("PI-WEB")));
  const desktop = new RegExp(patterns.find((pattern) => !pattern.includes("PI-WEB")));

  assert.match("PI-WEB-2026-000001", web);
  assert.doesNotMatch("PI-WEB-26-000001", web);
  assert.doesNotMatch("PI-WEB-2026-00001", web);
  assert.doesNotMatch("PI-2026-001", web);

  for (const value of ["PI-2026-001", "INV-42", "ABC-001", "PI-2026-AB12CD", "sales-2026-7"]) {
    assert.match(value, desktop, `${value} must be storable verbatim`);
  }
  for (const value of ["", "PI 2026 001", "PI/2026/001", "A", `PI-${"1".repeat(48)}`]) {
    assert.doesNotMatch(value, desktop, `${value} must be rejected`);
  }
});

test("replacing the inline number check cannot silently no-op", () => {
  assert.match(sql, /FROM pg_constraint c/);
  assert.match(sql, /pg_get_constraintdef\(c\.oid\) ~ 'proforma_number\[\[:space:\]\]\*~'/);
  assert.match(sql, /EXECUTE format\('ALTER TABLE public\.inquiry_proformas DROP CONSTRAINT %I', v_name\)/);
  assert.match(sql, /RAISE EXCEPTION 'An unrecognised proforma_number format check remains/);
});

test("the migration adds no PI-DSK prefix and no later-phase column", () => {
  assert.doesNotMatch(sql, /PI-DSK/);
  for (const later of [
    "source_revision",
    "source_content_hash",
    "source_status",
    "pricing_version",
    "summary",
    "issued_at",
    "document_sha256",
    "superseded_by",
    "inquiry_proforma_revisions",
    "inquiry_proforma_events",
  ]) {
    assert.doesNotMatch(sql, new RegExp(`\\b${later}\\b`), `${later} belongs to C2 or later`);
  }
});

test("existing proforma rows, lines and saves keep their shape", () => {
  assert.doesNotMatch(sql, /inquiry_proforma_lines/);
  assert.doesNotMatch(sql, /inquiry_proforma_saves/);
  assert.doesNotMatch(sql, /approved_rate|total_sqm|line_value|total_value/);
  assert.doesNotMatch(sql, /\bstatus\b/);
  // The only proforma columns this migration names are identity columns.
  const added = [...sql.matchAll(/ADD COLUMN IF NOT EXISTS ([a-z_]+)/g)].map((match) => match[1]);
  assert.deepEqual(added.sort(), ["client_public_id", "web_submission_id"]);
});

test("the implementation report states the rollback and the production position", () => {
  assert.match(report, /^##\s+[0-9.]+\s*Rollback$/m);
  assert.match(report, /not applied to production/i);
  assert.match(report, /supabase-inquiry-proforma-phaseC1\.sql/);
});
