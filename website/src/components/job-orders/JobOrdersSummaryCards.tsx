"use client";

import type { JobOrderSummary } from "@/types/job-orders";

type Props = { summary: JobOrderSummary | null; loading: boolean };

export function JobOrdersSummaryCards({ summary, loading }: Props) {
  if (loading && !summary) {
    return (
      <div className="dense-card-grid dense-fixed-grid grid grid-cols-3 gap-3 sm:grid-cols-5">
        {Array.from({ length: 5 }).map((_, index) => (
          <div key={index} className="h-24 animate-pulse rounded-2xl border border-line-strong/40 bg-ink-900/60 p-3.5">
            <div className="mb-3 h-3 w-24 rounded bg-line-strong/50" />
            <div className="mb-2 h-7 w-20 rounded bg-line-strong/60" />
            <div className="h-2.5 w-28 rounded bg-line-strong/40" />
          </div>
        ))}
      </div>
    );
  }

  const value = summary ?? {
    totalJobOrders: 0,
    totalSourceRows: 0,
    totalQty: 0,
    totalSqm: 0,
    activeCompaniesCount: 0,
    activeSalesmenCount: 0,
  };
  const cards = [
    { label: "Confirmed PIs", value: value.totalJobOrders.toLocaleString(), note: "One queue item per PI", tone: "text-text-primary", tag: "COUNT(PI)" },
    { label: "Source rows", value: value.totalSourceRows.toLocaleString(), note: "Grouped from Excel Sync", tone: "text-text-secondary", tag: "COUNT(rows)" },
    { label: "Ordered pieces", value: `${value.totalQty.toLocaleString()} pcs`, note: "Confirmed proforma quantity", tone: "text-brand-300", tag: "SUM(qty)" },
    { label: "Ordered area", value: `${value.totalSqm.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} m²`, note: "Confirmed proforma area", tone: "text-glass-teal", tag: "SUM(sqm)" },
    { label: "Customers", value: value.activeCompaniesCount.toLocaleString(), note: "In the filtered queue", tone: "text-text-secondary", tag: "DISTINCT" },
  ];

  return (
    <div className="dense-card-grid dense-fixed-grid grid grid-cols-3 gap-3 sm:grid-cols-5">
      {cards.map((card) => (
        <div key={card.label} className="relative overflow-hidden rounded-2xl border border-line-strong/60 bg-ink-900/80 p-3.5 shadow-panel transition-colors hover:border-brand-400/35 hover:bg-ink-850/90">
          <div className="flex items-center justify-between gap-1.5">
            <span className="text-2xs font-semibold uppercase tracking-wider text-text-faint">{card.label}</span>
            <span className="font-mono text-3xs uppercase text-brand-300">{card.tag}</span>
          </div>
          <div className={`mt-2 font-mono text-xl font-bold tracking-tight sm:text-2xl ${card.tone}`}>{card.value}</div>
          <div className="mt-1 truncate text-3xs text-text-muted">{card.note}</div>
        </div>
      ))}
    </div>
  );
}
