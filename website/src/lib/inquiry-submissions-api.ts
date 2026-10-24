import { createHash, randomUUID } from "node:crypto";
import { extname } from "node:path";
import { unzipSync } from "fflate";
import type { SupabaseClient } from "@supabase/supabase-js";
import { isSupabaseConfigured, isSupabaseUnavailableError, supabaseServerWithToken } from "@/lib/supabase";
import {
  sha256Hex,
  validateInquiryDocumentCandidate,
} from "@/lib/inquiry-documents";
import { withoutPipelineFields } from "@/lib/inquiry-pipeline";
import type { CustomerInquiryDocument, CustomerInquiryListItem, CustomerInquiryProfile, CustomerInquiryProject, CustomerInquirySubmission, CustomerInquiryType } from "@/types/inquiry-submissions";

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
  if (isSupabaseUnavailableError(error)) {
    throw new CustomerInquiryFailure("TEMPORARY_FAILURE", "The inquiry service is temporarily unreachable. Please try again.");
  }
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
  if (user.error && isSupabaseUnavailableError(user.error)) throw new CustomerInquiryFailure("TEMPORARY_FAILURE", "The authentication service is temporarily unreachable. Please try again.");
  if (user.error || !user.data.user) throw new CustomerInquiryFailure("UNAUTHORIZED", "Your session is expired or invalid.");
  return { client, userId: user.data.user.id };
}

async function operatorClient(token: string): Promise<SupabaseClient> {
  const { client, userId } = await customerClient(token);
  const role = await client.from("inquiry_operator_roles").select("role,enabled").eq("user_id", userId).maybeSingle();
  if (role.error) failDatabase(role.error);
  // The Phase 12 matrix gives inquiry_viewer no inbox access, so the page refuses
  // it here as clearly as the database does.
  if (!role.data?.enabled || !["inquiry_auditor", "inquiry_admin"].includes(role.data.role)) {
    throw new CustomerInquiryFailure("FORBIDDEN", "An enabled inquiry operator role is required.");
  }
  return client;
}

function field(form: FormData, name: string, max: number, required = false): string {
  const value = form.get(name);
  const normalized = typeof value === "string" ? value.normalize("NFKC").trim() : "";
  if ((required && !normalized) || normalized.length > max) throw new CustomerInquiryFailure("INVALID", `Invalid ${name.replaceAll("_", " ")}.`);
  return normalized;
}
const INQUIRY_TYPES = new Set<CustomerInquiryType>(["quotation","drawing_review","technical","order_handover","other"]);
function inquiryType(value: FormDataEntryValue | null): CustomerInquiryType {
  const normalized = typeof value === "string" ? value : "";
  if (!INQUIRY_TYPES.has(normalized as CustomerInquiryType)) throw new CustomerInquiryFailure("INVALID", "Invalid inquiry type.");
  return normalized as CustomerInquiryType;
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

export async function getCustomerInquirySetup(token: string): Promise<{ profile: CustomerInquiryProfile | null; projects: CustomerInquiryProject[] }> {
  const { client, userId } = await customerClient(token);
  const [profile, projects] = await Promise.all([
    client.from("inquiry_customer_profiles").select("contact_name,company_name,phone,vat_trn_number,attention_name").eq("customer_user_id", userId).maybeSingle(),
    client.from("inquiry_customer_projects").select("id,project_name,project_location,default_inquiry_type,active,created_at,updated_at").eq("customer_user_id", userId).eq("active", true).order("project_name"),
  ]);
  if (profile.error) failDatabase(profile.error);
  if (projects.error) failDatabase(projects.error);
  return { profile: profile.data as CustomerInquiryProfile | null, projects: (projects.data || []) as CustomerInquiryProject[] };
}

export async function saveCustomerInquiryProfile(token: string, form: FormData): Promise<CustomerInquiryProfile> {
  const { client } = await customerClient(token);
  const saved = await client.rpc("save_customer_inquiry_profile", {
    p_contact_name: field(form,"contact_name",120), p_company_name: field(form,"company_name",160),
    p_phone: field(form,"phone",60), p_vat_trn_number: field(form,"vat_trn_number",50), p_attention_name: field(form,"attention_name",120),
  });
  if (saved.error) failDatabase(saved.error);
  return saved.data as CustomerInquiryProfile;
}

export async function saveCustomerInquiryProject(token: string, form: FormData): Promise<CustomerInquiryProject> {
  const { client } = await customerClient(token);
  const projectId = field(form,"project_id",36);
  if (projectId && !UUID.test(projectId)) throw new CustomerInquiryFailure("INVALID","Invalid project selection.");
  const saved = await client.rpc("save_customer_inquiry_project", {
    p_project_id: projectId || null, p_project_name: field(form,"project_name",180,true),
    p_project_location: field(form,"project_location",180), p_default_inquiry_type: inquiryType(form.get("inquiry_type")), p_active: true,
  });
  if (saved.error) failDatabase(saved.error);
  return saved.data as CustomerInquiryProject;
}

export async function createCustomerInquiryDraft(token: string, form: FormData) {
  const { client } = await customerClient(token);
  const description = field(form, "description", 5000, true);
  if (description.length < 10) throw new CustomerInquiryFailure("INVALID", "Description must contain at least 10 characters.");
  const expected = Number(form.get("expected_document_count"));
  if (!Number.isInteger(expected) || expected < 0 || expected > CUSTOMER_INQUIRY_MAX_FILES) {
    throw new CustomerInquiryFailure("INVALID", `Select at most ${CUSTOMER_INQUIRY_MAX_FILES} documents.`);
  }
  const projectId = field(form, "project_id", 36);
  if (projectId && !UUID.test(projectId)) throw new CustomerInquiryFailure("INVALID", "Invalid project selection.");
  const created = await client.rpc("create_customer_inquiry_draft_v2", {
    p_contact_name: field(form, "contact_name", 120, true), p_company_name: field(form, "company_name", 160, true),
    p_phone: field(form, "phone", 60), p_customer_reference: field(form, "customer_reference", 100),
    p_attention_name: field(form, "attention_name", 120), p_vat_trn_number: field(form, "vat_trn_number", 50),
    p_salesman_reference: field(form, "salesman_reference", 120), p_handover_estimator_name: field(form, "handover_estimator_name", 120),
    p_project_id: projectId || null, p_project_name: field(form, "project_name", 180, true),
    p_project_location: field(form, "project_location", 180), p_inquiry_type: inquiryType(form.get("inquiry_type")),
    p_description: description, p_expected_document_count: expected,
  });
  if (created.error) failDatabase(created.error);
  const row = Array.isArray(created.data) ? created.data[0] : created.data;
  if (!row?.id || !UUID.test(row.id)) throw new CustomerInquiryFailure("TEMPORARY_FAILURE", "The inquiry draft was not created correctly.");
  return row as { id: string; submission_reference: string };
}

export async function uploadCustomerInquiryDocument(token: string, submissionId: string, form: FormData): Promise<CustomerInquiryDocument> {
  if (!UUID.test(submissionId)) throw new CustomerInquiryFailure("NOT_FOUND", "Inquiry draft not found.");
  const { client, userId } = await customerClient(token);
  const files = await validatedFiles(form);
  if (files.length !== 1) throw new CustomerInquiryFailure("INVALID", "Upload exactly one document at a time.");
  const file = files[0]!;
  const documentId = randomUUID();
  const key = `${userId}/${submissionId}/${documentId}`.toLowerCase();
  const upload = await client.storage.from("inquiry-intake-documents").upload(key, file.bytes, { contentType: file.mime, upsert: false, cacheControl: "0" });
  if (upload.error) failDatabase(upload.error);
  const registered = await client.rpc("register_customer_inquiry_document", {
    p_submission_id: submissionId, p_document_id: documentId, p_original_filename: file.filename,
    p_extension: file.extension, p_detected_mime: file.mime, p_byte_size: file.bytes.byteLength,
    p_content_sha256: file.hash, p_storage_object_key: key,
  });
  if (registered.error) failDatabase(registered.error);
  return { id: documentId, original_filename: file.filename, extension: file.extension, detected_mime: file.mime,
    byte_size: file.bytes.byteLength, content_sha256: file.hash, uploaded_at: new Date().toISOString() };
}

export async function finalizeCustomerInquiry(token: string, submissionId: string): Promise<CustomerInquirySubmission> {
  if (!UUID.test(submissionId)) throw new CustomerInquiryFailure("NOT_FOUND", "Inquiry draft not found.");
  const { client } = await customerClient(token);
  const finalized = await client.rpc("finalize_customer_inquiry_submission", { p_submission_id: submissionId });
  if (finalized.error) failDatabase(finalized.error);
  return getCustomerInquiry(token, submissionId);
}

export async function listCustomerInquiries(token: string): Promise<CustomerInquiryListItem[]> {
  const { client, userId } = await customerClient(token);
  const result = await client.from("inquiry_customer_submissions").select("*,inquiry_customer_documents(id)")
    .eq("customer_user_id", userId).eq("upload_state", "complete").order("submitted_at", { ascending: false }).limit(100);
  if (result.error) failDatabase(result.error);
  return (result.data || []).map((row) => {
    // The desktop pipeline columns are internal workflow state, so they are
    // removed here rather than trusted not to travel to a customer browser.
    const { inquiry_customer_documents, description: _description, phone: _phone, ...rest } = withoutPipelineFields(row);
    return { ...rest, document_count: Array.isArray(inquiry_customer_documents) ? inquiry_customer_documents.length : 0 } as CustomerInquiryListItem;
  });
}

export async function listOperatorCustomerInquiries(token: string): Promise<CustomerInquiryListItem[]> {
  const client = await operatorClient(token);
  const result = await client.from("inquiry_customer_submissions").select("*,inquiry_customer_documents(id)")
    .eq("upload_state", "complete").order("submitted_at", { ascending: false }).limit(200);
  if (result.error) failDatabase(result.error);
  return (result.data || []).map((row) => {
    const { inquiry_customer_documents, description: _description, phone: _phone, ...rest } = row;
    return { ...rest, document_count: Array.isArray(inquiry_customer_documents) ? inquiry_customer_documents.length : 0 } as CustomerInquiryListItem;
  });
}

export async function reviewOperatorCustomerInquiry(token: string, id: string, values: { status: string; estimatedResponseAt: string | null; operatorNote: string }): Promise<CustomerInquirySubmission> {
  if (!UUID.test(id)) throw new CustomerInquiryFailure("NOT_FOUND", "Inquiry submission not found.");
  const { client } = await customerClient(token);
  if (values.operatorNote.normalize("NFKC").trim().length > 2000) throw new CustomerInquiryFailure("INVALID", "Operator note exceeds 2,000 characters.");
  const reviewed = await client.rpc("review_customer_inquiry_submission", { p_submission_id: id, p_status: values.status, p_estimated_response_at: values.estimatedResponseAt || null, p_operator_note: values.operatorNote.normalize("NFKC").trim() });
  if (reviewed.error) failDatabase(reviewed.error);
  return getOperatorCustomerInquiry(token, id);
}

export async function getOperatorCustomerInquiry(token: string, id: string): Promise<CustomerInquirySubmission> {
  if (!UUID.test(id)) throw new CustomerInquiryFailure("NOT_FOUND", "Inquiry submission not found.");
  const client = await operatorClient(token);
  const result = await client.from("inquiry_customer_submissions").select("*").eq("id", id).eq("upload_state", "complete").maybeSingle();
  if (result.error) failDatabase(result.error);
  if (!result.data) throw new CustomerInquiryFailure("NOT_FOUND", "Inquiry submission not found.");
  const docs = await client.from("inquiry_customer_documents").select("id,original_filename,extension,detected_mime,byte_size,content_sha256,uploaded_at").eq("submission_id", id).order("uploaded_at");
  if (docs.error) failDatabase(docs.error);
  return asSubmission(result.data as Record<string, unknown>, (docs.data || []) as CustomerInquiryDocument[]);
}

export async function getCustomerInquiry(token: string, id: string): Promise<CustomerInquirySubmission> {
  if (!UUID.test(id)) throw new CustomerInquiryFailure("NOT_FOUND", "Inquiry submission not found.");
  const { client, userId } = await customerClient(token);
  const result = await client.from("inquiry_customer_submissions").select("*").eq("id", id).eq("customer_user_id", userId).maybeSingle();
  if (result.error) failDatabase(result.error);
  if (!result.data) throw new CustomerInquiryFailure("NOT_FOUND", "Inquiry submission not found.");
  const docs = await client.from("inquiry_customer_documents").select("id,original_filename,extension,detected_mime,byte_size,content_sha256,uploaded_at").eq("submission_id", id).order("uploaded_at");
  if (docs.error) failDatabase(docs.error);
  return asSubmission(withoutPipelineFields(result.data as Record<string, unknown>), (docs.data || []) as CustomerInquiryDocument[]);
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
