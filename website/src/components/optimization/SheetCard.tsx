import { cn } from "@/lib/cn";
import type { PlateView } from "@/lib/guillotinePack";

/**
 * One plate, drawn small.
 *
 * This is the website's version of the application's gallery card — the
 * miniature sheet in the 2D View tab's Multi-Sheet Gallery
 * (`Views/Optimization/Tabs/TwoDViewTabView.xaml`, the `OptGalleryCard`
 * template). Same four parts, in the same order:
 *
 *   Sheet 7                     [ 78.1% ]   ← plate number, stock size, yield badge
 *   3210 × 2250 mm · 6 mm
 *   ┌──────────────────────────────────┐
 *   │  pieces drawn to scale           │   ← miniature canvas, plate proportions
 *   └──────────────────────────────────┘
 *   9 pieces · 4.21 m² used                 ← what is on it and what is left
 *   1.58 m² offcut
 *
 * Why a thumbnail and not a big drawing: a run has as many plates as it has,
 * and a plate is wider than it is tall. Drawn full width, seven plates is seven
 * near-empty boxes stacked down the page — which is what this replaced. Drawn at
 * thumbnail size and wrapped several to a row, the same seven plates are visible
 * at once, which is the only way to compare them.
 *
 * The drawing is a projection of the plate: every rectangle is a placed piece,
 * positioned and sized from the pack result, so the picture cannot disagree with
 * the numbers printed under it.
 */

/** A plate of stock and the pieces on it. `compact` drops to number and yield. */
export function SheetCard({
  plate,
  thicknessMm,
  compact = false,
  className,
}: {
  plate: PlateView;
  /** Shown beside the stock size when the job is all one thickness. */
  thicknessMm?: number;
  /** The preview variant: no stock line, no offcut line, just number and yield. */
  compact?: boolean;
  className?: string;
}) {
  return (
    <article className={cn("surface flex h-full flex-col overflow-hidden", className)}>
      {/* ── Header ─────────────────────────────────────────────────── */}
      <div className="flex items-center justify-between gap-2 border-b border-line/60 px-2.5 py-2">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold leading-tight text-text-primary">Sheet {plate.plate}</p>
          {!compact ? (
            <p className="mt-0.5 truncate font-mono text-[0.625rem] leading-tight text-text-faint">
              {plate.stockLabel}
              {thicknessMm ? ` · ${thicknessMm} mm` : ""}
            </p>
          ) : null}
        </div>
        {/*
          The yield badge. Accent-tinted rather than a traffic light: the site
          has no proven threshold above which a plan counts as good, so a green
          badge would be inventing one. It reports, it does not judge.
        */}
        <span className="shrink-0 rounded-full border border-brand-400/40 bg-brand-500/12 px-2 py-0.5 font-mono text-[0.625rem] font-semibold tabular-nums text-brand-200">
          {plate.utilizationPct.toFixed(1)}%
        </span>
      </div>

      {/* ── The plate, drawn to scale ──────────────────────────────── */}
      <div className="p-2">
        <svg
          viewBox={`0 0 ${plate.widthMm} ${plate.heightMm}`}
          className="w-full rounded-[0.35rem] border border-line-strong/60 bg-ink-950/70"
          role="img"
          aria-label={`Sheet ${plate.plate}: ${plate.pieceCount} pieces nested on a ${plate.stockLabel} millimetre plate, ${plate.utilizationPct} percent used.`}
        >
          {plate.pieces.map((piece) => (
            <rect
              key={piece.id}
              x={piece.x}
              y={piece.y}
              width={piece.width}
              height={piece.height}
              rx={8}
              /* A rotated piece is drawn in the accent, the way the application's
                 legend marks one, so an odd-looking fit explains itself. */
              className={piece.rotated ? "fill-brand-400/45 stroke-brand-200/75" : "fill-brand-400/28 stroke-brand-200/55"}
              strokeWidth={7}
            />
          ))}
        </svg>
      </div>

      {/* ── Footer ─────────────────────────────────────────────────── */}
      <div className="mt-auto border-t border-line/60 px-2.5 py-2">
        <p className="text-xs font-medium leading-tight text-text-secondary">
          {plate.pieceCount} {plate.pieceCount === 1 ? "piece" : "pieces"}
          {!compact ? <span className="text-text-faint"> · {plate.usedSqm} m² used</span> : null}
        </p>
        {!compact ? (
          <p className="mt-0.5 font-mono text-[0.625rem] leading-tight text-text-faint">{plate.offcutSqm} m² offcut</p>
        ) : null}
      </div>
    </article>
  );
}
