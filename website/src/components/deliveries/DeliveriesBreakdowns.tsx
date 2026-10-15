"use client";

import type { DeliveryBreakdownItem, DeliveryBreakdowns } from "@/types/deliveries";

/**
 * Four slices of the same balance: who owes it, who sold it, where it is in
 * the lifecycle, and what kind of glass it is.
 *
 * Each panel shows the **balance** as its bar rather than the row count,
 * because on this page the interesting quantity is what is still owed — a
 * company with forty delivered PIs is not the one to ring, and a list sorted
 * by count would put it on top. Rows are clickable and feed the page's filters,
 * which is how an operator goes from "24 part-delivered" to the eight PIs that
 * belong to one client.
 */
export function DeliveriesBreakdowns({
  breakdowns,
  loading,
  activeCompany,
  activeSalesman,
  activeTypeOfWork,
  activeLifecycle,
  onSelectCompany,
  onSelectSalesman,
  onSelectTypeOfWork,
  onSelectLifecycle,
}: {
  breakdowns: DeliveryBreakdowns | null;
  loading: boolean;
  activeCompany?: string;
  activeSalesman?: string;
  activeTypeOfWork?: string;
  activeLifecycle?: string;
  onSelectCompany: (name: string) => void;
  onSelectSalesman: (name: string) => void;
  onSelectTypeOfWork: (name: string) => void;
  onSelectLifecycle: (name: string) => void;
}) {
  if (loading && !breakdowns) {
    return (
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <div
            key={i}
            className="animate-pulse rounded-2xl border border-line-strong/40 bg-ink-900/60 p-4 h-64"
          >
            <div className="h-4 w-28 bg-line-strong/60 rounded mb-4" />
            <div className="space-y-3">
              <div className="h-3 w-full bg-line-strong/40 rounded" />
              <div className="h-3 w-4/5 bg-line-strong/40 rounded" />
              <div className="h-3 w-3/4 bg-line-strong/40 rounded" />
            </div>
          </div>
        ))}
      </div>
    );
  }

  const b: DeliveryBreakdowns = breakdowns ?? {
    company: [],
    salesman: [],
    lifecycle: [],
    typeOfWork: [],
  };

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <BreakdownCard
        title="Company"
        subtitle="Outstanding m²"
        items={b.company}
        activeValue={activeCompany}
        onSelect={onSelectCompany}
        barColor="bg-brand-400"
      />
      <BreakdownCard
        title="Salesman"
        subtitle="Outstanding m²"
        items={b.salesman}
        activeValue={activeSalesman}
        onSelect={onSelectSalesman}
        barColor="bg-glass-teal"
      />
      <BreakdownCard
        title="Lifecycle"
        subtitle="GROUP BY balance"
        items={b.lifecycle}
        activeValue={activeLifecycle}
        onSelect={onSelectLifecycle}
        barColor="bg-state-warning"
      />
      <BreakdownCard
        title="Type of Work"
        subtitle="Outstanding m²"
        items={b.typeOfWork}
        activeValue={activeTypeOfWork}
        onSelect={onSelectTypeOfWork}
        barColor="bg-glass-cyan"
      />
    </div>
  );
}

function BreakdownCard({
  title,
  subtitle,
  items,
  activeValue,
  onSelect,
  barColor,
}: {
  title: string;
  subtitle: string;
  items: DeliveryBreakdownItem[];
  activeValue?: string;
  onSelect: (name: string) => void;
  barColor: string;
}) {
  const displayItems = items.slice(0, 6);
  const largest = Math.max(1, ...displayItems.map((item) => item.balanceSqm));

  return (
    <div className="flex flex-col justify-between rounded-2xl border border-line-strong/60 bg-ink-900/80 p-4 shadow-panel">
      <div>
        <div className="flex items-baseline justify-between gap-2 border-b border-line/60 pb-2.5">
          <h3 className="card-title text-sm font-bold text-text-primary">{title}</h3>
          <span className="font-mono text-3xs text-brand-300">{subtitle}</span>
        </div>

        {displayItems.length === 0 ? (
          <p className="py-6 text-center text-xs text-text-faint">No PIs matching filters</p>
        ) : (
          <div className="mt-3 space-y-2.5">
            {displayItems.map((item) => {
              const isSelected = activeValue === item.name;
              const width = Math.min(100, Math.max(3, (item.balanceSqm / largest) * 100));
              return (
                <button
                  key={item.name}
                  type="button"
                  onClick={() => onSelect(isSelected ? "" : item.name)}
                  className={`w-full rounded-lg border p-1.5 text-left transition-all group ${
                    isSelected
                      ? "border-brand-400/60 bg-brand-500/20"
                      : "border-transparent hover:bg-ink-850/80"
                  }`}
                  title={`Filter by ${item.name}`}
                >
                  <div className="flex items-center justify-between text-xs">
                    <span
                      className={`max-w-[170px] truncate font-medium ${
                        isSelected
                          ? "font-semibold text-brand-200"
                          : "text-text-secondary group-hover:text-text-primary"
                      }`}
                    >
                      {item.name}
                    </span>
                    <span className="ml-1 shrink-0 font-mono text-2xs font-semibold text-text-primary">
                      {item.balanceSqm.toLocaleString(undefined, {
                        minimumFractionDigits: 1,
                        maximumFractionDigits: 1,
                      })}
                      <span className="ml-1 text-3xs font-normal text-text-faint">
                        ({item.count})
                      </span>
                    </span>
                  </div>
                  <div className="mt-1 h-1.5 w-full overflow-hidden rounded-full bg-ink-950">
                    <div
                      className={`h-full rounded-full ${barColor} opacity-75 transition-all duration-300`}
                      style={{ width: `${width}%` }}
                    />
                  </div>
                </button>
              );
            })}
          </div>
        )}
      </div>

      {items.length > 6 && (
        <div className="mt-2 border-t border-line/40 pt-2.5 text-center">
          <span className="font-mono text-3xs text-text-faint">
            + {items.length - 6} more in filtered set
          </span>
        </div>
      )}
    </div>
  );
}
