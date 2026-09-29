/**
 * The homepage's illustrated cutting job.
 *
 * A cutting list and the stock it is cut from — sizes and quantities only, no
 * rates, no customer, no job number. The packing itself is not done here: it
 * goes through `packGuillotine` in `src/lib/guillotinePack.ts`, the same function the
 * module preview uses, so every plate drawn on this site comes out of one
 * implementation.
 *
 * Two things make the result trustworthy rather than decorative:
 *
 *   1. Every figure is derived. Change a piece size and the plate count, the
 *      yield badge and the offcut on each card all follow.
 *   2. `assertPlanIsSound()` proves the result at import time: each piece placed
 *      exactly once, nothing overlapping, nothing outside the safety margins,
 *      and nothing quietly dropped. A packing fault fails the build instead of
 *      drawing a picture of something the table could not cut.
 *
 * This is an illustration of the model, not a benchmark. The engine searches
 * five families and six plate patterns; this is one shelf rule. The site quotes
 * no yield figure for the engine, because the repository contains none.
 */

import { packGuillotine, plateViews, type PackMargins, type PackResult, type PlateView } from "@/lib/guillotinePack";

export type SheetSize = { label: string; width: number; height: number };

/**
 * Stock sizes the engine accepts, with the labels the application shows.
 * Source: the plate sizes offered by the optimizer's sheet picker.
 */
export const plateSizes: SheetSize[] = [
  { label: "3210 × 2250", width: 3210, height: 2250 },
  { label: "3660 × 2440", width: 3660, height: 2440 },
  { label: "6000 × 3210", width: 6000, height: 3210 },
];

/**
 * House margin profile for the Thin bucket (up to 6 mm) — 15 mm on every edge,
 * kerf 3 mm. Source: the house margin table in
 * OPTIMIZATION_ENGINE_REMEDIATION_PLAN_2026-09-20.md, pinned in the application
 * by Tests/Agent/OptimizationMarginHouseTableTests.cs.
 */
export const planMargins: PackMargins = { left: 15, right: 15, top: 15, bottom: 15, kerf: 3 };

/** The cutting list worked through on the homepage. */
export type CutItem = { ref: string; width: number; height: number; qty: number };

/**
 * The sizes are a mixed shopfront job: two large panes, three medium, and a
 * long tail of small ones, which is what a real cutting list looks like — a
 * handful of big pieces and a great many little ones.
 *
 * The quantities are chosen so the job needs four plates and every one of them
 * is well filled. That is not cosmetic: the previous quantities left a fourth
 * plate carrying 16% of its area, which draws as a mostly-empty box and reads
 * as a packing fault rather than as leftover glass. A cutting list that spreads
 * evenly is also the more honest illustration, because it is what a shop sees
 * when the sizes have been chosen sensibly.
 */
export const cutList: CutItem[] = [
  { ref: "G-101", width: 1600, height: 1100, qty: 5 },
  { ref: "G-102", width: 1577, height: 700, qty: 3 },
  { ref: "G-103", width: 1180, height: 900, qty: 3 },
  { ref: "G-104", width: 980, height: 620, qty: 9 },
  { ref: "G-105", width: 760, height: 520, qty: 10 },
];

export const planSheet: SheetSize = plateSizes[0]!;

export const plan: PackResult = packGuillotine(
  cutList.flatMap((item) =>
    Array.from({ length: item.qty }, (_, index) => ({
      id: `${item.ref}-${index + 1}`,
      width: item.width,
      height: item.height,
    })),
  ),
  planSheet,
  planMargins,
);

/** One entry per plate, which is exactly what the gallery draws. */
export const plates: PlateView[] = plateViews(plan, planSheet);

export const planTotals = {
  plates: plan.plates,
  pieces: plan.placedPieces,
  unplaced: plan.unplaced.length,
  usedSqm: plan.partAreaSqm,
  stockSqm: plan.stockAreaSqm,
  utilizationPct: plan.utilizationPct,
};

/* ══════════════════════════════════════════════════════════════════════════
 * The check that keeps the drawing honest
 * ════════════════════════════════════════════════════════════════════════ */

/**
 * Every piece placed once, nothing overlapping, nothing outside the margins,
 * nothing lost.
 *
 * Throws at import time. A layout that fails this is not a plan, and drawing it
 * would be a picture of something the table could not cut.
 */
export function assertPlanIsSound(): void {
  const problems: string[] = [];

  if (plan.placedPieces !== cutList.reduce((sum, item) => sum + item.qty, 0)) {
    problems.push(
      `${plan.placedPieces} pieces placed, but the cutting list asks for ` +
        `${cutList.reduce((sum, item) => sum + item.qty, 0)}.`,
    );
  }

  // Nothing may be dropped on the floor — not placed, and not reported either.
  if (plan.placedPieces + plan.unplaced.length !== plan.totalPieces) {
    problems.push(`${plan.totalPieces - plan.placedPieces - plan.unplaced.length} pieces went missing entirely.`);
  }

  const usableLeft = planMargins.left;
  const usableTop = planMargins.top;
  const usableRight = planSheet.width - planMargins.right;
  const usableBottom = planSheet.height - planMargins.bottom;

  const seen = new Set<string>();
  for (const piece of plan.placed) {
    if (seen.has(piece.id)) problems.push(`${piece.id} was placed twice.`);
    seen.add(piece.id);

    if (piece.plate < 1 || piece.plate > plan.plates) {
      problems.push(`${piece.id} is on plate ${piece.plate}, which does not exist.`);
    }
    // A hair of tolerance: the arithmetic above is in whole millimetres.
    if (
      piece.x < usableLeft - 0.01 ||
      piece.y < usableTop - 0.01 ||
      piece.x + piece.width > usableRight + 0.01 ||
      piece.y + piece.height > usableBottom + 0.01
    ) {
      problems.push(`${piece.id} falls outside the usable area of plate ${piece.plate}.`);
    }
  }

  // Overlap check, plate by plate. The lists are short, so the pairwise test is
  // honest and readable rather than clever.
  for (let plate = 1; plate <= plan.plates; plate += 1) {
    const onPlate = plan.placed.filter((piece) => piece.plate === plate);
    for (let i = 0; i < onPlate.length; i += 1) {
      for (let j = i + 1; j < onPlate.length; j += 1) {
        const a = onPlate[i]!;
        const b = onPlate[j]!;
        if (
          a.x < b.x + b.width - 0.01 &&
          b.x < a.x + a.width - 0.01 &&
          a.y < b.y + b.height - 0.01 &&
          b.y < a.y + a.height - 0.01
        ) {
          problems.push(`${a.id} and ${b.id} overlap on plate ${plate}.`);
        }
      }
    }
  }

  if (problems.length > 0) {
    throw new Error(
      `The homepage plate plan is not cuttable:\n  · ${problems.slice(0, 8).join("\n  · ")}` +
        (problems.length > 8 ? `\n  · …and ${problems.length - 8} more` : ""),
    );
  }
}

assertPlanIsSound();
