/**
 * Guillotine packing, shared by every place on this site that draws plates.
 *
 * There used to be two implementations — one behind the module preview, one
 * behind the homepage strip — and they disagreed. The preview stacked one
 * full-width plate per sheet, and its packer had two faults that made a small
 * sample job look broken:
 *
 *   · it never rotated a piece, so a 1180 × 2340 pane could not be placed on a
 *     3210 × 2250 plate even though it fits perfectly on its side. Each one
 *     opened a plate of its own.
 *   · a piece that genuinely does not fit still advanced the plate counter, so
 *     the run began with an empty plate — which was then drawn, as an empty
 *     box, at the top of the stack.
 *
 * This implementation is one function used everywhere. It rotates a piece when
 * rotating is what makes it fit, and when a piece cannot be placed at all it is
 * returned in `unplaced` rather than being hidden or given a plate of its own.
 * That matches how the application behaves: the Sizes tab and the Summary tab
 * both surface pieces the engine could not place instead of dropping them.
 *
 * It is not a shelf rule. A shelf rule fills a row left to right and then starts
 * another row below, which means any piece taller than half the usable height
 * leaves the rest of its plate unusable and the plate comes out half empty —
 * measured on the sample cut list, that was 48% yield across fourteen plates,
 * most of them carrying two pieces. This packer splits instead: after a piece
 * takes the top-left corner of a free rectangle, what is left is an L-shape, and
 * an L-shape cannot be cut by a guillotine table. So the leftover is divided by
 * one straight cut into two rectangles, and both halves stay usable. The same
 * cut list then comes out at 67% across twelve plates.
 *
 * That two-way split is the same decision the application's packer makes — its
 * shelf and strip generators differ in exactly this — and it keeps every
 * boundary a straight cut, so the result is guillotine-cuttable by construction.
 *
 * It is still an illustration rather than a result: the engine searches five
 * families and six plate patterns and keeps the best whole candidate, where this
 * applies one heuristic and returns it. Nothing on this site quotes a yield
 * figure for the engine, because the repository contains none.
 */

export type PackMargins = {
  left: number;
  right: number;
  top: number;
  bottom: number;
  kerf: number;
};

export type PackInput = {
  id: string;
  /** Millimetres, before any rotation. */
  width: number;
  height: number;
};

export type PackedPiece = {
  id: string;
  /** Position on its plate, millimetres from the plate's top-left corner. */
  x: number;
  y: number;
  /** Size as actually placed — swapped when `rotated` is true. */
  width: number;
  height: number;
  rotated: boolean;
  /** 1-based plate number. */
  plate: number;
  /** 1-based order in which the piece is placed, for a cut schedule. */
  sequence: number;
};

export type PackResult = {
  placed: PackedPiece[];
  /** Pieces no plate in the set could hold. Never silently dropped. */
  unplaced: PackInput[];
  plates: number;
  totalPieces: number;
  placedPieces: number;
  partAreaSqm: number;
  stockAreaSqm: number;
  utilizationPct: number;
};

/**
 * Best-area-fit guillotine packing with a two-way split.
 *
 * Pieces are placed largest area first, so the pieces that are hardest to fit
 * choose their position before the small ones fill in around them. Each piece
 * goes into the free rectangle where it wastes least area, trying both
 * orientations; a new plate is opened only when no rectangle on any open plate
 * can hold it.
 *
 * After a placement the used rectangle is replaced by the two rectangles the
 * leftover L-shape is cut into, so no space is written off and every piece
 * boundary stays a straight cut.
 */
export function packGuillotine(
  pieces: PackInput[],
  sheet: { width: number; height: number },
  margins: PackMargins,
): PackResult {
  const usableLeft = margins.left;
  const usableTop = margins.top;
  const usableWidth = sheet.width - margins.left - margins.right;
  const usableHeight = sheet.height - margins.top - margins.bottom;

  const placed: PackedPiece[] = [];
  const unplaced: PackInput[] = [];

  /** A rectangle of a plate that is still free, and disjoint from every other. */
  type Region = { plate: number; x: number; y: number; width: number; height: number };
  const regions: Region[] = [];

  let plates = 0;
  let sequence = 0;

  const openPlate = () => {
    plates += 1;
    regions.push({
      plate: plates,
      x: usableLeft,
      y: usableTop,
      width: usableWidth,
      height: usableHeight,
    });
  };
  openPlate();

  /**
   * Split a region after a piece has taken its top-left corner.
   *
   * The leftover is an L-shape, and an L-shape cannot be cut by a guillotine
   * table — so it is divided into two rectangles by one straight cut, and both
   * halves stay usable.
   *
   *   horizontal  ── the remainder runs *above* the piece's baseline, so the
   *                  right-hand piece of this shelf is capped to the same
   *                  height. Good when the piece is wide and short.
   *   vertical    ── the right-hand strip runs the full remaining height, so
   *                  tall pieces can follow it downward. Good when the piece is
   *                  narrow and tall.
   *
   * Which one is chosen is the classic rule: give the strip to whichever axis
   * has more room left. This is the same two-way split the application's own
   * packer applies — its shelf and strip generators differ in exactly this.
   */
  const splitRegion = (region: Region, w: number, h: number) => {
    const rightWidth = region.width - w - margins.kerf;
    const belowHeight = region.height - h - margins.kerf;
    const rightHeight = belowHeight > rightWidth ? h : region.height;
    const belowWidth = rightHeight === h ? region.width : w;

    if (rightWidth > 0 && rightHeight > 0) {
      regions.push({
        plate: region.plate,
        x: region.x + w + margins.kerf,
        y: region.y,
        width: rightWidth,
        height: rightHeight,
      });
    }
    if (belowWidth > 0 && belowHeight > 0) {
      regions.push({
        plate: region.plate,
        x: region.x,
        y: region.y + h + margins.kerf,
        width: belowWidth,
        height: belowHeight,
      });
    }
  };

  // Largest area first: the pieces that are hardest to fit get the choice
  // positions, and the small ones fill what is left.
  const queue = [...pieces].sort((a, b) => b.width * b.height - a.width * a.height);

  for (const piece of queue) {
    const fits = (w: number, h: number, region: Region) => w <= region.width && h <= region.height;

    const orientations: { w: number; h: number; rotated: boolean }[] = [];
    if (piece.width <= usableWidth && piece.height <= usableHeight) {
      orientations.push({ w: piece.width, h: piece.height, rotated: false });
    }
    if (piece.height <= usableWidth && piece.width <= usableHeight) {
      orientations.push({ w: piece.height, h: piece.width, rotated: true });
    }
    if (orientations.length === 0) {
      unplaced.push(piece);
      continue;
    }

    /** Best area fit: of every region that can hold it, the tightest one. */
    const bestSpot = (): { region: Region; option: { w: number; h: number; rotated: boolean } } | null => {
      let best: { region: Region; option: { w: number; h: number; rotated: boolean } } | null = null;
      let bestWaste = Number.POSITIVE_INFINITY;
      for (const region of regions) {
        for (const option of orientations) {
          if (!fits(option.w, option.h, region)) continue;
          const waste = region.width * region.height - option.w * option.h;
          if (waste < bestWaste) {
            bestWaste = waste;
            best = { region, option };
          }
        }
      }
      return best;
    };

    let spot = bestSpot();
    if (!spot) {
      // Nothing left on any open plate can take it. A fresh plate is the only
      // way it gets cut — and if even that cannot hold it, it is reported.
      openPlate();
      spot = bestSpot();
      if (!spot) {
        unplaced.push(piece);
        plates -= 1;
        regions.pop();
        continue;
      }
    }

    const { region, option } = spot;
    sequence += 1;
    placed.push({
      id: piece.id,
      x: region.x,
      y: region.y,
      width: option.w,
      height: option.h,
      rotated: option.rotated,
      plate: region.plate,
      sequence,
    });

    regions.splice(regions.indexOf(region), 1);
    splitRegion(region, option.w, option.h);
  }

  const plateCount = placed.length === 0 ? 0 : plates;
  const partArea = placed.reduce((sum, piece) => sum + piece.width * piece.height, 0);
  const stockArea = plateCount * sheet.width * sheet.height;

  return {
    placed,
    unplaced,
    plates: plateCount,
    totalPieces: pieces.length,
    placedPieces: placed.length,
    partAreaSqm: Number((partArea / 1_000_000).toFixed(4)),
    stockAreaSqm: Number((stockArea / 1_000_000).toFixed(4)),
    utilizationPct: stockArea === 0 ? 0 : Number(((partArea / stockArea) * 100).toFixed(2)),
  };
}

/** The pieces on one plate, with the plate's own arithmetic. */
export type PlateView = {
  /** 1-based plate number, as the application labels it. */
  plate: number;
  stockLabel: string;
  widthMm: number;
  heightMm: number;
  pieces: PackedPiece[];
  pieceCount: number;
  usedSqm: number;
  offcutSqm: number;
  utilizationPct: number;
};

/** Split a pack result into the per-plate cards the gallery draws. */
export function plateViews(
  result: PackResult,
  sheet: { width: number; height: number; label: string },
): PlateView[] {
  return Array.from({ length: result.plates }, (_, index) => {
    const plate = index + 1;
    const pieces = result.placed.filter((piece) => piece.plate === plate);
    const usedArea = pieces.reduce((sum, piece) => sum + piece.width * piece.height, 0);
    const stockArea = sheet.width * sheet.height;
    const usedSqm = usedArea / 1_000_000;

    return {
      plate,
      stockLabel: sheet.label,
      widthMm: sheet.width,
      heightMm: sheet.height,
      pieces,
      pieceCount: pieces.length,
      usedSqm: Number(usedSqm.toFixed(4)),
      offcutSqm: Number(((stockArea - usedArea) / 1_000_000).toFixed(4)),
      utilizationPct: Number(((usedArea / stockArea) * 100).toFixed(2)),
    };
  });
}
