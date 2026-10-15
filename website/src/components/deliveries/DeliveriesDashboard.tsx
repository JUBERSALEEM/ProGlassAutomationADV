"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { useOperatorSession } from "@/lib/useOperatorSession";
import type {
  DeliveriesApiResponse,
  DeliveriesApiData,
  DeliveryFilters,
  DeliveryLifecycle,
  DeliveryRow,
  DeliverySort,
  DeliverySortField,
} from "@/types/deliveries";
import { lifecycleFromLabel, lifecycleLabel, sortDeliveryRows } from "@/lib/delivery-ledger";
import { DeliveriesHeader } from "./DeliveriesHeader";
import { DeliveriesSummaryCards } from "./DeliveriesSummaryCards";
import { DeliveriesFilterPanel } from "./DeliveriesFilterPanel";
import { DeliveriesBreakdowns } from "./DeliveriesBreakdowns";
import { DeliveriesTable } from "./DeliveriesTable";
import { DeliveriesDetailModal } from "./DeliveriesDetailModal";
import { RecordsPagination } from "@/components/records/RecordsPagination";
import { ExportActions } from "@/components/operations/ExportActions";

const INITIAL_FILTERS: DeliveryFilters = {
  search: "",
  datePreset: "all",
  dateFrom: "",
  dateTo: "",
  company: "",
  salesman: "",
  preparedBy: "",
  typeOfWork: "",
  piStatus: "",
  deliveryStatus: "",
  balanceOnly: false,
  sourceFile: "",
  pi: "",
};

const INITIAL_SORT: DeliverySort = {
  field: "order_date",
  direction: "desc",
};

const EMPTY_DATA: DeliveriesApiData | null = null;

/**
 * The tracker's client shell.
 *
 * Deliberately mirrors `RecordsDashboard`: same URL-as-state contract (every
 * filter, sort, page and the selected PI is in the query string, so a link can
 * carry a whole view), same bounded page sizes, same operator session, same
 * skeleton and gate. An operator who has used one page knows the other.
 *
 * What differs is the shape of the answer: rows are PIs, the expansion is a
 * ledger, and the stage filter speaks lifecycle rather than workbook status.
 */
export function DeliveriesDashboard() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const [filters, setFilters] = useState<DeliveryFilters>(() => ({
    search: searchParams.get("search") || "",
    datePreset: (searchParams.get("datePreset") || "all") as DeliveryFilters["datePreset"],
    dateFrom: searchParams.get("dateFrom") || "",
    dateTo: searchParams.get("dateTo") || "",
    company: searchParams.get("company") || "",
    salesman: searchParams.get("salesman") || "",
    preparedBy: searchParams.get("preparedBy") || "",
    typeOfWork: searchParams.get("typeOfWork") || "",
    piStatus: searchParams.get("piStatus") || "",
    deliveryStatus: (searchParams.get("deliveryStatus") || "") as DeliveryFilters["deliveryStatus"],
    balanceOnly: searchParams.get("balanceOnly") === "1",
    sourceFile: searchParams.get("sourceFile") || "",
    pi: searchParams.get("pi") || "",
  }));

  const [sort, setSort] = useState<DeliverySort>(() => ({
    field: (searchParams.get("sortBy") || "order_date") as DeliverySortField,
    direction: searchParams.get("sortOrder") === "asc" ? "asc" : "desc",
  }));

  const [page, setPage] = useState<number>(() => {
    const parsed = parseInt(searchParams.get("page") || "1", 10);
    return Number.isNaN(parsed) || parsed < 1 ? 1 : parsed;
  });

  const [pageSize, setPageSize] = useState<number>(() => {
    const parsed = parseInt(searchParams.get("pageSize") || "50", 10);
    return Number.isNaN(parsed) || parsed < 10 ? 50 : parsed;
  });

  const [density, setDensity] = useState<"comfortable" | "compact">("comfortable");
  const [data, setData] = useState<DeliveriesApiData | null>(EMPTY_DATA);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedRow, setSelectedRow] = useState<DeliveryRow | null>(null);
  const [refreshCounter, setRefreshCounter] = useState(0);

  const abortControllerRef = useRef<AbortController | null>(null);

  const {
    checking: authChecking,
    userEmail,
    authToken,
    isAuthRequired,
    signOut: handleSignOut,
    requireAuth,
  } = useOperatorSession();

  const updateUrlParams = useCallback(
    (nextFilters: DeliveryFilters, nextSort: DeliverySort, nextPage: number, nextPageSize: number) => {
      const params = new URLSearchParams();

      if (nextFilters.search) params.set("search", nextFilters.search);
      if (nextFilters.pi) params.set("pi", nextFilters.pi);
      if (nextFilters.datePreset !== "all") params.set("datePreset", nextFilters.datePreset);
      if (nextFilters.dateFrom) params.set("dateFrom", nextFilters.dateFrom);
      if (nextFilters.dateTo) params.set("dateTo", nextFilters.dateTo);
      if (nextFilters.company) params.set("company", nextFilters.company);
      if (nextFilters.salesman) params.set("salesman", nextFilters.salesman);
      if (nextFilters.preparedBy) params.set("preparedBy", nextFilters.preparedBy);
      if (nextFilters.typeOfWork) params.set("typeOfWork", nextFilters.typeOfWork);
      if (nextFilters.piStatus) params.set("piStatus", nextFilters.piStatus);
      if (nextFilters.deliveryStatus) params.set("deliveryStatus", nextFilters.deliveryStatus);
      if (nextFilters.balanceOnly) params.set("balanceOnly", "1");
      if (nextFilters.sourceFile) params.set("sourceFile", nextFilters.sourceFile);

      if (nextSort.field !== INITIAL_SORT.field) params.set("sortBy", nextSort.field);
      if (nextSort.direction !== INITIAL_SORT.direction) params.set("sortOrder", nextSort.direction);

      if (nextPage > 1) params.set("page", String(nextPage));
      if (nextPageSize !== 50) params.set("pageSize", String(nextPageSize));

      const query = params.toString();
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    },
    [pathname, router],
  );

  // Fetch whenever filters, sort, pagination, session or refresh counter change.
  useEffect(() => {
    if (authChecking || (isAuthRequired && !authToken)) return;

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
        if (filters.pi) params.set("pi", filters.pi);
        if (filters.dateFrom) params.set("dateFrom", filters.dateFrom);
        if (filters.dateTo) params.set("dateTo", filters.dateTo);
        if (filters.company) params.set("company", filters.company);
        if (filters.salesman) params.set("salesman", filters.salesman);
        if (filters.preparedBy) params.set("preparedBy", filters.preparedBy);
        if (filters.typeOfWork) params.set("typeOfWork", filters.typeOfWork);
        if (filters.piStatus) params.set("piStatus", filters.piStatus);
        if (filters.deliveryStatus) params.set("deliveryStatus", filters.deliveryStatus);
        if (filters.balanceOnly) params.set("balanceOnly", "1");
        if (filters.sourceFile) params.set("sourceFile", filters.sourceFile);

        const headers: Record<string, string> = {};
        if (authToken) headers.Authorization = `Bearer ${authToken}`;

        const response = await fetch(`/api/deliveries?${params.toString()}`, {
          signal: controller.signal,
          headers,
          cache: "no-store",
        });

        if (response.status === 401) {
          requireAuth();
          setLoading(false);
          return;
        }

        const json: DeliveriesApiResponse = await response.json();
        if (isCancelled) return;

        if (!response.ok || !json.ok) {
          throw new Error(json.ok ? "Failed to load deliveries" : json.error.message);
        }

        setData(json.data);
        setError(null);
      } catch (fetchError) {
        if (!isCancelled && !(fetchError instanceof Error && fetchError.name === "AbortError")) {
          console.error("[deliveries-dashboard] Data fetch failed:", fetchError);
          setError(
            fetchError instanceof Error
              ? fetchError.message
              : "Unable to load deliveries. Please try again.",
          );
        }
      } finally {
        if (!isCancelled) setLoading(false);
      }
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

  const handleRefresh = useCallback(() => {
    setLoading(true);
    setRefreshCounter((counter) => counter + 1);
  }, []);

  const handleFiltersChange = (updated: Partial<DeliveryFilters>) => {
    // Changing the workbook invalidates every option and page the previous
    // workbook produced — same reset contract as the reports dashboard.
    if (updated.sourceFile !== undefined && updated.sourceFile !== filters.sourceFile) {
      const nextFilters: DeliveryFilters = {
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
    setPage(1);
    updateUrlParams(nextFilters, sort, 1, pageSize);
  };

  const handleResetFilters = () => {
    setLoading(true);
    setFilters(INITIAL_FILTERS);
    setSort(INITIAL_SORT);
    setPage(1);
    updateUrlParams(INITIAL_FILTERS, INITIAL_SORT, 1, pageSize);
  };

  const handleSortChange = (field: DeliverySortField) => {
    const nextSort: DeliverySort = {
      field,
      direction: sort.field === field && sort.direction === "desc" ? "asc" : "desc",
    };
    setLoading(true);
    setSort(nextSort);
    updateUrlParams(filters, nextSort, page, pageSize);
  };

  const handlePageChange = (nextPage: number) => {
    setLoading(true);
    setPage(nextPage);
    updateUrlParams(filters, sort, nextPage, pageSize);
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const handlePageSizeChange = (nextSize: number) => {
    setLoading(true);
    setPageSize(nextSize);
    setPage(1);
    updateUrlParams(filters, sort, 1, nextSize);
  };

  /** Rows come sorted from the API; this only re-sorts the current page. */
  const displayedRows = useMemo(() => {
    const rows = data?.rows ?? [];
    if (rows.length === 0) return rows;
    return sortDeliveryRows(rows, sort);
  }, [data?.rows, sort]);

  if (authChecking) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-ink-950 p-5">
        <div className="flex flex-col items-center gap-3">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-brand-400 border-t-transparent" />
          <span className="font-mono text-xs text-text-muted">Verifying operator credentials...</span>
        </div>
      </div>
    );
  }

  if (isAuthRequired) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-ink-950 px-4 py-20">
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
            <Badge tone="warning" className="font-mono text-2xs">
              Restricted Access
            </Badge>
            <span className="font-mono text-2xs text-text-faint">Authentication Required</span>
          </div>
          <h2 className="mt-3 text-xl font-bold tracking-tight text-text-primary">
            Deliveries Tracker Sign In
          </h2>
          <p className="mt-2 text-xs leading-relaxed text-text-muted">
            Dispatch records, delivery balances and customer movements are operational data and are
            restricted to registered operators and authorized staff.
          </p>
          <div className="mt-5 flex flex-col gap-3">
            <LocaleLink
              href={`/reports/login?redirect=${encodeURIComponent(pathname)}`}
              className="flex h-11 items-center justify-center rounded-xl bg-brand-400 px-5 text-sm font-semibold text-accent-ink transition-colors hover:bg-brand-300"
            >
              Sign In to Access Deliveries
            </LocaleLink>
            <LocaleLink
              href="/"
              className="flex h-10 items-center justify-center rounded-xl border border-line-strong/70 bg-ink-950/60 text-xs font-semibold text-text-muted transition-colors hover:text-text-primary"
            >
              ← Back to Public Website
            </LocaleLink>
          </div>
        </div>
      </div>
    );
  }

  const activeLifecycleLabel = filters.deliveryStatus
    ? lifecycleLabel(filters.deliveryStatus)
    : undefined;

  return (
    <div className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <DeliveriesHeader
        source={data?.source || "mock_preview"}
        serverTime={data?.serverTime}
        executionMs={data?.executionMs}
        loading={loading}
        onRefresh={handleRefresh}
        totalPis={data?.summary.totalPis ?? 0}
        confirmedStatuses={data?.confirmedStatuses ?? ["Confirmed"]}
        ledgerInstalled={data?.ledgerInstalled ?? false}
        userEmail={userEmail}
        onSignOut={handleSignOut}
        currentSourceFile={data?.currentSourceFile}
        isCurrentSourceScoped={data?.isCurrentSourceScoped}
      />

      <div className="shell mt-5 space-y-4">
        {error && (
          <div className="flex items-center justify-between rounded-2xl border border-state-danger/50 bg-state-danger/10 p-3.5 text-xs text-state-danger">
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

        {data?.warning && !error && (
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-state-warning/30 bg-state-warning/10 px-4 py-2 text-2xs text-state-warning">
            <span>{data.warning}</span>
            <span className="font-mono text-3xs text-text-faint">
              {data.ledgerInstalled ? "Ledger: live" : "Ledger: not installed"}
            </span>
          </div>
        )}

        <ExportActions
          report="deliveries"
          filters={filters}
          sort={sort}
          authToken={authToken}
          disabled={loading}
        />

        <DeliveriesSummaryCards summary={data?.summary ?? null} loading={loading} />

        <DeliveriesFilterPanel
          filters={filters}
          filterOptions={
            data?.filterOptions ?? {
              companies: [],
              salesmen: [],
              preparedBy: [],
              typesOfWork: [],
              piStatuses: [],
              lifecycles: [],
              recordStatuses: [],
              sourceFiles: [],
              currentSourceFile: null,
            }
          }
          onChange={handleFiltersChange}
          onReset={handleResetFilters}
          totalFiltered={data?.summary.totalPis ?? 0}
          loading={loading}
        />

        <DeliveriesBreakdowns
          breakdowns={data?.breakdowns ?? null}
          loading={loading}
          activeCompany={filters.company}
          activeSalesman={filters.salesman}
          activeTypeOfWork={filters.typeOfWork}
          activeLifecycle={activeLifecycleLabel}
          onSelectCompany={(name) => handleFiltersChange({ company: name })}
          onSelectSalesman={(name) => handleFiltersChange({ salesman: name })}
          onSelectTypeOfWork={(name) => handleFiltersChange({ typeOfWork: name })}
          onSelectLifecycle={(label) =>
            handleFiltersChange({
              deliveryStatus: lifecycleFromLabel(label) as DeliveryLifecycle | "",
            })
          }
        />

        <DeliveriesTable
          rows={displayedRows}
          loading={loading}
          sort={sort}
          onSortChange={handleSortChange}
          onSelectRow={(row) => setSelectedRow(row)}
          density={density}
          onResetFilters={handleResetFilters}
        />

        <div className="flex flex-wrap items-center justify-between gap-3">
          <button
            type="button"
            onClick={() =>
              setDensity(density === "comfortable" ? "compact" : "comfortable")
            }
            className="rounded-lg border border-line-strong/70 bg-ink-850/80 px-3 py-1.5 font-mono text-2xs uppercase text-text-muted transition-colors hover:text-text-primary"
          >
            Density: {density}
          </button>

          {data && (
            <div className="flex-1">
              <RecordsPagination
                pagination={data.pagination}
                onPageChange={handlePageChange}
                onPageSizeChange={handlePageSizeChange}
                loading={loading}
              />
            </div>
          )}
        </div>
      </div>

      <DeliveriesDetailModal row={selectedRow} onClose={() => setSelectedRow(null)} />
    </div>
  );
}
