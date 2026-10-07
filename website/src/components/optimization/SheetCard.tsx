import { cn } from "@/lib/cn";
import type { PackMargins, PlateView } from "@/lib/guillotinePack";
import { PlateCad } from "@/components/optimization/PlateCad";
import type { Translator } from "@/i18n/core";

/**
 * One plate of a run, drawn small.
 *
 * This is the website's version of the application's gallery card — the
 * miniature sheet in the 2D view's Multi-Sheet Gallery
 * (`Views/Optimization/Tabs/TwoDViewTabView.xaml`, the `OptGalleryCard`
 * template). Same parts, in the same order:
 *
 *   Sheet 7                     [ 78.1% ]   ← plate number, stock size, yield badge
 *   3210 × 2250 mm · 6 mm
 *   ┌──────────────────────────────────┐
 *   │  the plate, drawn to scale        │   ← the CAD miniature: trims, pieces,
 *   └──────────────────────────────────┘     their cut numbers, the remnant
 *   9 pieces · 4.21 m² used                 ← what is on it and what is left
 *   1.58 m² offcut
 *
 * The drawing is the same `PlateCad` the full-size sheet uses, at card density:
 * the plate's trims, breakout band, every placed piece and the reusable remnant
 * are all there, and a piece carries its cut number only where the miniature has
 * room for it — which is what the application's own miniature canvas does.
 *
 * When a gallery drives a canvas, every card becomes the control that focuses
 * its sheet on it: the application's "Inspect CAD →" in one click rather than
 * two. The markup is all spans so that the interactive form stays valid — a
 * button may not contain block content — and the non-interactive form is an
 * article, exactly as before.
 */
export function SheetCard({
  t,
  plate,
  margins,
  minBreakoutMm = 15,
  thicknessMm,
  compact = false,
  selected = false,
  onSelect,
  className,
}: {
  t: Translator;
  plate: PlateView;
  margins: PackMargins;
  minBreakoutMm?: number;
  /** Shown beside the stock size when the job is all one thickness. */
  thicknessMm?: number;
  /** The preview variant: no stock line, no offcut line, just number and yield. */
  compact?: boolean;
  selected?: boolean;
  /** Present when the gallery drives a canvas — makes the whole card the control. */
  onSelect?: () => void;
  className?: string;
}) {
  const inner = (
    <>
      {/* ── Header ─────────────────────────────────────────────────── */}
      <span className="flex items-center justify-between gap-2 border-b border-line/60 px-2.5 py-2">
        <span className="min-w-0">
          <span className="block truncate text-sm font-semibold leading-tight text-text-primary">
            {t("Sheet {v1}", { v1: plate.plate })}
          </span>
          {!compact ? (
            <span className="mt-0.5 block truncate font-mono text-[0.625rem] leading-tight text-text-faint">
              {plate.stockLabel}
              {thicknessMm ? ` · ${thicknessMm} mm` : ""}
            </span>
          ) : null}
        </span>
        {/*
          The yield badge. Accent-tinted rather than a traffic light: the site
          has no proven threshold above which a plan counts as good, so a green
          badge would be inventing one. It reports, it does not judge.
        */}
        <span className="shrink-0 rounded-full border border-brand-400/40 bg-brand-500/12 px-2 py-0.5 font-mono text-[0.625rem] font-semibold tabular-nums text-brand-200">
          {plate.utilizationPct.toFixed(1)}%
        </span>
      </span>

      {/* ── The plate, drawn to scale ──────────────────────────────── */}
      <span className="block p-2">
        <PlateCad
          t={t}
          plate={plate}
          margins={margins}
          minBreakoutMm={minBreakoutMm}
          density="card"
          showCallouts={false}
          className="rounded-[0.35rem] border border-line-strong/60 bg-ink-950/70"
        />
      </span>

      {/* ── Footer ─────────────────────────────────────────────────── */}
      <span className="mt-auto block border-t border-line/60 px-2.5 py-2">
        <span className="block text-xs font-medium leading-tight text-text-secondary">
          {plate.pieceCount === 1 ? t("1 piece") : t("{v1} pieces", { v1: plate.pieceCount })}
          {!compact ? <span className="text-text-faint">{` · ${plate.usedSqm} m²`}</span> : null}
        </span>
        {!compact ? (
          <span className="mt-0.5 block font-mono text-[0.625rem] leading-tight text-text-faint">
            {`${plate.offcutSqm} m² ${t("offcut")}`}
          </span>
        ) : null}
        {onSelect ? (
          <span className="mt-1.5 block font-mono text-[0.625rem] uppercase tracking-[0.12em] text-brand-200">
            {selected ? t("On the canvas") : t("Inspect CAD →")}
          </span>
        ) : null}
      </span>
    </>
  );

  if (onSelect) {
    return (
      <button
        type="button"
        onClick={onSelect}
        aria-pressed={selected}
        className={cn(
          "surface flex h-full flex-col overflow-hidden text-left transition-[border-color,transform] duration-200",
          "hover:-translate-y-0.5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-400",
          selected ? "border-brand-400/70 ring-1 ring-inset ring-brand-400/40" : "hover:border-brand-400/60",
          className,
        )}
      >
        {inner}
      </button>
    );
  }

  return <article className={cn("surface flex h-full flex-col overflow-hidden", className)}>{inner}</article>;
}
