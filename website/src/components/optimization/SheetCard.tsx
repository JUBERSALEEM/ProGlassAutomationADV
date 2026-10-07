import { cn } from "@/lib/cn";
import type { PackMargins, PlateView } from "@/lib/guillotinePack";
import { PlateCad } from "@/components/optimization/PlateCad";
import type { Translator } from "@/i18n/core";

/**
 * One plate of a run, drawn small.
 *
 * The application's gallery card — the miniature sheet in the 2D view's
 * Multi-Sheet Gallery (`Views/Optimization/Tabs/TwoDViewTabView.xaml`, the
 * `OptGalleryCard` template) — on the application's own light card:
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
 * The yield badge is the application's, in the application's success triad.
 * `OptSuccessBg / OptSuccessBorder / OptSuccessInk` paint the same badge on the
 * toolbar of its 2D view and on every one of its gallery cards, at whatever the
 * figure says — it is the screen's label, not a judgement this site is making.
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
      <span className="flex items-center justify-between gap-2 border-b px-2.5 py-2" style={{ borderColor: "var(--cad-keyline)" }}>
        <span className="min-w-0">
          <span className="block truncate text-sm font-semibold leading-tight" style={{ color: "var(--cad-text)" }}>
            {t("Sheet {v1}", { v1: plate.plate })}
          </span>
          {!compact ? (
            <span className="mt-0.5 block truncate font-mono text-[0.625rem] leading-tight" style={{ color: "var(--cad-subtle)" }}>
              {plate.stockLabel}
              {thicknessMm ? ` · ${thicknessMm} mm` : ""}
            </span>
          ) : null}
        </span>
        <span className="studio-chip-yield shrink-0 text-[0.625rem]">
          {plate.utilizationPct.toFixed(1)}%
        </span>
      </span>

      {/* ── The plate, drawn to scale ──────────────────────────────── */}
      <span className="block p-2">
        <span className="studio-stage block p-[0.15rem]">
          <PlateCad
            t={t}
            plate={plate}
            margins={margins}
            minBreakoutMm={minBreakoutMm}
            density="card"
            showCallouts={false}
            className="block rounded-[0.25rem]"
          />
        </span>
      </span>

      {/* ── Footer ─────────────────────────────────────────────────── */}
      <span className="mt-auto block border-t px-2.5 py-2" style={{ borderColor: "var(--cad-keyline)" }}>
        <span className="block text-xs font-medium leading-tight" style={{ color: "var(--cad-text)" }}>
          {plate.pieceCount === 1 ? t("1 piece") : t("{v1} pieces", { v1: plate.pieceCount })}
          {!compact ? (
            <span style={{ color: "var(--cad-subtle)" }}>{` · ${plate.usedSqm} m²`}</span>
          ) : null}
        </span>
        {!compact ? (
          <span className="mt-0.5 block font-mono text-[0.625rem] leading-tight" style={{ color: "var(--cad-subtle)" }}>
            {`${plate.offcutSqm} m² ${t("offcut")}`}
          </span>
        ) : null}
        {onSelect ? (
          <span className="mt-1.5 block font-mono text-[0.625rem] uppercase tracking-[0.12em]" style={{ color: "var(--cad-accent)" }}>
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
          "studio-sheet flex h-full flex-col overflow-hidden text-left transition-[border-color,transform,box-shadow] duration-200",
          "hover:-translate-y-0.5 focus-visible:outline-2 focus-visible:outline-offset-2",
          className,
        )}
        style={selected ? { borderColor: "var(--cad-accent)", boxShadow: "0 0 0 1px var(--cad-accent-line)" } : undefined}
      >
        {inner}
      </button>
    );
  }

  return <article className={cn("studio-sheet flex h-full flex-col overflow-hidden", className)}>{inner}</article>;
}
