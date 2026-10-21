"use client";

import { useEffect, useMemo, useState } from "react";
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
import { SampleTag, SearchBox } from "@/components/product/previewAtoms";
import { SpecificationSurface, type SpecBlockInput } from "@/components/product/SpecificationSurface";
import { ProfitLossSurface } from "@/components/product/ProfitLossSurface";
import { LogSurface } from "@/components/product/LogSurface";
import { StageQueueSurface } from "@/components/product/StageQueueSurface";
import { stageChain, type StageChainId } from "@/data/stageQueue";
import { SheetGallery } from "@/components/optimization/SheetGallery";
import { PlateCad } from "@/components/optimization/PlateCad";
import { OptimizerSettingsSurface } from "@/components/optimization/OptimizerSettingsSurface";
import { enginePipeline } from "@/data/optimization";
import { packGuillotine, plateViews } from "@/lib/guillotinePack";
import { useT } from "@/i18n/provider";
import { NoTranslate } from "@/components/i18n/NoTranslate";
import { useAppearance } from "@/lib/useTheme";
import { setFamily } from "@/lib/useStudio";

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
  /**
   * Which screen of the module to draw, when the caller knows.
   *
   * Optimization has three tabs on the site and three screens in the
   * application, and they are not the same screen: the engine configuration,
   * the 2D view, and the report. Without this the slides were one preview shown
   * three times under three different captions.
   */
  variant,
}: {
  moduleKey: string;
  name: string;
  capabilities?: string[];
  className?: string;
  compact?: boolean;
  variant?: string;
}) {
  const t = useT();
  const demo = useMemo(() => demoFor(moduleKey, name, capabilities), [moduleKey, name, capabilities]);
  const [viewIndex, setViewIndex] = useState(0);

  const views = demo.views.length > 0 ? demo.views : [];
  const safeIndex = Math.min(viewIndex, Math.max(views.length - 1, 0));
  const view = views[safeIndex];

  return (
    <div className={cn("flex flex-col", className)}>
        {/* ── Tabs ─────────────────────────────────────────────────────── */}
        {views.length > 1 && demo.kind !== "wizard" ? (
          <div role="tablist" aria-label={`${name} views`} className="flex flex-wrap gap-1.5 border-b border-line/50 pb-2.5">
            {views.map((entry, index) => {
              const active = index === safeIndex;
              return (
                <button
                  key={`${t(entry.label)}-${index}`}
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
                  {t(entry.label)}
                </button>
              );
            })}
          </div>
        ) : null}

        {/* ── Engine ───────────────────────────────────────────────────── */}
        <div className="pt-3">
          {demo.kind === "log" ? (
            <LogSurface compact={compact} />
          ) : demo.kind === "optimizer" ? (
            variant === "settings" ? (
              <OptimizerSettingsSurface />
            ) : (
              <OptimizerEngine
                demo={demo}
                compact={compact}
                view={variant === "report" ? "report" : "engine"}
              />
            )
          ) : demo.kind === "stage" ? (
            <StageQueueSurface stage={stageFromKey(moduleKey)} compact={compact} />
          ) : demo.kind === "queue" ? (
            <QueueEngine demo={demo} compact={compact} />
          ) : demo.kind === "wizard" ? (
            <WizardEngine demo={demo} view={view} onViewIndex={setViewIndex} compact={compact} />
          ) : demo.kind === "dashboard" ? (
            <DashboardEngine demo={demo} view={view} viewIndex={safeIndex} onViewIndex={setViewIndex} compact={compact} />
          ) : demo.kind === "form" ? (
            <FormEngine moduleKey={moduleKey} demo={demo} view={view} />
          ) : (
            <TableEngine view={view} compact={compact} />
          )}
      </div>
    </div>
  );
}


/**
 * The eight stage modules are separate pages in the application, so the surface
 * needs to know which one it is drawing. The chain's own ids are the module
 * keys, which is why this is a lookup rather than a mapping table.
 */
function stageFromKey(moduleKey: string): StageChainId {
  const match = stageChain.find((stage) => stage.id === moduleKey);
  return match ? match.id : "Cutting";
}

/* ══════════════════════════════════════════════════════════════════════════
 * Shared chrome
 * ════════════════════════════════════════════════════════════════════════ */

/*
 * The pill colours itself from the status the application stores, and prints
 * the status the reader understands. Those are two different strings once the
 * page is in another language — "Ready to dispatch" becomes "جاهز للإرسال" and
 * no colour rule would match it — so the English value keeps doing the colour
 * test while the translated one is what is shown.
 */
function StatusPill({ value, tone }: { value: string; tone?: string }) {
  const probe = tone ?? value;
  const positive = /ready|complete|settled|delivered|active|ok|pass/i.test(probe);
  const warning = /warn|pending|part|transit|loaded/i.test(probe);

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

function TableEngine({
  view,
  compact,
}: {
  view: DemoView | undefined;
  compact?: boolean;
}) {
  const t = useT();
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
        <SearchBox value={query} onChange={setQuery} label={t("Filter rows")} />
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
                    {t(column.label)}
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
              const id = `${t(view.label)}-${index}-${row[columns[0]?.key ?? "ref"] ?? index}`;
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
                      {column.kind === "status" ? (
                        <StatusPill
                          value={t(String(row[column.key] ?? ""))}
                          tone={String(row[column.key] ?? "")}
                        />
                      ) : (
                        t(String(row[column.key] ?? ""))
                      )}
                    </td>
                  ))}
                </tr>
              );
            })}
            {visible.length === 0 ? (
              <tr>
                <td colSpan={columns.length} className="px-2 py-6 text-center text-[0.75rem] text-text-faint">
                  {t("No rows match “{query}”.", { query: query })}
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>

      {!compact ? (
        <div className="mt-2.5 flex flex-wrap items-center justify-between gap-3 border-t border-line/50 pt-2.5">
          <p className="text-[0.75rem] text-text-faint">
            {t("{shown} of {total} rows", { shown: visible.length, total: rows.length })}
            {selected ? t(" · one row selected") : ""}
          </p>
          {view.summary && view.summary.length > 0 ? (
            <dl className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
              {view.summary.map((item) => (
                <div key={t(item.label)} className="flex items-baseline gap-1.5">
                  <dt className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">
                    {t(item.label)}
                  </dt>
                  <dd
                    className={cn(
                      "font-mono text-[0.75rem] font-semibold tabular-nums",
                      item.money ? "text-money-neutral" : "text-text-primary",
                    )}
                  >
                    {t(item.value)}
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
 * Optimizer engine — the real packer over the sample cut list
 * ════════════════════════════════════════════════════════════════════════ */

/**
 * The margins the sample run is cut to.
 *
 * Mapped once, here, from the shape the data file uses into the shape the
 * packer takes — and it is the application's house Thin profile: 15 mm on all
 * four edges and a 3 mm blade gap for 6 mm glass. The preview used to be drawn
 * 15/15/3/3 with a 4 mm kerf, which is not a profile the application has for
 * any thickness, so the plate on screen showed trims the table would not make.
 */
const SAMPLE_MARGINS = {
  left: optimizerLimits.marginLeft,
  right: optimizerLimits.marginRight,
  top: optimizerLimits.marginTop,
  bottom: optimizerLimits.marginBottom,
  kerf: optimizerLimits.kerf,
};

function OptimizerEngine({
  demo,
  compact,
  view = "engine",
}: {
  demo: Demo;
  compact?: boolean;
  /** `engine` is the 2D view; `report` is the industrial report the run prints. */
  view?: "engine" | "report";
}) {
  const t = useT();
  const [sheetIndex, setSheetIndex] = useState(1);
  const [piecesPerPart, setPiecesPerPart] = useState(1);
  /**
   * A run has three states, and the preview starts in the first one: a cutting
   * list with nothing computed from it. The application behaves the same way —
   * the optimization tabs are empty until a run has been asked for, and they say
   * so ("Run optimization to generate 2D sheet layouts").
   */
  const [phase, setPhase] = useState<"idle" | "running" | "done">("idle");
  /** How far through the engine's own pipeline the run is. */
  const [stage, setStage] = useState(0);
  const [run, setRun] = useState(0);
  /** Which plate the canvas is showing. The gallery sets it. */
  const [focused, setFocused] = useState(1);

  const sheet = sheetSizes[sheetIndex] ?? sheetSizes[1]!;

  const parts = useMemo(() => {
    const base = demo.views[0]?.rows ?? [];
    return base.flatMap((row) => {
      const w = Number(row.w ?? 0);
      const h = Number(row.h ?? 0);
      /*
       * The list's own quantity, times the slider. The cap is the largest
       * quantity in the list (10) times the slider's own maximum (4), so the
       * control can never quietly drop a piece the visitor asked for.
       */
      const qty = Math.max(1, Number(row.qty ?? 1) * piecesPerPart);
      return Array.from({ length: Math.min(qty, 40) }, (_, i) => ({
        piece: `${row.piece ?? "P"}-${i + 1}`,
        w,
        h,
      }));
    });
  }, [demo.views, piecesPerPart]);

  const sizes = demo.views[0]?.rows.length ?? 0;

  /*
   * Nothing is packed until a run has been asked for. That is not a loading
   * trick: it is the difference between showing a visitor a result they did not
   * ask for and letting them produce one. Everything below this line is
   * therefore `null` in the idle state, and the page carries no plate drawing,
   * no yield figure and no schedule until the run completes.
   *
   * The packing itself lives in `src/lib/guillotinePack.ts`, shared with the
   * homepage and the optimization page so every plate drawing on this site comes
   * out of one implementation and they cannot disagree with each other. It is a
   * two-way split — each leftover L-shape divided by one straight cut, so every
   * boundary stays a cut a guillotine table can make — used to illustrate how a
   * run spreads across plates. The engine is not that: it searches five families
   * and six plate patterns. Everything printed here is arithmetic over the
   * pieces actually placed, and a piece that no plate can hold is reported
   * rather than dropped.
   */
  const { result, plates } = useMemo(() => {
    if (phase === "idle") return { result: null, plates: [] as ReturnType<typeof plateViews> };
    const packed = packGuillotine(
      parts.map((part) => ({ id: part.piece, width: part.w, height: part.h })),
      { width: sheet.w, height: sheet.h },
      SAMPLE_MARGINS,
    );
    return {
      result: packed,
      plates: plateViews(packed, { width: sheet.w, height: sheet.h, label: `${sheet.w} × ${sheet.h}` }),
    };
  }, [phase, parts, sheet]);

  /*
   * The run walks the engine's own stage list, then reports. Every state change
   * happens inside the timer — a stage tick, then the hand-off to the result —
   * so the effect itself never renders synchronously.
   */
  useEffect(() => {
    if (phase !== "running") return;
    const finished = stage >= enginePipeline.length;
    const timer = setTimeout(
      () => {
        if (finished) setPhase("done");
        else setStage((value) => value + 1);
      },
      finished ? 420 : stage === 0 ? 220 : 260,
    );
    return () => clearTimeout(timer);
  }, [phase, stage]);

  const beginRun = () => {
    setRun((value) => value + 1);
    setStage(0);
    setFocused(1);
    setPhase("running");
  };

  /* Changing an input invalidates the result, so the run has to be made again. */
  const changeInput = (change: () => void) => {
    change();
    setPhase("idle");
    setStage(0);
  };

  const activePlate = plates.find((entry) => entry.plate === focused) ?? plates[0];
  const partArea = parts.reduce((total, part) => total + (part.w * part.h) / 1_000_000, 0);
  const stockArea = result?.stockAreaSqm ?? 0;
  const usedArea = result?.partAreaSqm ?? 0;
  const utilization = result?.utilizationPct ?? 0;
  const wastage = stockArea - usedArea;
  const scrap = stockArea - partArea;
  const progress = Math.min(1, stage / (enginePipeline.length + 1));

  return (
    <div className="flex h-full flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SampleTag />
        <button
          type="button"
          onClick={beginRun}
          disabled={phase === "running"}
          className="studio-button studio-button-primary"
        >
          <span
            aria-hidden="true"
            className="h-1.5 w-1.5 rounded-full bg-white"
            style={{ opacity: phase === "running" ? 1 : 0.6 }}
          />
          {phase === "idle"
            ? t("Run optimizer")
            : phase === "running"
              ? t("Optimizing…")
              : t("Re-run · pass {pass}", { pass: run + 1 })}
        </button>
      </div>

      {/* ── The inputs, in the application's own controls ─────────────── */}
      <div className="studio-sheet flex flex-wrap items-end gap-4 px-3 py-3">
        <label className="flex flex-col gap-1">
          <span className="studio-label">{t("Sheet")}</span>
          <select
            value={sheetIndex}
            onChange={(event) => changeInput(() => setSheetIndex(Number(event.target.value)))}
            className="studio-inset px-2 py-1 text-[0.75rem]"
          >
            {sheetSizes.map((entry, index) => (
              <option key={`${t(entry.label)}-${index}`} value={index}>
                {t(entry.label)}
              </option>
            ))}
          </select>
        </label>

        <label className="flex min-w-[9rem] flex-1 flex-col gap-1">
          <span className="studio-label">
            {t("Pieces per size · {piecesPerPart}", { piecesPerPart: piecesPerPart })}
          </span>
          <input
            type="range"
            min={1}
            max={4}
            step={1}
            value={piecesPerPart}
            onChange={(event) => changeInput(() => setPiecesPerPart(Number(event.target.value)))}
            className="accent-brand-400"
          />
        </label>

        <span className="studio-chip">
          {t("{v1} sizes · {v2} pieces", { v1: sizes, v2: parts.length })}
        </span>
        <span className="studio-chip">
          {t("trim {v1} · kerf {v2} mm", { v1: SAMPLE_MARGINS.left, v2: SAMPLE_MARGINS.kerf })}
        </span>
      </div>

      {/* ── Before a run: the cutting list, and nothing else ──────────── */}
      {phase === "idle" ? (
        <div className="studio-sheet flex min-h-0 flex-1 flex-col p-3">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <p className="font-mono text-[0.6875rem] uppercase tracking-[0.12em] font-semibold" style={{ color: "var(--cad-muted)" }}>
              {t("Cutting list · the sample job")}
            </p>
            <p className="studio-label">
              {t("{v1} sizes · {v2} pieces on the table", { v1: sizes, v2: parts.length })}
            </p>
          </div>

          <div className="studio-inset mt-2 min-h-0 flex-1 overflow-auto p-1">
            <table className="studio-table w-full min-w-[22rem] border-separate border-spacing-0 text-[0.75rem]">
              <caption className="sr-only">{t("The twenty-two sizes this sample job is cut from")}</caption>
              <thead>
                <tr className="text-left">
                  {["Piece", "Room", "W", "H", "Qty"].map((heading) => (
                    <th
                      key={heading}
                      scope="col"
                      className="sticky top-0 px-2 py-1.5 font-mono text-3xs uppercase tracking-[0.1em] font-medium"
                      style={{ backgroundColor: "var(--cad-canvas)" }}
                    >
                      {t(heading)}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {(demo.views[0]?.rows ?? []).map((row) => (
                  <tr key={String(row.piece)}>
                    <td className="px-2 py-1 font-semibold">{row.piece}</td>
                    <td className="px-2 py-1" style={{ color: "var(--cad-muted)" }}>
                      {row.room}
                    </td>
                    <td className="px-2 py-1 text-right font-mono tabular-nums">{row.w}</td>
                    <td className="px-2 py-1 text-right font-mono tabular-nums">{row.h}</td>
                    <td className="px-2 py-1 text-right font-mono tabular-nums">{row.qty}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <p className="mt-3 text-[0.75rem] leading-relaxed font-medium" style={{ color: "var(--cad-subtle)" }}>
            {t("Nothing has been computed yet — the application opens the same way, with the optimization tabs empty until a run is asked for. Press Run optimizer and the plan, the yield and the cut schedule appear together when the run completes.")}
          </p>
        </div>
      ) : null}

      {/* ── During a run: the engine's own stages ─────────────────────── */}
      {phase === "running" ? (
        <div className="studio-sheet flex flex-1 flex-col justify-center gap-4 p-5">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <p className="font-mono text-sm font-semibold" style={{ color: "var(--cad-text)" }}>
              {t("Optimization running…")}
            </p>
            <span className="studio-chip">{Math.round(progress * 100)}%</span>
          </div>
          <div className="studio-track" role="progressbar" aria-label={t("Optimization progress")} aria-valuenow={Math.round(progress * 100)} aria-valuemin={0} aria-valuemax={100}>
            <span style={{ width: `${Math.round(progress * 100)}%`, transition: "width 260ms linear" }} />
          </div>
          <ol className="space-y-2">
            {enginePipeline.map((entry, index) => (
              <li key={`${entry.step}-${index}`} className="flex items-start gap-3">
                <span
                  aria-hidden="true"
                  className="mt-1.5 h-2 w-2 shrink-0 rounded-full"
                  style={{
                    backgroundColor: index <= stage ? "var(--cad-accent)" : "var(--cad-line)",
                  }}
                />
                <span
                  className="text-[0.75rem] leading-relaxed"
                  style={{ color: index <= stage ? "var(--cad-text)" : "var(--cad-subtle)" }}
                >
                  <span className="font-semibold">{t(entry.step)}</span>
                  {" — "}
                  {t(entry.detail)}
                </span>
              </li>
            ))}
          </ol>
          <p className="text-[0.6875rem] leading-relaxed font-medium" style={{ color: "var(--cad-subtle)" }}>
            {t("These are the stages the application reports its search through. This preview computes the plan in the browser the moment it starts, so what you are watching is the sequence, not a long calculation.")}
          </p>
        </div>
      ) : null}

      {/* ── After a run: the result ───────────────────────────────────── */}
      {phase === "done" && result ? (
        <>
          {view === "report" ? (
            /*
             * The industrial report the run produces: the nested sheets summary,
             * which is the section an office reads — one row per plate, its
             * stock, what went on it and what it yielded — over the run's own
             * totals.
             */
            <div className="studio-sheet p-3">
              <div className="flex flex-wrap items-center justify-between gap-2 px-1 pb-2">
                <p className="font-mono text-[0.6875rem] uppercase tracking-[0.12em] font-semibold" style={{ color: "var(--cad-muted)" }}>
                  {t("Nested sheets summary")}
                </p>
                <span className="studio-label">
                  {t("trim {v1} · kerf {v2} mm", { v1: SAMPLE_MARGINS.left, v2: SAMPLE_MARGINS.kerf })}
                </span>
              </div>
              <div className="studio-inset max-h-[24rem] overflow-auto p-1">
                <table className="studio-table w-full min-w-[24rem] border-separate border-spacing-0 text-[0.75rem]">
                  <caption className="sr-only">
                    {t("Every plate of the sample run, with what it carries and what it yielded")}
                  </caption>
                  <thead>
                    <tr className="text-left">
                      {["Sheet", "Stock", "Pieces", "Used", "Yield"].map((heading) => (
                        <th
                          key={heading}
                          scope="col"
                          className="sticky top-0 px-2 py-1.5 font-mono text-3xs uppercase tracking-[0.1em] font-medium"
                          style={{ backgroundColor: "var(--cad-canvas)" }}
                        >
                          {t(heading)}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {plates.map((entry) => (
                      <tr key={entry.plate}>
                        <td className="px-2 py-1.5 font-semibold">{entry.plate}</td>
                        <td className="px-2 py-1.5 font-mono" style={{ color: "var(--cad-muted)" }}>
                          {entry.stockLabel}
                        </td>
                        <td className="px-2 py-1.5 text-right font-mono tabular-nums">{entry.pieceCount}</td>
                        <td className="px-2 py-1.5 text-right font-mono tabular-nums">{`${entry.usedSqm} m²`}</td>
                        <td className="px-2 py-1.5 text-right font-mono font-semibold tabular-nums" style={{ color: "var(--cad-accent-deep)" }}>
                          {`${entry.utilizationPct.toFixed(1)}%`}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <p className="px-1 pt-2 text-[0.6875rem] leading-relaxed font-medium" style={{ color: "var(--cad-subtle)" }}>
                {t("The report prints four sections: the industrial cut schedule in execution order, this nested sheets summary, the merged specifications the batch was built from, and the stock plates the run consumed.")}
              </p>
            </div>
          ) : (
            <>
              {/*
                The plate itself, drawn the way the 2D View tab draws it: on the
                application's light studio panel, to scale, with the edge trims,
                the breakout band, the pieces labelled with their references, cut
                sizes and areas, their cut-sequence tags, and the remnant that
                goes back on the rack.
              */}
              {activePlate ? (
                <div className="studio-sheet p-2">
                  <div className="-mx-1 overflow-x-auto px-1">
                    <div className="studio-stage p-1">
                      <PlateCad
                        t={t}
                        plate={activePlate}
                        margins={SAMPLE_MARGINS}
                        minBreakoutMm={optimizerLimits.minBreakout}
                        density="inspect"
                        className="block min-w-[24rem] rounded-[0.35rem]"
                      />
                    </div>
                  </div>
                  <div className="mt-2 flex flex-wrap items-center justify-between gap-2 px-1 pt-2" style={{ borderTop: "1px solid var(--cad-keyline)" }}>
                    <span className="studio-chip">
                      {t("Sheet {v1} of {v2}", { v1: activePlate.plate, v2: plates.length })}
                    </span>
                    <span className="studio-chip-yield">{t("Yield: {v1}", { v1: `${activePlate.utilizationPct.toFixed(1)}%` })}</span>
                    <span className="studio-label">
                      {t("{v1} pieces on this sheet", { v1: activePlate.pieceCount })}
                    </span>
                  </div>
                </div>
              ) : null}

              {/*
                Every plate of the run, as thumbnails, and each one focuses its
                sheet on the canvas above — the application's gallery, where a
                card opens its own layout.
              */}
              <SheetGallery
                plates={plates}
                margins={SAMPLE_MARGINS}
                minBreakoutMm={optimizerLimits.minBreakout}
                selectedPlate={activePlate?.plate}
                onSelectPlate={setFocused}
                pageSize={compact ? 4 : 6}
                paginate={!compact}
                columns={compact ? "grid-cols-2" : "grid-cols-2 sm:grid-cols-3"}
              />
            </>
          )}

          {result.unplaced.length > 0 ? (
            <p className="studio-chip-warning">
              {t("{v1} pieces could not be placed on a {v2} × {v3} plate — too large for the usable area in either orientation. Choose a larger sheet, or cut them from stock that can hold them.", {
                v1: result.unplaced.length,
                v2: sheet.w,
                v3: sheet.h,
              })}
            </p>
          ) : null}

          {/*
            The six figures the application's Summary tab reports for a run, in
            its order. Sheets used and utilization first, because those are the
            two a shop owner looks for; wastage, stock, part area and scrap after
            them. All six are computed from the pieces actually placed.
          */}
          {!compact ? (
            <dl className="grid grid-cols-3 gap-2 sm:grid-cols-6">
              {[
                { label: t("Sheets used"), value: String(result.plates) },
                { label: t("Utilization"), value: `${utilization.toFixed(1)}%`, tone: "accent" },
                { label: t("Wastage"), value: `${wastage.toFixed(2)} m²` },
                { label: t("Stock area"), value: `${stockArea.toFixed(2)} m²` },
                { label: t("Part area"), value: `${partArea.toFixed(2)} m²` },
                { label: t("Scrap"), value: `${scrap.toFixed(2)} m²` },
              ].map((item) => (
                <div key={item.label} className="studio-sheet px-2.5 py-2">
                  <dt className="studio-label text-[0.625rem]">{item.label}</dt>
                  <dd
                    className="mt-0.5 font-mono text-sm font-semibold tabular-nums"
                    style={{ color: item.tone === "accent" ? "var(--cad-accent-deep)" : "var(--cad-text)" }}
                  >
                    {item.value}
                  </dd>
                </div>
              ))}
            </dl>
          ) : null}

          <p className="text-[0.75rem] leading-relaxed font-medium" style={{ color: "var(--cad-subtle)" }}>
            {t("{v1} Arranged here with a two-way guillotine split over the sample cutting list, so the figures are genuinely calculated — change the sheet or the quantity and the result clears until you run it again.", { v1: t(demo.blurb) })}
          </p>
        </>
      ) : null}
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
  const t = useT();
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

      <div role="list" aria-label={t("Production stages")} className="flex flex-wrap gap-1.5">
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

      <div role="list" aria-label={t("Jobs in this stage")} className="min-h-0 flex-1 space-y-2 overflow-y-auto">
        {rows.map((row, index) => {
          const id = rowKey(row, index);
          const done = progress[id] ?? 0;
          const qty = Number(row.qty ?? 0);
          const arrived = Math.round(qty * done);
          const ready = done >= 1;
          const label = row.job || row.piece || row.ref || `Row ${index + 1}`;
          return (
            <div role="listitem" key={`${id}-${index}`} className="surface p-2.5">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="min-w-0">
                  <p className="truncate text-[0.75rem] font-semibold text-text-primary">
                    {label}
                    {row.glass ? ` · ${row.glass}` : row.spec ? ` · ${row.spec}` : ""}
                  </p>
                  <p className="truncate text-[0.75rem] text-text-faint font-medium">
                    {row.party}{t(" · arrived at {time}: {arrived} of {qty} pieces", { time: active, arrived, qty })}
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
                    {ready ? t("Ready") : t("Advance")}
                  </button>
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {!compact ? (
        <p className="text-[0.75rem] leading-relaxed text-text-faint font-medium">{t("{v1} A job becomes dispatch-ready when the full quantity has finished the chain — press Advance to walk one through.", { v1: t(demo.blurb) })}</p>
      ) : null}
    </div>
  );
}

/* ══════════════════════════════════════════════════════════════════════════
 * Wizard engine — clickable step rail
 * ════════════════════════════════════════════════════════════════════════ */

function WizardEngine({
  demo,
  view,
  onViewIndex,
  compact,
}: {
  demo: Demo;
  view: DemoView | undefined;
  onViewIndex: (index: number) => void;
  compact?: boolean;
}) {
  const t = useT();
  const steps = demo.steps ?? [];
  const [step, setStep] = useState(demo.defaultStep ?? 1);

  /**
   * The specification Step 5 edits, held here rather than inside the surface.
   *
   * Step 9 reads the same rows: edit a size on the specification and the
   * revenue, the glass cost, the margin and the verdict on the profit-and-loss
   * screen all move with it. In the application those two screens share one
   * engine, and a preview that pinned them apart would be lying about the part
   * that matters.
   */
  const [blocks, setBlocks] = useState<SpecBlockInput[]>(() => demo.spec?.blocks ?? []);

  /** The view a step owns, if it has one — Step 4's estimator, Step 5's grid, Step 9's P&L. */
  const stepViewIndex = demo.views.findIndex((entry) => entry.step === step);
  const activeView = demo.views[stepViewIndex] ?? view;
  const stepNote = demo.stepNotes?.[step - 1];

  function goToStep(next: number) {
    setStep(next);
    const index = demo.views.findIndex((entry) => entry.step === next);
    if (index >= 0) onViewIndex(index);
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SampleTag />
        <p className="font-mono text-[0.6875rem] uppercase tracking-[0.14em] text-brand-300">
          {compact ? (steps[step - 1] ?? `${step} of ${steps.length || 1}`) : `Step ${step} of ${steps.length || 1}`}
        </p>
      </div>

      {/*
        The three screens with an engine behind them, one click away.
        The nine-step rail is the application's own navigation and stays — but
        a visitor should not have to know that step 5 is the one worth opening,
        and in a card a nine-pill rail is a wall. These three are the screens
        the proforma is about.
      */}
      {demo.views.some((entry) => entry.step) ? (
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="font-mono text-[0.625rem] uppercase tracking-[0.14em] text-text-faint font-medium">
            {t("Live screens")}
          </span>
          {demo.views
            .filter((entry) => entry.step)
            .map((entry) => {
              const active = entry.step === step;
              return (
                <button
                  key={t(entry.label)}
                  type="button"
                  onClick={() => goToStep(entry.step!)}
                  aria-pressed={active}
                  className={cn(
                    "rounded-full border px-2.5 py-1 text-[0.75rem] font-medium transition-colors duration-200",
                    active
                      ? "border-brand-400/60 bg-brand-400/16 text-brand-100"
                      : "border-line-strong/60 bg-ink-800/60 text-text-muted hover:border-brand-400/40 hover:text-text-primary",
                  )}
                >
                  {t(entry.label)}
                </button>
              );
            })}
        </div>
      ) : null}

      {/*
        In a card the nine-step rail is a wall of pills and the live screens are
        what matter, so the rail is shown when there is room for it.
      */}
      {/*
        Rendered or not rendered, never hidden by a class: `flex` and `hidden`
        are both display utilities and which one wins is a stylesheet-order
        accident. In a card the rail is left out of the DOM entirely.
      */}
      {!compact ? (
      <div role="list" aria-label={t("Steps")} className="flex flex-wrap gap-1.5">
        {steps.map((label, index) => {
          const isActive = index + 1 === step;
          return (
            <div role="listitem" key={`${label}-${index}`}>
              <button
                type="button"
                onClick={() => goToStep(index + 1)}
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
      ) : null}

      <div className="min-h-0 flex-1 overflow-y-auto">
        {activeView?.step === 5 && demo.spec ? (
          <SpecificationSurface spec={demo.spec} blocks={blocks} onBlocks={setBlocks} compact={compact} />
        ) : activeView?.step === 9 && demo.pnl ? (
          <ProfitLossSurface pnl={demo.pnl} blocks={blocks} compact={compact} />
        ) : activeView ? (
          <TableEngine view={activeView} compact={compact} />
        ) : (
          <StepCard step={step} label={steps[step - 1] ?? ""} note={stepNote} />
        )}
      </div>

      {!compact ? <p className="text-[0.75rem] leading-relaxed text-text-faint font-medium">{t(demo.footnote ?? demo.blurb)}</p> : null}
    </div>
  );
}

/**
 * A wizard step that is not a grid.
 *
 * Steps 4, 5 and 9 carry the engine — the estimator's charges, the
 * specification grid, the profit-and-loss analysis. The rest are forms, and a
 * form shown as somebody else's table would be a worse lie than saying what is
 * on it. So each gets its own line, and the rail still moves.
 */
function StepCard({ step, label, note }: { step: number; label: string; note?: string }) {
  const t = useT();
  return (
    <div className="flex h-full flex-col justify-center rounded-xl border border-line/50 bg-ink-900/40 px-4 py-6">
      <p className="font-mono text-[0.6875rem] uppercase tracking-[0.14em] text-brand-300 font-medium">
        Step {String(step).padStart(2, "0")} · {label}
      </p>
      <p className="mt-2 max-w-[60ch] text-[0.8125rem] leading-relaxed text-text-secondary font-medium">{t(note ?? "")}</p>
      <p className="mt-3 text-[0.6875rem] text-text-faint font-medium">
        {t("Press 05 or 09 in the rail for the specification grid and the profit-and-loss analysis — those two screens are the live ones.")}
      </p>
    </div>
  );
}


/* ══════════════════════════════════════════════════════════════════════════
 * Dashboard engine — the range toggle drives a real recomputation
 * ════════════════════════════════════════════════════════════════════════ */

const ranges = ["Today", "This week", "This month"] as const;

/**
 * The dashboard — one pulse per surface, over a tab per module.
 *
 * The dashboard is not a single screen of four counters. It is an overview with
 * a tab strip, and each tab is a different module's dashboard: the quote and
 * production pulses, the dispatch and stock pulses, the collections pulse. The
 * engine below used to render `demo.views[0]` and nothing else, so the tab strip
 * above it moved and the panel never did — which is what "not updating as per
 * switch the tab" describes. It now reads the active view, so choosing a tab
 * changes the tiles, the chart and the footnote together.
 *
 * The tiles are the application's own panel titles and counts. They are not
 * scaled by the range control: the figures in this preview are the seeded
 * workflow's — one confirmed inquiry, one approved proforma of 22 items in 3
 * groups, one released job, one partial delivery of 15 items and one invoice —
 * and multiplying a real document count by 3.9 to make a range selector feel
 * alive would be inventing trade the shop never did. The range says what it
 * does instead.
 */
/*
 * `TabTitle` → `TabSubtitle`, from the seven dashboard page view models.
 *
 * The tab strip carries the titles; the subtitle belongs to the panel the tab
 * opens, which is where the desktop application prints it.
 */
const TAB_SUBTITLES: Record<string, string> = {
  Overview: "Business at a glance",
  "Sheet Store": "Stock and inventory value",
  "Proforma Invoice": "Quotes and conversion",
  "Job Orders": "Production workload",
  Deliveries: "Dispatch and balance",
  "Tax Invoices": "Revenue and ageing",
  Statements: "Customer balances",
};

function DashboardEngine({
  demo,
  view,
  viewIndex,
  onViewIndex,
  compact,
}: {
  demo: Demo;
  view: DemoView | undefined;
  viewIndex: number;
  onViewIndex: (index: number) => void;
  compact?: boolean;
}) {
  const t = useT();
  const [range, setRange] = useState<(typeof ranges)[number]>("This week");
  const summary = view?.summary ?? [];
  const rows = view?.rows ?? [];
  /*
   * The bar chart is the seeded job's own quantities — the three specification
   * groups' piece counts — normalised, rather than seven invented days.
   */
  const bars = useMemo(() => {
    const pieces = [45, 24, 14];
    const peak = Math.max(...pieces);
    return pieces.map((value) => value / peak);
  }, []);

  return (
    <div className="flex h-full min-h-0 flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SampleTag />
        <div className="flex gap-1" role="group" aria-label={t("Period")}>
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

      {/*
        The tab strip the dashboard's own rail carries. It is drawn here rather
        than only in the frame's chrome because on this module the tabs *are* the
        screen: each one is a different module's dashboard, and switching one has
        to change what is below it.
      */}
      {demo.views.length > 1 ? (
        <div
          role="tablist"
          aria-label={t("Dashboard tabs")}
          className="flex flex-wrap gap-1.5 rounded-xl border border-line/60 bg-ink-950/40 p-2"
        >
          {demo.views.map((entry, index) => (
            <button
              key={t(entry.label)}
              type="button"
              role="tab"
              aria-selected={index === viewIndex}
              onClick={() => onViewIndex(index)}
              title={t(TAB_SUBTITLES[entry.label] ?? entry.label)}
              className={cn(
                "rounded-lg border px-2.5 py-1 text-[0.75rem] font-semibold transition-colors duration-200",
                index === viewIndex
                  ? "border-brand-400/60 bg-brand-400/16 text-brand-100"
                  : "border-line-strong/50 bg-ink-900/60 text-text-muted hover:border-brand-400/40 hover:text-text-primary",
              )}
            >
              {t(entry.label)}
            </button>
          ))}
        </div>
      ) : null}

      {/*
        The panel's own title and subtitle, as `DashboardPageBase` carries them.
        A tab that changed only the tiles looked like the same screen with
        different numbers; the heading changes with it, which is what a page
        switch looks like.
      */}
      {view ? (
        <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5 rounded-xl border border-line/60 bg-ink-900/40 px-3 py-2">
          <p className="text-[0.8125rem] font-semibold text-text-primary">{t(view.label)}</p>
          <p className="font-mono text-3xs uppercase tracking-[0.08em] text-text-muted font-medium">
            {t(TAB_SUBTITLES[view.label] ?? "Module dashboard")}
          </p>
        </div>
      ) : null}

      <dl className="kpi-row grid grid-cols-2 gap-2 lg:grid-cols-4">
        {summary.map((item, index) => (
          <div key={`${t(item.label)}-${index}`} className="surface-raised p-2.5">
            <dt className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">{t(item.label)}</dt>
            <dd className="mt-1 text-[0.8125rem] font-semibold leading-snug text-text-primary">{t(item.value)}</dd>
          </div>
        ))}
      </dl>

      {rows.length > 0 ? (
        <div className="max-h-[22rem] overflow-auto rounded-xl border border-line/60 bg-ink-950/40">
          <table className="w-full border-collapse text-left">
            <caption className="sr-only">
              {t.template`${view?.label ?? "Dashboard"} — the module's own panels and figures.`}
            </caption>
            <tbody>
              {rows.map((row, index) => {
                const cells = Object.entries(row);
                return (
                  <tr key={`row-${index}`} className="border-b border-line/30 last:border-0 hover:bg-ink-800/40">
                    <th scope="row" className="w-2/5 px-2.5 py-1.5 text-left text-[0.75rem] font-medium text-text-secondary">
                      {cells[0]?.[1]}
                    </th>
                    <td className="px-2.5 py-1.5 font-mono text-[0.75rem] text-text-muted">{cells[1]?.[1]}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="flex h-44 items-end gap-1.5 rounded-card border border-line/60 bg-ink-950/40 p-3 sm:h-52">
          {/* §X: this panel was `flex-1` inside an `h-full` column,
              so whenever the frame's workspace was stretched (the
              rail sets the row height in AppFrame) the chart ballooned
              into a several-hundred-pixel empty glass box under the
              tiles — dead paint area in exactly the region the device
              witness showed failing. A chart has a size; it keeps it. */}
          <p className="sr-only">{t("Piece counts for the dashboard's selected period.")}</p>
          {bars.map((value, index) => (
            <span
              key={index}
              className="flex-1 rounded-t-[3px] bg-gradient-to-t from-brand-600/70 to-glass-cyan/70 transition-[height] duration-500"
              style={{ height: `${Math.round(value * 100)}%` }}
              title={
                ["Main facade — 45 pieces", "Sun-exposed areas — 24 pieces", "Low-E / DGU zones — 14 pieces"][index] ??
                `Series ${index + 1}`
              }
            />
          ))}
        </div>
      )}

      {!compact && demo.footnote ? (
        <p className="text-[0.75rem] leading-relaxed text-text-faint font-medium">{t(demo.footnote)}</p>
      ) : null}

      {!compact ? (
        <p className="text-[0.6875rem] leading-relaxed text-text-faint font-medium">
          {t.template`Showing the ${range.toLowerCase()} period. The figures are the seeded workflow's own documents, so a wider period does not multiply them — the range control changes which period the dashboard is reading, not how much has been traded.`}
        </p>
      ) : null}
    </div>
  );
}

/* ══════════════════════════════════════════════════════════════════════════
 * Form engine — field list; Settings gets the real appearance controls
 * ════════════════════════════════════════════════════════════════════════ */

function FormEngine({ moduleKey, demo, view }: { moduleKey: string; demo: Demo; view: DemoView | undefined }) {
  const t = useT();
  return (
    <div className="flex h-full flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SampleTag />
        <NoTranslate as="p" className="font-mono text-[0.6875rem] uppercase tracking-[0.14em] text-text-faint font-medium">
          {moduleKey}
        </NoTranslate>
      </div>

      <dl className="space-y-1.5">
        {(view?.summary ?? []).map((item) => (
          <div
            key={t(item.label)}
            className="flex flex-wrap items-baseline justify-between gap-2 border-b border-line/40 pb-1.5 last:border-0"
          >
            <dt className="text-[0.75rem] font-medium text-text-secondary">{t(item.label)}</dt>
            <dd className="font-mono text-[0.75rem] text-text-muted">
              <NoTranslate>{t(item.value)}</NoTranslate>
            </dd>
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

      <p className="text-[0.75rem] leading-relaxed text-text-faint font-medium">{t(demo.footnote ?? demo.blurb)}</p>
    </div>
  );
}

/**
 * The application's four visual families, wired to this website's own theme.
 * Pressing one here re-themes the page — the fastest way to show what the
 * setting does inside the product.
 */
function AppearanceDeck() {
  const t = useT();
  const activeFamily = useAppearance();
  const families = [
    { id: "clay", label: t("Clay") },
    { id: "crystal", label: t("Crystal") },
    { id: "neumorphism", label: t("Neumorphism") },
    { id: "monet", label: t("Monet") },
    { id: "industrial", label: t("Industrial") },
  ] as const;

  return (
    <div className="surface-raised p-3">
      <p className="font-mono text-[0.6875rem] uppercase tracking-[0.14em] text-brand-200">{t("Live · changes this site")}</p>
      <p className="mt-1.5 text-[0.75rem] leading-relaxed text-text-secondary">{t("These are the application's four families. Pressing one re-themes this page, which is exactly what it does to the application.")}</p>
      <div className="mt-3 flex flex-wrap gap-1.5">
        {families.map((entry) => (
          <FamilyButton
            key={entry.id}
            id={entry.id}
            label={t(entry.label)}
            active={activeFamily === entry.id}
          />
        ))}
      </div>
    </div>
  );
}

function FamilyButton({
  id,
  label,
  active,
}: {
  id: "clay" | "crystal" | "neumorphism" | "monet" | "industrial";
  label: string;
  active: boolean;
}) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={() => setFamily(id)}
      className={cn(
        "rounded-full border px-2.5 py-1 text-[0.75rem] font-medium transition-colors",
        active
          ? "border-brand-400 bg-brand-500/20 text-text-primary"
          : "border-line-strong/70 bg-ink-900/70 text-text-secondary hover:border-brand-400/50 hover:text-text-primary",
      )}
    >
      {label}
    </button>
  );
}

/* ══════════════════════════════════════════════════════════════════════════
 * Fallback
 * ════════════════════════════════════════════════════════════════════════ */

function EmptyPlate() {
  const t = useT();
  return (
    <div className="flex h-full min-h-[10rem] flex-col items-center justify-center gap-2 rounded-card border border-dashed border-line-strong/60 bg-ink-950/40 p-5 text-center">
      <p className="text-[0.75rem] font-semibold text-text-secondary">{t("This screen has no preview yet.")}</p>
      <p className="max-w-sm text-[0.75rem] leading-relaxed text-text-faint font-medium">
        {t("The module exists in the application. Its preview is added once the layout is confirmed.")}
      </p>
    </div>
  );
}

export type { DemoColumn, DemoRow };
