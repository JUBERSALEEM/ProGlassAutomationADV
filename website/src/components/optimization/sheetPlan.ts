/**
 * A small multi-plate plan, computed rather than drawn by hand.
 *
 * The homepage needs one thing the full animated demonstration cannot give it:
 * several plates, shown side by side and small. So this file takes a cutting
 * list and lays it out across as many sheets as it needs, using one simple,
 * honest strategy — shelf packing, which is guillotine-cuttable by
 * construction, because every piece is separated from the next by a full-width
 * straight cut in one direction.
 *
 * Two rules make the result trustworthy:
 *
 *   1. The geometry is derived, not typed. Change a piece size and the layout,
 *      the per-plate utilisation and the plate count all follow.
 *   2. `assertPlanIsSound()` proves the result: every piece appears exactly
 *      once, no two pieces overlap, and nothing crosses a safety margin. It
 *      runs at module load, so a bad layout fails the build instead of drawing
 *      a picture that could not be cut.
 *
 * This is an illustration of the model, not a benchmark. The site never claims
 * a savings figure for the engine, and nothing here is a recorded run.
 */

export type PlanPiece = {
  id: string;
  /** Millimetres. */
  width: number;
  height: number;
  /** Where it sits on its plate. */
  x: number;
  y: number;
  /** Which plate it was placed on, 1-based. */
  plate: number;
};

export type SheetSize = { label: string; width: number; height: number };

/**
 * Real stock sizes the engine accepts.
 * Source: the plate sizes offered by the optimizer's sheet picker.
 */
export const plateSizes: SheetSize[] = [
  { label: "3210 × 2250", width: 3210, height: 2250 },
  { label: "3660 × 2440", width: 3660, height: 2440 },
  { label: "6000 × 3210", width: 6000, height: 3210 },
];

/**
 * House margin profile for the Thin bucket (up to 6 mm) — 15 mm on every edge,
 * kerf 3 mm. Source: Services/Optimization/OptimizationModels.cs
 */
export const planMargins = { left: 15, right: 15, top: 15, bottom: 15, kerf: 3 } as const;

/** The cutting list. Sizes and quantities only — no rates, no customer. */
export type CutItem = { ref: string; width: number; height: number; qty: number };

export const cutList: CutItem[] = [
  { ref: "G-101", width: 1600, height: 1100, qty: 2 },
  { ref: "G-102", width: 1577, height: 700, qty: 3 },
  { ref: "G-103", width: 1180, height: 900, qty: 4 },
  { ref: "G-104", width: 980, height: 620, qty: 5 },
  { ref: "G-105", width: 760, height: 520, qty: 6 },
];

/**
 * Shelf packing across plates.
 *
 * Pieces are placed left to right along a shelf of one piece's height. When a
 * piece no longer fits across the remaining usable width, a new shelf starts
 * below, separated by the kerf. When no shelf is left on the plate, the piece
 * goes to a fresh plate. Every boundary is a straight full-width cut, so the
 * plan is guillotine-cuttable without further reasoning.
 */
export function buildPlan(
  items: CutItem[],
  sheet: SheetSize,
  margins = planMargins,
): { pieces: PlanPiece[]; plates: number } {
  const usableX = margins.left;
  const usableY = margins.top;
  const usableW = sheet.width - margins.left - margins.right;
  const usableH = sheet.height - margins.top - margins.bottom;

  const pieces: PlanPiece[] = [];
  let plate = 1;
  let cursorX = usableX;
  let cursorY = usableY;
  let shelfHeight = 0;

  // Tallest first: the standard shelf heuristic, and it keeps a heavy piece
  // from being stranded when the plate is nearly full.
  const queue = items
    .flatMap((item) => Array.from({ length: item.qty }, (_, index) => ({ ...item, id: `${item.ref}-${index + 1}` })))
    .sort((a, b) => b.height - a.height || b.width - a.width);

  const startPlate = () => {
    plate += 1;
    cursorX = usableX;
    cursorY = usableY;
    shelfHeight = 0;
  };

  for (const piece of queue) {
    // A piece wider or taller than the usable area could never be cut from
    // this plate. Refusing loudly beats emitting a layout that is impossible.
    if (piece.width > usableW || piece.height > usableH) {
      throw new Error(
        `${piece.id} is ${piece.width}×${piece.height} mm, which does not fit the usable ` +
          `${usableW}×${usableH} mm of a ${sheet.label} plate.`,
      );
    }

    const needsNewShelf = cursorX + piece.width > usableX + usableW;
    if (needsNewShelf) {
      cursorX = usableX;
      cursorY += shelfHeight + margins.kerf;
      shelfHeight = 0;
    }
    if (cursorY + piece.height > usableY + usableH) {
      startPlate();
    }

    pieces.push({
      id: piece.id,
      width: piece.width,
      height: piece.height,
      x: cursorX,
      y: cursorY,
      plate,
    });

    cursorX += piece.width + margins.kerf;
    shelfHeight = Math.max(shelfHeight, piece.height);
  }

  return { pieces, plates: plate };
}

export const planSheet = plateSizes[0]!;
const plan = buildPlan(cutList, planSheet);

/* ══════════════════════════════════════════════════════════════════════════
 * Per-plate arithmetic, derived from the placement above
 * ════════════════════════════════════════════════════════════════════════ */

const sheetArea = planSheet.width * planSheet.height;

export type PlateSummary = {
  plate: number;
  pieces: number;
  usedSqm: number;
  offcutSqm: number;
  utilizationPct: number;
  /** The pieces on this plate, for the thumbnail. */
  layout: PlanPiece[];
};

export const plates: PlateSummary[] = Array.from({ length: plan.plates }, (_, index) => {
  const layout = plan.pieces.filter((piece) => piece.plate === index + 1);
  const placedArea = layout.reduce((sum, piece) => sum + piece.width * piece.height, 0);
  const stockSqm = sheetArea / 1_000_000;
  const usedSqm = placedArea / 1_000_000;

  return {
    plate: index + 1,
    pieces: layout.length,
    usedSqm: Number(usedSqm.toFixed(4)),
    offcutSqm: Number((stockSqm - usedSqm).toFixed(4)),
    utilizationPct: Number(((placedArea / sheetArea) * 100).toFixed(2)),
    layout,
  };
});

export const planTotals = {
  plates: plates.length,
  pieces: plan.pieces.length,
  usedSqm: Number(plates.reduce((sum, plate) => sum + plate.usedSqm, 0).toFixed(4)),
  stockSqm: Number(((sheetArea * plates.length) / 1_000_000).toFixed(4)),
  /** Utilisation across the whole job, not an average of the plates. */
  utilizationPct: Number(
    ((plates.reduce((sum, plate) => sum + plate.usedSqm, 0) / ((sheetArea * plates.length) / 1_000_000)) * 100).toFixed(2),
  ),
};

/** The kerf line between two shelves, for the drawing. */
export function shelfLines(plate: PlateSummary): number[] {
  const tops = new Set<number>();
  for (const piece of plate.layout) tops.add(piece.y);
  return [...tops].sort((a, b) => a - b);
}

/* ══════════════════════════════════════════════════════════════════════════
 * The check that keeps the drawing honest
 * ════════════════════════════════════════════════════════════════════════ */

/**
 * Every piece placed once, nothing overlapping, nothing outside the margins.
 *
 * Throws at import time. A layout that fails this is not a plan, and drawing
 * it would be a picture of something the table could not cut.
 */
export function assertPlanIsSound(): void {
  const expected = cutList.reduce((sum, item) => sum + item.qty, 0);
  if (plan.pieces.length !== expected) {
    throw new Error(`The plan holds ${plan.pieces.length} pieces but the cutting list asks for ${expected}.`);
  }

  const usableW = planSheet.width - planMargins.left - planMargins.right;
  const usableH = planSheet.height - planMargins.top - planMargins.bottom;

  for (const piece of plan.pieces) {
    if (
      piece.x < planMargins.left ||
      piece.y < planMargins.top ||
      piece.x + piece.width > planMargins.left + usableW ||
      piece.y + piece.height > planMargins.top + usableH
    ) {
      throw new Error(`${piece.id} falls outside the usable area of plate ${piece.plate}.`);
    }
  }

  // Overlap check, plate by plate. The lists are short (tens of pieces), so the
  // pairwise test is honest and readable rather than clever.
  for (let plate = 1; plate <= plan.plates; plate += 1) {
    const onPlate = plan.pieces.filter((piece) => piece.plate === plate);
    for (let i = 0; i < onPlate.length; i += 1) {
      for (let j = i + 1; j < onPlate.length; j += 1) {
        const a = onPlate[i]!;
        const b = onPlate[j]!;
        const overlaps =
          a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height;
        if (overlaps) {
          throw new Error(`${a.id} and ${b.id} overlap on plate ${plate}.`);
        }
      }
    }
  }
}

assertPlanIsSound();
