import type { SupabaseClient } from "@supabase/supabase-js";
import { isSupabaseConfigured, isSupabaseUnavailableError, supabaseServerWithToken } from "@/lib/supabase";
import { isUuid } from "@/lib/inquiries-api";
import { normaliseProformaStatus } from "@/lib/proforma-status";
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

/** What the customer currently holds, and whether that is the current revision. */
export type ProformaShareState = {
  proforma_id: string | null;
  current_revision: number;
  /** False when the inquiry has no linked customer submission, so nobody to share with. */
  customer_linked: boolean;
  share: { revision: number; shared_at: string } | null;
  /** True when a newer revision exists than the one the customer was given. */
  stale: boolean;
  /** True when this account may share or withdraw (administrator only). */
  can_write: boolean;
};

/**
 * Read the share state of the inquiry's website proforma.
 *
 * A deployment that has not applied C1 has no `web_submission_id`, and one that
 * has not applied C7 has no share ledger. Neither is an error here: the panel
 * says the document cannot be shared yet instead of breaking the workspace.
 */
export async function readProformaShare(token: string, inquiryId: string): Promise<ProformaShareState> {
  if (!isUuid(inquiryId)) throw new ProformaApiFailure("NOT_FOUND", "Inquiry not found.");
  const client = await authorizedClient(token, "read");
  const empty: ProformaShareState = { proforma_id: null, current_revision: 0, customer_linked: false, share: null, stale: false, can_write: false };

  // The role decides whether the panel offers the action. It is read from the
  // same table every write function checks, so the button and the refusal agree.
  const user = await client.auth.getUser(token);
  const roleRow = user.data.user
    ? await client.from("inquiry_operator_roles").select("role").eq("user_id", user.data.user.id).maybeSingle()
    : { data: null };
  const canWrite = Boolean(roleRow.data && (roleRow.data as { role?: string }).role === "inquiry_admin");

  const proforma = await client
    .from("inquiry_proformas")
    .select("id,revision,status")
    .eq("inquiry_id", inquiryId)
    .eq("source_system", "website")
    .order("updated_at", { ascending: false })
    .limit(1)
    .maybeSingle();
  if (proforma.error) {
    if (missingRelation(proforma.error)) return { ...empty, can_write: canWrite };
    databaseFailure(proforma.error);
  }
  if (!proforma.data) return { ...empty, can_write: canWrite };

  const linked = await client.from("inquiries").select("web_submission_id").eq("id", inquiryId).maybeSingle();
  const customerLinked = !linked.error && Boolean((linked.data as unknown as Record<string, unknown> | null)?.web_submission_id);

  const share = await client
    .from("inquiry_proforma_shares")
    .select("revision,shared_at")
    .eq("proforma_id", String(proforma.data.id))
    .maybeSingle();
  if (share.error && !missingRelation(share.error)) databaseFailure(share.error);

  const currentRevision = numeric((proforma.data as unknown as Record<string, unknown>).revision);
  const shared = share.data
    ? { revision: numeric((share.data as unknown as Record<string, unknown>).revision), shared_at: String((share.data as unknown as Record<string, unknown>).shared_at || "") }
    : null;

  return {
    proforma_id: String(proforma.data.id),
    current_revision: currentRevision,
    customer_linked: customerLinked,
    share: shared,
    stale: Boolean(shared && shared.revision !== currentRevision),
    can_write: canWrite,
  };
}

/**
 * Share the current revision with the customer account that owns the inquiry.
 * The database decides who that is and refuses when the inquiry is unlinked.
 */
export async function shareProforma(token: string, inquiryId: string): Promise<number> {
  const state = await readProformaShare(token, inquiryId);
  if (!state.proforma_id) throw new ProformaApiFailure("NOT_FOUND", "Save the proforma before sharing it.");
  const client = await authorizedClient(token, "write");
  const { data, error } = await client.rpc("share_inquiry_proforma", { p_proforma_id: state.proforma_id });
  if (error) {
    if (error.code === "42501" || /permission denied|needs an enabled/i.test(error.message || "")) {
      throw new ProformaApiFailure("FORBIDDEN", error.message || "Sharing a proforma needs an inquiry administrator role.");
    }
    if (error.code === "P0002") throw new ProformaApiFailure("INVALID", error.message);
    if (error.code === "22023") throw new ProformaApiFailure("INVALID", error.message);
    databaseFailure(error);
  }
  return numeric(data);
}

export async function revokeProformaShare(token: string, inquiryId: string): Promise<void> {
  const state = await readProformaShare(token, inquiryId);
  if (!state.proforma_id) throw new ProformaApiFailure("NOT_FOUND", "There is no proforma to withdraw.");
  const client = await authorizedClient(token, "write");
  const { error } = await client.rpc("revoke_inquiry_proforma_share", { p_proforma_id: state.proforma_id });
  if (error) {
    if (error.code === "42501" || /permission denied|needs an enabled/i.test(error.message || "")) {
      throw new ProformaApiFailure("FORBIDDEN", error.message || "Withdrawing a shared proforma needs an inquiry administrator role.");
    }
    databaseFailure(error);
  }
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

function groupRow(row: Record<string, unknown>): { group_no: number; base_rate: number; surcharge_percent: number } {
  return {
    group_no: numeric(row.group_no),
    base_rate: numeric(row.base_rate),
    surcharge_percent: numeric(row.surcharge_percent),
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
    status: normaliseProformaStatus(row.status as string | null),
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

  /*
   * A cancelled proforma is still opened and shown as cancelled. Hiding it would
   * silently present an earlier draft as the current one, which is exactly the
   * confusion a cancellation is meant to end.
   */
  const proforma = await client
    .from("inquiry_proformas")
    .select("*")
    .eq("inquiry_id", inquiryId)
    .eq("source_system", "website")
    .order("updated_at", { ascending: false })
    .limit(1)
    .maybeSingle();

  if (proforma.error) {
    if (missingRelation(proforma.error)) {
      return { inquiry: header, proforma: null, lines: [], groups: [], groups_persisted: false, saves: [], installed: false };
    }
    databaseFailure(proforma.error);
  }
  if (!proforma.data) {
    return { inquiry: header, proforma: null, lines: [], groups: [], groups_persisted: true, saves: [], installed: true };
  }

  const record = recordRow(proforma.data as unknown as Record<string, unknown>);
  const [lines, saves, groups] = await Promise.all([
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
    client
      .from("inquiry_proforma_groups")
      .select("group_no,base_rate,surcharge_percent")
      .eq("proforma_id", record.id)
      .eq("revision", record.revision)
      .order("group_no", { ascending: true }),
  ]);
  if (lines.error) databaseFailure(lines.error);
  if (saves.error) databaseFailure(saves.error);
  // A deployment without the C5 table still opens: the settings are simply absent,
  // and the page says they are not stored rather than inventing them.
  const groupsInstalled = !groups.error;
  if (groups.error && !missingRelation(groups.error)) databaseFailure(groups.error);

  return {
    inquiry: header,
    proforma: record,
    lines: ((lines.data || []) as unknown as Record<string, unknown>[]).map(lineRow),
    groups: groupsInstalled
      ? ((groups.data || []) as unknown as Record<string, unknown>[]).map(groupRow)
      : [],
    groups_persisted: groupsInstalled,
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
  const groups = (input.groups || []).map((group) => ({
    group_no: Math.trunc(group.group_no),
    base_rate: Math.round(group.base_rate * 100) / 100,
    surcharge_percent: Math.round(group.surcharge_percent * 100) / 100,
  }));

  /*
   * v2 stores the lines and the specification settings in one revision. If this
   * deployment has not applied the C5 migration, the function does not exist and
   * the save falls back to v1, which stores the lines exactly as before; the
   * settings are then reported as not stored rather than silently dropped.
   */
  let saved = await client.rpc("save_inquiry_proforma_v2", {
    p_inquiry_id: inquiryId,
    p_idempotency_key: input.idempotency_key,
    p_expected_revision: input.expected_revision,
    p_currency: currency,
    p_lines: lines,
    p_groups: groups,
  });
  if (saved.error && (saved.error.code === "42883" || saved.error.code === "PGRST202")) {
    saved = await client.rpc("save_inquiry_proforma_v1", {
      p_inquiry_id: inquiryId,
      p_idempotency_key: input.idempotency_key,
      p_expected_revision: input.expected_revision,
      p_currency: currency,
      p_lines: lines,
    });
  }
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
