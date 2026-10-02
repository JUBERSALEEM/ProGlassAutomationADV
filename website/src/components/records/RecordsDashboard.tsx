"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { Badge } from "@/components/ui/Badge";
import { isSupabaseConfigured, supabaseBrowser } from "@/lib/supabase";
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
  direction: "desc",
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
    direction: searchParams.get("sortOrder") === "asc" ? "asc" : "desc",
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

  // Authentication & operator session state
  const [authChecking, setAuthChecking] = useState(() => isSupabaseConfigured());
  const [userEmail, setUserEmail] = useState<string | null>(null);
  const [authToken, setAuthToken] = useState<string | null>(null);
  const [isAuthRequired, setIsAuthRequired] = useState(false);

  const abortControllerRef = useRef<AbortController | null>(null);

  // Monitor Supabase Auth state for protected reports access
  useEffect(() => {
    if (!isSupabaseConfigured()) {
      return;
    }

    let isSubscribed = true;

    async function checkSession() {
      try {
        const supabase = supabaseBrowser();
        const {
          data: { session },
        } = await supabase.auth.getSession();
        if (!isSubscribed) return;

        if (session?.user) {
          setUserEmail(session.user.email ?? null);
          setAuthToken(session.access_token);
          setIsAuthRequired(false);
        } else if (process.env.NODE_ENV === "production") {
          setIsAuthRequired(true);
        }
      } catch (err) {
        console.warn("[records-dashboard] Session check failed:", err);
      } finally {
        if (isSubscribed) {
          setAuthChecking(false);
        }
      }
    }

    checkSession();

    let unsubscribe = () => {};
    try {
      const {
        data: { subscription },
      } = supabaseBrowser().auth.onAuthStateChange((_event, session) => {
        if (!isSubscribed) return;
        if (session?.user) {
          setUserEmail(session.user.email ?? null);
          setAuthToken(session.access_token);
          setIsAuthRequired(false);
        } else {
          setUserEmail(null);
          setAuthToken(null);
          if (process.env.NODE_ENV === "production") {
            setIsAuthRequired(true);
          }
        }
      });
      unsubscribe = () => subscription.unsubscribe();
    } catch {
      // ignore
    }

    return () => {
      isSubscribed = false;
      unsubscribe();
    };
  }, []);

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
      if (newSort.direction !== "desc") params.set("sortOrder", newSort.direction);

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
          setIsAuthRequired(true);
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
          if (loadedRecords.length > 0) {
            setAccumulatedOptions((prev) => {
              const mergeDistinct = (existing: string[], serverList: string[] = [], getter: (r: RecordRow) => unknown): string[] => {
                const set = new Set<string>([...existing, ...serverList]);
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
                companies: mergeDistinct(prev.companies, apiOptions?.companies, (r) => r.company),
                salesmen: mergeDistinct(prev.salesmen, apiOptions?.salesmen, (r) => r.salesman),
                preparedBy: mergeDistinct(prev.preparedBy, apiOptions?.preparedBy, (r) => r.prepared_by),
                statuses: mergeDistinct(prev.statuses, apiOptions?.statuses, (r) => r.status),
                piStatuses: mergeDistinct(prev.piStatuses, apiOptions?.piStatuses, (r) => r.pi_status),
                typesOfWork: mergeDistinct(prev.typesOfWork, apiOptions?.typesOfWork, (r) => r.type_of_work),
                zohoStatuses: mergeDistinct(prev.zohoStatuses, apiOptions?.zohoStatuses, (r) => r.zoho_status),
                sourceFiles: Array.from(new Set([...(prev.sourceFiles || []), ...(apiOptions?.sourceFiles || [])])).sort((a, b) => a.localeCompare(b)),
                currentSourceFile: apiOptions?.currentSourceFile ?? json.data?.currentSourceFile ?? prev.currentSourceFile,
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
  }, [page, pageSize, sort, filters, refreshCounter, authToken, isAuthRequired, authChecking]);

  // Handlers
  const handleSignOut = useCallback(async () => {
    try {
      if (isSupabaseConfigured()) {
        await supabaseBrowser().auth.signOut();
      }
      try {
        await fetch("/api/auth/signout", { method: "POST" });
      } catch {
        // ignore
      }
    } finally {
      setUserEmail(null);
      setAuthToken(null);
      setIsAuthRequired(true);
      router.push(`/reports/login?redirect=${encodeURIComponent(pathname)}`);
    }
  }, [pathname, router]);

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
      });
    }
    const nextFilters = { ...filters, ...updated };
    setLoading(true);
    setFilters(nextFilters);
    setPage(1); // Reset to page 1 on filter change
    updateUrlParams(nextFilters, sort, 1, pageSize);
  };

  const handleResetFilters = () => {
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

  if (authChecking) {
    return (
      <div className="min-h-screen bg-ink-950 flex items-center justify-center p-6">
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
        <div className="w-full max-w-md rounded-panel border border-line-strong/80 bg-ink-900/80 p-8 text-center shadow-raised backdrop-blur-xl">
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
          <div className="mt-4 flex items-center justify-center gap-2">
            <Badge tone="warning" className="text-2xs font-mono">Restricted Access</Badge>
            <span className="text-2xs text-text-faint font-mono">Authentication Required</span>
          </div>
          <h2 className="mt-3 text-xl font-bold tracking-tight text-text-primary">
            Reports & Operations Sign In
          </h2>
          <p className="mt-2 text-xs leading-relaxed text-text-muted">
            Live operational records, factory transactions, and Excel Sync aggregation are restricted to registered operators and authorized staff.
          </p>
          <div className="mt-6 flex flex-col gap-3">
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

      <div className="shell mt-6 space-y-6">
        {/* Error message banner */}
        {error && (
          <div className="rounded-2xl border border-state-danger/50 bg-state-danger/10 p-4 text-xs text-state-danger flex items-center justify-between">
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
        {data?.warning && !error && (
          <div className="rounded-xl border border-state-warning/30 bg-state-warning/10 px-4 py-2 text-2xs text-state-warning flex items-center justify-between">
            <span>{data.warning}</span>
            <span className="font-mono text-3xs text-text-faint">Data isolation: active</span>
          </div>
        )}

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
          records={data?.records || []}
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
