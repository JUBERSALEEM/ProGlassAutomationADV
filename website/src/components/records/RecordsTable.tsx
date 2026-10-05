"use client";

import { memo } from "react";
import type { RecordRow, RecordSort, RecordSortField } from "@/types/records";
import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { formatDateDDMMYYYY } from "@/lib/date-utils";

type RecordsTableProps = {
  records: RecordRow[];
  loading: boolean;
  sort: RecordSort;
  onSortChange: (field: RecordSortField) => void;
  onSelectRecord: (record: RecordRow) => void;
  density: "comfortable" | "compact";
  onResetFilters?: () => void;
};

export function RecordsTable({
  records,
  loading,
  sort,
  onSortChange,
  onSelectRecord,
  density,
  onResetFilters,
}: RecordsTableProps) {
  const isCompact = density === "compact";
  const cellPy = isCompact ? "py-2" : "py-3.5";

  if (loading && records.length === 0) {
    return (
      <div className="overflow-hidden rounded-2xl border border-line-strong/60 bg-ink-900/80 p-4 shadow-panel">
        <div className="space-y-2.5">
          {Array.from({ length: 8 }).map((_, i) => (
            <div
              key={i}
              className="h-10 w-full animate-pulse rounded-lg bg-ink-850/60 border border-line-strong/40"
            />
          ))}
        </div>
      </div>
    );
  }

  if (records.length === 0) {
    return (
      <div className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-12 text-center shadow-panel">
        <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl border border-line-strong bg-ink-850 text-text-faint">
          <svg aria-hidden="true" className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="1.5" d="M9.172 16.172a4 4 0 015.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
        </div>
        <h3 className="mt-4 text-base font-bold text-text-primary">No records found</h3>
        <p className="mt-1.5 text-xs text-text-muted max-w-sm mx-auto">
          No operations match your active filter criteria or date range. Try broadening your search or resetting filters.
        </p>
        {onResetFilters && (
          <div className="mt-5">
            <button
              type="button"
              onClick={onResetFilters}
              className="rounded-xl border border-line-strong bg-ink-850 px-4 py-2 text-xs font-semibold text-brand-300 hover:bg-ink-800 hover:text-brand-200 transition-colors"
            >
              Reset All Filters
            </button>
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="relative overflow-hidden rounded-2xl border border-line-strong/60 bg-ink-900/90 shadow-panel">
      {/* Scrollable table container */}
      <div className="overflow-x-auto">
        <table className="w-full text-left text-xs border-collapse">
          <thead>
            <tr className="border-b border-line-strong/80 bg-ink-950/90 text-3xs font-semibold uppercase tracking-wider text-text-faint select-none">
              <SortHeader label="Date" field="date" currentSort={sort} onSort={onSortChange} className="pl-4" />
              <SortHeader label="PI Number" field="pi_number" currentSort={sort} onSort={onSortChange} />
              <th className="px-3 py-3 font-semibold">Customer Ref</th>
              <SortHeader label="Company" field="company" currentSort={sort} onSort={onSortChange} />
              <SortHeader label="Salesman" field="salesman" currentSort={sort} onSort={onSortChange} />
              <SortHeader label="Prepared By" field="prepared_by" currentSort={sort} onSort={onSortChange} />
              <th className="px-3 py-3 font-semibold">Type of Work</th>
              <SortHeader label="Qty" field="qty" currentSort={sort} onSort={onSortChange} align="right" />
              <SortHeader label="SQM" field="sqm" currentSort={sort} onSort={onSortChange} align="right" />
              <SortHeader label="Status" field="status" currentSort={sort} onSort={onSortChange} />
              <th className="px-3 py-3 font-semibold">PI Status</th>
              <th className="px-3 py-3 font-semibold">Zoho</th>
              <th className="px-4 py-3 font-semibold text-right pr-4">Details</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line/40">
            {records.map((record) => (
              <TableRow
                key={record.id}
                record={record}
                cellPy={cellPy}
                onSelect={() => onSelectRecord(record)}
              />
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

const TableRow = memo(function TableRow({
  record,
  cellPy,
  onSelect,
}: {
  record: RecordRow;
  cellPy: string;
  onSelect: () => void;
}) {
  const isConfirmed = record.status.toLocaleLowerCase() === "confirmed";

  return (
    <tr
      onClick={onSelect}
      className="group transition-colors hover:bg-ink-850/80 cursor-pointer"
    >
      <td className={`pl-4 pr-3 ${cellPy} font-mono text-2xs text-text-muted whitespace-nowrap`}>
        {formatDateDDMMYYYY(record.date)}
      </td>
      <td className={`px-3 ${cellPy} font-mono text-xs font-semibold whitespace-nowrap`}>
        {isConfirmed ? (
          <LocaleLink
            href={`/job-orders?search=${encodeURIComponent(record.pi_number)}`}
            title={`Open confirmed Job Order hand-off for ${record.pi_number}`}
            onClick={(event) => event.stopPropagation()}
            className="text-brand-300 underline-offset-2 transition-colors hover:text-brand-200 hover:underline"
          >
            {record.pi_number}
          </LocaleLink>
        ) : (
          <span title="Only proformas marked Confirmed enter the Job Order queue" className="text-text-secondary">
            {record.pi_number}
          </span>
        )}
      </td>
      <td className={`px-3 ${cellPy} font-mono text-3xs text-text-faint truncate max-w-[120px]`}>
        {record.customer_ref || "—"}
      </td>
      <td className={`px-3 ${cellPy} font-medium text-text-primary truncate max-w-[170px]`}>
        {record.company}
      </td>
      <td className={`px-3 ${cellPy} text-text-secondary whitespace-nowrap`}>
        {record.salesman}
      </td>
      <td className={`px-3 ${cellPy} text-text-muted whitespace-nowrap`}>
        {record.prepared_by}
      </td>
      <td className={`px-3 ${cellPy} whitespace-nowrap`}>
        <span className="font-mono text-3xs rounded bg-ink-800 px-1.5 py-0.5 text-text-secondary">
          {record.type_of_work}
        </span>
      </td>
      <td className={`px-3 ${cellPy} text-right font-mono text-xs font-semibold text-text-primary whitespace-nowrap`}>
        {(Number(record.qty) || 0).toLocaleString()}
      </td>
      <td className={`px-3 ${cellPy} text-right font-mono text-xs font-semibold text-glass-teal whitespace-nowrap`}>
        {(Number(record.sqm) || 0).toFixed(2)}
      </td>
      <td className={`px-3 ${cellPy} whitespace-nowrap`}>
        <StatusBadge status={record.status} />
      </td>
      <td className={`px-3 ${cellPy} whitespace-nowrap`}>
        <PiStatusBadge status={record.pi_status} />
      </td>
      <td className={`px-3 ${cellPy} whitespace-nowrap`}>
        <ZohoBadge status={record.zoho_status} />
      </td>
      <td className={`pl-3 pr-4 ${cellPy} text-right whitespace-nowrap`}>
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            onSelect();
          }}
          className="rounded-md border border-line-strong px-2 py-1 text-3xs font-semibold text-text-faint hover:border-brand-400 hover:text-brand-200 transition-colors"
        >
          Inspect
        </button>
      </td>
    </tr>
  );
});

function SortHeader({
  label,
  field,
  currentSort,
  onSort,
  align = "left",
  className = "",
}: {
  label: string;
  field: RecordSortField;
  currentSort: RecordSort;
  onSort: (field: RecordSortField) => void;
  align?: "left" | "right";
  className?: string;
}) {
  const isSorted = currentSort.field === field;
  const isAsc = currentSort.direction === "asc";

  return (
    <th
      className={`px-3 py-3 cursor-pointer hover:text-text-primary transition-colors ${
        align === "right" ? "text-right" : "text-left"
      } ${className}`}
      onClick={() => onSort(field)}
    >
      <div className={`inline-flex items-center gap-1 ${align === "right" ? "justify-end" : ""}`}>
        <span>{label}</span>
        <span className="font-mono text-3xs">
          {isSorted ? (isAsc ? "▲" : "▼") : "↕"}
        </span>
      </div>
    </th>
  );
}

function StatusBadge({ status }: { status: string }) {
  if (status === "Completed") return <Badge tone="success">{status}</Badge>;
  if (status === "In Production") return <Badge tone="accent">{status}</Badge>;
  if (status === "Confirmed") return <Badge tone="accent">{status}</Badge>;
  if (status === "Pending") return <Badge tone="warning">{status}</Badge>;
  return <Badge tone="neutral">{status}</Badge>;
}

function PiStatusBadge({ status }: { status: string }) {
  if (status === "Approved") return <Badge tone="success">{status}</Badge>;
  if (status === "Sent") return <Badge tone="accent">{status}</Badge>;
  if (status === "Draft") return <Badge tone="warning">{status}</Badge>;
  return <Badge tone="neutral">{status}</Badge>;
}

function ZohoBadge({ status }: { status: string }) {
  if (status === "Synced") {
    return (
      <span className="font-mono text-3xs text-state-success bg-state-success/10 border border-state-success/20 px-1.5 py-0.5 rounded">
        Synced
      </span>
    );
  }
  if (status === "Pending") {
    return (
      <span className="font-mono text-3xs text-state-warning bg-state-warning/10 border border-state-warning/20 px-1.5 py-0.5 rounded">
        Pending
      </span>
    );
  }
  return (
    <span className="font-mono text-3xs text-text-faint bg-ink-850 border border-line-strong px-1.5 py-0.5 rounded">
      {status || "None"}
    </span>
  );
}
