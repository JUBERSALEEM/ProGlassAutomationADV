import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { DatabaseSync } from "node:sqlite";
import test from "node:test";

const bridge = new URL("./inquiry-bridge.mjs", import.meta.url).pathname;
const sourceId = "11111111-1111-4111-8111-111111111111";

function fixture({ filename = "quote.pdf", fileBytes = Buffer.from("%PDF-1.7\nfixture\n"), storagePath } = {}) {
  const root = mkdtempSync(join(tmpdir(), "proglass-inquiry-bridge-"));
  const folder = join(root, "Inquiries", "1", "Attachments");
  mkdirSync(folder, { recursive: true });
  const db = new DatabaseSync(join(root, "inquiries.db"));
  db.exec(`CREATE TABLE Inquiries(Id INTEGER PRIMARY KEY, InquiryNumber TEXT, InquiryReference TEXT,
    CustomerName TEXT, CustomerLocation TEXT, ProjectName TEXT, ProjectDescription TEXT,
    SalesmanName TEXT, PreparedByName TEXT, Status TEXT, Priority TEXT, InquiryValue TEXT,
    ReceivedAt TEXT, PreparedAt TEXT, SentAt TEXT, ConfirmedAt TEXT, UpdatedAt TEXT,
    ProformaNumber TEXT, JobOrderNumber TEXT);
    INSERT INTO Inquiries VALUES(1,'INQ-2026-0001','Customer Inquiry','Fixture Customer','Mumbai',
    'Curtain Wall','Controlled test fixture','Sales','Estimator','Received','High','1000',
    '2026-10-15T08:00:00Z',NULL,NULL,NULL,'2026-10-15T09:00:00Z','','');`);
  db.close();
  const path = join(folder, `123_${filename}`);
  writeFileSync(path, fileBytes);
  writeFileSync(join(folder, "manifest.json"), JSON.stringify([{
    Id: 123, InquiryId: 1, FileName: filename, StoragePath: storagePath ?? path,
    UploadedAt: "2026-10-15T09:00:00Z",
  }]));
  return root;
}

function run(root) {
  const stdout = execFileSync(process.execPath, ["--no-warnings", "--experimental-strip-types", bridge], {
    env: { ...process.env, PROGLASS_DATA_ROOT: root, INQUIRY_SOURCE_DATASET_ID: sourceId },
    encoding: "utf8",
  });
  return JSON.parse(stdout);
}

test("dry-run reads a real SQLite row, manifest and valid PDF without remote mutation", () => {
  const root = fixture();
  try {
    const result = run(root);
    assert.deepEqual(result, { mode: "dry-run", sourceDatasetId: sourceId, inquiries: 1, documents: 1, warnings: 0 });
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test("unsupported local attachment is excluded and makes reconciliation unsafe", () => {
  const root = fixture({ filename: "drawing.dwg", fileBytes: Buffer.from("AC1032 fixture") });
  try {
    const result = run(root);
    assert.equal(result.inquiries, 1);
    assert.equal(result.documents, 0);
    assert.equal(result.warnings, 1);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test("manifest path outside the authorized inquiry tree is never read", () => {
  const outside = join(tmpdir(), `proglass-secret-${process.pid}.pdf`);
  writeFileSync(outside, "%PDF-1.7\nsecret\n");
  const root = fixture({ storagePath: outside });
  try {
    const blocked = run(root);
    assert.equal(blocked.documents, 0);
    assert.equal(blocked.warnings, 1);
  } finally {
    rmSync(root, { recursive: true, force: true });
    rmSync(outside, { force: true });
  }
});

test("dry-run requires stable source identity and never accepts a demo root", () => {
  const root = fixture();
  try {
    assert.throws(() => execFileSync(process.execPath, ["--no-warnings", "--experimental-strip-types", bridge], {
      env: { ...process.env, PROGLASS_DATA_ROOT: root, INQUIRY_SOURCE_DATASET_ID: "not-a-uuid" }, stdio: "pipe",
    }), /Command failed/);
    const demo = `${root}.ProGlassAutomation.Demo`;
    mkdirSync(demo);
    assert.throws(() => execFileSync(process.execPath, ["--no-warnings", "--experimental-strip-types", bridge], {
      env: { ...process.env, PROGLASS_DATA_ROOT: demo, INQUIRY_SOURCE_DATASET_ID: sourceId }, stdio: "pipe",
    }), /Command failed/);
    rmSync(demo, { recursive: true, force: true });
  } finally { rmSync(root, { recursive: true, force: true }); }
});
