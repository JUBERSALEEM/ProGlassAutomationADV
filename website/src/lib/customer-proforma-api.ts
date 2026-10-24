import { isSupabaseConfigured, isSupabaseUnavailableError, supabaseServerWithToken } from "@/lib/supabase";
import type { ProformaInquiryHeader, ProformaLine, ProformaRecord, ProformaStatus } from "@/types/proforma";
import { normaliseProformaStatus } from "@/lib/proforma-status";

/**
 * A customer's own proforma invoices.
 *
 * This module never decides who may read a document. It sends the caller's own
 * token to PostgREST, and the row level policies added by phase C7 are what limit
 * the answer to the documents shared with exactly that account, at exactly the
 * revision that was shared. A bug here can at worst show the caller nothing; it
 * cannot show them somebody else's price.
 */

export class CustomerProformaFailure extends Error {
  code: "UNAUTHORIZED" | "NOT_FOUND" | "NOT_CONFIGURED" | "TEMPORARY_FAILURE" | "INTERNAL_ERROR";
  constructor(code: CustomerProformaFailure["code"], message: string) {
    super(message);
    this.code = code;
  }
}

export type CustomerProformaSummary = {
  id: string;
  proforma_number: string;
  inquiry_number: string;
  revision: number;
  status: ProformaStatus;
  currency: string;
  line_count: number;
  total_sqm: number;
  total_value: number;
  updated_at: string;
  /** When this revision was shared with the account, from the share ledger. */
  shared_at: string;
};

function numeric(value: unknown): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
}

function databaseFailure(error: { code?: string; message?: string }): never {
  if (isSupabaseUnavailableError(error)) {
    throw new CustomerProformaFailure("TEMPORARY_FAILURE", "The document store is temporarily unreachable. Please try again.");
  }
  if (/jwt|token|expired|unauthorized|permission denied|42501|PGRST301/i.test(`${error.code || ""} ${error.message || ""}`)) {
    throw new CustomerProformaFailure("UNAUTHORIZED", "Your session has expired. Sign in again to see your documents.");
  }
  console.error("[customer-proforma-api] Supabase query failed:", { code: error.code, message: error.message });
  throw new CustomerProformaFailure("TEMPORARY_FAILURE", "Your documents could not be loaded. Please try again.");
}

async function clientFor(token: string) {
  if (!token) throw new CustomerProformaFailure("UNAUTHORIZED", "Sign in to see your proforma invoices.");
  if (!isSupabaseConfigured()) throw new CustomerProformaFailure("NOT_CONFIGURED", "The document store is not configured.");
  const client = supabaseServerWithToken(token);
  const user = await client.auth.getUser(token);
  if (user.error && isSupabaseUnavailableError(user.error)) {
    throw new CustomerProformaFailure("TEMPORARY_FAILURE", "The authentication service is temporarily unreachable.");
  }
  if (user.error || !user.data.user) throw new CustomerProformaFailure("UNAUTHORIZED", "Your session has expired.");
  return { client, user: user.data.user };
}

/** One stored row, as the customer list shows it. Pure, so it can be tested. */
export function toCustomerSummary(row: Record<string, unknown>): CustomerProformaSummary {
  const embedded = row.inquiry_proforma_shares;
  const share = Array.isArray(embedded) ? (embedded[0] as { shared_at?: string } | undefined) : null;
  return {
    id: String(row.id),
    proforma_number: String(row.proforma_number || ""),
    inquiry_number: String(row.inquiry_number || ""),
    revision: numeric(row.revision),
    status: normaliseProformaStatus(row.status as string | null),
    currency: String(row.currency || "AED"),
    line_count: numeric(row.line_count),
    total_sqm: numeric(row.total_sqm),
    total_value: numeric(row.total_value),
    updated_at: String(row.updated_at || ""),
    shared_at: String(share?.shared_at || ""),
  };
}

/** One row per shared document, newest first. */
export async function listCustomerProformas(token: string): Promise<CustomerProformaSummary[]> {
  const { client } = await clientFor(token);
  const { data, error } = await client
    .from("inquiry_proformas")
    .select("id,proforma_number,inquiry_number,revision,status,currency,line_count,total_sqm,total_value,updated_at,inquiry_proforma_shares(shared_at,revision)")
    .order("updated_at", { ascending: false })
    .limit(50);
  if (error) databaseFailure(error);

  const rows = (data || []) as unknown as Array<Record<string, unknown>>;
  return rows
    .map(toCustomerSummary)
    // A proforma the caller cannot see the share row for is not theirs; the
    // policies already exclude it, and this keeps the list honest if a policy is
    // ever relaxed by mistake.
    .filter((row) => row.shared_at !== "");
}

export type CustomerProformaDocument = {
  proforma: ProformaRecord;
  inquiry: ProformaInquiryHeader;
  lines: ProformaLine[];
  shared_at: string;
};

function recordRow(row: Record<string, unknown>): ProformaRecord {
  return {
    id: String(row.id),
    inquiry_id: String(row.inquiry_id || ""),
    proforma_number: String(row.proforma_number || ""),
    source_system: (row.source_system as ProformaRecord["source_system"]) || "website",
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
  } as ProformaRecord;
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
  } as ProformaLine;
}

/** One shared document, with the lines of the revision the account was given. */
export async function fetchCustomerProforma(token: string, proformaId: string): Promise<CustomerProformaDocument> {
  const { client } = await clientFor(token);
  const { data, error } = await client
    .from("inquiry_proformas")
    .select("*")
    .eq("id", proformaId)
    .maybeSingle();
  if (error) databaseFailure(error);
  if (!data) throw new CustomerProformaFailure("NOT_FOUND", "That proforma invoice is not available to this account.");

  const record = recordRow(data as unknown as Record<string, unknown>);

  const [lines, share] = await Promise.all([
    client
      .from("inquiry_proforma_lines")
      .select("group_no,line_no,glass_ref,w1,h1,w2,h2,qty,approved_rate,sqm,total_sqm,line_value")
      .eq("proforma_id", proformaId)
      .order("group_no", { ascending: true })
      .order("line_no", { ascending: true }),
    client
      .from("inquiry_proforma_shares")
      .select("shared_at,revision")
      .eq("proforma_id", proformaId)
      .maybeSingle(),
  ]);
  if (lines.error) databaseFailure(lines.error);
  if (share.error) databaseFailure(share.error);

  const inquiry: ProformaInquiryHeader = {
    id: String((data as unknown as Record<string, unknown>).inquiry_id || ""),
    inquiry_number: record.inquiry_number,
    customer_name: record.customer_name,
    project_name: String((data as unknown as Record<string, unknown>).project_name || ""),
    status: "Prepared",
    proforma_number: record.proforma_number,
    inquiry_value: record.total_value,
  };

  return {
    proforma: record,
    inquiry,
    lines: ((lines.data || []) as unknown as Record<string, unknown>[]).map(lineRow),
    shared_at: String((share.data as unknown as Record<string, unknown> | null)?.shared_at || ""),
  };
}
