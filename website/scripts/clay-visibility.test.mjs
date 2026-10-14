import assert from "node:assert/strict";
import test from "node:test";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

/*
 * Guard for the "claymorphism is not visible on white cards" defect.
 *
 * The recipe (docs/CLAYMORPHISM.md §4) is four lights: a hue-keyed SE
 * drop, a white NW lift, a white TL sheen, a hue-keyed BR depth. On the
 * light faces — daylight's near-white cards and midnight's pastel
 * tiles — three of the four had been turned down or re-tinted until
 * they measured at the page's own colour:
 *
 *   · the daylight lift and sheen read `--overlay-face` (#EAE2FF)
 *     against a #EEE8F8 page — ≈1.02:1 and ≈1.03:1, i.e. painted in
 *     the colour of the wall behind the card;
 *   · the midnight pastel tiles' hue drop rested at 0.12 (≈1.17:1)
 *     and their inset depth at 0.18 (≈1.06:1) after the §26.1 lamp
 *     removal overshot;
 *   · the midnight drop hue is the page's own colour (≈1.02:1), so
 *     the white lift — at 0.07 (≈1.19:1) — moulded nothing either.
 *
 * These tests hold the measured floors: the light channels are white,
 * and every leg of the recipe blends to at least 1.25:1 against the
 * ground it is painted on. Contrast here is the WCAG ratio of the
 * shadow colour alpha-blended over its ground, which is what a
 * blurred shadow's core actually paints.
 */

const root = fileURLToPath(new URL("..", import.meta.url));
const clay = readFileSync(join(root, "src/app/clay-engine.css"), "utf8");

/* The two themed token blocks, sliced so daylight values are never
   read out of the midnight block (they share declaration names). */
const daylight = clay.match(/\[data-theme="daylight"\]\[data-appearance="clay"\] \{([\s\S]*?)\n\}/)?.[1] ?? "";
assert.ok(daylight.length > 0, "daylight clay token block not found");

function lum([r, g, b]) {
  const f = (v) => {
    const s = v / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
}
function contrast(a, b) {
  const [hi, lo] = [lum(a), lum(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}
const blend = (fg, alpha, bg) => fg.map((v, i) => Math.round(v * alpha + bg[i] * (1 - alpha)));

const PAGE_DAY = [238, 232, 248]; // clay daylight ground (--ink-950)
const PAGE_NIGHT = [22, 15, 33]; // clay midnight ground
const DROP_H = [140, 108, 255]; // --clay-drop-h
const CARD_FACE_DAY = [206, 196, 255]; // --card-face (daylight default)

test("daylight light channels are white, never the page-tinted overlay", () => {
  for (const token of ["--clay-hi", "--clay-hi-2", "--clay-lip"]) {
    assert.match(daylight, new RegExp(`${token}: rgb\\(255 255 255 /`), `daylight ${token} is tinted again — the sheen dies on light cards`);
  }
  assert.doesNotMatch(daylight, /--shadow-(panel|raised|float):[^;]*var\(--overlay-face\)/, "a daylight shadow token lifts in --overlay-face (the page colour) again");
});

test("every daylight leg of the recipe measures on a near-white card", () => {
  const drop = Number(daylight.match(/--shadow-panel: [\s\S]*?\/ calc\(([\d.]+) \* var\(--clay-depth/)?.[1]);
  assert.ok(drop >= 0.4, `daylight panel drop alpha ${drop} < 0.40`);
  assert.ok(contrast(blend(DROP_H, drop, PAGE_DAY), PAGE_DAY) >= 1.35, "daylight SE drop does not measure against the page");
  const sheen = Number(daylight.match(/--clay-hi: rgb\(255 255 255 \/ ([\d.]+)\)/)?.[1]);
  assert.ok(contrast(blend([255, 255, 255], sheen, CARD_FACE_DAY), CARD_FACE_DAY) >= 1.25, "daylight TL sheen does not measure on the card face");
  const depth = Number(daylight.match(/--clay-lo: rgb\(var\(--clay-drop-h\) \/ ([\d.]+)\)/)?.[1]);
  assert.ok(contrast(blend(DROP_H, depth, CARD_FACE_DAY), CARD_FACE_DAY) >= 1.25, "daylight BR depth does not measure on the card face");
});

test("midnight's mould is carried by a measured white lift", () => {
  const lift = Number(clay.match(/--shadow-panel: 12px 12px 32px[\s\S]*?-10px -10px 24px rgb\(255 255 255 \/ ([\d.]+)\)/)?.[1]);
  assert.ok(lift >= 0.12, `midnight panel lift alpha ${lift} < 0.12`);
  assert.ok(contrast(blend([255, 255, 255], lift, PAGE_NIGHT), PAGE_NIGHT) >= 1.3, "midnight NW lift does not measure against the dark page");
});

test("midnight pastel tiles are moulded by their own hue, at measured strength", () => {
  const drops = [...clay.matchAll(/html\[data-theme="midnight"\]\[data-appearance="clay"\] \.stat-tile:nth-child\(4n\+\d\) \{\s*box-shadow: 8px 8px 14px rgb\(var\(--rot-\dh, var\(--clay-drop-h\)\) \/ ([\d.]+)\)/g)].map((m) => Number(m[1]));
  assert.equal(drops.length, 4, "expected the four midnight tile rules");
  for (const d of drops) assert.ok(d >= 0.24, `midnight tile drop ${d} < 0.24 — flat pastel again`);
  const depths = [...clay.matchAll(/html\[data-theme="midnight"\]\[data-appearance="clay"\] \.stat-tile:nth-child\(4n\+\d\) \{[\s\S]*?inset calc\(-6px \* var\(--clay-corner-spread\)\) calc\(-6px \* var\(--clay-corner-spread\)\) calc\(12px \* var\(--clay-corner-spread\)\) rgb\(var\(--rot-\dh, var\(--clay-drop-h\)\) \/ min\([\d.]+, calc\(([\d.]+) \* var\(--clay-corner-depth/g)].map((m) => Number(m[1]));
  assert.equal(depths.length, 4, "expected the four midnight tile depth rules");
  for (const d of depths) assert.ok(d >= 0.28, `midnight tile depth ${d} < 0.28 — flat pastel again`);
});

test("the lightest tiles carry the interior wash pattern", () => {
  assert.match(clay, /\[data-appearance="clay"\] \.stat-tile,\s*\n\[data-appearance="clay"\] \.menu-card,/, ".stat-tile fell out of the §16 interior wash list");
});

/* The corrected material invariant, taken from the owner's reference
 * boards and docs/CLAYMORPHISM.md §4: Clay cards have NO drawn
 * perimeter. Their boundary is the face meeting a coloured cast
 * shadow, a blurred white top-left sheen and a blurred hue shade in
 * the bottom-right. The studio adjustment is Corner depth: it scales
 * that soft light/shade, never a border, ring, band or 1px inset. */
test("Clay surfaces draw no perimeter line", () => {
  const registry = clay.match(/\[data-appearance="clay"\] :is\(\.surface, \.surface-raised, \.stat-tile, \.glass-primary,([\s\S]*?)\) \{\s*border-color: ([^;]+);/)?.[2];
  assert.equal(registry?.trim(), "transparent", "the §7.3 registry paints a card border again");
  assert.match(clay, /:is\(\[class\*="border-line"\], \[class\*="border-ink"\]\):is\(\[class\*="rounded"\]\):not\(\[data-color-plate\]\) \{\s*border-color: transparent/, "hand-written rounded cards regained a drawn perimeter (§9.6)");
  assert.doesNotMatch(clay, /mask-composite: exclude/, "a masked gradient ring/band returned to Clay surfaces");
  assert.doesNotMatch(clay, /THE CURVED PLATE EDGE|THE INNER PLATE/, "the rejected inner-line/plate treatment returned");
  assert.doesNotMatch(clay, /0 0 0 5px var\(--clay-primary-soft\)/, "the featured pricing card regained its drawn ring");
  assert.match(clay, /§22 · NO DRAWN PERIMETER IN THE BESPOKE PACK/, "the bespoke pack perimeter suppression is gone");
  assert.match(clay, /\.pricing-clay__card, \.testi-clay,[\s\S]*?border-color: transparent/, "bespoke pack cards regained a drawn perimeter");
});

test("Corner depth scales soft corner light and shade — never a line", () => {
  assert.match(clay, /--clay-corner-depth: 1;/, "the --clay-corner-depth dial token is gone");
  assert.match(clay, /--clay-corner-spread: calc\(1 \+ max\(0, var\(--clay-corner-depth/, "the corner shadow geometry does not grow with the dial");
  const panel = clay.match(/--shadow-panel: ([\s\S]*?);/)?.[1] ?? "";
  assert.match(panel, /inset calc\(4px \* var\(--clay-corner-spread\)\)/, "panel corner sheen is not scaled by Corner depth");
  assert.match(panel, /inset calc\(-6px \* var\(--clay-corner-spread\)\)/, "panel corner shade is not scaled by Corner depth");
  assert.match(panel, /min\((?:0\.72|0\.88), calc\(var\(--clay-corner-sheen-alpha\) \* var\(--clay-corner-depth/, "corner sheen is not capped/scaled by the dial");
  assert.doesNotMatch(panel, /inset 0 0 0 1px/, "the panel token carries a 1px inset ring again");
  assert.match(clay, /rgb\(255 255 255 \/ min\(0\.55, calc\(var\(--clay-corner-light/, "the broad corner light is not scaled by Corner depth");
});

test("the altitude tokens carry blurred mould, not inset rims", () => {
  for (const token of ["--shadow-panel", "--shadow-raised"]) {
    const decl = clay.match(new RegExp(`${token}: ([\\s\\S]*?);`))?.[1] ?? "";
    assert.match(decl, /inset calc\(\dpx \* var\(--clay-corner-spread\)\)/, `${token} lost its blurred corner sheen`);
    assert.match(decl, /inset calc\(-\dpx \* var\(--clay-corner-spread\)\)/, `${token} lost its blurred corner shade`);
    assert.doesNotMatch(decl, /inset 0 0 0 1px|inset 0 1px 0/, `${token} carries a hard inset line again`);
  }
  for (const token of ["--shadow-well", "--shadow-float"]) {
    const decl = clay.match(new RegExp(`${token}: ([\\s\\S]*?);`))?.[1] ?? "";
    assert.doesNotMatch(decl, /inset 0 0 0 1px|inset 0 1px 0/, `${token} carries a hard inset line again`);
  }
});
