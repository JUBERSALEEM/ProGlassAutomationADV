"use client";

import { useEffect, useState } from "react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { LocaleLink } from "@/components/ui/LocaleLink";
import type { DeliveryRow } from "@/types/deliveries";
import { DeliveryProgress, DeliveryStatusBadge } from "./DeliveryAtoms";
import { MovementLedger } from "./DeliveryMovementLedger";
import { reportsHrefForPi } from "@/lib/delivery-links";

/**
 * The single-PI view: the obligation, the ledger, and the reconciliation.
 *
 * Everything an operator needs to answer a phone call about one proforma is on
 * this panel — how much was ordered, how much has gone, what is left, which
 * note carried which pieces — plus a link back to the exact rows in Reports
 * that the obligation was summed from.
 */
export function DeliveriesDetailModal({
  row,
  onClose,
}: {
  row: DeliveryRow | null;
  onClose: () => void;
}) {
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onClose]);

  if (!row) return null;

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(
        JSON.stringify(
          {
            pi_number: row.pi_number,
            company: row.company,
            ordered_qty: row.ordered_qty,
            delivered_qty: row.delivered_qty,
            balance_qty: row.balance_qty,
            ordered_sqm: row.ordered_sqm,
            delivered_sqm: row.delivered_sqm,
            balance_sqm: row.balance_sqm,
            delivery_status: row.delivery_status,
            movements: row.movements,
          },
          null,
          2,
        ),
      );
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard unavailable — the button simply does nothing visible.
    }
  };

  return (
    <div className="animate-fade-in fixed inset-0 z-50 flex items-center justify-center bg-ink-950/80 p-4 backdrop-blur-sm">
      <div
        className="relative max-h-[92vh] w-full max-w-4xl overflow-y-auto rounded-2xl border border-line-strong/80 bg-ink-900 p-6 shadow-raised"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="flex flex-wrap items-start justify-between gap-3 border-b border-line-strong/60 pb-4">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <span className="eyebrow text-brand-300">PI Delivery Ledger</span>
              <DeliveryStatusBadge lifecycle={row.delivery_status} />
              {row.tracked_without_confirmed_status && (
                <Badge tone="warning" className="text-3xs font-mono">
                  No longer marked Confirmed
                </Badge>
              )}
            </div>
            <h2 className="mt-1 font-mono text-lg font-bold text-text-primary sm:text-xl">
              {row.pi_number}
            </h2>
            <p className="mt-0.5 text-xs text-text-muted">
              Client: <strong className="text-text-primary">{row.company || "N/A"}</strong> · Customer
              Ref: <strong className="text-text-primary">{row.customer_ref || "N/A"}</strong>
            </p>
          </div>

          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-line-strong p-1.5 text-text-faint hover:bg-ink-850 hover:text-text-primary"
            aria-label="Close details"
          >
            ✕
          </button>
        </div>

        {/* Obligation vs delivered vs balance, stated once and plainly. */}
        <div className="mt-5 grid grid-cols-2 gap-3.5 sm:grid-cols-4">
          <Metric label="Ordered" value={`${row.ordered_qty.toLocaleString()} pcs`} sub={`${row.ordered_sqm.toFixed(2)} m²`} />
          <Metric
            label="Delivered"
            value={`${row.delivered_qty.toLocaleString()} pcs`}
            sub={`${row.delivered_sqm.toFixed(2)} m²`}
            tone="text-brand-300"
          />
          <Metric
            label="Balance"
            value={`${row.balance_qty.toLocaleString()} pcs`}
            sub={`${row.balance_sqm.toFixed(2)} m²`}
            tone={row.balance_qty > 0 ? "text-state-warning" : "text-state-success"}
          />
          <Metric
            label="Loads"
            value={row.delivery_count.toLocaleString()}
            sub={`${row.total_movement_count} on the ledger`}
          />
        </div>

        <div className="mt-4 rounded-xl border border-line-strong/50 bg-ink-950/50 px-4 py-3">
          <DeliveryProgress progressPct={row.progress_pct} lifecycle={row.delivery_status} />
        </div>

        <div className="mt-5 grid grid-cols-1 gap-3 text-xs sm:grid-cols-3">
          <DetailField label="Salesman" value={row.salesman || "—"} />
          <DetailField label="Prepared By" value={row.prepared_by || "—"} />
          <DetailField label="Type of Work" value={row.type_of_work || "—"} />
          <DetailField label="Order Date" value={row.order_date || "—"} mono />
          <DetailField label="Latest Order Date" value={row.latest_order_date || "—"} mono />
          <DetailField label="Last Delivery" value={row.last_delivery_date ?? "—"} mono />
          <DetailField label="PI Status (workbook)" value={row.pi_status || "—"} />
          <DetailField label="Record Status" value={row.record_status || "—"} />
          <DetailField label="Zoho" value={row.zoho_status || "—"} />
        </div>

        <div className="mt-5">
          <MovementLedger row={row} />
        </div>

        <div className="mt-5 border-t border-line-strong/60 pt-4">
          <span className="eyebrow text-text-faint">Provenance</span>
          <div className="mt-2 grid grid-cols-1 gap-3 text-xs sm:grid-cols-3">
            <DetailField label="Source File" value={row.source_file || "—"} mono />
            <DetailField
              label="Records Rows Summed"
              value={`${row.record_row_count} row${row.record_row_count === 1 ? "" : "s"}`}
              mono
            />
            <DetailField
              label="Days Since Last Movement"
              value={row.days_since_last_delivery === null ? "—" : String(row.days_since_last_delivery)}
              mono
            />
          </div>
        </div>

        <div className="mt-6 flex flex-wrap items-center justify-between gap-3 border-t border-line-strong/60 pt-4">
          <Button variant="secondary" size="sm" onClick={handleCopy}>
            {copied ? "✓ Copied JSON" : "Copy Raw JSON"}
          </Button>

          <div className="flex flex-wrap items-center gap-2">
            <LocaleLink
              href={reportsHrefForPi(row.pi_number, { sourceFile: row.source_file ?? "all" })}
              className="inline-flex h-9 items-center justify-center rounded-xl border border-line-strong/80 bg-ink-850/80 px-4 text-xs font-semibold text-text-muted transition-colors hover:border-glass-teal hover:text-glass-teal"
            >
              View source rows in Reports →
            </LocaleLink>
            <Button variant="primary" size="sm" onClick={onClose}>
              Done
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}

function Metric({
  label,
  value,
  sub,
  tone = "text-text-primary",
}: {
  label: string;
  value: string;
  sub: string;
  tone?: string;
}) {
  return (
    <div className="rounded-xl border border-line-strong/50 bg-ink-950/60 px-3.5 py-3">
      <span className="block text-3xs font-semibold uppercase tracking-wider text-text-faint">
        {label}
      </span>
      <span className={`mt-1 block font-mono text-base font-bold ${tone}`}>{value}</span>
      <span className="block text-3xs text-text-muted">{sub}</span>
    </div>
  );
}

function DetailField({
  label,
  value,
  mono = false,
}: {
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div>
      <span className="mb-0.5 block text-3xs font-semibold uppercase tracking-wider text-text-faint">
        {label}
      </span>
      <span className={`block truncate text-xs text-text-primary ${mono ? "font-mono" : ""}`}>
        {value}
      </span>
    </div>
  );
}
