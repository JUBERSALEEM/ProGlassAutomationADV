"use client";

import type { RecordSummary } from "@/types/records";

type RecordsSummaryCardsProps = {
  summary: RecordSummary | null;
  loading: boolean;
};

export function RecordsSummaryCards({ summary, loading }: RecordsSummaryCardsProps) {
  if (loading && !summary) {
    return (
      <div className="dense-card-grid dense-fixed-grid grid grid-cols-3 gap-3 sm:grid-cols-6">
        {Array.from({ length: 6 }).map((_, i) => (
          <div
            key={i}
            className="animate-pulse rounded-2xl border border-line-strong/40 bg-ink-900/60 p-3.5 h-24"
          >
            <div className="h-3 w-16 bg-line-strong/50 rounded mb-3" />
            <div className="h-7 w-24 bg-line-strong/60 rounded mb-2" />
            <div className="h-2.5 w-12 bg-line-strong/40 rounded" />
          </div>
        ))}
      </div>
    );
  }

  const s = summary || {
    totalEntries: 0,
    totalQty: 0,
    totalSqm: 0,
    averageSqm: 0,
    activeCompaniesCount: 0,
    activeSalesmenCount: 0,
  };

  const cards = [
    {
      label: "Total Entries",
      value: s.totalEntries.toLocaleString(),
      subtext: "Filtered operations",
      accent: "text-text-primary",
      tag: "COUNT(*)",
    },
    {
      label: "Total Qty",
      value: `${s.totalQty.toLocaleString()} pcs`,
      subtext: "Total fabricated units",
      accent: "text-brand-300",
      tag: "SUM(qty)",
    },
    {
      label: "Total SQM",
      value: `${s.totalSqm.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} m²`,
      subtext: "Total glass surface",
      accent: "text-glass-teal",
      tag: "SUM(sqm)",
    },
    {
      label: "Average Area",
      value: `${s.averageSqm.toFixed(2)} m²`,
      subtext: "Per fabricated piece",
      accent: "text-text-secondary",
      tag: "SQM / QTY",
    },
    {
      label: "Active Companies",
      value: s.activeCompaniesCount.toString(),
      subtext: "Clients in dataset",
      accent: "text-text-secondary",
      tag: "DISTINCT",
    },
    {
      label: "Active Salesmen",
      value: s.activeSalesmenCount.toString(),
      subtext: "Personnel attributed",
      accent: "text-text-secondary",
      tag: "DISTINCT",
    },
  ];

  return (
    <div className="dense-card-grid dense-fixed-grid grid grid-cols-3 gap-3 sm:grid-cols-6">
      {cards.map((card) => (
        <div
          key={card.label}
          className="relative overflow-hidden rounded-2xl border border-line-strong/60 bg-ink-900/80 p-3.5 transition-all hover:border-brand-400/35 hover:bg-ink-850/90 shadow-panel"
        >
          <div className="flex items-center justify-between gap-1.5">
            <span className="text-2xs font-semibold uppercase tracking-wider text-text-faint">
              {card.label}
            </span>
            <span className="font-mono text-3xs text-brand-300 uppercase">
              {card.tag}
            </span>
          </div>
          <div className={`mt-2 font-mono text-xl font-bold tracking-tight sm:text-2xl ${card.accent}`}>
            {card.value}
          </div>
          <div className="mt-1 text-3xs text-text-muted truncate">
            {card.subtext}
          </div>
        </div>
      ))}
    </div>
  );
}
