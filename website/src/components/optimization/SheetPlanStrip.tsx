"use client";

import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { SheetGallery } from "@/components/optimization/SheetGallery";
import { cutList, planMargins, planSheet, planTotals, plates } from "@/components/optimization/sheetPlan";
import { useT } from "@/i18n/provider";

/**
 * The homepage's view of a nesting run.
 *
 * This was once one plate drawn across the full width of the section, and the
 * plate's proportions are wider than tall, so it alone was most of a screen.
 * Then it was a row of four large drawings, which still spent a lot of the page
 * on a single job. It is now the same treatment the application gives a run in
 * the 2D View tab: every plate as a thumbnail, several to a row, with a pager,
 * so a job that needs fourteen plates is still one glance rather than fourteen
 * screens.
 *
 * Everything drawn is computed in `sheetPlan.ts` from the cutting list below
 * and proved cuttable at build time. The full animated demonstration still runs
 * on /optimization, where there is room to watch it.
 */
export function SheetPlanStrip() {
  const t = useT();
  return (
    <div>
      {/* ── The header line: what is being cut, and off what ──────────── */}
      <div className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-2">
        <p className="mono-label">{t("Sample job · {v1} references · {v2} pieces", { v1: cutList.length, v2: planTotals.pieces })}</p>
        <p className="mono-label">{t("{v1} mm plate · trim {v2} mm · kerf {v3} mm", { v1: t(planSheet.label), v2: planMargins.left, v3: planMargins.kerf })}</p>
      </div>

      {/* ── Every plate, as thumbnails ────────────────────────────────── */}
      <SheetGallery plates={plates} thicknessMm={6} className="mt-4" />

      {/* ── What the run adds up to, and what it is not ───────────────── */}
      {/* A rule rather than a box: this is the caption of the plan above it,
          and a third nested card inside the primary card was exactly the
          box-in-box repetition the redesign removes. */}
      <div className="mt-6 flex flex-col gap-4 border-t pt-5 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-sm leading-relaxed text-text-secondary">
            {t.template`${planTotals.pieces} pieces need ${planTotals.plates} plates at this size — ${planTotals.usedSqm} m² of the ${planTotals.stockSqm} m² the job draws from stock, the rest returning to the racks as offcut.`}
          </p>
          <p className="mt-2 text-xs font-medium leading-relaxed text-text-faint">{t("{v1}every boundary is a cut the table can make. It illustrates how a job spreads across stock; it is not a result from the engine, which searches five families and six plate patterns, and the site quotes no yield figure for it because the repository contains none.", { v1: t("Plates arranged by a two-way guillotine split — each leftover rectangle divided by one straight cut, so") })}</p>
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
