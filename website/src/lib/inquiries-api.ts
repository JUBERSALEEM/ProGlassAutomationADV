import { createHash } from "node:crypto";
import { isSupabaseConfigured, isSupabaseUnavailableError, supabaseServerWithToken } from "@/lib/supabase";
import type { SupabaseClient } from "@supabase/supabase-js";
import type { InquiryDetail, InquiryDocumentRow, InquiryListData, InquiryListRow, InquiryPriority, InquiryStatus } from "@/types/inquiries";

const VIEW_ROLES = ["inquiry_viewer", "inquiry_auditor", "inquiry_admin"] as const;
const STATUSES: InquiryStatus[] = ["Received", "Prepared", "Sent", "Confirmed", "Hold", "Cancelled"];
const PRIORITIES: InquiryPriority[] = ["Low", "Normal", "High", "Urgent"];
const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export class InquiryApiFailure extends Error {
  code: "UNAUTHORIZED" | "FORBIDDEN" | "NOT_FOUND" | "NOT_CONFIGURED" | "TEMPORARY_FAILURE" | "INTERNAL_ERROR";
  constructor(code: InquiryApiFailure["code"], message: string) {
    super(message);
    this.code = code;
  }
}

export function isUuid(value: string): boolean { return UUID_PATTERN.test(value); }

function schemaMissing(error: { code?: string; message?: string }): boolean {
  return error.code === "42P01" || error.code === "PGRST205" || /inquiry_(?:operator_roles|documents)|public\.inquiries|schema cache|does not exist/i.test(error.message || "");
}
function authFailure(error: { code?: string; message?: string }): boolean {
  return /jwt|token|expired|unauthorized|permission denied|42501|PGRST301/i.test(`${error.code || ""} ${error.message || ""}`);
}
function databaseFailure(error: { code?: string; message?: string }): never {
  if (isSupabaseUnavailableError(error)) throw new InquiryApiFailure("TEMPORARY_FAILURE", "The inquiry service is temporarily unreachable. Please try again.");
  if (schemaMissing(error)) throw new InquiryApiFailure("NOT_CONFIGURED", "The private inquiry projection is not installed in this deployment.");
  if (authFailure(error)) throw new InquiryApiFailure("UNAUTHORIZED", "Your operator session has expired or is unauthorized.");
  console.error("[inquiries-api] Supabase query failed:", { code: error.code, message: error.message });
  throw new InquiryApiFailure("TEMPORARY_FAILURE", "The inquiry service could not complete this request. Please try again.");
}

async function authorizedClient(token: string): Promise<SupabaseClient> {
  if (!token) throw new InquiryApiFailure("UNAUTHORIZED", "Sign in to access private inquiries.");
  if (!isSupabaseConfigured()) throw new InquiryApiFailure("NOT_CONFIGURED", "Supabase is not configured for the private inquiry workflow.");
  const client = supabaseServerWithToken(token);
  const user = await client.auth.getUser(token);
  if (user.error && isSupabaseUnavailableError(user.error)) throw new InquiryApiFailure("TEMPORARY_FAILURE", "The authentication service is temporarily unreachable. Please try again.");
  if (user.error || !user.data.user) throw new InquiryApiFailure("UNAUTHORIZED", "Your operator session is expired or invalid.");
  const { data, error } = await client.from("inquiry_operator_roles").select("role, enabled").eq("user_id", user.data.user.id).maybeSingle();
  if (error) databaseFailure(error);
  if (!data || data.enabled !== true || !VIEW_ROLES.includes(data.role as typeof VIEW_ROLES[number])) {
    throw new InquiryApiFailure("FORBIDDEN", "Your account does not have an enabled inquiry viewer role.");
  }
  return client;
}

function safeSearch(value: string): string {
  return value.normalize("NFKC").replace(/[^\p{L}\p{N}\s._/-]/gu, " ").replace(/\s+/g, " ").trim().slice(0, 120);
}
function numeric(value: unknown): number {
  const parsed = Number(value); return Number.isFinite(parsed) ? parsed : 0;
}
function listRow(row: Record<string, unknown>, attachmentCount = 0): InquiryListRow {
  return {
    id: String(row.id), inquiry_number: String(row.inquiry_number || ""), inquiry_reference: String(row.inquiry_reference || ""),
    customer_name: String(row.customer_name || ""), project_name: String(row.project_name || ""), salesman_name: String(row.salesman_name || ""),
    prepared_by_name: String(row.prepared_by_name || ""), status: row.status as InquiryStatus, priority: row.priority as InquiryPriority,
    inquiry_value: numeric(row.inquiry_value), received_at: String(row.received_at || ""), source_updated_at: String(row.source_updated_at || ""),
    proforma_number: String(row.proforma_number || ""), job_order_number: String(row.job_order_number || ""),
    availability: row.availability as InquiryListRow["availability"], attachment_count: attachmentCount, published_at: String(row.published_at || ""),
  };
}

export async function fetchInquiries(token: string, input: { page: number; pageSize: number; search: string; status: string; priority: string; hasDocuments: boolean }): Promise<InquiryListData> {
  const client = await authorizedClient(token);
  const page = Math.max(1, input.page); const pageSize = Math.min(100, Math.max(10, input.pageSize));
  const from = (page - 1) * pageSize; const to = from + pageSize - 1;
  const search = safeSearch(input.search);

  const columns = "id,inquiry_number,inquiry_reference,customer_name,project_name,salesman_name,prepared_by_name,status,priority,inquiry_value,received_at,source_updated_at,proforma_number,job_order_number,availability,published_at";
  let query = client.from("inquiries").select(input.hasDocuments ? `${columns},inquiry_documents!inner(id)` : columns, { count: "exact" }).neq("availability", "tombstoned");
  if (STATUSES.includes(input.status as InquiryStatus)) query = query.eq("status", input.status);
  if (PRIORITIES.includes(input.priority as InquiryPriority)) query = query.eq("priority", input.priority);
  if (input.hasDocuments) query = query.eq("inquiry_documents.availability", "available");
  if (search) query = query.or(`inquiry_number.ilike.%${search}%,customer_name.ilike.%${search}%,project_name.ilike.%${search}%,proforma_number.ilike.%${search}%,job_order_number.ilike.%${search}%`);
  query = query.order("source_updated_at", { ascending: false }).range(from, to);
  const result = await query;
  if (result.error) databaseFailure(result.error);
  const rows = (result.data || []) as unknown as Record<string, unknown>[];
  const ids = rows.map((row) => String(row.id));
  const counts = new Map<string, number>();
  if (ids.length) {
    const docs = await client.from("inquiry_documents").select("inquiry_id").in("inquiry_id", ids).eq("availability", "available");
    if (docs.error) databaseFailure(docs.error);
    for (const doc of docs.data || []) counts.set(String(doc.inquiry_id), (counts.get(String(doc.inquiry_id)) || 0) + 1);
  }

  const [active, confirmed, withDocs, source] = await Promise.all([
    client.from("inquiries").select("id", { count: "exact", head: true }).in("status", ["Received", "Prepared", "Sent"]).neq("availability", "tombstoned"),
    client.from("inquiries").select("id", { count: "exact", head: true }).eq("status", "Confirmed").neq("availability", "tombstoned"),
    client.from("inquiry_documents").select("inquiry_id").eq("availability", "available"),
    client.from("inquiry_sources").select("last_success_at").eq("enabled", true).order("last_success_at", { ascending: false, nullsFirst: false }).limit(1).maybeSingle(),
  ]);
  for (const response of [active, confirmed, withDocs, source]) if (response.error) databaseFailure(response.error);
  const withDocumentIds = new Set((withDocs.data || []).map((row) => String(row.inquiry_id)));
  const totalEntries = result.count || 0;
  return {
    rows: rows.map((row) => listRow(row, counts.get(String(row.id)) || 0)),
    pagination: { page, pageSize, totalEntries, totalPages: Math.max(1, Math.ceil(totalEntries / pageSize)) },
    summary: { total: result.count || 0, active: active.count || 0, confirmed: confirmed.count || 0, withDocuments: withDocumentIds.size },
    lastSuccessfulSyncAt: source.data?.last_success_at || null,
  };
}

export async function fetchInquiryDetail(token: string, id: string): Promise<InquiryDetail> {
  if (!isUuid(id)) throw new InquiryApiFailure("NOT_FOUND", "Inquiry not found.");
  const client = await authorizedClient(token);
  const inquiry = await client.from("inquiries").select("*").eq("id", id).neq("availability", "tombstoned").maybeSingle();
  if (inquiry.error) databaseFailure(inquiry.error);
  if (!inquiry.data) throw new InquiryApiFailure("NOT_FOUND", "Inquiry not found or not published.");
  const documents = await client.from("inquiry_documents").select("id,original_filename,extension,availability,current_version_id").eq("inquiry_id", id).order("original_filename");
  if (documents.error) databaseFailure(documents.error);
  const versionIds = (documents.data || []).map((document) => document.current_version_id).filter((value): value is string => Boolean(value));
  const versions = versionIds.length ? await client.from("inquiry_document_versions").select("id,byte_size,detected_mime,content_sha256,source_uploaded_at,source_modified_at,published_at").in("id", versionIds) : { data: [], error: null };
  if (versions.error) databaseFailure(versions.error);
  const versionsById = new Map((versions.data || []).map((version) => [version.id, version]));
  const docs: InquiryDocumentRow[] = (documents.data || []).map((document) => ({
    id: document.id, original_filename: document.original_filename, extension: document.extension,
    availability: document.availability as InquiryDocumentRow["availability"],
    current_version: document.current_version_id && versionsById.has(document.current_version_id) ? versionsById.get(document.current_version_id)! as InquiryDocumentRow["current_version"] : null,
  }));
  const row = inquiry.data as unknown as Record<string, unknown>;
  return { ...listRow(row, docs.filter((doc) => doc.availability === "available").length), customer_location: String(row.customer_location || ""), project_description: String(row.project_description || ""), prepared_at: row.prepared_at ? String(row.prepared_at) : null, sent_at: row.sent_at ? String(row.sent_at) : null, confirmed_at: row.confirmed_at ? String(row.confirmed_at) : null, last_observed_at: String(row.last_observed_at || ""), documents: docs };
}

export async function authorizeDocumentDownload(token: string, inquiryId: string, documentId: string) {
  if (!isUuid(inquiryId) || !isUuid(documentId)) throw new InquiryApiFailure("NOT_FOUND", "Document not found.");
  const client = await authorizedClient(token);
  const owner = await client.from("inquiry_documents").select("inquiry_id").eq("id", documentId).eq("inquiry_id", inquiryId).maybeSingle();
  if (owner.error) databaseFailure(owner.error);
  if (!owner.data) throw new InquiryApiFailure("NOT_FOUND", "Document not found.");
  const resolved = await client.rpc("authorize_inquiry_document_download", { p_document_id: documentId });
  if (resolved.error) {
    if (resolved.error.code === "P0002") throw new InquiryApiFailure("NOT_FOUND", "The current document is not available.");
    databaseFailure(resolved.error);
  }
  const metadata = Array.isArray(resolved.data) ? resolved.data[0] : resolved.data;
  if (!metadata) throw new InquiryApiFailure("NOT_FOUND", "The current document is not available.");
  const download = await client.storage.from(metadata.storage_bucket).download(metadata.storage_object_key);
  if (download.error) databaseFailure(download.error);
  const bytes = Buffer.from(await download.data.arrayBuffer());
  const expectedSize = numeric(metadata.byte_size);
  const actualHash = createHash("sha256").update(bytes).digest("hex");
  if (bytes.byteLength !== expectedSize || actualHash !== metadata.content_sha256) {
    console.error("[inquiry-download] Private object integrity check failed:", { documentId, expectedSize, actualSize: bytes.byteLength });
    throw new InquiryApiFailure("TEMPORARY_FAILURE", "The document failed its integrity check and was not delivered.");
  }
  return { bytes, filename: String(metadata.original_filename), mime: String(metadata.detected_mime), byteSize: expectedSize };
}
