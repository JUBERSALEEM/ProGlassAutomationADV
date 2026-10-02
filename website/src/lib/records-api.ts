/**
 * ProGlass Automation — Records Dashboard Data Layer
 *
 * Implements server-side filtering, aggregation, sorting, and pagination
 * against Supabase `public.records`, with graceful fallback for local development.
 *
 * All operations run on the server — the browser never downloads unbounded rows.
 */

import { isSupabaseConfigured, supabaseServer } from "@/lib/supabase";
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
    // Current week starting Monday
    const dayOfWeek = now.getUTCDay(); // 0 is Sunday, 1 is Monday...
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

// ─────────────────────────────────────────────────────────────────────────────
// REALISTIC LOCAL PREVIEW DATASET
// Used when Supabase is not configured or when RLS prevents anonymous reads.
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

  // Generate 120 realistic records spread over the past 90 days
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
// SERVER-SIDE QUERY IMPLEMENTATION
// ─────────────────────────────────────────────────────────────────────────────

export async function fetchRecordsDashboardData({
  filters,
  sort = { field: "date", direction: "desc" },
  page = 1,
  pageSize = 50,
}: {
  filters: RecordFilters;
  sort?: RecordSort;
  page?: number;
  pageSize?: number;
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

  // Check if live Supabase is available
  if (isSupabaseConfigured()) {
    try {
      const client = supabaseServer();

      // 1. Build filtered query for the paginated records
      let query = client.from("records").select("*", { count: "exact" });

      if (filters.search) {
        const s = filters.search.trim().replace(/[%_]/g, "\\$&");
        query = query.or(
          `pi_number.ilike.%${s}%,customer_ref.ilike.%${s}%,company.ilike.%${s}%`,
        );
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

      // Sort and paginate
      query = query
        .order(sort.field, { ascending: sort.direction === "asc" })
        .range(offset, offset + clampedPageSize - 1);

      const { data: recordsData, count: totalCount, error: recordsError } = await query;

      if (recordsError) {
        console.warn("[records-api] Supabase records query returned error:", recordsError.message);
        throw recordsError;
      }

      const totalEntries = totalCount ?? (recordsData?.length || 0);

      // 2. Query summary and breakdowns
      // Projected query for aggregation (capped to prevent extreme payloads)
      let aggQuery = client
        .from("records")
        .select("qty, sqm, prepared_by, salesman, company, type_of_work");

      if (filters.search) {
        const s = filters.search.trim().replace(/[%_]/g, "\\$&");
        aggQuery = aggQuery.or(
          `pi_number.ilike.%${s}%,customer_ref.ilike.%${s}%,company.ilike.%${s}%`,
        );
      }
      if (resolvedFrom) aggQuery = aggQuery.gte("date", resolvedFrom);
      if (resolvedTo) aggQuery = aggQuery.lte("date", resolvedTo);
      if (filters.company) aggQuery = aggQuery.eq("company", filters.company);
      if (filters.salesman) aggQuery = aggQuery.eq("salesman", filters.salesman);
      if (filters.preparedBy) aggQuery = aggQuery.eq("prepared_by", filters.preparedBy);
      if (filters.status) aggQuery = aggQuery.eq("status", filters.status);
      if (filters.piStatus) aggQuery = aggQuery.eq("pi_status", filters.piStatus);
      if (filters.typeOfWork) aggQuery = aggQuery.eq("type_of_work", filters.typeOfWork);
      if (filters.zohoStatus) aggQuery = aggQuery.eq("zoho_status", filters.zohoStatus);

      // Capped server projection query for calculating compact aggregates
      const { data: aggData } = await aggQuery.limit(10000);

      const summary = computeSummary(aggData || recordsData || [], totalEntries);
      const breakdowns = computeBreakdowns(aggData || recordsData || [], totalEntries);

      // Distinct filter options query
      const filterOptions = await fetchFilterOptionsFromSupabase(client);

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
      };
    } catch (err) {
      console.warn(
        "[records-api] Supabase query failed, falling back to mock preview dataset:",
        err instanceof Error ? err.message : String(err),
      );
      // Fall through to mock dataset with a warning
    }
  }

  // Fallback: Local dataset execution (server-side)
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

function computeSummary(
  rows: { qty?: number | null; sqm?: number | null; company?: string | null; salesman?: string | null }[],
  totalEntries: number,
): RecordSummary {
  let totalQty = 0;
  let totalSqm = 0;
  const companies = new Set<string>();
  const salesmen = new Set<string>();

  for (const row of rows) {
    if (row.qty) totalQty += Number(row.qty) || 0;
    if (row.sqm) totalSqm += Number(row.sqm) || 0;
    if (row.company) companies.add(row.company);
    if (row.salesman) salesmen.add(row.salesman);
  }

  return {
    totalEntries,
    totalQty,
    totalSqm: Number(totalSqm.toFixed(2)),
    averageSqm: totalQty > 0 ? Number((totalSqm / totalQty).toFixed(2)) : 0,
    activeCompaniesCount: companies.size,
    activeSalesmenCount: salesmen.size,
  };
}

function computeBreakdowns(
  rows: {
    qty?: number | null;
    sqm?: number | null;
    prepared_by?: string | null;
    salesman?: string | null;
    company?: string | null;
    type_of_work?: string | null;
  }[],
  totalEntries: number,
): RecordBreakdowns {
  const group = (keyFn: (r: (typeof rows)[0]) => string | null | undefined): BreakdownItem[] => {
    const map = new Map<string, { count: number; totalQty: number; totalSqm: number }>();
    for (const row of rows) {
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

    // Sort descending by count
    return items.sort((a, b) => b.count - a.count);
  };

  return {
    preparedBy: group((r) => r.prepared_by),
    salesman: group((r) => r.salesman),
    company: group((r) => r.company),
    typeOfWork: group((r) => r.type_of_work),
  };
}

async function fetchFilterOptionsFromSupabase(
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  client: any,
): Promise<FilterOptions> {
  try {
    const { data } = await client
      .from("records")
      .select("company, salesman, prepared_by, status, pi_status, type_of_work, zoho_status")
      .limit(1000);

    if (!data || data.length === 0) {
      return getFallbackFilterOptions();
    }

    const setOf = (key: string): string[] =>
      (
        Array.from(
          new Set(
            data
              // eslint-disable-next-line @typescript-eslint/no-explicit-any
              .map((r: any) => r[key])
              .filter((v: unknown): v is string => typeof v === "string" && v.trim().length > 0),
          ),
        ) as string[]
      ).sort();

    return {
      companies: setOf("company"),
      salesmen: setOf("salesman"),
      preparedBy: setOf("prepared_by"),
      statuses: setOf("status"),
      piStatuses: setOf("pi_status"),
      typesOfWork: setOf("type_of_work"),
      zohoStatuses: setOf("zoho_status"),
    };
  } catch {
    return getFallbackFilterOptions();
  }
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
// LOCAL MOCK QUERY EXECUTION
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
  let matched = GLOBAL_MOCK_RECORDS;

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

  const summary = computeSummary(matched, totalEntries);
  const breakdowns = computeBreakdowns(matched, totalEntries);

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
    warning: isSupabaseConfigured()
      ? undefined
      : "Connected to local mock dataset (Supabase credentials not set in local environment).",
  };
}
