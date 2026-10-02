"use client";

import { useEffect, useState, useTransition } from "react";
import type { DatePreset, FilterOptions, RecordFilters } from "@/types/records";

type RecordsFilterPanelProps = {
  filters: RecordFilters;
  filterOptions: FilterOptions;
  onChange: (updated: Partial<RecordFilters>) => void;
  onReset: () => void;
  totalFiltered: number;
  loading: boolean;
};

const DATE_PRESETS: { id: DatePreset; label: string }[] = [
  { id: "all", label: "All Time" },
  { id: "today", label: "Today" },
  { id: "yesterday", label: "Yesterday" },
  { id: "this_week", label: "This Week" },
  { id: "this_month", label: "This Month" },
  { id: "previous_month", label: "Prev Month" },
  { id: "custom", label: "Custom Range" },
];

export function RecordsFilterPanel({
  filters,
  filterOptions,
  onChange,
  onReset,
  totalFiltered,
  loading: _loading,
}: RecordsFilterPanelProps) {
  const [, startTransition] = useTransition();
  const [searchInput, setSearchInput] = useState(filters.search);
  const [prevFilterSearch, setPrevFilterSearch] = useState(filters.search);

  // Sync internal search state if outer filters change (React pattern for prop sync)
  if (filters.search !== prevFilterSearch) {
    setPrevFilterSearch(filters.search);
    setSearchInput(filters.search);
  }

  // Debounced search (350ms)
  useEffect(() => {
    const handler = setTimeout(() => {
      if (searchInput !== filters.search) {
        startTransition(() => {
          onChange({ search: searchInput });
        });
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
    Boolean(filters.status),
    Boolean(filters.piStatus),
    Boolean(filters.typeOfWork),
    Boolean(filters.zohoStatus),
  ].filter(Boolean).length;

  return (
    <div className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-4 sm:p-5 shadow-panel">
      {/* Top row: Search input + Date Presets */}
      <div className="flex flex-col gap-3.5 lg:flex-row lg:items-center lg:justify-between">
        {/* Search Field */}
        <div className="relative flex-1 min-w-[280px]">
          <span className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-text-faint">
            <svg aria-hidden="true" className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
          </span>
          <input
            type="search"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Search PI Number, Customer Ref, Company..."
            className="w-full rounded-xl border border-line-strong/70 bg-ink-950/80 py-2.5 pl-10 pr-9 text-xs sm:text-sm text-text-primary placeholder:text-text-faint/70 focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400"
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

        {/* Date presets pill strip */}
        <div className="flex flex-wrap items-center gap-1.5 overflow-x-auto pb-1 sm:pb-0">
          <span className="text-3xs font-semibold uppercase tracking-wider text-text-faint mr-1 hidden xl:inline-block">
            Date:
          </span>
          {DATE_PRESETS.map((p) => {
            const isSelected = filters.datePreset === p.id;
            return (
              <button
                key={p.id}
                type="button"
                onClick={() => {
                  onChange({ datePreset: p.id });
                }}
                className={`rounded-lg px-2.5 py-1.5 font-mono text-2xs font-medium transition-all ${
                  isSelected
                    ? "bg-brand-500/25 border border-brand-400 text-brand-200 shadow-sm"
                    : "border border-line-strong/60 bg-ink-850/60 text-text-muted hover:border-brand-400/40 hover:text-text-primary"
                }`}
              >
                {p.label}
              </button>
            );
          })}
        </div>
      </div>

      {/* Custom Date Inputs (when custom range is active or toggled) */}
      {filters.datePreset === "custom" && (
        <div className="mt-3.5 flex flex-wrap items-center gap-3 border-t border-line/60 pt-3.5 text-xs text-text-muted">
          <span className="font-mono text-2xs uppercase text-text-faint">Custom Date Range:</span>
          <div className="flex items-center gap-2">
            <label className="text-3xs uppercase text-text-faint">From:</label>
            <input
              type="date"
              value={filters.dateFrom}
              onChange={(e) => onChange({ dateFrom: e.target.value })}
              className="rounded-lg border border-line-strong/70 bg-ink-950 px-2.5 py-1 font-mono text-xs text-text-primary focus:border-brand-400 focus:outline-none"
            />
          </div>
          <div className="flex items-center gap-2">
            <label className="text-3xs uppercase text-text-faint">To:</label>
            <input
              type="date"
              value={filters.dateTo}
              onChange={(e) => onChange({ dateTo: e.target.value })}
              className="rounded-lg border border-line-strong/70 bg-ink-950 px-2.5 py-1 font-mono text-xs text-text-primary focus:border-brand-400 focus:outline-none"
            />
          </div>
        </div>
      )}

      {/* Filter dropdowns row */}
      <div className="mt-4 grid grid-cols-2 gap-2.5 sm:grid-cols-3 lg:grid-cols-7 border-t border-line/60 pt-4">
        {/* Company Dropdown */}
        <div>
          <label className="block text-3xs font-semibold uppercase tracking-wider text-text-faint mb-1">
            Company
          </label>
          <select
            value={filters.company}
            onChange={(e) => onChange({ company: e.target.value })}
            className="w-full rounded-lg border border-line-strong/70 bg-ink-950 py-1.5 px-2 text-xs text-text-primary focus:border-brand-400 focus:outline-none truncate"
          >
            <option value="">All Companies</option>
            {filterOptions.companies.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </div>

        {/* Salesman Dropdown */}
        <div>
          <label className="block text-3xs font-semibold uppercase tracking-wider text-text-faint mb-1">
            Salesman
          </label>
          <select
            value={filters.salesman}
            onChange={(e) => onChange({ salesman: e.target.value })}
            className="w-full rounded-lg border border-line-strong/70 bg-ink-950 py-1.5 px-2 text-xs text-text-primary focus:border-brand-400 focus:outline-none truncate"
          >
            <option value="">All Salesmen</option>
            {filterOptions.salesmen.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </div>

        {/* Prepared By Dropdown */}
        <div>
          <label className="block text-3xs font-semibold uppercase tracking-wider text-text-faint mb-1">
            Prepared By
          </label>
          <select
            value={filters.preparedBy}
            onChange={(e) => onChange({ preparedBy: e.target.value })}
            className="w-full rounded-lg border border-line-strong/70 bg-ink-950 py-1.5 px-2 text-xs text-text-primary focus:border-brand-400 focus:outline-none truncate"
          >
            <option value="">All Authors</option>
            {filterOptions.preparedBy.map((p) => (
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>
        </div>

        {/* Status Dropdown */}
        <div>
          <label className="block text-3xs font-semibold uppercase tracking-wider text-text-faint mb-1">
            Status
          </label>
          <select
            value={filters.status}
            onChange={(e) => onChange({ status: e.target.value })}
            className="w-full rounded-lg border border-line-strong/70 bg-ink-950 py-1.5 px-2 text-xs text-text-primary focus:border-brand-400 focus:outline-none truncate"
          >
            <option value="">All Statuses</option>
            {filterOptions.statuses.map((st) => (
              <option key={st} value={st}>
                {st}
              </option>
            ))}
          </select>
        </div>

        {/* PI Status Dropdown */}
        <div>
          <label className="block text-3xs font-semibold uppercase tracking-wider text-text-faint mb-1">
            PI Status
          </label>
          <select
            value={filters.piStatus}
            onChange={(e) => onChange({ piStatus: e.target.value })}
            className="w-full rounded-lg border border-line-strong/70 bg-ink-950 py-1.5 px-2 text-xs text-text-primary focus:border-brand-400 focus:outline-none truncate"
          >
            <option value="">All PI Statuses</option>
            {filterOptions.piStatuses.map((pi) => (
              <option key={pi} value={pi}>
                {pi}
              </option>
            ))}
          </select>
        </div>

        {/* Type of Work Dropdown */}
        <div>
          <label className="block text-3xs font-semibold uppercase tracking-wider text-text-faint mb-1">
            Type of Work
          </label>
          <select
            value={filters.typeOfWork}
            onChange={(e) => onChange({ typeOfWork: e.target.value })}
            className="w-full rounded-lg border border-line-strong/70 bg-ink-950 py-1.5 px-2 text-xs text-text-primary focus:border-brand-400 focus:outline-none truncate"
          >
            <option value="">All Work Types</option>
            {filterOptions.typesOfWork.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </select>
        </div>

        {/* Zoho Status Dropdown */}
        <div>
          <label className="block text-3xs font-semibold uppercase tracking-wider text-text-faint mb-1">
            Zoho Status
          </label>
          <select
            value={filters.zohoStatus}
            onChange={(e) => onChange({ zohoStatus: e.target.value })}
            className="w-full rounded-lg border border-line-strong/70 bg-ink-950 py-1.5 px-2 text-xs text-text-primary focus:border-brand-400 focus:outline-none truncate"
          >
            <option value="">All Zoho Status</option>
            {filterOptions.zohoStatuses.map((z) => (
              <option key={z} value={z}>
                {z}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Active filters pill bar + Clear button */}
      <div className="mt-3.5 flex flex-wrap items-center justify-between gap-2.5 pt-3 border-t border-line/40 text-xs">
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="text-3xs uppercase font-mono text-text-faint">
            Matches: <strong className="text-text-primary">{totalFiltered.toLocaleString()}</strong> rows
          </span>

          {activeFilterCount > 0 && (
            <span className="text-text-faint text-3xs">·</span>
          )}

          {filters.search && (
            <span className="inline-flex items-center gap-1 rounded-md border border-line-strong bg-ink-850 px-2 py-0.5 text-2xs text-text-secondary">
              Search: &quot;{filters.search}&quot;
              <button
                type="button"
                onClick={() => onChange({ search: "" })}
                className="hover:text-state-danger text-text-faint"
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
                className="hover:text-state-danger text-text-faint"
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
                className="hover:text-state-danger text-text-faint"
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
                className="hover:text-state-danger text-text-faint"
              >
                ×
              </button>
            </span>
          )}

          {filters.status && (
            <span className="inline-flex items-center gap-1 rounded-md border border-line-strong bg-ink-850 px-2 py-0.5 text-2xs text-text-secondary">
              Status: {filters.status}
              <button
                type="button"
                onClick={() => onChange({ status: "" })}
                className="hover:text-state-danger text-text-faint"
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
                className="hover:text-state-danger text-text-faint"
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
                className="hover:text-state-danger text-text-faint"
              >
                ×
              </button>
            </span>
          )}

          {filters.zohoStatus && (
            <span className="inline-flex items-center gap-1 rounded-md border border-line-strong bg-ink-850 px-2 py-0.5 text-2xs text-text-secondary">
              Zoho: {filters.zohoStatus}
              <button
                type="button"
                onClick={() => onChange({ zohoStatus: "" })}
                className="hover:text-state-danger text-text-faint"
              >
                ×
              </button>
            </span>
          )}
        </div>

        {activeFilterCount > 0 && (
          <button
            type="button"
            onClick={onReset}
            className="text-2xs font-semibold text-brand-300 hover:text-brand-200 uppercase tracking-wider underline underline-offset-4 shrink-0"
          >
            Clear All Filters ({activeFilterCount})
          </button>
        )}
      </div>
    </div>
  );
}
