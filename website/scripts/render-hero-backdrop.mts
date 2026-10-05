/**
 * Hero backdrop renderer — public/images/brand/hero-backdrop-3840.png.
 *
 * Why this exists: the home hero's backdrop used to be a photograph of a glass
 * rack in a dark workshop, 1584 × 672, drawn edge to edge and then dimmed under
 * two ink gradients at 34% opacity. Three things were wrong with it at once. It
 * was a 2.3× enlargement on a 1920 px screen (a full-bleed band wants ~3584
 * device pixels), it was a *photograph of an invented workshop* — the one thing
 * on this site that was neither the product nor the site's own design language
 * — and, being photographic and dark, it read as haze: the "background looks
 * blurred" complaint was, precisely, that image.
 *
 * So the backdrop is drawn here instead, the same way the brand banner is: SVG
 * built from the site's own tokens, rasterised with sharp at true 2× so the
 * file carries real detail at the size it is displayed. What it shows is a
 * stack of glass sheets on a rack in perspective — the motif the application
 * itself uses for glass, drawn as geometry rather than borrowed as a photo, with
 * clean edges, a grid on the floor, and light raked across the surface.
 *
 * It is decoration. It carries no figures, no interface and no claim about the
 * product, and the hero hides it from assistive technology. The left third is
 * kept quiet on purpose: that is where the headline and the buttons sit.
 *
 * Usage, from `website/`:
 *
 *   npx tsx scripts/render-hero-backdrop.mts     # writes the 3840 × 1920 master
 *   npm run assets:optimize                       # re-encodes the web derivatives
 */
import path from "node:path";
import { mkdir } from "node:fs/promises";
import sharp from "sharp";

/* ══════════════════════════════════════════════════════════════════════════
 * The canvas
 * ════════════════════════════════════════════════════════════════════════ */

/** Design units. The hero band is roughly 1920 × 960 at its widest. */
const WIDTH = 1920;
const HEIGHT = 960;

/** Output is 2 × design units: 3840 × 1920, a true retina file. */
const SCALE = 2;

/* ══════════════════════════════════════════════════════════════════════════
 * Tokens — copied once from `src/app/globals.css` (midnight), as the banner
 * renderer does. If a token moves, both files move with it.
 * ════════════════════════════════════════════════════════════════════════ */

const palette = {
  ink950: "#0B0820",
  ink900: "#120D2E",
  ink850: "#171138",
  ink800: "#1D1646",
  line: "#2A2159",
  lineStrong: "#3B2F7A",
  brand300: "#C4B5FD",
  brand400: "#A78BFA",
  brand500: "#8B5CF6",
  brand600: "#7C3AED",
  cyan: "#22D3EE",
  teal: "#2DD4BF",
} as const;

/** Numbers in SVG, trimmed: 12.5 stays 12.5, 12.0 becomes 12. */
const n = (value: number) => Number(value.toFixed(2)).toString();

function rgba(hex: string, alpha: number) {
  const r = parseInt(hex.slice(1, 3), 16);
  const g = parseInt(hex.slice(3, 5), 16);
  const b = parseInt(hex.slice(5, 7), 16);
  return `rgba(${r}, ${g}, ${b}, ${Number(alpha.toFixed(3))})`;
}

/** An axis-aligned rectangle. */
function rect(x: number, y: number, width: number, height: number, fill: string, opacity = 1) {
  return `<rect x="${n(x)}" y="${n(y)}" width="${n(width)}" height="${n(height)}" fill="${fill}"${
    opacity === 1 ? "" : ` opacity="${n(opacity)}"`
  } />`;
}

/** A polygon from a flat list of points. */
function poly(points: number[], fill: string, opacity = 1, stroke?: string, strokeWidth = 1) {
  const pairs: string[] = [];
  for (let i = 0; i < points.length; i += 2) pairs.push(`${n(points[i])},${n(points[i + 1])}`);
  return `<polygon points="${pairs.join(" ")}" fill="${fill}"${
    opacity === 1 ? "" : ` opacity="${n(opacity)}"`
  }${stroke ? ` stroke="${stroke}" stroke-width="${n(strokeWidth)}"` : ""} />`;
}

/** A straight line. */
function line(x1: number, y1: number, x2: number, y2: number, stroke: string, width = 1, opacity = 1) {
  return `<line x1="${n(x1)}" y1="${n(y1)}" x2="${n(x2)}" y2="${n(y2)}" stroke="${stroke}" stroke-width="${n(
    width,
  )}"${opacity === 1 ? "" : ` opacity="${n(opacity)}"`} />`;
}

/* ══════════════════════════════════════════════════════════════════════════
 * The scene
 *
 * Paint order is the array order, so the ground, grid and light are pushed
 * first and the rack last.
 * ════════════════════════════════════════════════════════════════════════ */

const layers: string[] = [];

/** 1 · Ground: the site's own page colour, with a lift towards the rack. */
layers.push(rect(0, 0, WIDTH, HEIGHT, `url(#ground)`));

/** 2 · The etched grid on the floor, in perspective.
 *
 *  Two families of line: those running away from the viewer (a fan, spaced
 *  evenly along the floor) and those across it (spaced by depth, so they bunch
 *  towards the horizon). Every line fades with distance instead of stopping at
 *  the horizon — a hard end would read as a seam across the picture.
 */
{
  const horizon = 596;
  for (let i = -3; i <= 20; i++) {
    const x = i * 132;
    layers.push(line(x, HEIGHT + 60, x + (x - WIDTH / 2) * 0.34, horizon + 8, rgba(palette.line, 1), 1, 0.5));
  }
  let y = horizon + 6;
  let step = 9;
  let index = 0;
  while (y < HEIGHT + 60) {
    layers.push(line(0, y, WIDTH, y, rgba(palette.line, 1), 1, 0.34 + index * 0.03));
    y += step;
    step *= 1.26;
    index += 1;
  }
}

/** 3 · A wall behind: faint horizontal courses, to give the space a back. */
for (let i = 0; i < 8; i++) {
  const y = 84 + i * 66;
  layers.push(line(0, y, WIDTH, y, rgba(palette.lineStrong, 0.13), 1));
}

/** 4 · Light: a violet pool over the rack, cyan at its outer edge. */
layers.push(rect(0, 0, WIDTH, HEIGHT, `url(#poolRack)`, 0.9));
layers.push(rect(0, 0, WIDTH, HEIGHT, `url(#poolCyan)`, 0.8));

/** 5 · The rack: a stack of glass sheets seen almost edge-on.
 *
 *  The whole image turns on this group, and on one decision inside it: the face
 *  of each sheet is *translucent* (0.16–0.22), so the bright leading edge of
 *  every sheet behind it shows through. That is what glass does — a rack reads
 *  as a row of lit lines with a dark face over the front of them, not as a slab
 *  with stripes on it. Getting this wrong was the difference between a window
 *  and a rack of glass.
 *
 *  Geometry: sheets leaning right by a fixed angle, each next one offset along
 *  the floor and slightly up, so the tops of the ones behind clear the top edge
 *  of the one in front. No blur anywhere — every edge is a hairline, which is
 *  the point of drawing this instead of photographing it.
 */
{
  const count = 8;
  const floorY = 842; // the foot line
  const footX = 1300; // where the front sheet's foot sits
  const stepX = 30; // each sheet behind is this much further right
  const stepY = 9; // and this much higher up
  const height = 516; // sheet height
  const heightStep = 5; // each sheet behind is slightly taller
  const lean = 20; // top edge leans this far right, in design units
  const face = 248; // the visible width of a sheet's face

  // The frame: two legs leaning with the stack, plus a rail across the top.
  const legTop = floorY - height - 34;
  layers.push(line(footX - 30, floorY + 12, footX - 30 + lean, legTop, rgba(palette.ink900, 0.95), 9));
  layers.push(
    line(footX + face + stepX * count + 36, floorY - stepY * count - 44, footX + face + stepX * count + 36 + lean, legTop - heightStep * count - 8, rgba(palette.ink900, 0.95), 8),
  );
  layers.push(
    line(footX - 36 + lean, legTop + 4, footX + face + stepX * count + 44 + lean, legTop - heightStep * count - 4, rgba(palette.ink850, 1), 11),
  );
  layers.push(
    line(footX - 36 + lean, legTop - 1.5, footX + face + stepX * count + 44 + lean, legTop - heightStep * count - 9.5, rgba(palette.brand400, 0.28), 1.6),
  );

  // Back to front, so each sheet's translucent face covers the ones behind it.
  for (let i = count - 1; i >= 0; i--) {
    const x = footX + i * stepX;
    const y = floorY - i * stepY;
    const top = y - height - (count - i) * heightStep;
    const right = x + face;
    const topRight = top - lean;
    const bottomRight = y - lean;

    // The sheet's face: translucent, tinted, darker towards its far edge.
    layers.push(poly([x, top, right, topRight, right, bottomRight, x, y], `url(#sheet${i % 3})`, 1));

    // The leading edge — the lit line. The front sheet's is the brightest.
    const edge = i === 0 ? 4 : 2.4;
    layers.push(poly([x, top, x + edge, top - edge * 0.2, x + edge, y, x, y], rgba(i === 0 ? palette.brand300 : palette.brand400, i === 0 ? 0.98 : 0.62), 1));
    layers.push(poly([x + edge, top - edge * 0.2, x + edge + 1.6, top - edge * 0.2, x + edge + 1.6, y, x + edge, y], rgba(palette.ink950, 0.55)));

    // The top edge catches the same light, but weaker — it is nearly parallel
    // to the viewer, so a hairline is all it earns.
    layers.push(poly([x, top, right, topRight, right, topRight + 2.2, x, top + 2.2], rgba(palette.brand300, i === 0 ? 0.7 : 0.34)));

    // The far edge of the sheet, closing the silhouette.
    layers.push(poly([right, topRight, right + 2, topRight, right + 2, bottomRight, right, bottomRight], rgba(palette.ink950, 0.7)));

    // Reflections raked across the front sheet only: two hard-edged streaks,
    // no blur, because a blur here would be a soft edge in a crisp drawing.
    if (i < 3) {
      const k = i;
      layers.push(poly([x + 26 + k * 6, top + 300, x + 96 + k * 6, top + 176, x + 112 + k * 6, top + 196, x + 42 + k * 6, top + 322], rgba("#FFFFFF", 0.05)));
      layers.push(poly([x + 44 + k * 6, top + 430, x + 150 + k * 6, top + 250, x + 162 + k * 6, top + 274, x + 56 + k * 6, top + 452], rgba("#FFFFFF", 0.032)));
    }
  }

  // The light the rack stands in, as hard-edged pools on the floor rather than
  // a glow: two quads, one violet, one cyan, both faint.
  layers.push(
    poly([footX - 186, floorY + 96, footX + face + stepX * count + 190, floorY + 12, footX + face + stepX * count + 116, floorY + 158, footX - 118, floorY + 224], rgba(palette.brand500, 0.075)),
  );
  layers.push(
    poly([footX - 96, floorY + 116, footX + face + stepX * count + 108, floorY + 52, footX + face + stepX * count + 74, floorY + 120, footX - 44, floorY + 178], rgba(palette.cyan, 0.05)),
  );
}

/** 6 · The polished sheen: one diagonal band, hard-edged, very faint. */
layers.push(rect(0, 0, WIDTH, HEIGHT, "url(#sheen)", 0.85));

/** 7 · Depth: the left third is quieted so the headline sits on plain ground. */
layers.push(rect(0, 0, WIDTH, HEIGHT, "url(#quietLeft)"));

/** 8 · Vignette on all four edges. */
layers.push(rect(0, 0, WIDTH, HEIGHT, "url(#vignette)"));

/* ══════════════════════════════════════════════════════════════════════════
 * Definition block
 * ════════════════════════════════════════════════════════════════════════ */

const defs = `<defs>
  <linearGradient id="ground" x1="0" y1="0" x2="0" y2="1">
    <stop offset="0%" stop-color="${palette.ink950}" />
    <stop offset="58%" stop-color="${palette.ink900}" />
    <stop offset="100%" stop-color="${palette.ink950}" />
  </linearGradient>

  <radialGradient id="poolRack" cx="0.68" cy="0.42" r="0.6">
    <stop offset="0%" stop-color="${rgba(palette.brand600, 0.3)}" />
    <stop offset="55%" stop-color="${rgba(palette.brand600, 0.08)}" />
    <stop offset="100%" stop-color="${rgba(palette.brand600, 0)}" />
  </radialGradient>

  <radialGradient id="poolCyan" cx="0.93" cy="0.62" r="0.38">
    <stop offset="0%" stop-color="${rgba(palette.cyan, 0.16)}" />
    <stop offset="100%" stop-color="${rgba(palette.cyan, 0)}" />
  </radialGradient>

  <linearGradient id="sheet0" x1="0" y1="0" x2="1" y2="0.22">
    <stop offset="0%" stop-color="${rgba(palette.teal, 0.2)}" />
    <stop offset="100%" stop-color="${rgba(palette.ink900, 0.22)}" />
  </linearGradient>
  <linearGradient id="sheet1" x1="0" y1="0" x2="1" y2="0.22">
    <stop offset="0%" stop-color="${rgba(palette.brand500, 0.17)}" />
    <stop offset="100%" stop-color="${rgba(palette.ink900, 0.2)}" />
  </linearGradient>
  <linearGradient id="sheet2" x1="0" y1="0" x2="1" y2="0.22">
    <stop offset="0%" stop-color="${rgba(palette.cyan, 0.13)}" />
    <stop offset="100%" stop-color="${rgba(palette.ink900, 0.19)}" />
  </linearGradient>

  <linearGradient id="sheen" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0%" stop-color="${rgba("#FFFFFF", 0.05)}" />
    <stop offset="34%" stop-color="${rgba("#FFFFFF", 0.012)}" />
    <stop offset="100%" stop-color="${rgba("#FFFFFF", 0.035)}" />
  </linearGradient>

  <linearGradient id="quietLeft" x1="0" y1="0" x2="1" y2="0">
    <stop offset="0%" stop-color="${rgba(palette.ink950, 0.94)}" />
    <stop offset="34%" stop-color="${rgba(palette.ink950, 0.72)}" />
    <stop offset="58%" stop-color="${rgba(palette.ink950, 0.18)}" />
    <stop offset="100%" stop-color="${rgba(palette.ink950, 0)}" />
  </linearGradient>

  <radialGradient id="vignette" cx="0.5" cy="0.34" r="1.05">
    <stop offset="0%" stop-color="${rgba(palette.ink950, 0)}" />
    <stop offset="62%" stop-color="${rgba(palette.ink950, 0.05)}" />
    <stop offset="100%" stop-color="${rgba(palette.ink950, 0.72)}" />
  </radialGradient>
</defs>`;

const svg =
  `<svg xmlns="http://www.w3.org/2000/svg" width="${WIDTH * SCALE}" height="${HEIGHT * SCALE}" ` +
  `viewBox="0 0 ${WIDTH} ${HEIGHT}">${defs}${layers.join("")}</svg>`;

/* ══════════════════════════════════════════════════════════════════════════
 * Render
 * ════════════════════════════════════════════════════════════════════════ */

const target = path.resolve(
  import.meta.dirname,
  "..",
  "public",
  "images",
  "brand",
  "hero-backdrop-3840.png",
);

await mkdir(path.dirname(target), { recursive: true });

const { size } = await sharp(Buffer.from(svg))
  .resize(WIDTH * SCALE, HEIGHT * SCALE, { fit: "fill", kernel: "lanczos3" })
  .png({ compressionLevel: 9 })
  .toFile(target);

console.log(`wrote  ${path.relative(process.cwd(), target)}`);
console.log(
  `       ${WIDTH * SCALE} × ${HEIGHT * SCALE} (drawn at ${SCALE}×) · ${(size / 1024).toFixed(0)} KB · ` +
    `${layers.length} layers · no raster source`,
);
