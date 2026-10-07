/**
 * The arithmetic behind the 2D view.
 *
 * Everything the CAD drawing shows — the usable rectangle, the breakout band,
 * the reusable remnant, the cut schedule, and which label a piece is big enough
 * to carry — is derived here from the packing result, never typed into the
 * drawing. One plate in, one set of numbers out, and the drawing, the schedule
 * and the captions all read the same set, so they cannot disagree.
 *
 * The geometry is the application's, transcribed from
 * `ViewModels/Optimization/OptimizationViewModel.Preview.cs`:
 *
 *   usable rectangle   the stock inset by the four edge trims
 *   breakout band      the usable rectangle inset again by `MinBreakoutMm` —
 *                      the strip inside the trims that is still breakout waste
 *   reusable remnant   the right-hand strip from the edge of the last placed
 *                      piece to the usable edge, counted only when it is at
 *                      least 200 × 200 mm, because that is the narrowest offcut
 *                      the application will catalogue as reusable
 *
 * Note what the remnant rule is and is not. It measures one strip — the space
 * to the right of everything placed — and calls that the reusable offcut. It is
 * not a search for the largest rectangle in the leftovers, and this site does
 * not pretend otherwise: an offcut that is really several strips shows the
 * right-hand one, exactly as the application's preview does.
 */
import type { PackedPiece, PackMargins, PlateView } from "@/lib/guillotinePack";

/** A rectangle in millimetres, positioned on the plate. */
export type Zone = { x: number; y: number; width: number; height: number };

/**
 * The narrowest offcut the application will keep as reusable stock.
 * `OptimizationViewModel.Preview.cs` — the remnant is added to the visual only
 * when both its dimensions reach 200 mm.
 */
export const MIN_REUSABLE_OFFCUT_MM = 200;

/**
 * How big a piece has to be before a label may be drawn on it, in millimetres.
 *
 * The application measures in device pixels — `ShowDimensions` at 42 × 22 px,
 * `ShowSequence` at 60 × 34 px, `ShowFullDetails` at 80 × 44 px — against a
 * plate drawn about 520 px wide, which is a scale of roughly 0.162 px/mm. These
 * are those thresholds converted to millimetres, so the same rule survives a
 * responsive drawing that has no fixed pixel width.
 *
 *   inspect  the single-sheet CAD panel: full details on a large piece, the
 *            reference and its dimensions on a medium one, and the bare size on
 *            anything smaller
 *   card     a gallery thumbnail: only the cut number, and only on a piece with
 *            room for it — which is what the application's miniature canvas
 *            does
 */
export const CAD_LABEL_THRESHOLDS = {
  inspect: {
    dims: [260, 136],
    tag: [370, 210],
    area: [494, 272],
  },
  card: {
    sequence: [270, 190],
  },
} as const;

export type CadDensity = keyof typeof CAD_LABEL_THRESHOLDS;

/** The usable rectangle of a plate: the stock inset by its edge trims. */
export function usableZone(plate: PlateView, margins: PackMargins): Zone {
  return {
    x: margins.left,
    y: margins.top,
    width: plate.widthMm - margins.left - margins.right,
    height: plate.heightMm - margins.top - margins.bottom,
  };
}

/**
 * The breakout band: the usable rectangle inset by the minimum breakout, which
 * is the offcut strip the saw keeps inside the trims. Drawn dashed and amber,
 * the way the application draws it, so it does not disappear into the cut lines.
 */
export function breakoutZone(usable: Zone, minBreakoutMm: number): Zone {
  return {
    x: usable.x + minBreakoutMm,
    y: usable.y + minBreakoutMm,
    width: Math.max(0, usable.width - 2 * minBreakoutMm),
    height: Math.max(0, usable.height - 2 * minBreakoutMm),
  };
}

/** What the largest placed piece reaches, measured from the plate's edge. */
export function placedExtent(pieces: PackedPiece[]): { right: number; bottom: number } {
  return pieces.reduce(
    (extent, piece) => ({
      right: Math.max(extent.right, piece.x + piece.width),
      bottom: Math.max(extent.bottom, piece.y + piece.height),
    }),
    { right: 0, bottom: 0 },
  );
}

/**
 * The right-hand remnant, when it is worth keeping.
 *
 * Returns null when the strip is narrower or shorter than the 200 mm floor, and
 * also when there are no pieces at all — an empty plate has no "leftover".
 */
export function reusableOffcut(plate: PlateView, margins: PackMargins): (Zone & { label: string }) | null {
  if (plate.pieces.length === 0) return null;

  const usable = usableZone(plate, margins);
  const { right } = placedExtent(plate.pieces);

  const width = usable.x + usable.width - right;
  const height = usable.height;

  if (width < MIN_REUSABLE_OFFCUT_MM || height < MIN_REUSABLE_OFFCUT_MM) return null;

  return { x: right, y: usable.y, width, height, label: "Reusable offcut" };
}

/** `G-101-4` → `G-101`: the reference a schedule row prints. */
export function pieceRef(id: string): string {
  return id.replace(/-\d+$/, "");
}

/** One row of the piece cutting schedule, in the application's own columns. */
export type ScheduleRow = {
  /** 1-based placement order on this plate. */
  sequence: number;
  /** `Cut #4` — how the schedule names it. */
  cutTag: string;
  /** `G-101`, or the piece's whole id when it carries no suffix. */
  reference: string;
  /** `1600 × 1100 mm`, as placed. */
  dimensions: string;
  /** `Yes (90° ⟳)` / `No`. */
  rotated: string;
  /** `1.760 m²`. */
  area: string;
};

/**
 * The schedule for one plate.
 *
 * The rows come out in placement order, which is what the application's
 * `SequenceNumber` means: the order the packer placed them, and therefore the
 * order the operator works through them. It is not a re-sort of anything.
 */
export function scheduleFor(plate: PlateView): ScheduleRow[] {
  return [...plate.pieces]
    .sort((a, b) => a.sequence - b.sequence)
    .map((piece, index) => {
      const sequence = index + 1;
      const area = (piece.width * piece.height) / 1_000_000;
      return {
        sequence,
        cutTag: `Cut #${sequence}`,
        reference: pieceRef(piece.id),
        dimensions: `${round(piece.width)} × ${round(piece.height)} mm`,
        rotated: piece.rotated ? "Yes (90° ⟳)" : "No",
        area: `${area.toFixed(3)} m²`,
      };
    });
}

/**
 * Which labels the drawing may carry for one piece, at one density.
 *
 * A label that does not fit is not a label, it is a smudge — so the drawing asks
 * this before it draws anything, and a small piece simply goes without.
 */
export function labelsFor(
  piece: PackedPiece,
  density: CadDensity,
): { dimensions: boolean; cutTag: boolean; area: boolean } {
  const [width, height] = [piece.width, piece.height];

  if (density === "card") {
    const [sequenceW, sequenceH] = CAD_LABEL_THRESHOLDS.card.sequence;
    return { dimensions: false, cutTag: width >= sequenceW && height >= sequenceH, area: false };
  }

  const [dimsW, dimsH] = CAD_LABEL_THRESHOLDS.inspect.dims;
  const [tagW, tagH] = CAD_LABEL_THRESHOLDS.inspect.tag;
  const [areaW, areaH] = CAD_LABEL_THRESHOLDS.inspect.area;

  return {
    dimensions: width >= dimsW && height >= dimsH,
    cutTag: width >= tagW && height >= tagH,
    area: width >= areaW && height >= areaH,
  };
}

/** `1600 × 1100 mm`, or the compact form for a piece too small to carry one. */
export function dimensionsText(piece: PackedPiece): string {
  return `${round(piece.width)} × ${round(piece.height)} mm`;
}

export function shortDimensionsText(piece: PackedPiece): string {
  return `${round(piece.width)}×${round(piece.height)}`;
}

export function areaText(piece: PackedPiece): string {
  return `${((piece.width * piece.height) / 1_000_000).toFixed(3)} m²`;
}

/** The application's own status line: `margins LM 15 · RM 15 · TM 15 · BM 15 · kerf 3 mm`. */
export function marginZoneText(margins: PackMargins): string {
  return `margins LM ${margins.left} · RM ${margins.right} · TM ${margins.top} · BM ${margins.bottom} · kerf ${margins.kerf} mm`;
}

/** Millimetres are whole numbers on a cutting plan; this keeps them that way. */
function round(value: number): number {
  return Math.round(value * 10) / 10;
}
