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

/* crystal-engine §C4 (and below) is read alongside: the surface tokens
   after a bench pass live there, not in globals. */
const engineCss = readFileSync(
  fileURLToPath(new URL("../src/app/crystal-engine.css", import.meta.url)),
  "utf8",
);
const css = readFileSync(
  fileURLToPath(new URL("../src/app/globals.css", import.meta.url)),
  "utf8",
);

/* ── Real rule extraction, atrules honored ─────────────────────────── */
const blocks = [];
{
  /* The family reads TWO files: globals (the shared ink/text/wallpaper
     plane) and the crystal engine (the material plane, incl. §C4 aliases,
     which is where -base plumbing lives). Both are walked, engine second
     so the engine's aliases shadow globals' plain nudges, matching the
     import order of the shipped page. */
  const globalsRoot = postcss.parse(css);
  const engineRoot = postcss.parse(engineCss);
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
  walk(globalsRoot, []);
  walk(engineRoot, []);
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

/* The dial ENV: the -base numbers the §C4 algebra multiplies, read from the
   sheets themselves and resolved through one hypothetical multiplier.
   Defaults declared anywhere (var(x, N)) are forbidden in the DSL above,
   so a missing -base is a caught defect, never a silent 1. */
const BASE_PROPS = [
  "--surface-alpha-base", "--surface-alpha-base-2", "--surface-alpha-base-3",
  "--surface-blur-base", "--surface-specular-base",
];
const envOf = (lane, mode) => {
  const fam = sheet(lane, mode);
  const env = {};
  for (const p of BASE_PROPS) {
    const v = fam[p];
    if (v === undefined) continue;
    const m = /^([\d.]+)(px|%)?$/.exec(String(v).trim());
    assert.ok(m, `${lane} ${mode}: ${p} not a unit number: "${v}"`);
    env[p] = Number(m[1]);
  }
  return env;
};
const resolveSurface = (lane, mode, dial = {}) => {
  const fam = sheet(lane, mode);
  const env = envOf(lane, mode);
  const out = {};
  for (const p of ["--surface-alpha", "--surface-alpha-2", "--surface-alpha-3", "--surface-blur", "--surface-specular"]) {
    const decl = fam[p];
    if (decl === undefined) continue;
    out[p] = evalAliasAt(parseAlias(decl, `${lane} ${mode} ${p}`), env, dial);
  }
  return out;
};

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
/**
 * The alias language. Engine aliases like
 *   clamp(0.45, calc(var(--surface-alpha-base) * var(--glass-body-mult, 1)), 0.98)
 * are tiny expressions over two closures: env -> base number, dial -> multiplier.
 * Reading them as strings is a lie (the @supports fallback once proved prose
 * cannot speak CSS) so each is parsed as an AST and EVALUATED, per world,
 * under the whole dial grid below. Shapeshifted or malformed aliases throw
 * at parse: the contract files never silently degenerate into prose.
 *
 *   Clamp {floor, cap}  -> min(cap, max(floor, base * mult))
 *   Cap   {cap}         -> min(base * mult, cap)
 *   Linear              -> base * mult          (the open dial: blur)
 *   Opacity "body-only" -> identity; the body's own --glass-*-mult rides
 *                          inline on <html> and cannot be simulated centrally
 *   Static              -> the literal itself
 */
const num = (v, at) => {
  const m = /^([\d.]+)$/.exec((v ?? "").trim());
  assert.ok(m, `alias DSL (${at}): expected number, got "${v}"`);
  return Number(m[1]);
};

const parseAlias = (decl, at) => {
  const t = (decl ?? "").replace(/\s+/g, " ").trim();
  let m;
  if ((m = /^min\(calc\(var\(([-\w]+)\) \* var\(([-\w]+)(?:, 1)?\)\), ([\d.]+)\)$/.exec(t)))
    return { type: "cap", cap: num(m[3], at), baseVar: m[1], multVar: m[2] };
  if ((m = /^clamp\(([\d.]+), calc\(var\(([-\w]+)\) \* var\(([-\w]+)(?:, 1)?\)\), ([\d.]+)\)$/.exec(t)))
    return { type: "clamp", floor: num(m[1], at), cap: num(m[4], at), baseVar: m[2], multVar: m[3] };
  if ((m = /^calc\(var\(([-\w]+)\) \* var\(([-\w]+)(?:, 1)?\)\)$/.exec(t)))
    return { type: "linear", baseVar: m[1], multVar: m[2] };
  if ((m = /^var\(([-\w]+)\)$/.exec(t)))
    return { type: "opacity", kind: "body-only", multVar: m[1] };
  if ((m = /^([\d.]+)$/.exec(t)))
    return { type: "static", value: num(m[1], at) };
  throw new Error(`alias DSL (${at}): unparseable shape: ${t}`);
};

const evalAliasAt = (ast, env, dial) => {
  if (ast.type === "opacity") return dial[ast.multVar] ?? 1;
  if (ast.type === "static") return ast.value;
  const mult = dial[ast.multVar] ?? 1;
  const base = env[ast.baseVar];
  const missing = base === undefined || Number.isNaN(base);
  assert.ok(!missing, `alias DSL: ${ast.baseVar} missing from env (dial ${ast.multVar})`);
  const v = base * mult;
  if (ast.type === "cap") return Math.min(v, ast.cap);
  if (ast.type === "clamp") return Math.min(ast.cap, Math.max(ast.floor, v));
  return v; // linear
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
const buildFill = (lane, mode, alphaProp, { scrim = true, dial = {} } = {}) => {
  const fam = sheet(lane, mode);
  const layers = [];
  if (alphaProp) {
    const ink = triplet(modeSheet[mode]["--ink-850"], `${mode} --ink-850`);
    const alpha = resolveSurface(lane, mode, dial)[alphaProp];
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
  /* §C1's hard-coded fills, re-typed here as declarations of contract: the
     engine file owns these numbers; editing the engine below a floor fails
     this pair. (§C7 pass: midnight lift 0.66 → 0.50 with scrim 0.34 → 0.42
     — pattern fill weight; daylight unchanged: its glassiness comes from
     the pastel wall, the 0.80 doctrine holds.) */
  mainline: { midnight: 0.05, daylight: 0.18 },  // §E3: spec floor — measured, still ≥4.5
  fallback: { midnight: 0.94, daylight: 0.97 },
};
const buildLift = (lane, mode) => {
  const fam = sheet(lane, mode);
  const ink = triplet(modeSheet[mode]["--ink-900"], `${mode} --ink-900`);
  const layers = [[...ink, LIFT_ALPHA[lane][mode]]];

/* §E3-G · the map is the engine's own floor: the LIFT_ALPHA constants must
   equal the fills the engine file ships (parse the two §C1 lanes and force
   a sync — a model describing yesterday's file proves nothing). */
{
  const mid = /--glass-lift-a: calc\((0\.\d+) \* var\(--glass-body-mult/.exec(engineCss);
  const day = /--glass-lift-a: calc\((0\.\d+) \* var\(--glass-body-mult/g;
  let m; const vals = [];
  while ((m = day.exec(engineCss)) !== null) vals.push(m[1]);
  assert.ok(mid, "G§E3: midnight §C1 white-tint lane not found in the engine file");
  assert.equal(parseFloat(mid[1]), LIFT_ALPHA.mainline.midnight, "map/file drift: midnight");
  assert.ok(vals.includes(String(LIFT_ALPHA.mainline.daylight)), "map/file drift: daylight");
}
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

/* ── G5 · the catch light over the text ──────────────────────────────
   The beacon's silver flash was the attacking class: a moving white wash
   over paragraphs. §C5's catch peaks at specular × 0.55, capped by the
   same §C4 dial math (glint 2.0 ⇒ specular 0.55 ⇒ α = 0.3025 at the very
   center, falling to 0). Prove the worst point the cursor can paint keeps
   body text above the floor. */
for (const mode of ["midnight", "daylight"]) {
  const spec = resolveSurface("mainline", mode, { "--glass-glint-mult": 2 })["--surface-specular"];
  const catchPeak = [255, 255, 255, spec * 0.55];
  pair(`hover catch at peak (glint 2.0, center) / body (text-2)`, "mainline", mode, "--text-2",
    [...buildFill("mainline", mode, "--surface-alpha"), catchPeak], 4.5);
}

/* ── G4 · the bench under voltage ──────────────────────────────────── */
/* The shipped default is the m = 1 case (proved above). The bench's whole
   danger is every OTHER point, so the contract sweeps the body dial across
   its extremes and the mid-point — and for each grid point recomputes the
   tier fills through the AST, the same arithmetic the page runs. */
const DIAL_GRID = {
  "--glass-body-mult": [0.25, 0.5, 0.75, 1.5, 3],
};
for (const lane of ["mainline", "fallback"]) {
  for (const mode of ["midnight", "daylight"]) {
    for (const body of DIAL_GRID["--glass-body-mult"]) {
      const dial = { "--glass-body-mult": body };
      pair(`bench body=${body.toFixed(2)} tier-1 fill / body (text-2)`, lane, mode, "--text-2",
        buildFill(lane, mode, "--surface-alpha", { dial }), 4.5);
      pair(`bench body=${body.toFixed(2)} tier-3 fill / body (text-2)`, lane, mode, "--text-2",
        buildFill(lane, mode, "--surface-alpha-3", { dial }), 4.5);
    }
  }
}

/* The blur dial never moves contrast (it is AT, not OF, the material), but
   the electrician's slip — a shapeshifted alias that would freeze the
   frost at 26px forever — is caught by evaluating it out loud. */
test("G4·frost: blur(t) = base × frost-mult sweeps its documented window in every world", () => {
  for (const lane of ["mainline", "fallback"]) {
    for (const mode of ["midnight", "daylight"]) {
      for (const frost of [0.4, 1, 1.6]) {
        const blur = resolveSurface(lane, mode, { "--glass-frost-mult": frost })["--surface-blur"];
        const env = envOf(lane, mode);
        assert.ok(
          Math.abs(blur - env["--surface-blur-base"] * frost) < 1e-9,
          `${lane} ${mode}: blur alias is not linear in the frost dial (base ${env["--surface-blur-base"]} × ${frost} ≠ ${blur})`,
        );
      }
    }
  }
});

test("G4·caps: the glint cap and the daylight body floor hold everywhere (the honest caps, evaluated)", () => {
  for (const lane of ["mainline", "fallback"]) {
    for (const mode of ["midnight", "daylight"]) {
      for (const glint of [0.5, 1, 2]) {
        const spec = resolveSurface(lane, mode, { "--glass-glint-mult": glint })["--surface-specular"];
        assert.ok(spec <= 0.55, `${lane} ${mode}: glint ${glint} → specular ${spec} above the 0.55 cap`);
        assert.ok(spec > 0.01, `${lane} ${mode}: specular ${spec} froze to nothing — the alias died`);
      }
      for (const body of [0.5, 0.75, 1.5]) {
        const a = resolveSurface(lane, mode, { "--glass-body-mult": body })["--surface-alpha"];
        if (mode === "daylight") assert.ok(a >= 0.67, `${lane} daylight: body ${body} → alpha ${a} below the 0.68 floor (user-signed §C8 renegotiation)`);
        assert.ok(a >= 0.44, `${lane} ${mode}: body ${body} → alpha ${a} below the 0.45 global floor`);
      }
    }
  }
});

/* Isolation: the engine must never SELECT outside crystal nor WRITE a
   text/ink token. Read from the engine, not remembered. */
test("G4·isolation: every engine selector stays under data-appearance=crystal", () => {
  const root = postcss.parse(engineCss);
  const offenders = [];
  root.walkRules((r) => {
    /* keyframe steps ("from", "to", "50%") are not selectors — skip them */
    if (r.parent?.type === "atrule" && r.parent.name === "keyframes") return;
    for (const sel of r.selectors) {
      if (!sel.includes('appearance="crystal"') && !sel.includes("appearance=crystal")) offenders.push(sel);
    }
  });
  assert.deepEqual(offenders, []);
});

test("G4·text: the engine never writes --text-/​--ink- tokens (glass is material, tiers are mode)", () => {
  const root = postcss.parse(engineCss);
  const hits = [];
  root.walkDecls(/^--(text|ink)-/u, (d) => hits.push(`${d.prop} = ${d.value}`));
  assert.deepEqual(hits, []);
});

test(`crystal contract complete: ${pass} pairs across 2 lanes × 2 modes, parsed not restated`, () => {
  assert.ok(pass === 82, `expected 82 pairs (40 default + 40 bench-grid (5 dial points) + 2 catch-peak), got ${pass}`);
});
