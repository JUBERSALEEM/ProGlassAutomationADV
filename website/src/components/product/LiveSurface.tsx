"use client";

import { useMemo, useState } from "react";
import {
  demoFor,
  optimizerLimits,
  sheetSizes,
  stageNames,
  type Demo,
  type DemoColumn,
  type DemoRow,
  type DemoView,
} from "@/data/liveDemos";
import { cn } from "@/lib/cn";
import { SheetGallery } from "@/components/optimization/SheetGallery";
import { packGuillotine, plateViews } from "@/lib/guillotinePack";

/**
 * The live module preview.
 *
 * This replaces the old "capture pending" plate. Instead of an empty frame the
 * visitor gets the module's own layout, filled with sample data, and they can
 * work it: sort the grid, search it, pick a row, step the wizard, drag the
 * optimizer sheet, walk a job through the eight production queues.
 *
 * It is a reconstruction of the screen, built from the same navigation labels,
 * field names and charge keys as the application, and it is labelled as such
 * wherever it appears — it is never presented as a screen capture.
 */
export function LiveSurface({
  moduleKey,
  name,
  capabilities = [],
  className,
  compact = false,
}: {
  moduleKey: string;
  name: string;
  capabilities?: string[];
  className?: string;
  compact?: boolean;
}) {
  const demo = useMemo(() => demoFor(moduleKey, name, capabilities), [moduleKey, name, capabilities]);
  const [viewIndex, setViewIndex] = useState(0);

  const views = demo.views.length > 0 ? demo.views : [];
  const safeIndex = Math.min(viewIndex, Math.max(views.length - 1, 0));
  const view = views[safeIndex];

  return (
    <div className={cn("flex h-full flex-col", className)}>
        {/* ── Tabs ─────────────────────────────────────────────────────── */}
        {views.length > 1 ? (
          <div role="tablist" aria-label={`${name} views`} className="flex flex-wrap gap-1.5 border-b border-line/50 pb-2.5">
            {views.map((entry, index) => {
              const active = index === safeIndex;
              return (
                <button
                  key={`${entry.label}-${index}`}
                  type="button"
                  role="tab"
                  aria-selected={active}
                  onClick={() => setViewIndex(index)}
                  className={cn(
                    "rounded-full px-2.5 py-1 text-[0.75rem] font-medium transition-colors duration-200",
                    active
                      ? "bg-brand-400/18 text-brand-100 ring-1 ring-inset ring-brand-400/40"
                      : "text-text-muted hover:bg-ink-800/70 hover:text-text-primary",
                  )}
                >
                  {entry.label}
                </button>
              );
            })}
          </div>
        ) : null}

        {/* ── Engine ───────────────────────────────────────────────────── */}
        <div className="min-h-0 flex-1 pt-3">
          {demo.kind === "optimizer" ? (
            <OptimizerEngine demo={demo} compact={compact} />
          ) : demo.kind === "queue" ? (
            <QueueEngine demo={demo} compact={compact} />
          ) : demo.kind === "wizard" ? (
            <WizardEngine demo={demo} view={view} compact={compact} />
          ) : demo.kind === "dashboard" ? (
            <DashboardEngine demo={demo} compact={compact} />
          ) : demo.kind === "form" ? (
            <FormEngine moduleKey={moduleKey} demo={demo} view={view} />
          ) : (
            <TableEngine view={view} compact={compact} />
          )}
      </div>
    </div>
  );
}


/* ══════════════════════════════════════════════════════════════════════════
 * Shared chrome
 * ════════════════════════════════════════════════════════════════════════ */

function SampleTag() {
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full border border-line-strong/60 bg-ink-800/70 px-2 py-0.5 font-mono text-[0.6875rem] uppercase tracking-[0.14em] text-text-faint font-medium">
      <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-brand-400" />
      Sample data · live
    </span>
  );
}

function SearchBox({ value, onChange, label }: { value: string; onChange: (next: string) => void; label: string }) {
  return (
    <label className="flex items-center gap-1.5 rounded-lg border border-line-strong/60 bg-ink-900/70 px-2 py-1">
      <svg aria-hidden="true" viewBox="0 0 16 16" className="h-3 w-3 text-text-faint" fill="none" stroke="currentColor" strokeWidth="1.6">
        <circle cx="7" cy="7" r="4.5" />
        <path d="m10.5 10.5 3 3" strokeLinecap="round" />
      </svg>
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={label}
        className="w-24 bg-transparent text-[0.75rem] text-text-primary outline-none placeholder:text-text-faint focus:w-36 sm:w-32"
      />
    </label>
  );
}

function StatusPill({ value }: { value: string }) {
  const positive = /ready|complete|settled|delivered|active|ok|pass/i.test(value);
  const warning = /warn|pending|part|transit|loaded/i.test(value);

  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 whitespace-nowrap rounded-full border px-2 py-0.5 text-[0.75rem] font-medium",
        positive && "border-money-positive/35 text-money-positive",
        warning && !positive && "border-state-warning/35 text-state-warning",
        !positive && !warning && "border-line-strong/60 text-text-muted",
      )}
    >
      {value}
    </span>
  );
}

/* ══════════════════════════════════════════════════════════════════════════
 * Table engine — search, sort, select, totals
 * ════════════════════════════════════════════════════════════════════════ */

function TableEngine({ view, compact }: { view: DemoView | undefined; compact?: boolean }) {
  const [query, setQuery] = useState("");
  const [sortKey, setSortKey] = useState<string | null>(null);
  const [ascending, setAscending] = useState(true);
  const [selected, setSelected] = useState<string | null>(null);

  const columns = useMemo(() => view?.columns ?? [], [view]);
  const rows = useMemo(() => view?.rows ?? [], [view]);

  const visible = useMemo(() => {
    const needle = query.trim().toLowerCase();
    let out = rows;
    if (needle) {
      out = out.filter((row) => Object.values(row).some((cell) => cell.toLowerCase().includes(needle)));
    }
    if (sortKey) {
      const copy = [...out];
      copy.sort((a, b) => {
        const left = a[sortKey] ?? "";
        const right = b[sortKey] ?? "";
        const asNumber = Number(left.replace(/[^0-9.-]/g, ""));
        const asNumberRight = Number(right.replace(/[^0-9.-]/g, ""));
        const numeric = Number.isFinite(asNumber) && Number.isFinite(asNumberRight) && /\d/.test(left);
        const result = numeric ? asNumber - asNumberRight : left.localeCompare(right);
        return ascending ? result : -result;
      });
      out = copy;
    }
    return out;
  }, [rows, query, sortKey, ascending]);

  if (!view) return <EmptyPlate />;

  return (
    <div className="flex h-full flex-col">
      <div className="mb-2.5 flex flex-wrap items-center justify-between gap-2">
        <SampleTag />
        <SearchBox value={query} onChange={setQuery} label="Filter rows" />
      </div>

      <div className="min-h-0 flex-1 overflow-x-auto">
        <table className="w-full border-collapse text-left">
          <thead>
            <tr className="border-b border-line/60">
              {columns.map((column) => (
                <th
                  key={column.key}
                  scope="col"
                  className={cn(
                    "whitespace-nowrap px-2 py-1.5 font-mono text-[0.6875rem] font-semibold uppercase tracking-[0.14em] text-text-faint",
                    column.align === "right" && "text-right",
                    column.hideBelow === "sm" && "hidden sm:table-cell",
                    column.hideBelow === "md" && "hidden md:table-cell",
                  )}
                >
                  <button
                    type="button"
                    onClick={() => {
                      if (sortKey === column.key) setAscending((value) => !value);
                      else {
                        setSortKey(column.key);
                        setAscending(true);
                      }
                    }}
                    className={cn(
                      "inline-flex items-center gap-1 transition-colors hover:text-text-secondary",
                      sortKey === column.key && "text-brand-300",
                    )}
                  >
                    {column.label}
                    <span aria-hidden="true" className="text-[0.625rem]">
                      {sortKey === column.key ? (ascending ? "▲" : "▼") : "⇅"}
                    </span>
                  </button>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {visible.map((row, index) => {
              const id = `${view.label}-${index}-${row[columns[0]?.key ?? "ref"] ?? index}`;
              const isSelected = selected === id;
              return (
                <tr
                  key={`${id}-${index}`}
                  onClick={() => setSelected(isSelected ? null : id)}
                  className={cn(
                    "cursor-pointer border-b border-line/30 transition-colors duration-150",
                    isSelected ? "bg-brand-400/10" : "hover:bg-ink-800/60",
                  )}
                >
                  {columns.map((column) => (
                    <td
                      key={column.key}
                      className={cn(
                        "whitespace-nowrap px-2 py-1.5 text-[0.75rem]",
                        column.kind === "money" && "font-mono tabular-nums text-money-neutral",
                        column.kind === "num" && "font-mono tabular-nums text-text-secondary",
                        column.kind === "status" && "text-text-muted",
                        !column.kind && "text-text-secondary",
                        column.align === "right" && "text-right",
                        column.hideBelow === "sm" && "hidden sm:table-cell",
                        column.hideBelow === "md" && "hidden md:table-cell",
                      )}
                    >
                      {column.kind === "status" ? <StatusPill value={row[column.key] ?? ""} /> : row[column.key]}
                    </td>
                  ))}
                </tr>
              );
            })}
            {visible.length === 0 ? (
              <tr>
                <td colSpan={columns.length} className="px-2 py-6 text-center text-[0.75rem] text-text-faint">
                  No rows match “{query}”.
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>

      {!compact ? (
        <div className="mt-2.5 flex flex-wrap items-center justify-between gap-3 border-t border-line/50 pt-2.5">
          <p className="text-[0.75rem] text-text-faint">
            {visible.length} of {rows.length} rows
            {selected ? " · one row selected" : ""}
          </p>
          {view.summary && view.summary.length > 0 ? (
            <dl className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
              {view.summary.map((item) => (
                <div key={item.label} className="flex items-baseline gap-1.5">
                  <dt className="font-mono text-[0.6875rem] uppercase tracking-[0.12em] text-text-faint font-medium">
                    {item.label}
                  </dt>
                  <dd
                    className={cn(
                      "font-mono text-[0.75rem] font-semibold tabular-nums",
                      item.money ? "text-money-neutral" : "text-text-primary",
                    )}
                  >
                    {item.value}
                  </dd>
                </div>
              ))}
            </dl>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

/* ══════════════════════════════════════════════════════════════════════════
 * Optimizer engine — a real shelf packer over the cut list
 * ════════════════════════════════════════════════════════════════════════ */

/**
 * The plate set for the sample cut list.
 *
 * The packing itself lives in `src/lib/guillotinePack.ts`, shared with the
 * homepage and the optimization page so every plate drawing on this site comes
 * out of one implementation and they cannot disagree with each other.
 *
 * It is a shelf rule — the simplest guillotine-cuttable strategy — used to
 * illustrate how a run spreads across plates. The engine is not that: it
 * searches five families and six plate patterns. Everything printed here is
 * arithmetic over the pieces actually placed, and a piece that no plate can
 * hold is reported rather than dropped.
 */
function usePackedSample(parts: { piece: string; w: number; h: number }[], sheet: { label: string; w: number; h: number }) {
  return useMemo(() => {
    const result = packGuillotine(
      parts.map((part) => ({ id: part.piece, width: part.w, height: part.h })),
      { width: sheet.w, height: sheet.h },
      /*
       * `optimizerLimits` names its margins left/right/top/bottom for the
       * display; the packer takes them as four edges and a kerf. Mapped here,
       * once, rather than keeping two shapes of the same fact.
       */
      {
        left: optimizerLimits.marginLeft,
        right: optimizerLimits.marginRight,
        top: optimizerLimits.marginTop,
        bottom: optimizerLimits.marginBottom,
        kerf: optimizerLimits.kerf,
      },
    );
    return {
      result,
      plates: plateViews(result, { width: sheet.w, height: sheet.h, label: `${sheet.w} × ${sheet.h}` }),
    };
  }, [parts, sheet]);
}

function OptimizerEngine({ demo, compact }: { demo: Demo; compact?: boolean }) {
  const [sheetIndex, setSheetIndex] = useState(1);
  const [run, setRun] = useState(0);
  const [piecesPerPart, setPiecesPerPart] = useState(1);

  const sheet = sheetSizes[sheetIndex] ?? sheetSizes[1]!;

  const parts = useMemo(() => {
    const base = demo.views[0]?.rows ?? [];
    return base.flatMap((row) => {
      const w = Number(row.w ?? 0);
      const h = Number(row.h ?? 0);
      const qty = Math.max(1, Number(row.qty ?? 1) * piecesPerPart);
      return Array.from({ length: Math.min(qty, 12) }, (_, i) => ({
        piece: `${row.piece ?? "P"}-${i + 1}`,
        w,
        h,
      }));
    });
  }, [demo.views, piecesPerPart]);

  const { result, plates } = usePackedSample(parts, sheet);

  const partArea = parts.reduce((total, part) => total + (part.w * part.h) / 1_000_000, 0);
  const stockArea = result.stockAreaSqm;
  const usedArea = result.partAreaSqm;
  const utilization = result.utilizationPct;
  const wastage = stockArea - usedArea;
  const scrap = stockArea - partArea;

  return (
    <div className="flex h-full flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SampleTag />
        <button
          type="button"
          onClick={() => setRun((value) => value + 1)}
          className="inline-flex items-center gap-1.5 rounded-full border border-brand-400/45 bg-brand-500/14 px-3 py-1 text-[0.75rem] font-semibold text-brand-100 transition-colors hover:border-brand-300/70"
        >
          <span aria-hidden="true" className={cn("h-1.5 w-1.5 rounded-full bg-brand-300", run > 0 && "animate-pulse-soft")} />
          {run === 0 ? "Run optimizer" : `Re-run · pass ${run + 1}`}
        </button>
      </div>

      <div className="flex flex-wrap items-end gap-4">
        <label className="flex flex-col gap-1">
          <span className="font-mono text-[0.6875rem] uppercase tracking-[0.14em] text-text-faint font-medium">Sheet</span>
          <select
            value={sheetIndex}
            onChange={(event) => setSheetIndex(Number(event.target.value))}
            className="rounded-lg border border-line-strong/60 bg-ink-900/80 px-2 py-1 text-[0.75rem] text-text-primary outline-none"
          >
            {sheetSizes.map((entry, index) => (
              <option key={`${entry.label}-${index}`} value={index}>
                {entry.label}
              </option>
            ))}
          </select>
        </label>

        <label className="flex min-w-[9rem] flex-1 flex-col gap-1">
          <span className="font-mono text-[0.6875rem] uppercase tracking-[0.14em] text-text-faint font-medium">
            Pieces per size · {piecesPerPart}
          </span>
          <input
            type="range"
            min={1}
            max={4}
            step={1}
            value={piecesPerPart}
            onChange={(event) => setPiecesPerPart(Number(event.target.value))}
            className="accent-brand-400"
          />
        </label>
      </div>

      {/*
        Every plate of the run, as thumbnails.

        This replaced a vertical stack of full-width plates. A plate is wider
        than it is tall, so one drawn across the frame is already ~70% of its
        width in height — and the sample job needs more than a dozen of them.
        The old version therefore put a column of near-empty boxes down the
        page, the last few almost blank, and pushed everything below it out of
        sight.

        Thumbnails wrapped across the frame keep the whole run visible at the
        size a glance actually needs. It is the same treatment the application
        gives it in the 2D View tab, where the Multi-Sheet Gallery exists for
        precisely this reason.
      */}
      <SheetGallery
        plates={plates}
        pageSize={compact ? 3 : 6}
        paginate={!compact}
        columns={compact ? "grid-cols-3" : "grid-cols-2 sm:grid-cols-3"}
      />

      {result.unplaced.length > 0 ? (
        <p className="rounded-card border border-state-warning/40 bg-state-warning/10 px-3 py-2 text-[0.75rem] leading-relaxed text-text-secondary">
          <strong className="font-semibold text-text-primary">
            {result.unplaced.length} {result.unplaced.length === 1 ? "piece" : "pieces"} could not be placed
          </strong>{" "}
          on a {sheet.w} × {sheet.h} plate — too large for the usable area in either orientation. Choose a larger sheet,
          or cut {result.unplaced.length === 1 ? "it" : "them"} from stock that can hold {result.unplaced.length === 1 ? "it" : "them"}.
        </p>
      ) : null}

      {/*
        The six figures the application's Summary tab reports for a run, in its
        order. Sheets used and utilization first, because those are the two a
        shop owner looks for; wastage, stock, part area and scrap after them.
        All six are computed from the pieces actually placed.
      */}
      {!compact ? (
        <dl className="grid grid-cols-3 gap-2 sm:grid-cols-6">
          {[
            { label: "Sheets used", value: String(result.plates) },
            { label: "Utilization", value: `${utilization.toFixed(1)}%`, good: true },
            { label: "Wastage", value: `${wastage.toFixed(2)} m²`, bad: true },
            { label: "Stock area", value: `${stockArea.toFixed(2)} m²` },
            { label: "Part area", value: `${partArea.toFixed(2)} m²` },
            { label: "Scrap", value: `${scrap.toFixed(2)} m²`, bad: true },
          ].map((item) => (
            <div key={item.label} className="stat-tile px-2.5 py-2">
              <dt className="font-mono text-[0.625rem] uppercase tracking-[0.12em] text-text-faint font-medium">{item.label}</dt>
              <dd
                className={cn(
                  "mt-0.5 font-mono text-sm font-semibold tabular-nums",
                  item.good && "text-money-positive",
                  item.bad && "text-money-negative",
                  !item.good && !item.bad && "text-text-primary",
                )}
              >
                {item.value}
              </dd>
            </div>
          ))}
        </dl>
      ) : null}

      <p className="text-[0.75rem] leading-relaxed text-text-faint font-medium">
        {demo.blurb} Arranged here with a shelf packer over the sample cut list, so the figures are genuinely
        calculated — change the sheet or the quantity and read them again.
      </p>
    </div>
  );
}


/* ══════════════════════════════════════════════════════════════════════════
 * Queue engine — the eight production stages
 * ════════════════════════════════════════════════════════════════════════ */

/**
 * A stable identity for a queue row.
 *
 * The job number alone is not unique — a cut list can hold several pieces of
 * the same job, and the stage queues carry rows with no job number at all — so
 * the key falls back through piece, then reference, then the row's position.
 */
function rowKey(row: DemoRow, index: number): string {
  const label = row.job || row.piece || row.ref || row.note || row.code;
  return label ? `${label}-${index}` : `row-${index}`;
}

function QueueEngine({ demo, compact }: { demo: Demo; compact?: boolean }) {
  const [stage, setStage] = useState(1);
  const rows = demo.views[0]?.rows ?? [];
  const [progress, setProgress] = useState<Record<string, number>>(() =>
    Object.fromEntries(
      rows.map((row, index) => [
        rowKey(row, index),
        Number(row.qty ?? 0) > 0 ? Number(row.cut ?? 0) / Number(row.qty ?? 1) : 0,
      ]),
    ),
  );

  const active = stageNames[stage - 1] ?? stageNames[0];

  const advance = (job: string) => {
    setProgress((current) => ({
      ...current,
      [job]: Math.min(1, (current[job] ?? 0) + 0.25),
    }));
  };

  return (
    <div className="flex h-full flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SampleTag />
        <p className="font-mono text-[0.6875rem] uppercase tracking-[0.14em] text-brand-300">
          Stage {stage} of 8 · {active}
        </p>
      </div>

      <div role="list" aria-label="Production stages" className="flex flex-wrap gap-1.5">
        {stageNames.map((label, index) => {
          const isActive = index + 1 === stage;
          const passed = index + 1 < stage;
          return (
            <div role="listitem" key={`stage-${label}-${index}`}>
              <button
                type="button"
                onClick={() => setStage(index + 1)}
                className={cn(
                  "rounded-full border px-2.5 py-1 text-[0.75rem] font-medium transition-colors duration-200",
                  isActive
                    ? "border-brand-400/60 bg-brand-400/16 text-brand-100"
                    : passed
                      ? "border-money-positive/35 text-money-positive"
                      : "border-line-strong/60 text-text-muted hover:border-brand-400/40 hover:text-text-primary",
                )}
              >
                {label}
              </button>
            </div>
          );
        })}
      </div>

      <div role="list" aria-label="Jobs in this stage" className="min-h-0 flex-1 space-y-2 overflow-y-auto">
        {rows.map((row, index) => {
          const id = rowKey(row, index);
          const done = progress[id] ?? 0;
          const qty = Number(row.qty ?? 0);
          const arrived = Math.round(qty * done);
          const ready = done >= 1;
          const label = row.job || row.piece || row.ref || `Row ${index + 1}`;
          return (
            <div role="listitem" key={`${id}-${index}`} className="card-live p-2.5">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="min-w-0">
                  <p className="truncate text-[0.75rem] font-semibold text-text-primary">
                    {label}
                    {row.glass ? ` · ${row.glass}` : row.spec ? ` · ${row.spec}` : ""}
                  </p>
                  <p className="truncate text-[0.75rem] text-text-faint font-medium">
                    {row.party} · arrived at {active}: {arrived} of {qty} pieces
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <div className="h-1.5 w-20 overflow-hidden rounded-full bg-ink-800 sm:w-28">
                    <span
                      className="block h-full rounded-full bg-gradient-to-r from-brand-500 to-glass-cyan transition-[width] duration-500"
                      style={{ width: `${Math.round(done * 100)}%` }}
                    />
                  </div>
                  <span className="w-9 text-right font-mono text-[0.75rem] tabular-nums text-text-secondary">
                    {Math.round(done * 100)}%
                  </span>
                  <button
                    type="button"
                    onClick={() => advance(id)}
                    disabled={ready}
                    className={cn(
                      "rounded-full border px-2 py-0.5 text-[0.6875rem] font-semibold uppercase tracking-[0.1em] transition-colors",
                      ready
                        ? "border-money-positive/40 text-money-positive"
                        : "border-line-strong/70 text-text-muted hover:border-brand-400/50 hover:text-text-primary",
                    )}
                  >
                    {ready ? "Ready" : "Advance"}
                  </button>
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {!compact ? (
        <p className="text-[0.75rem] leading-relaxed text-text-faint font-medium">
          {demo.blurb} A job becomes dispatch-ready when the full quantity has finished the chain — press Advance to
          walk one through.
        </p>
      ) : null}
    </div>
  );
}

/* ══════════════════════════════════════════════════════════════════════════
 * Wizard engine — clickable step rail
 * ════════════════════════════════════════════════════════════════════════ */

function WizardEngine({ demo, view, compact }: { demo: Demo; view: DemoView | undefined; compact?: boolean }) {
  const steps = demo.steps ?? [];
  const [step, setStep] = useState(1);

  return (
    <div className="flex h-full flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SampleTag />
        <p className="font-mono text-[0.6875rem] uppercase tracking-[0.14em] text-brand-300">
          Step {step} of {steps.length || 1}
        </p>
      </div>

      <div role="list" aria-label="Steps" className="flex flex-wrap gap-1.5">
        {steps.map((label, index) => {
          const isActive = index + 1 === step;
          return (
            <div role="listitem" key={`${label}-${index}`}>
              <button
                type="button"
                onClick={() => setStep(index + 1)}
                className={cn(
                  "flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[0.75rem] font-medium transition-colors duration-200",
                  isActive
                    ? "border-brand-400/60 bg-brand-400/16 text-brand-100"
                    : index + 1 < step
                      ? "border-money-positive/35 text-money-positive"
                      : "border-line-strong/60 text-text-muted hover:border-brand-400/40 hover:text-text-primary",
                )}
              >
                <span aria-hidden="true" className="font-mono text-[0.625rem] opacity-70">
                  {String(index + 1).padStart(2, "0")}
                </span>
                {label}
              </button>
            </div>
          );
        })}
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <TableEngine view={view} compact={compact} />
      </div>

      {!compact ? <p className="text-[0.75rem] leading-relaxed text-text-faint font-medium">{demo.footnote ?? demo.blurb}</p> : null}
    </div>
  );
}

/* ══════════════════════════════════════════════════════════════════════════
 * Dashboard engine — the range toggle drives a real recomputation
 * ════════════════════════════════════════════════════════════════════════ */

const ranges = ["Today", "This week", "This month"] as const;

function DashboardEngine({ demo, compact }: { demo: Demo; compact?: boolean }) {
  const [range, setRange] = useState<(typeof ranges)[number]>("This week");
  const factor = range === "Today" ? 0.22 : range === "This week" ? 1 : 3.9;
  const summary = demo.views[0]?.summary ?? [];
  const bars = [0.42, 0.66, 0.38, 0.82, 0.55, 0.94, 0.7].map((value) => Math.min(1, value * (factor / 3.9) + 0.18));

  return (
    <div className="flex h-full flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SampleTag />
        <div className="flex gap-1" role="group" aria-label="Range">
          {ranges.map((entry) => (
            <button
              key={entry}
              type="button"
              aria-pressed={entry === range}
              onClick={() => setRange(entry)}
              className={cn(
                "rounded-full px-2.5 py-0.5 text-[0.6875rem] font-semibold uppercase tracking-[0.1em] transition-colors",
                entry === range
                  ? "bg-brand-400/18 text-brand-100 ring-1 ring-inset ring-brand-400/40"
                  : "text-text-faint hover:text-text-primary",
              )}
            >
              {entry}
            </button>
          ))}
        </div>
      </div>

      <dl className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        {summary.map((item, index) => {
          const numeric = Number(item.value);
          const scaled = Number.isFinite(numeric) ? Math.max(1, Math.round(numeric * factor)) : null;
          return (
            <div key={`${item.label}-${index}`} className="stat-tile p-2.5">
              <dt className="font-mono text-[0.625rem] uppercase tracking-[0.12em] text-text-faint font-medium">{item.label}</dt>
              <dd className="mt-1 font-mono text-lg font-semibold tabular-nums text-text-primary">
                {scaled !== null ? scaled : item.value}
              </dd>
              <dd className="mt-0.5 text-[0.6875rem] text-text-faint font-medium">
                {scaled !== null ? (index === 0 ? "across the period" : "across the range") : "—"}
              </dd>
            </div>
          );
        })}
      </dl>

      <div className="flex flex-1 items-end gap-1.5 rounded-card border border-line/60 bg-ink-950/40 p-3">
        {bars.map((value, index) => (
          <span
            key={index}
            className="flex-1 rounded-t-[3px] bg-gradient-to-t from-brand-600/70 to-glass-cyan/70 transition-[height] duration-500"
            style={{ height: `${Math.round(value * 100)}%` }}
            title={`Day ${index + 1}`}
          />
        ))}
      </div>

      {!compact && demo.footnote ? (
        <p className="text-[0.75rem] leading-relaxed text-text-faint font-medium">{demo.footnote}</p>
      ) : null}
    </div>
  );
}

/* ══════════════════════════════════════════════════════════════════════════
 * Form engine — field list; Settings gets the real appearance controls
 * ════════════════════════════════════════════════════════════════════════ */

function FormEngine({ moduleKey, demo, view }: { moduleKey: string; demo: Demo; view: DemoView | undefined }) {
  return (
    <div className="flex h-full flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SampleTag />
        <p className="font-mono text-[0.6875rem] uppercase tracking-[0.14em] text-text-faint font-medium">{moduleKey}</p>
      </div>

      <dl className="space-y-1.5">
        {(view?.summary ?? []).map((item) => (
          <div
            key={item.label}
            className="flex flex-wrap items-baseline justify-between gap-2 border-b border-line/40 pb-1.5 last:border-0"
          >
            <dt className="text-[0.75rem] font-medium text-text-secondary">{item.label}</dt>
            <dd className="font-mono text-[0.75rem] text-text-muted">{item.value}</dd>
          </div>
        ))}
      </dl>

      {view && view.rows.length > 0 ? (
        <div className="min-h-0 flex-1 overflow-y-auto">
          <div role="list" className="space-y-1">
            {view.rows.map((row, index) => {
              const cells = Object.entries(row);
              return (
                <div
                  role="listitem"
                  key={`field-${index}`}
                  className="flex flex-wrap items-baseline justify-between gap-2 rounded-lg px-2 py-1.5 hover:bg-ink-800/60"
                >
                  <span className="text-[0.75rem] text-text-secondary">{cells[0]?.[1]}</span>
                  <span className="font-mono text-[0.75rem] text-text-muted">{cells[1]?.[1]}</span>
                </div>
              );
            })}
          </div>
        </div>
      ) : null}

      {moduleKey === "Settings" ? <AppearanceDeck /> : null}

      <p className="text-[0.75rem] leading-relaxed text-text-faint font-medium">{demo.footnote ?? demo.blurb}</p>
    </div>
  );
}

/**
 * The application's four visual families, wired to this website's own theme.
 * Pressing one here re-themes the page — the fastest way to show what the
 * setting does inside the product.
 */
function AppearanceDeck() {
  const families = [
    { id: "studio", label: "Studio" },
    { id: "monet", label: "Monet" },
    { id: "industrial", label: "Industrial" },
    { id: "crystal", label: "Crystal" },
  ] as const;

  return (
    <div className="card-accent p-3">
      <p className="font-mono text-[0.6875rem] uppercase tracking-[0.14em] text-brand-200">Live · changes this site</p>
      <p className="mt-1.5 text-[0.75rem] leading-relaxed text-text-secondary">
        These are the application&apos;s four families. Pressing one re-themes this page, which is exactly what it does
        to the application.
      </p>
      <div className="mt-3 flex flex-wrap gap-1.5">
        {families.map((entry) => (
          <FamilyButton key={entry.id} id={entry.id} label={entry.label} />
        ))}
      </div>
    </div>
  );
}

function FamilyButton({ id, label }: { id: string; label: string }) {
  return (
    <button
      type="button"
      onClick={() => {
        document.documentElement.setAttribute("data-appearance", id);
        try {
          window.localStorage.setItem("proglass-appearance", id);
        } catch {
          /* Storage may be unavailable; the attribute still applies. */
        }
        window.dispatchEvent(new Event("proglass-theme"));
      }}
      className="rounded-full border border-line-strong/70 bg-ink-900/70 px-2.5 py-1 text-[0.75rem] font-medium text-text-secondary transition-colors hover:border-brand-400/50 hover:text-text-primary"
    >
      {label}
    </button>
  );
}

/* ══════════════════════════════════════════════════════════════════════════
 * Fallback
 * ════════════════════════════════════════════════════════════════════════ */

function EmptyPlate() {
  return (
    <div className="flex h-full min-h-[10rem] flex-col items-center justify-center gap-2 rounded-card border border-dashed border-line-strong/60 bg-ink-950/40 p-5 text-center">
      <p className="text-[0.75rem] font-semibold text-text-secondary">This screen has no preview yet.</p>
      <p className="max-w-sm text-[0.75rem] leading-relaxed text-text-faint font-medium">
        The module exists in the application. Its preview is added once the layout is confirmed.
      </p>
    </div>
  );
}

export type { DemoColumn, DemoRow };
