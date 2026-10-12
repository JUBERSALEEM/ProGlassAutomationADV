import assert from "node:assert/strict";
import test from "node:test";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import postcss from "postcss";

/*
 * The crystal token contract, measured instead of asserted.
 *
 * Twin of clay-contrast.test.mjs, for the glass family (glassmorphism
 * roadmap, phase G0): tokens are PARSED from globals.css, fills are
 * composited exactly as the family declares them — page under tier fill
 * under the legibility scrim — and every reading pair must clear its floor.
 *
 * Three things this harness owes its existence to (learned the hard way):
 *
 * 1 · Structure, not strings. The family ships two CSS lanes: the mainline
 *     rules and the `@supports not (backdrop-filter…)` fallback lane that
 *     RAISES fills for blur-less engines. A flat regex scan cannot see the
 *     atrule; folding lanes by last-write-wins composites the daylight
 *     scrim onto the midnight sheet and "finds" a 2.19:1 disaster no
 *     browser paints. postcss walks the real tree; lanes are measured
 *     separately, because BOTH are shipped products (fallback is what an
 *     old engine's reader actually sees).
 *
 * 2 · Gradients are not counted. hi/lo/sheen/frost are corner-confined by
 *     design (the 72%/84% ellipses documented in globals.css); a paragraph
 *     sits where they are transparent. The scrim IS counted in full — the
 *     family declares it the load-bearing legibility layer.
 *
 * 3 · --ink-* and --text-* invert per mode for every family. The pairings
 *     are computed per (lane × mode), never once.
 */

const css = readFileSync(
  fileURLToPath(new URL("../src/app/globals.css", import.meta.url)),
  "utf8",
);

/* ── Real rule extraction, atrules honored ─────────────────────────── */
const blocks = [];
{
  const root = postcss.parse(css);
  const walk = (node, atStack) => {
    node.each((child) => {
      if (child.type === "atrule") {
        walk(child, [...atStack, `${child.name} ${child.params}`]);
        return;
      }
      if (child.type !== "rule") return;
      const decls = {};
      for (const n of child.nodes) if (n.type === "decl") decls[n.prop] = n.value;
      for (const sel of child.selector.split(",")) {
        blocks.push({ sel: sel.replace(/\s+/g, " ").trim(), atStack, decls });
      }
    });
  };
  walk(root, []);
}

/* Lanes. Everything in globals.css rides cascade layers (@layer base/
   components/utilities) — layering is orthogonal to the two-world split.
     mainline  rules NOT inside the backdrop-filter fallback and NOT media-
             gated — what the shipped design paints in a modern engine
   fallback  rules inside `@supports not (backdrop-filter…)` — the blur-less
             safety net, held to the same floors
*/
const LANES = {
  mainline: (b) =>
    !b.atStack.some((a) => a.startsWith("supports not") || a.startsWith("media")),
  fallback: (b) => b.atStack.some((a) => a.startsWith("supports not") && a.includes("backdrop-filter")),
};

const scopeLane = (lane, include, exclude = []) => {
  const out = {};
  for (const b of blocks) {
    if (!LANES[lane](b)) continue;
    if (!include.every((x) => b.sel.includes(x))) continue;
    if (exclude.some((x) => b.sel.includes(x))) continue;
    for (const [k, v] of Object.entries(b.decls)) out[k] = v;
  }
  return out;
};

const CRYSTAL = '[data-appearance="crystal"]';
const DAY = '[data-theme="daylight"]';

/* Family sheets per (lane × mode): mainline family tokens (+ daylight
   overrides for daylight); the fallback sheet is the mainline sheet with
   the @supports raises applied — exactly how the cascade composes. */
const familySheet = (lane, mode) => {
  const base = scopeLane(lane, [CRYSTAL], mode === "midnight" ? [DAY] : []);
  if (mode === "midnight") return base;
  return { ...base, ...scopeLane(lane, [DAY, CRYSTAL]) };
};
const sheet = (lane, mode) =>
  lane === "mainline" ? familySheet(lane, mode) : { ...familySheet("mainline", mode), ...familySheet("fallback", mode) };

/* Mode sheets are family-independent tokens (the ink and text ramps), mainline
   only — the fallback lane never touches them. */
const modeSheet = {
  midnight: scopeLane("mainline", ['data-theme="midnight"']),
  daylight: scopeLane("mainline", [DAY], ['data-appearance=']),
};

/* ── Color math ────────────────────────────────────────────────────── */
const triplet = (v, at) => {
  const m = /^\s*(\d{1,3})\s+(\d{1,3})\s+(\d{1,3})\s*$/.exec(v ?? "");
  assert.ok(m, `${at}: expected an "r g b" triplet, got "${v}"`);
  return [Number(m[1]), Number(m[2]), Number(m[3])];
};
const rgba = (v, at) => {
  /* handles both "rgb(6 26 38 / .34)" and deferred tokens "rgb(var(--x) / .5)" */
  const m = /^rgb\(\s*(\d{1,3})\s+(\d{1,3})\s+(\d{1,3})\s*\/\s*([\d.]+)\s*\)$/.exec(v ?? "");
  assert.ok(m, `${at}: expected rgb(r g b / a), got "${v}"`);
  return [Number(m[1]), Number(m[2]), Number(m[3]), Number(m[4])];
};
const composite = ([r, g, b, a], [br, bg, bb]) => [
  r * a + br * (1 - a),
  g * a + bg * (1 - a),
  b * a + bb * (1 - a),
];
const lum = ([r, g, b]) => {
  const f = (x) => {
    const c = x / 255;
    return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
};
const cr = (fg, bg) => {
  const [hi, lo] = [lum(fg), lum(bg)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
};

/* ── Fill builders: page under fill under scrim (the declared stack) ── */
const buildFill = (lane, mode, alphaProp, { scrim = true } = {}) => {
  const fam = sheet(lane, mode);
  const layers = [];
  if (alphaProp) {
    const ink = triplet(modeSheet[mode]["--ink-850"], `${mode} --ink-850`);
    const alpha = Number(fam[alphaProp]);
    assert.ok(Number.isFinite(alpha) && alpha > 0 && alpha <= 1, `${lane} ${mode} ${alphaProp}: bad alpha "${fam[alphaProp]}"`);
    layers.push([...ink, alpha]);
  }
  if (scrim) {
    const sc = fam["--glass-scrim"];
    assert.ok(sc, `${lane} ${mode}: no --glass-scrim`);
    layers.push(rgba(sc, `${lane} ${mode} --glass-scrim`));
  }
  return layers;
};
const solidFill = (mode, token, at) => [[...triplet(modeSheet[mode][token], `${mode} ${token}`), at]];

/* §C1's hard-coded fills, re-typed here as declarations of contract: the
   engine file owns these numbers; editing the engine below a floor
   fails this pair. (Engine: crystal-engine.css §C1 + §C1f) */
const LIFT_ALPHA = {
  mainline: { midnight: 0.66, daylight: 0.86 },
  fallback: { midnight: 0.94, daylight: 0.97 },
};
const buildLift = (lane, mode) => {
  const fam = sheet(lane, mode);
  const ink = triplet(modeSheet[mode]["--ink-900"], `${mode} --ink-900`);
  const layers = [[...ink, LIFT_ALPHA[lane][mode]]];
  layers.push(rgba(fam["--glass-scrim"], `${lane} ${mode} --glass-scrim`));
  return layers;
};

const ratioFor = (mode, textVar, layers) => {
  const fg = [...triplet(modeSheet[mode][textVar], `${mode} ${textVar}`), 1];
  let bg = triplet(modeSheet[mode]["--ink-950"], `${mode} --ink-950`);
  for (const layer of layers) bg = composite(layer, bg);
  return cr(fg, bg);
};

let pass = 0;
const pair = (label, lane, mode, textVar, layers, min) => {
  const r = ratioFor(mode, textVar, layers);
  test(`${lane} · ${mode} · crystal ${label} — ${r.toFixed(2)}:1 ≥ ${min}:1`, () => {
    assert.ok(r >= min, `${lane} ${mode} crystal ${label}: ${r.toFixed(2)}:1 below ${min}:1`);
  });
  pass++;
};

for (const lane of ["mainline", "fallback"]) {
  for (const mode of ["midnight", "daylight"]) {
    pair("page / headline (text-1)", lane, mode, "--text-1", [], 7);
    pair("page / body (text-2)", lane, mode, "--text-2", [], 7);
    pair("tier-1 fill / body (text-2)", lane, mode, "--text-2", buildFill(lane, mode, "--surface-alpha"), 4.5);
    pair("tier-1 fill / faint label (text-4) ← the scrim's stated promise", lane, mode, "--text-4", buildFill(lane, mode, "--surface-alpha"), 4.5);
    pair("tier-2 fill / body (text-2)", lane, mode, "--text-2", buildFill(lane, mode, "--surface-alpha-2"), 4.5);
    pair("tier-3 fill / body (text-2)", lane, mode, "--text-2", buildFill(lane, mode, "--surface-alpha-3"), 4.5);
    pair("menu-card fill / item (text-2)", lane, mode, "--text-2", [...solidFill(mode, "--ink-900", lane === "fallback" ? 1 : 0.9)], 4.5);
    pair("chip fill / label (text-2)", lane, mode, "--text-2", solidFill(mode, "--ink-800", 1), 4.5);
    /* §C1 · the lift's own fill, exactly as the engine declares it
       (0.66 midnight / 0.86 mainline daylight / 0.94+0.97 fallback). */
    pair("lift fill / well text (text-1)", lane, mode, "--text-1", buildLift(lane, mode), 4.5);
    pair("lift fill / faint label (text-4)", lane, mode, "--text-4", buildLift(lane, mode), 4.5);
  }
}

test(`crystal contract complete: ${pass} pairs across 2 lanes × 2 modes, parsed not restated`, () => {
  assert.ok(pass === 40, `expected 40 pairs, got ${pass}`);
});
