/**
 * Confirmed PI queue and website production-release tracking types.
 *
 * Confirmed source lines remain in Excel Sync's public.records table. An
 * authorized operator can create a separate website production Job Order
 * snapshot and track its stage quantities without mutating the source PI or
 * desktop application's local Job Order database.
 */
import type { DatePreset, PaginationInfo } from "@/types/records";

import type { TrackedProductionJobOrder } from "@/lib/job-order-production";

export type JobOrderSourceLine = {
  id: string;
  type_of_work: string;
  qty: number;
  sqm: number;
};

export type JobOrderRow = {
  /** PI number plus workbook is the idempotent hand-off key. */
  id: string;
  pi_number: string;
  company: string;
  customer_ref: string;
  salesman: string;
  prepared_by: string;
  type_of_work: string;
  order_date: string;
  confirmation_status: "Confirmed";
  pi_status: string;
  zoho_status: string;
  ordered_qty: number;
  ordered_sqm: number;
  record_count: number;
  record_ids: (string | number)[];
  source_lines: JobOrderSourceLine[];
  source_file: string | null;
  production_job_order?: TrackedProductionJobOrder | null;
};

export type JobOrderFilters = {
  search: string;
  datePreset: DatePreset;
  dateFrom: string;
  dateTo: string;
  company: string;
  salesman: string;
  preparedBy: string;
  typeOfWork: string;
  /** `current`, `all`, or one of the source workbook names. */
  sourceFile?: string;
};

export type JobOrderSortField = "order_date" | "pi_number" | "company" | "ordered_qty" | "ordered_sqm";
export type JobOrderSort = { field: JobOrderSortField; direction: "asc" | "desc" };

export type JobOrderSummary = {
  totalJobOrders: number;
  totalSourceRows: number;
  totalQty: number;
  totalSqm: number;
  activeCompaniesCount: number;
  activeSalesmenCount: number;
};

export type JobOrderFilterOptions = {
  companies: string[];
  salesmen: string[];
  preparedBy: string[];
  typesOfWork: string[];
  sourceFiles: string[];
  currentSourceFile: string | null;
};

export type JobOrdersApiData = {
  rows: JobOrderRow[];
  summary: JobOrderSummary;
  filterOptions: JobOrderFilterOptions;
  pagination: PaginationInfo;
  serverTime: string;
  executionMs: number;
  source: "supabase" | "mock_preview";
  currentSourceFile: string | null;
  isCurrentSourceScoped: boolean;
  productionTrackingAvailable: boolean;
  warning?: string;
};

export type JobOrdersApiSuccessResponse = { ok: true; data: JobOrdersApiData };
export type JobOrdersApiErrorResponse = {
  ok: false;
  error: { message: string; code?: string; details?: string };
};
export type JobOrdersApiResponse = JobOrdersApiSuccessResponse | JobOrdersApiErrorResponse;
