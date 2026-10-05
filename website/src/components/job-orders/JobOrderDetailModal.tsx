"use client";

import { useEffect } from "react";
import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { formatDateDDMMYYYY } from "@/lib/date-utils";
import { confirmedProformaHrefForJobOrder, deliveryNoteHrefForJobOrder } from "@/lib/job-order-links";
import type { JobOrderRow } from "@/types/job-orders";

export function JobOrderDetailModal({ row, onClose }: { row: JobOrderRow | null; onClose: () => void }) {
  useEffect(() => {
    if (!row) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [row, onClose]);

  if (!row) return null;
  const values = [
    ["Proforma date", formatDateDDMMYYYY(row.order_date) || "—", true],
    ["Customer reference", row.customer_ref || "—", true],
    ["Company", row.company || "Unassigned", false],
    ["Salesman", row.salesman || "—", false],
    ["Prepared by", row.prepared_by || "—", false],
    ["Type of work", row.type_of_work || "—", true],
    ["Confirmed quantity", `${row.ordered_qty.toLocaleString()} pcs`, true],
    ["Confirmed area", `${row.ordered_sqm.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} m²`, true],
    ["PI status", row.pi_status || "—", false],
    ["Zoho status", row.zoho_status || "—", false],
    ["Source rows grouped", row.record_count.toLocaleString(), true],
    ["Workbook", row.source_file?.split(/[\\/]/).pop() || "Unlabelled source", true],
  ] as const;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink-950/80 p-4 backdrop-blur-sm animate-fade-in" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
      <section role="dialog" aria-modal="true" aria-labelledby="job-order-detail-title" className="relative w-full max-w-2xl overflow-hidden rounded-2xl border border-line-strong/80 bg-ink-900 p-5 shadow-raised sm:p-6">
        <div className="flex items-start justify-between gap-4 border-b border-line-strong/60 pb-4">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <span className="eyebrow text-brand-300">Confirmed proforma hand-off</span>
              <Badge tone="success">{row.confirmation_status}</Badge>
            </div>
            <h2 id="job-order-detail-title" className="mt-2 truncate font-mono text-lg font-bold text-text-primary sm:text-xl">{row.pi_number}</h2>
            <p className="mt-1 truncate text-xs text-text-muted">{row.company || "Unassigned company"}{row.customer_ref ? ` · ${row.customer_ref}` : ""}</p>
          </div>
          <button type="button" onClick={onClose} aria-label="Close job order details" className="shrink-0 rounded-lg border border-line-strong p-1.5 text-text-faint transition-colors hover:bg-ink-850 hover:text-text-primary">✕</button>
        </div>

        <div className="mt-5 grid max-h-[55vh] grid-cols-2 gap-x-5 gap-y-4 overflow-y-auto pr-1 sm:grid-cols-3">
          {values.map(([label, value, mono]) => (
            <div key={label} className="min-w-0">
              <span className="mb-1 block text-3xs font-semibold uppercase tracking-wider text-text-faint">{label}</span>
              <span className={`block break-words text-xs text-text-primary ${mono ? "font-mono" : ""}`}>{value}</span>
            </div>
          ))}
        </div>

        <p className="mt-5 rounded-xl border border-brand-400/20 bg-brand-400/5 px-3.5 py-3 text-2xs leading-relaxed text-text-muted">
          This website view is a read-only queue. It does not create or edit a Job Order in the desktop application; the confirmed PI and its source rows remain the authority.
        </p>

        <div className="mt-5 flex flex-wrap items-center justify-end gap-2 border-t border-line-strong/60 pt-4">
          <LocaleLink href={confirmedProformaHrefForJobOrder(row.pi_number, row.source_file)} className="inline-flex h-9 items-center justify-center rounded-xl border border-line-strong/80 bg-ink-850/80 px-3.5 text-xs font-semibold text-text-muted transition-colors hover:border-brand-400 hover:text-brand-200">
            Open Proforma Records
          </LocaleLink>
          <LocaleLink href={deliveryNoteHrefForJobOrder(row.pi_number, row.source_file)} className="inline-flex h-9 items-center justify-center rounded-xl border border-brand-400/40 bg-brand-400/10 px-3.5 text-xs font-semibold text-brand-200 transition-colors hover:bg-brand-400/20">
            Next: Delivery Note →
          </LocaleLink>
          <button type="button" onClick={onClose} className="inline-flex h-9 items-center justify-center rounded-xl bg-brand-400 px-4 text-xs font-semibold text-accent-ink transition-colors hover:bg-brand-300">Done</button>
        </div>
      </section>
    </div>
  );
}
