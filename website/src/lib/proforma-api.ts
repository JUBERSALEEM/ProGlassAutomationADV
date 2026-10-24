import type { SupabaseClient } from "@supabase/supabase-js";
import { isSupabaseConfigured, isSupabaseUnavailableError, supabaseServerWithToken } from "@/lib/supabase";
import { isUuid } from "@/lib/inquiries-api";
import type { InquiryStatus } from "@/types/inquiries";
import type {
  ProformaApiError,
  ProformaLine,
  ProformaLineInput,
  ProformaRecord,
  ProformaSaveInput,
  ProformaWorkspaceData,
} from "@/types/proforma";

const READ_ROLES = ["inquiry_admin", "inquiry_auditor"] as const;
const WRITE_ROLE = "inquiry_admin";
const CURRENCY = /^[A-Z]{3}$/;
const MAX_LINES = 200;
const MAX_GROUPS = 50;

export class ProformaApiFailure extends Error {
  code: ProformaApiError["code"];
  constructor(code: ProformaApiError["code"], message: string) {
    super(message);
    this.code = code;
  }
}

function missingRelation(error: { code?: string; message?: string }): boolean {
  return error.code === "42P01" || error.code === "PGRST205" || /inquiry_proforma|schema cache|does not exist/i.test(error.message || "");
}
function authFailure(error: { code?: string; message?: string }): boolean {
  return /jwt|token|expired|unauthorized|permission denied|42501|PGRST301/i.test(`${error.code || ""} ${error.message || ""}`);
}
function databaseFailure(error: { code?: string; message?: string }): never {
  if (isSupabaseUnavailableError(error)) throw new ProformaApiFailure("TEMPORARY_FAILURE", "The proforma service is temporarily unreachable. Please try again.");
  if (authFailure(error)) throw new ProformaApiFailure("FORBIDDEN", "Your operator session cannot read or write proformas.");
  console.error("[proforma-api] Supabase query failed:", { code: error.code, message: error.message });
  throw new ProformaApiFailure("TEMPORARY_FAILURE", "The proforma service could not complete this request. Please try again.");
}
function numeric(value: unknown): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
}

/**
 * Authorization for both reading and writing a proforma.
 *
 * The role is read from `inquiry_operator_roles` with the caller's own token.
 * Nothing in a request body, header or page decides who the caller is. The
 * database write function checks the role again, because a route is not a
 * security boundary.
 */
async function authorizedClient(token: string, mode: "read" | "write"): Promise<SupabaseClient> {
  if (!token) throw new ProformaApiFailure("UNAUTHORIZED", "Sign in to open a proforma invoice.");
  if (!isSupabaseConfigured()) throw new ProformaApiFailure("NOT_CONFIGURED", "Supabase is not configured for the private inquiry workflow.");
  const client = supabaseServerWithToken(token);
  const user = await client.auth.getUser(token);
  if (user.error && isSupabaseUnavailableError(user.error)) {
    throw new ProformaApiFailure("TEMPORARY_FAILURE", "The authentication service is temporarily unreachable. Please try again.");
  }
  if (user.error || !user.data.user) throw new ProformaApiFailure("UNAUTHORIZED", "Your operator session is expired or invalid.");
  const { data, error } = await client.from("inquiry_operator_roles").select("role, enabled").eq("user_id", user.data.user.id).maybeSingle();
  if (error) databaseFailure(error);
  const role = data?.role as string | undefined;
  const allowed = mode === "write" ? role === WRITE_ROLE : Boolean(role && READ_ROLES.includes(role as (typeof READ_ROLES)[number]));
  if (data?.enabled !== true || !allowed) {
    throw new ProformaApiFailure(
      "FORBIDDEN",
      mode === "write"
        ? "Saving a proforma needs an enabled inquiry administrator role."
        : "Your account does not have an enabled inquiry role that can read proformas.",
    );
  }
  return client;
}

function lineRow(row: Record<string, unknown>): ProformaLine {
  return {
    group_no: numeric(row.group_no),
    line_no: numeric(row.line_no),
    glass_ref: String(row.glass_ref || ""),
    w1: numeric(row.w1),
    h1: numeric(row.h1),
    w2: numeric(row.w2),
    h2: numeric(row.h2),
    qty: numeric(row.qty),
    approved_rate: numeric(row.approved_rate),
    sqm: numeric(row.sqm),
    total_sqm: numeric(row.total_sqm),
    line_value: numeric(row.line_value),
  };
}

function recordRow(row: Record<string, unknown>): ProformaRecord {
  return {
    id: String(row.id),
    inquiry_id: String(row.inquiry_id || ""),
    proforma_number: String(row.proforma_number || ""),
    source_system: row.source_system === "desktop" ? "desktop" : "website",
    source_proforma_number: String(row.source_proforma_number || ""),
    inquiry_number: String(row.inquiry_number || ""),
    customer_name: String(row.customer_name || ""),
    status: (row.status as ProformaRecord["status"]) || "Draft",
    currency: String(row.currency || "AED"),
    revision: numeric(row.revision),
    line_count: numeric(row.line_count),
    total_sqm: numeric(row.total_sqm),
    total_value: numeric(row.total_value),
    created_at: String(row.created_at || ""),
    updated_at: String(row.updated_at || ""),
  };
}

export async function fetchProformaWorkspace(token: string, inquiryId: string): Promise<ProformaWorkspaceData> {
  if (!isUuid(inquiryId)) throw new ProformaApiFailure("NOT_FOUND", "Inquiry not found.");
  const client = await authorizedClient(token, "read");

  const inquiry = await client
    .from("inquiries")
    .select("id,inquiry_number,customer_name,project_name,status,proforma_number,inquiry_value,availability")
    .eq("id", inquiryId)
    .maybeSingle();
  if (inquiry.error) databaseFailure(inquiry.error);
  if (!inquiry.data || inquiry.data.availability === "tombstoned") {
    throw new ProformaApiFailure("NOT_FOUND", "Inquiry not found or no longer published.");
  }

  const header = {
    id: String(inquiry.data.id),
    inquiry_number: String(inquiry.data.inquiry_number || ""),
    customer_name: String(inquiry.data.customer_name || ""),
    project_name: String(inquiry.data.project_name || ""),
    status: inquiry.data.status as InquiryStatus,
    proforma_number: String(inquiry.data.proforma_number || ""),
    inquiry_value: numeric(inquiry.data.inquiry_value),
  };

  const proforma = await client
    .from("inquiry_proformas")
    .select("*")
    .eq("inquiry_id", inquiryId)
    .eq("source_system", "website")
    .neq("status", "Void")
    .order("updated_at", { ascending: false })
    .limit(1)
    .maybeSingle();

  if (proforma.error) {
    if (missingRelation(proforma.error)) {
      return { inquiry: header, proforma: null, lines: [], saves: [], installed: false };
    }
    databaseFailure(proforma.error);
  }
  if (!proforma.data) {
    return { inquiry: header, proforma: null, lines: [], saves: [], installed: true };
  }

  const record = recordRow(proforma.data as unknown as Record<string, unknown>);
  const [lines, saves] = await Promise.all([
    client
      .from("inquiry_proforma_lines")
      .select("group_no,line_no,glass_ref,w1,h1,w2,h2,qty,approved_rate,sqm,total_sqm,line_value")
      .eq("proforma_id", record.id)
      .order("group_no", { ascending: true })
      .order("line_no", { ascending: true }),
    client
      .from("inquiry_proforma_saves")
      .select("revision,line_count,total_value,saved_at")
      .eq("proforma_id", record.id)
      .order("revision", { ascending: false })
      .limit(20),
  ]);
  if (lines.error) databaseFailure(lines.error);
  if (saves.error) databaseFailure(saves.error);

  return {
    inquiry: header,
    proforma: record,
    lines: ((lines.data || []) as unknown as Record<string, unknown>[]).map(lineRow),
    saves: ((saves.data || []) as unknown as Record<string, unknown>[]).map((row) => ({
      revision: numeric(row.revision),
      line_count: numeric(row.line_count),
      total_value: numeric(row.total_value),
      saved_at: String(row.saved_at || ""),
    })),
    installed: true,
  };
}

function invalid(message: string): never {
  throw new ProformaApiFailure("INVALID", message);
}

/** Validates the proposed lines before the database sees them. */
export function validateLines(lines: ProformaLineInput[]): ProformaLineInput[] {
  if (!Array.isArray(lines) || lines.length < 1) invalid("Add at least one line before saving.");
  if (lines.length > MAX_LINES) invalid(`A proforma holds at most ${MAX_LINES} lines.`);
  const seen = new Set<string>();
  const groups = new Set<number>();
  return lines.map((line, index) => {
    const group = Number(line.group_no);
    const lineNo = Number(line.line_no);
    const w1 = Math.round(Number(line.w1));
    const h1 = Math.round(Number(line.h1));
    const w2 = Math.round(Number(line.w2 || 0));
    const h2 = Math.round(Number(line.h2 || 0));
    const qty = Math.round(Number(line.qty));
    const rate = Math.round(Number(line.rate) * 100) / 100;
    if (!Number.isInteger(group) || group < 1 || group > MAX_GROUPS) invalid(`Line ${index + 1} is not inside a specification group.`);
    if (!Number.isInteger(lineNo) || lineNo < 1 || lineNo > 500) invalid(`Line ${index + 1} has an invalid position.`);
    if (!(w1 >= 1 && w1 <= 20000) || !(h1 >= 1 && h1 <= 20000)) invalid(`Line ${index + 1} needs a width and a height between 1 and 20000 mm.`);
    if (!(w2 >= 0 && w2 <= 20000) || !(h2 >= 0 && h2 <= 20000)) invalid(`Line ${index + 1} has an inner panel size outside 0 to 20000 mm.`);
    if (!(qty >= 1 && qty <= 100000)) invalid(`Line ${index + 1} needs a quantity of at least one.`);
    if (!Number.isFinite(rate) || rate < 0 || rate > 1000000) invalid(`Line ${index + 1} has an approved rate outside the accepted range.`);
    if (seen.has(`${group}:${lineNo}`)) invalid(`Specification ${group} has a repeated line number.`);
    seen.add(`${group}:${lineNo}`);
    groups.add(group);
    return {
      group_no: group,
      line_no: lineNo,
      glass_ref: String(line.glass_ref || "").trim().slice(0, 200),
      w1,
      h1,
      w2,
      h2,
      qty,
      rate,
    };
  });
}

export async function saveProforma(token: string, inquiryId: string, input: ProformaSaveInput): Promise<ProformaWorkspaceData> {
  if (!isUuid(inquiryId)) throw new ProformaApiFailure("NOT_FOUND", "Inquiry not found.");
  if (!isUuid(input.idempotency_key)) invalid("A canonical idempotency key is required.");
  if (!Number.isInteger(input.expected_revision) || input.expected_revision < 0) invalid("An expected revision is required.");
  const currency = String(input.currency || "").trim().toUpperCase();
  if (!CURRENCY.test(currency)) invalid("A three letter currency code is required.");
  const lines = validateLines(input.lines);

  const client = await authorizedClient(token, "write");
  const saved = await client.rpc("save_inquiry_proforma_v1", {
    p_inquiry_id: inquiryId,
    p_idempotency_key: input.idempotency_key,
    p_expected_revision: input.expected_revision,
    p_currency: currency,
    p_lines: lines,
  });
  if (saved.error) {
    if (saved.error.code === "40001") {
      throw new ProformaApiFailure("CONFLICT", "This proforma changed since it was loaded. Reload it and apply the change again.");
    }
    if (saved.error.code === "42501") throw new ProformaApiFailure("FORBIDDEN", "Saving a proforma needs an enabled inquiry administrator role.");
    if (saved.error.code === "P0002") throw new ProformaApiFailure("NOT_FOUND", "Inquiry not found or no longer published.");
    if (saved.error.code === "22023" || saved.error.code === "23505" || saved.error.code === "22P02") {
      throw new ProformaApiFailure("INVALID", saved.error.message || "The proforma was rejected by the database.");
    }
    if (missingRelation(saved.error)) throw new ProformaApiFailure("NOT_CONFIGURED", "The proforma tables are not installed in this deployment.");
    databaseFailure(saved.error);
  }
  return fetchProformaWorkspace(token, inquiryId);
}
