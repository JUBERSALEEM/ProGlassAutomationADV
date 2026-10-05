/**
 * Confirmed PI release queue and persisted production progress data layer.
 *
 * The release candidates come from Excel Sync's public.records table; only
 * active `Confirmed` rows enter. Production release snapshots and stage
 * progress live separately in Supabase, grouped by PI and source workbook.
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
  JobOrderRow,
  JobOrderSort,
  JobOrdersApiData,
} from "@/types/job-orders";
import type { TrackedProductionJobOrder, TrackedProductionStage } from "@/lib/job-order-production";
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

type ProductionOrderRecord = {
  id: string;
  job_order_number: string;
  source_pi_number: string;
  source_file: string;
  status: string;
  released_at: string;
  ordered_qty: number | string;
  ordered_sqm: number | string;
  route_source: string;
  operations?: ProductionOperationRecord[];
};

type ProductionOperationRecord = {
  id: string;
  job_order_id: string;
  stage_code: string;
  stage_label: string;
  stage_order: number;
  planned_qty: number | string;
  completed_qty: number | string;
  quantity_unit: string;
  status: string;
  updated_at: string;
};

function missingProductionSchema(error: { code?: string; message?: string }): boolean {
  return error.code === "42P01" || error.code === "PGRST205" ||
    /production_job_orders|production_job_order_operations|schema cache|does not exist/i.test(error.message || "");
}

function numericValue(value: number | string): number {
  const parsed = typeof value === "number" ? value : Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
}

async function attachProductionOrders(
  rows: JobOrderRow[],
  authToken: string | null,
): Promise<{ rows: JobOrderRow[]; available: boolean; warning?: string }> {
  const empty = rows.map((row) => ({ ...row, production_job_order: null }));
  const client = authToken ? supabaseServerWithToken(authToken) : supabaseServer();
  if (rows.length === 0) {
    const parentProbe = await client.from("production_job_orders").select("id").limit(1);
    const operationProbe = parentProbe.error
      ? parentProbe
      : await client.from("production_job_order_operations").select("id").limit(1);
    if (operationProbe.error) {
      return {
        rows: empty,
        available: false,
        warning: missingProductionSchema(operationProbe.error)
          ? "Production tracking is not installed in this Supabase project. Run website/supabase-job-orders.sql after website/supabase-dashboard.sql."
          : "The Job Order queue loaded, but Supabase would not return production-release data. Check authenticated SELECT policies on the production tracking tables.",
      };
    }
    return { rows: empty, available: true };
  }

  const piNumbers = Array.from(new Set(rows.map((row) => row.pi_number.trim().toLocaleUpperCase()).filter(Boolean)));
  const jobs: ProductionOrderRecord[] = [];
  let queryError: { code?: string; message?: string } | null = null;
  const batchSize = 500;

  for (let start = 0; start < piNumbers.length && !queryError; start += batchSize) {
    const piBatch = piNumbers.slice(start, start + batchSize);
    let offset = 0;
    while (true) {
      const { data, error } = await client
        .from("production_job_orders")
        .select("id, job_order_number, source_pi_number, source_file, status, released_at, ordered_qty, ordered_sqm, route_source, operations:production_job_order_operations(id, job_order_id, stage_code, stage_label, stage_order, planned_qty, completed_qty, quantity_unit, status, updated_at)")
        .in("source_pi_number", piBatch)
        .order("source_pi_number", { ascending: true })
        .order("source_file", { ascending: true })
        .range(offset, offset + batchSize - 1);
      if (error) {
        queryError = error;
        break;
      }
      const batch = (data ?? []) as unknown as ProductionOrderRecord[];
      jobs.push(...batch);
      if (batch.length < batchSize) break;
      offset += batch.length;
    }
  }

  if (queryError) {
    return {
      rows: empty,
      available: false,
      warning: missingProductionSchema(queryError)
        ? "Production tracking is not installed or incomplete in this Supabase project. Run website/supabase-job-orders.sql after website/supabase-dashboard.sql."
        : "The Job Order queue loaded, but Supabase would not return production-release data. Check authenticated SELECT policies on the production tracking tables.",
    };
  }

  const bySource = new Map<string, ProductionOrderRecord>();
  for (const job of jobs) {
    bySource.set(`${job.source_pi_number.trim().toLocaleUpperCase()}\u0000${job.source_file.trim().toLocaleLowerCase()}`, job);
  }

  return {
    available: true,
    rows: rows.map((row) => {
      const sourceKey = `${row.pi_number.trim().toLocaleUpperCase()}\u0000${(row.source_file || "").trim().toLocaleLowerCase()}`;
      const job = bySource.get(sourceKey);
      if (!job) return { ...row, production_job_order: null };
      const status = ["Released", "In Production", "Completed"].includes(job.status)
        ? job.status as TrackedProductionJobOrder["status"]
        : "Released";
      const stages = (job.operations ?? []).map((operation) => ({
        id: operation.id,
        stage_code: operation.stage_code as TrackedProductionStage["stage_code"],
        stage_label: operation.stage_label,
        stage_order: Number(operation.stage_order),
        planned_qty: numericValue(operation.planned_qty),
        completed_qty: numericValue(operation.completed_qty),
        quantity_unit: operation.quantity_unit as TrackedProductionStage["quantity_unit"],
        status: operation.status as TrackedProductionStage["status"],
        updated_at: operation.updated_at,
      })).sort((a, b) => a.stage_order - b.stage_order);
      return {
        ...row,
        production_job_order: {
          id: job.id,
          job_order_number: job.job_order_number,
          status,
          source_file: job.source_file,
          released_at: job.released_at,
          ordered_qty: numericValue(job.ordered_qty),
          ordered_sqm: numericValue(job.ordered_sqm),
          route_source: "records_summary",
          stages,
        },
      };
    }),
  };
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
      .eq("status", JOB_ORDER_CONFIRMED_STATUS)
      .order("pi_number", { ascending: true, nullsFirst: false })
      .order("id", { ascending: true })
      .range(offset, offset + READ_BATCH_SIZE - 1);

    if (useActiveFlag) query = query.eq("is_active", true);
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

  let pageRows = rows;
  let productionTrackingAvailable = false;
  let productionTrackingWarning: string | undefined;
  if (source === "supabase") {
    const production = await attachProductionOrders(rows, authToken ?? null);
    pageRows = production.rows;
    productionTrackingAvailable = production.available && !degraded;
    productionTrackingWarning = degraded
      ? "Production release is disabled because active/workbook scope could not be verified. Apply website/supabase-dashboard.sql, then refresh."
      : production.warning;
  } else {
    productionTrackingWarning = "Production release tracking requires live Supabase data; demo preview rows cannot be released.";
  }

  if (truncated) {
    warning = `The confirmed PI read reached the ${MAX_SOURCE_ROWS.toLocaleString()} source-row safety limit. Counts are a lower bound; narrow the workbook or date range.`;
  } else if (degraded) {
    warning =
      "The public.records schema is missing is_active and/or source_file. Active/workbook scope cannot be verified; release is disabled until website/supabase-dashboard.sql is applied.";
  } else if (latestRowsAreUnlabelled) {
    warning =
      "Recent source rows are not labelled with a workbook. All active workbooks are included so newer confirmed proformas are not hidden.";
  } else if (grouped.length === 0 && rawRows.length > 0) {
    warning = "Confirmed source rows were read, but none contained a usable PI number.";
  }
  if (productionTrackingWarning) {
    warning = [warning, productionTrackingWarning].filter(Boolean).join(" ");
  }

  return {
    rows: pageRows,
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
    productionTrackingAvailable,
    ...(warning ? { warning } : {}),
  };
}
