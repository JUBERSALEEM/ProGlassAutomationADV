/**
 * ProGlass Automation — Records & Operations Dashboard Types
 *
 * Types matching the production Supabase `public.records` table schema
 * and the dashboard's aggregation, filtering, and pagination architecture.
 */

export type RecordRow = {
  id: string | number;
  date: string;
  salesman: string;
  company: string;
  pi_number: string;
  type_of_work: string;
  customer_ref: string;
  qty: number | string;
  sqm: number | string;
  prepared_by: string;
  status: string;
  pi_status: string;
  zoho_status: string;
  source_file?: string | null;
  source_worksheet?: string | null;
  created_at?: string | null;
  updated_at?: string | null;
};

export type DatePreset =
  | "all"
  | "today"
  | "yesterday"
  | "this_week"
  | "this_month"
  | "previous_month"
  | "custom";

export type RecordFilters = {
  search: string;
  datePreset: DatePreset;
  dateFrom: string;
  dateTo: string;
  company: string;
  salesman: string;
  preparedBy: string;
  status: string;
  piStatus: string;
  typeOfWork: string;
  zohoStatus: string;
};

export type RecordSortField =
  | "date"
  | "pi_number"
  | "customer_ref"
  | "company"
  | "salesman"
  | "prepared_by"
  | "qty"
  | "sqm"
  | "status"
  | "pi_status"
  | "type_of_work"
  | "zoho_status";

export type SortDirection = "asc" | "desc";

export type RecordSort = {
  field: RecordSortField;
  direction: SortDirection;
};

export type RecordSummary = {
  totalEntries: number;
  totalQty: number;
  totalSqm: number;
  averageSqm: number;
  activeCompaniesCount: number;
  activeSalesmenCount: number;
};

export type BreakdownItem = {
  name: string;
  count: number;
  totalQty: number;
  totalSqm: number;
  percentage: number;
};

export type RecordBreakdowns = {
  preparedBy: BreakdownItem[];
  salesman: BreakdownItem[];
  company: BreakdownItem[];
  typeOfWork: BreakdownItem[];
};

export type FilterOptions = {
  companies: string[];
  salesmen: string[];
  preparedBy: string[];
  statuses: string[];
  piStatuses: string[];
  typesOfWork: string[];
  zohoStatuses: string[];
};

export type PaginationInfo = {
  page: number;
  pageSize: number;
  totalEntries: number;
  totalPages: number;
};

export type RecordsApiSuccessResponse = {
  ok: true;
  data: {
    records: RecordRow[];
    summary: RecordSummary;
    breakdowns: RecordBreakdowns;
    filterOptions: FilterOptions;
    pagination: PaginationInfo;
    serverTime: string;
    source: "supabase" | "mock_preview";
    executionMs: number;
    warning?: string;
    diagnostics?: {
      targetHost: string;
      authRole: string | null;
      recordsCount: number | null;
      recordsLength: number;
      recordsError: { message: string; code?: string; details?: string; hint?: string } | null;
      rpcStatus: "success" | "error" | "not_installed";
      rpcError: { message: string; code?: string; details?: string; hint?: string } | null;
      possibleRlsBlock: boolean;
    };
  };
};

export type RecordsApiErrorResponse = {
  ok: false;
  error: {
    message: string;
    code?: string;
    details?: string;
  };
  fallbackData?: RecordsApiSuccessResponse["data"];
};

export type RecordsApiResponse = RecordsApiSuccessResponse | RecordsApiErrorResponse;
