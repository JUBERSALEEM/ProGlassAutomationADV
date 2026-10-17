"use client";

import type { DeliverySummary } from "@/types/deliveries";

/**
 * The statistics band — the same visual grammar as the Reports dashboard's
 * summary cards, answering the tracker's own six questions:
 *
 *   what was ordered · what has gone out · what is owed · how far along ·
 *   how many loads · how much of it is at risk
 *
 * The cards carry their own SQL-ish tag the way the reports cards do, because
 * the tag is the honest description of where the number comes from: these are
 * sums over the filtered ledger, not estimates.
 */
export function DeliveriesSummaryCards({
  summary,
  loading,
}: {
  summary: DeliverySummary | null;
  loading: boolean;
}) {
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

  const s: DeliverySummary = summary ?? {
    totalPis: 0,
    totalMovements: 0,
    orderedQty: 0,
    orderedSqm: 0,
    deliveredQty: 0,
    deliveredSqm: 0,
    balanceQty: 0,
    balanceSqm: 0,
    progressPct: 0,
    fullyDeliveredPIs: 0,
    partiallyDeliveredPIs: 0,
    awaitingDispatchPIs: 0,
    overDeliveredPIs: 0,
    stalePIs: 0,
    averageMovementsPerPi: 0,
    largestBalanceSqm: 0,
    concentration: [],
  };

  const cards = [
    {
      label: "Confirmed PIs",
      value: s.totalPis.toLocaleString(),
      subtext: "Delivery obligations",
      accent: "text-text-primary",
      tag: "COUNT(DISTINCT pi)",
    },
    {
      label: "Ordered",
      value: `${s.orderedQty.toLocaleString()} pcs`,
      subtext: `${s.orderedSqm.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} m² · confirmed`,
      accent: "text-text-secondary",
      tag: "SUM(qty)",
    },
    {
      label: "Delivered",
      value: `${s.deliveredQty.toLocaleString()} pcs`,
      subtext: `${s.deliveredSqm.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} m² · left the building`,
      accent: "text-brand-300",
      tag: "SUM(movements)",
    },
    {
      label: "Balance",
      value: `${s.balanceQty.toLocaleString()} pcs`,
      subtext: `${s.balanceSqm.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} m² still owed`,
      accent: s.balanceQty > 0 ? "text-state-warning" : "text-state-success",
      tag: "ORDERED − DELIVERED",
    },
    {
      label: "Progress",
      value: `${s.progressPct.toFixed(1)}%`,
      subtext: "By glass area (m²)",
      accent: "text-glass-teal",
      tag: "DELIVERED / ORDERED",
    },
    {
      label: "Movements",
      value: s.totalMovements.toLocaleString(),
      subtext: `${s.averageMovementsPerPi.toFixed(2)} per PI on average`,
      accent: "text-text-secondary",
      tag: "COUNT(entries)",
    },
  ];

  const tiles = [
    {
      label: "Awaiting dispatch",
      value: s.awaitingDispatchPIs,
      tone: "text-text-secondary",
      note: "Confirmed, nothing moved",
    },
    {
      label: "Part delivered",
      value: s.partiallyDeliveredPIs,
      tone: "text-state-warning",
      note: "Open balance, at least one load",
    },
    {
      label: "Delivered",
      value: s.fullyDeliveredPIs,
      tone: "text-state-success",
      note: s.overDeliveredPIs > 0 ? `Includes ${s.overDeliveredPIs} over delivered` : "Fully dispatched",
    },
    {
      label: "Stale 14+ days",
      value: s.stalePIs,
      tone: s.stalePIs > 0 ? "text-state-danger" : "text-text-secondary",
      note: "No movement in two weeks",
    },
  ];

  return (
    <div className="space-y-3.5">
      <div className="dense-card-grid dense-fixed-grid grid grid-cols-3 gap-3 sm:grid-cols-6">
        {cards.map((card) => (
          <div
            key={card.label}
            className="relative overflow-hidden rounded-2xl border border-line-strong/60 bg-ink-900/80 p-3.5 shadow-panel transition-all hover:border-brand-400/35 hover:bg-ink-850/90"
          >
            <div className="flex items-center justify-between gap-1.5">
              <span className="text-2xs font-semibold uppercase tracking-wider text-text-faint">
                {card.label}
              </span>
              <span className="font-mono text-3xs uppercase text-brand-300">{card.tag}</span>
            </div>
            <div className={`mt-2 font-mono text-xl font-bold tracking-tight sm:text-2xl ${card.accent}`}>
              {card.value}
            </div>
            <div className="mt-1 truncate text-3xs text-text-muted">{card.subtext}</div>
          </div>
        ))}
      </div>

      {/* Where the outstanding glass actually sits. Four panels sorted by
          balance answer "who owes what"; this answers "how concentrated is it",
          which is the question that decides who gets today's vehicle. */}
      {s.concentration.length > 0 && s.balanceSqm > 0 && (
        <div className="rounded-2xl border border-line-strong/50 bg-ink-950/60 px-4 py-2.5">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <span className="text-2xs font-semibold uppercase tracking-wider text-text-faint">
              Outstanding balance concentration
            </span>
            <span className="font-mono text-3xs text-text-faint">
              top {Math.min(3, s.concentration.length)} of {s.concentration.length} clients with a balance
            </span>
          </div>
          <div className="mt-2 flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-4">
            {s.concentration.slice(0, 3).map((entry) => (
              <div key={entry.name} className="min-w-0 flex-1">
                <div className="flex items-baseline justify-between gap-2 text-2xs">
                  <span className="truncate text-text-secondary" title={entry.name}>
                    {entry.name}
                  </span>
                  <span className="shrink-0 font-mono text-text-primary">
                    {entry.balanceSqm.toLocaleString(undefined, { maximumFractionDigits: 1 })} m²
                    <span className="ml-1 text-3xs text-text-faint">{entry.share.toFixed(0)}%</span>
                  </span>
                </div>
                <div className="mt-1 h-1 w-full overflow-hidden rounded-full bg-ink-900">
                  <div
                    className="h-full rounded-full bg-state-warning/70"
                    style={{ width: `${Math.min(100, Math.max(2, entry.share))}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Lifecycle strip: the four states a confirmed PI can be in, counted. */}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {tiles.map((tile) => (
          <div
            key={tile.label}
            className="flex items-center justify-between gap-3 rounded-2xl border border-line-strong/50 bg-ink-950/60 px-4 py-2.5"
          >
            <div className="min-w-0">
              <div className="text-2xs font-semibold uppercase tracking-wider text-text-faint">
                {tile.label}
              </div>
              <div className="mt-0.5 truncate text-3xs text-text-muted">{tile.note}</div>
            </div>
            <span className={`font-mono text-lg font-bold ${tile.tone}`}>
              {tile.value.toLocaleString()}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}
