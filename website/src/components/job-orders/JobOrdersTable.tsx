"use client";

import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { formatDateDDMMYYYY } from "@/lib/date-utils";
import type { JobOrderRow, JobOrderSort, JobOrderSortField } from "@/types/job-orders";

type Props = {
  rows: JobOrderRow[];
  loading: boolean;
  sort: JobOrderSort;
  onSortChange: (field: JobOrderSortField) => void;
  onSelect: (row: JobOrderRow) => void;
  onResetFilters?: () => void;
};

export function JobOrdersTable({ rows, loading, sort, onSortChange, onSelect, onResetFilters }: Props) {
  if (loading && rows.length === 0) {
    return (
      <div className="overflow-hidden rounded-2xl border border-line-strong/60 bg-ink-900/80 p-4 shadow-panel">
        <div className="space-y-2.5">
          {Array.from({ length: 7 }).map((_, index) => (
            <div key={index} className="h-10 animate-pulse rounded-lg border border-line-strong/40 bg-ink-850/60" />
          ))}
        </div>
      </div>
    );
  }

  if (rows.length === 0) {
    return (
      <div className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-12 text-center shadow-panel">
        <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl border border-line-strong bg-ink-850 text-brand-300">
          <svg aria-hidden="true" className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="1.5" d="M8 4h8l4 4v12H4V4h4zm0 0v5h8V4m-8 9h8m-8 3h5" />
          </svg>
        </div>
        <h2 className="mt-4 text-base font-bold text-text-primary">No confirmed proformas found</h2>
        <p className="mx-auto mt-1.5 max-w-md text-xs leading-relaxed text-text-muted">
          This queue only includes active PI records marked Confirmed. Clear the filters or confirm a proforma in the source workbook.
        </p>
        {onResetFilters && (
          <button type="button" onClick={onResetFilters} className="mt-5 rounded-xl border border-line-strong bg-ink-850 px-4 py-2 text-xs font-semibold text-brand-300 transition-colors hover:bg-ink-800 hover:text-brand-200">
            Reset filters
          </button>
        )}
      </div>
    );
  }

  return (
    <div className="relative overflow-hidden rounded-2xl border border-line-strong/60 bg-ink-900/90 shadow-panel">
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-left text-xs">
          <thead>
            <tr className="select-none border-b border-line-strong/80 bg-ink-950/90 text-3xs font-semibold uppercase tracking-wider text-text-faint">
              <SortHeader label="Order date" field="order_date" sort={sort} onSortChange={onSortChange} />
              <SortHeader label="Proforma / PI" field="pi_number" sort={sort} onSortChange={onSortChange} />
              <th scope="col" className="px-3 py-3">Customer ref</th>
              <SortHeader label="Company" field="company" sort={sort} onSortChange={onSortChange} />
              <th scope="col" className="px-3 py-3">Type of work</th>
              <SortHeader label="Qty" field="ordered_qty" sort={sort} onSortChange={onSortChange} align="right" />
              <SortHeader label="Area" field="ordered_sqm" sort={sort} onSortChange={onSortChange} align="right" />
              <th scope="col" className="px-3 py-3">PI gate</th>
              <th scope="col" className="px-4 py-3 text-right">Details</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line/40">
            {rows.map((row) => (
              <tr key={row.id} className="group transition-colors hover:bg-ink-850/80">
                <td className="whitespace-nowrap px-4 py-3.5 font-mono text-2xs text-text-muted">{formatDateDDMMYYYY(row.order_date) || "—"}</td>
                <td className="whitespace-nowrap px-3 py-3.5 font-mono text-xs font-semibold">
                  <LocaleLink
                    href={`/reports?search=${encodeURIComponent(row.pi_number)}`}
                    title={`Open ${row.pi_number} in the records dashboard`}
                    className="text-brand-300 underline-offset-2 transition-colors hover:text-brand-200 hover:underline"
                  >
                    {row.pi_number}
                  </LocaleLink>
                  {row.record_count > 1 && <span className="ml-2 text-3xs font-normal text-text-faint">{row.record_count} lines</span>}
                </td>
                <td className="max-w-36 truncate px-3 py-3.5 font-mono text-3xs text-text-faint" title={row.customer_ref}>{row.customer_ref || "—"}</td>
                <td className="max-w-56 truncate px-3 py-3.5 font-medium text-text-primary" title={row.company}>{row.company || "Unassigned"}</td>
                <td className="whitespace-nowrap px-3 py-3.5">
                  {row.type_of_work ? <span className="rounded bg-ink-800 px-1.5 py-0.5 font-mono text-3xs text-text-secondary">{row.type_of_work}</span> : <span className="text-text-faint">—</span>}
                </td>
                <td className="whitespace-nowrap px-3 py-3.5 text-right font-mono text-xs font-semibold text-text-primary">{row.ordered_qty.toLocaleString()}</td>
                <td className="whitespace-nowrap px-3 py-3.5 text-right font-mono text-xs font-semibold text-glass-teal">{row.ordered_sqm.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} m²</td>
                <td className="whitespace-nowrap px-3 py-3.5"><Badge tone="success">{row.confirmation_status}</Badge></td>
                <td className="whitespace-nowrap px-4 py-3.5 text-right">
                  <button type="button" onClick={() => onSelect(row)} className="rounded-md border border-line-strong px-2.5 py-1 text-3xs font-semibold text-text-faint transition-colors hover:border-brand-400 hover:text-brand-200">
                    Inspect
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-line-strong/50 bg-ink-950/50 px-4 py-2.5 text-3xs text-text-faint">
        <span>Each PI appears once; source-line quantities and area are summed.</span>
        <span className="font-mono">Only status = Confirmed</span>
      </div>
    </div>
  );
}

function SortHeader({
  label,
  field,
  sort,
  onSortChange,
  align = "left",
}: {
  label: string;
  field: JobOrderSortField;
  sort: JobOrderSort;
  onSortChange: (field: JobOrderSortField) => void;
  align?: "left" | "right";
}) {
  const active = sort.field === field;
  return (
    <th scope="col" aria-sort={active ? (sort.direction === "asc" ? "ascending" : "descending") : "none"} className={`px-3 py-3 ${align === "right" ? "text-right" : "text-left"}`}>
      <button type="button" onClick={() => onSortChange(field)} className={`inline-flex items-center gap-1 transition-colors hover:text-text-primary ${align === "right" ? "justify-end" : ""}`}>
        <span>{label}</span>
        <span aria-hidden="true" className="font-mono text-3xs">{active ? (sort.direction === "asc" ? "▲" : "▼") : "↕"}</span>
      </button>
    </th>
  );
}
