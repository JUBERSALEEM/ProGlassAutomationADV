"use client";

import { useEffect, useState, useTransition } from "react";
import type {
  DeliveryDatePreset,
  DeliveryFilterOptions,
  DeliveryFilters,
  DeliveryLifecycle,
} from "@/types/deliveries";
import { lifecycleLabel } from "@/lib/delivery-ledger";

type DeliveriesFilterPanelProps = {
  filters: DeliveryFilters;
  filterOptions: DeliveryFilterOptions;
  onChange: (updated: Partial<DeliveryFilters>) => void;
  onReset: () => void;
  totalFiltered: number;
  loading: boolean;
};

const DATE_PRESETS: { id: DeliveryDatePreset; label: string }[] = [
  { id: "all", label: "All Time" },
  { id: "today", label: "Today" },
  { id: "yesterday", label: "Yesterday" },
  { id: "this_week", label: "This Week" },
  { id: "this_month", label: "This Month" },
  { id: "previous_month", label: "Prev Month" },
  { id: "custom", label: "Custom Range" },
];

const LIFECYCLE_CHOICES: DeliveryLifecycle[] = [
  "awaiting_dispatch",
  "partial",
  "complete",
  "over_delivered",
];

function getDisplayFileName(filepath: string): string {
  if (!filepath) return "";
  const parts = filepath.split(/[\\/]/);
  return parts[parts.length - 1] || filepath;
}

/**
 * The tracker's filter bar.
 *
 * Two controls here do not exist on the Reports dashboard and are the whole
 * point of a separate page: the **delivery lifecycle** selector (awaiting /
 * part / complete / over) and **only outstanding balance**. "Who still owes us
 * glass" is the question this page exists to answer, and it should be one click
 * from the default view rather than something reconstructed by reading a
 * quantity column.
 *
 * The date presets filter on the PI's order date — when the commitment was
 * made — and the panel says so, because a reader who assumes they filter on
 * delivery date would draw the wrong conclusion from an empty week.
 */
export function DeliveriesFilterPanel({
  filters,
  filterOptions,
  onChange,
  onReset,
  totalFiltered,
  loading: _loading,
}: DeliveriesFilterPanelProps) {
  const [, startTransition] = useTransition();
  const [searchInput, setSearchInput] = useState(filters.search);
  const [prevFilterSearch, setPrevFilterSearch] = useState(filters.search);

  if (filters.search !== prevFilterSearch) {
    setPrevFilterSearch(filters.search);
    setSearchInput(filters.search);
  }

  useEffect(() => {
    const handler = setTimeout(() => {
      if (searchInput !== filters.search) {
        startTransition(() => onChange({ search: searchInput }));
      }
    }, 350);
    return () => clearTimeout(handler);
  }, [searchInput, filters.search, onChange]);

  const activeFilterCount = [
    Boolean(filters.search),
    filters.datePreset !== "all",
    Boolean(filters.company),
    Boolean(filters.salesman),
    Boolean(filters.preparedBy),
    Boolean(filters.typeOfWork),
    Boolean(filters.piStatus),
    Boolean(filters.deliveryStatus),
    filters.balanceOnly,
    Boolean(filters.sourceFile),
    Boolean(filters.pi),
  ].filter(Boolean).length;

  return (
    <div className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-4 shadow-panel sm:p-5">
      {/* Row 1: search + date presets + the outstanding-balance switch */}
      <div className="flex flex-col gap-3.5 lg:flex-row lg:items-center lg:justify-between">
        <div className="relative min-w-[280px] flex-1">
          <span className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-text-faint">
            <svg aria-hidden="true" className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
              />
            </svg>
          </span>
          <input
            type="search"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder="Search PI number, customer ref, company, delivery note…"
            className="w-full rounded-xl border border-line-strong/70 bg-ink-950/80 py-2.5 pl-10 pr-9 text-xs text-text-primary placeholder:text-text-faint/70 focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400 sm:text-sm"
          />
          {searchInput && (
            <button
              type="button"
              onClick={() => {
                setSearchInput("");
                onChange({ search: "" });
              }}
              className="absolute inset-y-0 right-0 flex items-center pr-3 text-text-faint hover:text-text-primary"
              title="Clear search"
            >
              ×
            </button>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-1.5 overflow-x-auto pb-1 sm:pb-0">
          <span className="mr-1 hidden text-3xs font-semibold uppercase tracking-wider text-text-faint xl:inline-block">
            Order date:
          </span>
          {DATE_PRESETS.map((preset) => {
            const isSelected = filters.datePreset === preset.id;
            return (
              <button
                key={preset.id}
                type="button"
                onClick={() => onChange({ datePreset: preset.id })}
                className={`rounded-lg px-2.5 py-1.5 font-mono text-2xs font-medium transition-all ${
                  isSelected
                    ? "border border-brand-400 bg-brand-500/25 text-brand-200 shadow-sm"
                    : "border border-line-strong/60 bg-ink-850/60 text-text-muted hover:border-brand-400/40 hover:text-text-primary"
                }`}
              >
                {preset.label}
              </button>
            );
          })}
        </div>
      </div>

      {filters.datePreset === "custom" && (
        <div className="mt-3.5 flex flex-wrap items-center gap-3 border-t border-line/60 pt-3.5 text-xs text-text-muted">
          <span className="font-mono text-2xs uppercase text-text-faint">
            Order date range:
          </span>
          <div className="flex items-center gap-2">
            <label className="text-3xs uppercase text-text-faint">From:</label>
            <input
              type="date"
              value={filters.dateFrom}
              onChange={(event) => onChange({ dateFrom: event.target.value })}
              className="rounded-lg border border-line-strong/70 bg-ink-950 px-2.5 py-1 font-mono text-xs text-text-primary focus:border-brand-400 focus:outline-none"
            />
          </div>
          <div className="flex items-center gap-2">
            <label className="text-3xs uppercase text-text-faint">To:</label>
            <input
              type="date"
              value={filters.dateTo}
              onChange={(event) => onChange({ dateTo: event.target.value })}
              className="rounded-lg border border-line-strong/70 bg-ink-950 px-2.5 py-1 font-mono text-xs text-text-primary focus:border-brand-400 focus:outline-none"
            />
          </div>
        </div>
      )}

      {/* Row 2: the dropdowns */}
      <div className="mt-4 grid grid-cols-2 gap-2.5 border-t border-line/60 pt-4 sm:grid-cols-4 lg:grid-cols-7">
        <div>
          <label className="mb-1 block text-3xs font-semibold uppercase tracking-wider text-text-faint">
            Delivery status
          </label>
          <select
            value={filters.deliveryStatus}
            onChange={(event) =>
              onChange({ deliveryStatus: event.target.value as DeliveryLifecycle | "" })
            }
            className="w-full truncate rounded-lg border border-line-strong/70 bg-ink-950 px-2 py-1.5 text-xs text-text-primary focus:border-brand-400 focus:outline-none"
          >
            <option value="">All stages</option>
            {LIFECYCLE_CHOICES.map((lifecycle) => (
              <option key={lifecycle} value={lifecycle}>
                {lifecycleLabel(lifecycle)}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="mb-1 block text-3xs font-semibold uppercase tracking-wider text-text-faint">
            Attached File
          </label>
          <select
            value={filters.sourceFile || ""}
            onChange={(event) => onChange({ sourceFile: event.target.value })}
            className="w-full truncate rounded-lg border border-line-strong/70 bg-ink-950 px-2 py-1.5 text-xs text-text-primary focus:border-brand-400 focus:outline-none"
            title="Scope both the records and the delivery ledger to one workbook"
          >
            <option value="">
              Current File (Auto)
              {filterOptions.currentSourceFile
                ? ` (${getDisplayFileName(filterOptions.currentSourceFile)})`
                : ""}
            </option>
            <option value="all">All Attached Files</option>
            {filters.sourceFile &&
              filters.sourceFile !== "all" &&
              !filterOptions.sourceFiles?.includes(filters.sourceFile) && (
                <option value={filters.sourceFile}>
                  {getDisplayFileName(filters.sourceFile)}
                </option>
              )}
            {filterOptions.sourceFiles?.map((file) => (
              <option key={file} value={file}>
                {getDisplayFileName(file)}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="mb-1 block text-3xs font-semibold uppercase tracking-wider text-text-faint">
            Company
          </label>
          <select
            value={filters.company}
            onChange={(event) => onChange({ company: event.target.value })}
            className="w-full truncate rounded-lg border border-line-strong/70 bg-ink-950 px-2 py-1.5 text-xs text-text-primary focus:border-brand-400 focus:outline-none"
          >
            <option value="">All Companies</option>
            {filters.company && !filterOptions.companies.includes(filters.company) && (
              <option value={filters.company}>{filters.company}</option>
            )}
            {filterOptions.companies.map((company) => (
              <option key={company} value={company}>
                {company}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="mb-1 block text-3xs font-semibold uppercase tracking-wider text-text-faint">
            Salesman
          </label>
          <select
            value={filters.salesman}
            onChange={(event) => onChange({ salesman: event.target.value })}
            className="w-full truncate rounded-lg border border-line-strong/70 bg-ink-950 px-2 py-1.5 text-xs text-text-primary focus:border-brand-400 focus:outline-none"
          >
            <option value="">All Salesmen</option>
            {filters.salesman && !filterOptions.salesmen.includes(filters.salesman) && (
              <option value={filters.salesman}>{filters.salesman}</option>
            )}
            {filterOptions.salesmen.map((salesman) => (
              <option key={salesman} value={salesman}>
                {salesman}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="mb-1 block text-3xs font-semibold uppercase tracking-wider text-text-faint">
            Prepared By
          </label>
          <select
            value={filters.preparedBy}
            onChange={(event) => onChange({ preparedBy: event.target.value })}
            className="w-full truncate rounded-lg border border-line-strong/70 bg-ink-950 px-2 py-1.5 text-xs text-text-primary focus:border-brand-400 focus:outline-none"
          >
            <option value="">All Prepared By</option>
            {filters.preparedBy && !filterOptions.preparedBy.includes(filters.preparedBy) && (
              <option value={filters.preparedBy}>{filters.preparedBy}</option>
            )}
            {filterOptions.preparedBy.map((prepared) => (
              <option key={prepared} value={prepared}>
                {prepared}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="mb-1 block text-3xs font-semibold uppercase tracking-wider text-text-faint">
            Type of Work
          </label>
          <select
            value={filters.typeOfWork}
            onChange={(event) => onChange({ typeOfWork: event.target.value })}
            className="w-full truncate rounded-lg border border-line-strong/70 bg-ink-950 px-2 py-1.5 text-xs text-text-primary focus:border-brand-400 focus:outline-none"
          >
            <option value="">All Types of Work</option>
            {filters.typeOfWork && !filterOptions.typesOfWork.includes(filters.typeOfWork) && (
              <option value={filters.typeOfWork}>{filters.typeOfWork}</option>
            )}
            {filterOptions.typesOfWork.map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="mb-1 block text-3xs font-semibold uppercase tracking-wider text-text-faint">
            PI Status
          </label>
          <select
            value={filters.piStatus}
            onChange={(event) => onChange({ piStatus: event.target.value })}
            className="w-full truncate rounded-lg border border-line-strong/70 bg-ink-950 px-2 py-1.5 text-xs text-text-primary focus:border-brand-400 focus:outline-none"
          >
            <option value="">All PI Statuses</option>
            {filters.piStatus && !filterOptions.piStatuses.includes(filters.piStatus) && (
              <option value={filters.piStatus}>{filters.piStatus}</option>
            )}
            {filterOptions.piStatuses.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Row 3: outstanding-balance switch + active filter pills */}
      <div className="mt-3.5 flex flex-wrap items-center justify-between gap-2.5 border-t border-line/40 pt-3 text-xs">
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => onChange({ balanceOnly: !filters.balanceOnly })}
            aria-pressed={filters.balanceOnly}
            className={`inline-flex items-center gap-2 rounded-lg border px-2.5 py-1.5 font-mono text-2xs font-medium transition-all ${
              filters.balanceOnly
                ? "border-state-warning/60 bg-state-warning/15 text-state-warning"
                : "border-line-strong/60 bg-ink-850/60 text-text-muted hover:border-brand-400/40 hover:text-text-primary"
            }`}
          >
            <span
              className={`h-1.5 w-1.5 rounded-full ${
                filters.balanceOnly ? "bg-state-warning" : "bg-line-strong"
              }`}
            />
            Only outstanding balance
          </button>

          {totalFiltered > 0 && (
            <span className="text-3xs font-mono text-text-faint">
              Matches: <strong className="text-text-primary">{totalFiltered.toLocaleString()}</strong>{" "}
              confirmed {totalFiltered === 1 ? "PI" : "PIs"}
            </span>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-1.5">
          {filters.pi && (
            <span className="inline-flex items-center gap-1 rounded-md border border-brand-400/40 bg-brand-500/10 px-2 py-0.5 text-2xs text-brand-200">
              PI: <strong className="font-mono">{filters.pi}</strong>
              <button
                type="button"
                onClick={() => onChange({ pi: "" })}
                className="text-brand-300 hover:text-state-danger"
              >
                ×
              </button>
            </span>
          )}

          {filters.company && (
            <span className="inline-flex items-center gap-1 rounded-md border border-line-strong bg-ink-850 px-2 py-0.5 text-2xs text-text-secondary">
              Company: {filters.company}
              <button
                type="button"
                onClick={() => onChange({ company: "" })}
                className="text-text-faint hover:text-state-danger"
              >
                ×
              </button>
            </span>
          )}

          {filters.salesman && (
            <span className="inline-flex items-center gap-1 rounded-md border border-line-strong bg-ink-850 px-2 py-0.5 text-2xs text-text-secondary">
              Salesman: {filters.salesman}
              <button
                type="button"
                onClick={() => onChange({ salesman: "" })}
                className="text-text-faint hover:text-state-danger"
              >
                ×
              </button>
            </span>
          )}

          {filters.preparedBy && (
            <span className="inline-flex items-center gap-1 rounded-md border border-line-strong bg-ink-850 px-2 py-0.5 text-2xs text-text-secondary">
              Prepared By: {filters.preparedBy}
              <button
                type="button"
                onClick={() => onChange({ preparedBy: "" })}
                className="text-text-faint hover:text-state-danger"
              >
                ×
              </button>
            </span>
          )}

          {filters.typeOfWork && (
            <span className="inline-flex items-center gap-1 rounded-md border border-line-strong bg-ink-850 px-2 py-0.5 text-2xs text-text-secondary">
              Work: {filters.typeOfWork}
              <button
                type="button"
                onClick={() => onChange({ typeOfWork: "" })}
                className="text-text-faint hover:text-state-danger"
              >
                ×
              </button>
            </span>
          )}

          {filters.piStatus && (
            <span className="inline-flex items-center gap-1 rounded-md border border-line-strong bg-ink-850 px-2 py-0.5 text-2xs text-text-secondary">
              PI Status: {filters.piStatus}
              <button
                type="button"
                onClick={() => onChange({ piStatus: "" })}
                className="text-text-faint hover:text-state-danger"
              >
                ×
              </button>
            </span>
          )}

          {filters.deliveryStatus && (
            <span className="inline-flex items-center gap-1 rounded-md border border-line-strong bg-ink-850 px-2 py-0.5 text-2xs text-text-secondary">
              Stage: {lifecycleLabel(filters.deliveryStatus)}
              <button
                type="button"
                onClick={() => onChange({ deliveryStatus: "" })}
                className="text-text-faint hover:text-state-danger"
              >
                ×
              </button>
            </span>
          )}

          {filters.sourceFile && (
            <span className="inline-flex items-center gap-1 rounded-md border border-line-strong bg-ink-850 px-2 py-0.5 text-2xs text-text-secondary">
              File:{" "}
              {filters.sourceFile === "all" ? "All Files" : getDisplayFileName(filters.sourceFile)}
              <button
                type="button"
                onClick={() => onChange({ sourceFile: "" })}
                className="text-text-faint hover:text-state-danger"
              >
                ×
              </button>
            </span>
          )}

          {activeFilterCount > 0 && (
            <button
              type="button"
              onClick={onReset}
              className="rounded-md border border-line-strong/70 bg-ink-850 px-2 py-0.5 text-2xs font-semibold text-text-muted transition-colors hover:border-state-danger/50 hover:text-state-danger"
            >
              Clear all ({activeFilterCount})
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
