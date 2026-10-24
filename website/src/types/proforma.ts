import type { InquiryStatus } from "@/types/inquiries";

export type ProformaStatus = "Draft" | "Issued" | "Void";
export type ProformaSourceSystem = "website" | "desktop";

/**
 * One priced line of a website proforma.
 *
 * `sqm` is the billable panel area, floored at half a square metre, `total_sqm`
 * is that area times the quantity, and `line_value` is `total_sqm` times the
 * approved rate. All three are computed by the database on save, so a line read
 * back is the record of what was accepted, not what a browser proposed.
 */
export type ProformaLine = {
  group_no: number;
  line_no: number;
  glass_ref: string;
  w1: number;
  h1: number;
  w2: number;
  h2: number;
  qty: number;
  approved_rate: number;
  sqm: number;
  total_sqm: number;
  line_value: number;
};

export type ProformaRecord = {
  id: string;
  inquiry_id: string;
  proforma_number: string;
  source_system: ProformaSourceSystem;
  source_proforma_number: string;
  inquiry_number: string;
  customer_name: string;
  status: ProformaStatus;
  currency: string;
  revision: number;
  line_count: number;
  total_sqm: number;
  total_value: number;
  created_at: string;
  updated_at: string;
};

export type ProformaSaveSummary = {
  revision: number;
  line_count: number;
  total_value: number;
  saved_at: string;
};

export type ProformaInquiryHeader = {
  id: string;
  inquiry_number: string;
  customer_name: string;
  project_name: string;
  status: InquiryStatus;
  proforma_number: string;
  inquiry_value: number;
};

export type ProformaWorkspaceData = {
  inquiry: ProformaInquiryHeader;
  proforma: ProformaRecord | null;
  lines: ProformaLine[];
  saves: ProformaSaveSummary[];
  /**
   * False when the proforma tables are not installed in this deployment. The
   * workspace then says so instead of offering a save that cannot succeed.
   */
  installed: boolean;
};

export type ProformaLineInput = {
  group_no: number;
  line_no: number;
  glass_ref: string;
  w1: number;
  h1: number;
  w2: number;
  h2: number;
  qty: number;
  rate: number;
};

export type ProformaSaveInput = {
  idempotency_key: string;
  expected_revision: number;
  currency: string;
  lines: ProformaLineInput[];
};

export type ProformaApiError = {
  code: "UNAUTHORIZED" | "FORBIDDEN" | "NOT_FOUND" | "INVALID" | "CONFLICT" | "NOT_CONFIGURED" | "TEMPORARY_FAILURE";
  message: string;
};

export type ProformaResponse =
  | { ok: true; data: ProformaWorkspaceData }
  | { ok: false; error: ProformaApiError };
