/**
 * Read-only Job Order queue data layer.
 *
 * The source is the Excel Sync proforma record set, not a speculative
 * `job_orders` table: only rows whose `status` is exactly `Confirmed` enter the
 * queue. Rows are grouped by PI before totals, filtering and pagination, so a
 * PI with several source lines still appears once.
 */
import { isSupabaseConfigured, supabaseServer, supabaseServerWithToken } from "@/lib/supabase";
import { fetchRecordsDashboardData, resolveDateRange } from "@/lib/records-api";
import { buildSourceFileFilter, resolveSourceScope } from "@/lib/source-scope";
import {
  buildJobOrderFilterOptions,
  buildJobOrderSummary,
  filterJobOrders,
  groupConfirmedProformas,
  sortJobOrders,
  type JobOrderSourceRecord,
} from "@/lib/job-order-ledger";
import type {
  JobOrderFilters,
  JobOrderSort,
  JobOrdersApiData,
} from "@/types/job-orders";
import type { RecordRow } from "@/types/records";

export const JOB_ORDER_CONFIRMED_STATUS = "Confirmed";
const READ_BATCH_SIZE = 1_000;
const MAX_SOURCE_ROWS = 25_000;
const MOCK_PAGE_SIZE = 250;

const CORE_COLUMNS = [
  "id",
  "date",
  "pi_number",
  "company",
  "customer_ref",
  "salesman",
  "prepared_by",
  "type_of_work",
  "status",
  "pi_status",
  "zoho_status",
  "qty",
  "sqm",
].join(", ");
const EXTENDED_COLUMNS = "source_file";

function isMissingColumn(error: { code?: string; message?: string }, column: string): boolean {
  const message = `${error.code ?? ""} ${error.message ?? ""}`.toLocaleLowerCase();
  return (
    message.includes(column.toLocaleLowerCase()) &&
    (message.includes("column") || message.includes("schema cache") || message.includes("does not exist"))
  );
}

function errorMessage(error: { code?: string; message?: string }): string {
  return `Supabase confirmed-proforma query failed [${error.code || "unknown"}]: ${error.message || "Unknown database error"}`;
}

async function readConfirmedSourceRows(
  client: ReturnType<typeof supabaseServer>,
  effectiveSourceFile: string | null,
): Promise<{
  rows: JobOrderSourceRecord[];
  truncated: boolean;
  degraded: boolean;
}> {
  const rows: JobOrderSourceRecord[] = [];
  let offset = 0;
  let useActiveFlag = true;
  let useExtendedColumns = true;
  let degraded = false;

  while (offset < MAX_SOURCE_ROWS) {
    const columns = [
      CORE_COLUMNS,
      ...(useActiveFlag ? ["is_active"] : []),
      ...(useExtendedColumns ? [EXTENDED_COLUMNS] : []),
    ].join(", ");

    let query = client
      .from("records")
      .select(columns)
      .ilike("status", JOB_ORDER_CONFIRMED_STATUS)
      .order("pi_number", { ascending: true, nullsFirst: false })
      .order("id", { ascending: true })
      .range(offset, offset + READ_BATCH_SIZE - 1);

    if (useActiveFlag) query = query.neq("is_active", false);
    if (effectiveSourceFile && useExtendedColumns) {
      query = query.or(buildSourceFileFilter(effectiveSourceFile));
    }

    const { data, error } = await query;
    if (error) {
      if (useActiveFlag && isMissingColumn(error, "is_active")) {
        useActiveFlag = false;
        degraded = true;
        continue;
      }
      if (useExtendedColumns && isMissingColumn(error, "source_file")) {
        if (effectiveSourceFile) {
          throw new Error(`Workbook scope '${effectiveSourceFile}' cannot be applied because public.records.source_file is unavailable.`);
        }
        useExtendedColumns = false;
        degraded = true;
        continue;
      }
      throw new Error(errorMessage(error));
    }

    const batch = (data ?? []) as unknown as JobOrderSourceRecord[];
    rows.push(...batch);
    offset += batch.length;
    if (batch.length < READ_BATCH_SIZE) break;
  }

  return {
    rows,
    truncated: offset >= MAX_SOURCE_ROWS,
    degraded,
  };
}

export async function fetchJobOrdersDashboardData({
  filters,
  sort = { field: "order_date", direction: "desc" },
  page = 1,
  pageSize = 50,
  authToken,
}: {
  filters: JobOrderFilters;
  sort?: JobOrderSort;
  page?: number;
  pageSize?: number;
  authToken?: string | null;
}): Promise<JobOrdersApiData> {
  const started = Date.now();
  const clampedPageSize = Math.min(Math.max(Number(pageSize) || 50, 10), MAX_SOURCE_ROWS);
  const clampedPage = Math.max(Number(page) || 1, 1);
  const { from, to } = resolveDateRange(filters.datePreset, filters.dateFrom, filters.dateTo);
  const isProduction = process.env.NODE_ENV === "production";

  if (isProduction && !isSupabaseConfigured()) {
    throw new Error(
      "Supabase environment variables (NEXT_PUBLIC_SUPABASE_URL and NEXT_PUBLIC_SUPABASE_ANON_KEY) are not configured in production.",
    );
  }

  let rawRows: JobOrderSourceRecord[];
  let source: JobOrdersApiData["source"];
  let currentSourceFile: string | null = null;
  let isCurrentSourceScoped = false;
  let warning: string | undefined;
  let truncated = false;
  let degraded = false;

  let availableSourceFiles: string[] = [];
  let latestRowsAreUnlabelled = false;

  if (isSupabaseConfigured()) {
    const client = authToken ? supabaseServerWithToken(authToken) : supabaseServer();
    const scope = await resolveSourceScope(client, filters.sourceFile);
    const result = await readConfirmedSourceRows(client, scope.effectiveSourceFile);
    rawRows = result.rows;
    truncated = result.truncated;
    degraded = result.degraded;
    source = "supabase";
    currentSourceFile = scope.currentSourceFile;
    availableSourceFiles = scope.availableSourceFiles;
    isCurrentSourceScoped = Boolean(scope.effectiveSourceFile);
    latestRowsAreUnlabelled = scope.detectedLatestHasNoSourceFile;
  } else {
    // Keep local development useful without a Supabase project. This reuses the
    // records dashboard's isolated mock data and still fixes the status filter
    // server-side rather than accepting a status from the request.
    const mock = await fetchRecordsDashboardData({
      filters: {
        search: "",
        datePreset: "all",
        dateFrom: "",
        dateTo: "",
        company: "",
        salesman: "",
        preparedBy: "",
        status: JOB_ORDER_CONFIRMED_STATUS,
        piStatus: "",
        typeOfWork: "",
        zohoStatus: "",
        sourceFile: filters.sourceFile || "",
      },
      sort: { field: "date", direction: "asc" },
      page: 1,
      pageSize: MOCK_PAGE_SIZE,
      authToken,
    });
    rawRows = mock.records as RecordRow[] as JobOrderSourceRecord[];
    source = mock.source;
    currentSourceFile = mock.currentSourceFile ?? mock.filterOptions.currentSourceFile ?? null;
    availableSourceFiles = mock.sourceFiles ?? mock.filterOptions.sourceFiles ?? [];
    isCurrentSourceScoped = Boolean(mock.isCurrentSourceScoped);
    if (mock.pagination.totalEntries > mock.records.length) {
      warning =
        "The local preview contains more confirmed source rows than this page can load; configure Supabase for the live queue.";
    }
  }

  const grouped = groupConfirmedProformas(rawRows);
  const filtered = filterJobOrders(grouped, filters, { from, to });
  const sorted = sortJobOrders(filtered, sort);
  const totalEntries = sorted.length;
  const offset = (clampedPage - 1) * clampedPageSize;
  const rows = sorted.slice(offset, offset + clampedPageSize);
  const sourceFiles = availableSourceFiles.length > 0
    ? availableSourceFiles
    : Array.from(
        new Set(grouped.map((row) => row.source_file).filter((value): value is string => Boolean(value))),
      ).sort((a, b) => a.localeCompare(b));

  if (truncated) {
    warning = `The confirmed PI read reached the ${MAX_SOURCE_ROWS.toLocaleString()} source-row safety limit. Counts are a lower bound; narrow the workbook or date range.`;
  } else if (degraded) {
    warning =
      "The source_file column is unavailable. Confirmed proformas are still read, but workbook scoping cannot be enforced.";
  } else if (latestRowsAreUnlabelled) {
    warning =
      "Recent source rows are not labelled with a workbook. All active workbooks are included so newer confirmed proformas are not hidden.";
  } else if (grouped.length === 0 && rawRows.length > 0) {
    warning = "Confirmed source rows were read, but none contained a usable PI number.";
  }

  return {
    rows,
    summary: buildJobOrderSummary(filtered),
    filterOptions: buildJobOrderFilterOptions(grouped, {
      sourceFiles,
      currentSourceFile,
    }),
    pagination: {
      page: clampedPage,
      pageSize: clampedPageSize,
      totalEntries,
      totalPages: Math.max(1, Math.ceil(totalEntries / clampedPageSize)),
    },
    serverTime: new Date().toISOString(),
    executionMs: Date.now() - started,
    source,
    currentSourceFile,
    isCurrentSourceScoped,
    ...(warning ? { warning } : {}),
  };
}
