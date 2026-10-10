"use client";

import { Fragment, memo, useState } from "react";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { EmptyState } from "@/components/ui/EmptyState";
import { footerLinkIcons } from "@/data/footerIcons";
import type { DeliveryRow, DeliverySort, DeliverySortField } from "@/types/deliveries";
import { DeliveryProgress, DeliveryStatusBadge } from "./DeliveryAtoms";
import { MovementLedger } from "./DeliveryMovementLedger";
import { reportsHrefForPi } from "@/lib/delivery-links";

type DeliveriesTableProps = {
  rows: DeliveryRow[];
  loading: boolean;
  sort: DeliverySort;
  onSortChange: (field: DeliverySortField) => void;
  onSelectRow: (row: DeliveryRow) => void;
  density: "comfortable" | "compact";
  onResetFilters?: () => void;
};

/**
 * One row per confirmed PI, with its ledger on demand.
 *
 * The expansion is the page's central idea made physical: a PI is not one
 * delivery, it is a list of them, and the list is one click away rather than a
 * separate page. Rows that need attention — over-delivered, stale, or carrying
 * a balance — say so in the row itself, so the table can be scanned rather
 * than read.
 */
export function DeliveriesTable({
  rows,
  loading,
  sort,
  onSortChange,
  onSelectRow,
  density,
  onResetFilters,
}: DeliveriesTableProps) {
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const isCompact = density === "compact";
  const cellPy = isCompact ? "py-2" : "py-3.5";

  const toggle = (pi: string) => {
    setExpanded((previous) => {
      const next = new Set(previous);
      if (next.has(pi)) next.delete(pi);
      else next.add(pi);
      return next;
    });
  };

  if (loading && rows.length === 0) {
    return (
      <div className="overflow-hidden rounded-2xl border border-line-strong/60 bg-ink-900/80 p-4 shadow-panel">
        <div className="space-y-2.5">
          {Array.from({ length: 8 }).map((_, index) => (
            <div
              key={index}
              className="h-10 w-full animate-pulse rounded-lg border border-line-strong/40 bg-ink-850/60"
            />
          ))}
        </div>
      </div>
    );
  }

  if (rows.length === 0) {
    return (
      <EmptyState
        glyph={footerLinkIcons["/deliveries"]!}
        title="No deliveries to track"
        body="Nothing matches the current filters. The tracker only lists PIs whose status is Confirmed — or PIs that already carry a delivery movement — so a draft proforma will not appear here however it is filtered."
        action={onResetFilters ? (
            <button
              type="button"
              onClick={onResetFilters}
              className="rounded-xl border border-line-strong bg-ink-850 px-4 py-2 text-xs font-semibold text-brand-300 transition-colors hover:bg-ink-800 hover:text-brand-200"
            >
              Reset All Filters
            </button>
          ) : undefined}
      />
    );
  }

  return (
    <div className="relative overflow-hidden rounded-2xl border border-line-strong/60 bg-ink-900/90 shadow-panel">
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-left text-xs">
          <thead>
            <tr className="select-none border-b border-line-strong/80 bg-ink-950/90 text-3xs font-semibold uppercase tracking-wider text-text-faint">
              <th className="w-8 pl-3 py-3" aria-label="Expand" />
              <SortHeader label="PI Number" field="pi_number" currentSort={sort} onSort={onSortChange} />
              <SortHeader label="Company" field="company" currentSort={sort} onSort={onSortChange} />
              <th className="px-3 py-3 font-semibold">Customer Ref</th>
              <SortHeader label="Order Date" field="order_date" currentSort={sort} onSort={onSortChange} />
              <SortHeader label="Ordered" field="ordered_qty" currentSort={sort} onSort={onSortChange} align="right" />
              <SortHeader label="Delivered" field="delivered_qty" currentSort={sort} onSort={onSortChange} align="right" />
              <SortHeader label="Balance" field="balance_qty" currentSort={sort} onSort={onSortChange} align="right" />
              <SortHeader label="Progress" field="progress" currentSort={sort} onSort={onSortChange} />
              <SortHeader label="Loads" field="delivery_count" currentSort={sort} onSort={onSortChange} align="right" />
              <SortHeader
                label="Last Delivery"
                field="last_delivery_date"
                currentSort={sort}
                onSort={onSortChange}
              />
              <SortHeader label="Stage" field="delivery_status" currentSort={sort} onSort={onSortChange} />
              <th className="py-3 pl-3 pr-4 text-right font-semibold">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line/40">
            {rows.map((row) => {
              const isOpen = expanded.has(row.pi_number);
              const isStale =
                (row.delivery_status === "partial" || row.delivery_status === "awaiting_dispatch") &&
                row.days_since_last_delivery !== null &&
                row.days_since_last_delivery >= 14;

              return (
                <Fragment key={row.pi_number}>
                  <tr
                    className={`group transition-colors ${
                      isOpen ? "bg-ink-850/70" : "hover:bg-ink-850/60"
                    }`}
                  >
                    <td className={`pl-3 ${cellPy}`}>
                      <button
                        type="button"
                        onClick={() => toggle(row.pi_number)}
                        aria-expanded={isOpen}
                        aria-label={isOpen ? "Collapse delivery ledger" : "Expand delivery ledger"}
                        className="flex h-6 w-6 items-center justify-center rounded-md border border-line-strong/70 bg-ink-950/60 font-mono text-2xs text-text-muted transition-colors hover:border-brand-400 hover:text-brand-200"
                      >
                        {row.movements.length > 0 ? (isOpen ? "−" : "+") : "·"}
                      </button>
                    </td>

                    <td className={`px-3 ${cellPy} whitespace-nowrap`}>
                      <button
                        type="button"
                        onClick={() => onSelectRow(row)}
                        className="font-mono text-xs font-semibold text-brand-300 transition-colors hover:text-brand-200"
                      >
                        {row.pi_number}
                      </button>
                      {row.tracked_without_confirmed_status && (
                        <span
                          className="ml-2 rounded border border-state-warning/30 bg-state-warning/10 px-1.5 py-0.5 font-mono text-3xs text-state-warning"
                          title="This PI is no longer marked Confirmed, but it has movements — the ledger keeps it visible."
                        >
                          carried
                        </span>
                      )}
                    </td>

                    <td className={`px-3 ${cellPy} max-w-[180px] truncate font-medium text-text-primary`}>
                      {row.company || "—"}
                    </td>
                    <td className={`px-3 ${cellPy} max-w-[120px] truncate font-mono text-3xs text-text-faint`}>
                      {row.customer_ref || "—"}
                    </td>
                    <td className={`px-3 ${cellPy} whitespace-nowrap font-mono text-2xs text-text-muted`}>
                      {row.order_date || "—"}
                    </td>

                    <td className={`px-3 ${cellPy} whitespace-nowrap text-right font-mono text-xs text-text-primary`}>
                      {row.ordered_qty.toLocaleString()}
                      <span className="ml-1 text-3xs text-text-faint">
                        {row.ordered_sqm.toFixed(1)} m²
                      </span>
                    </td>

                    <td className={`px-3 ${cellPy} whitespace-nowrap text-right font-mono text-xs text-brand-300`}>
                      {row.delivered_qty.toLocaleString()}
                      <span className="ml-1 text-3xs text-text-faint">{row.delivered_sqm.toFixed(1)} m²</span>
                    </td>

                    <td
                      className={`px-3 ${cellPy} whitespace-nowrap text-right font-mono text-xs ${
                        row.balance_qty > 0 ? "text-state-warning" : "text-text-faint"
                      }`}
                    >
                      {row.balance_qty.toLocaleString()}
                      <span className="ml-1 text-3xs text-text-faint">{row.balance_sqm.toFixed(1)} m²</span>
                    </td>

                    <td className={`px-3 ${cellPy}`}>
                      <DeliveryProgress
                        progressPct={row.progress_pct}
                        lifecycle={row.delivery_status}
                        compact
                      />
                    </td>

                    <td className={`px-3 ${cellPy} text-right`}>
                      <span className="font-mono text-xs font-semibold text-text-primary">
                        {row.delivery_count}
                      </span>
                      {row.total_movement_count > row.delivery_count && (
                        <span
                          className="ml-1 font-mono text-3xs text-text-faint"
                          title="Movements excluded from the balance: draft or voided notes"
                        >
                          (+{row.total_movement_count - row.delivery_count} excl.)
                        </span>
                      )}
                    </td>

                    <td className={`px-3 ${cellPy} whitespace-nowrap font-mono text-2xs text-text-muted`}>
                      {row.last_delivery_date ?? "—"}
                      {isStale && (
                        <span
                          className="ml-2 rounded border border-state-danger/30 bg-state-danger/10 px-1.5 py-0.5 font-mono text-3xs text-state-danger"
                          title={`No movement for ${row.days_since_last_delivery} days`}
                        >
                          stale
                        </span>
                      )}
                    </td>

                    <td className={`px-3 ${cellPy} whitespace-nowrap`}>
                      <DeliveryStatusBadge lifecycle={row.delivery_status} />
                    </td>

                    <td className={`py-3 pl-3 pr-4 text-right whitespace-nowrap`}>
                      <div className="inline-flex items-center gap-1.5">
                        <button
                          type="button"
                          onClick={() => toggle(row.pi_number)}
                          aria-expanded={isOpen}
                          className="rounded-md border border-line-strong px-2 py-1 text-3xs font-semibold text-text-faint transition-colors hover:border-brand-400 hover:text-brand-200"
                        >
                          {isOpen ? "Hide ledger" : "Ledger"}
                        </button>
                        <LocaleLink
                          href={reportsHrefForPi(row.pi_number, { sourceFile: row.source_file ?? "all" })}
                          className="rounded-md border border-line-strong px-2 py-1 text-3xs font-semibold text-text-faint transition-colors hover:border-glass-teal hover:text-glass-teal"
                          title="Open the source rows for this PI in Reports"
                        >
                          Reports
                        </LocaleLink>
                      </div>
                    </td>
                  </tr>

                  {isOpen && (
                    <tr className="bg-ink-950/60">
                      <td colSpan={13} className="px-3 pb-4 pt-2">
                        <MovementLedger row={row} compact />
                      </td>
                    </tr>
                  )}
                </Fragment>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-line-strong/50 bg-ink-950/70 px-4 py-2.5 text-3xs text-text-faint">
        <span className="font-mono">
          {rows.length.toLocaleString()} row{rows.length === 1 ? "" : "s"} on this page · expand any
          row for its movement ledger
        </span>
        <span className="font-mono">Balance = Σ counted movements − returns, per PI</span>
      </div>
    </div>
  );
}

const SortHeader = memo(function SortHeader({
  label,
  field,
  currentSort,
  onSort,
  align = "left",
}: {
  label: string;
  field: DeliverySortField;
  currentSort: DeliverySort;
  onSort: (field: DeliverySortField) => void;
  align?: "left" | "right";
}) {
  const isSorted = currentSort.field === field;
  const isAsc = currentSort.direction === "asc";

  return (
    <th
      className={`cursor-pointer px-3 py-3 transition-colors hover:text-text-primary ${
        align === "right" ? "text-right" : "text-left"
      }`}
      onClick={() => onSort(field)}
    >
      <div className={`inline-flex items-center gap-1 ${align === "right" ? "justify-end" : ""}`}>
        <span>{label}</span>
        <span className="font-mono text-3xs">{isSorted ? (isAsc ? "▲" : "▼") : "↕"}</span>
      </div>
    </th>
  );
});
