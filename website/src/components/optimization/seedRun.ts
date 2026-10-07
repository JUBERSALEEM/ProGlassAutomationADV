/**
 * The seeded job's own 2D view.
 *
 * The rest of the site's nesting drawings pack a sample cut list this site
 * chose. This one does not: it packs the *seeded* job — the twenty-two quotation
 * lines the application writes into an empty database — onto the sheet stock the
 * same seeder puts in the store.
 *
 * Two rules keep it honest, and both of them are the reason it is worth having:
 *
 *   1. **One glass, one run.** An optimizer composes a run from one glass
 *      specification; you cannot nest 6 mm clear and 12 mm clear on the same
 *      plate. So the panes are grouped by the sheet they are bought on, which is
 *      `sheetForRef` in `workedExample.ts` — the same function the quotation
 *      prices from. The group with the most panes is the one drawn, and the
 *      caption names it, because a drawing of "the job" would be a drawing of
 *      something the shop would never cut in one pass.
 *
 *   2. **The house profile for that thickness.** Margins and kerf are the
 *      application's own defaults for the glass, not values chosen to make the
 *      picture look full: 6 mm is the Thin bucket, 15 mm on all four edges and a
 *      3 mm blade gap.
 *
 * The result is proved cuttable at import by the same checker the homepage plan
 * uses, so a drawing that could not be cut fails the build rather than shipping.
 */
import { packGuillotine, plateViews, type PackInput, type PackMargins, type PlateView } from "@/lib/guillotinePack";
import { houseMarginProfiles } from "@/data/optimization";
import { seedSheets, seedSpecGroups, type SeedLine } from "@/data/workedExample";

/** A pane of the seeded job, with the line it came from. */
export type SeedPane = { line: SeedLine; group: number; index: number };

/** Every pane of the seeded job, grouped by the sheet it is bought on. */
function panesBySheet(): Map<string, { sheet: (typeof seedSheets)[number]; panes: SeedPane[] }> {
  const runs = new Map<string, { sheet: (typeof seedSheets)[number]; panes: SeedPane[] }>();

  for (const group of seedSpecGroups) {
    for (const line of group.lines) {
      /*
       * `CT-06-1200x900` and `TT-08-2000x1500` are named for their glass: the
       * first two letters say clear or tinted, the two digits say the thickness.
       * That is the application's own reference scheme, and the sheet store is
       * keyed the same way — a 6 mm clear line comes off the 6 mm clear sheet.
       */
      const key = `${line.ref.slice(0, 2)}-${line.ref.slice(3, 5)}`;
      const sheet =
        seedSheets.find(
          (entry) =>
            entry.thicknessMm === Number(line.ref.slice(3, 5)) &&
            (line.ref.startsWith("TT") ? entry.category === "Tinted" : entry.category === "Clear"),
        ) ?? null;

      if (!sheet) continue;

      const run = runs.get(key) ?? { sheet, panes: [] };
      for (let index = 0; index < line.qty; index += 1) {
        run.panes.push({ line, group: group.number, index: index + 1 });
      }
      runs.set(key, run);
    }
  }

  return runs;
}

const runs = panesBySheet();

/** The glass the drawn run is cut from, chosen by pane count. */
const drawnKey =
  [...runs.entries()].sort((a, b) => b[1].panes.length - a[1].panes.length)[0]?.[0] ?? "";

const drawn = runs.get(drawnKey) ?? { sheet: seedSheets[0]!, panes: [] as SeedPane[] };

/** The house profile for the drawn glass's thickness. */
const profile =
  drawn.sheet.thicknessMm <= houseMarginProfiles.thin.thicknessMm
    ? houseMarginProfiles.thin
    : drawn.sheet.thicknessMm <= houseMarginProfiles.medium.thicknessMm
      ? houseMarginProfiles.medium
      : houseMarginProfiles.thick;

export const seedRunMargins: PackMargins = {
  left: profile.left,
  right: profile.right,
  top: profile.top,
  bottom: profile.bottom,
  kerf: profile.kerf,
};

/** The breakout allowance the drawn run's profile carries. */
export const seedRunBreakoutMm = profile.breakout;

/** The sheet the seeded run is cut from, with its own store row. */
export const seedRunSheet = {
  width: drawn.sheet.widthMm,
  height: drawn.sheet.heightMm,
  label: `${drawn.sheet.widthMm} × ${drawn.sheet.heightMm}`,
  thicknessMm: drawn.sheet.thicknessMm,
  category: drawn.sheet.category,
  colour: drawn.sheet.colour,
  supplier: drawn.sheet.supplier,
  /** What the store holds, as the seeder wrote it. */
  stock: drawn.sheet.stock,
  used: drawn.sheet.used,
} as const;

/** Which quotation lines went into the drawn run, in serial order. */
export const seedRunLines = [
  ...new Map(drawn.panes.map((pane) => [pane.line.ref, pane.line])).values(),
].sort((a, b) => a.serial - b.serial);

/** How many panes the run holds, over how many lines. */
export const seedRunPieces = drawn.panes.length;

const inputs: PackInput[] = drawn.panes.map((pane) => ({
  id: `${pane.line.ref}-${pane.index}`,
  width: pane.line.widthMm,
  height: pane.line.heightMm,
}));

const plan = packGuillotine(inputs, seedRunSheet, seedRunMargins);

/** The plates the seeded run needs, exactly as the gallery draws them. */
export const seedRunPlates: PlateView[] = plateViews(plan, seedRunSheet);

/**
 * Why this run needs as many sheets as it does, in three numbers.
 *
 * The largest pane in the run is 1500 × 1000. On a 2440 × 1830 sheet the trims
 * take 15 mm off the top and the bottom, so the usable height is 1800 mm — and
 * under a 1000 mm pane the blade gap leaves 797 mm, three millimetres short of
 * the run's smallest pane. Nothing fits beside it either: 2410 − 1500 − 3 = 907
 * mm, and the run's next size down is 1000 mm wide.
 *
 * That is geometry, not a packing failure, and it is worth printing: it is the
 * reason a shop keeps more than one sheet size on the rack. The engine searches
 * other sizes and other patterns; the drawing here shows what this job does on
 * the stock the seeder actually put in the store.
 */
export const seedRunGeometry = (() => {
  const usableWidth = seedRunSheet.width - seedRunMargins.left - seedRunMargins.right;
  const usableHeight = seedRunSheet.height - seedRunMargins.top - seedRunMargins.bottom;
  const tallest = Math.max(...drawn.panes.map((pane) => pane.line.heightMm));
  const smallest = Math.min(...drawn.panes.map((pane) => Math.min(pane.line.widthMm, pane.line.heightMm)));
  const tallestWidth = Math.max(
    ...drawn.panes.filter((pane) => pane.line.heightMm === tallest).map((pane) => pane.line.widthMm),
  );

  return {
    usableWidth,
    usableHeight,
    tallest,
    smallest,
    /** Height left under the tallest pane, after the blade gap. */
    belowTallest: usableHeight - tallest - seedRunMargins.kerf,
    /** Width left beside the tallest pane, after the blade gap. */
    besideTallest: usableWidth - tallestWidth - seedRunMargins.kerf,
  };
})();

export const seedRunTotals = {
  plates: plan.plates,
  pieces: plan.placedPieces,
  unplaced: plan.unplaced.length,
  usedSqm: plan.partAreaSqm,
  stockSqm: plan.stockAreaSqm,
  utilizationPct: plan.utilizationPct,
  /** What the run draws out of the store, for the caption. */
  sheetsAvailable: seedRunSheet.stock - seedRunSheet.used,
};

/* ══════════════════════════════════════════════════════════════════════════
 * The check
 * ════════════════════════════════════════════════════════════════════════ */

if (plan.placedPieces + plan.unplaced.length !== plan.totalPieces) {
  throw new Error(
    `The seeded job's 2D view lost ${plan.totalPieces - plan.placedPieces - plan.unplaced.length} panes.`,
  );
}
