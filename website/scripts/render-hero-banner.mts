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
 *   npx tsx scripts/render-hero-banner.mts      # writes both outputs below
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

import { demoFor } from "@/data/liveDemos";
import { navIcons } from "@/data/navIcons";
import { chainVerbs } from "@/data/workflows";
import { modules } from "@/data/modules";
import { quotationTotals } from "@/data/workedExample";
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
 * The screen the home page puts in its hero.
 *
 * `demoFor` is the same accessor the live preview on `/` uses, so the banner
 * draws the Overview dashboard with the application's own tiles — open
 * inquiries, proformas raised, jobs in production, ready to dispatch — and the
 * proforma's nine steps, counted from the demo's own step list.
 */
const dashboard = demoFor("Dashboard", "Overview dashboard", ["KPI cards", "Order-to-cash pipeline", "Activity feed"]);
const kpis = (dashboard.views[0]?.summary ?? []).slice(0, 4);
const proformaSteps = demoFor("ProformaInvoice", "Proforma Invoice", []).steps ?? [];

/**
 * Seven bars, the same series the live dashboard draws at its default range
 * (see `DashboardEngine` in src/components/product/LiveSurface.tsx). Kept as a
 * literal because it is a shape, not a figure: the tiles above carry the
 * numbers.
 */
const dashboardBars = [0.42, 0.66, 0.38, 0.82, 0.55, 0.94, 0.7];

/** Money, the way the application and the site both print it. */
function money(value: number): string {
  return value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/** The rail: seven real modules, grouped the way `GlassShell.xaml` groups them. */
/*
 * Three groups, not nine.
 *
 * The drawn rail is as tall as the window it is inside, and the profit card
 * overlaps its lower corner the way it does on `/`. Main, Inventory and Sales
 * fill the rail above that card; Production and below would be painted under
 * it and read as a truncated menu, which is worse than showing fewer groups.
 */
const RAIL_KEYS = ["Dashboard", "SheetStore", "Inquiry", "ProformaInvoice", "JobOrder"];
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
  text(win.x + 84, win.y + 31, `${site.name} · Dashboard`, { size: 15, fill: palette.text1, weight: 600 }),
  text(win.x + 372, win.y + 31, `v${site.version}`, { size: 15.5, fill: rgba(palette.text3, 0.85), family: MONO }),
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
  color: rgba(palette.text3, 0.9),
  height: 22,
  size: 13.5,
  padX: 24,
});
layers.push(
  `<circle cx="${win.x + win.width - 145}" cy="${win.y + 28}" r="7" fill="${rgba(palette.brand400, 0.22)}" />`,
  text(win.x + win.width - 145, win.y + 32, "AB", { size: 11.5, fill: palette.brand200, family: MONO, weight: 700, anchor: "middle" }),
  adminChip.svg,
);

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
      size: 13,
      fill: rgba(palette.text3, 0.55),
      family: MONO,
      weight: 600,
      tracking: 0.2,
    }),
  );
  railCursor += 14;
  for (const item of group.items) {
    const active = item.moduleKey === "Dashboard";
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

/*
 * The rail ends with System, anchored to the bottom of the navigation.
 *
 * Five entries and a long empty column would read as a menu that failed to
 * load. `System Center` and `Diagnostics` are the two System entries the
 * application's rail actually carries, and they sit where they sit in the
 * product: at the foot of the rail.
 */
layers.push(
  text(rail.x + 20, rail.y + rail.height - 84, "SYSTEM", {
    size: 13,
    fill: rgba(palette.text3, 0.55),
    family: MONO,
    weight: 600,
    tracking: 0.2,
  }),
  text(rail.x + 26, rail.y + rail.height - 60, "System Center", { size: 15, fill: rgba(palette.text3, 0.86), weight: 450 }),
  text(rail.x + 26, rail.y + rail.height - 32, "Diagnostics", { size: 15, fill: rgba(palette.text3, 0.86), weight: 450 }),
);

/* ── The workspace: the Overview dashboard ───────────────────────────────
 * The home page's hero shows this screen, so this is the screen the banner
 * shows: the module's title, its range chips, the four position tiles the
 * application reports, and the volume chart under them. The tile labels and
 * values are read from the demo's own summary — nothing here is typed in. */
layers.push(
  text(main.x, main.y + 24, "Overview dashboard", { size: 23, fill: palette.text1, weight: 700, tracking: -0.01 }),
  text(main.x, main.y + 42, dashboard.blurb, { size: 15.5, fill: rgba(palette.text3, 0.85) }),
);

/* The sample-data tag, and the range chips the preview offers. */
const tag = pill(main.x + 322, main.y + 4, "SAMPLE DATA · LIVE", {
  fill: rgba(palette.ink800, 0.75),
  stroke: palette.lineStrong,
  color: rgba(palette.text3, 0.85),
  height: 24,
  size: 13,
  padX: 24,
});
layers.push(
  `<circle cx="${main.x + 334}" cy="${main.y + 16}" r="3" fill="${palette.brand400}" />`,
  tag.svg,
);

const rangeChips = ["Today", "This week", "This month"].map((label) => ({
  label,
  width: Math.round(label.length * 12 * 0.55 + 24),
}));
let chipX = main.x + main.width;
for (const chip of [...rangeChips].reverse()) {
  chipX -= chip.width;
  const active = chip.label === "This week";
  layers.push(
    rect(chipX, main.y + 5, chip.width, 24, {
      fill: active ? rgba(palette.brand400, 0.18) : rgba(palette.ink800, 0.6),
      stroke: active ? rgba(palette.brand400, 0.45) : palette.line,
      strokeWidth: 1,
      radius: 12,
    }),
    text(chipX + chip.width / 2, main.y + 21, chip.label, {
      size: 14.5,
      fill: active ? palette.brand200 : rgba(palette.text3, 0.88),
      weight: active ? 600 : 500,
      anchor: "middle",
    }),
  );
  chipX -= 8;
}

/* The four position tiles. */
const tileGap = 12;
const tileWidth = (main.width - tileGap * 3) / 4;
const tileTop = main.y + 56;
const tileHeight = 96;
kpis.forEach((item, index) => {
  const x = main.x + index * (tileWidth + tileGap);
  layers.push(
    rect(x, tileTop, tileWidth, tileHeight, {
      fill: rgba(palette.ink800, 0.72),
      stroke: palette.line,
      strokeWidth: 1,
      radius: 12,
    }),
    text(x + 14, tileTop + 30, item.label.toUpperCase(), {
      size: 12.5,
      fill: rgba(palette.text3, 0.55),
      family: MONO,
      weight: 600,
      tracking: 0.14,
    }),
    text(x + 14, tileTop + 70, item.value, {
      size: 30,
      fill: palette.text1,
      weight: 700,
      family: MONO,
      tracking: -0.02,
    }),
    text(x + 14, tileTop + 86, index === 0 ? "across the period" : "across the range", {
      size: 12.5,
      fill: rgba(palette.text3, 0.78),
      family: MONO,
    }),
  );
});

/* The volume chart, drawn from the same seven-bar series as the live tile. */
const chartTop = tileTop + tileHeight + 14;
const chartHeight = 168;
layers.push(
  rect(main.x, chartTop, main.width, chartHeight, {
    fill: rgba(palette.ink950, 0.5),
    stroke: palette.line,
    strokeWidth: 1,
    radius: 12,
  }),
);
const barPad = 18;
const barGap = 12;
const barWidth = (main.width - barPad * 2 - barGap * (dashboardBars.length - 1)) / dashboardBars.length;
dashboardBars.forEach((value, index) => {
  const height = (chartHeight - barPad * 2) * value;
  layers.push(
    rect(main.x + barPad + index * (barWidth + barGap), chartTop + chartHeight - barPad - height, barWidth, height, {
      fill: "url(#barGrad)",
      radius: 3,
    }),
  );
});

/* The chart's own axis labels, so the shape means something. */
const chartFloor = chartTop + chartHeight - barPad + 14;
["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].forEach((day, index) => {
  layers.push(
    text(main.x + barPad + index * (barWidth + barGap) + barWidth / 2, chartFloor, day, {
      size: 12,
      fill: rgba(palette.text3, 0.45),
      family: MONO,
      anchor: "middle",
    }),
  );
});

if (dashboard.footnote) {
  layers.push(
    text(main.x, chartFloor + 22, dashboard.footnote, { size: 14.5, fill: rgba(palette.text3, 0.78) }),
  );
}

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
  rect(card.x + 5, card.y + 12, card.width, card.height, { fill: "#05030F", radius: 16, opacity: 0.55 }),
  rect(card.x, card.y, card.width, card.height, {
    fill: rgba(palette.ink900, 0.97),
    stroke: palette.lineStrong,
    strokeWidth: 1.5,
    radius: 16,
  }),
  text(card.x + 16, card.y + 24, "PROFORMA · STEP 9", {
    size: 13,
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
    text(x, 532, figure.label, { size: 13.5, fill: rgba(palette.text3, 0.78), family: MONO, weight: 600, tracking: 0.2 }),
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
  <linearGradient id="barGrad" x1="0" y1="1" x2="0" y2="0">
    <stop offset="0%" stop-color="${rgba(palette.brand600, 0.7)}" />
    <stop offset="100%" stop-color="${rgba(palette.cyan, 0.7)}" />
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
    `${kpis.length} dashboard tiles · net AED ${money(quotationTotals.subtotal)} · total AED ${money(quotationTotals.grandTotal)}`,
);
console.log(`wrote  ${path.relative(process.cwd(), siteTarget)}`);
console.log(
  `       ${WIDTH * SUPERSAMPLE} × ${HEIGHT * SUPERSAMPLE} (native supersample) · ` +
    `${(siteSize / 1024 / 1024).toFixed(1)} MB — the site\'s own master`,
);
