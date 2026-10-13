import assert from "node:assert/strict";
import test from "node:test";
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import postcss from "postcss";

/*
 * THE PAINT HARNESS — what midnight and daylight actually paint, computed
 * from the BUILT stylesheet, never re-read by eye.
 *
 * The 11:28 screenshots showed zones and cards at war (dark cards on
 * lilac fields, washed paragraphs on lavender) while every rule read
 * correctly in source. The truth lives in the cascade: layers, order,
 * specificity, and a hundred custom-property re-definitions. This harness
 * reproduces it: parse the built CSS, model the real DOM chains from the
 * components (band → card → title → paragraph → pill), and resolve the
 * winning declaration for background-color and color on every probe —
 * then hold the paint to the floors: dark grounds stay dark in midnight
 * (the 70/20/10 rule: plum page, one-step cards, pastel only on accents),
 * and every reading pair meets its WCAG floor in both modes.
 */

/* ── Built CSS ─────────────────────────────────────────────────────────── */
const CSS_DIR = new URL("../.next/static/", import.meta.url).pathname;
const cssFiles = [];
const findCss = (dir) => {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) findCss(path);
    else if (entry.name.endsWith(".css")) cssFiles.push(path);
  }
};
findCss(CSS_DIR);
assert.ok(cssFiles.length > 0, "no built CSS — run `npm run build` first");
/* Cascade order = the <link> order the browser actually receives. Recover
   it from a built page's HTML; alphabetical order would scramble it. */
{
  const htmlPath = new URL("../.next/server/app/en-US.html", import.meta.url).pathname;
  const pageHtml = readFileSync(htmlPath, "utf8");
  const linkOrder = [...pageHtml.matchAll(/\/_next\/static\/chunks\/([\w-]+\.css)/gu)].map((m) => m[1]);
  const rank = (file) => {
    const idx = linkOrder.indexOf(file.split("/").pop());
    return idx === -1 ? linkOrder.length : idx;
  };
  cssFiles.sort((a, b) => rank(a) - rank(b));
}
const cssText = cssFiles.map((f) => readFileSync(f, "utf8")).join("\n");

/* ── Rule extraction (layers + media awareness) ────────────────────────── */
const LAYER_RANK = new Map();
let layerSeq = 1;
const rules = [];
{
  const root = postcss.parse(cssText);
  let order = 0;
  const VIEWPORT = 1280; /* desktop preview profile */
  const skipMedia = (params) => {
    if (/print|speech/u.test(params)) return true;
    if (/prefers-color-scheme/u.test(params)) return true; /* attrs drive mode */
    if (/prefers-reduced-motion|\(hover/u.test(params) && !/hover:\s*none/u.test(params)) return true;
    const min = /\(min-width:\s*([\d.]+)px\)/u.exec(params);
    if (min && VIEWPORT < parseFloat(min[1])) return true;
    const max = /\(max-width:\s*([\d.]+)px\)/u.exec(params);
    if (max && VIEWPORT > parseFloat(max[1])) return true;
    return false;
  };
  const walk = (node, layerStack, inSkippedMedia) => {
    node.each((child) => {
      if (child.type === "atrule" && child.name === "layer") {
        /* Statement form declares order; block form opens a layer. */
        const names = child.params.split(",").map((s) => s.trim()).filter(Boolean);
        for (const n of names) if (!LAYER_RANK.has(n)) LAYER_RANK.set(n, layerSeq++);
        if (child.nodes && child.nodes.length) {
          walk(child, [...layerStack, names[0]], inSkippedMedia);
        }
        return;
      }
      if (child.type === "atrule") {
        walk(child, layerStack, inSkippedMedia || (child.name === "media" && skipMedia(child.params)));
        return;
      }
      if (child.type === "rule") {
        if (inSkippedMedia) return;
        rules.push({
          selector: child.selector,
          layerRank: layerStack.length ? LAYER_RANK.get(layerStack[layerStack.length - 1]) ?? 0 : null,
          order: order++,
          decls: child.nodes
            .filter((n) => n.type === "decl")
            .map((n) => ({ prop: n.prop, value: n.value, important: !!n.important })),
        });
      }
    });
  };
  walk(root, [], false);
}
if (process.env.DEBUG_RULES) {
  for (const r of rules) {
    if (r.selector.includes(process.env.DEBUG_RULES)) {
      console.error(`RULE: ${r.selector}  layer=${r.layerRank} #${r.order}  decls=${r.decls.map((d) => `${d.prop}:${d.value}`).join("; ").slice(0, 200)}`);
    }
  }
}

/* ── Selector engine (the subset this stylesheet speaks) ───────────────── */
const splitTop = (str, sep = ",") => {
  const out = [];
  let depth = 0;
  let cur = "";
  for (const ch of str) {
    if (ch === "(") depth += 1;
    if (ch === ")") depth -= 1;
    if (ch === sep && depth === 0) {
      out.push(cur.trim());
      cur = "";
    } else cur += ch;
  }
  if (cur.trim()) out.push(cur.trim());
  return out;
};

/* El = { tag, classes:Set, attrs:Map, childIndex (1-based, null = n/a) } */
const compoundMatches = (compound, el) => {
  // tokenize one compound selector
  const re = /(\*|[a-zA-Z][\w-]*|\.(?:[\w-]|\\.)+|\[[^\]]+\]|::?[\w-]+(?:\([^)]*\))?)/g;
  let m;
  let any = false;
  while ((m = re.exec(compound))) {
    const tok = m[1];
    any = true;
    if (tok === "*") continue;
    if (tok.startsWith(".")) {
      /* Built CSS escapes carrier classes: .text-brand-200\/90. The harness
         hand-feeds the real DOM class ("text-brand-200/90"), so peel the
         backslash before comparing — otherwise class selectors silently
         miss and elements render with inherited colour, painting a probe
         "green" for a variant its product classes never apply to. */
      if (!el.classes.has(tok.slice(1).replace(/\\(.)/g, "$1"))) return false;
      continue;
    }
    if (tok.startsWith("[")) {
      const inner = tok.slice(1, -1);
      const am = /^([\w-]+)(?:([~*|^$]?=)"?([^"\]]*)"?)?$/.exec(inner);
      if (!am) return false;
      const [, name, op, val] = am;
      const actual = name === "class" ? [...el.classes].join(" ") : el.attrs.get(name);
      if (actual === undefined) return false;
      if (!op) continue;
      if (op === "=" && actual !== val) return false;
      if (op === "*=" && !actual.includes(val)) return false;
      if (op === "~=" && !actual.split(/\s+/).includes(val)) return false;
      if (op === "^=" && !actual.startsWith(val)) return false;
      if (op === "$=" && !actual.endsWith(val)) return false;
      if (op === "|=" && actual !== val && !actual.startsWith(val + "-")) return false;
      continue;
    }
    if (tok.startsWith("::")) return false; // pseudo-elements handled by caller
    if (tok.startsWith(":")) {
      if (tok === ":root") {
        if (el.tag.toLowerCase() !== "html") return false;
        continue;
      }
      const pm = /^:(is|where|not)\((.*)\)$/.exec(tok);
      if (pm) {
        const [, fn, arg] = pm;
        const alts = splitTop(arg);
        if (fn === "not") {
          if (alts.some((a) => compoundMatches(a, el))) return false;
        } else if (!alts.some((a) => compoundMatches(a, el))) return false;
        continue;
      }
      if (/^:nth-child\((.+)\)$/.test(tok)) {
        if (el.childIndex == null) return false;
        const expr = /^:nth-child\((.+)\)$/.exec(tok)[1].replace(/\s/g, "");
        const i = el.childIndex;
        const nm = /^([+-]?\d*)n([+-]\d+)?$/.exec(expr);
        if (nm) {
          const a = nm[1] === "" || nm[1] === "+" ? 1 : nm[1] === "-" ? -1 : parseInt(nm[1], 10);
          const b = nm[2] ? parseInt(nm[2], 10) : 0;
          if (a === 0 ? i !== b : ((i - b) / a < 0 || (i - b) % a !== 0)) return false;
        } else if (/^\d+$/.test(expr) ? i !== parseInt(expr, 10) : expr === "odd" ? i % 2 !== 1 : expr === "even" ? i % 2 !== 0 : true) {
          return false;
        }
        continue;
      }
      /* Interactive pseudo-classes: resting probes never match. */
      return false;
    }
    /* Type selector */
    if (el.tag.toLowerCase() !== tok.toLowerCase()) return false;
  }
  return any;
};

const specificityOf = (compound) => {
  let a = 0;
  let b = 0;
  let c = 0;
  const re = /(\*|[a-zA-Z][\w-]*|\.(?:[\w-]|\\.)+|\[[^\]]+\]|::?[\w-]+(?:\([^)]*\))?)/g;
  let m;
  while ((m = re.exec(compound))) {
    const tok = m[1];
    if (tok === "*") continue;
    if (tok.startsWith("#")) { a += 1; continue; }
    if (tok.startsWith(".") || tok.startsWith("[")) { b += 1; continue; }
    if (tok.startsWith("::")) { c += 1; continue; }
    const pm = /^:(is|not|where)\((.*)\)$/.exec(tok);
    if (pm) {
      const [, fn, arg] = pm;
      if (fn === "where") continue;
      const specs = splitTop(arg).map(specificityOf);
      specs.sort((x, y) => (y[0] - x[0]) || (y[1] - x[1]) || (y[2] - x[2]));
      a += specs[0][0]; b += specs[0][1]; c += specs[0][2];
      continue;
    }
    if (tok.startsWith(":")) { b += 1; continue; }
    c += 1;
  }
  return [a, b, c];
};

/* Match a full selector against an ancestor chain (last el = target). */
const selectorMatches = (selector, chain) => {
  const sepParts = [];
  {
    // split on combinators > and whitespace at top level
    let depth = 0;
    let cur = "";
    for (const ch of selector + " ") {
      if (ch === "(") depth += 1;
      if (ch === ")") depth -= 1;
      if (depth === 0 && (ch === ">" || /\s/.test(ch))) {
        if (cur.trim()) { sepParts.push(cur.trim()); cur = ""; }
        if (ch === ">") sepParts.push(">");
        continue;
      }
      cur += ch;
    }
  }
  if (sepParts[sepParts.length - 1] === ">") return false;
  const target = chain[chain.length - 1];
  if (!compoundMatches(sepParts[sepParts.length - 1], target)) return false;
  let ci = chain.length - 1;
  let needChild = false;
  for (let pi = sepParts.length - 2; pi >= 0; pi--) {
    const part = sepParts[pi];
    if (part === ">") { needChild = true; continue; }
    const from = needChild ? ci - 1 : 0;
    let found = -1;
    if (needChild && from < 0) return false;
    for (let i = from; i < ci; i++) {
      if (compoundMatches(part, chain[i])) { found = i; break; }
    }
    if (found < 0) return false;
    ci = found;
    needChild = false;
  }
  return true;
};

/* ── Cascade: one pass over the full chain → per-element winner maps ──── */
const cmpKeys = (a, b) => {
  for (let i = 0; i < a.length; i++) if (a[i] !== b[i]) return a[i] - b[i];
  return 0;
};

const ruleSelectors = new Map(); /* rule → its top-level selector list */
const selectorsOf = (rule) => {
  if (!ruleSelectors.has(rule)) ruleSelectors.set(rule, splitTop(rule.selector));
  return ruleSelectors.get(rule);
};

const cascadeChain = (() => {
  const cache = new Map();
  return (chain) => {
    const cacheKey = chain.map((el) => el.tag + "|" + [...el.classes].join(".") + "|" + [...el.attrs].join(";") + "|" + el.childIndex).join(">");
    if (cache.has(cacheKey)) return cache.get(cacheKey);
    /* maps[i] holds ONLY declarations whose selector's subject is chain[i].
       Inheritance and background-scoping then behave like the real DOM. */
    const maps = chain.map(() => new Map());
    for (let i = 0; i < chain.length; i++) {
      const slice = chain.slice(0, i + 1);
      for (const rule of rules) {
        for (const selector of selectorsOf(rule)) {
          if (!selectorMatches(selector, slice)) continue;
          const spec = specificityOf(selector);
          for (const decl of rule.decls) {
            const key = [
              decl.important ? 1 : 0,
              rule.layerRank === null ? 1e6 : rule.layerRank,
              spec[0], spec[1], spec[2], rule.order,
            ];
            if (process.env.DEBUG_VAR && decl.prop === process.env.DEBUG_VAR && process.env.DEBUG_EL === String(i)) {
              console.error(`    CAND ${decl.prop} @el${i}: ${decl.value}  sel=${selector} spec=${spec} layer=${rule.layerRank} #${rule.order}`);
            }
            const prev = maps[i].get(decl.prop);
            if (!prev || cmpKeys(key, prev.key) > 0) {
              maps[i].set(decl.prop, { value: decl.value, key, selector, layerRank: rule.layerRank, order: rule.order });
            }
          }
        }
      }
      /* Inline styles: win against every rule, lose only to !important. */
      const inline = chain[i].attrs.get("style");
      if (inline) {
        for (const decl of inline.split(";")) {
          const idx = decl.indexOf(":");
          if (idx < 1) continue;
          const prop = decl.slice(0, idx).trim();
          const value = decl.slice(idx + 1).trim();
          maps[i].set(prop, { value, key: [0, 1e9, 0, 0, 0, 1e9], selector: "[style]", layerRank: null, order: 1e9 });
        }
      }
    }
    cache.set(cacheKey, maps);
    return maps;
  };
})();

/* Inherited props walk toward the root; plain props read only the element. */
const pick = (maps, idx, prop, inherited = true) => {
  for (let i = idx; i >= 0; i--) {
    const hit = maps[i].get(prop);
    if (hit) return hit;
    if (!inherited) break;
  }
  return null;
};

/* Deep var() substitution — theme tokens nest inside rgb()/color-mix(). */
const resolveDeep = (maps, idx, input) => {
  let v = input;
  for (let pass = 0; pass < 20 && v.includes("var("); pass++) {
    v = v.replace(/var\(\s*(--[\w-]+)\s*(?:,([^()]*(?:\([^()]*\))?[^()]*))?\)/g, (all, name, fb) => {
      const hit = pick(maps, idx, name, true);
      if (hit) return hit.value;
      return fb !== undefined ? fb.trim() : all;
    });
  }
  return evalCalc(v);
};

/* The §C4 bench left `clamp()/calc()/min()` arithmetic where a number sat
   during G1–G3 — after substitution it is pure arithmetic over literals
   (multipliers absent → fallback 1). A numerator cannot read strings, so
   the evaluator folds the arithmetic it finds: calc(A * B), clamp(l, e, h),
   min(e, h), parens, + - * / with px/%-free numbers. Anything it cannot
   fold it leaves alone (the parse hand-off still catches what must fail). */
const evalCalc = (s) => {
  if (!/[\w-]+\(|\bcalc\(/u.test(s)) return s;
  const num = "(-?\\d*\\.?\\d+)";
  let v = s;
  for (let pass = 0; pass < 24; pass++) {
    const before = v;
    /* innermost calc/function-free parens */
    v = v.replace(/calc\(([^()]+)\)/u, (_, inner) => foldExpr(inner));
    v = v.replace(/clamp\(\s*([^(),]+?)\s*,\s*([^(),]+?)\s*,\s*([^(),]+?)\s*\)/u,
      (_, lo, e, hi) => foldNum(Math.min(lit(hi), Math.max(lit(lo), lit(e)))));
    v = v.replace(/min\(\s*([^(),]+?)\s*,\s*([^(),]+?)\s*\)/u,
      (_, a, b) => foldNum(Math.min(lit(a), lit(b))));
    v = v.replace(/max\(\s*([^(),]+?)\s*,\s*([^(),]+?)\s*\)/u,
      (_, a, b) => foldNum(Math.max(lit(a), lit(b))));
    /* multiplication of literals, unit-carrying right or left */
    v = v.replace(new RegExp(num + "(px|%)?\\s*\\*\\s*" + num + "(px|%)?(?!\\s*[\\w(])", "u"),
      (_, a, ua, b, ub) => foldNum(parseFloat(a) * parseFloat(b)) + (ub || ua || ""));
    if (v === before) break;
  }
  return v;
};
/* number-or-calc-folded number; unit-carrying answers stay unfoldable
   (left as-is rather than mangled — the parse hand-off handles those). */
const lit = (x) => {
  const m = /^\s*(-?[\d.]+)\s*$/u.exec(x);
  if (m) return parseFloat(m[1]);
  const f = foldExpr(x);
  const m2 = /^(-?[\d.]+)(px|%)?$/u.exec(f);
  if (m2) return parseFloat(m2[1]);
  return NaN; /* genuinely non-numeric: the pair below fails loudly */
};
const foldExpr = (expr) => {
  /* "0.55 * 1" or "26px * 1" when observered innermost */
  const m = /^\s*(-?[\d.]+)(px|%)?\s*([*/+-])\s*(-?[\d.]+)(px|%)?\s*$/u.exec(expr);
  if (!m) {
    const plain = /^\s*(-?[\d.]+)(px|%)?\s*$/u.exec(expr);
    if (plain) return plain[1] + (plain[2] ?? "");
    return `calc(${expr})`;
  }
  const a = parseFloat(m[1]), b = parseFloat(m[4]);
  const op = m[3];
  const out = op === "*" ? a * b : op === "/" ? a / b : op === "+" ? a + b : a - b;
  const unit = m[5] ?? m[2] ?? "";
  return foldNum(out) + (op === "*" || op === "/" ? unit : m[5] && m[5] === m[2] ? unit : unit);
};
const foldNum = (n) => String(Math.round(n * 1e6) / 1e6);

/* ── Colour math ───────────────────────────────────────────────────────── */
const oklchToSrgb = (L, C, H) => {
  const hr = (H * Math.PI) / 180;
  const a = C * Math.cos(hr), b = C * Math.sin(hr);
  const Lp = L + 0.3963377774 * a + 0.2158037573 * b;
  const Mp = L - 0.1055613458 * a - 0.0638541728 * b;
  const Sp = L - 0.0894841775 * a - 1.291485548 * b;
  const l = Lp ** 3, m = Mp ** 3, s = Sp ** 3;
  const gam = (c) => (c <= 0.0031308 ? 12.92 * c : 1.055 * Math.pow(Math.max(c, 0), 1 / 2.4) - 0.055);
  const R = +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
  const G = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
  const B = -0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s;
  return [gam(R), gam(G), gam(B)].map((x) => Math.max(0, Math.min(255, Math.round(x * 255))));
};

const parseColor = (value) => {
  const v = value.trim();
  const mix = /^color-mix\(in (?:srgb|oklab|oklch),\s*(.+?)\s+([\d.]+)%,\s*(.+)\)$/u.exec(v);
  if (mix) {
    const c1 = parseColor(mix[1]);
    const c2 = parseColor(mix[3]);
    const p = parseFloat(mix[2]) / 100;
    return [
      Math.round(c1[0] * p + c2[0] * (1 - p)),
      Math.round(c1[1] * p + c2[1] * (1 - p)),
      Math.round(c1[2] * p + c2[2] * (1 - p)),
      c1[3] * p + c2[3] * (1 - p),
    ];
  }
  const ok = /^oklch\(([^)]+)\)$/u.exec(v);
  if (ok) {
    const parts = ok[1].split(/[\s/]+/u).filter(Boolean).map((x) => (x.endsWith("%") ? parseFloat(x) / 100 : parseFloat(x)));
    const [r, g, b] = oklchToSrgb(parts[0], parts[1] ?? 0, parts[2] ?? 0);
    return [r, g, b, parts.length > 3 ? parts[3] : 1];
  }
  if (/^#/u.test(v)) {
    let h = v.slice(1);
    if (h.length === 3 || h.length === 4) h = h.split("").map((c) => c + c).join("");
    const n = parseInt(h.slice(0, 6), 16);
    const alpha = h.length === 8 ? parseInt(h.slice(6, 8), 16) / 255 : 1;
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255, alpha];
  }
  const rm = /^rgba?\((.*)\)$/u.exec(v);
  if (rm) {
    const parts = rm[1].split(/[\s,/]+/u).filter(Boolean).map((x) => (x === "none" ? "0" : x));
    const rgb = parts.slice(0, 3).map((x) => (x.endsWith("%") ? parseFloat(x) * 2.55 : parseFloat(x)));
    const alpha = parts.length > 3 ? (parts[3].endsWith("%") ? parseFloat(parts[3]) / 100 : parseFloat(parts[3])) : 1;
    return [rgb[0], rgb[1], rgb[2], alpha];
  }
  const hm = /^hsla?\(([^)]+)\)$/u.exec(v);
  if (hm) {
    const parts = hm[1].split(/[\s,/]+/u).filter(Boolean);
    const h = parseFloat(parts[0]), s = parseFloat(parts[1]) / 100, l = parseFloat(parts[2]) / 100;
    const a = s * Math.min(l, 1 - l);
    const f = (n) => {
      const k = (n + h / 30) % 12;
      return Math.round(255 * (l - a * Math.max(-1, Math.min(k - 3, 9 - k, 1))));
    };
    return [f(0), f(8), f(4), parts.length > 3 ? parseFloat(parts[3]) : 1];
  }
  if (v === "transparent") return [0, 0, 0, 0];
  throw new Error(`unparsed color: ${v}`);
};

const composite = ([r, g, b, a], [br, bg, bb]) => [
  Math.round(r * a + br * (1 - a)),
  Math.round(g * a + bg * (1 - a)),
  Math.round(b * a + bb * (1 - a)),
];

const luminance = ([r, g, b]) => {
  const lin = (c) => {
    const s = c / 255;
    return s <= 0.04045 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
};
const contrast = (fg, bg) => {
  const [h, l] = [luminance(fg), luminance(bg)].sort((x, y) => y - x);
  return (h + 0.05) / (l + 0.05);
};

/* ── DOM modeling ──────────────────────────────────────────────────────── */
const El = (tag, classStr = "", attrs = {}, childIndex = null) => ({
  tag,
  classes: new Set(classStr.split(/\s+/).filter(Boolean)),
  attrs: new Map(Object.entries(attrs)),
  childIndex,
});

const paint = (label, mode, chain, htmlOverride = {}, pseudo = null) => {
  /* pseudo: ""::selection"" models the UA selection sheet — the reading is
     the selection tint over the probe's own background, in both modes. */
  const htmlAttrs = {
    "data-theme": mode,
    "data-theme-mode": mode === "midnight" ? "dark" : "light",
    "data-appearance": "clay",
    lang: "en",
    ...htmlOverride,
  };
  const fullChain = [El("html", "", htmlAttrs), ...chain];
  const maps = cascadeChain(fullChain);
  /* Chrome: the target element's winning box-shadow, resolved enough to
     quote in a paint verdict (var()s expanded; unresolved pieces left
     verbatim, since a computed-value var gap is itself the finding). */
  const shHit = pick(maps, fullChain.length - 1, "box-shadow", false);
  let shadow = null;
  if (shHit) { try { shadow = resolveDeep(maps, fullChain.length - 1, shHit.value); } catch { shadow = null; } }
  /* Background: each element paints ONLY its own background-color
     (a non-inherited property), composited root→target over white. */
  let bg = [255, 255, 255]; /* behind the document itself */
  const bgRawValues = [];
  for (let i = 0; i < fullChain.length; i++) {
    const hit = pick(maps, i, "background-color", false);
    if (!hit) continue;
    const resolved = resolveDeep(maps, i, hit.value);
    const el = fullChain[i];
    bgRawValues.push(`${el.tag}.${[...el.classes].join(".")}: ${hit.value} → ${resolved}  ⮜ ${hit.selector} (layer ${hit.layerRank} #${hit.order})`);
    try {
      bg = composite(parseColor(resolved), bg);
    } catch { /* gradient-only layers carry no flat tint */ }
  }
  /* Text color: inherited — nearest ancestor's winning declaration. */
  const cHit = pick(maps, fullChain.length - 1, "color", true);
  let color = null;
  let colorErr = "";
  let colorRaw = "";
  if (cHit) {
    try {
      colorRaw = resolveDeep(maps, fullChain.length - 1, cHit.value);
      const c = parseColor(colorRaw);
      color = c[3] < 1 ? composite(c, bg) : c.slice(0, 3);
    } catch (e) { colorErr = String(e.message ?? e); color = null; }
  }
  const ratio = color ? contrast(color, bg) : null;
  if (process.env.PAINT_DEBUG === label) {
    console.error("=== PAINT_DEBUG:", label, "|", mode);
    console.error(bgRawValues.join("\n"));
    console.error("color:", colorRaw, cHit?.selector, "| bg:", bg.map((x) => x.toFixed(1)).join(","));
    const m0 = maps[0];
    for (const k of ["--surface-alpha", "--surface-alpha-2", "--surface-alpha-base", "--surface-alpha-base-2"]) {
      console.error("maps[0]", k, "→", m0.has(k) ? m0.get(k).value : "ABSENT");
    }
  }
  return {
    label, mode, bg, color, ratio, colorRaw, colorErr, bgRawValues, shadow, pseudo,
    colorWinner: cHit ? `${cHit.selector} (layer ${cHit.layerRank} #${cHit.order})` : "",
  };
};

const hx = ([r, g, b]) => `#${[r,g,b].map((x) => Math.max(0, Math.min(255, Math.round(x))).toString(16).padStart(2, "0")).join("")}`;
const report = [];
const UA_DARK_HINTS = {
  "input::selection": [26, 18, 46, 1],
  "::selection": [184, 166, 255, 0.45],
};

const probe = (label, modes, chain, min = 4.5) => {
  for (const mode of modes) {
    const p = paint(label, mode, chain);
    report.push(p);
    if (p.color && min) {
      test(`${mode} · ${label} — ${p.ratio.toFixed(2)}:1 ≥ ${min}:1  (${hx(p.color)} on ${hx(p.bg)})`, () => {
        assert.ok(p.ratio >= min, `${mode} ${label}: ${p.ratio.toFixed(2)}:1 below ${min}:1 — ${hx(p.color)} on ${hx(p.bg)}`);
      });
    }
  }
};

const MODES = ["midnight", "daylight"];
const BODY = El("body");

/* The real chains, from the components as shipped: */

/* 1–2. Page text on the plum ground. */
probe("page paragraph (text-text-secondary)", MODES, [BODY, El("p", "text-text-secondary")]);
probe("page heading (text-text-primary)", MODES, [BODY, El("h2", "text-text-primary")], 7);

/* 3. The closing band and its porcelain button. */
const BAND = [BODY, El("footer", "band-deep relative overflow-hidden")];
probe("band-deep paragraph", MODES, [...BAND, El("p", "text-text-secondary")], 7);
probe("band-deep mono-label", MODES, [...BAND, El("p", "mono-label")]);
probe("band porcelain button (.btn-primary)", MODES, [...BAND, El("a", "btn btn-primary")], 3);
probe("band secondary button (.btn-secondary)", MODES, [...BAND, El("a", "btn btn-secondary")]);

/* 4. Footer link pills — the dark-pills-on-lilac question. */
probe(
  "footer link pill (card-interactive bg-ink-900/70)",
  MODES,
  [...BAND, El("a", "card-interactive group relative flex items-center justify-center gap-2.5 rounded-card border-2 border-line-strong/60 bg-ink-900/70 px-3 py-2.5 text-sm font-medium text-text-secondary")],
  4.5,
);

/* 5. The services cards (registry after the source lift). */
const SERVICE = [BODY, El("div", "surface card-interactive p-5 flex flex-col justify-between")];
probe("service card title (text-text-primary)", MODES, [...SERVICE, El("h4", "mt-2 text-sm font-semibold text-text-primary")], 7);
probe("service card body (text-text-muted)", MODES, [...SERVICE, El("p", "mt-2 text-xs leading-relaxed text-text-muted")]);
probe("service card faint label (text-text-faint)", MODES, [...SERVICE, El("span", "text-2xs font-mono text-text-faint")], 3);
probe(
  "service file chip (bg-ink-950/60 + text-brand-200/90)",
  MODES,
  [...SERVICE, El("li", "font-mono text-2xs text-brand-200/90 break-all bg-ink-950/60 rounded px-1.5 py-0.5")],
  4.5,
);

/* 6. 'Being a user first' cards, same registry. */
probe("user-first card body", MODES, [BODY, El("div", "surface card-pad card-interactive flex h-full flex-col"), El("p", "mt-4 text-sm leading-relaxed text-text-muted")]);

/* 7. The pale lavender plates, plum-inked everywhere inside. */
probe("glass-primary heading (display-2)", MODES, [BODY, El("div", "glass-primary card-pad"), El("h2", "display-2")], 7);
probe("glass-primary paragraph (text-text-secondary)", MODES, [BODY, El("div", "glass-primary card-pad"), El("p", "max-w-3xl pt-6 text-sm leading-relaxed text-text-secondary")], 4.5);
probe("glass-primary eyebrow", MODES, [BODY, El("div", "glass-primary card-pad"), El("p", "eyebrow")], 4.5);

/* 8. You-talk card: card-accent — every text tier it contains. */
const CTA = [BODY, El("div", "card-accent grid gap-8 p-8 lg:p-10")];
probe("card-accent paragraph (text-text-secondary)", MODES, [...CTA, El("p", "measure mt-4 text-sm leading-relaxed text-text-secondary")], 4.5);
probe("card-accent heading (display-2)", MODES, [...CTA, El("h2", "display-2")], 7);
probe("card-accent secondary button", MODES, [...CTA, El("a", "btn btn-secondary")], 4.5);

/* 9. Stat tiles — every pastel, both modes. */
for (const [nth, name] of [[1, "lavender"], [2, "mint"], [3, "peach"], [4, "sky"]]) {
  const TILE = [BODY, El("dl", "grid grid-cols-2 gap-3"), El("div", "surface-raised p-3.5", {}, nth)];
  probe(`metric tile ${name} big number`, MODES, [...TILE, El("dd", "mt-1.5 text-base font-semibold text-text-primary")], 7);
  probe(`metric tile ${name} note (text-text-muted)`, MODES, [...TILE, El("dd", "mt-1 text-[0.75rem] leading-relaxed text-text-muted")], 4.5);
}

/* 10. Boring rows: chips inside surfaces. */
probe("chip inside .surface", MODES, [BODY, El("div", "surface p-4"), El("span", "chip")], 4.5);

/* 11. The nav menu card and its destination links. */
const MENU = [BODY, El("header"), El("nav", "nav-pill"), El("div", "relative"), El("div", "menu-card animate-scale-in rounded-panel p-2 shadow-raised")];
probe("nav dropdown card (menu-card)", MODES, [...MENU, El("p", "mono-label px-2.5 pb-2 pt-1")], 4.5);
probe("nav dropdown link tile", MODES, [...MENU, El("a", "card-interactive group flex items-start gap-3 rounded-card border-2 border-line-strong/60 bg-ink-900/70 px-3 py-2.5"), El("span", "flex items-center gap-1.5 text-sm font-semibold text-text-primary")], 7);

/* 12. Nav bar items at rest. */
probe("nav pill link at rest", MODES, [BODY, El("header"), El("nav", "nav-pill"), El("a", "relative flex items-center gap-1.5 rounded-card border-2 px-2.5 py-1.5 text-sm font-semibold border-transparent text-text-muted")], 4.5);

/* §13 plate bench: every plate must carry the plum bench in BOTH modes,
   and a custom color behaves like any named plate. */
const PLATES = [
  ["lavender", "plate-1"],
  ["mint", "plate-2"],
  ["peach", "plate-3"],
  ["sky", "plate-4"],
  ["rose", "plate-5"],
  ["lemon", "plate-6"],
];
for (const [name, cls] of PLATES) {
  const CHAIN = [BODY, El("div", "p-6"), El("div", `${cls} rounded-panel-card p-5`)];
  probe(`plate ${name} heading`, MODES, [...CHAIN, El("p", "text-sm font-semibold text-text-primary")], 7);
  probe(`plate ${name} body`, MODES, [...CHAIN, El("p", "mt-1 text-xs text-text-muted")], 4.5);
}
probe("plate custom heading (--plate-custom set inline)", MODES, [BODY, El("div", "p-6"), El("div", "plate-custom rounded-panel-card p-5", { style: "--plate-custom: 255 205 222" }), El("p", "text-sm font-semibold text-text-primary")], 7);

/* §15 The clay bar: segmented plate + the sliding pill. The pressed item
   pair is measured as the pill's lavender face against the pressed ink. */
const CLAYBAR = [BODY, El("header"), El("nav", "nav-pill")];
probe("clay bar resting link", MODES, [...CLAYBAR, El("a", "relative flex items-center gap-1.5 rounded-card px-2.5 py-1.5 text-sm font-semibold")], 4.5);
probe("clay bar pressed item on the pill", MODES, [...CLAYBAR, El("span", "navclay__indicator"), El("a", "relative flex items-center gap-1.5 rounded-card px-2.5 py-1.5 text-sm font-semibold", { "aria-current": "page" })], 7);

/* The developer dossier (DeveloperStory /about): a light tinted plate
   nested in band-rich — its innards were washed white at midnight (D20). */
{
  const DOSS = [BODY, El("section", "band-rich relative overflow-hidden"), El("div", "shell relative py-16"), El("div", "panel-feature edge-conic p-6 sm:p-8")];
  probe("dossier name (text-text-primary)", MODES, [...DOSS, El("p", "mt-1.5 text-xl font-semibold tracking-tight text-text-primary")], 7);
  probe("dossier figure (.figure-md)", MODES, [...DOSS, El("span", "figure figure-md block")], 7);
  probe("dossier muted line (text-text-muted)", MODES, [...DOSS, El("p", "mt-1 text-sm text-text-muted")], 4.5);
  probe("dossier mono-label", MODES, [...DOSS, El("p", "mono-label")], 4.5);
  probe("dossier spine phase (text-text-primary)", MODES, [...DOSS, El("li", ""), El("p", "text-sm font-semibold text-text-primary")], 7);
  probe("dossier spine note (text-text-muted)", MODES, [...DOSS, El("li", ""), El("p", "mt-0.5 text-sm leading-relaxed text-text-muted")], 4.5);
  probe("dossier date row (mono-label)", MODES, [...DOSS, El("span", "mono-label")], 4.5);
}

/* 15–20. CHROME & FORMS (phase 6, §19) + FAMILY ISOLATION gate.        */
const WELL = [BODY, El("div", "surface p-6"), El("input", "w-full rounded-xl border border-line-strong/70 bg-ink-950/80 py-2.5 pl-10 pr-9 text-xs")];
probe("form well input text (pressed, both modes)", MODES, WELL, 7);
test("well placeholder reads one tier quieter than its text", () => {
  const phs = rules.filter((r) =>
    /bg-ink/u.test(r.selector) && /(?::|-)placeholder/u.test(r.selector));
  const decls = phs.flatMap((r) => r.decls.filter((d) => d.prop === "color"));
  assert.ok(decls.length >= 1, "placeholder carve missing from the engine");
  assert.ok(decls.every((d) => /--text-3/u.test(d.value)), `placeholder must read the muted tier (--text-3), got ${decls.map((d) => d.value).join(" , ")}`);
});
test("wells carve, they do not lift", () => {
  for (const mode of MODES) {
    const p = paint("well shadow", mode, WELL);
    assert.ok(p.shadow, `${mode}: well has no winning box-shadow`);
    assert.ok(/inset/u.test(p.shadow), `${mode}: well shadow must be inset, got: ${p.shadow}`);
    assert.ok(!/transparent/i.test(String(p.shadow)), `${mode}: well lost its recess`);
  }
});
test("form wells sit in the recess band, not on the card face", () => {
  for (const mode of MODES) {
    const wellBg = paint("well bg", mode, WELL).bg;
    const cardBg = paint("card bg", mode, [BODY, El("div", "surface p-6")]).bg;
    assert.ok(
      Math.abs(luminance(wellBg) - luminance(cardBg)) > 0.004,
      `${mode}: well indistinguishable from its card (${hx(wellBg)} on ${hx(cardBg)})`,
    );
  }
});
test("chrome grammar: focus rings, scrollbars, accent-color, print — all present", () => {
  const clayRules = rules.filter((r) => r.selector.includes("[data-appearance=clay]") || r.selector.includes('data-appearance="clay"'));
  const sel = (piece) => clayRules.filter((r) => r.selector.includes(piece));
  const decl = (prop, needle) => clayRules.some((r) => r.decls.some((d) => d.prop === prop && (!needle || needle.test(d.value))));
  assert.ok(sel(":focus-visible").length >= 2, "focus-visible carve missing (both modes)");
  assert.ok(sel("::-webkit-scrollbar").length >= 3, "scrollbar grammar missing");
  assert.ok(decl("accent-color", /clay-lavender/u), "accent-color bridge missing");
  assert.ok(/@media\s+print/u.test(cssText), "print collapse missing");
});
test("phase-5 motion grammar is complete (media card + clay-fall + wide vignette)", () => {
  const mc = rules.filter((r) => r.selector.includes("clay-media-card"));
  assert.ok(mc.length >= 10, "media card rule set thinned out");
  assert.ok(/@container\s*\(max-width:\s*340px\)/u.test(cssText), "compact container query missing");
  assert.ok(/@container\s*\(min-width:\s*480px\)/u.test(cssText), "wide vignette query missing");
  assert.ok(/@keyframes clay-fall/u.test(cssText), "clay-fall keyframes missing");
  assert.ok(/@supports\s*\(animation-timeline\s*:\s*view\(\)\)/u.test(cssText), "clay-fall feature gate missing");
  {
    const reduceBlocks = cssText.split(/@media \(prefers-reduced-motion\s*:\s*reduce\)\s*\{/u);
    assert.ok(reduceBlocks.length > 1, "no reduced-motion block at all");
    assert.ok(
      reduceBlocks.slice(1).some((b) => /hero-shard/u.test(b.slice(0, 1500)) && /animation:\s*none/u.test(b.slice(0, 1500))),
      "reduced-motion no longer covers the shards",
    );
  }
});

test("::selection — reading a selection never inverts the page", () => {
  /* UA sheets do not model ::selection in this harness; what CAN be
     asserted is the tint exists in the engine as a token-driven color,
     sits on the accent budget, and contrasts a white or near-white page
     in both modes. */
  const selRules = rules.filter((r) => r.selector.includes("::selection"));
  assert.ok(selRules.length >= 2, "::selection is unmanned (" + selRules.length + " rules)")
  const sel = UA_DARK_HINTS["::selection"];
  const white = [255, 255, 255];
  const night = [18, 13, 30];
  const dayC = contrast(composite(sel, white), white);
  const nightC = contrast(composite(sel, night), night);
  assert.ok(dayC > 1.2, `selection tint invisible in daylight: ${dayC.toFixed(2)}:1`);
  assert.ok(nightC > 1.2, `selection tint invisible at midnight: ${nightC.toFixed(2)}:1`);
});

/* 21. Family isolation — the engine's selectors live behind
   [data-appearance="clay"]; under another family NOTHING the engine
   owns may win. Probes replay twilight glass + void glass; the engine
   may still write vars (they are scoped by design) but no SURFACE rule
   should paint. */
const GLASS_TILE = [BODY, El("dl", "grid grid-cols-2 gap-3"), El("div", "stat-tile p-3.5", {}, 2)];
for (const family of ["twilight", "void"]) {
  test(`family isolation: stat-tile rotation silent under ${family}`, () => {
    for (const mode of MODES) {
      const clay = paint("tile", mode, GLASS_TILE, { "data-appearance": "clay" });
      const other = paint("tile", mode, GLASS_TILE, { "data-appearance": family });
      assert.notDeepEqual(other.bg, clay.bg, `${family} · ${mode}: clay rotation leaked onto the tile`);
    }
  });
}

/* 14. NORIA + ZONE PLATES (phase 4) — a grid wrapper may re-point the stat
   rotation at any 4-of-6 plates, the feature grid walks panel/band faces
   in daylight, and both fall silent at night (zones keep the night).    */
test("noria: .plate-set-b re-points its own stat rotation", () => {
  const tile = (set, nth) =>
    paint("tile", "daylight", [BODY, El("dl", `${set} grid grid-cols-2 gap-3`.trim()), El("div", "stat-tile p-3.5", {}, nth)]).bg;
  const base1 = tile("", 1);
  const alt1 = tile("plate-set-b", 1);
  const alt3 = tile("plate-set-b", 3);
  assert.notDeepEqual(alt1, base1, `plate-set-b never moved tile 1 (still ${hx(base1)})`);
  assert.ok(luminance(alt1) > 0.55, `plate-set-b tile 1 left the pastel band: ${hx(alt1)}`);
  assert.ok(luminance(alt3) > 0.55, `plate-set-b tile 3 left the pastel band: ${hx(alt3)}`);
});
test("noria: no wrapper means exactly the house rotation", () => {
  const t1 = paint("tile", "daylight", [BODY, El("dl", "grid"), El("div", "stat-tile p-3.5", {}, 1)]).bg;
  const t2 = paint("tile", "daylight", [BODY, El("dl", "grid"), El("div", "stat-tile p-3.5", {}, 2)]).bg;
  assert.notDeepEqual(t1, t2, "house rotation stopped rotating");
});
test("noria: midnight tiles stay pastel even under a re-pointed set", () => {
  const t = paint("tile", "midnight", [BODY, El("dl", "plate-set-b grid"), El("div", "stat-tile p-3.5", {}, 3)]).bg;
  assert.ok(luminance(t) > 0.55, `plate-set-b tile darkened at midnight: ${hx(t)}`);
});
test("feature-plates: daylight walks panel/band faces, midnight stays zones", () => {
  const grid = (mode, nth) =>
    paint("fp", mode, [BODY, El("div", "feature-plates mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4"), El("div", "surface-raised p-5", {}, nth)]).bg;
  const day1 = grid("daylight", 1), day2 = grid("daylight", 2), day3 = grid("daylight", 3);
  assert.notDeepEqual(day1, day2, `feature-plates even tiles took the panel face: ${hx(day1)} on 1`);
  assert.notDeepEqual(day3, day1, `feature-plates nth-3 repeats the panel face instead of the band: ${hx(day3)}`);
  const night1 = grid("midnight", 1), night2 = grid("midnight", 2);
  assert.deepEqual(night1, night2, `midnight feature grid rotated: ${hx(night1)} vs ${hx(night2)}`);
  assert.ok(luminance(night1) < 0.16, `midnight feature panel lit up: ${hx(night1)}`);
});
probe("feature-plates heading on the panel face (daylight)", ["daylight"], [BODY, El("div", "feature-plates"), El("div", "surface-raised p-5", {}, 1), El("h3", "card-title")], 7);
probe("feature-plates heading on the band face (daylight)", ["daylight"], [BODY, El("div", "feature-plates"), El("div", "surface-raised p-5", {}, 3), El("h3", "card-title")], 7);
probe("feature-plates note on the card face (daylight)", ["daylight"], [BODY, El("div", "feature-plates"), El("div", "surface-raised p-5", {}, 2), El("p", "mt-2.5 text-sm leading-relaxed text-text-muted")], 4.5);
probe("feature-plates heading at midnight (zone ink)", ["midnight"], [BODY, El("div", "feature-plates"), El("div", "surface-raised p-5", {}, 1), El("h3", "card-title")], 7);
test("rule-plate: the carve exists in both modes, reading family hues", () => {
  const hits = rules.filter((r) => r.selector.includes("rule-plate"));
  assert.ok(hits.length >= 4, `engine lost the rule-plate rules: ${hits.length} left`);
  assert.ok(
    hits.some((h) => h.decls.some((d) => /clay-drop-h/u.test(d.value))),
    "rule-plate daylight carve does not read --clay-drop-h",
  );
  assert.ok(
    hits.some((h) => /midnight/u.test(h.selector) && h.decls.some((d) => /clay-foil/u.test(d.value))),
    "rule-plate midnight carve does not read --clay-foil",
  );
});

/* The zone-luminance contract (the 70/20/10 rule at midnight). */
test("midnight zones stay dark: page < cards < accents (70/20/10)", () => {
  const pageBg = paint("page", "midnight", [BODY]).bg;
  const bandBg = paint("band", "midnight", [BODY, El("footer", "band-deep relative overflow-hidden")]).bg;
  const cardBg = paint("card", "midnight", [BODY, El("div", "surface p-4")]).bg;
  const tileBg = paint("tile", "midnight", [BODY, El("dl", "grid"), El("div", "stat-tile p-3.5", {}, 2)]).bg;
  assert.ok(luminance(pageBg) < 0.06, `page too light: ${hx(pageBg)} (${luminance(pageBg).toFixed(3)})`);
  assert.ok(luminance(bandBg) < 0.16, `band too light: ${hx(bandBg)} (${luminance(bandBg).toFixed(3)})`);
  assert.ok(luminance(cardBg) < 0.12, `card too light: ${hx(cardBg)} (${luminance(cardBg).toFixed(3)})`);
  assert.ok(luminance(tileBg) > 0.55, `pastel tile lost its lift: ${hx(tileBg)} (${luminance(tileBg).toFixed(3)})`);
});


/* ═══════════════════════════════════════════════════════════════════════
 * G0 · THE CRYSTAL DOSSIER + THE FAMILY-WIDE LIGHT-CHIP DEFECT
 * (glassmorphism roadmap, phase G0 — gates first, red before green)
 *
 * probeIn() is the family-parametric twin of probe(): same chains, same
 * floors, but data-appearance is a parameter. The clay probes above stay
 * untouched — this section is additive by contract.
 *
 * defectPin() encodes a VERIFIED LIVE DEFECT: the reading is asserted to
 * be BELOW the floor *today*. The row goes green in CI because the defect
 * is real and catalogued; the day G1's lift fixes it, the pin fails loudly
 * — which is the signal to promote the row to an ordinary floor probe.
 * ═══════════════════════════════════════════════════════════════════════ */

const probeIn = (appearance, label, modes, chain, min = 4.5) => {
  for (const mode of modes) {
    const p = paint(label, mode, chain, { "data-appearance": appearance });
    report.push(p);
    if (p.color && min) {
      test(`${mode} · [${appearance}] ${label} — ${p.ratio.toFixed(2)}:1 ≥ ${min}:1  (${hx(p.color)} on ${hx(p.bg)})`, () => {
        assert.ok(
          p.ratio >= min,
          `${mode} ${appearance} ${label}: ${p.ratio.toFixed(2)}:1 below ${min}:1 — ${hx(p.color)} on ${hx(p.bg)}`,
        );
      });
    }
  }
};

/* The exact product pairing from the 16:29 screenshot, file-name chips on
   the developer page (developer/page.tsx SERVICE FILES). On daylight the
   --ink-950 wash is paper-light in EVERY family (mode-inverted triplet,
   globals.css :341), so brand-200 light ink sits on light paper — the clay
   §20 fix covered clay only. These pins hold the defect in the harness for
   the four non-clay families; midnight reads dark-on-dark and meets floor
   already, so those rows are ordinary probes. */
const FILE_CHIP = [BODY, El("li", "font-mono text-2xs text-brand-200/90 break-all bg-ink-950/60 rounded px-1.5 py-0.5")];
/* G1 LANDED: crystal-engine §C1 lifts the carrier to glass and globals'
   daylight legibility grammar re-inks the foil — the four G1 pins sprung
   and were promoted to these ordinary floor probes (1.25→fixed). */
for (const family of ["crystal", "studio", "monet", "industrial"]) {
  probeIn(family, "service file chip (bg-ink-950/60 + text-brand-200/90) · post-G1", MODES, FILE_CHIP, 4.5);
}

/* The crystal material itself — what the family already paints correctly.
   These rows are the green board G1–G6 must keep green while re-glassing:
   core surfaces in both modes, floors at WCAG body (4.5) and display (7). */
probeIn("crystal", "surface card body", MODES, [BODY, El("div", "surface card-interactive p-5"), El("p", "text-text-secondary")], 4.5);
probeIn("crystal", "surface-raised heading", MODES, [BODY, El("div", "surface-raised p-6"), El("h2", "card-title")], 7);
probeIn("crystal", "stat-tile big number", MODES, [BODY, El("dl", "grid"), El("div", "stat-tile p-3.5", {}, 2), El("dd", "mt-1.5 text-base font-semibold text-text-primary")], 7);
probeIn("crystal", "glass-primary heading (display-2)", MODES, [BODY, El("div", "glass-primary card-pad"), El("h2", "display-2")], 7);
probeIn("crystal", "glass-primary paragraph", MODES, [BODY, El("div", "glass-primary card-pad"), El("p", "text-text-secondary")], 4.5);
probeIn("crystal", "band-solid paragraph", MODES, [BODY, El("div", "band-solid"), El("p", "text-text-secondary")], 4.5);
probeIn("crystal", "menu-card item label (tier-3)", MODES, [BODY, El("div", "menu-card"), El("span", "text-text-secondary")], 4.5);
probeIn("crystal", "float-card caption over artwork", MODES, [BODY, El("div", "float-card rounded-panel p-4"), El("p", "text-text-secondary")], 4.5);
probeIn("crystal", "card-accent paragraph", MODES, [BODY, El("div", "card-accent card-pad"), El("p", "text-text-secondary")], 4.5);
probeIn("crystal", "plate body text", MODES, [BODY, El("div", "plate p-5"), El("p", "text-text-secondary")], 4.5);

/* ── G1 · the lift itself, painted ──────────────────────────────────────
   The §C1 rules change what utility carriers render; these probes hang
   the floors on the LIFTED paint, not the pre-lift stand-in. */
probeIn("crystal", "lifted container (bg-ink-900/80 well) body text", MODES,
  [BODY, El("div", "rounded-2xl border border-line-strong/60 bg-ink-900/80 p-4 shadow-panel"), El("p", "text-text-secondary")], 4.5);
probeIn("crystal", "lifted chip on card in icy daylight", MODES,
  [BODY, El("div", "surface card-pad"), El("span", "rounded-full bg-ink-950/60 px-2 py-1 font-mono text-2xs text-text-secondary")], 4.5);

/* G2 · frosted wells: the input wells and the inked caret family. */
probeIn("crystal", "form well text (bg-ink-950/60 input)", MODES,
  [BODY, El("div", "surface-raised p-6"), El("input", "mt-2 h-11 w-full rounded-xl border bg-ink-950/60 px-3.5 text-sm text-text-primary")], 4.5);
probeIn("crystal", "form well label above the glass", MODES,
  [BODY, El("div", "surface-raised p-6"), El("label", "block text-sm font-medium text-text-secondary")], 4.5);

/* G3 · the named surfaces: chips, eyebrow pill, table header, inline code. */
probeIn("crystal", "chip (.chip pill)", MODES,
  [BODY, El("span", "chip"), El("span", "text-text-secondary")], 4.5);
probeIn("crystal", "eyebrow-chip (brand pill)", MODES,
  [BODY, El("p", "eyebrow-chip")], 4.5);
probeIn("crystal", "table header on the chilled row", MODES,
  [BODY, El("table", "w-full border-collapse"), El("thead"), El("tr", "bg-ink-950/90 text-text-faint"), El("th", "text-3xs font-semibold uppercase")], 4.5);
probeIn("crystal", "inline code chip (bg-ink-850 + text-brand-100)", MODES,
  [BODY, El("p", "text-text-secondary"), El("code", "rounded bg-ink-850 px-1.5 py-0.5 font-mono text-brand-100")], 4.5);
/* The inline-code defect class exists wherever brand-100 rides a foil —
   it is family-generic: same grammar, four times. */
for (const family of ["studio", "monet", "industrial"]) {
  probeIn(family, "inline code chip (bg-ink-850 + text-brand-100)", MODES,
    [BODY, El("p", "text-text-secondary"), El("code", "rounded bg-ink-850 px-1.5 py-0.5 font-mono text-brand-100")], 4.5);
}


/* The studio foil contract — the 16:54 screenshots. When a visitor picks an
   accent in the ThemeStudio, useStudio rewrites --clay-foil inline on <html>,
   and §1 of the engine paints EVERY bg-ink-* utility carrier with it. The
   write once twisted the midnight base (46 34 62) in daylight too, so the
   P&L bars, stage chips and sample tags went dark maroon under daylight's
   plum ink (measured 1.0–1.6:1). These probes pin the contract at the
   values the studio actually writes, per mode (raspberry-hub twists): the
   foil may change hue, never its own mode's luminance. */
const studioFoilProbe = (mode, foil, label, chain) => {
  const p = paint(label, mode, chain, { style: `--clay-foil: ${foil}` });
  report.push(p);
  test(`${mode} · ${label} — ${p.ratio?.toFixed(2)}:1 ≥ 4.5:1  (${p.color ? hx(p.color) : "?"} on ${hx(p.bg)})`, () => {
    assert.ok(p.color && p.ratio >= 4.5, `${mode} ${label}: ${p.ratio?.toFixed(2)}:1 below 4.5:1 — foil ${foil}`);
  });
};
const PNL_BAR = [
  BODY, El("main"), El("div", "plate p-5"), El("dl", "mt-5 space-y-3"),
  El("div", "rounded-card border border-line/60 bg-ink-950/50 p-3.5"),
  El("dd", "mt-1 text-sm leading-relaxed text-text-secondary"),
];
const STAGE_CHIP = [
  BODY, El("main"),
  El("div", "min-w-0 flex-1 rounded-card border border-line/60 bg-ink-900/55 p-4"),
  El("span", "rounded-md border border-line-strong/60 bg-ink-850/80 px-2 py-0.5 font-mono text-text-muted"),
];
studioFoilProbe("daylight", "rgb(252 234 247)", "studio foil (daylight base) · P&L bar in .plate", PNL_BAR);
studioFoilProbe("daylight", "rgb(252 234 247)", "studio foil (daylight base) · stage status chip", STAGE_CHIP);
studioFoilProbe("midnight", "rgb(62 34 53)", "studio foil (midnight base) · P&L bar in .plate", PNL_BAR);
studioFoilProbe("midnight", "rgb(62 34 53)", "studio foil (midnight base) · stage status chip", STAGE_CHIP);

/* The engineering-metrics tiles as StatCounter actually ships them
   (17:21 witness: one merged glowing column, white figures). The figure
   carries text-text-primary so the surface's own ink lock governs it —
   the probe uses the component's real shipped classes. Since the
   site-wide dynamic pass, StatCounter/StatBadge render surface-raised
   everywhere; there is no pastel tile left to measure. */
probe("stat figure (StatCounter on surface-raised)", MODES,
  [BODY, El("div", "mt-8 grid gap-4"), El("div", "surface-raised p-4"), El("p", "figure figure-md flex items-baseline gap-1 text-text-primary")], 7);
probe("stat label (StatCounter on surface-raised)", MODES,
  [BODY, El("div", "mt-8 grid gap-4"), El("div", "surface-raised p-4"), El("p", "mt-2 text-xs font-semibold text-text-secondary")], 4.5);
probeIn("crystal", "stat figure (surface-raised at midnight)", ["midnight"],
  [BODY, El("div", "mt-8 grid gap-4"), El("div", "surface-raised p-4"), El("p", "figure figure-md flex items-baseline gap-1 text-text-primary")], 7);
probeIn("crystal", "stat hint (surface-raised at midnight)", ["midnight"],
  [BODY, El("div", "mt-8 grid gap-4"), El("div", "surface-raised p-4"), El("p", "mt-1 text-3xs leading-relaxed text-text-faint font-medium")], 4.5);

/* ── Developer page rewrite (2026-10-13, full-page dynamic pass) —─────
   The /developer page no longer uses stat-tile, card-live, card-accent or
   glass-primary: every card wears the family's standard surface recipe.
   These probes measure the page's real shipped chains on those surfaces
   in all five appearances × both modes. */
probe("developer metric figure (surface-raised)", MODES,
  [BODY, El("div", "mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4"), El("div", "surface-raised p-4"), El("p", "figure figure-md flex items-baseline gap-1 text-text-primary")], 7);
probe("developer metric label (surface-raised)", MODES,
  [BODY, El("div", "mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4"), El("div", "surface-raised p-4"), El("p", "mt-2 text-xs font-semibold text-text-secondary")], 4.5);
probe("developer metric hint (surface-raised)", MODES,
  [BODY, El("div", "mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4"), El("div", "surface-raised p-4"), El("p", "mt-1 text-[11px] leading-relaxed text-text-muted")], 4.5);
probe("developer profile tile value (surface-raised)", MODES,
  [BODY, El("div", "surface-raised card-pad"), El("dl", "mt-5 grid gap-3 sm:grid-cols-3"), El("div", "surface-raised p-3.5"), El("p", "text-sm font-medium text-text-primary")], 4.5);
probe("developer profile tile label (surface-raised)", MODES,
  [BODY, El("div", "surface-raised card-pad"), El("dl", "mt-5 grid gap-3 sm:grid-cols-3"), El("div", "surface-raised p-3.5"), El("p", "text-[11px] text-text-muted")], 4.5);
probe("developer card title (surface)", MODES,
  [BODY, El("div", "surface flex gap-4 p-5 sm:p-6"), El("h3", "card-title")], 4.5);
probe("developer card body (surface)", MODES,
  [BODY, El("div", "surface flex gap-4 p-5 sm:p-6"), El("p", "card-text")], 4.5);
probe("developer gap title (surface)", MODES,
  [BODY, El("li", "surface p-4"), El("h3", "text-sm font-semibold text-text-primary")], 4.5);
probe("developer gap body (surface)", MODES,
  [BODY, El("li", "surface p-4"), El("p", "mt-2 text-xs leading-relaxed text-text-secondary")], 4.5);
probe("developer closing heading (surface-raised)", MODES,
  [BODY, El("div", "surface-raised grid gap-8 p-8"), El("h2", "heading-3 text-text-primary")], 4.5);
probe("developer closing body (surface-raised)", MODES,
  [BODY, El("div", "surface-raised grid gap-8 p-8"), El("p", "mt-3 text-sm leading-relaxed text-text-secondary")], 4.5);

/* ── Cross-page card cross-check (2026-10-13) ──────────────────────────
   The machine-files cards on /optimization (a .dat extension in
   text-text-faint measured 4.32:1 in daylight clay) prompted a sweep of
   every text tier that sits on the standard cards, in every appearance.
   Three clay defects fell out: small brand chips whitened by the generic
   brand-wash ink rule (1.88:1), state badges repainted lemon by the
   [class*=bg-state-*] foil rules (1.04:1), and --text-4 on lilac card
   faces (3.63:1). The locks live in clay-engine.css §7.4b + the chip &
   badge ink section; these probes hold them. */
const X_SUNKEN = El("section", "section relative band-deep border-y border-line/50");
const X_MACHINE = [BODY, X_SUNKEN, El("article", "surface flex h-full flex-col p-5")];
probe("xcheck machine name", MODES, [...X_MACHINE, El("h3", "font-mono text-sm font-semibold text-text-primary")], 7);
probe("xcheck machine extension (text-muted)", MODES, [...X_MACHINE, El("h3", "font-mono text-sm font-semibold text-text-primary"), El("span", "text-text-muted")]);
probe("xcheck machine note (text-muted)", MODES, [...X_MACHINE, El("p", "mt-3 text-sm leading-relaxed text-text-muted")]);
const X_BADGE = "badge-pill inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-3xs font-bold uppercase tracking-[0.1em]";
probe("xcheck badge warning", MODES, [...X_MACHINE, El("span", `${X_BADGE} border-state-warning/50 bg-state-warning/22 text-state-warning`)]);
probe("xcheck badge success", MODES, [...X_MACHINE, El("span", `${X_BADGE} border-state-success/50 bg-state-success/22 text-state-success`)]);
probe("xcheck badge danger", MODES, [...X_MACHINE, El("span", `${X_BADGE} border-state-danger/50 bg-state-danger/22 text-state-danger`)]);
probe("xcheck index chip", MODES, [BODY, El("div", "surface p-5"), El("span", "index-chip inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-[0.6rem] border border-brand-500/45 bg-brand-600/25 font-mono text-xs font-bold text-brand-200")]);
probe("xcheck faint evidence on surface-raised", MODES, [BODY, El("div", "surface-raised p-6"), El("p", "mt-4 font-mono text-2xs text-text-faint")]);
probe("xcheck state-warning on surface-raised", MODES, [BODY, El("div", "surface-raised p-6"), El("dd", "mt-2 text-sm font-semibold text-state-warning")]);
for (const fam of ["crystal", "studio", "monet", "industrial"]) {
  probeIn(fam, "xcheck machine extension (text-muted)", MODES, [...X_MACHINE, El("h3", "font-mono text-sm font-semibold text-text-primary"), El("span", "text-text-muted")]);
  probeIn(fam, "xcheck badge warning", MODES, [...X_MACHINE, El("span", `${X_BADGE} border-state-warning/50 bg-state-warning/22 text-state-warning`)]);
  probeIn(fam, "xcheck faint evidence on surface-raised", MODES, [BODY, El("div", "surface-raised p-6"), El("p", "mt-4 font-mono text-2xs text-text-faint")]);
}

/* Print the full paint table — the analysis the screenshots demanded. */
test("paint report", () => {
  console.log("\nMIDNIGHT / DAYLIGHT PAINT TABLE");
  for (const p of report) {
    console.log(
      `  ${p.mode.padEnd(8)} ${p.label.padEnd(48)} bg ${hx(p.bg)}  text ${p.color ? hx(p.color) : "—"}  ${p.ratio ? p.ratio.toFixed(2) + ":1" : ""}`,
    );
    if (process.env.DEBUG_PAINT) {
      console.log(`    colorRaw: ${p.colorRaw ?? "—"}  ${p.colorErr ? "ERR:" + p.colorErr : ""}${p.colorWinner ? "  ⮜ " + p.colorWinner : ""}`);
      for (const b of p.bgRawValues ?? []) console.log(`    bgRaw: ${b}`);
    }
  }
});
