/**
 * Read-only Job Order queue types.
 *
 * A website job-order candidate is a confirmed proforma, grouped once per PI.
 * The website does not create or mutate desktop Job Order documents: the
 * authoritative source remains Excel Sync's public.records table.
 */
import type { DatePreset, PaginationInfo } from "@/types/records";

export type JobOrderRow = {
  /** PI number is the hand-off key until a dedicated job-order store exists. */
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
  source_file: string | null;
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
  warning?: string;
};

export type JobOrdersApiSuccessResponse = { ok: true; data: JobOrdersApiData };
export type JobOrdersApiErrorResponse = {
  ok: false;
  error: { message: string; code?: string; details?: string };
};
export type JobOrdersApiResponse = JobOrdersApiSuccessResponse | JobOrdersApiErrorResponse;
