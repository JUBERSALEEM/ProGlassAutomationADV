#!/usr/bin/env node
/**
 * Read-only Windows publisher for the private inquiry projection.
 * Dry-run is the default. Pass --publish only after Phase 3 policies have been
 * applied and verified in the target Supabase project.
 */
import { createHash, randomUUID } from "node:crypto";
import { existsSync, lstatSync, realpathSync } from "node:fs";
import { mkdir, readFile, stat, writeFile } from "node:fs/promises";
import { basename, dirname, extname, isAbsolute, join, relative, resolve, sep } from "node:path";
import { DatabaseSync } from "node:sqlite";
import { createClient } from "@supabase/supabase-js";
import { unzipSync } from "fflate";
import {
  INQUIRY_DOCUMENT_BUCKET,
  inquiryDocumentObjectKey,
  sha256Hex,
  validateInquiryDocumentCandidate,
} from "../src/lib/inquiry-documents.ts";

const publish = process.argv.includes("--publish");
const root = resolve(process.env.PROGLASS_DATA_ROOT || "");
const sourceDatasetId = (process.env.INQUIRY_SOURCE_DATASET_ID || "").toLowerCase();
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
if (!process.env.PROGLASS_DATA_ROOT || !existsSync(root)) throw new Error("PROGLASS_DATA_ROOT must be an existing authoritative data root.");
if (!UUID.test(sourceDatasetId)) throw new Error("INQUIRY_SOURCE_DATASET_ID must be a canonical UUID assigned to this data root.");
if (/ProGlassAutomation\.Demo/i.test(root)) throw new Error("Demo data roots are never published.");

const dbPath = join(root, "inquiries.db");
if (!existsSync(dbPath)) throw new Error("The authoritative inquiries.db was not found.");
const checkpointPath = resolve(process.env.INQUIRY_BRIDGE_CHECKPOINT || join(root, ".proglass-web-bridge", "checkpoint.json"));
const allowedTree = resolve(root, "Inquiries") + sep;
const maxRows = Math.min(100_000, Math.max(1, Number(process.env.INQUIRY_BRIDGE_MAX_ROWS) || 25_000));

function deterministicUuid(value) {
  const bytes = createHash("sha256").update(value).digest().subarray(0, 16);
  bytes[6] = (bytes[6] & 0x0f) | 0x50;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = bytes.toString("hex");
  return `${hex.slice(0,8)}-${hex.slice(8,12)}-${hex.slice(12,16)}-${hex.slice(16,20)}-${hex.slice(20)}`;
}
function canonical(value) {
  if (Array.isArray(value)) return value.map(canonical);
  if (value && typeof value === "object") return Object.fromEntries(Object.keys(value).sort().map((key) => [key, canonical(value[key])]));
  return value;
}
function fingerprint(value) { return sha256Hex(Buffer.from(JSON.stringify(canonical(value)))); }
function iso(value) {
  if (!value) return null;
  const date = new Date(String(value));
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}
function mimeFromSignature(extension) {
  const map = { ".pdf":"application/pdf", ".jpg":"image/jpeg", ".jpeg":"image/jpeg", ".png":"image/png", ".gif":"image/gif", ".webp":"image/webp", ".bmp":"image/bmp", ".csv":"text/csv", ".xlsx":"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" };
  return map[extension] || "application/octet-stream";
}
function deepValidate(extension, bytes) {
  if (extension === ".csv") { new TextDecoder("utf-8", { fatal: true }).decode(bytes); return true; }
  if (extension === ".xlsx") {
    const files = unzipSync(bytes, { filter: (file) => file.name === "[Content_Types].xml" || file.name === "xl/workbook.xml" || /vbaProject\.bin$/i.test(file.name) });
    if (!files["[Content_Types].xml"] || !files["xl/workbook.xml"] || Object.keys(files).some((name) => /vbaProject\.bin$/i.test(name))) throw new Error("XLSX is missing required entries or contains macros.");
    return true;
  }
  return undefined;
}
function safeResolvedPath(inquiryId, attachment) {
  const folder = resolve(root, "Inquiries", String(inquiryId), "Attachments");
  const raw = String(attachment.StoragePath || attachment.storagePath || "");
  const fallbackName = basename(raw || String(attachment.FileName || attachment.fileName || ""));
  const candidates = [raw && isAbsolute(raw) ? resolve(raw) : "", fallbackName ? resolve(folder, fallbackName) : ""].filter(Boolean);
  for (const candidate of candidates) {
    if (!candidate.startsWith(allowedTree) || relative(folder, candidate).startsWith("..") || !existsSync(candidate)) continue;
    const info = lstatSync(candidate);
    if (!info.isFile() || info.isSymbolicLink()) continue;
    const real = realpathSync(candidate);
    if (!real.startsWith(allowedTree) || relative(folder, real).startsWith("..")) continue;
    return real;
  }
  return null;
}
async function manifestFor(inquiryId) {
  const path = join(root, "Inquiries", String(inquiryId), "Attachments", "manifest.json");
  if (!existsSync(path)) return [];
  const parsed = JSON.parse(await readFile(path, "utf8"));
  if (!Array.isArray(parsed)) throw new Error("Manifest root must be an array.");
  return parsed;
}
async function checkpoint() {
  try { return JSON.parse(await readFile(checkpointPath, "utf8")); } catch { return { lastCompletedRevision: 0 }; }
}
async function saveCheckpoint(value) {
  await mkdir(dirname(checkpointPath), { recursive: true });
  await writeFile(checkpointPath, JSON.stringify(value, null, 2), { encoding: "utf8", mode: 0o600 });
}

const sqlite = new DatabaseSync(dbPath, { readOnly: true });
sqlite.exec("PRAGMA query_only=ON; PRAGMA busy_timeout=5000;");
const rows = sqlite.prepare(`SELECT Id, InquiryNumber, InquiryReference, CustomerName, CustomerLocation,
  ProjectName, ProjectDescription, SalesmanName, PreparedByName, Status, Priority, InquiryValue,
  ReceivedAt, PreparedAt, SentAt, ConfirmedAt, UpdatedAt, ProformaNumber, JobOrderNumber
  FROM Inquiries ORDER BY Id LIMIT ?`).all(maxRows + 1);
sqlite.close();
if (rows.length > maxRows) throw new Error(`Safety limit exceeded (${maxRows}); no partial scan will be published.`);

const prepared = [];
const warnings = [];
for (const row of rows) {
  let manifest;
  try { manifest = await manifestFor(row.Id); } catch (error) { warnings.push(`Inquiry ${row.Id}: unreadable manifest (${error.message})`); manifest = []; }
  const documents = [];
  for (const attachment of manifest) {
    try {
      const path = safeResolvedPath(row.Id, attachment);
      if (!path) throw new Error("file is missing or escapes the authorized inquiry tree");
      const content = new Uint8Array(await readFile(path));
      const filename = String(attachment.FileName || attachment.fileName || basename(path));
      const extension = extname(filename).toLowerCase();
      const mime = mimeFromSignature(extension);
      const hash = sha256Hex(content);
      const validation = validateInquiryDocumentCandidate({ filename, byteSize: content.byteLength, declaredMime: mime, detectedMime: mime, sha256: hash, signature: content.subarray(0, 32), deepContentValidated: deepValidate(extension, content) });
      if (!validation.ok) throw new Error(`${validation.code}: ${validation.message}`);
      const sourceAttachmentId = Number(attachment.Id || attachment.id);
      if (!Number.isSafeInteger(sourceAttachmentId) || sourceAttachmentId <= 0) throw new Error("invalid source attachment ID");
      const fileStat = await stat(path);
      documents.push({ sourceAttachmentId, filename: validation.normalizedFilename, extension: validation.extension, mime, hash, content, sourceUploadedAt: iso(attachment.UploadedAt || attachment.uploadedAt), sourceModifiedAt: fileStat.mtime.toISOString() });
    } catch (error) { warnings.push(`Inquiry ${row.Id} attachment: ${error.message}`); }
  }
  const metadata = {
    sourceInquiryId: Number(row.Id), inquiryNumber: String(row.InquiryNumber || ""), inquiryReference: String(row.InquiryReference || ""),
    customerName: String(row.CustomerName || ""), customerLocation: String(row.CustomerLocation || ""), projectName: String(row.ProjectName || ""),
    projectDescription: String(row.ProjectDescription || ""), salesmanName: String(row.SalesmanName || ""), preparedByName: String(row.PreparedByName || ""),
    status: String(row.Status || "Received"), priority: String(row.Priority || "Normal"), inquiryValue: Number(row.InquiryValue || 0),
    receivedAt: iso(row.ReceivedAt), preparedAt: iso(row.PreparedAt), sentAt: iso(row.SentAt), confirmedAt: iso(row.ConfirmedAt),
    sourceUpdatedAt: iso(row.UpdatedAt), proformaNumber: String(row.ProformaNumber || ""), jobOrderNumber: String(row.JobOrderNumber || ""),
  };
  if (!metadata.receivedAt || !metadata.sourceUpdatedAt) { warnings.push(`Inquiry ${row.Id}: invalid required timestamps; skipped`); continue; }
  prepared.push({ metadata, sourceFingerprint: fingerprint(metadata), documents });
}

console.log(JSON.stringify({ mode: publish ? "publish" : "dry-run", sourceDatasetId, inquiries: prepared.length, documents: prepared.reduce((sum, item) => sum + item.documents.length, 0), warnings: warnings.length }, null, 2));
for (const warning of warnings) console.warn(`[inquiry-bridge] ${warning}`);
if (!publish) process.exit(0);

const required = ["NEXT_PUBLIC_SUPABASE_URL", "NEXT_PUBLIC_SUPABASE_ANON_KEY", "INQUIRY_BRIDGE_EMAIL", "INQUIRY_BRIDGE_PASSWORD"];
for (const name of required) if (!process.env[name]) throw new Error(`${name} is required in --publish mode.`);
const client = createClient(process.env.NEXT_PUBLIC_SUPABASE_URL, process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY, { auth: { persistSession: false, autoRefreshToken: false } });
const signedIn = await client.auth.signInWithPassword({ email: process.env.INQUIRY_BRIDGE_EMAIL, password: process.env.INQUIRY_BRIDGE_PASSWORD });
if (signedIn.error || !signedIn.data.user) throw new Error(`Bridge authentication failed: ${signedIn.error?.message || "no session"}`);
const state = await checkpoint();
const revision = Math.max(Number(state.lastCompletedRevision || 0) + 1, Date.now());
const runId = randomUUID();
const begun = await client.rpc("begin_inquiry_sync_run", { p_run_id: runId, p_source_dataset_id: sourceDatasetId, p_source_revision: revision });
if (begun.error) throw new Error(`Could not begin sync: ${begun.error.message}`);
let documentCount = 0;
for (const item of prepared) {
  const m = item.metadata;
  const saved = await client.rpc("publish_inquiry_snapshot", {
    p_run_id: runId, p_source_inquiry_id: m.sourceInquiryId, p_inquiry_number: m.inquiryNumber, p_inquiry_reference: m.inquiryReference,
    p_customer_name: m.customerName, p_customer_location: m.customerLocation, p_project_name: m.projectName, p_project_description: m.projectDescription,
    p_salesman_name: m.salesmanName, p_prepared_by_name: m.preparedByName, p_status: m.status, p_priority: m.priority,
    p_inquiry_value: m.inquiryValue, p_received_at: m.receivedAt, p_prepared_at: m.preparedAt, p_sent_at: m.sentAt,
    p_confirmed_at: m.confirmedAt, p_source_updated_at: m.sourceUpdatedAt, p_proforma_number: m.proformaNumber,
    p_job_order_number: m.jobOrderNumber, p_source_fingerprint: item.sourceFingerprint,
  });
  if (saved.error) throw new Error(`Inquiry ${m.inquiryNumber} failed: ${saved.error.message}`);
  const inquiryId = saved.data;
  for (const document of item.documents) {
    const documentId = deterministicUuid(`${sourceDatasetId}:${m.sourceInquiryId}:${document.sourceAttachmentId}`);
    const versionId = deterministicUuid(`${documentId}:${document.hash}`);
    const objectKey = inquiryDocumentObjectKey({ sourceDatasetId, inquiryId, documentId, versionId });
    const uploaded = await client.storage.from(INQUIRY_DOCUMENT_BUCKET).upload(objectKey, document.content, { contentType: document.mime, upsert: false, cacheControl: "0" });
    if (uploaded.error && !/already exists|duplicate/i.test(uploaded.error.message)) throw new Error(`Upload ${document.filename} failed: ${uploaded.error.message}`);
    const registered = await client.rpc("publish_inquiry_document", {
      p_run_id: runId, p_inquiry_id: inquiryId, p_document_id: documentId, p_version_id: versionId,
      p_source_attachment_id: document.sourceAttachmentId, p_original_filename: document.filename, p_extension: document.extension,
      p_content_sha256: document.hash, p_byte_size: document.content.byteLength, p_detected_mime: document.mime,
      p_storage_object_key: objectKey, p_source_uploaded_at: document.sourceUploadedAt, p_source_modified_at: document.sourceModifiedAt,
    });
    if (registered.error) throw new Error(`Register ${document.filename} failed: ${registered.error.message}`);
    documentCount += 1;
  }
}
const completed = await client.rpc("complete_inquiry_sync_run", {
  p_run_id: runId, p_inquiries_seen: prepared.length, p_documents_seen: documentCount, p_warnings_count: warnings.length,
  p_observed_inquiry_ids: prepared.map((item) => item.metadata.sourceInquiryId),
  p_observed_attachment_keys: prepared.flatMap((item) => item.documents.map((document) => `${item.metadata.sourceInquiryId}:${document.sourceAttachmentId}`)),
});
if (completed.error) throw new Error(`Could not complete sync: ${completed.error.message}`);
await saveCheckpoint({ sourceDatasetId, lastCompletedRevision: revision, lastCompletedRunId: runId, completedAt: new Date().toISOString() });
console.log(`[inquiry-bridge] Published ${prepared.length} inquiries and ${documentCount} documents.`);
