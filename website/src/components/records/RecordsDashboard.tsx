"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
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

  const abortControllerRef = useRef<AbortController | null>(null);

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

        const response = await fetch(`/api/records?${params.toString()}`, {
          signal: controller.signal,
        });

        const json: RecordsApiResponse = await response.json();

        if (!isCancelled) {
          if (!response.ok || !json.ok) {
            throw new Error(json.ok ? "Failed to load records" : json.error.message);
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

    load();

    return () => {
      isCancelled = true;
      controller.abort();
    };
  }, [page, pageSize, sort, filters, refreshCounter]);

  // Handlers
  const handleRefresh = useCallback(() => {
    setLoading(true);
    setRefreshCounter((c) => c + 1);
  }, []);

  const handleFiltersChange = (updated: Partial<RecordFilters>) => {
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

  const emptyFilterOptions: FilterOptions = {
    companies: [],
    salesmen: [],
    preparedBy: [],
    statuses: [],
    piStatuses: [],
    typesOfWork: [],
    zohoStatuses: [],
  };

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
          filterOptions={data?.filterOptions || emptyFilterOptions}
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
