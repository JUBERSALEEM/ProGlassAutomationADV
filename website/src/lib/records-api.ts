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
// DATA-DRIVEN FILTER OPTIONS EXTRACTION
// Extracts distinct, trimmed values strictly from actual record entries
// ─────────────────────────────────────────────────────────────────────────────

export function extractFilterOptionsFromRecords(records: RecordRow[]): FilterOptions {
  const extractDistinct = (getter: (r: RecordRow) => unknown): string[] => {
    const set = new Set<string>();
    for (const r of records) {
      const raw = getter(r);
      if (typeof raw === "string") {
        const trimmed = raw.trim();
        if (
          trimmed.length > 0 &&
          trimmed !== "(Unassigned)" &&
          trimmed !== "—" &&
          trimmed !== "-" &&
          trimmed.toLowerCase() !== "null" &&
          trimmed.toLowerCase() !== "undefined"
        ) {
          set.add(trimmed);
        }
      }
    }
    return Array.from(set).sort((a, b) => a.localeCompare(b));
  };

  const sourceFiles = extractDistinct((r) => r.source_file);
  const currentSourceFile = records.length > 0 ? (records[0]?.source_file || null) : null;

  return {
    companies: extractDistinct((r) => r.company),
    salesmen: extractDistinct((r) => r.salesman),
    preparedBy: extractDistinct((r) => r.prepared_by),
    statuses: extractDistinct((r) => r.status),
    piStatuses: extractDistinct((r) => r.pi_status),
    typesOfWork: extractDistinct((r) => r.type_of_work),
    zohoStatuses: extractDistinct((r) => r.zoho_status),
    sourceFiles,
    currentSourceFile,
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// CANONICAL PROGLASS ERP REFERENCE DATASET
// Used ONLY in local development when Supabase credentials are absent.
// Uses canonical ProGlass client & estimator references (Al Noor Glazing, Juber Saleem, Ahmed Rahman)
// ─────────────────────────────────────────────────────────────────────────────

const CANONICAL_COMPANIES = [
  "Al Noor Glazing Contracting LLC",
  "Al Noor Glass Factory LLC",
  "Crystal Clear Glass L.L.C.",
];

const CANONICAL_SALESMEN = ["Ahmed Rahman"];
const CANONICAL_PREPARED_BY = ["Juber Saleem"];
const CANONICAL_TYPES = ["DGU", "SGU", "Laminated", "Tempered"];
const CANONICAL_STATUSES = ["Confirmed", "In Production", "Completed", "Ready"];
const CANONICAL_PI_STATUSES = ["Approved", "Draft", "Sent"];
const CANONICAL_ZOHO_STATUSES = ["Synced", "Pending"];

function generateMockRecords(): RecordRow[] {
  const records: RecordRow[] = [];
  const baseDate = new Date("2026-10-02T12:00:00Z");

  for (let i = 1; i <= 60; i++) {
    const daysAgo = Math.floor((i * 90) / 60);
    const dateObj = new Date(baseDate.getTime() - daysAgo * 86400000);
    const dateStr = dateObj.toISOString().split("T")[0]!;

    const company = CANONICAL_COMPANIES[i % CANONICAL_COMPANIES.length] || "Al Noor Glazing Contracting LLC";
    const salesman = CANONICAL_SALESMEN[0] || "Ahmed Rahman";
    const prepared_by = CANONICAL_PREPARED_BY[0] || "Juber Saleem";
    const type_of_work = CANONICAL_TYPES[i % CANONICAL_TYPES.length] || "DGU";
    const status = CANONICAL_STATUSES[i % CANONICAL_STATUSES.length] || "Confirmed";
    const pi_status = CANONICAL_PI_STATUSES[i % CANONICAL_PI_STATUSES.length] || "Approved";
    const zoho_status = CANONICAL_ZOHO_STATUSES[i % CANONICAL_ZOHO_STATUSES.length] || "Synced";

    const piNum = `PI-2026-${String(1000 + i).padStart(4, "0")}`;
    const custRef = `PO-ALNOOR-${String(200 + (i % 50))}`;
    const qty = 4 + ((i * 17) % 80);
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

    // Determine current attached Excel workbook from Supabase public.records
    let currentSourceFile: string | null = null;
    let availableSourceFiles: string[] = [];
    let detectedLatestHasNoSourceFile = false;

    // Step 1 & 2: Multi-dimensional source resolution (ranking by newest effective activity)
    // Excel sync tools may insert rows (populating created_at, date, or id) without touching updated_at.
    // Query candidate records across RPC, created_at, date, updated_at, and sequential id to find the true latest workbook.
    type CandidateRecord = {
      source_file: string;
      created_at?: string | null;
      updated_at?: string | null;
      date?: string | null;
      id?: string | number | null;
      source_type: "rpc" | "latest_created" | "latest_date" | "latest_id" | "latest_updated";
    };

    const candidates: CandidateRecord[] = [];

    // Query candidate via dedicated RPC if installed
    try {
      const { data: rpcCurrentSource } = await client.rpc("get_current_excel_source");
      let sf: string | null = null;
      if (rpcCurrentSource && typeof rpcCurrentSource === "string" && rpcCurrentSource.trim().length > 0) {
        sf = rpcCurrentSource.trim();
      } else if (Array.isArray(rpcCurrentSource) && rpcCurrentSource.length > 0 && rpcCurrentSource[0]?.source_file) {
        sf = String(rpcCurrentSource[0].source_file).trim();
      }
      if (sf) {
        candidates.push({ source_file: sf, source_type: "rpc" });
      }
    } catch {}

    // Resilient candidate query helper: tries with is_active first, falls back if is_active column does not exist
    const fetchTopCandidate = async (
      orderColumn: string,
      sourceType: CandidateRecord["source_type"],
    ) => {
      try {
        let res = await client
          .from("records")
          .select("source_file, created_at, updated_at, date, id")
          .neq("is_active", false)
          .not("source_file", "is", null)
          .neq("source_file", "")
          .order(orderColumn, { ascending: false, nullsFirst: false })
          .limit(1);

        if (res.error) {
          // If is_active column doesn't exist (code 42703) or orderColumn fails, retry without is_active
          res = await client
            .from("records")
            .select("source_file, created_at, updated_at, date, id")
            .not("source_file", "is", null)
            .neq("source_file", "")
            .order(orderColumn, { ascending: false, nullsFirst: false })
            .limit(1);
        }

        if (res.data && res.data.length > 0 && res.data[0]?.source_file) {
          const row = res.data[0];
          candidates.push({
            source_file: String(row.source_file).trim(),
            created_at: row.created_at,
            updated_at: row.updated_at,
            date: row.date,
            id: row.id,
            source_type: sourceType,
          });
        }
      } catch {}
    };

    // Query candidates across creation timestamp, transaction date, row id, and update timestamp
    await fetchTopCandidate("created_at", "latest_created");
    await fetchTopCandidate("date", "latest_date");
    await fetchTopCandidate("id", "latest_id");
    await fetchTopCandidate("updated_at", "latest_updated");

    // Diagnostic check: inspect whether the latest rows in the database have source_file populated
    try {
      let latestRowsRes = await client
        .from("records")
        .select("source_file, created_at, date, id")
        .neq("is_active", false)
        .order("created_at", { ascending: false, nullsFirst: false })
        .limit(5);

      if (latestRowsRes.error || !latestRowsRes.data || latestRowsRes.data.length === 0) {
        latestRowsRes = await client
          .from("records")
          .select("source_file, created_at, date, id")
          .order("date", { ascending: false, nullsFirst: false })
          .limit(5);
      }

      if (latestRowsRes.data && latestRowsRes.data.length > 0) {
        const hasAnySource = latestRowsRes.data.some((r) => {
          const raw = r as Record<string, unknown>;
          const sf =
            r.source_file ||
            (typeof raw.file_name === "string" ? raw.file_name : null) ||
            (typeof raw.filename === "string" ? raw.filename : null);
          return sf && typeof sf === "string" && sf.trim().length > 0;
        });
        if (!hasAnySource) {
          detectedLatestHasNoSourceFile = true;
        }
      }
    } catch {}

    // Rank candidates to determine the true active workbook:
    // If the newest rows in the database have no source_file, currentSourceFile stays null to avoid locking onto an ancient file
    if (!detectedLatestHasNoSourceFile && candidates.length > 0) {
      const latestCreated = candidates.find((c) => c.source_type === "latest_created");
      const latestDate = candidates.find((c) => c.source_type === "latest_date");
      const latestId = candidates.find((c) => c.source_type === "latest_id");
      const latestUpdated = candidates.find((c) => c.source_type === "latest_updated");
      const rpcCandidate = candidates.find((c) => c.source_type === "rpc");

      const tCreated = latestCreated?.created_at ? new Date(latestCreated.created_at).getTime() : -1;
      const tUpdated = latestUpdated?.updated_at ? new Date(latestUpdated.updated_at).getTime() : -1;
      const dLatest = latestDate?.date ? new Date(latestDate.date).getTime() : -1;

      // Evaluation precedence:
      // 1. If created_at is valid on latest_created, the newest inserted rows represent the current file
      if (tCreated > 0 && latestCreated?.source_file) {
        currentSourceFile = latestCreated.source_file;
      }
      // 2. If business transaction date is newer on latest_date, that workbook has the newest operations
      else if (dLatest > 0 && latestDate?.source_file) {
        currentSourceFile = latestDate.source_file;
      }
      // 3. If explicit updated_at exists and is newer than created_at
      else if (tUpdated > tCreated && latestUpdated?.source_file) {
        currentSourceFile = latestUpdated.source_file;
      }
      // 4. Fall back to highest row id
      else if (latestId?.source_file) {
        currentSourceFile = latestId.source_file;
      }
      // 5. Fall back to RPC candidate
      else if (rpcCandidate?.source_file) {
        currentSourceFile = rpcCandidate.source_file;
      } else {
        currentSourceFile = candidates[0]?.source_file ?? null;
      }
    }

    // Step 3: Retrieve distinct active source files for filter selection from multiple angles
    const sfSet = new Set<string>();

    const collectSourceFiles = async (orderCol?: string) => {
      try {
        let q = client
          .from("records")
          .select("source_file")
          .neq("is_active", false)
          .not("source_file", "is", null)
          .neq("source_file", "");
        if (orderCol) {
          q = q.order(orderCol, { ascending: false, nullsFirst: false });
        }
        let res = await q.limit(200);
        if (res.error) {
          let fallbackQ = client
            .from("records")
            .select("source_file")
            .not("source_file", "is", null)
            .neq("source_file", "");
          if (orderCol) {
            fallbackQ = fallbackQ.order(orderCol, { ascending: false, nullsFirst: false });
          }
          res = await fallbackQ.limit(200);
        }
        if (res.data) {
          for (const row of res.data) {
            if (row.source_file && typeof row.source_file === "string") {
              const t = row.source_file.trim();
              if (t) sfSet.add(t);
            }
          }
        }
      } catch {}
    };

    await collectSourceFiles("created_at");
    await collectSourceFiles("date");
    await collectSourceFiles();

    if (currentSourceFile) {
      sfSet.add(currentSourceFile);
    }
    availableSourceFiles = Array.from(sfSet).sort((a, b) => a.localeCompare(b));

    // Determine effective source file scoping:
    // - "all": operator explicitly requests viewing all historical Excel files combined
    // - specific filename: operator selected a specific attached workbook
    // - empty or "current" (default): strictly scope to current attached workbook to prevent reading old files
    // If the latest rows in the database have no source_file assigned, do not force scoping to an ancient file
    const effectiveSourceFile =
      filters.sourceFile === "all"
        ? null
        : filters.sourceFile && filters.sourceFile !== "current"
          ? filters.sourceFile
          : detectedLatestHasNoSourceFile
            ? null
            : currentSourceFile;

    // Helper to safely format PostgREST .or() filter with double-quoted values
    const buildSourceFileFilter = (sourceFile: string) => {
      const cleanName = sourceFile.split(/[\\/]/).pop()?.trim() || sourceFile;
      const qFull = `"${sourceFile.replace(/"/g, '""')}"`;
      const qClean = `"%${cleanName.replace(/"/g, '""')}%"`;
      return `source_file.eq.${qFull},source_file.ilike.${qClean}`;
    };

    // 1. Fetch paginated records slice (50, 100, or 250 rows only)
    // Filter by is_active is not false and scope to current attached Excel file
    let query = client.from("records").select("*", { count: "exact" }).neq("is_active", false);

    if (effectiveSourceFile) {
      query = query.or(buildSourceFileFilter(effectiveSourceFile));
    }

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
      if (effectiveSourceFile) {
        fallbackQuery = fallbackQuery.or(buildSourceFileFilter(effectiveSourceFile));
      }
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
      const rpcParams: Record<string, unknown> = {
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
        p_source_file: effectiveSourceFile || null,
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
      let filterOptions: FilterOptions = extractFilterOptionsFromRecords((recordsData || []) as RecordRow[]);
      let rpcStatus: "success" | "error" | "not_installed" = "not_installed";
      let capturedRpcError: { message: string; code?: string; details?: string; hint?: string } | null = null;

      try {
        let { data: rpcData, error: rpcError } = await client.rpc(
          "get_records_dashboard_aggregates",
          rpcParams,
        );

        // Resilient fallback if Supabase RPC function is still the 10-param signature without p_source_file
        if (
          rpcError &&
          (rpcError.code === "PGRST202" ||
            rpcError.message?.toLowerCase().includes("argument") ||
            rpcError.message?.toLowerCase().includes("parameter") ||
            rpcError.message?.toLowerCase().includes("function"))
        ) {
          const legacyParams = { ...rpcParams };
          delete legacyParams.p_source_file;
          const legacyRes = await client.rpc("get_records_dashboard_aggregates", legacyParams);
          if (!legacyRes.error) {
            rpcData = legacyRes.data;
            rpcError = null;
          }
        }

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
          filterOptions =
            res.filterOptions && typeof res.filterOptions === "object"
              ? {
                  companies: Array.isArray(res.filterOptions.companies)
                    ? res.filterOptions.companies.filter((x: unknown) => typeof x === "string" && x.trim().length > 0)
                    : [],
                  salesmen: Array.isArray(res.filterOptions.salesmen)
                    ? res.filterOptions.salesmen.filter((x: unknown) => typeof x === "string" && x.trim().length > 0)
                    : [],
                  preparedBy: Array.isArray(res.filterOptions.preparedBy)
                    ? res.filterOptions.preparedBy.filter((x: unknown) => typeof x === "string" && x.trim().length > 0)
                    : [],
                  statuses: Array.isArray(res.filterOptions.statuses)
                    ? res.filterOptions.statuses.filter((x: unknown) => typeof x === "string" && x.trim().length > 0)
                    : [],
                  piStatuses: Array.isArray(res.filterOptions.piStatuses)
                    ? res.filterOptions.piStatuses.filter((x: unknown) => typeof x === "string" && x.trim().length > 0)
                    : [],
                  typesOfWork: Array.isArray(res.filterOptions.typesOfWork)
                    ? res.filterOptions.typesOfWork.filter((x: unknown) => typeof x === "string" && x.trim().length > 0)
                    : [],
                  zohoStatuses: Array.isArray(res.filterOptions.zohoStatuses)
                    ? res.filterOptions.zohoStatuses.filter((x: unknown) => typeof x === "string" && x.trim().length > 0)
                    : [],
                  sourceFiles: Array.isArray(res.filterOptions.sourceFiles)
                    ? res.filterOptions.sourceFiles.filter((x: unknown) => typeof x === "string" && x.trim().length > 0)
                    : availableSourceFiles,
                  currentSourceFile:
                    typeof res.filterOptions.currentSourceFile === "string"
                      ? res.filterOptions.currentSourceFile
                      : currentSourceFile,
                }
              : extractFilterOptionsFromRecords((recordsData || []) as RecordRow[]);

          if (!currentSourceFile && typeof res.filterOptions?.currentSourceFile === "string" && res.filterOptions.currentSourceFile.trim()) {
            currentSourceFile = res.filterOptions.currentSourceFile.trim();
          }
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
            activeCompaniesCount: new Set((recordsData || []).map((r) => r.company).filter(Boolean)).size,
            activeSalesmenCount: new Set((recordsData || []).map((r) => r.salesman).filter(Boolean)).size,
          };
          if (summary.totalQty > 0) {
            summary.averageSqm = Number((summary.totalSqm / summary.totalQty).toFixed(2));
          }

          const groupSlice = (keyFn: (r: RecordRow) => string | null | undefined): BreakdownItem[] => {
            const map = new Map<string, { count: number; totalQty: number; totalSqm: number }>();
            for (const row of (recordsData || [])) {
              const name = keyFn(row as RecordRow)?.trim() || "(Unassigned)";
              const existing = map.get(name) || { count: 0, totalQty: 0, totalSqm: 0 };
              existing.count += 1;
              existing.totalQty += Number(row.qty) || 0;
              existing.totalSqm += Number(row.sqm) || 0;
              map.set(name, existing);
            }

            const items: BreakdownItem[] = [];
            const sliceTotal = (recordsData || []).length;
            for (const [name, val] of map.entries()) {
              items.push({
                name,
                count: val.count,
                totalQty: val.totalQty,
                totalSqm: Number(val.totalSqm.toFixed(2)),
                percentage: sliceTotal > 0 ? Number(((val.count / sliceTotal) * 100).toFixed(1)) : 0,
              });
            }

            return items.sort((a, b) => b.count - a.count);
          };

          breakdowns = {
            preparedBy: groupSlice((r) => r.prepared_by),
            salesman: groupSlice((r) => r.salesman),
            company: groupSlice((r) => r.company),
            typeOfWork: groupSlice((r) => r.type_of_work),
          };
          // Data-driven extraction directly from actual record entries
          filterOptions = {
            ...extractFilterOptionsFromRecords((recordsData || []) as RecordRow[]),
            sourceFiles: availableSourceFiles,
            currentSourceFile,
          };

          // If the page slice has fewer rows than the total dataset, fetch distinct values from records
          // STRICTLY SCOPED to active records within the effective source file
          if (totalEntries > (recordsData?.length || 0)) {
            try {
              let colQuery = client
                .from("records")
                .select("company, salesman, prepared_by, status, pi_status, type_of_work, zoho_status")
                .neq("is_active", false);

              if (effectiveSourceFile) {
                colQuery = colQuery.or(buildSourceFileFilter(effectiveSourceFile));
              }

              let colRows: typeof recordsData = null;
              const { data: initialColRows, error: colError } = await colQuery.limit(1000);
              colRows = initialColRows;

              // If is_active column doesn't exist yet, retry without is_active but STILL scoped to source_file
              if (colError) {
                let fallbackCol = client
                  .from("records")
                  .select("company, salesman, prepared_by, status, pi_status, type_of_work, zoho_status");
                if (effectiveSourceFile) {
                  fallbackCol = fallbackCol.or(buildSourceFileFilter(effectiveSourceFile));
                }
                const res = await fallbackCol.limit(1000);
                colRows = res.data;
              }

              if (colRows && colRows.length > 0) {
                const fullOptions = extractFilterOptionsFromRecords(colRows as RecordRow[]);
                filterOptions = {
                  ...fullOptions,
                  sourceFiles: availableSourceFiles,
                  currentSourceFile,
                };
              }
            } catch {
              // Retain preliminary slice filter options
            }
          }
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
        // Data-driven extraction directly from actual record entries
        filterOptions = extractFilterOptionsFromRecords((recordsData || []) as RecordRow[]);
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
      } else if (detectedLatestHasNoSourceFile && !warning) {
        warning =
          "Notice: The most recently synced rows in Supabase public.records do not have 'source_file' populated. Showing all recent entries. To enable isolated workbook switching, configure ProGlassExcelSync to populate 'source_file' with the Excel workbook filename.";
      }

      return {
        records: ((recordsData as RecordRow[]) || []).map((r) => {
          const raw = r as Record<string, unknown>;
          const resolvedSf =
            r.source_file ||
            (typeof raw.file_name === "string" ? raw.file_name : null) ||
            (typeof raw.filename === "string" ? raw.filename : null) ||
            (typeof raw.excel_file === "string" ? raw.excel_file : null);
          return {
            ...r,
            source_file: resolvedSf,
          };
        }),
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
        currentSourceFile,
        sourceFiles: filterOptions.sourceFiles || availableSourceFiles,
        isCurrentSourceScoped: Boolean(effectiveSourceFile),
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

  if (filters.sourceFile && filters.sourceFile !== "all" && filters.sourceFile !== "current") {
    matched = matched.filter((r) => r.source_file === filters.sourceFile);
  }

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
    filterOptions: {
      ...extractFilterOptionsFromRecords(matched),
      sourceFiles: ["Daily_Orders_Master_2026.xlsx"],
      currentSourceFile: "Daily_Orders_Master_2026.xlsx",
    },
    pagination: {
      page,
      pageSize,
      totalEntries,
      totalPages: Math.ceil(totalEntries / pageSize) || 1,
    },
    serverTime: new Date().toISOString(),
    source: "mock_preview",
    executionMs: Date.now() - started,
    currentSourceFile: "Daily_Orders_Master_2026.xlsx",
    sourceFiles: ["Daily_Orders_Master_2026.xlsx"],
    isCurrentSourceScoped: true,
    warning: "Local development preview (Supabase environment variables not set).",
  };
}
