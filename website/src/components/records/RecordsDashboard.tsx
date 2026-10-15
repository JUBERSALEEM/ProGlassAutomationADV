"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";
import type {
  DatePreset,
  FilterOptions,
  RecordFilters,
  RecordRow,
  RecordSort,
  RecordSortField,
  RecordsApiResponse,
  RecordsApiSuccessResponse,
} from "@/types/records";
import { RecordsHeader } from "./RecordsHeader";
import { RecordsSummaryCards } from "./RecordsSummaryCards";
import { RecordsFilterPanel } from "./RecordsFilterPanel";
import { RecordsBreakdowns } from "./RecordsBreakdowns";
import { RecordsTable } from "./RecordsTable";
import { RecordsPagination } from "./RecordsPagination";
import { RecordsRecordDetailModal } from "./RecordsRecordDetailModal";
import { ExportActions } from "@/components/operations/ExportActions";
import { compareRecordDates } from "@/lib/date-utils";
import { useOperatorSession } from "@/lib/useOperatorSession";
import { jobOrdersHrefFromRecordFilters } from "@/lib/job-order-links";

const INITIAL_FILTERS: RecordFilters = {
  search: "",
  datePreset: "all",
  dateFrom: "",
  dateTo: "",
  company: "",
  salesman: "",
  preparedBy: "",
  status: "",
  piStatus: "",
  typeOfWork: "",
  zohoStatus: "",
  sourceFile: "",
};

const INITIAL_SORT: RecordSort = {
  field: "date",
  direction: "asc",
};

export function RecordsDashboard() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  // Initialize filters from URL params if present
  const [filters, setFilters] = useState<RecordFilters>(() => ({
    search: searchParams.get("search") || "",
    datePreset: (searchParams.get("datePreset") || "all") as DatePreset,
    dateFrom: searchParams.get("dateFrom") || "",
    dateTo: searchParams.get("dateTo") || "",
    company: searchParams.get("company") || "",
    salesman: searchParams.get("salesman") || "",
    preparedBy: searchParams.get("preparedBy") || "",
    status: searchParams.get("status") || "",
    piStatus: searchParams.get("piStatus") || "",
    typeOfWork: searchParams.get("typeOfWork") || "",
    zohoStatus: searchParams.get("zohoStatus") || "",
    sourceFile: searchParams.get("sourceFile") || "",
  }));

  const [sort, setSort] = useState<RecordSort>(() => ({
    field: (searchParams.get("sortBy") || "date") as RecordSortField,
    direction: searchParams.get("sortOrder") === "desc" ? "desc" : "asc",
  }));

  const [page, setPage] = useState<number>(() => {
    const p = parseInt(searchParams.get("page") || "1", 10);
    return isNaN(p) || p < 1 ? 1 : p;
  });

  const [pageSize, setPageSize] = useState<number>(() => {
    const s = parseInt(searchParams.get("pageSize") || "50", 10);
    return isNaN(s) || s < 10 ? 50 : s;
  });

  const [density, setDensity] = useState<"comfortable" | "compact">("comfortable");
  const [data, setData] = useState<RecordsApiSuccessResponse["data"] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedRecord, setSelectedRecord] = useState<RecordRow | null>(null);

  // Keep an accumulated set of distinct values observed strictly from loaded record entries
  const [accumulatedOptions, setAccumulatedOptions] = useState<FilterOptions>({
    companies: [],
    salesmen: [],
    preparedBy: [],
    statuses: [],
    piStatuses: [],
    typesOfWork: [],
    zohoStatuses: [],
    sourceFiles: [],
  });

  const abortControllerRef = useRef<AbortController | null>(null);

  // One operator session, shared with the Deliveries Tracker. The hook owns
  // the check, the token and the sign-out; this page only renders the gate.
  const {
    checking: authChecking,
    userEmail,
    authToken,
    isAuthRequired,
    signOut: handleSignOut,
    requireAuth,
  } = useOperatorSession();

  // Sync state to URL search parameters
  const updateUrlParams = useCallback(
    (newFilters: RecordFilters, newSort: RecordSort, newPage: number, newPageSize: number) => {
      const params = new URLSearchParams();

      if (newFilters.search) params.set("search", newFilters.search);
      if (newFilters.datePreset !== "all") params.set("datePreset", newFilters.datePreset);
      if (newFilters.dateFrom) params.set("dateFrom", newFilters.dateFrom);
      if (newFilters.dateTo) params.set("dateTo", newFilters.dateTo);
      if (newFilters.company) params.set("company", newFilters.company);
      if (newFilters.salesman) params.set("salesman", newFilters.salesman);
      if (newFilters.preparedBy) params.set("preparedBy", newFilters.preparedBy);
      if (newFilters.status) params.set("status", newFilters.status);
      if (newFilters.piStatus) params.set("piStatus", newFilters.piStatus);
      if (newFilters.typeOfWork) params.set("typeOfWork", newFilters.typeOfWork);
      if (newFilters.zohoStatus) params.set("zohoStatus", newFilters.zohoStatus);
      if (newFilters.sourceFile) params.set("sourceFile", newFilters.sourceFile);

      if (newSort.field !== "date") params.set("sortBy", newSort.field);
      if (newSort.direction !== "asc") params.set("sortOrder", newSort.direction);

      if (newPage > 1) params.set("page", String(newPage));
      if (newPageSize !== 50) params.set("pageSize", String(newPageSize));

      const queryStr = params.toString();
      const targetUrl = queryStr ? `${pathname}?${queryStr}` : pathname;
      router.replace(targetUrl, { scroll: false });
    },
    [pathname, router],
  );

  const [refreshCounter, setRefreshCounter] = useState(0);

  // Fetch data from API whenever filters, pagination, sort, or refresh counter changes
  useEffect(() => {
    let isCancelled = false;
    const controller = new AbortController();
    abortControllerRef.current = controller;

    async function load() {
      try {
        const params = new URLSearchParams({
          page: String(page),
          pageSize: String(pageSize),
          sortBy: sort.field,
          sortOrder: sort.direction,
          datePreset: filters.datePreset,
        });

        if (filters.search) params.set("search", filters.search);
        if (filters.dateFrom) params.set("dateFrom", filters.dateFrom);
        if (filters.dateTo) params.set("dateTo", filters.dateTo);
        if (filters.company) params.set("company", filters.company);
        if (filters.salesman) params.set("salesman", filters.salesman);
        if (filters.preparedBy) params.set("preparedBy", filters.preparedBy);
        if (filters.status) params.set("status", filters.status);
        if (filters.piStatus) params.set("piStatus", filters.piStatus);
        if (filters.typeOfWork) params.set("typeOfWork", filters.typeOfWork);
        if (filters.zohoStatus) params.set("zohoStatus", filters.zohoStatus);
        if (filters.sourceFile) params.set("sourceFile", filters.sourceFile);

        const headers: Record<string, string> = {};
        if (authToken) {
          headers["Authorization"] = `Bearer ${authToken}`;
        }

        const response = await fetch(`/api/records?${params.toString()}`, {
          signal: controller.signal,
          headers,
          cache: "no-store",
        });

        if (response.status === 401) {
          requireAuth();
          setLoading(false);
          return;
        }

        const json: RecordsApiResponse = await response.json();

        if (!isCancelled) {
          if (!response.ok || !json.ok) {
            throw new Error(json.ok ? "Failed to load records" : json.error.message);
          }
          const loadedRecords = json.data?.records || [];
          const apiOptions = json.data?.filterOptions;
          const currentFile = json.data?.currentSourceFile || apiOptions?.currentSourceFile || null;

          const sanitizeList = (list: string[] | undefined): string[] => {
            if (!list || !Array.isArray(list)) return [];
            return list
              .filter(
                (x): x is string =>
                  typeof x === "string" &&
                  x.trim().length > 0 &&
                  x !== "(Unassigned)" &&
                  x !== "—" &&
                  x !== "-" &&
                  x.toLowerCase() !== "null" &&
                  x.toLowerCase() !== "undefined",
              )
              .map((x) => x.trim())
              .sort((a, b) => a.localeCompare(b));
          };

          const serverHasData =
            apiOptions &&
            ((apiOptions.companies?.length || 0) > 0 ||
              (apiOptions.salesmen?.length || 0) > 0 ||
              (apiOptions.preparedBy?.length || 0) > 0);

          if (serverHasData && apiOptions) {
            setAccumulatedOptions({
              companies: sanitizeList(apiOptions.companies),
              salesmen: sanitizeList(apiOptions.salesmen),
              preparedBy: sanitizeList(apiOptions.preparedBy),
              statuses: sanitizeList(apiOptions.statuses),
              piStatuses: sanitizeList(apiOptions.piStatuses),
              typesOfWork: sanitizeList(apiOptions.typesOfWork),
              zohoStatuses: sanitizeList(apiOptions.zohoStatuses),
              sourceFiles: apiOptions.sourceFiles || [],
              currentSourceFile: currentFile,
            });
          } else if (loadedRecords.length > 0) {
            setAccumulatedOptions((prev) => {
              const isDifferentFile =
                Boolean(currentFile && prev.currentSourceFile && currentFile !== prev.currentSourceFile);
              const baseCompanies = isDifferentFile ? [] : prev.companies;
              const baseSalesmen = isDifferentFile ? [] : prev.salesmen;
              const basePreparedBy = isDifferentFile ? [] : prev.preparedBy;
              const baseStatuses = isDifferentFile ? [] : prev.statuses;
              const basePiStatuses = isDifferentFile ? [] : prev.piStatuses;
              const baseTypesOfWork = isDifferentFile ? [] : prev.typesOfWork;
              const baseZohoStatuses = isDifferentFile ? [] : prev.zohoStatuses;

              const mergeDistinct = (existing: string[], getter: (r: RecordRow) => unknown): string[] => {
                const set = new Set<string>(existing);
                for (const r of loadedRecords) {
                  const val = getter(r);
                  if (typeof val === "string") {
                    const trimmed = val.trim();
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

              return {
                companies: mergeDistinct(baseCompanies, (r) => r.company),
                salesmen: mergeDistinct(baseSalesmen, (r) => r.salesman),
                preparedBy: mergeDistinct(basePreparedBy, (r) => r.prepared_by),
                statuses: mergeDistinct(baseStatuses, (r) => r.status),
                piStatuses: mergeDistinct(basePiStatuses, (r) => r.pi_status),
                typesOfWork: mergeDistinct(baseTypesOfWork, (r) => r.type_of_work),
                zohoStatuses: mergeDistinct(baseZohoStatuses, (r) => r.zoho_status),
                sourceFiles: Array.from(
                  new Set([
                    ...(isDifferentFile ? [] : prev.sourceFiles || []),
                    ...(apiOptions?.sourceFiles || []),
                  ]),
                ).sort((a, b) => a.localeCompare(b)),
                currentSourceFile: currentFile ?? prev.currentSourceFile,
              };
            });
          }
          setData(json.data);
          setError(null);
          setLoading(false);
        }
      } catch (err) {
        if (!isCancelled && !(err instanceof Error && err.name === "AbortError")) {
          console.error("[records-dashboard] Data fetch failed:", err);
          setError(err instanceof Error ? err.message : "Unable to load records. Please try again.");
          setLoading(false);
        }
      }
    }

    if (authChecking || (isAuthRequired && !authToken)) {
      return;
    }

    load();

    return () => {
      isCancelled = true;
      controller.abort();
    };
  }, [
    page,
    pageSize,
    sort,
    filters,
    refreshCounter,
    authToken,
    isAuthRequired,
    authChecking,
    requireAuth,
  ]);

  // Handlers
  const handleRefresh = useCallback(() => {
    setAccumulatedOptions({
      companies: [],
      salesmen: [],
      preparedBy: [],
      statuses: [],
      piStatuses: [],
      typesOfWork: [],
      zohoStatuses: [],
      sourceFiles: [],
      currentSourceFile: null,
    });
    setLoading(true);
    setRefreshCounter((c) => c + 1);
  }, []);

  const handleFiltersChange = (updated: Partial<RecordFilters>) => {
    if (updated.sourceFile !== undefined && updated.sourceFile !== filters.sourceFile) {
      setAccumulatedOptions({
        companies: [],
        salesmen: [],
        preparedBy: [],
        statuses: [],
        piStatuses: [],
        typesOfWork: [],
        zohoStatuses: [],
        sourceFiles: [],
        currentSourceFile: null,
      });
      const nextFilters: RecordFilters = {
        ...INITIAL_FILTERS,
        search: filters.search,
        sourceFile: updated.sourceFile,
      };
      setLoading(true);
      setFilters(nextFilters);
      setPage(1);
      updateUrlParams(nextFilters, sort, 1, pageSize);
      return;
    }
    const nextFilters = { ...filters, ...updated };
    setLoading(true);
    setFilters(nextFilters);
    setPage(1); // Reset to page 1 on filter change
    updateUrlParams(nextFilters, sort, 1, pageSize);
  };

  const handleResetFilters = () => {
    setAccumulatedOptions({
      companies: [],
      salesmen: [],
      preparedBy: [],
      statuses: [],
      piStatuses: [],
      typesOfWork: [],
      zohoStatuses: [],
      sourceFiles: [],
      currentSourceFile: null,
    });
    setLoading(true);
    setFilters(INITIAL_FILTERS);
    setSort(INITIAL_SORT);
    setPage(1);
    updateUrlParams(INITIAL_FILTERS, INITIAL_SORT, 1, pageSize);
  };

  const handleSortChange = (field: RecordSortField) => {
    const nextSort: RecordSort = {
      field,
      direction: sort.field === field && sort.direction === "asc" ? "desc" : "asc",
    };
    setLoading(true);
    setSort(nextSort);
    updateUrlParams(filters, nextSort, page, pageSize);
  };

  const handlePageChange = (newPage: number) => {
    setLoading(true);
    setPage(newPage);
    updateUrlParams(filters, sort, newPage, pageSize);
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const handlePageSizeChange = (newSize: number) => {
    setLoading(true);
    setPageSize(newSize);
    setPage(1);
    updateUrlParams(filters, sort, 1, newSize);
  };

  // Dynamically compute data-driven filter options strictly from actual entries
  const dataDrivenFilterOptions = useMemo<FilterOptions>(() => {
    const currentRecords = data?.records || [];

    const extractStrictlyFromRecords = (
      accumulatedList: string[],
      getter: (r: RecordRow) => unknown,
    ): string[] => {
      const set = new Set<string>(accumulatedList);
      for (const r of currentRecords) {
        const val = getter(r);
        if (typeof val === "string") {
          const trimmed = val.trim();
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

    // If records are present or accumulated, use STRICTLY the record entry column values
    const optionsFromEntries = {
      companies: extractStrictlyFromRecords(accumulatedOptions.companies, (r) => r.company),
      salesmen: extractStrictlyFromRecords(accumulatedOptions.salesmen, (r) => r.salesman),
      preparedBy: extractStrictlyFromRecords(accumulatedOptions.preparedBy, (r) => r.prepared_by),
      statuses: extractStrictlyFromRecords(accumulatedOptions.statuses, (r) => r.status),
      piStatuses: extractStrictlyFromRecords(accumulatedOptions.piStatuses, (r) => r.pi_status),
      typesOfWork: extractStrictlyFromRecords(accumulatedOptions.typesOfWork, (r) => r.type_of_work),
      zohoStatuses: extractStrictlyFromRecords(accumulatedOptions.zohoStatuses, (r) => r.zoho_status),
      sourceFiles: data?.filterOptions?.sourceFiles || accumulatedOptions.sourceFiles || [],
      currentSourceFile: data?.currentSourceFile || data?.filterOptions?.currentSourceFile || null,
    };

    // If entries have yielded any values, return them strictly
    const hasAnyRecordValues =
      optionsFromEntries.companies.length > 0 ||
      optionsFromEntries.salesmen.length > 0 ||
      optionsFromEntries.preparedBy.length > 0;

    if (hasAnyRecordValues) {
      return optionsFromEntries;
    }

    // Otherwise (initial loading before rows arrive), sanitize backend options
    const backendOptions = data?.filterOptions;
    const sanitizeList = (list: string[] | undefined): string[] => {
      if (!list || !Array.isArray(list)) return [];
      return list
        .filter(
          (x): x is string =>
            typeof x === "string" &&
            x.trim().length > 0 &&
            x !== "(Unassigned)" &&
            x !== "—" &&
            x !== "-" &&
            x.toLowerCase() !== "null" &&
            x.toLowerCase() !== "undefined",
        )
        .map((x) => x.trim())
        .sort((a, b) => a.localeCompare(b));
    };

    return {
      companies: sanitizeList(backendOptions?.companies),
      salesmen: sanitizeList(backendOptions?.salesmen),
      preparedBy: sanitizeList(backendOptions?.preparedBy),
      statuses: sanitizeList(backendOptions?.statuses),
      piStatuses: sanitizeList(backendOptions?.piStatuses),
      typesOfWork: sanitizeList(backendOptions?.typesOfWork),
      zohoStatuses: sanitizeList(backendOptions?.zohoStatuses),
      sourceFiles: backendOptions?.sourceFiles || [],
      currentSourceFile: backendOptions?.currentSourceFile || null,
    };
  }, [accumulatedOptions, data?.currentSourceFile, data?.filterOptions, data?.records]);

  // Memoized records guaranteed sorted by Excel date and update date serialization
  const displayedRecords = useMemo(() => {
    const recs = data?.records || [];
    if (recs.length === 0) return recs;

    if (sort.field === "date") {
      return [...recs].sort((a, b) => compareRecordDates(a, b, sort.direction === "asc"));
    }

    const field = sort.field;
    const dir = sort.direction === "asc" ? 1 : -1;
    return [...recs].sort((a, b) => {
      const valA = a[field];
      const valB = b[field];

      if (valA === valB) {
        return compareRecordDates(a, b, true);
      }
      if (valA === null || valA === undefined) return 1;
      if (valB === null || valB === undefined) return -1;

      if (typeof valA === "number" && typeof valB === "number") {
        return (valA - valB) * dir;
      }

      return String(valA).localeCompare(String(valB)) * dir;
    });
  }, [data?.records, sort.field, sort.direction]);

  if (authChecking) {
    return (
      <div className="min-h-screen bg-ink-950 flex items-center justify-center p-5">
        <div className="flex flex-col items-center gap-3">
          <div className="h-8 w-8 rounded-full border-2 border-brand-400 border-t-transparent animate-spin" />
          <span className="text-xs font-mono text-text-muted">Verifying operator credentials...</span>
        </div>
      </div>
    );
  }

  if (isAuthRequired) {
    return (
      <div className="min-h-screen bg-ink-950 flex flex-col justify-center items-center py-20 px-4">
        <div className="w-full max-w-md rounded-panel border border-line-strong/80 bg-ink-900 text-center shadow-raised card-pad">
          <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl border border-brand-400/40 bg-brand-500/10 text-brand-300">
            <svg className="h-7 w-7" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={1.8}
                d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"
              />
            </svg>
          </div>
          <div className="mt-3.5 flex items-center justify-center gap-2">
            <Badge tone="warning" className="text-2xs font-mono">Restricted Access</Badge>
            <span className="text-2xs text-text-faint font-mono">Authentication Required</span>
          </div>
          <h2 className="mt-3 text-xl font-bold tracking-tight text-text-primary">
            Reports & Operations Sign In
          </h2>
          <p className="mt-2 text-xs leading-relaxed text-text-muted">
            Live operational records, factory transactions, and Excel Sync aggregation are restricted to registered operators and authorized staff.
          </p>
          <div className="mt-5 flex flex-col gap-3">
            <Link
              href={`/reports/login?redirect=${encodeURIComponent(pathname)}`}
              className="flex h-11 items-center justify-center rounded-xl bg-brand-400 px-5 text-sm font-semibold text-accent-ink transition-colors hover:bg-brand-300"
            >
              Sign In to Access Reports
            </Link>
            <Link
              href="/"
              className="flex h-10 items-center justify-center rounded-xl border border-line-strong/70 bg-ink-950/60 text-xs font-semibold text-text-muted hover:text-text-primary transition-colors"
            >
              ← Back to Public Website
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      {/* Header */}
      <RecordsHeader
        source={data?.source || "mock_preview"}
        serverTime={data?.serverTime}
        executionMs={data?.executionMs}
        loading={loading}
        onRefresh={handleRefresh}
        density={density}
        onToggleDensity={() => setDensity(density === "comfortable" ? "compact" : "comfortable")}
        totalEntries={data?.summary.totalEntries ?? 0}
        userEmail={userEmail}
        onSignOut={handleSignOut}
        currentSourceFile={data?.currentSourceFile || data?.filterOptions?.currentSourceFile}
        isCurrentSourceScoped={data?.isCurrentSourceScoped}
      />

      <div className="shell mt-5 space-y-4">
        {/* Error message banner */}
        {error && (
          <div className="rounded-2xl border border-state-danger/50 bg-state-danger/10 p-3.5 text-xs text-state-danger flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="font-bold">Error:</span>
              <span>{error}</span>
            </div>
            <button
              type="button"
              onClick={handleRefresh}
              className="rounded-lg border border-state-danger/40 px-3 py-1 font-semibold uppercase tracking-wider hover:bg-state-danger/20"
            >
              Retry
            </button>
          </div>
        )}

        {/* Local Mock Notice if in preview */}
        {data?.warning &&
          !error &&
          !data.warning.includes("PGRST") &&
          !data.warning.includes("Aggregation RPC") &&
          !data.warning.includes("schema cache") && (
            <div className="rounded-xl border border-state-warning/30 bg-state-warning/10 px-4 py-2 text-2xs text-state-warning flex items-center justify-between">
              <span>{data.warning}</span>
              <span className="font-mono text-3xs text-text-faint">Data isolation: active</span>
            </div>
          )}

        <ExportActions
          report="proformas"
          filters={filters}
          sort={sort}
          authToken={authToken}
          disabled={loading}
        />

        {/* Proforma → Job Order is the next hand-off in the document chain.
            The destination API owns the strict Confirmed gate and this link
            carries the operator's active workbook and filters across. */}
        <div className="flex flex-col gap-3 rounded-2xl border border-brand-400/25 bg-brand-500/[0.06] px-4 py-2.5 sm:flex-row sm:items-center sm:justify-between">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <span className="eyebrow text-brand-300">Next: Job Order</span>
              <Badge tone="accent" className="text-3xs font-mono">
                Confirmed PIs only
              </Badge>
            </div>
            <p className="mt-1 text-xs leading-relaxed text-text-muted">
              Continue from proforma to production hand-off. The queue groups each confirmed PI once and excludes drafts and later statuses.
            </p>
          </div>
          <LocaleLink
            href={jobOrdersHrefFromRecordFilters(filters)}
            className="inline-flex h-9 shrink-0 items-center justify-center rounded-xl border border-brand-400/40 bg-ink-950/60 px-4 text-xs font-semibold text-brand-200 transition-colors hover:border-brand-300 hover:text-brand-100"
          >
            Open Job Order Queue →
          </LocaleLink>
        </div>

        {/* Section 1: Summary Statistics Cards */}
        <RecordsSummaryCards summary={data?.summary || null} loading={loading} />

        {/* Section 2: Filters Panel */}
        <RecordsFilterPanel
          filters={filters}
          filterOptions={dataDrivenFilterOptions}
          onChange={handleFiltersChange}
          onReset={handleResetFilters}
          totalFiltered={data?.summary.totalEntries ?? 0}
          loading={loading}
        />

        {/* Section 3: Breakdowns (Prepared By, Salesman, Company, Type of Work) */}
        <RecordsBreakdowns
          breakdowns={data?.breakdowns || null}
          loading={loading}
          activePreparedBy={filters.preparedBy}
          activeSalesman={filters.salesman}
          activeCompany={filters.company}
          activeTypeOfWork={filters.typeOfWork}
          onSelectPreparedBy={(name) => handleFiltersChange({ preparedBy: name })}
          onSelectSalesman={(name) => handleFiltersChange({ salesman: name })}
          onSelectCompany={(name) => handleFiltersChange({ company: name })}
          onSelectTypeOfWork={(name) => handleFiltersChange({ typeOfWork: name })}
        />

        {/* Section 4: Records Table */}
        <RecordsTable
          records={displayedRecords}
          loading={loading}
          sort={sort}
          onSortChange={handleSortChange}
          onSelectRecord={(rec) => setSelectedRecord(rec)}
          density={density}
          onResetFilters={handleResetFilters}
        />

        {/* Section 5: Pagination */}
        {data && (
          <RecordsPagination
            pagination={data.pagination}
            onPageChange={handlePageChange}
            onPageSizeChange={handlePageSizeChange}
            loading={loading}
          />
        )}
      </div>

      {/* Record detail inspection modal */}
      <RecordsRecordDetailModal
        record={selectedRecord}
        onClose={() => setSelectedRecord(null)}
      />
    </div>
  );
}
