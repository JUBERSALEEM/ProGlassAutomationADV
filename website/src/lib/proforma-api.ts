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
  ProformaWorkflowSnapshot,
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

/** Staff-only status and audit metadata for legacy customer share records. */
export type ProformaShareState = {
  proforma_id: string | null;
  current_revision: number;
  /** False when the inquiry has no linked customer submission, so nobody to share with. */
  customer_linked: boolean;
  share: { revision: number; shared_at: string; revoked_at: string | null } | null;
  /** True when a newer revision exists than the one recorded in the legacy share row. */
  stale: boolean;
  /** True when this account may share or withdraw (administrator only). */
  can_write: boolean;
};

/**
 * Read the staff-only audit state of legacy customer shares for the inquiry's website proforma.
 * The C11 migration adds `revoked_at`; apply it before deploying this reader.
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
    .select("revision,shared_at,revoked_at")
    .eq("proforma_id", String(proforma.data.id))
    .maybeSingle();
  if (share.error && !missingRelation(share.error)) databaseFailure(share.error);

  const currentRevision = numeric((proforma.data as unknown as Record<string, unknown>).revision);
  const shared = share.data
    ? {
        revision: numeric((share.data as unknown as Record<string, unknown>).revision),
        shared_at: String((share.data as unknown as Record<string, unknown>).shared_at || ""),
        revoked_at: typeof (share.data as unknown as Record<string, unknown>).revoked_at === "string"
          ? String((share.data as unknown as Record<string, unknown>).revoked_at)
          : null,
      }
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

export async function revokeProformaShare(token: string, inquiryId: string): Promise<void> {
  const state = await readProformaShare(token, inquiryId);
  if (!state.proforma_id || !state.share || state.share.revoked_at) {
    throw new ProformaApiFailure("NOT_FOUND", "There is no active legacy share to withdraw.");
  }
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
      return { inquiry: header, proforma: null, lines: [], groups: [], groups_persisted: false, workflow_snapshot: null, workflow_snapshot_persisted: false, saves: [], installed: false };
    }
    databaseFailure(proforma.error);
  }
  if (!proforma.data) {
    return { inquiry: header, proforma: null, lines: [], groups: [], groups_persisted: true, workflow_snapshot: null, workflow_snapshot_persisted: false, saves: [], installed: true };
  }

  const record = recordRow(proforma.data as unknown as Record<string, unknown>);
  const [lines, saves, groups, workflow] = await Promise.all([
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
    client
      .from("inquiry_proforma_workflow_states")
      .select("snapshot")
      .eq("proforma_id", record.id)
      .eq("revision", record.revision)
      .maybeSingle(),
  ]);
  if (lines.error) databaseFailure(lines.error);
  if (saves.error) databaseFailure(saves.error);
  // A deployment without the C5 table still opens: the settings are simply absent,
  // and the page says they are not stored rather than inventing them.
  const groupsInstalled = !groups.error;
  if (groups.error && !missingRelation(groups.error)) databaseFailure(groups.error);
  const workflowSnapshotInstalled = !workflow.error;
  if (workflow.error && !missingRelation(workflow.error)) databaseFailure(workflow.error);
  let workflowSnapshot: ProformaWorkflowSnapshot | null = null;
  if (workflow.data?.snapshot) {
    try {
      workflowSnapshot = validateWorkflowSnapshot(workflow.data.snapshot);
    } catch (error) {
      console.error("[proforma-api] Ignoring malformed saved Step 4/6 workflow snapshot:", error instanceof Error ? error.message : "invalid payload");
    }
  }

  return {
    inquiry: header,
    proforma: record,
    lines: ((lines.data || []) as unknown as Record<string, unknown>[]).map(lineRow),
    groups: groupsInstalled
      ? ((groups.data || []) as unknown as Record<string, unknown>[]).map(groupRow)
      : [],
    groups_persisted: groupsInstalled,
    workflow_snapshot: workflowSnapshot,
    workflow_snapshot_persisted: workflowSnapshotInstalled && workflowSnapshot !== null,
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

function isObject(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

/** Keep the revision-pinned workflow payload bounded and structurally safe. */
export function validateWorkflowSnapshot(value: unknown): ProformaWorkflowSnapshot {
  if (!isObject(value) || !isObject(value.step4_groups) || !isObject(value.step6)) invalid("The saved Step 4–6 workflow state is invalid.");
  const allowedCodes = new Set(["SGU", "DGU", "LAM", "DGU+LAM"]);
  const step4_groups: ProformaWorkflowSnapshot["step4_groups"] = {};
  const entries = Object.entries(value.step4_groups);
  if (entries.length > MAX_GROUPS) invalid("Too many Step 4 specification groups.");
  for (const [rawGroup, rawBuild] of entries) {
    const groupNo = Number(rawGroup);
    if (!Number.isInteger(groupNo) || groupNo < 1 || groupNo > MAX_GROUPS || !isObject(rawBuild) || !allowedCodes.has(String(rawBuild.code)) || !isObject(rawBuild.values)) invalid("A Step 4 specification group is invalid.");
    const values = Object.entries(rawBuild.values);
    if (values.length > 100 || values.some(([key, item]) => key.length > 80 || typeof item !== "string" || item.length > 500)) invalid("A Step 4 specification contains invalid or oversized values.");
    step4_groups[groupNo] = { code: String(rawBuild.code) as ProformaWorkflowSnapshot["step4_groups"][number]["code"], values: Object.fromEntries(values) as Record<string, string> };
  }
  const rawStep6 = value.step6;
  if ((rawStep6.mode !== "advanced" && rawStep6.mode !== "manual") || !Array.isArray(rawStep6.charges) || !Array.isArray(rawStep6.wastages) || !Array.isArray(rawStep6.customCharges) || rawStep6.charges.length > 100 || rawStep6.wastages.length > 200 || rawStep6.customCharges.length > 200) invalid("The saved Step 6 charges are invalid.");
  const stringList = (items: unknown, max: number): string[] => {
    if (!Array.isArray(items) || items.length > max || items.some((item) => typeof item !== "string" || item.length > 100)) invalid("The saved Step 6 selection is invalid.");
    return [...new Set(items as string[])];
  };
  const validGroups = (items: unknown): number[] => {
    if (!Array.isArray(items) || items.length > MAX_GROUPS || items.some((item) => !Number.isInteger(item) || Number(item) < 1 || Number(item) > MAX_GROUPS)) invalid("A Step 6 target specification is invalid.");
    return [...new Set(items as number[])];
  };
  const chargeKeys = new Set(["ARGON_GAS", "SILICON_BED", "SILICON_BED_SQM", "OVERLAP", "OVERLAP_SQM", "SHUTTER_BONDING", "MITRING", "POLISH", "POLISH_MATCHED_LAM", "CUTOUT", "HOLES", "SANDBLASTING", "EDGE_GRINDING", "COUNTERSUNK_HOLES", "FAN_HOLES", "DELIVERY", "U_INSERT"]);
  const metricKeys = new Set(["LM1", "LM2", "LM_COMBINED", "SQM1", "SQM2", "SQM_COMBINED", "DGU_CAVITY", "QTY", "TOTAL_PIECES", "DOUBLE_GLASS"]);
  const numericString = (value: unknown, label: string, max = 1_000_000): string => {
    if (typeof value !== "string" || value.length > 40) invalid(`${label} is invalid.`);
    const trimmed = value.trim();
    if (trimmed && (!Number.isFinite(Number(trimmed)) || Number(trimmed) < 0 || Number(trimmed) > max)) invalid(`${label} is outside the accepted range.`);
    return value;
  };
  const charges = rawStep6.charges.map((item) => {
    if (!isObject(item) || typeof item.key !== "string" || !chargeKeys.has(item.key) || typeof item.isActive !== "boolean" || typeof item.metricKey !== "string" || !metricKeys.has(item.metricKey) || (item.polishType !== undefined && (typeof item.polishType !== "string" || item.polishType.length > 100)) || (item.customDescription !== undefined && (typeof item.customDescription !== "string" || item.customDescription.length > 300)) || (item.customQty !== undefined && typeof item.customQty !== "string")) invalid("A Step 6 predefined charge is invalid.");
    const rate = numericString(item.rate, "A Step 6 rate");
    const customQty = numericString(String(item.customQty ?? ""), "A custom polish quantity");
    return { key: item.key, isActive: item.isActive, rate, metricKey: item.metricKey, groupNos: validGroups(item.groupNos), polishType: String(item.polishType ?? "AllSidePolish"), customDescription: String(item.customDescription ?? ""), customQty };
  });
  const wastages = rawStep6.wastages.map((item) => {
    if (!isObject(item) || typeof item.id !== "string" || item.id.length > 100 || typeof item.isActive !== "boolean" || typeof item.color !== "string" || item.color.length > 200 || !["outer", "inner", "third"].includes(String(item.side)) || typeof item.rate !== "string" || typeof item.sheetQuantity !== "string") invalid("A Step 6 wastage row is invalid.");
    const rate = numericString(item.rate, "A Step 6 wastage rate");
    const sheetQuantity = numericString(item.sheetQuantity, "A Step 6 sheet quantity", 100_000);
    return { id: item.id, isActive: item.isActive, color: item.color, side: item.side as "outer" | "inner" | "third", rate, sheetQuantity, groupNos: validGroups(item.groupNos) };
  });
  const customCharges = rawStep6.customCharges.map((item) => {
    if (!isObject(item) || typeof item.id !== "string" || item.id.length > 100 || typeof item.isActive !== "boolean" || typeof item.name !== "string" || item.name.length > 300 || typeof item.qty !== "string" || typeof item.rate !== "string" || !["Sqm", "Lm", "Qty"].includes(String(item.unit))) invalid("A Step 6 custom charge is invalid.");
    const qty = numericString(item.qty, "A custom Step 6 quantity");
    const rate = numericString(item.rate, "A custom Step 6 rate");
    return { id: item.id, isActive: item.isActive, name: item.name, qty, rate, unit: item.unit as "Sqm" | "Lm" | "Qty" };
  });
  let step5_ownership: ProformaWorkflowSnapshot["step5_ownership"];
  if (value.step5_ownership !== undefined) {
    if (!isObject(value.step5_ownership) || Object.keys(value.step5_ownership).length > MAX_LINES) invalid("The saved Step 5 ownership map is invalid.");
    step5_ownership = {};
    for (const [key, rawOwner] of Object.entries(value.step5_ownership)) {
      if (!/^\d{1,2}:\d{1,3}$/.test(key) || !isObject(rawOwner) || !["step4", "manual"].includes(String(rawOwner.glassRef)) || !["step4", "manual"].includes(String(rawOwner.rate))) invalid("A Step 5 ownership marker is invalid.");
      step5_ownership[key] = { glassRef: rawOwner.glassRef as "step4" | "manual", rate: rawOwner.rate as "step4" | "manual" };
    }
  }
  return { step4_groups, step6: { mode: rawStep6.mode, charges, wastages, customCharges, manualActiveKeys: stringList(rawStep6.manualActiveKeys ?? [], 100), manualTargetKeys: stringList(rawStep6.manualTargetKeys ?? [], 100), manualWastageIds: stringList(rawStep6.manualWastageIds ?? [], 200) }, ...(step5_ownership ? { step5_ownership } : {}) };
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
    if (!(w1 >= 1 && w1 <= 10000) || !(h1 >= 1 && h1 <= 10000)) invalid(`Line ${index + 1} needs primary width and height between 1 and 10000 mm.`);
    if (!(w2 >= 0 && w2 <= 20000) || !(h2 >= 0 && h2 <= 20000)) invalid(`Line ${index + 1} has an inner panel size outside 0 to 20000 mm.`);
    if (!(qty >= 1 && qty <= 10000)) invalid(`Line ${index + 1} needs a quantity between 1 and 10000.`);
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
  const workflowSnapshot = input.workflow_snapshot === undefined ? undefined : validateWorkflowSnapshot(input.workflow_snapshot);

  const client = await authorizedClient(token, "write");
  const groups = (input.groups || []).map((group) => ({
    group_no: Math.trunc(group.group_no),
    base_rate: Math.round(group.base_rate * 100) / 100,
    surcharge_percent: Math.round(group.surcharge_percent * 100) / 100,
  }));

  /* v3 writes Step 4/6 snapshot and line/rate data under one revision. */
  const args = {
    p_inquiry_id: inquiryId,
    p_idempotency_key: input.idempotency_key,
    p_expected_revision: input.expected_revision,
    p_currency: currency,
    p_lines: lines,
    p_groups: groups,
  };
  let saved = workflowSnapshot
    ? await client.rpc("save_inquiry_proforma_v3", { ...args, p_workflow_snapshot: workflowSnapshot })
    : await client.rpc("save_inquiry_proforma_v2", args);
  if (saved.error && (saved.error.code === "42883" || saved.error.code === "PGRST202") && workflowSnapshot) {
    saved = await client.rpc("save_inquiry_proforma_v2", args);
  }
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
