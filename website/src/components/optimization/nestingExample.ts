/**
 * Worked nesting example used by the animated demonstration.
 *
 * This is a single, fully-stated cutting list rather than a benchmark claim.
 * The layout below is a real guillotine arrangement for the stated sheet and
 * margins, so the utilization figure shown is arithmetic over the numbers on
 * screen — not a marketing percentage. The site never quotes a savings figure
 * for the engine because the repository does not contain one.
 */

export type ExamplePiece = {
  id: string;
  label: string;
  /** Millimetres. */
  width: number;
  height: number;
  x: number;
  y: number;
  /** Colour key for the 2D view. */
  tone: "a" | "b" | "c";
  rotated?: boolean;
};

export const exampleSheet = {
  /** A jumbo plate, millimetres. */
  width: 3210,
  height: 2250,
  thickness: 6,
} as const;

/**
 * House margin profile for the Thin bucket (up to 6 mm):
 * 15 mm on all four edges, kerf 3 mm, breakout 15 mm.
 * Source: Services/Optimization/OptimizationModels.cs → ThicknessBucket.Thin
 */
export const exampleMargins = { left: 15, right: 15, top: 15, bottom: 15, kerf: 3 } as const;

const M = exampleMargins;
const usableX = M.left;
const usableY = M.top;
const usableW = exampleSheet.width - M.left - M.right; // 3180
const usableH = exampleSheet.height - M.top - M.bottom; // 2220

// Cut 1 — vertical guillotine cut, 1600 mm from the usable origin.
const stripAW = 1600;
const stripAX = usableX;
const stripBX = usableX + stripAW + M.kerf; // 1618
const stripBW = usableW - stripAW - M.kerf; // 1577

// Strip A is then cut horizontally: two 1100 mm plates.
const aH = 1100;

// Strip B is then cut horizontally: 700 / 700 / 796.
const bH1 = 700;
const bH2 = 700;
const bH3 = 796;

export const examplePieces: ExamplePiece[] = [
  { id: "A1", label: "G-101", ...place(stripAX, usableY, stripAW, aH), tone: "a" } as ExamplePiece,
  { id: "A2", label: "G-101", ...place(stripAX, usableY + aH + M.kerf, stripAW, aH), tone: "a" } as ExamplePiece,
  { id: "B1", label: "G-102", ...place(stripBX, usableY, stripBW, bH1), tone: "b" } as ExamplePiece,
  { id: "B2", label: "G-102", ...place(stripBX, usableY + bH1 + M.kerf, stripBW, bH2), tone: "b" } as ExamplePiece,
  { id: "B3", label: "G-103", ...place(stripBX, usableY + bH1 + bH2 + M.kerf * 2, stripBW, bH3), tone: "c" } as ExamplePiece,
];

function place(x: number, y: number, width: number, height: number) {
  return { x, y, width, height };
}

/** The guillotine cut sequence the layout above is produced by. */
export const exampleCuts = [
  { id: "cut-1", axis: "vertical" as const, atMm: stripAX + stripAW, span: 0 },
  { id: "cut-2", axis: "horizontal" as const, atMm: usableY + aH, span: 0 },
  { id: "cut-3", axis: "horizontal" as const, atMm: usableY + aH + M.kerf + aH, span: 0 },
  { id: "cut-4", axis: "horizontal" as const, atMm: usableY + bH1, span: 1 },
  { id: "cut-5", axis: "horizontal" as const, atMm: usableY + bH1 + M.kerf + bH2, span: 1 },
  { id: "cut-6", axis: "horizontal" as const, atMm: usableY + bH1 + bH2 + M.kerf * 2 + bH3, span: 1 },
];

export const exampleUsable = { x: usableX, y: usableY, width: usableW, height: usableH };

/** Computed from the geometry above — no rounded marketing numbers. */
export const exampleMetrics = (() => {
  const placedArea = examplePieces.reduce((sum, p) => sum + p.width * p.height, 0);
  const stockArea = exampleSheet.width * exampleSheet.height;
  const usedSqm = placedArea / 1_000_000;
  const stockSqm = stockArea / 1_000_000;
  return {
    pieces: examplePieces.length,
    sheets: 1,
    usedSqm: Number(usedSqm.toFixed(4)),
    stockSqm: Number(stockSqm.toFixed(4)),
    offcutSqm: Number((stockSqm - usedSqm).toFixed(4)),
    utilizationPct: Number(((placedArea / stockArea) * 100).toFixed(2)),
  };
})();
