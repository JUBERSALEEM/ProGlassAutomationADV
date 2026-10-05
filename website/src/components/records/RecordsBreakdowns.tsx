"use client";

import type { BreakdownItem, RecordBreakdowns } from "@/types/records";

type RecordsBreakdownsProps = {
  breakdowns: RecordBreakdowns | null;
  loading: boolean;
  activePreparedBy?: string;
  activeSalesman?: string;
  activeCompany?: string;
  activeTypeOfWork?: string;
  onSelectPreparedBy: (name: string) => void;
  onSelectSalesman: (name: string) => void;
  onSelectCompany: (name: string) => void;
  onSelectTypeOfWork: (name: string) => void;
};

export function RecordsBreakdowns({
  breakdowns,
  loading,
  activePreparedBy,
  activeSalesman,
  activeCompany,
  activeTypeOfWork,
  onSelectPreparedBy,
  onSelectSalesman,
  onSelectCompany,
  onSelectTypeOfWork,
}: RecordsBreakdownsProps) {
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

  const b = breakdowns || {
    preparedBy: [],
    salesman: [],
    company: [],
    typeOfWork: [],
  };

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {/* 1. PREPARED BY */}
      <BreakdownCard
        title="Prepared By"
        subtitle="GROUP BY prepared_by"
        items={b.preparedBy}
        activeValue={activePreparedBy}
        onSelect={onSelectPreparedBy}
        barColor="bg-brand-400"
      />

      {/* 2. SALESMAN */}
      <BreakdownCard
        title="Salesman"
        subtitle="GROUP BY salesman"
        items={b.salesman}
        activeValue={activeSalesman}
        onSelect={onSelectSalesman}
        barColor="bg-glass-teal"
      />

      {/* 3. COMPANY */}
      <BreakdownCard
        title="Company"
        subtitle="GROUP BY company"
        items={b.company}
        activeValue={activeCompany}
        onSelect={onSelectCompany}
        barColor="bg-brand-300"
      />

      {/* 4. TYPE OF WORK */}
      <BreakdownCard
        title="Type of Work"
        subtitle="GROUP BY type_of_work"
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
  items: BreakdownItem[];
  activeValue?: string;
  onSelect: (name: string) => void;
  barColor: string;
}) {
  const displayItems = items.slice(0, 6); // Top 6

  return (
    <div className="rounded-2xl border border-line-strong/60 bg-ink-900/80 p-4 shadow-panel flex flex-col justify-between">
      <div>
        <div className="flex items-baseline justify-between gap-2 border-b border-line/60 pb-2.5">
          <h3 className="card-title text-sm font-bold text-text-primary">{title}</h3>
          <span className="font-mono text-3xs text-brand-400/70">{subtitle}</span>
        </div>

        {displayItems.length === 0 ? (
          <p className="py-6 text-center text-xs text-text-faint">No records matching filters</p>
        ) : (
          <div className="mt-3 space-y-2.5">
            {displayItems.map((item) => {
              const isSelected = activeValue === item.name;
              return (
                <button
                  key={item.name}
                  type="button"
                  onClick={() => onSelect(isSelected ? "" : item.name)}
                  className={`w-full text-left rounded-lg p-1.5 transition-all group ${
                    isSelected
                      ? "bg-brand-500/20 border border-brand-400/60"
                      : "hover:bg-ink-850/80 border border-transparent"
                  }`}
                  title={`Click to filter by ${item.name}`}
                >
                  <div className="flex items-center justify-between text-xs">
                    <span
                      className={`truncate max-w-[170px] font-medium ${
                        isSelected ? "text-brand-200 font-semibold" : "text-text-secondary group-hover:text-text-primary"
                      }`}
                    >
                      {item.name}
                    </span>
                    <span className="font-mono text-2xs font-semibold text-text-primary ml-1 shrink-0">
                      {item.count.toLocaleString()}
                      <span className="text-3xs text-text-faint font-normal ml-1">({item.percentage}%)</span>
                    </span>
                  </div>

                  {/* Horizontal progress bar */}
                  <div className="mt-1 h-1.5 w-full rounded-full bg-ink-950 overflow-hidden">
                    <div
                      className={`h-full rounded-full ${barColor} opacity-75 transition-all duration-300`}
                      style={{ width: `${Math.min(100, Math.max(3, item.percentage))}%` }}
                    />
                  </div>
                </button>
              );
            })}
          </div>
        )}
      </div>

      {items.length > 6 && (
        <div className="pt-2.5 mt-2 border-t border-line/40 text-center">
          <span className="text-3xs font-mono text-text-faint">
            + {items.length - 6} more in filtered dataset
          </span>
        </div>
      )}
    </div>
  );
}
