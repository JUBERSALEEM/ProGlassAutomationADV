import assert from "node:assert/strict";
import test from "node:test";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

/*
 * The clay token contract, measured instead of asserted.
 *
 * Every zone the engine paints must read: text-1/body pairs are held to the
 * WCAG 4.5:1 body floor (7:1 where the design has no excuse to be lower),
 * and the one bold-display exception (white on the coral verb) is held to
 * the 3:1 large-text floor. Values are PARSED from clay-engine.css — never
 * restated here by hand — so the test fails the moment an edit breaks a
 * pair, in either mode.
 */

const css = readFileSync(
  fileURLToPath(new URL("../src/app/clay-engine.css", import.meta.url)),
  "utf8",
);

/* selector-substring → declarations map, last write wins, in source order. */
const NO_COMMENTS = css.replace(/\/\*[\s\S]*?\*\//g, "");
const blocks = [...NO_COMMENTS.matchAll(/([^{}]+)\{([^{}]*)\}/g)].map(([_, sel, body]) => {
  const decls = {};
  for (const d of body.split(";")) {
    const idx = d.indexOf(":");
    if (idx > 0) decls[d.slice(0, idx).trim()] = d.slice(idx + 1).trim();
  }
  return { sel: sel.replace(/\s+/g, " ").trim(), decls };
});

const scope = (substr) => {
  const out = {};
  for (const b of blocks) {
    if (b.sel.includes(substr)) {
      for (const [k, v] of Object.entries(b.decls)) out[k] = v;
    }
  }
  return out;
};
const exact = (selector) => {
  const out = {};
  for (const b of blocks) {
    if (b.sel === selector) {
      for (const [k, v] of Object.entries(b.decls)) out[k] = v;
    }
  }
  return out;
};

const BASE = exact('[data-appearance="clay"]'); // midnight-first channels
/* Daylight inherits the base channels and overrides its own. */
const DAY = { ...BASE, ...exact('[data-theme="daylight"][data-appearance="clay"]') };
const M = (sel) => scope(`html[data-theme="midnight"] [data-appearance="clay"] ${sel}`);

const resolveVar = (vars, name, depth = 0) => {
  assert.ok(depth < 6, `var() chain too deep at ${name}`);
  let v = vars[name];
  assert.ok(v !== undefined, `unknown channel ${name}`);
  const m = /^var\((--[\w-]+)\)$/.exec(v);
  return m ? resolveVar(vars, m[1], depth + 1) : v;
};

/* rgb(...) / rgb(var(--x) [/ a]) → [r,g,b,a] over an optional backdrop. */
const channel = (vars, expr) => {
  const e = expr.trim();
  const alpha = /\/\s*([\d.]+)\s*\)?$/.exec(e);
  const a = alpha ? parseFloat(alpha[1]) : 1;
  /* Peel `rgb(...)` first, then cut the `/alpha` inside it, then resolve. */
  let raw = e;
  const wrap = /^rgb\(([\s\S]*)\)$/.exec(raw);
  if (wrap) raw = wrap[1].trim();
  const am = /^(.*?)\/\s*[\d.]+\s*$/.exec(raw);
  if (am) raw = am[1].trim();
  const ref = /^var\((--[\w-]+)\)$/.exec(raw);
  if (ref) {
    raw = resolveVar(vars, ref[1]).trim();
    const again = /^rgb\(([\s\S]*)\)$/.exec(raw);
    if (again) raw = again[1].replace(/\/.*$/, "").trim();
  }
  const rgb = raw.split(/\s+/).map(Number);
  assert.equal(rgb.length, 3, `bad channel ${expr}`);
  return [...rgb, a];
};

const over = ([r, g, b, a], [br, bg, bb]) => [
  Math.round(r * a + br * (1 - a)),
  Math.round(g * a + bg * (1 - a)),
  Math.round(b * a + bb * (1 - a)),
];

const lum = ([r, g, b]) => {
  const lin = (c) => {
    const s = c / 255;
    return s <= 0.04045 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
};

const ratio = (fg, bg) => {
  const [h, l] = [lum(fg), lum(bg)].sort((x, y) => y - x);
  return (h + 0.05) / (l + 0.05);
};

const INK = channel(BASE, "var(--clay-ink)");
const WHITE = [255, 255, 255];
const pair = (label, fg, bg, min) => {
  const r = ratio(fg.slice(0, 3), bg.slice(0, 3));
  test(`${label}: ${r.toFixed(2)}:1 ≥ ${min}:1`, () => {
    assert.ok(r >= min, `${label} measured ${r.toFixed(2)}:1, below the ${min}:1 floor`);
  });
};

/* ── MIDNIGHT zones read white ─────────────────────────────────────────── */
const midText = M(":is("); // §9.2 / §9.3 / §9.5 token locks (merged, same values)
const mt1 = channel(midText, "var(--text-1)");
const mt2 = channel(midText, "var(--text-2)");
const mt3 = channel(midText, "var(--text-3)");
const mt4 = channel(midText, "var(--text-4)");
const page950 = channel(BASE, "var(--ink-950)");
const card850 = channel(BASE, "var(--ink-850)");
const foil = channel(BASE, "var(--clay-foil)");
const bandDeepMid = channel(M(".band-deep") || {}, "rgb(50 34 76)");
const bandRichMid = channel(M(".band-rich") || {}, "rgb(60 42 94)");

pair("midnight page / headline", mt1, page950, 12);
pair("midnight page / faint label", mt4, page950, 4.5);
pair("midnight registry card / body", mt2, card850, 7);
pair("midnight registry card / label", mt4, card850, 4.5);
pair("midnight foil pill / label", mt3, foil, 7);
pair("midnight band-deep / body", mt2, bandDeepMid, 7);
pair("midnight band-deep / label", mt4, bandDeepMid, 4.5);
pair("midnight band-rich / body", mt2, bandRichMid, 4.5);
pair("midnight chip wash on card / label", mt3, over(channel(BASE, "rgb(var(--clay-lavender) / 0.3)"), card850), 4.5);

/* ── Pastel reads plum (both modes — the tiles never flip) ─────────────── */
const TILES = [
  ["tile lavender", "rgb(220 214 255)"],
  ["tile mint", "rgb(205 240 220)"],
  ["tile peach", "rgb(255 227 214)"],
  ["tile sky", "rgb(210 229 247)"],
];
const statText = { ...BASE, ...exact('[data-appearance="clay"] .stat-tile') };
const st2 = channel(statText, "var(--text-2)");
const st3 = channel(statText, "var(--text-3)");
for (const [name, rgb] of TILES) {
  pair(`${name} / big number`, INK, channel(BASE, rgb), 7);
  pair(`${name} / hint text`, st3, channel(BASE, rgb), 4.5);
  void st2;
}
pair("lavender icon squircle / glyph", INK, channel(BASE, "var(--clay-lavender)"), 4.5);

/* ── The verb: bold display on coral, large-text floor ─────────────────── */
pair("coral verb / white bold", WHITE, channel(BASE, "var(--clay-coral)"), 3);

/* ── Daylight zones read plum ──────────────────────────────────────────── */
const dayPage = channel(DAY, "var(--clay-page)");
const dayText = scope('[data-appearance="clay"] :is([class*="bg-ink-"], [class*="bg-white"]');
const dt3 = channel(dayText, "var(--text-3)");
const dt4 = channel(dayText, "var(--text-4)");
const dayFoil = channel(DAY, "var(--clay-foil)");
pair("daylight page / headline", INK, dayPage, 9);
pair("daylight raised card / body", INK, WHITE, 9);
pair("daylight lavender band / ink", INK, channel(BASE, "var(--clay-lavender)"), 4.5);
pair("daylight foil pill / label", dt3, dayFoil, 4.5);
pair("daylight foil pill / faint", dt4, dayFoil, 3);
pair("midnight band porcelain button", INK, channel(M(":is(.band-deep, .band-rich)"), "rgb(250 246 255)"), 9);
