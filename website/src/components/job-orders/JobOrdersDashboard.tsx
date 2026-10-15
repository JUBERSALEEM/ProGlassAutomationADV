"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { RecordsPagination } from "@/components/records/RecordsPagination";
import { useOperatorSession } from "@/lib/useOperatorSession";
import { JobOrderDetailModal } from "@/components/job-orders/JobOrderDetailModal";
import { JobOrdersHeader } from "@/components/job-orders/JobOrdersHeader";
import { JobOrdersSummaryCards } from "@/components/job-orders/JobOrdersSummaryCards";
import { JobOrdersTable } from "@/components/job-orders/JobOrdersTable";
import { ExportActions } from "@/components/operations/ExportActions";
import type {
  JobOrderFilters,
  JobOrderRow,
  JobOrderSort,
  JobOrderSortField,
  JobOrdersApiData,
  JobOrdersApiResponse,
} from "@/types/job-orders";
import type { DatePreset } from "@/types/records";

const INITIAL_FILTERS: JobOrderFilters = {
  search: "",
  datePreset: "all",
  dateFrom: "",
  dateTo: "",
  company: "",
  salesman: "",
  preparedBy: "",
  typeOfWork: "",
};
const INITIAL_SORT: JobOrderSort = { field: "order_date", direction: "desc" };
const PAGE_SIZES = [50, 100, 250];

function validDatePreset(value: string | null): DatePreset {
  const presets: DatePreset[] = ["all", "today", "yesterday", "this_week", "this_month", "previous_month", "custom"];
  return presets.includes(value as DatePreset) ? (value as DatePreset) : "all";
}

function validSortField(value: string | null): JobOrderSortField {
  const fields: JobOrderSortField[] = ["order_date", "pi_number", "company", "ordered_qty", "ordered_sqm"];
  return fields.includes(value as JobOrderSortField) ? (value as JobOrderSortField) : "order_date";
}

function AccessGate({ pathname }: { pathname: string }) {
  return (
    <div className="min-h-screen bg-ink-950 px-4 py-20 text-text-primary">
      <div className="mx-auto w-full max-w-md rounded-panel border border-line-strong/80 bg-ink-900 p-6 text-center shadow-raised">
        <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl border border-brand-400/40 bg-brand-500/10 text-brand-300">
          <svg aria-hidden="true" className="h-7 w-7" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="1.8" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6zm10-10V7a4 4 0 00-8 0v4h8z" />
          </svg>
        </div>
        <div className="mt-3.5 flex items-center justify-center gap-2">
          <Badge tone="warning">Restricted access</Badge>
          <span className="font-mono text-2xs text-text-faint">Authentication required</span>
        </div>
        <h2 className="mt-3 text-xl font-bold tracking-tight">Job Order Queue Sign In</h2>
        <p className="mt-2 text-xs leading-relaxed text-text-muted">
          Confirmed proforma records are restricted to registered operators and authorized staff.
        </p>
        <div className="mt-5 flex flex-col gap-3">
          <LocaleLink href={`/reports/login?redirect=${encodeURIComponent(pathname)}`} className="flex h-11 items-center justify-center rounded-xl bg-brand-400 px-5 text-sm font-semibold text-accent-ink transition-colors hover:bg-brand-300">
            Sign in to view Job Orders
          </LocaleLink>
          <LocaleLink href="/" className="flex h-10 items-center justify-center rounded-xl border border-line-strong/70 bg-ink-950/60 text-xs font-semibold text-text-muted transition-colors hover:text-text-primary">
            ← Back to public website
          </LocaleLink>
        </div>
      </div>
    </div>
  );
}

function LoadingScreen() {
  return (
    <div className="flex min-h-[55vh] items-center justify-center bg-ink-950 p-5 text-text-primary">
      <div className="flex flex-col items-center gap-3">
        <div className="h-8 w-8 animate-spin rounded-full border-2 border-brand-400 border-t-transparent" />
        <span className="font-mono text-xs text-text-muted">Verifying operator credentials…</span>
      </div>
    </div>
  );
}

export function JobOrdersDashboard() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const [filters, setFilters] = useState<JobOrderFilters>(() => ({
    ...INITIAL_FILTERS,
    search: searchParams.get("search") || "",
    datePreset: validDatePreset(searchParams.get("datePreset")),
    dateFrom: searchParams.get("dateFrom") || "",
    dateTo: searchParams.get("dateTo") || "",
    company: searchParams.get("company") || "",
    salesman: searchParams.get("salesman") || "",
    preparedBy: searchParams.get("preparedBy") || "",
    typeOfWork: searchParams.get("typeOfWork") || "",
    sourceFile: searchParams.get("sourceFile") || "",
  }));
  const [searchInput, setSearchInput] = useState(searchParams.get("search") || "");
  const [sort, setSort] = useState<JobOrderSort>(() => ({
    field: validSortField(searchParams.get("sortBy")),
    direction: searchParams.get("sortOrder") === "asc" ? "asc" : "desc",
  }));
  const [page, setPage] = useState(() => Math.max(1, parseInt(searchParams.get("page") || "1", 10) || 1));
  const [pageSize, setPageSize] = useState(() => {
    const requested = parseInt(searchParams.get("pageSize") || "50", 10);
    return PAGE_SIZES.includes(requested) ? requested : 50;
  });
  const [data, setData] = useState<JobOrdersApiData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedRow, setSelectedRow] = useState<JobOrderRow | null>(null);
  const [refreshCounter, setRefreshCounter] = useState(0);

  const {
    checking: authChecking,
    userEmail,
    authToken,
    isAuthRequired,
    signOut: handleSignOut,
    requireAuth,
  } = useOperatorSession();
  const abortControllerRef = useRef<AbortController | null>(null);

  const updateUrl = useCallback((nextFilters: JobOrderFilters, nextSort: JobOrderSort, nextPage: number, nextPageSize: number) => {
    const params = new URLSearchParams();
    if (nextFilters.search) params.set("search", nextFilters.search);
    if (nextFilters.datePreset !== "all") params.set("datePreset", nextFilters.datePreset);
    if (nextFilters.dateFrom) params.set("dateFrom", nextFilters.dateFrom);
    if (nextFilters.dateTo) params.set("dateTo", nextFilters.dateTo);
    if (nextFilters.company) params.set("company", nextFilters.company);
    if (nextFilters.salesman) params.set("salesman", nextFilters.salesman);
    if (nextFilters.preparedBy) params.set("preparedBy", nextFilters.preparedBy);
    if (nextFilters.typeOfWork) params.set("typeOfWork", nextFilters.typeOfWork);
    if (nextFilters.sourceFile) params.set("sourceFile", nextFilters.sourceFile);
    if (nextSort.field !== INITIAL_SORT.field) params.set("sortBy", nextSort.field);
    if (nextSort.direction !== INITIAL_SORT.direction) params.set("sortOrder", nextSort.direction);
    if (nextPage > 1) params.set("page", String(nextPage));
    if (nextPageSize !== 50) params.set("pageSize", String(nextPageSize));
    const query = params.toString();
    router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
  }, [pathname, router]);

  useEffect(() => {
    if (authChecking || (isAuthRequired && !authToken)) return;
    let cancelled = false;
    const controller = new AbortController();
    abortControllerRef.current?.abort();
    abortControllerRef.current = controller;

    const load = async () => {
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
        if (filters.typeOfWork) params.set("typeOfWork", filters.typeOfWork);
        if (filters.sourceFile) params.set("sourceFile", filters.sourceFile);

        const headers: Record<string, string> = {};
        if (authToken) headers.Authorization = `Bearer ${authToken}`;
        const response = await fetch(`/api/job-orders?${params.toString()}`, {
          signal: controller.signal,
          headers,
          cache: "no-store",
        });
        const json: JobOrdersApiResponse = await response.json();
        if (cancelled) return;
        if (response.status === 401) {
          requireAuth();
          setLoading(false);
          return;
        }
        if (!response.ok || !json.ok) throw new Error(json.ok ? "Unable to load Job Orders." : json.error.message);
        setData(json.data);
        setError(null);
      } catch (loadError) {
        if (!cancelled && !(loadError instanceof Error && loadError.name === "AbortError")) {
          console.error("[job-orders-dashboard] Data fetch failed:", loadError);
          setError(loadError instanceof Error ? loadError.message : "Unable to load confirmed proformas.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    void load();
    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [authChecking, authToken, filters, isAuthRequired, page, pageSize, refreshCounter, requireAuth, sort]);

  const handleRefresh = useCallback(() => {
    setLoading(true);
    setRefreshCounter((value) => value + 1);
  }, []);

  const handleFilterChange = (update: Partial<JobOrderFilters>) => {
    const next = { ...filters, ...update };
    setFilters(next);
    setPage(1);
    setLoading(true);
    updateUrl(next, sort, 1, pageSize);
  };

  const handleSortChange = (field: JobOrderSortField) => {
    const next: JobOrderSort = { field, direction: sort.field === field && sort.direction === "asc" ? "desc" : "asc" };
    setSort(next);
    setPage(1);
    setLoading(true);
    updateUrl(filters, next, 1, pageSize);
  };

  const handlePageChange = (nextPage: number) => {
    setPage(nextPage);
    setLoading(true);
    updateUrl(filters, sort, nextPage, pageSize);
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const handlePageSizeChange = (nextSize: number) => {
    setPageSize(nextSize);
    setPage(1);
    setLoading(true);
    updateUrl(filters, sort, 1, nextSize);
  };

  const handleResetFilters = () => {
    setFilters(INITIAL_FILTERS);
    setSearchInput("");
    setPage(1);
    setLoading(true);
    updateUrl(INITIAL_FILTERS, sort, 1, pageSize);
  };

  const handleSearchSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    handleFilterChange({ search: searchInput.trim() });
  };

  if (authChecking) return <LoadingScreen />;
  if (isAuthRequired) return <AccessGate pathname={pathname || "/job-orders"} />;

  const rows = data?.rows ?? [];
  const activeFilters = Boolean(
    filters.search || filters.company || filters.salesman || filters.preparedBy || filters.typeOfWork ||
    filters.sourceFile || filters.datePreset !== "all",
  );
  const pagination = data?.pagination ?? { page, pageSize, totalEntries: 0, totalPages: 1 };

  return (
    <div className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <JobOrdersHeader
        source={data?.source || "mock_preview"}
        serverTime={data?.serverTime}
        executionMs={data?.executionMs}
        totalJobOrders={data?.summary.totalJobOrders ?? 0}
        loading={loading}
        onRefresh={handleRefresh}
        userEmail={userEmail}
        onSignOut={handleSignOut}
        currentSourceFile={data?.currentSourceFile || data?.filterOptions.currentSourceFile}
        sourceFileFilter={filters.sourceFile}
        isCurrentSourceScoped={data?.isCurrentSourceScoped}
      />

      <main className="shell mt-5 space-y-4">
        {error && (
          <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-state-danger/50 bg-state-danger/10 p-3.5 text-xs text-state-danger">
            <p><strong className="mr-1">Error:</strong>{error}</p>
            <button type="button" onClick={handleRefresh} className="rounded-lg border border-state-danger/40 px-3 py-1 font-semibold uppercase tracking-wider hover:bg-state-danger/20">Retry</button>
          </div>
        )}
        {data?.warning && !error && (
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-state-warning/30 bg-state-warning/10 px-4 py-2.5 text-2xs text-state-warning">
            <span>{data.warning}</span>
            <span className="font-mono text-3xs text-text-faint">Confirmed-only source</span>
          </div>
        )}

        <ExportActions
          report="job-orders"
          filters={filters}
          sort={sort}
          authToken={authToken}
          disabled={loading}
        />

        <JobOrdersSummaryCards summary={data?.summary ?? null} loading={loading} />

        <section aria-label="Filter Job Orders" className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-3.5 shadow-panel sm:p-4">
          <div className="flex flex-col gap-3.5 xl:flex-row xl:items-end">
            <form onSubmit={handleSearchSubmit} className="min-w-0 flex-1">
              <label htmlFor="job-order-search" className="mb-1.5 block text-3xs font-semibold uppercase tracking-wider text-text-faint">Search confirmed PIs</label>
              <div className="flex gap-2">
                <input id="job-order-search" type="search" value={searchInput} onChange={(event) => setSearchInput(event.target.value)} placeholder="PI number, customer reference, company…" className="min-w-0 flex-1 rounded-xl border border-line-strong/70 bg-ink-950/80 px-3.5 py-2.5 text-xs text-text-primary placeholder:text-text-faint focus:border-brand-400 focus:outline-none" />
                <button type="submit" className="rounded-xl bg-brand-400 px-4 py-2 text-xs font-semibold text-accent-ink transition-colors hover:bg-brand-300">Search</button>
              </div>
            </form>
            <FilterSelect label="Company" value={filters.company} options={data?.filterOptions.companies ?? []} onChange={(value) => handleFilterChange({ company: value })} />
            <FilterSelect label="Salesman" value={filters.salesman} options={data?.filterOptions.salesmen ?? []} onChange={(value) => handleFilterChange({ salesman: value })} />
            <FilterSelect label="Work type" value={filters.typeOfWork} options={data?.filterOptions.typesOfWork ?? []} onChange={(value) => handleFilterChange({ typeOfWork: value })} />
            <SourceFileSelect
              value={filters.sourceFile || ""}
              files={data?.filterOptions.sourceFiles ?? []}
              onChange={(value) => handleFilterChange({ sourceFile: value })}
            />
            <label className="min-w-36 text-3xs font-semibold uppercase tracking-wider text-text-faint">
              Order date
              <select value={filters.datePreset} onChange={(event) => handleFilterChange({ datePreset: validDatePreset(event.target.value), dateFrom: "", dateTo: "" })} className="mt-1.5 block w-full rounded-xl border border-line-strong/70 bg-ink-950/80 px-3 py-2.5 text-xs font-normal normal-case tracking-normal text-text-primary focus:border-brand-400 focus:outline-none">
                <option value="all">All time</option>
                <option value="today">Today</option>
                <option value="yesterday">Yesterday</option>
                <option value="this_week">This week</option>
                <option value="this_month">This month</option>
                <option value="previous_month">Previous month</option>
                <option value="custom">Custom range</option>
              </select>
            </label>
          </div>
          {filters.datePreset === "custom" && (
            <div className="mt-3.5 flex flex-wrap gap-3">
              <label className="text-3xs font-semibold uppercase tracking-wider text-text-faint">From
                <input type="date" value={filters.dateFrom} onChange={(event) => handleFilterChange({ dateFrom: event.target.value })} className="ml-2 rounded-lg border border-line-strong/70 bg-ink-950/80 px-2.5 py-1.5 font-mono text-xs text-text-primary focus:border-brand-400 focus:outline-none" />
              </label>
              <label className="text-3xs font-semibold uppercase tracking-wider text-text-faint">To
                <input type="date" value={filters.dateTo} onChange={(event) => handleFilterChange({ dateTo: event.target.value })} className="ml-2 rounded-lg border border-line-strong/70 bg-ink-950/80 px-2.5 py-1.5 font-mono text-xs text-text-primary focus:border-brand-400 focus:outline-none" />
              </label>
            </div>
          )}
          <div className="mt-3.5 flex flex-wrap items-center justify-between gap-2 border-t border-line-strong/50 pt-3">
            <div className="flex flex-wrap items-center gap-2">
              <Badge tone="success">Status locked: Confirmed</Badge>
              {filters.preparedBy && <Badge tone="neutral">Prepared by: {filters.preparedBy}</Badge>}
              {activeFilters && <button type="button" onClick={handleResetFilters} className="text-2xs font-semibold text-text-muted underline underline-offset-2 transition-colors hover:text-text-primary">Clear all filters</button>}
            </div>
            <span className="font-mono text-2xs text-text-faint">{pagination.totalEntries.toLocaleString()} matching PIs</span>
          </div>
        </section>

        <JobOrdersTable
          rows={rows}
          loading={loading}
          sort={sort}
          onSortChange={handleSortChange}
          onSelect={setSelectedRow}
          productionTrackingAvailable={data?.productionTrackingAvailable ?? false}
          onResetFilters={activeFilters ? handleResetFilters : undefined}
        />

        {pagination.totalEntries > 0 && (
          <RecordsPagination pagination={pagination} onPageChange={handlePageChange} onPageSizeChange={handlePageSizeChange} loading={loading} />
        )}
      </main>

      <JobOrderDetailModal
        key={selectedRow?.id ?? "closed"}
        row={selectedRow}
        authToken={authToken}
        productionTrackingAvailable={data?.productionTrackingAvailable ?? false}
        onClose={() => setSelectedRow(null)}
        onChanged={() => {
          setSelectedRow(null);
          handleRefresh();
        }}
      />
    </div>
  );
}

function FilterSelect({
  label,
  value,
  options,
  onChange,
}: {
  label: string;
  value: string;
  options: string[];
  onChange: (value: string) => void;
}) {
  const placeholders: Record<string, string> = {
    Company: "All companies",
    Salesman: "All salesmen",
    "Work type": "All work types",
  };
  return (
    <label className="min-w-36 text-3xs font-semibold uppercase tracking-wider text-text-faint">
      {label}
      <select value={value} onChange={(event) => onChange(event.target.value)} className="mt-1.5 block w-full rounded-xl border border-line-strong/70 bg-ink-950/80 px-3 py-2.5 text-xs font-normal normal-case tracking-normal text-text-primary focus:border-brand-400 focus:outline-none">
        <option value="">{placeholders[label] || `All ${label.toLowerCase()}`}</option>
        {options.map((option) => <option key={option} value={option}>{option}</option>)}
      </select>
    </label>
  );
}

function SourceFileSelect({
  value,
  files,
  onChange,
}: {
  value: string;
  files: string[];
  onChange: (value: string) => void;
}) {
  return (
    <label className="min-w-44 text-3xs font-semibold uppercase tracking-wider text-text-faint">
      Workbook
      <select value={value} onChange={(event) => onChange(event.target.value)} className="mt-1.5 block w-full rounded-xl border border-line-strong/70 bg-ink-950/80 px-3 py-2.5 text-xs font-normal normal-case tracking-normal text-text-primary focus:border-brand-400 focus:outline-none">
        <option value="">Current workbook (auto)</option>
        <option value="all">All workbooks</option>
        {files.map((file) => <option key={file} value={file}>{file.split(/[\\/]/).pop() || file}</option>)}
      </select>
    </label>
  );
}
