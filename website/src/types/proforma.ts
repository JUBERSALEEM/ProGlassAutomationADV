import type { InquiryStatus } from "@/types/inquiries";
import type { ProformaStatus } from "@/lib/proforma-status";
import type { Step4Build, Step5Ownership, Step6Draft } from "@/lib/proforma-workflow";

export type ProformaWorkflowSnapshot = {
  step4_groups: Record<number, Step4Build>;
  step6: Step6Draft;
  /** Ownership markers are keyed by persisted group/line position. */
  step5_ownership?: Step5Ownership;
};

export type { ProformaStatus };

/**
 * A specification's stored pricing settings, pinned to the revision that saved
 * them. `base_rate` and `surcharge_percent` are what the rate bar shows; the saved
 * money is still the per-line rate the write function priced.
 */
export type ProformaGroupSetting = {
  group_no: number;
  base_rate: number;
  surcharge_percent: number;
};
export type ProformaSourceSystem = "website" | "desktop";

/**
 * One priced line of a website proforma.
 *
 * `sqm` is the billable panel area rounded to WPF's three-decimal
 * midpoint-to-even precision before applying the half-square-metre floor;
 * `total_sqm` is that area times quantity, rounded to three decimals, and
 * `line_value` is the money-rounded total at the approved rate. The database
 * computes all three in the current Phase C8 save RPCs, so a read-back is the
 * record of what was accepted, not what a browser proposed.
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
  /** The settings stored for the revision this workspace loaded, if any. */
  groups: ProformaGroupSetting[];
  /**
   * False when this deployment has no `inquiry_proforma_groups` table or no
   * `save_inquiry_proforma_v2`, so the rate bar still works but cannot store its
   * two inputs. The page says so rather than pretending they were saved.
   */
  groups_persisted: boolean;
  /** Step 4 build inputs and Step 6 charges pinned to the loaded revision. */
  workflow_snapshot: ProformaWorkflowSnapshot | null;
  workflow_snapshot_persisted: boolean;
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
  /** Specification settings to store with this revision. Omitted when unknown. */
  groups?: Array<{ group_no: number; base_rate: number; surcharge_percent: number }>;
  workflow_snapshot?: ProformaWorkflowSnapshot;
};

export type ProformaApiError = {
  code: "UNAUTHORIZED" | "FORBIDDEN" | "NOT_FOUND" | "INVALID" | "CONFLICT" | "NOT_CONFIGURED" | "TEMPORARY_FAILURE";
  message: string;
};

export type ProformaResponse =
  | { ok: true; data: ProformaWorkspaceData }
  | { ok: false; error: ProformaApiError };
