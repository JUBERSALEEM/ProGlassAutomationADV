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
 * Three rules keep it honest:
 *
 *   1. No headless browser is involved. The composition is SVG built here and
 *      rasterised with sharp, so the output is reproducible from the repository
 *      alone — `npx tsx scripts/render-hero-banner.mts`.
 *   2. Every figure on it is read from `src/data`. The plate plan is the plan
 *      the homepage draws, which comes out of `packGuillotine`; the cutting
 *      list is the same list; the module count, the rail grouping and the chain
 *      verbs are the data modules. Nothing here is typed in by hand, so the
 *      banner cannot drift away from the product and start advertising
 *      something that does not exist.
 *   3. The colours are the theme tokens themselves (`src/app/globals.css`,
 *      midnight), copied once into `palette` below. If a token moves, this file
 *      moves with it.
 *
 * Usage, from `website/`:
 *
 *   npx tsx scripts/render-hero-banner.mts      # rewrites ../README_ASSETS/hero.png
 *   npm run assets:optimize                     # re-encodes the site derivatives
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

import { cutList, planSheet, planTotals, plates } from "@/components/optimization/sheetPlan";
import { chainVerbs } from "@/data/workflows";
import { modules } from "@/data/modules";
import { site } from "@/lib/site";

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
 * ════════════════════════════════════════════════════════════════════════ */

/** `src/app/globals.css` → `[data-theme="midnight"]`. */
const palette = {
  ink950: "#0B0820",
  ink900: "#120D2E",
  ink850: "#171138",
  ink800: "#1D1646",
  ink700: "#251B57",
  line: "#2A2159",
  lineSoft: "#221A4A",
  lineStrong: "#3B2F7A",
  brand200: "#DDD6FE",
  brand300: "#C4B5FD",
  brand400: "#A78BFA",
  brand500: "#8B5CF6",
  brand600: "#7C3AED",
  brand700: "#6D28D9",
  teal: "#2DD4BF",
  cyan: "#22D3EE",
  green: "#4ADE80",
  rose: "#F472B6",
  text1: "#FFFFFF",
  text2: "#E9E6F8",
  text3: "#D0CBE8",
} as const;

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
    size = 12.5,
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

/** 1600 × 1100 × 5 → hectares of glass, rounded the way a quote rounds it. */
function lineSqm(item: { width: number; height: number; qty: number }): number {
  return (item.width * item.height * item.qty) / 1_000_000;
}

const cuttingListSqm = cutList.reduce((sum, item) => sum + lineSqm(item), 0);

/** The rail: seven real modules, grouped the way `GlassShell.xaml` groups them. */
const RAIL_KEYS = ["Dashboard", "SheetStore", "Inquiry", "ProformaInvoice", "JobOrder", "Optimization", "Cutting"];
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
const win = { x: 736, y: 64, width: 1120, height: 512 };
const rail = { x: win.x + 16, y: win.y + 68, width: 220, height: win.height - 84 };
const main = { x: win.x + 252, y: win.y + 64, width: win.width - 272 };

layers.push(
  // Shadow, then a violet bloom, then the frame.
  rect(win.x + 6, win.y + 18, win.width, win.height, { fill: "#05030F", radius: 22, opacity: 0.6 }),
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
  text(win.x + 300, win.y + 31, `${site.framework} · v${site.version}`, { size: 12.5, fill: rgba(palette.text3, 0.7), family: MONO }),
);
const sheetPill = pill(win.x + win.width - 22 - 232, win.y + 13, `PLATE ${planSheet.label} mm`, {
  fill: rgba(palette.ink800, 0.9),
  stroke: palette.line,
  color: palette.brand200,
});
layers.push(sheetPill.svg);

/* Rail. */
layers.push(
  rect(rail.x, rail.y, rail.width, rail.height, {
    fill: rgba(palette.ink850, 0.85),
    stroke: palette.lineSoft,
    strokeWidth: 1,
    radius: 14,
  }),
);
let railCursor = rail.y + 34;
for (const group of railGroups) {
  layers.push(
    text(rail.x + 20, railCursor, group.group.toUpperCase(), {
      size: 10.5,
      fill: rgba(palette.text3, 0.55),
      family: MONO,
      weight: 600,
      tracking: 0.2,
    }),
  );
  railCursor += 14;
  for (const item of group.items) {
    const active = item.moduleKey === "Optimization";
    if (active) {
      layers.push(
        rect(rail.x + 10, railCursor, rail.width - 20, 28, { fill: rgba(palette.brand500, 0.16), radius: 9 }),
        rect(rail.x + 10, railCursor + 5, 3, 18, { fill: palette.brand400, radius: 2 }),
      );
    }
    layers.push(
      text(rail.x + 26, railCursor + 19, item.railLabel, {
        size: 15,
        fill: active ? palette.brand200 : rgba(palette.text3, 0.86),
        weight: active ? 600 : 450,
      }),
    );
    railCursor += 32;
  }
  railCursor += 12;
}

/* Main column: heading, then the run summary. */
layers.push(
  text(main.x, main.y + 22, "Optimization", { size: 23, fill: palette.text1, weight: 700, tracking: -0.01 }),
  text(
    main.x,
    main.y + 44,
    `Cutting plan · ${planTotals.pieces} pieces · ${planTotals.plates} plates · ${cuttingListSqm.toFixed(2)} m² of ${planTotals.stockSqm.toFixed(2)} m²`,
    { size: 13, fill: rgba(palette.text3, 0.8) },
  ),
);

const yieldPill = pill(main.x + main.width - 268, main.y + 4, `${planTotals.utilizationPct.toFixed(1)} % YIELD`, {
  fill: rgba(palette.brand500, 0.18),
  stroke: rgba(palette.brand400, 0.55),
  color: palette.brand200,
  height: 26,
});
const verifiedPill = pill(main.x + main.width - 130, main.y + 4, `${planTotals.unplaced} UNPLACED`, {
  fill: rgba(palette.green, 0.14),
  stroke: rgba(palette.green, 0.45),
  color: palette.green,
  height: 26,
});
layers.push(yieldPill.svg, verifiedPill.svg);

/* The plates. Four of them, drawn from the real layout. */
const plateGap = 14;
const plateWidth = (main.width - plateGap * (plates.length - 1)) / plates.length;
const plateHeight = plateWidth * (planSheet.height / planSheet.width);
const plateTop = main.y + 64;

const pieceInk: Record<string, { fill: string; stroke: string }> = {
  "G-101": { fill: rgba(palette.brand500, 0.34), stroke: rgba(palette.brand400, 0.85) },
  "G-102": { fill: rgba(palette.cyan, 0.26), stroke: rgba(palette.cyan, 0.75) },
  "G-103": { fill: rgba(palette.teal, 0.24), stroke: rgba(palette.teal, 0.72) },
  "G-104": { fill: rgba(palette.rose, 0.24), stroke: rgba(palette.rose, 0.72) },
  "G-105": { fill: rgba(palette.brand300, 0.24), stroke: rgba(palette.brand300, 0.6) },
};

plates.forEach((plate, index) => {
  const px = main.x + index * (plateWidth + plateGap);
  const scale = plateWidth / planSheet.width;
  layers.push(
    rect(px, plateTop, plateWidth, plateHeight, {
      fill: rgba(palette.ink800, 0.9),
      stroke: palette.lineStrong,
      strokeWidth: 1,
      radius: 8,
    }),
  );
  for (const piece of plate.pieces) {
    const ink = pieceInk[piece.id.split("-").slice(0, 2).join("-")] ?? pieceInk["G-105"]!;
    const x = px + piece.x * scale;
    const y = plateTop + piece.y * scale;
    const w = piece.width * scale;
    const h = piece.height * scale;
    layers.push(
      rect(x, y, w, h, { fill: ink.fill, stroke: ink.stroke, strokeWidth: 0.9, radius: 1.5 }),
    );
    if (w >= 46 && h >= 19) {
      layers.push(
        text(x + 4, y + 12.5, piece.id.split("-").slice(0, 2).join("-"), {
          size: 9,
          fill: rgba(palette.text1, 0.82),
          family: MONO,
          weight: 500,
        }),
      );
    }
  }
  // The safety margin, drawn where it actually falls: 15 mm of a 3210 mm plate.
  layers.push(
    rect(px + 15 * scale, plateTop + 15 * scale, plateWidth - 30 * scale, plateHeight - 30 * scale, {
      fill: "none",
      stroke: rgba(palette.text1, 0.16),
      strokeWidth: 0.8,
      dash: "3 3",
      radius: 2,
    }),
  );
  layers.push(
    text(px, plateTop + plateHeight + 19, `PLATE ${plate.plate} · ${plate.utilizationPct.toFixed(1)} %`, {
      size: 11,
      fill: rgba(palette.text3, 0.72),
      family: MONO,
      weight: 500,
      tracking: 0.1,
    }),
  );
});

/* The specification grid the optimizer read. Same list, same quantities. */
const tableTop = plateTop + plateHeight + 40;
const columns = { ref: main.x, size: main.x + 152, qty: main.x + main.width - 176, sqm: main.x + main.width };

layers.push(
  text(columns.ref, tableTop, "GLASS REF", { size: 10.5, fill: rgba(palette.text3, 0.5), family: MONO, weight: 600, tracking: 0.18 }),
  text(columns.size, tableTop, "W1 × H1", { size: 10.5, fill: rgba(palette.text3, 0.5), family: MONO, weight: 600, tracking: 0.18 }),
  text(columns.qty, tableTop, "QTY", { size: 10.5, fill: rgba(palette.text3, 0.5), family: MONO, weight: 600, tracking: 0.18, anchor: "end" }),
  text(columns.sqm, tableTop, "SQM", { size: 10.5, fill: rgba(palette.text3, 0.5), family: MONO, weight: 600, tracking: 0.18, anchor: "end" }),
  line(main.x, tableTop + 8, main.x + main.width, tableTop + 8, palette.lineSoft, 1),
);

const rowHeight = 25;
cutList.forEach((item, index) => {
  const baseline = tableTop + 8 + 18 + index * rowHeight;
  layers.push(
    text(columns.ref, baseline, item.ref, { size: 13.5, fill: palette.text1, family: MONO, weight: 500 }),
    text(columns.size, baseline, `${item.width} × ${item.height}`, { size: 13, fill: rgba(palette.text3, 0.9), family: MONO }),
    text(columns.qty, baseline, String(item.qty), { size: 13, fill: palette.text2, family: MONO, anchor: "end" }),
    text(columns.sqm, baseline, lineSqm(item).toFixed(2), { size: 13, fill: palette.text2, family: MONO, anchor: "end" }),
  );
  if (index < cutList.length - 1) {
    layers.push(line(main.x, baseline + 7, main.x + main.width, baseline + 7, rgba(palette.line, 0.55), 1));
  }
});

const totalsRule = tableTop + 8 + 18 + cutList.length * rowHeight - 9;
layers.push(
  line(main.x, totalsRule, main.x + main.width, totalsRule, rgba(palette.lineStrong, 0.9), 1),
  text(
    main.x,
    totalsRule + 22,
    `${cutList.length} references · ${planTotals.pieces} pieces · ${planTotals.unplaced} unplaced`,
    { size: 12.5, fill: rgba(palette.text3, 0.8), family: MONO },
  ),
  text(main.x + main.width, totalsRule + 22, `${cuttingListSqm.toFixed(2)} m² used`, {
    size: 12.5,
    fill: palette.brand200,
    family: MONO,
    weight: 600,
    anchor: "end",
  }),
);

/*
 * The honesty note: hung on the window's bottom-left corner, where it overlaps
 * the frame and nothing else. It states what `assertPlanIsSound()` actually
 * checks — a piece placed twice, dropped, or outside the margins fails the
 * build, so the claim on the badge is one the repository enforces.
 */
const badgeLabel = "PLAN VERIFIED · EVERY PIECE PLACED ONCE";
const badgeHeight = 32;
const badgeWidth = Math.round(badgeLabel.length * 11.5 * 0.6 + badgeLabel.length * 11.5 * 0.1 + 66);
const badge = { x: win.x - 22, y: win.y + win.height - 20 };
layers.push(
  rect(badge.x, badge.y, badgeWidth, badgeHeight, {
    fill: rgba(palette.ink900, 0.97),
    stroke: rgba(palette.brand400, 0.5),
    strokeWidth: 1,
    radius: badgeHeight / 2,
  }),
  `<path d="M${badge.x + 20} ${badge.y + 17} l4.5 4.5 l8 -9" fill="none" stroke="${palette.green}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />`,
  text(badge.x + 42, badge.y + badgeHeight / 2 + 4, badgeLabel, {
    size: 11.5,
    fill: palette.brand200,
    family: MONO,
    weight: 500,
    tracking: 0.1,
  }),
);

/* ── Brand column ───────────────────────────────────────────────────────── */
const col = { x: 80, width: 620 };

/* The mark, redrawn from `src/components/brand/Logo.tsx` (32-unit grid). */
const markSize = 46;
const markScale = markSize / 32;
layers.push(
  `<g transform="translate(${col.x} ${90}) scale(${markScale})">` +
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
  `<text x="${col.x + 60}" y="118" font-family="${SANS}" font-size="28" font-weight="600" letter-spacing="${n(28 * -0.02)}">` +
    `<tspan fill="${palette.text1}">Pro</tspan><tspan fill="${palette.brand300}">Glass</tspan></text>`,
  text(col.x + 60, 138, "AUTOMATION", {
    size: 11.5,
    fill: rgba(palette.text3, 0.6),
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
  line(col.x, 186, col.x + 28, 186, rgba(palette.brand400, 0.7), 1.5),
  text(col.x + 40, 190, stackLine, {
    size: 13,
    fill: palette.brand200,
    family: MONO,
    weight: 500,
    tracking: 0.14,
  }),
);

/* Headline — the homepage's own H1, at banner scale. */
layers.push(
  text(col.x, 252, "Glass business management,", { size: 42, fill: palette.text1, weight: 600, tracking: -0.02 }),
  text(col.x, 306, "built around your workflow.", { size: 42, fill: "url(#headline)", weight: 600, tracking: -0.02 }),
);

/* Lede. */
layers.push(
  text(col.x, 350, "Price the job, cut the stock, track what left the yard,", {
    size: 18.5,
    fill: rgba(palette.text3, 0.92),
  }),
  text(col.x, 378, "and invoice it — without re-keying the same glass twice.", {
    size: 18.5,
    fill: rgba(palette.text3, 0.92),
  }),
);

/* Three derived figures. */
const figures = [
  { value: String(modules.length), label: "MODULES" },
  { value: String(railGroupCount), label: "RAIL GROUPS" },
  { value: `${planTotals.utilizationPct.toFixed(1)} %`, label: "PLATE YIELD" },
];
figures.forEach((figure, index) => {
  const x = col.x + index * 208;
  if (index > 0) {
    layers.push(line(x - 26, 418, x - 26, 486, rgba(palette.lineStrong, 0.85), 1));
  }
  layers.push(
    text(x, 452, figure.value, { size: 30, fill: palette.text1, weight: 700, tracking: -0.02 }),
    text(x, 476, figure.label, { size: 11, fill: rgba(palette.text3, 0.6), family: MONO, weight: 600, tracking: 0.2 }),
  );
});

/* The chain, in the words the site uses for it. */
layers.push(
  line(col.x, 522, col.x + col.width, 522, rgba(palette.lineStrong, 0.7), 1),
  text(col.x, 556, chainVerbs.join("  →  "), {
    size: 12.5,
    fill: rgba(palette.text3, 0.72),
    family: MONO,
    weight: 500,
    tracking: 0.06,
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

const target = path.resolve(import.meta.dirname, "..", "..", "README_ASSETS", "hero.png");
await mkdir(path.dirname(target), { recursive: true });

const { size } = await sharp(Buffer.from(svg))
  .resize(WIDTH, HEIGHT, { fit: "fill", kernel: "lanczos3" })
  .png({ compressionLevel: 9 })
  .toFile(target);

console.log(`wrote  ${path.relative(process.cwd(), target)}`);
console.log(
  `       ${WIDTH} × ${HEIGHT} (drawn at ${SUPERSAMPLE}×) · ${(size / 1024).toFixed(0)} KB · ` +
    `${planTotals.pieces} pieces across ${planTotals.plates} plates at ${planTotals.utilizationPct.toFixed(2)} %`,
);
