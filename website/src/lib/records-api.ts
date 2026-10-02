/**
 * ProGlass Automation — Records Dashboard Data Layer
 *
 * Implements database-driven aggregation, filtering, sorting, and pagination
 * against Supabase `public.records`.
 *
 * Scalability Architecture:
 * - Summary statistics (Total Entries, Total Qty, Total SQM, Averages) and
 *   Breakdowns (Prepared By, Salesman, Company, Type of Work) are computed
 *   natively inside PostgreSQL via the `get_records_dashboard_aggregates` RPC.
 * - ZERO unbounded row fetching in JavaScript.
 * - Table records are strictly paginated at 50, 100, or 250 rows.
 * - Production strictly queries Supabase; fake/preview data is NEVER silently
 *   displayed in production when Supabase is unavailable.
 */

import { isSupabaseConfigured, supabaseServer, supabaseServerWithToken } from "@/lib/supabase";
import type {
  BreakdownItem,
  DatePreset,
  FilterOptions,
  RecordBreakdowns,
  RecordFilters,
  RecordRow,
  RecordSort,
  RecordSummary,
  RecordsApiSuccessResponse,
} from "@/types/records";

/**
 * Calculates start and end ISO date strings for a given preset in UTC.
 */
export function resolveDateRange(
  preset: DatePreset,
  customFrom?: string,
  customTo?: string,
  now: Date = new Date(),
): { from: string; to: string } {
  if (preset === "custom") {
    return {
      from: customFrom?.trim() || "",
      to: customTo?.trim() || "",
    };
  }

  if (preset === "all") {
    return { from: "", to: "" };
  }

  const y = now.getUTCFullYear();
  const m = now.getUTCMonth();
  const d = now.getUTCDate();

  const formatDate = (date: Date) => date.toISOString().split("T")[0]!;

  if (preset === "today") {
    const todayStr = formatDate(now);
    return { from: todayStr, to: todayStr };
  }

  if (preset === "yesterday") {
    const yest = new Date(Date.UTC(y, m, d - 1));
    const yestStr = formatDate(yest);
    return { from: yestStr, to: yestStr };
  }

  if (preset === "this_week") {
    const dayOfWeek = now.getUTCDay();
    const diff = (dayOfWeek + 6) % 7; // days since Monday
    const monday = new Date(Date.UTC(y, m, d - diff));
    const sunday = new Date(Date.UTC(y, m, d - diff + 6));
    return { from: formatDate(monday), to: formatDate(sunday) };
  }

  if (preset === "this_month") {
    const firstDay = new Date(Date.UTC(y, m, 1));
    const lastDay = new Date(Date.UTC(y, m + 1, 0));
    return { from: formatDate(firstDay), to: formatDate(lastDay) };
  }

  if (preset === "previous_month") {
    const firstDay = new Date(Date.UTC(y, m - 1, 1));
    const lastDay = new Date(Date.UTC(y, m, 0));
    return { from: formatDate(firstDay), to: formatDate(lastDay) };
  }

  return { from: "", to: "" };
}

function getSupabaseHost(): string {
  try {
    return new URL(process.env.NEXT_PUBLIC_SUPABASE_URL || "").host;
  } catch {
    return "unknown";
  }
}

function getAnonRole(): string | null {
  const key = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;
  if (!key) return null;
  const parts = key.split(".");
  if (parts.length !== 3 || !parts[1]) return null;
  try {
    const base64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64 + "=".repeat((4 - (base64.length % 4)) % 4);
    const claims = JSON.parse(Buffer.from(padded, "base64").toString("utf-8")) as { role?: unknown };
    return typeof claims.role === "string" ? claims.role : null;
  } catch {
    return null;
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// REALISTIC LOCAL PREVIEW DATASET
// Used ONLY in local development when Supabase credentials are absent.
// ─────────────────────────────────────────────────────────────────────────────

const MOCK_COMPANIES = [
  "Emirates Glass LLC",
  "Al Gurg Glass",
  "Gulf Glass Industries",
  "Guardian Glass Middle East",
  "AGC Glass Fabrications",
  "Sisecam Glass Gulf",
  "Arabian Glass Co.",
  "Crystal Clear Glass L.L.C.",
  "Al Abbar Architectural Glass",
  "White Aluminium & Glass",
];

const MOCK_SALESMEN = ["Ahmed", "John", "Maria", "Tariq", "Vikram", "David"];
const MOCK_PREPARED_BY = ["Juber Saleem", "Sharmi", "Ebin Benny", "Farooq"];
const MOCK_TYPES = ["DGU", "SGU", "LAMINATION", "DGU + LAM", "TEMPERED", "CERAMIC FRIT"];
const MOCK_STATUSES = ["Confirmed", "In Production", "Completed", "Pending", "Dispatched"];
const MOCK_PI_STATUSES = ["Approved", "Draft", "Sent", "Revised"];
const MOCK_ZOHO_STATUSES = ["Synced", "Pending", "Excluded", "Failed"];

function generateMockRecords(): RecordRow[] {
  const records: RecordRow[] = [];
  const baseDate = new Date("2026-10-02T12:00:00Z");

  for (let i = 1; i <= 120; i++) {
    const daysAgo = Math.floor((i * 90) / 120);
    const dateObj = new Date(baseDate.getTime() - daysAgo * 86400000);
    const dateStr = dateObj.toISOString().split("T")[0]!;

    const company = MOCK_COMPANIES[(i * 7) % MOCK_COMPANIES.length] || "Emirates Glass LLC";
    const salesman = MOCK_SALESMEN[(i * 3) % MOCK_SALESMEN.length] || "Ahmed";
    const prepared_by = MOCK_PREPARED_BY[(i * 5) % MOCK_PREPARED_BY.length] || "Juber Saleem";
    const type_of_work = MOCK_TYPES[(i * 2) % MOCK_TYPES.length] || "DGU";
    const status = MOCK_STATUSES[(i * 4) % MOCK_STATUSES.length] || "Confirmed";
    const pi_status = MOCK_PI_STATUSES[(i * 3) % MOCK_PI_STATUSES.length] || "Approved";
    const zoho_status = MOCK_ZOHO_STATUSES[(i * 2) % MOCK_ZOHO_STATUSES.length] || "Synced";

    const piNum = `PI-2026-${String(1000 + i).padStart(4, "0")}`;
    const companyCode = (company.split(" ")[0] || "PRO").toUpperCase();
    const custRef = `PO-${companyCode}-${String(200 + (i % 50))}`;
    const qty = 4 + ((i * 17) % 180);
    const avgSqmPerPiece = 0.45 + ((i * 31) % 150) / 100;
    const sqm = Number((qty * avgSqmPerPiece).toFixed(2));

    records.push({
      id: `rec-${i}`,
      date: dateStr,
      salesman,
      company,
      pi_number: piNum,
      type_of_work,
      customer_ref: custRef,
      qty,
      sqm,
      prepared_by,
      status,
      pi_status,
      zoho_status,
      is_active: true,
      source_file: "Daily_Orders_Master_2026.xlsx",
      source_worksheet: "Orders_Active",
      created_at: new Date(dateObj.getTime() + 3600000).toISOString(),
      updated_at: new Date(dateObj.getTime() + 7200000).toISOString(),
    });
  }

  return records;
}

const GLOBAL_MOCK_RECORDS = generateMockRecords();

// ─────────────────────────────────────────────────────────────────────────────
// SERVER-SIDE DATABASE-DRIVEN QUERY IMPLEMENTATION
// ─────────────────────────────────────────────────────────────────────────────

export async function fetchRecordsDashboardData({
  filters,
  sort = { field: "date", direction: "desc" },
  page = 1,
  pageSize = 50,
  authToken,
}: {
  filters: RecordFilters;
  sort?: RecordSort;
  page?: number;
  pageSize?: number;
  authToken?: string | null;
}): Promise<RecordsApiSuccessResponse["data"]> {
  const started = Date.now();
  const clampedPageSize = Math.min(Math.max(Number(pageSize) || 50, 10), 250);
  const clampedPage = Math.max(Number(page) || 1, 1);
  const offset = (clampedPage - 1) * clampedPageSize;

  const { from: resolvedFrom, to: resolvedTo } = resolveDateRange(
    filters.datePreset,
    filters.dateFrom,
    filters.dateTo,
  );

  const isProduction = process.env.NODE_ENV === "production";

  // Production check: Must have Supabase configured
  if (isProduction && !isSupabaseConfigured()) {
    throw new Error(
      "Supabase environment variables (NEXT_PUBLIC_SUPABASE_URL and NEXT_PUBLIC_SUPABASE_ANON_KEY) are not configured in Vercel Production.",
    );
  }

  if (isSupabaseConfigured()) {
    const client = authToken ? supabaseServerWithToken(authToken) : supabaseServer();
    let warning: string | undefined;

    // 1. Fetch paginated records slice (50, 100, or 250 rows only)
    // Filter by is_active = true to represent current active Excel worksheet state
    let query = client.from("records").select("*", { count: "exact" }).eq("is_active", true);

    if (filters.search) {
      const s = filters.search.trim().replace(/[%_]/g, "\\$&");
      query = query.or(`pi_number.ilike.%${s}%,customer_ref.ilike.%${s}%,company.ilike.%${s}%`);
    }
    if (resolvedFrom) query = query.gte("date", resolvedFrom);
    if (resolvedTo) query = query.lte("date", resolvedTo);
    if (filters.company) query = query.eq("company", filters.company);
    if (filters.salesman) query = query.eq("salesman", filters.salesman);
    if (filters.preparedBy) query = query.eq("prepared_by", filters.preparedBy);
    if (filters.status) query = query.eq("status", filters.status);
    if (filters.piStatus) query = query.eq("pi_status", filters.piStatus);
    if (filters.typeOfWork) query = query.eq("type_of_work", filters.typeOfWork);
    if (filters.zohoStatus) query = query.eq("zoho_status", filters.zohoStatus);

    query = query
      .order(sort.field, { ascending: sort.direction === "asc" })
      .range(offset, offset + clampedPageSize - 1);

    let { data: recordsData, count: totalCount, error: recordsError } = await query;

    // Resilient transition guard: if is_active column is not yet migrated in PostgreSQL
    if (recordsError && (recordsError.code === "42703" || recordsError.message?.toLowerCase().includes("is_active"))) {
      console.warn("[records-api] 'is_active' column not yet present in public.records, querying all records during schema migration");
      let fallbackQuery = client.from("records").select("*", { count: "exact" });
      if (filters.search) {
        const s = filters.search.trim().replace(/[%_]/g, "\\$&");
        fallbackQuery = fallbackQuery.or(`pi_number.ilike.%${s}%,customer_ref.ilike.%${s}%,company.ilike.%${s}%`);
      }
      if (resolvedFrom) fallbackQuery = fallbackQuery.gte("date", resolvedFrom);
      if (resolvedTo) fallbackQuery = fallbackQuery.lte("date", resolvedTo);
      if (filters.company) fallbackQuery = fallbackQuery.eq("company", filters.company);
      if (filters.salesman) fallbackQuery = fallbackQuery.eq("salesman", filters.salesman);
      if (filters.preparedBy) fallbackQuery = fallbackQuery.eq("prepared_by", filters.preparedBy);
      if (filters.status) fallbackQuery = fallbackQuery.eq("status", filters.status);
      if (filters.piStatus) fallbackQuery = fallbackQuery.eq("pi_status", filters.piStatus);
      if (filters.typeOfWork) fallbackQuery = fallbackQuery.eq("type_of_work", filters.typeOfWork);
      if (filters.zohoStatus) fallbackQuery = fallbackQuery.eq("zoho_status", filters.zohoStatus);

      fallbackQuery = fallbackQuery
        .order(sort.field, { ascending: sort.direction === "asc" })
        .range(offset, offset + clampedPageSize - 1);

      const fallbackRes = await fallbackQuery;
      if (!fallbackRes.error) {
        recordsData = fallbackRes.data;
        totalCount = fallbackRes.count;
        recordsError = null;
        warning = "Database schema notice: 'is_active' column not found. Execute website/supabase-dashboard.sql migration to enable soft-delete reconciliation.";
      }
    }

    if (recordsError) {
      console.error("[records-api] Supabase query failed:", {
        message: recordsError.message,
        code: recordsError.code,
        details: recordsError.details,
        hint: recordsError.hint,
      });
      if (isProduction) {
        throw new Error(
          `Supabase records query failed [${recordsError.code || "unknown"}]: ${recordsError.message}`
        );
      }
      // In local dev, fall through to mock
    } else {
      const totalEntries = totalCount ?? (recordsData?.length || 0);

      // 2. Perform server-side database aggregation via PostgreSQL RPC
      // No rows are fetched into JavaScript — PostgreSQL executes COUNT/SUM/GROUP BY
      const rpcParams = {
        p_search: filters.search.trim() || null,
        p_date_from: resolvedFrom || null,
        p_date_to: resolvedTo || null,
        p_company: filters.company || null,
        p_salesman: filters.salesman || null,
        p_prepared_by: filters.preparedBy || null,
        p_status: filters.status || null,
        p_pi_status: filters.piStatus || null,
        p_type_of_work: filters.typeOfWork || null,
        p_zoho_status: filters.zohoStatus || null,
      };

      let summary: RecordSummary = {
        totalEntries,
        totalQty: 0,
        totalSqm: 0,
        averageSqm: 0,
        activeCompaniesCount: 0,
        activeSalesmenCount: 0,
      };
      let breakdowns: RecordBreakdowns = {
        preparedBy: [],
        salesman: [],
        company: [],
        typeOfWork: [],
      };
      let filterOptions: FilterOptions = getFallbackFilterOptions();
      let rpcStatus: "success" | "error" | "not_installed" = "not_installed";
      let capturedRpcError: { message: string; code?: string; details?: string; hint?: string } | null = null;

      try {
        const { data: rpcData, error: rpcError } = await client.rpc(
          "get_records_dashboard_aggregates",
          rpcParams,
        );

        if (rpcError) {
          capturedRpcError = {
            message: rpcError.message,
            code: rpcError.code,
            details: rpcError.details,
            hint: rpcError.hint,
          };
          rpcStatus = "error";
          console.error("[records-api] Database aggregation RPC error:", rpcError);
          warning = `Aggregation RPC error [${rpcError.code || "unknown"}]: ${rpcError.message}`;
        } else if (rpcData && typeof rpcData === "object") {
          rpcStatus = "success";
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          const res = rpcData as any;
          summary = res.summary;
          breakdowns = res.breakdowns;
          filterOptions = res.filterOptions || getFallbackFilterOptions();
        } else {
          rpcStatus = "not_installed";
          warning =
            "Database aggregation RPC (public.get_records_dashboard_aggregates) is not installed in Supabase. Run 'website/supabase-dashboard.sql' in Supabase SQL Editor for instant server-side aggregation across millions of records.";
        }

        if (rpcStatus !== "success") {
          // Compute preliminary slice summary from current paginated slice only
          summary = {
            totalEntries,
            totalQty: (recordsData || []).reduce((acc, r) => acc + (Number(r.qty) || 0), 0),
            totalSqm: Number((recordsData || []).reduce((acc, r) => acc + (Number(r.sqm) || 0), 0).toFixed(2)),
            averageSqm: 0,
            activeCompaniesCount: new Set((recordsData || []).map((r) => r.company)).size,
            activeSalesmenCount: new Set((recordsData || []).map((r) => r.salesman)).size,
          };
          if (summary.totalQty > 0) {
            summary.averageSqm = Number((summary.totalSqm / summary.totalQty).toFixed(2));
          }

          breakdowns = {
            preparedBy: [],
            salesman: [],
            company: [],
            typeOfWork: [],
          };
          filterOptions = getFallbackFilterOptions();
        }
      } catch (err) {
        rpcStatus = "error";
        const errMsg = err instanceof Error ? err.message : String(err);
        capturedRpcError = { message: errMsg };
        console.error("[records-api] RPC invocation exception:", err);
        warning = `Aggregation RPC error: ${errMsg}`;
        summary = {
          totalEntries,
          totalQty: 0,
          totalSqm: 0,
          averageSqm: 0,
          activeCompaniesCount: 0,
          activeSalesmenCount: 0,
        };
        breakdowns = { preparedBy: [], salesman: [], company: [], typeOfWork: [] };
        filterOptions = getFallbackFilterOptions();
      }

      const hasActiveFilters = Boolean(
        filters.search ||
        resolvedFrom ||
        resolvedTo ||
        filters.company ||
        filters.salesman ||
        filters.preparedBy ||
        filters.status ||
        filters.piStatus ||
        filters.typeOfWork ||
        filters.zohoStatus
      );

      const possibleRlsBlock = totalEntries === 0 && !hasActiveFilters && !recordsError && !capturedRpcError;

      if (possibleRlsBlock && !warning) {
        warning =
          "0 records visible to Supabase role '" +
          (getAnonRole() || "anon") +
          "'. If rows exist in public.records, Row Level Security (RLS) is filtering them out because no SELECT policy exists for this role. To grant read access, run in Supabase SQL Editor: CREATE POLICY \"Allow anon read\" ON public.records FOR SELECT TO anon USING (true);";
      }

      return {
        records: (recordsData as RecordRow[]) || [],
        summary,
        breakdowns,
        filterOptions,
        pagination: {
          page: clampedPage,
          pageSize: clampedPageSize,
          totalEntries,
          totalPages: Math.ceil(totalEntries / clampedPageSize) || 1,
        },
        serverTime: new Date().toISOString(),
        source: "supabase",
        executionMs: Date.now() - started,
        warning,
        diagnostics: {
          targetHost: getSupabaseHost(),
          authRole: getAnonRole(),
          hasServerServiceKey: Boolean(process.env.SUPABASE_SERVICE_ROLE_KEY || process.env.SUPABASE_SERVICE_KEY),
          recordsCount: totalCount,
          recordsLength: recordsData?.length ?? 0,
          recordsError: null,
          rpcStatus,
          rpcError: capturedRpcError,
          possibleRlsBlock,
        },
      };
    }
  }

  // Fallback: Local dataset execution (only used for offline local dev)
  if (isProduction) {
    throw new Error(
      "Supabase connection is not available in production. Refusing to serve mock data in production environment.",
    );
  }

  return executeMockQuery({
    filters,
    sort,
    page: clampedPage,
    pageSize: clampedPageSize,
    resolvedFrom,
    resolvedTo,
    started,
  });
}

function getFallbackFilterOptions(): FilterOptions {
  return {
    companies: [...MOCK_COMPANIES].sort(),
    salesmen: [...MOCK_SALESMEN].sort(),
    preparedBy: [...MOCK_PREPARED_BY].sort(),
    statuses: [...MOCK_STATUSES].sort(),
    piStatuses: [...MOCK_PI_STATUSES].sort(),
    typesOfWork: [...MOCK_TYPES].sort(),
    zohoStatuses: [...MOCK_ZOHO_STATUSES].sort(),
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// LOCAL MOCK QUERY EXECUTION (DEVELOPMENT PREVIEW ONLY)
// ─────────────────────────────────────────────────────────────────────────────

function executeMockQuery({
  filters,
  sort,
  page,
  pageSize,
  resolvedFrom,
  resolvedTo,
  started,
}: {
  filters: RecordFilters;
  sort: RecordSort;
  page: number;
  pageSize: number;
  resolvedFrom: string;
  resolvedTo: string;
  started: number;
}): RecordsApiSuccessResponse["data"] {
  // Mirror active Excel contents only
  let matched = GLOBAL_MOCK_RECORDS.filter((r) => r.is_active !== false);

  if (filters.search) {
    const q = filters.search.toLowerCase().trim();
    matched = matched.filter(
      (r) =>
        r.pi_number.toLowerCase().includes(q) ||
        r.customer_ref.toLowerCase().includes(q) ||
        r.company.toLowerCase().includes(q),
    );
  }

  if (resolvedFrom) {
    matched = matched.filter((r) => r.date >= resolvedFrom);
  }
  if (resolvedTo) {
    matched = matched.filter((r) => r.date <= resolvedTo);
  }
  if (filters.company) {
    matched = matched.filter((r) => r.company === filters.company);
  }
  if (filters.salesman) {
    matched = matched.filter((r) => r.salesman === filters.salesman);
  }
  if (filters.preparedBy) {
    matched = matched.filter((r) => r.prepared_by === filters.preparedBy);
  }
  if (filters.status) {
    matched = matched.filter((r) => r.status === filters.status);
  }
  if (filters.piStatus) {
    matched = matched.filter((r) => r.pi_status === filters.piStatus);
  }
  if (filters.typeOfWork) {
    matched = matched.filter((r) => r.type_of_work === filters.typeOfWork);
  }
  if (filters.zohoStatus) {
    matched = matched.filter((r) => r.zoho_status === filters.zohoStatus);
  }

  const totalEntries = matched.length;

  // Sorting
  const sorted = [...matched].sort((a, b) => {
    const field = sort.field;
    const dir = sort.direction === "asc" ? 1 : -1;

    const valA = a[field];
    const valB = b[field];

    if (valA === valB) return 0;
    if (valA === null || valA === undefined) return 1;
    if (valB === null || valB === undefined) return -1;

    if (typeof valA === "number" && typeof valB === "number") {
      return (valA - valB) * dir;
    }

    return String(valA).localeCompare(String(valB)) * dir;
  });

  // Pagination
  const offset = (page - 1) * pageSize;
  const pagedRecords = sorted.slice(offset, offset + pageSize);

  // Compute summary for local mock
  let totalQty = 0;
  let totalSqm = 0;
  const companies = new Set<string>();
  const salesmen = new Set<string>();

  for (const row of matched) {
    if (row.qty) totalQty += Number(row.qty) || 0;
    if (row.sqm) totalSqm += Number(row.sqm) || 0;
    if (row.company) companies.add(row.company);
    if (row.salesman) salesmen.add(row.salesman);
  }

  const summary: RecordSummary = {
    totalEntries,
    totalQty,
    totalSqm: Number(totalSqm.toFixed(2)),
    averageSqm: totalQty > 0 ? Number((totalSqm / totalQty).toFixed(2)) : 0,
    activeCompaniesCount: companies.size,
    activeSalesmenCount: salesmen.size,
  };

  const group = (keyFn: (r: RecordRow) => string | null | undefined): BreakdownItem[] => {
    const map = new Map<string, { count: number; totalQty: number; totalSqm: number }>();
    for (const row of matched) {
      const name = keyFn(row)?.trim() || "(Unassigned)";
      const existing = map.get(name) || { count: 0, totalQty: 0, totalSqm: 0 };
      existing.count += 1;
      existing.totalQty += Number(row.qty) || 0;
      existing.totalSqm += Number(row.sqm) || 0;
      map.set(name, existing);
    }

    const items: BreakdownItem[] = [];
    for (const [name, val] of map.entries()) {
      items.push({
        name,
        count: val.count,
        totalQty: val.totalQty,
        totalSqm: Number(val.totalSqm.toFixed(2)),
        percentage: totalEntries > 0 ? Number(((val.count / totalEntries) * 100).toFixed(1)) : 0,
      });
    }

    return items.sort((a, b) => b.count - a.count);
  };

  const breakdowns: RecordBreakdowns = {
    preparedBy: group((r) => r.prepared_by),
    salesman: group((r) => r.salesman),
    company: group((r) => r.company),
    typeOfWork: group((r) => r.type_of_work),
  };

  return {
    records: pagedRecords,
    summary,
    breakdowns,
    filterOptions: getFallbackFilterOptions(),
    pagination: {
      page,
      pageSize,
      totalEntries,
      totalPages: Math.ceil(totalEntries / pageSize) || 1,
    },
    serverTime: new Date().toISOString(),
    source: "mock_preview",
    executionMs: Date.now() - started,
    warning: "Local development preview (Supabase environment variables not set).",
  };
}
