import { createHash, randomUUID } from "node:crypto";
import { extname } from "node:path";
import { unzipSync } from "fflate";
import type { SupabaseClient } from "@supabase/supabase-js";
import { isSupabaseConfigured, supabaseServerWithToken } from "@/lib/supabase";
import {
  sha256Hex,
  validateInquiryDocumentCandidate,
} from "@/lib/inquiry-documents";
import type { CustomerInquiryDocument, CustomerInquiryListItem, CustomerInquirySubmission } from "@/types/inquiry-submissions";

export const CUSTOMER_INQUIRY_MAX_FILES = 15;
export const CUSTOMER_INQUIRY_MAX_FILE_BYTES = 30 * 1024 * 1024;
export const CUSTOMER_INQUIRY_MAX_REQUEST_BYTES = CUSTOMER_INQUIRY_MAX_FILES * CUSTOMER_INQUIRY_MAX_FILE_BYTES + 512_000;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export class CustomerInquiryFailure extends Error {
  constructor(public readonly code: "UNAUTHORIZED" | "FORBIDDEN" | "INVALID" | "NOT_FOUND" | "NOT_CONFIGURED" | "TEMPORARY_FAILURE", message: string) {
    super(message);
  }
}

function failDatabase(error: { code?: string; message?: string }): never {
  if (/42P01|PGRST202|PGRST205|schema cache|does not exist/i.test(`${error.code || ""} ${error.message || ""}`)) {
    throw new CustomerInquiryFailure("NOT_CONFIGURED", "Customer inquiry intake is not installed in this deployment.");
  }
  if (/jwt|token|42501|unauthorized|permission denied/i.test(`${error.code || ""} ${error.message || ""}`)) {
    throw new CustomerInquiryFailure("FORBIDDEN", "This account cannot perform that inquiry action.");
  }
  console.error("[customer-inquiry] Supabase failure:", { code: error.code, message: error.message });
  throw new CustomerInquiryFailure("TEMPORARY_FAILURE", "The inquiry service could not complete this request.");
}

async function customerClient(token: string): Promise<{ client: SupabaseClient; userId: string }> {
  if (!token) throw new CustomerInquiryFailure("UNAUTHORIZED", "Sign in before submitting an inquiry.");
  if (!isSupabaseConfigured()) throw new CustomerInquiryFailure("NOT_CONFIGURED", "Supabase is not configured for inquiry intake.");
  const client = supabaseServerWithToken(token);
  const user = await client.auth.getUser(token);
  if (user.error || !user.data.user) throw new CustomerInquiryFailure("UNAUTHORIZED", "Your session is expired or invalid.");
  return { client, userId: user.data.user.id };
}

function field(form: FormData, name: string, max: number, required = false): string {
  const value = form.get(name);
  const normalized = typeof value === "string" ? value.normalize("NFKC").trim() : "";
  if ((required && !normalized) || normalized.length > max) throw new CustomerInquiryFailure("INVALID", `Invalid ${name.replaceAll("_", " ")}.`);
  return normalized;
}

function detectedMime(extension: string, bytes: Uint8Array): string {
  if (extension === ".pdf" && bytes.subarray(0, 5).toString() === "37,80,68,70,45") return "application/pdf";
  if ((extension === ".jpg" || extension === ".jpeg") && bytes[0] === 0xff && bytes[1] === 0xd8 && bytes[2] === 0xff) return "image/jpeg";
  if (extension === ".png") return "image/png";
  if (extension === ".gif") return "image/gif";
  if (extension === ".webp") return "image/webp";
  if (extension === ".bmp") return "image/bmp";
  if (extension === ".xlsx") return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
  if (extension === ".csv") return "text/csv";
  return "application/octet-stream";
}

function deepValidate(extension: string, bytes: Uint8Array): boolean | undefined {
  if (extension === ".csv") {
    new TextDecoder("utf-8", { fatal: true }).decode(bytes);
    return true;
  }
  if (extension === ".xlsx") {
    const entries = unzipSync(bytes, { filter: (entry) => entry.name === "[Content_Types].xml" || entry.name === "xl/workbook.xml" || /vbaProject\.bin$/i.test(entry.name) });
    if (!entries["[Content_Types].xml"] || !entries["xl/workbook.xml"] || Object.keys(entries).some((name) => /vbaProject\.bin$/i.test(name))) return false;
    return true;
  }
  return undefined;
}

async function validatedFiles(form: FormData) {
  const files = form.getAll("documents").filter((item): item is File => item instanceof File && item.size > 0);
  if (files.length > CUSTOMER_INQUIRY_MAX_FILES) throw new CustomerInquiryFailure("INVALID", `Upload at most ${CUSTOMER_INQUIRY_MAX_FILES} documents.`);
  const duplicateName = files.find((file, index) => files.findIndex((candidate) => candidate.name.normalize("NFKC").trim().toLocaleLowerCase() === file.name.normalize("NFKC").trim().toLocaleLowerCase()) !== index);
  if (duplicateName) throw new CustomerInquiryFailure("INVALID", `${duplicateName.name} is attached more than once. Remove or rename the duplicate document.`);
  const output = [];
  for (const file of files) {
    if (file.size > CUSTOMER_INQUIRY_MAX_FILE_BYTES) throw new CustomerInquiryFailure("INVALID", `${file.name} exceeds the 30 MB per-file limit.`);
    const bytes = new Uint8Array(await file.arrayBuffer());
    const extension = extname(file.name).toLowerCase();
    const mime = detectedMime(extension, bytes);
    let deep: boolean | undefined;
    try { deep = deepValidate(extension, bytes); } catch { deep = false; }
    const validation = validateInquiryDocumentCandidate({
      filename: file.name, byteSize: bytes.byteLength, declaredMime: mime, detectedMime: mime,
      maxByteSize: CUSTOMER_INQUIRY_MAX_FILE_BYTES,
      sha256: sha256Hex(bytes), signature: bytes.subarray(0, 32), deepContentValidated: deep,
    });
    if (!validation.ok) throw new CustomerInquiryFailure("INVALID", `${file.name}: ${validation.message}`);
    output.push({ bytes, filename: validation.normalizedFilename, extension: validation.extension, mime, hash: sha256Hex(bytes) });
  }
  return output;
}

function asSubmission(row: Record<string, unknown>, documents: CustomerInquiryDocument[]): CustomerInquirySubmission {
  return { ...(row as unknown as Omit<CustomerInquirySubmission, "documents">), documents };
}

export async function createCustomerInquiry(token: string, form: FormData): Promise<CustomerInquirySubmission> {
  const { client, userId } = await customerClient(token);
  const description = field(form, "description", 5000, true);
  if (description.length < 10) throw new CustomerInquiryFailure("INVALID", "Description must contain at least 10 characters.");
  const values = {
    p_contact_name: field(form, "contact_name", 120, true), p_company_name: field(form, "company_name", 160, true),
    p_phone: field(form, "phone", 60), p_customer_reference: field(form, "customer_reference", 100),
    p_attention_name: field(form, "attention_name", 120), p_vat_trn_number: field(form, "vat_trn_number", 50),
    p_salesman_reference: field(form, "salesman_reference", 120),
    p_handover_estimator_name: field(form, "handover_estimator_name", 120),
    p_project_name: field(form, "project_name", 180, true), p_project_location: field(form, "project_location", 180),
    p_description: description,
  };
  const files = await validatedFiles(form);
  const created = await client.rpc("create_customer_inquiry_submission", values);
  if (created.error) failDatabase(created.error);
  const createdRow = Array.isArray(created.data) ? created.data[0] : created.data;
  if (!createdRow?.id || !UUID.test(createdRow.id)) throw new CustomerInquiryFailure("TEMPORARY_FAILURE", "The inquiry was not created correctly.");
  const documents: CustomerInquiryDocument[] = [];
  for (const file of files) {
    const documentId = randomUUID();
    const key = `${userId}/${createdRow.id}/${documentId}`.toLowerCase();
    const upload = await client.storage.from("inquiry-intake-documents").upload(key, file.bytes, { contentType: file.mime, upsert: false, cacheControl: "0" });
    if (upload.error) failDatabase(upload.error);
    const registered = await client.rpc("register_customer_inquiry_document", {
      p_submission_id: createdRow.id, p_document_id: documentId, p_original_filename: file.filename,
      p_extension: file.extension, p_detected_mime: file.mime, p_byte_size: file.bytes.byteLength,
      p_content_sha256: file.hash, p_storage_object_key: key,
    });
    if (registered.error) failDatabase(registered.error);
    documents.push({ id: documentId, original_filename: file.filename, extension: file.extension, detected_mime: file.mime,
      byte_size: file.bytes.byteLength, content_sha256: file.hash, uploaded_at: new Date().toISOString() });
  }
  const detail = await client.from("inquiry_customer_submissions").select("*").eq("id", createdRow.id).eq("customer_user_id", userId).single();
  if (detail.error) failDatabase(detail.error);
  return asSubmission(detail.data as Record<string, unknown>, documents);
}

export async function listCustomerInquiries(token: string): Promise<CustomerInquiryListItem[]> {
  const { client, userId } = await customerClient(token);
  const result = await client.from("inquiry_customer_submissions").select("*,inquiry_customer_documents(id)")
    .eq("customer_user_id", userId).order("submitted_at", { ascending: false }).limit(100);
  if (result.error) failDatabase(result.error);
  return (result.data || []).map((row) => {
    const { inquiry_customer_documents, description: _description, phone: _phone, ...rest } = row;
    return { ...rest, document_count: Array.isArray(inquiry_customer_documents) ? inquiry_customer_documents.length : 0 } as CustomerInquiryListItem;
  });
}

export async function getCustomerInquiry(token: string, id: string): Promise<CustomerInquirySubmission> {
  if (!UUID.test(id)) throw new CustomerInquiryFailure("NOT_FOUND", "Inquiry submission not found.");
  const { client, userId } = await customerClient(token);
  const result = await client.from("inquiry_customer_submissions").select("*").eq("id", id).eq("customer_user_id", userId).maybeSingle();
  if (result.error) failDatabase(result.error);
  if (!result.data) throw new CustomerInquiryFailure("NOT_FOUND", "Inquiry submission not found.");
  const docs = await client.from("inquiry_customer_documents").select("id,original_filename,extension,detected_mime,byte_size,content_sha256,uploaded_at").eq("submission_id", id).order("uploaded_at");
  if (docs.error) failDatabase(docs.error);
  return asSubmission(result.data as Record<string, unknown>, (docs.data || []) as CustomerInquiryDocument[]);
}

export async function downloadCustomerInquiryDocument(token: string, submissionId: string, documentId: string) {
  if (!UUID.test(submissionId) || !UUID.test(documentId)) throw new CustomerInquiryFailure("NOT_FOUND", "Document not found.");
  const { client } = await customerClient(token);
  const authorized = await client.rpc("authorize_customer_inquiry_document_download", { p_submission_id: submissionId, p_document_id: documentId });
  if (authorized.error) {
    if (authorized.error.code === "P0002") throw new CustomerInquiryFailure("NOT_FOUND", "Document not found.");
    failDatabase(authorized.error);
  }
  const metadata = Array.isArray(authorized.data) ? authorized.data[0] : authorized.data;
  if (!metadata) throw new CustomerInquiryFailure("NOT_FOUND", "Document not found.");
  const object = await client.storage.from(metadata.storage_bucket).download(metadata.storage_object_key);
  if (object.error) failDatabase(object.error);
  const bytes = Buffer.from(await object.data.arrayBuffer());
  const hash = createHash("sha256").update(bytes).digest("hex");
  if (bytes.byteLength !== Number(metadata.byte_size) || hash !== metadata.content_sha256) {
    throw new CustomerInquiryFailure("TEMPORARY_FAILURE", "The document failed its integrity check and was not delivered.");
  }
  return { bytes, filename: String(metadata.original_filename), mime: String(metadata.detected_mime) };
}
