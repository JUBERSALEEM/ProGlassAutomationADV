/**
 * Hero banner renderer — README_ASSETS/hero.png.
 *
 * The banner is the repository's front page (README.md) and the site's brand
 * plate on `/`, and it is also the Open Graph image every shared link carries.
 * It used to be a stock illustration of a cutting table in teal — a picture of
 * the industry rather than of the product, and in colours the application does
 * not use. This script redraws it as the product's own interface, in the site's
 * own tokens.
 *
 * The screen it draws is the **2D View** tab of the Optimization module, and
 * that is the point of the plate: what the product does for a glass shop is
 * nest a cutting list across stock sheets and show the operator where every
 * piece comes from. The window's main area is therefore the light studio
 * drawing — the stone bed, the near-white pieces, the teal trim boundary and
 * the indigo cut order — and beside it the run's own cut schedule and figures.
 *
 * Three rules keep it honest:
 *
 *   1. No headless browser is involved. The composition is SVG built here and
 *      rasterised with sharp, so the output is reproducible from the repository
 *      alone — `npm run assets:banner`.
 *   2. Every figure on it is read from `src/data`. The plate is packed here, at
 *      render time, by the same `packGuillotine` the site's own preview uses,
 *      over the same twenty-two-size cutting list the Optimization module
 *      publishes; the sheet, the trims and the kerf are the house Thin profile;
 *      the schedule rows, the utilisation and the wastage are read back out of
 *      that result. The module count, the rail grouping and the chain verbs are
 *      the data modules. Nothing here is typed in by hand, so the banner cannot
 *      drift away from the product and start advertising something that does
 *      not exist.
 *   3. The colours are the theme tokens themselves (`src/app/globals.css`,
 *      midnight) and the studio tokens the 2D view is painted from, read live
 *      by `scripts/lib/theme-tokens.mts` — this file owns no colour of its own.
 *      If a token moves, this file moves with it.
 *
 * Usage, from `website/`:
 *
 *   npm run assets:banner       # writes both outputs below
 *   npm run assets:plates       # both plates, then the web derivatives
 *   npm run assets:optimize                     # re-encodes the web derivatives
 *
 * Two outputs, because they are used at different sizes:
 *
 *   ../README_ASSETS/hero.png                         1920 × 640 — the README
 *   public/images/brand/proglass-hero-banner-3840.png 3840 × 1280 — the site
 *
 * The site master is the full supersampled raster, not a copy of the README
 * one. The banner is drawn edge to edge across the shell, so on a 1920 px
 * screen at 2× it is asked for at roughly 3584 device pixels; a 1920 px file
 * was being enlarged to meet that, which is what made it look soft. The
 * renderer already rasterises at `SUPERSAMPLE ×` — this keeps that raster
 * instead of throwing it away.
 *
 * Runtime prerequisites: `sharp` (a devDependency) and the two families the
 * site self-hosts. The font files in `src/fonts` are woff2; if this script
 * cannot find an installed Inter or JetBrains Mono it still renders, falling
 * back to whatever sans/mono the machine has, so a missing font is a cosmetic
 * difference and never a failed build.
 */
import path from "node:path";
import { mkdir } from "node:fs/promises";
import sharp from "sharp";

import { demoFor, optimizerLimits, sheetSizes } from "@/data/liveDemos";
import { navIcons } from "@/data/navIcons";
import { chainVerbs } from "@/data/workflows";
import { modules } from "@/data/modules";
import { quotationTotals } from "@/data/workedExample";
import { packGuillotine, plateViews, type PackMargins, type PlateView } from "@/lib/guillotinePack";
import {
  breakoutZone,
  dimensionsText,
  labelsFor,
  marginZoneText,
  pieceRef,
  reusableOffcut,
  shortDimensionsText,
  usableZone,
} from "@/components/optimization/cad";
import { site } from "@/lib/site";
import { palette, studio } from "./lib/theme-tokens.mts";

/* ══════════════════════════════════════════════════════════════════════════
 * The canvas
 * ════════════════════════════════════════════════════════════════════════ */

/** 3:1, which is the box `ProductHero` draws with `aspect-[3/1]`. */
const WIDTH = 1920;
const HEIGHT = 640;

/**
 * Supersampling factor. The SVG is authored in design units and rendered at
 * `SUPERSAMPLE ×`, then resampled down — text edges at 1920 px wide are the
 * whole difference between "a screenshot" and "a blurry screenshot".
 */
const SUPERSAMPLE = 2;

/* ══════════════════════════════════════════════════════════════════════════
 * Tokens
 *
 * `palette` is read live from `[data-theme="midnight"]` in
 * `src/app/globals.css` by `scripts/lib/theme-tokens.mts` — this file owns no
 * colours of its own, so re-rendering after a theme change is enough to bring
 * the plate with it.
 * ════════════════════════════════════════════════════════════════════════ */


/**
 * The two families the site self-hosts (`src/fonts`). The fallbacks are the
 * families a bare Linux container is most likely to have; a banner rendered in
 * DejaVu is still a correct banner.
 */
const SANS = "Inter, DejaVu Sans, Helvetica, sans-serif";
const MONO = "JetBrains Mono, DejaVu Sans Mono, Consolas, monospace";

/* ══════════════════════════════════════════════════════════════════════════
 * Drawing helpers
 * ════════════════════════════════════════════════════════════════════════ */

function escapeXml(value: string): string {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

/** A number with at most two decimals, so the SVG stays readable. */
function n(value: number): string {
  return String(Math.round(value * 100) / 100);
}

/** `rgb(255 255 255 / 0.5)` in the stylesheets → `rgba(255,255,255,0.5)` here. */
function rgba(hex: string, alpha: number): string {
  const value = hex.replace("#", "");
  const r = Number.parseInt(value.slice(0, 2), 16);
  const g = Number.parseInt(value.slice(2, 4), 16);
  const b = Number.parseInt(value.slice(4, 6), 16);
  return `rgba(${r},${g},${b},${alpha})`;
}

type TextOptions = {
  size: number;
  fill: string;
  family?: string;
  weight?: number;
  anchor?: "start" | "middle" | "end";
  /** Em units, matching the CSS `tracking-[…]` values used on the site. */
  tracking?: number;
  opacity?: number;
};

function text(x: number, y: number, value: string, options: TextOptions): string {
  const {
    size,
    fill,
    family = SANS,
    weight = 400,
    anchor = "start",
    tracking = 0,
    opacity = 1,
  } = options;
  const attributes = [
    `x="${n(x)}"`,
    `y="${n(y)}"`,
    `font-family="${family}"`,
    `font-size="${n(size)}"`,
    `font-weight="${weight}"`,
    `fill="${fill}"`,
    anchor === "start" ? "" : `text-anchor="${anchor}"`,
    tracking === 0 ? "" : `letter-spacing="${n(size * tracking)}"`,
    opacity === 1 ? "" : `opacity="${n(opacity)}"`,
  ].filter(Boolean);
  return `<text ${attributes.join(" ")}>${escapeXml(value)}</text>`;
}

type RectOptions = {
  fill?: string;
  stroke?: string;
  strokeWidth?: number;
  radius?: number;
  opacity?: number;
  dash?: string;
};

function rect(x: number, y: number, width: number, height: number, options: RectOptions = {}): string {
  const { fill = "none", stroke, strokeWidth = 1, radius = 0, opacity = 1, dash } = options;
  const attributes = [
    `x="${n(x)}"`,
    `y="${n(y)}"`,
    `width="${n(width)}"`,
    `height="${n(height)}"`,
    radius > 0 ? `rx="${n(radius)}"` : "",
    `fill="${fill}"`,
    stroke ? `stroke="${stroke}"` : "",
    stroke ? `stroke-width="${n(strokeWidth)}"` : "",
    dash ? `stroke-dasharray="${dash}"` : "",
    opacity === 1 ? "" : `opacity="${n(opacity)}"`,
  ].filter(Boolean);
  return `<rect ${attributes.join(" ")} />`;
}

function line(x1: number, y1: number, x2: number, y2: number, stroke: string, strokeWidth = 1, dash?: string): string {
  return (
    `<line x1="${n(x1)}" y1="${n(y1)}" x2="${n(x2)}" y2="${n(y2)}" ` +
    `stroke="${stroke}" stroke-width="${n(strokeWidth)}"${dash ? ` stroke-dasharray="${dash}"` : ""} />`
  );
}

/** A small pill: fill, hairline border, one line of text. */
function pill(
  x: number,
  y: number,
  label: string,
  options: {
    height?: number;
    fill: string;
    stroke: string;
    color: string;
    size?: number;
    family?: string;
    weight?: number;
    tracking?: number;
    padX?: number;
  },
): { svg: string; width: number } {
  const {
    height = 26,
    fill,
    stroke,
    color,
    size = 15.5,
    family = MONO,
    weight = 500,
    tracking = 0.06,
    padX = 11,
  } = options;
  // Advance width per glyph at the given size, per family. Deliberately
  // generous: a pill that is 4 px too wide is invisible, one that is 4 px too
  // narrow clips its last character.
  const perGlyph = family === MONO ? 0.6 : 0.55;
  const width = Math.round(label.length * size * perGlyph + padX * 2 + label.length * size * tracking);
  const svg =
    rect(x, y, width, height, { fill, stroke, strokeWidth: 1, radius: height / 2 }) +
    text(x + width / 2, y + height / 2 + size * 0.36, label, { size, fill: color, family, weight, anchor: "middle", tracking });
  return { svg, width };
}

/* ══════════════════════════════════════════════════════════════════════════
 * Figures the banner quotes — all derived
 * ════════════════════════════════════════════════════════════════════════ */

/*
 * The run the banner draws.
 *
 * The cutting list, the sheet and the margins are the Optimization module's
 * own: `demoFor("Optimization")` is the same accessor the module's live preview
 * on `/optimization` reads, so its twenty-two sizes are the twenty-two sizes
 * the preview runs on, and the margins are the house Thin profile out of
 * `src/data/liveDemos.ts` (15 mm on all four edges, 3 mm blade gap for 6 mm
 * glass, from `Services/Optimization/OptimizationModels.cs`).
 *
 * The packing is then run here, at render time, by the same `packGuillotine`
 * the preview and the homepage use. Every number the banner prints about the
 * run — the plate it is showing, the schedule rows on it, the sheets used, the
 * utilisation, the wastage — is read back out of that result, which is why the
 * banner cannot carry a yield figure the product does not actually produce.
 */
const optimization = demoFor("Optimization", "Optimization report", ["Sheets used", "Utilization", "Wastage"]);
const cutList = optimization.views[0]?.rows ?? [];

/** The sheet the preview opens on, and the profile it cuts to. */
const runSheet = sheetSizes[1] ?? sheetSizes[0]!;
const runMargins: PackMargins = {
  left: optimizerLimits.marginLeft,
  right: optimizerLimits.marginRight,
  top: optimizerLimits.marginTop,
  bottom: optimizerLimits.marginBottom,
  kerf: optimizerLimits.kerf,
};

/** Every row of the cutting list, expanded to its pieces, as the preview does. */
const runParts = cutList.flatMap((row) => {
  const width = Number(row.w ?? 0);
  const height = Number(row.h ?? 0);
  const quantity = Math.max(1, Number(row.qty ?? 1));
  return Array.from({ length: Math.min(quantity, 12) }, (_, index) => ({
    id: `${row.piece ?? "P"}-${index + 1}`,
    width,
    height,
  }));
});

const run = packGuillotine(runParts, { width: runSheet.w, height: runSheet.h }, runMargins);
const runPlates: PlateView[] = plateViews(run, {
  width: runSheet.w,
  height: runSheet.h,
  label: `${runSheet.w} × ${runSheet.h}`,
});

/*
 * The plate the banner shows.
 *
 * The third one, not the first: the application opens the 2D view on plate 1
 * and plate 1 of this job carries two large panes, which is honest but reads as
 * an empty sheet at banner size. Plate 3 is the plate that shows what the
 * nesting actually does — six pieces, one of them turned through 90°, a
 * removable strip left beside them — and it is the same drawing either way,
 * every number on it derived from the same result. Falls back to whatever
 * exists, so a change to the cutting list cannot leave the banner blank.
 */
const bannerPlate = runPlates[2] ?? runPlates[0]!;
const schedule = [...bannerPlate.pieces].sort((a, b) => a.sequence - b.sequence);
const wastageSqm = run.stockAreaSqm - run.partAreaSqm;
const runPlatesUsed = run.plates;

/** Money, the way the application and the site both print it. */
function money(value: number): string {
  return value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/*
 * The rail: the groups the drawn screen sits in, the way `GlassShell.xaml`
 * groups them.
 *
 * Optimization is a Production module, so the rail is drawn from the top down
 * to it — Main, Inventory, Sales, Production — and the active entry is the
 * module the workspace beside it is showing. That is the whole reason the list
 * is not simply the first five modules: a rail that lit Dashboard while the
 * workspace drew the 2D view would be a picture of nothing.
 */
const RAIL_KEYS = [
  "Dashboard",
  "SheetStore",
  "Specifications",
  "Inquiry",
  "ProformaInvoice",
  "JobOrder",
  "Optimization",
];
const ACTIVE_RAIL_KEY = "Optimization";
const railEntries = RAIL_KEYS.map((key) => modules.find((entry) => entry.moduleKey === key)).filter(
  (entry): entry is (typeof modules)[number] => Boolean(entry),
);
const railGroups: { group: string; items: typeof railEntries }[] = [];
for (const entry of railEntries) {
  const existing = railGroups.find((candidate) => candidate.group === entry.railGroup);
  if (existing) existing.items.push(entry);
  else railGroups.push({ group: entry.railGroup, items: [entry] });
}
const railGroupCount = new Set(modules.map((entry) => entry.railGroup)).size;

/* ══════════════════════════════════════════════════════════════════════════
 * The composition
 * ════════════════════════════════════════════════════════════════════════ */

/** The drawing, in paint order: backdrop, window, figure, panel. */
const layers: string[] = [];

/* ── Backdrop ─────────────────────────────────────────────────────────────
 * The same recipe the site's `body` uses: a violet bed, four ambient pools,
 * a masked 56 px grid, and a vignette so the edges fall away. */
layers.push(`<rect width="${WIDTH}" height="${HEIGHT}" fill="${palette.ink950}" />`);
layers.push(`<rect width="${WIDTH}" height="${HEIGHT}" fill="url(#bed)" />`);

for (let x = 0; x <= WIDTH; x += 56) {
  layers.push(line(x, 0, x, HEIGHT, rgba(palette.brand500, 0.05), 1));
}
for (let y = 0; y <= HEIGHT; y += 56) {
  layers.push(line(0, y, WIDTH, y, rgba(palette.brand500, 0.05), 1));
}

layers.push(`<ellipse cx="240" cy="-60" rx="520" ry="380" fill="url(#poolViolet)" />`);
layers.push(`<ellipse cx="1620" cy="60" rx="420" ry="300" fill="url(#poolCyan)" />`);
layers.push(`<ellipse cx="640" cy="700" rx="420" ry="300" fill="url(#poolRose)" />`);
layers.push(`<rect width="${WIDTH}" height="${HEIGHT}" fill="url(#vignette)" />`);
layers.push(
  `<rect x="0" y="0" width="${WIDTH}" height="1.5" fill="url(#topHairline)" />`,
);

/* ── The application window ───────────────────────────────────────────────
 * Right two thirds. It is a drawing of the Optimization screen, not a
 * screenshot — but the plate geometry inside it is the geometry the site's own
 * optimizer preview draws, from the same packer. */
const win = { x: 736, y: 100, width: 1120, height: 448 };
const rail = { x: win.x + 16, y: win.y + 68, width: 220, height: win.height - 84 };
const main = { x: win.x + 252, y: win.y + 64, width: win.width - 272 };

layers.push(
  // Shadow, then a violet bloom, then the frame.
  rect(win.x + 6, win.y + 18, win.width, win.height, { fill: palette.brand700, radius: 22, opacity: 0.5 }),
  rect(win.x - 4, win.y - 4, win.width + 8, win.height + 8, {
    fill: "none",
    stroke: rgba(palette.brand500, 0.18),
    strokeWidth: 2,
    radius: 24,
  }),
  rect(win.x, win.y, win.width, win.height, {
    fill: rgba(palette.ink900, 0.92),
    stroke: palette.lineStrong,
    strokeWidth: 1.5,
    radius: 20,
  }),
  line(win.x + 22, win.y + 1.5, win.x + win.width - 22, win.y + 1.5, rgba("#FFFFFF", 0.14), 1.5),
  line(win.x, win.y + 52, win.x + win.width, win.y + 52, palette.lineSoft, 1.5),
);

/* Title bar. */
for (const [index, colour] of [palette.rose, "#FBBF24", palette.green].entries()) {
  layers.push(`<circle cx="${win.x + 26 + index * 16}" cy="${win.y + 26}" r="5" fill="${rgba(colour, 0.55)}" />`);
}
layers.push(
  text(win.x + 84, win.y + 31, `${site.name} · Optimization`, { size: 15, fill: palette.text1, weight: 600 }),
  text(win.x + win.width - 252, win.y + 31, `v${site.version}`, {
    size: 15.5,
    fill: rgba(palette.text3, 0.85),
    family: MONO,
    anchor: "end",
  }),
);
/* The window's own top-right controls, as the frame on `/` draws them. */
layers.push(
  text(win.x + win.width - 196, win.y + 31, "10:30", {
    size: 14.5,
    fill: rgba(palette.text3, 0.55),
    family: MONO,
    anchor: "end",
  }),
  rect(win.x + win.width - 184, win.y + 18, 20, 20, { fill: palette.ink800, stroke: palette.lineStrong, strokeWidth: 1, radius: 5 }),
);
const adminChip = pill(win.x + win.width - 156, win.y + 17, "ADMIN", {
  fill: rgba(palette.ink800, 0.9),
  stroke: palette.lineStrong,
  color: rgba(palette.text3, 0.92),
  height: 24,
  size: 14.5,
  padX: 24,
});
layers.push(
  `<circle cx="${win.x + win.width - 145}" cy="${win.y + 28}" r="8" fill="${rgba(palette.brand400, 0.22)}" />`,
  text(win.x + win.width - 145, win.y + 33, "AB", { size: 13, fill: palette.brand200, family: MONO, weight: 700, anchor: "middle" }),
  adminChip.svg,
);

/*
 * Rail.
 *
 * Seven entries in four groups, drawn at a 30 px pitch rather than the 32 the
 * application uses, because the window is 448 px tall and the point of the
 * drawing is that the rail reaches the entry the workspace is showing rather
 * than stopping above it. The active row is the application's own: a violet
 * wash, a 3 px bar on the leading edge, and the label in the brighter ink.
 */
layers.push(
  rect(rail.x, rail.y, rail.width, rail.height, {
    fill: rgba(palette.ink850, 0.85),
    stroke: palette.lineSoft,
    strokeWidth: 1,
    radius: 14,
  }),
);
const RAIL_PITCH = 30;
let railCursor = rail.y + 32;
for (const group of railGroups) {
  layers.push(
    text(rail.x + 20, railCursor, group.group.toUpperCase(), {
      size: 14,
      fill: rgba(palette.text3, 0.7),
      family: MONO,
      weight: 600,
      tracking: 0.2,
    }),
  );
  railCursor += 14;
  for (const item of group.items) {
    const active = item.moduleKey === ACTIVE_RAIL_KEY;
    if (active) {
      layers.push(
        rect(rail.x + 10, railCursor, rail.width - 20, 26, { fill: rgba(palette.brand500, 0.16), radius: 9 }),
        rect(rail.x + 10, railCursor + 4, 3, 18, { fill: palette.brand400, radius: 2 }),
      );
    }
    layers.push(
      text(rail.x + 26, railCursor + 18, item.railLabel, {
        size: 15,
        fill: active ? palette.brand200 : rgba(palette.text3, 0.86),
        weight: active ? 600 : 450,
      }),
    );
    railCursor += RAIL_PITCH;
  }
  railCursor += 10;
}

/* ── The workspace: the 2D View tab of the Optimization module ───────────
 *
 * This is the screen the product is bought for. The module nests a cutting
 * list across stock sheets and reports where every piece comes from; its 2D
 * View tab is the light studio drawing — a stone bed, near-white panes outlined
 * in the saw-channel ink, a trimmed boundary, and the cut order — with the
 * plate's own cut schedule beside it.
 *
 * The drawing is deliberately light inside a dark window. That is not a
 * mismatch: a plate is a sheet of drawing paper lying on the application's
 * surface, and the brushes are the ones the application itself paints its cut
 * bed with (`Views/Optimization/Tabs/TwoDViewTabView.xaml` → `OptCutBed`,
 * `OptPieceFill`, `OptCutInk`, `OptTrimInk`). The site's own live preview draws
 * the same screen the same way.
 *
 * Three cards, all of them from the same packing result: the sheet, the cut
 * schedule for it, and the run's figures. Nothing on them is typed in.
 */
layers.push(
  text(main.x, main.y + 24, "Optimization · 2D View", {
    size: 23,
    fill: palette.text1,
    weight: 700,
    tracking: -0.01,
  }),
  text(main.x, main.y + 42, `${runSheet.w} × ${runSheet.h} mm · house Thin profile`, {
    size: 15.5,
    fill: rgba(palette.text3, 0.85),
    family: MONO,
  }),
);

const contentTop = main.y + 52;
const contentHeight = win.y + win.height - 20 - contentTop;
const sheetPanel = { x: main.x, y: contentTop, width: 508, height: contentHeight };
const sideWidth = main.x + main.width - (sheetPanel.x + sheetPanel.width + 14);
const sidePanel = { x: sheetPanel.x + sheetPanel.width + 14, y: contentTop, width: sideWidth, height: contentHeight };
const schedulePanel = { x: sidePanel.x, y: sidePanel.y, width: sidePanel.width, height: 222 };
const figuresPanel = {
  x: sidePanel.x,
  y: schedulePanel.y + schedulePanel.height + 12,
  width: sidePanel.width,
  height: sidePanel.height - schedulePanel.height - 12,
};

/**
 * One plate, drawn the way the application's 2D view draws it.
 *
 * The same drawing the site renders in `src/components/optimization/PlateCad`,
 * built here as SVG text rather than as React: the stone bed, the pieces at
 * their placed coordinates (a piece turned through 90° in the light-sky tint
 * and the accent outline), the teal trim boundary, the amber breakout band, the
 * reusable remnant, and the cut order as an accent chip on the pieces with room
 * for one.
 *
 * Geometry is in millimetres and the whole drawing hangs off one `scale`
 * transform, so the stroke widths and the type sizes scale with it — which is
 * what makes this the same drawing at any size. Which labels a piece may carry
 * is decided by `labelsFor` from `src/components/optimization/cad.ts`, the same
 * thresholds the React drawing uses, so a piece too small for its label goes
 * without one here too instead of carrying a smudge.
 */
function plateDrawing(
  plate: PlateView,
  margins: PackMargins,
  box: { x: number; y: number; width: number; height: number },
): string {
  const scale = Math.min(box.width / plate.widthMm, box.height / plate.heightMm);
  const originX = box.x + (box.width - plate.widthMm * scale) / 2;
  const originY = box.y + (box.height - plate.heightMm * scale) / 2;

  /** A hairline, expressed in millimetres so it stays a hairline at any scale. */
  const hair = (pixels: number) => pixels / scale;

  const parts: string[] = [];

  const usable = usableZone(plate, margins);
  const breakout = breakoutZone(usable, optimizerLimits.minBreakout);
  const offcut = reusableOffcut(plate, margins);

  // The stock, on a violet rim rather than a black one.
  parts.push(
    rect(0, 0, plate.widthMm, plate.heightMm, {
      fill: studio.bed,
      stroke: rgba(studio.accent, 0.55),
      strokeWidth: hair(1),
      radius: plate.widthMm * 0.004,
    }),
    // The trim boundary, dashed teal, then the breakout band inside it.
    rect(usable.x, usable.y, usable.width, usable.height, {
      fill: "none",
      stroke: studio.trim,
      strokeWidth: hair(1),
      dash: `${n(hair(7))} ${n(hair(3.5))}`,
    }),
    rect(breakout.x, breakout.y, breakout.width, breakout.height, {
      fill: "none",
      stroke: studio.breakout,
      strokeWidth: hair(0.8),
      opacity: 0.5,
      dash: `${n(hair(3))} ${n(hair(3))}`,
    }),
  );

  const ordered = [...plate.pieces].sort((a, b) => a.sequence - b.sequence);

  ordered.forEach((piece, index) => {
    const labels = labelsFor(piece, "inspect");
    const reference = plate.widthMm / 52;
    const dimensions = plate.widthMm / 48;
    const centreX = piece.x + piece.width / 2;
    const centreY = piece.y + piece.height / 2;

    parts.push(
      rect(piece.x, piece.y, piece.width, piece.height, {
        fill: piece.rotated ? studio.pieceRotated : studio.piece,
        stroke: piece.rotated ? studio.accent : studio.ink,
        strokeWidth: hair(1.2),
        radius: plate.widthMm * 0.0015,
      }),
    );

    // The rotated mark: a triangle in the piece's top-right corner, as the app
    // marks one, so the tint is not the only thing saying a piece was turned.
    if (piece.rotated) {
      const mark = plate.widthMm * 0.035;
      parts.push(
        `<polygon points="${n(piece.x + piece.width - mark)},${n(piece.y)} ${n(piece.x + piece.width)},${n(piece.y)} ${n(piece.x + piece.width)},${n(piece.y + mark)}" fill="${studio.accent}" fill-opacity="0.5" />`,
      );
    }

    if (labels.dimensions) {
      parts.push(
        text(centreX, centreY - reference * 0.35, pieceRef(piece.id), {
          size: reference,
          fill: studio.ink,
          weight: 600,
          anchor: "middle",
        }),
        text(centreX, centreY + dimensions * 0.85, dimensionsText(piece), {
          size: dimensions,
          fill: studio.ink,
          weight: 700,
          anchor: "middle",
        }),
      );
    } else {
      parts.push(
        text(centreX, centreY + reference * 0.35, shortDimensionsText(piece), {
          size: plate.widthMm / 74,
          fill: studio.ink,
          weight: 700,
          anchor: "middle",
        }),
      );
    }

    // The cut order, on a piece with room for a chip: the number, in the
    // application's accent, on the panel it prints it on.
    if (labels.cutTag) {
      const chip = plate.widthMm / 74;
      parts.push(
        rect(piece.x + plate.widthMm * 0.006, piece.y + plate.widthMm * 0.006, chip * 2.6, chip * 1.9, {
          fill: studio.accent,
          radius: chip * 0.4,
        }),
        text(piece.x + plate.widthMm * 0.006 + chip * 1.3, piece.y + plate.widthMm * 0.006 + chip * 0.98, String(index + 1), {
          size: chip * 1.1,
          fill: "#FFFFFF",
          family: MONO,
          weight: 700,
          anchor: "middle",
        }),
      );
    }
  });

  // The reusable remnant, when there is one worth going back on the rack.
  if (offcut) {
    parts.push(
      rect(offcut.x, offcut.y, offcut.width, offcut.height, {
        fill: studio.offcutBg,
        stroke: studio.offcutLine,
        strokeWidth: hair(1),
        dash: `${n(hair(5))} ${n(hair(3.5))}`,
        radius: plate.widthMm * 0.002,
      }),
    );
    if (offcut.width > plate.widthMm * 0.18 && offcut.height > plate.widthMm * 0.08) {
      parts.push(
        text(offcut.x + offcut.width / 2, offcut.y + offcut.height / 2 + plate.widthMm / 90, `${Math.round(offcut.width)} × ${Math.round(offcut.height)}`, {
          size: plate.widthMm / 74,
          fill: studio.accentDeep,
          weight: 600,
          anchor: "middle",
        }),
      );
    }
  }

  return `<g transform="translate(${n(originX)} ${n(originY)}) scale(${n(scale)})">${parts.join("")}</g>`;
}

/* The sheet, with the plate on it. */
layers.push(
  rect(sheetPanel.x + 4, sheetPanel.y + 9, sheetPanel.width, sheetPanel.height, {
    fill: rgba(palette.brand700, 0.45),
    radius: 14,
  }),
  rect(sheetPanel.x, sheetPanel.y, sheetPanel.width, sheetPanel.height, {
    fill: studio.surface,
    stroke: studio.line,
    strokeWidth: 1.5,
    radius: 14,
  }),
  text(sheetPanel.x + 18, sheetPanel.y + 25, `Plate ${bannerPlate.plate} of ${runPlatesUsed}`, {
    size: 16.5,
    fill: studio.text,
    weight: 600,
  }),
  text(sheetPanel.x + sheetPanel.width - 18, sheetPanel.y + 25, `${runSheet.w} × ${runSheet.h} mm`, {
    size: 14.5,
    fill: studio.muted,
    family: MONO,
    anchor: "end",
  }),
  line(sheetPanel.x + 18, sheetPanel.y + 37, sheetPanel.x + sheetPanel.width - 18, sheetPanel.y + 37, studio.keyline, 1.5),
);
layers.push(
  plateDrawing(bannerPlate, runMargins, {
    x: sheetPanel.x + 18,
    y: sheetPanel.y + 46,
    width: sheetPanel.width - 36,
    height: sheetPanel.height - 76,
  }),
);
/*
 * The sheet's own settings, printed under it in the application's wording:
 * `margins LM 15 · RM 15 · TM 15 · BM 15 · kerf 3 mm`. The chip is explained
 * beside them rather than left to be guessed at.
 */
layers.push(
  text(
    sheetPanel.x + 18,
    sheetPanel.y + sheetPanel.height - 22,
    `${run.placedPieces} of ${run.totalPieces} pieces placed · ${runPlatesUsed} sheets`,
    { size: 13.5, fill: studio.muted, family: MONO },
  ),
  text(sheetPanel.x + sheetPanel.width - 18, sheetPanel.y + sheetPanel.height - 22, "chip = cut order", {
    size: 13.5,
    fill: studio.subtle,
    family: MONO,
    anchor: "end",
  }),
  text(sheetPanel.x + 18, sheetPanel.y + sheetPanel.height - 8, marginZoneText(runMargins), {
    size: 13.5,
    fill: studio.subtle,
    family: MONO,
  }),
);

/*
 * The cut schedule for the plate beside it — the application's own list, in
 * placement order: the strip the operator works through, one row per piece,
 * with the turn flagged where a piece was rotated to fit.
 *
 * The card is drawn for six rows because that is what the window has room for.
 * The header states the plate's full piece count regardless, so a change to the
 * cutting list can shorten this list but cannot make it lie about the plate.
 */
const SCHEDULE_ROWS = 6;
const scheduleRows = schedule.slice(0, SCHEDULE_ROWS);
layers.push(
  rect(schedulePanel.x + 4, schedulePanel.y + 9, schedulePanel.width, schedulePanel.height, {
    fill: rgba(palette.brand700, 0.45),
    radius: 14,
  }),
  rect(schedulePanel.x, schedulePanel.y, schedulePanel.width, schedulePanel.height, {
    fill: studio.surface,
    stroke: studio.line,
    strokeWidth: 1.5,
    radius: 14,
  }),
  text(schedulePanel.x + 18, schedulePanel.y + 25, "Cut schedule", {
    size: 16.5,
    fill: studio.text,
    weight: 600,
  }),
  text(schedulePanel.x + schedulePanel.width - 18, schedulePanel.y + 25, `${bannerPlate.pieceCount} cuts`, {
    size: 14.5,
    fill: studio.muted,
    family: MONO,
    anchor: "end",
  }),
  line(schedulePanel.x + 18, schedulePanel.y + 37, schedulePanel.x + schedulePanel.width - 18, schedulePanel.y + 37, studio.keyline, 1.5),
);

const scheduleColumns = {
  seq: schedulePanel.x + 18,
  reference: schedulePanel.x + 84,
  size: schedulePanel.x + schedulePanel.width - 96,
  rotated: schedulePanel.x + schedulePanel.width - 18,
};
layers.push(
  text(scheduleColumns.seq, schedulePanel.y + 58, "SEQ", {
    size: 12.5,
    fill: studio.subtle,
    family: MONO,
    weight: 600,
    tracking: 0.14,
  }),
  text(scheduleColumns.reference, schedulePanel.y + 58, "REF", {
    size: 12.5,
    fill: studio.subtle,
    family: MONO,
    weight: 600,
    tracking: 0.14,
  }),
  text(scheduleColumns.size, schedulePanel.y + 58, "CUT SIZE", {
    size: 12.5,
    fill: studio.subtle,
    family: MONO,
    weight: 600,
    tracking: 0.14,
    anchor: "end",
  }),
  text(scheduleColumns.rotated, schedulePanel.y + 58, "ROT", {
    size: 12.5,
    fill: studio.subtle,
    family: MONO,
    weight: 600,
    tracking: 0.14,
    anchor: "end",
  }),
);

const SCHEDULE_PITCH = 24;
scheduleRows.forEach((piece, index) => {
  const y = schedulePanel.y + 84 + index * SCHEDULE_PITCH;
  layers.push(
    line(schedulePanel.x + 18, y - 17, schedulePanel.x + schedulePanel.width - 18, y - 17, studio.keyline, 1),
    text(scheduleColumns.seq, y, String(index + 1), {
      size: 14.5,
      fill: studio.muted,
      family: MONO,
    }),
    text(scheduleColumns.reference, y, pieceRef(piece.id), {
      size: 15,
      fill: studio.text,
      weight: 600,
    }),
    text(scheduleColumns.size, y, `${Math.round(piece.width)} × ${Math.round(piece.height)}`, {
      size: 14.5,
      fill: studio.ink,
      family: MONO,
      anchor: "end",
    }),
    text(scheduleColumns.rotated, y, piece.rotated ? "90°" : "—", {
      size: 14.5,
      fill: piece.rotated ? studio.accentDeep : studio.subtle,
      family: MONO,
      anchor: "end",
    }),
  );
});

/*
 * The run's figures — the three an office reads off the report, read back out
 * of the same result the plate was packed from. Utilisation is the run's own
 * ratio of part area to stock area, printed with the application's yield badge
 * around it rather than as a headline of its own.
 */
const figureRows: { label: string; value: string; badge?: boolean }[] = [
  { label: "Sheets used", value: String(runPlatesUsed) },
  { label: "Utilization", value: `${run.utilizationPct.toFixed(2)} %`, badge: true },
  { label: "Wastage", value: `${wastageSqm.toFixed(2)} m²` },
];
layers.push(
  rect(figuresPanel.x + 4, figuresPanel.y + 9, figuresPanel.width, figuresPanel.height, {
    fill: rgba(palette.brand700, 0.45),
    radius: 14,
  }),
  rect(figuresPanel.x, figuresPanel.y, figuresPanel.width, figuresPanel.height, {
    fill: studio.surface,
    stroke: studio.line,
    strokeWidth: 1.5,
    radius: 14,
  }),
);
figureRows.forEach((row, index) => {
  const y = figuresPanel.y + 26 + index * 22;
  if (index > 0) {
    layers.push(line(figuresPanel.x + 18, y - 16, figuresPanel.x + figuresPanel.width - 18, y - 16, studio.keyline, 1));
  }
  layers.push(
    text(figuresPanel.x + 18, y, row.label, {
      size: 14.5,
      fill: studio.muted,
    }),
  );
  if (row.badge) {
    const width = Math.round(measure(row.value, 14.5, MONO) + 26);
    layers.push(
      rect(figuresPanel.x + figuresPanel.width - 18 - width, y - 15, width, 21, {
        fill: studio.successBg,
        stroke: studio.successLine,
        strokeWidth: 1,
        radius: 10.5,
      }),
      text(figuresPanel.x + figuresPanel.width - 18 - width / 2, y, row.value, {
        size: 14.5,
        fill: studio.successInk,
        family: MONO,
        weight: 700,
        anchor: "middle",
      }),
    );
  } else {
    layers.push(
      text(figuresPanel.x + figuresPanel.width - 18, y, row.value, {
        size: 15,
        fill: studio.text,
        family: MONO,
        weight: 600,
        anchor: "end",
      }),
    );
  }
});

/* ── The two floating cards the home page hangs off this window ───────────
 * In the hero, a profit snapshot card overlaps the frame's lower-left corner
 * and a chain chip sits above its top-right. Both are drawn here in the same
 * places, because they are part of what the home page's hero looks like. */

/*
 * Placed where the hero places it — overlapping the frame's lower-left corner
 * and hanging below it — but positioned so the chart above it and the footnote
 * beside it stay readable rather than being buried under the card.
 */
const card = { x: win.x - 110, y: win.y + win.height - 34, width: 320, height: 124 };
layers.push(
  rect(card.x + 5, card.y + 12, card.width, card.height, { fill: palette.brand700, radius: 16, opacity: 0.5 }),
  rect(card.x, card.y, card.width, card.height, {
    fill: rgba(palette.ink900, 0.97),
    stroke: palette.lineStrong,
    strokeWidth: 1.5,
    radius: 16,
  }),
  text(card.x + 16, card.y + 24, "PROFORMA · STEP 9", {
    size: 14.5,
    fill: rgba(palette.text3, 0.78),
    family: MONO,
    weight: 600,
    tracking: 0.16,
  }),
  text(card.x + 16, card.y + 45, "Profit & loss snapshot", { size: 14.5, fill: palette.text1, weight: 600 }),
  line(card.x + 16, card.y + 56, card.x + card.width - 16, card.y + 56, palette.lineSoft, 1),
);

/*
 * The seeded job's money, from `workedExample.ts`, which asserts these totals
 * against the application's seeder at build time. Net, tax and gross — the same
 * three lines Step 9 prints.
 */
const moneyRows: [string, string][] = [
  ["Net", money(quotationTotals.subtotal)],
  ["VAT 5%", money(quotationTotals.vat)],
  ["Total", money(quotationTotals.grandTotal)],
];
moneyRows.forEach(([label, value], index) => {
  const y = card.y + 76 + index * 22;
  const last = index === moneyRows.length - 1;
  layers.push(
    text(card.x + 16, y, label, { size: 14.5, fill: rgba(palette.text3, 0.88) }),
    text(card.x + card.width - 16, y, value, {
      size: 15.5,
      fill: last ? palette.brand200 : palette.text2,
      family: MONO,
      weight: last ? 700 : 500,
      anchor: "end",
    }),
  );
});

/* The chain chip: the first five verbs, as the hero prints them. */
const chainLabel = chainVerbs.slice(0, 5).join("  →  ");
const chainWidth = Math.round(chainLabel.length * 15.5 * 0.62 + 34 + chainLabel.length * 15.5 * 0.06);
const chainChipX = win.x + win.width - 34 - chainWidth;
layers.push(
  rect(chainChipX, win.y - 34, chainWidth, 34, {
    fill: rgba(palette.ink900, 0.97),
    stroke: palette.lineStrong,
    strokeWidth: 1.5,
    radius: 17,
  }),
  text(chainChipX + chainWidth / 2, win.y - 13, chainLabel, {
    size: 15.5,
    fill: palette.brand200,
    family: MONO,
    weight: 500,
    tracking: 0.06,
    anchor: "middle",
  }),
);

/* ── Brand column ───────────────────────────────────────────────────────── */
const col = { x: 80, width: 620 };

/* The mark, redrawn from `src/components/brand/Logo.tsx` (32-unit grid). */
const markSize = 46;
const markScale = markSize / 32;
layers.push(
  `<g transform="translate(${col.x} ${146}) scale(${markScale})">` +
    `<path d="M8.5 3.5h20l-4 4h-16z" fill="url(#markInner)" opacity="0.85" />` +
    `<path d="M4.5 12.5h20l-4 4h-16z" fill="url(#markInner)" opacity="0.6" />` +
    `<path d="M3.5 3.5h17.5l7.5 7.5v17.5H3.5z M21 3.5v7.5h7.5" fill="url(#markFace)" stroke="${rgba(palette.brand200, 0.55)}" stroke-width="0.8" stroke-linejoin="round" />` +
    `<path d="M3.5 3.5 L21 3.5 L12 24 L3.5 24 z" fill="url(#markGlint)" />` +
    `<path d="M21 3.5v7.5h7.5" fill="none" stroke="rgba(255,255,255,0.55)" stroke-width="0.9" stroke-linejoin="round" />` +
    `</g>`,
);
// The wordmark is two colours. Drawn as one text element with a coloured span
// rather than two elements, so the spacing between "Pro" and "Glass" is the
// font's own.
layers.push(
  `<text x="${col.x + 60}" y="174" font-family="${SANS}" font-size="28" font-weight="600" letter-spacing="${n(28 * -0.02)}">` +
    `<tspan fill="${palette.text1}">Pro</tspan><tspan fill="${palette.brand300}">Glass</tspan></text>`,
  text(col.x + 60, 194, "AUTOMATION", {
    size: 14.5,
    fill: rgba(palette.text3, 0.78),
    family: MONO,
    weight: 500,
    tracking: 0.28,
  }),
);

/*
 * The eyebrow, one line of stack facts.
 *
 * `site.platform` is "Windows desktop (WPF)" and `site.framework` is
 * ".NET 8 · WPF", so concatenating both would name WPF twice — the
 * parenthetical comes off the platform and WPF is named once, at the end.
 */
const stackLine = [
  "GLASS FABRICATION ERP",
  site.platform.replace(" (WPF)", "").toUpperCase(),
  site.framework.replace(" · WPF", ""),
  "WPF",
].join(" · ");
layers.push(
  line(col.x, 242, col.x + 28, 242, rgba(palette.brand400, 0.7), 1.5),
  text(col.x + 40, 246, stackLine, {
    size: 14,
    fill: palette.brand200,
    family: MONO,
    weight: 500,
    tracking: 0.09,
  }),
);

/* Headline — the homepage's own H1, at banner scale. */
layers.push(
  text(col.x, 308, "Glass business management,", { size: 42, fill: palette.text1, weight: 600, tracking: -0.02 }),
  text(col.x, 362, "built around your workflow.", { size: 42, fill: "url(#headline)", weight: 600, tracking: -0.02 }),
);

/* Lede. */
layers.push(
  text(col.x, 406, "Price the job, cut the stock, track what left the yard,", {
    size: 18.5,
    fill: rgba(palette.text3, 0.92),
  }),
  text(col.x, 434, "and invoice it — without re-keying the same glass twice.", {
    size: 18.5,
    fill: rgba(palette.text3, 0.92),
  }),
);

/* The proforma's nine steps, counted from the demo's own step list. */
const proformaSteps = demoFor("ProformaInvoice", "Proforma Invoice", []).steps ?? [];

/* Three derived figures. */
const figures = [
  { value: String(modules.length), label: "MODULES" },
  { value: String(railGroupCount), label: "RAIL GROUPS" },
  { value: `${proformaSteps.length}`, label: "PROFORMA STEPS" },
];
figures.forEach((figure, index) => {
  const x = col.x + index * 190;
  if (index > 0) {
    layers.push(line(x - 26, 474, x - 26, 542, rgba(palette.lineStrong, 0.85), 1));
  }
  layers.push(
    text(x, 508, figure.value, { size: 30, fill: palette.text1, weight: 700, tracking: -0.02 }),
    text(x, 532, figure.label, { size: 14.5, fill: rgba(palette.text3, 0.78), family: MONO, weight: 600, tracking: 0.2 }),
  );
});

/* The chain, in the words the site uses for it. */
layers.push(
  line(col.x, 578, col.x + 520, 578, rgba(palette.lineStrong, 0.7), 1),
  text(col.x, 612, chainVerbs.slice(0, 5).join("  →  "), {
    size: 15.5,
    fill: rgba(palette.text3, 0.72),
    family: MONO,
    weight: 500,
    tracking: 0.06,
  }),
);

/* ══════════════════════════════════════════════════════════════════════════
 * The site's own top panel
 * ════════════════════════════════════════════════════════════════════════
 *
 * The banner is the home page, so it carries the home page's navigation: one
 * card holding a card per item — the mark, the items with their glyphs, and the
 * call to action — exactly as `src/components/layout/Navbar.tsx` lays it out.
 * The glyphs come from the shared `src/data/navIcons.ts`, so the banner and the
 * live navigation cannot drift apart.
 */
const bar = { x: 80, y: 24, width: WIDTH - 160, height: 58 };



/** Rough advance width: a pill or a card measured without the font's metrics. */
function measure(value: string, size: number, family: string = SANS): number {
  const perGlyph = family === MONO ? 0.6 : 0.53;
  return value.length * size * perGlyph;
}

const navItems = ["Home", "Product", "Optimization", "Resources", "About", "Developer", "Sponsors"];
const navCards: { label: string; x: number; width: number }[] = [];
let navX = bar.x + 208;
for (const label of navItems) {
  const width = Math.round(20 + 14 + 6 + measure(label, 14) + (label === "Optimization" ? 4 : 0));
  navCards.push({ label, x: navX, width });
  navX += width + 4;
}

const ctaLabel = "Request a Demo";
const ctaWidth = Math.round(measure(ctaLabel, 16, MONO) + 40);
const ctaX = bar.x + bar.width - 18 - ctaWidth;
const toggleX = ctaX - 44;

layers.push(
  // The panel card: the edge, the panel corner, the violet wash.
  rect(bar.x, bar.y, bar.width, bar.height, {
    fill: rgba(palette.ink900, 0.55),
    stroke: rgba(palette.lineStrong, 0.5),
    strokeWidth: 2,
    radius: 16,
  }),
  line(bar.x + 18, bar.y + 2, bar.x + bar.width - 18, bar.y + 2, rgba("#FFFFFF", 0.04), 1),
  // The mark and the wordmark.
  `<g transform="translate(${bar.x + 18} ${bar.y + 15}) scale(${26 / 32})">` +
    `<path d="M8.5 3.5h20l-4 4h-16z" fill="url(#markInner)" opacity="0.85" />` +
    `<path d="M4.5 12.5h20l-4 4h-16z" fill="url(#markInner)" opacity="0.6" />` +
    `<path d="M3.5 3.5h17.5l7.5 7.5v17.5H3.5z M21 3.5v7.5h7.5" fill="url(#markFace)" stroke="${rgba(palette.brand200, 0.55)}" stroke-width="0.8" stroke-linejoin="round" />` +
    `<path d="M3.5 3.5 L21 3.5 L12 24 L3.5 24 z" fill="url(#markGlint)" />` +
    `</g>`,
  `<text x="${bar.x + 52}" y="${bar.y + 36}" font-family="${SANS}" font-size="17.5" font-weight="600">` +
    `<tspan fill="${palette.text1}">Pro</tspan><tspan fill="${palette.brand300}">Glass</tspan></text>`,
);

// A card per item; Home is lit because the home page is the page you are on.
for (const item of navCards) {
  const active = item.label === "Home";
  layers.push(
    rect(item.x, bar.y + 9, item.width, 40, {
      fill: active ? rgba(palette.brand500, 0.14) : "none",
      stroke: active ? rgba(palette.brand400, 0.6) : "none",
      strokeWidth: 2,
      radius: 12,
    }),
    `<g transform="translate(${item.x + 10} ${bar.y + 22}) scale(0.875)" fill="none" stroke="${
      active ? palette.text1 : rgba(palette.text3, 0.9)
    }" stroke-width="${1.6 / 0.875}" stroke-linecap="round" stroke-linejoin="round" opacity="${active ? 1 : 0.8}">` +
      `<path d="${navIcons[item.label] ?? ""}" /></g>`,
    text(item.x + 30, bar.y + 34, item.label, {
      size: 14,
      fill: active ? palette.text1 : rgba(palette.text3, 0.85),
      weight: 600,
    }),
  );
}

// The call to action, in the brand fill the site's primary button uses.
layers.push(
  rect(toggleX, bar.y + 13, 32, 32, {
    fill: rgba(palette.ink800, 0.8),
    stroke: rgba(palette.lineStrong, 0.8),
    strokeWidth: 1.5,
    radius: 10,
  }),
  `<g transform="translate(${toggleX + 8} ${bar.y + 21}) scale(0.9)" fill="none" stroke="${rgba(palette.text3, 0.85)}" stroke-width="1.7" stroke-linecap="round">` +
    `<path d="M8 2.5a5.5 5.5 0 1 0 0 11 8 8 0 0 1 0-11z" /></g>`,
  rect(ctaX, bar.y + 13, ctaWidth, 32, {
    fill: `url(#ctaFill)`,
    stroke: rgba(palette.brand300, 0.55),
    strokeWidth: 1.5,
    radius: 10,
  }),
  text(ctaX + ctaWidth / 2, bar.y + 34, ctaLabel, {
    size: 16,
    fill: "#FFFFFF",
    weight: 600,
    anchor: "middle",
  }),
);

/* ══════════════════════════════════════════════════════════════════════════
 * Definitions
 * ════════════════════════════════════════════════════════════════════════ */

const defs = `<defs>
  <linearGradient id="bed" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0%" stop-color="${palette.ink950}" />
    <stop offset="46%" stop-color="#100A2A" />
    <stop offset="100%" stop-color="${palette.ink800}" />
  </linearGradient>
  <radialGradient id="poolViolet">
    <stop offset="0%" stop-color="${rgba(palette.brand500, 0.3)}" />
    <stop offset="100%" stop-color="${rgba(palette.brand500, 0)}" />
  </radialGradient>
  <radialGradient id="poolCyan">
    <stop offset="0%" stop-color="${rgba(palette.cyan, 0.17)}" />
    <stop offset="100%" stop-color="${rgba(palette.cyan, 0)}" />
  </radialGradient>
  <radialGradient id="poolRose">
    <stop offset="0%" stop-color="${rgba(palette.rose, 0.13)}" />
    <stop offset="100%" stop-color="${rgba(palette.rose, 0)}" />
  </radialGradient>
  <radialGradient id="vignette" cx="0.42" cy="0.4" r="0.75">
    <stop offset="0%" stop-color="${rgba(palette.ink950, 0)}" />
    <stop offset="72%" stop-color="${rgba(palette.ink950, 0)}" />
    <stop offset="100%" stop-color="${rgba(palette.ink950, 0.86)}" />
  </radialGradient>
  <linearGradient id="topHairline" x1="0" y1="0" x2="1" y2="0">
    <stop offset="0%" stop-color="${rgba(palette.brand400, 0)}" />
    <stop offset="34%" stop-color="${rgba(palette.brand400, 0.55)}" />
    <stop offset="66%" stop-color="${rgba(palette.cyan, 0.35)}" />
    <stop offset="100%" stop-color="${rgba(palette.brand400, 0)}" />
  </linearGradient>
  <linearGradient id="headline" x1="0" y1="0" x2="1" y2="0.4">
    <stop offset="0%" stop-color="${palette.brand300}" />
    <stop offset="58%" stop-color="${palette.brand400}" />
    <stop offset="100%" stop-color="${palette.cyan}" />
  </linearGradient>
  <linearGradient id="markFace" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0%" stop-color="${palette.brand300}" />
    <stop offset="52%" stop-color="${palette.brand500}" />
    <stop offset="100%" stop-color="${palette.brand700}" />
  </linearGradient>
  <linearGradient id="markInner" x1="0" y1="1" x2="1" y2="0">
    <stop offset="0%" stop-color="${palette.cyan}" stop-opacity="0.85" />
    <stop offset="100%" stop-color="${palette.brand300}" stop-opacity="0.55" />
  </linearGradient>
  <linearGradient id="ctaFill" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0%" stop-color="${palette.brand500}" />
    <stop offset="100%" stop-color="${palette.brand700}" />
  </linearGradient>
  <linearGradient id="markGlint" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0%" stop-color="#FFFFFF" stop-opacity="0.5" />
    <stop offset="60%" stop-color="#FFFFFF" stop-opacity="0" />
  </linearGradient>
</defs>`;

const svg =
  `<svg xmlns="http://www.w3.org/2000/svg" width="${WIDTH * SUPERSAMPLE}" height="${HEIGHT * SUPERSAMPLE}" ` +
  `viewBox="0 0 ${WIDTH} ${HEIGHT}">${defs}${layers.join("")}</svg>`;

/* ══════════════════════════════════════════════════════════════════════════
 * Render
 * ════════════════════════════════════════════════════════════════════════ */

/**
 * The README copy: 1920 × 640, resampled down from the supersampled raster so
 * the text edges stay clean at the size GitHub displays.
 */
const readmeTarget = path.resolve(import.meta.dirname, "..", "..", "README_ASSETS", "hero.png");

/**
 * The site master: the supersampled raster itself, 3840 × 1280. Kept at full
 * size so `next/image` can hand a retina display a file with real detail in it
 * instead of enlarging a 1920 px one.
 */
const siteTarget = path.resolve(
  import.meta.dirname,
  "..",
  "public",
  "images",
  "brand",
  "proglass-hero-banner-3840.png",
);

await mkdir(path.dirname(readmeTarget), { recursive: true });
await mkdir(path.dirname(siteTarget), { recursive: true });

const source = sharp(Buffer.from(svg));

const { size } = await source
  .clone()
  .resize(WIDTH, HEIGHT, { fit: "fill", kernel: "lanczos3" })
  .png({ compressionLevel: 9 })
  .toFile(readmeTarget);

const { size: siteSize } = await source
  .clone()
  .resize(WIDTH * SUPERSAMPLE, HEIGHT * SUPERSAMPLE, { fit: "fill", kernel: "lanczos3" })
  .png({ compressionLevel: 9 })
  .toFile(siteTarget);

console.log(`wrote  ${path.relative(process.cwd(), readmeTarget)}`);
console.log(
  `       ${WIDTH} × ${HEIGHT} (drawn at ${SUPERSAMPLE}×) · ${(size / 1024).toFixed(0)} KB · ` +
    `plate ${bannerPlate.plate} of ${runPlatesUsed} · ${bannerPlate.pieceCount} cuts · ${run.utilizationPct.toFixed(2)}% utilisation · ` +
    `net AED ${money(quotationTotals.subtotal)} · total AED ${money(quotationTotals.grandTotal)}`,
);
console.log(`wrote  ${path.relative(process.cwd(), siteTarget)}`);
console.log(
  `       ${WIDTH * SUPERSAMPLE} × ${HEIGHT * SUPERSAMPLE} (native supersample) · ` +
    `${(siteSize / 1024 / 1024).toFixed(1)} MB — the site\'s own master`,
);
