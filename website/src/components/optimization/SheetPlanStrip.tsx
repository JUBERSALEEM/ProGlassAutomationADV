"use client";

import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { PlateInspector } from "@/components/optimization/PlateInspector";
import {
  cutList,
  planBreakoutMm,
  planMargins,
  planSheet,
  planThicknessMm,
  planTotals,
  plates,
} from "@/components/optimization/sheetPlan";
import { useT } from "@/i18n/provider";

/**
 * The homepage's view of a nesting run.
 *
 * What is drawn here is the same instrument the optimization page carries, at
 * the size a homepage section can hold: one plate at a time, to scale, with its
 * edge trims, its breakout band, its pieces labelled with their references and
 * cut sizes, and the remnant it leaves back on the rack — then the rest of the
 * run as thumbnails underneath, each one a control that brings its sheet onto
 * the canvas.
 *
 * It used to be a row of rectangles in boxes. The rectangles were in the right
 * places, but a reader could not tell a trim from a piece, could not see what
 * was left over, and had to take the little yield badge's word for everything
 * else. Nothing about the arithmetic changed here — it is the same
 * `packGuillotine` result, proved cuttable at import — only what it looks like,
 * which is the whole of what a 2D view is for.
 *
 * Everything drawn is computed in `sheetPlan.ts` from the cutting list in
 * `src/data`, and the full animated sequence runs on /optimization, where there
 * is room to watch it.
 */
export function SheetPlanStrip() {
  const t = useT();
  return (
    <div>
      {/* ── What is being cut, and off what ──────────────────────────── */}
      <div className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-2">
        <p className="mono-label">
          {t("Sample job · {v1} references · {v2} pieces", {
            v1: cutList.length,
            v2: planTotals.pieces,
          })}
        </p>
        <p className="mono-label">
          {t("{v1} · trim {v2} mm · kerf {v3} mm", {
            v1: t(planSheet.label),
            v2: planMargins.left,
            v3: planMargins.kerf,
          })}
        </p>
      </div>

      {/* ── The plate, its schedule, and the rest of the run ─────────── */}
      <PlateInspector
        plates={plates}
        margins={planMargins}
        minBreakoutMm={planBreakoutMm}
        thicknessMm={planThicknessMm}
        composition={t("{v1} mm plate · sample job", { v1: planThicknessMm })}
        showLegend
        className="mt-5"
      />

      {/* ── What the run adds up to, and what it is not ───────────────── */}
      {/* A rule rather than a box: this is the caption of the plan above it,
          and a third nested card inside the primary card was exactly the
          box-in-box repetition the redesign removes. */}
      <div className="mt-8 flex flex-col gap-4 border-t pt-5 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-sm leading-relaxed text-text-secondary">
            {t.template`${planTotals.pieces} pieces need ${planTotals.plates} plates at this size — ${planTotals.usedSqm} m² of the ${planTotals.stockSqm} m² the job draws from stock, the rest returning to the racks as offcut.`}
          </p>
          <p className="mt-2 text-xs font-medium leading-relaxed text-text-faint">
            {t("Plates arranged by a two-way guillotine split — every leftover rectangle divided by one straight cut, so every boundary is a cut the table can make. It illustrates how a job spreads across stock; it is not a result from the engine, which searches five families and six plate patterns, and the site quotes no yield figure for it because the repository contains none.")}
          </p>
        </div>
        <div className="flex shrink-0 flex-wrap gap-3">
          <ButtonLink href="/optimization" variant="secondary" size="md" trailing={<ArrowRight />}>
            {t("See the engine")}
          </ButtonLink>
        </div>
      </div>
    </div>
  );
}
