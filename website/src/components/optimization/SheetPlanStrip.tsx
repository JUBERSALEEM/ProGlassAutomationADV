"use client";

import { useReveal } from "@/lib/useReveal";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import {
  cutList,
  planMargins,
  planSheet,
  planTotals,
  plates,
} from "@/components/optimization/sheetPlan";

/**
 * The compact plate strip.
 *
 * The homepage used to render the full animated demonstration: one plate at
 * the container's full width, roughly a thousand pixels tall on a desktop. It
 * made the point once and pushed everything below it off the screen.
 *
 * This shows the thing that actually matters at this size — what happens when
 * a cutting list does not fit on one plate. Every plate the list needs is its
 * own box, drawn small and side by side, so a four-plate job reads at a glance
 * instead of as one very tall picture.
 *
 * The full demonstration still exists on /optimization, where there is room for
 * it and time to watch it.
 *
 * The drawing is labelled as an illustration of the model throughout. It is not
 * a recording of a run, and the figures on it are arithmetic over the sizes
 * shown rather than a yield claim — the site quotes no savings figure for the
 * engine because the repository contains none.
 */
export function SheetPlanStrip() {
  const { ref, style } = useReveal<HTMLDivElement>();

  return (
    <div ref={ref} style={style} className="reveal">
      {/* ── Header ─────────────────────────────────────────────────── */}
      <div className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-2">
        <p className="mono-label">
          Cutting list · {cutList.length} references · {planTotals.pieces} pieces
        </p>
        <p className="mono-label">
          {planSheet.label} mm plate · margins {planMargins.left} mm · kerf {planMargins.kerf} mm
        </p>
      </div>

      {/* ── One box per plate ──────────────────────────────────────── */}
      <ul className="mt-4 grid grid-cols-2 gap-3 sm:gap-4 xl:grid-cols-4">
        {plates.map((plate) => (
          <li key={plate.plate}>
            <article className="surface card-interactive flex h-full flex-col overflow-hidden">
              <svg
                viewBox={`0 0 ${planSheet.width} ${planSheet.height}`}
                className="w-full bg-ink-950/70"
                role="img"
                aria-label={`Plate ${plate.plate}: ${plate.pieces} pieces nested on a ${planSheet.label} millimetre sheet.`}
              >
                {/* The plate */}
                <rect
                  x={0}
                  y={0}
                  width={planSheet.width}
                  height={planSheet.height}
                  className="fill-ink-900"
                />
                {/* Safety edges */}
                <rect
                  x={planMargins.left}
                  y={planMargins.top}
                  width={planSheet.width - planMargins.left - planMargins.right}
                  height={planSheet.height - planMargins.top - planMargins.bottom}
                  fill="none"
                  className="stroke-brand-400/25"
                  strokeWidth={10}
                  strokeDasharray="60 40"
                />
                {/* Pieces */}
                {plate.layout.map((piece) => (
                  <rect
                    key={piece.id}
                    x={piece.x}
                    y={piece.y}
                    width={piece.width}
                    height={piece.height}
                    rx={12}
                    className="fill-brand-400/30 stroke-brand-200/70"
                    strokeWidth={8}
                  />
                ))}
              </svg>

              <div className="flex items-baseline justify-between gap-3 border-t border-line/60 px-3 py-2.5">
                <div className="min-w-0">
                  <p className="text-sm font-semibold text-text-primary">Plate {plate.plate}</p>
                  <p className="mt-0.5 truncate text-xs text-text-muted">
                    {plate.pieces} {plate.pieces === 1 ? "piece" : "pieces"} · {plate.usedSqm} m² used
                  </p>
                </div>
                <span className="shrink-0 font-mono text-xs text-text-faint">{plate.offcutSqm} m² left</span>
              </div>
            </article>
          </li>
        ))}
      </ul>

      {/* ── Summary ───────────────────────────────────────────────── */}
      <div className="surface mt-4 flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6">
        <div>
          <p className="text-sm leading-relaxed text-text-secondary">
            {planTotals.pieces} pieces need <strong className="font-semibold text-text-primary">{planTotals.plates} plates</strong>{" "}
            at this size — {planTotals.usedSqm} m² of the {planTotals.stockSqm} m² the job would draw from stock, the rest
            returning to the racks as offcut.
          </p>
          <p className="mt-2 text-xs leading-relaxed text-text-faint font-medium">
            Plates laid out side by side by a shelf rule, the simplest guillotine-cuttable strategy. It is an illustration
            of how the plan overflows a single plate, not the engine&apos;s result — the engine searches five families and
            six plate patterns, and the site quotes no yield figure for it because the repository contains none.
          </p>
        </div>
        <div className="flex shrink-0 flex-wrap gap-3">
          <ButtonLink href="/optimization" variant="secondary" size="md" trailing={<ArrowRight />}>
            See the engine
          </ButtonLink>
        </div>
      </div>
    </div>
  );
}
