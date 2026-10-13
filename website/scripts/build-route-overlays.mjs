#!/usr/bin/env node
/**
 * Per-route translation overlays, built at the same moment as the coverage table.
 *
 * Why this script exists: a page's client components translate through the
 * i18n provider's overlay, and that overlay used to be the WHOLE locale
 * dictionary, on every page — the marketing site's own phase-5 comment calls
 * the client/server translator split the correct architecture, but the split
 * delivered "everything" to the client side. Measured on the production build:
 *
 *   /en-US/   801 KB document
 *   /de-DE/  1350 KB document  (+549 KB of serialized German dictionary)
 *   /ar-AE/  1455 KB document  (+654 KB)
 *
 * The German dictionary a /legal/privacy visitor will never reference costs
 * over half a megabyte of HTML on every one of the 25 routes.
 *
 * This script replaces "the whole dictionary" with "the sentences a visitor
 * of this route can actually see in a translated client component". How it
 * decides, in the order it decides:
 *
 *   1. Route entries. Every `page.tsx` under `app/[locale]/`, plus
 *      `not-found.tsx` and `error.tsx`, is a route. The locale layout is a
 *      route of its own (`__chrome`): the navbar, footer, studio trigger,
 *      support bubble and build stamp mount there whatever page is open.
 *
 *   2. Import closures. Starting from each entry file, the walker follows
 *      static `from "…"` and dynamic `import("…")` edges through `src/`
 *      (`@/…` and relatives, `.ts`/`.tsx`/index). What a route's client
 *      components could import is a superset of what they ever render.
 *
 *   3. Client seeds. A file carrying `"use client"` is a client boundary;
 *      everything it imports is client-reachable by definition. Only the
 *      client-reachable part of a route ships translator data — sentences a
 *      server component renders are already rendered into the markup.
 *
 *   4. Key mining. For client-reachable files the miner ports the exact
 *      t-call grammar of `scripts/i18n-extract.mjs` (the plain-call and
 *      tagged-template forms), and for data files inside the closure the
 *      value literals, since those are what lookups like `t(module.name)`
 *      actually hand the translator.
 *
 *   5. The write. `src/i18n/route-overlays.json` — for chrome and for every
 *      route, per locale, the translated subset. Untranslated keys are left
 *      out entirely: the translator's fallback for a missing key is the key
 *      itself — the English sentence — which is exactly what an untranslated
 *      key renders as today, so absence is free and byte-identical in
 *      behaviour.
 *
 * Verification, and why a subset miss here cannot ship quietly: the anchors
 * below are sentences a human WILL notice switching to English — menu labels,
 * the demo call-to-action, the studio's headline, the support bubble — and
 * if a mining or closure regression ever drops one from the set it belongs
 * in, this script fails the build before it deploys. The anchors are run
 * against the mined key sets, before the JSON is written.
 *
 * Run via `npm run i18n:keys`, which regenerates the coverage table first.
 */
import { readFileSync, writeFileSync, existsSync, readdirSync } from "node:fs";
import { dirname, join, relative } from "node:path";

const LOCALES = [
  "en-US", "ar-AE", "bn-BD", "de-DE", "es-ES", "fr-FR", "hi-IN", "id-ID",
  "ja-JP", "ko-KR", "pt-BR", "ru-RU", "tr-TR", "ur-PK", "zh-CN",
];

/* ── route entries ──────────────────────────────────────────────────── */

const LOCALE_DIR = "src/app/[locale]";

/** Route key → entry file. Home is "". */
function routeEntries() {
  const entries = new Map([["__chrome", join(LOCALE_DIR, "layout.tsx")]]);
  for (const dir of walkDirs(LOCALE_DIR)) {
    const page = join(dir, "page.tsx");
    if (existsSync(page)) {
      entries.set(relative(LOCALE_DIR, dirname(page)) === "." ? "" : relative(LOCALE_DIR, page).replace(/\/page\.tsx$/, ""), page);
    }
  }
  for (const special of ["not-found", "error"]) {
    const file = join(LOCALE_DIR, `${special}.tsx`);
    if (existsSync(file)) entries.set(`__${special}`, file);
  }
  return entries;
}

function walkDirs(root) {
  const out = [];
  const stack = [root];
  while (stack.length > 0) {
    const dir = stack.pop();
    out.push(dir);
    for (const name of readdirSync(dir, { withFileTypes: true })) {
      if (name.isDirectory()) stack.push(join(dir, name.name));
    }
  }
  return out;
}

/* ── import walker ──────────────────────────────────────────────────── */

const IMPORT_FROM = /(?:import|export)[^'"]*?from\s+["']([^"']+)["']/g;
const IMPORT_DYN = /import\(\s*["']([^"']+)["']\s*\)/g;

function resolveImport(spec, fromFile) {
  let target;
  if (spec.startsWith("@/")) target = join("src", spec.slice(2));
  else if (spec.startsWith(".")) target = join(dirname(fromFile), spec);
  else return null;
  for (const candidate of [
    `${target}.ts`, `${target}.tsx`, `${target}.mts`,
    join(target, "index.ts"), join(target, "index.tsx"),
  ]) {
    if (existsSync(candidate)) return candidate;
  }
  return null;
}

/** Every `src/` file reachable from `seeds`; node_modules never count. */
function closureOf(seeds) {
  const seen = new Set();
  const stack = [...seeds];
  while (stack.length > 0) {
    const file = stack.pop();
    if (seen.has(file) || !existsSync(file)) continue;
    seen.add(file);
    const source = readFileSync(file, "utf8");
    for (const regex of [IMPORT_FROM, IMPORT_DYN]) {
      regex.lastIndex = 0;
      for (const match of source.matchAll(regex)) {
        const resolved = resolveImport(match[1], file);
        if (resolved && !seen.has(resolved)) stack.push(resolved);
      }
    }
  }
  return seen;
}

function isClientFile(file) {
  return /^(?:\s|\/\*[^]*?\*\/|\/\/[^\n]*\n)*["']use client["']/.test(readFileSync(file, "utf8"));
}

/* ── key mining — a faithful port of i18n-extract's t-call grammar ──── */

const CALL = /\bt\(\s*(?:"((?:[^"\\]|\\.)*)"|'((?:[^'\\]|\\.)*)'|`((?:[^`\\]|\\.)*)`)/g;
const TAGGED = /\bt\.template`|\btext`/g;

/* `taggedKey`, `taggedShape`, `looksLikeCode`, `translatable` are the same
   rules the dictionary was built with — duplicating them here is deliberate:
   if the overlay miner ever narrowed relative to the dictionary miner, the
   narrowest page would start rendering English where a translation exists. */
function taggedKey(source, start) {
  let depth = 0;
  let text = "";
  let values = 0;
  for (let cursor = start; cursor < source.length; cursor += 1) {
    const character = source[cursor];
    if (character === "\\") {
      text += source[cursor + 1] ?? "";
      cursor += 1;
      continue;
    }
    if (depth === 0 && character === "`") return text;
    if (depth === 0 && character === "$" && source[cursor + 1] === "{") {
      depth = 1;
      text += `{v${values}}`;
      values += 1;
      cursor += 1;
      continue;
    }
    if (depth > 0) {
      if (character === "{") depth += 1;
      else if (character === "}") depth -= 1;
      else if (character === "`") {
        cursor += 1;
        while (cursor < source.length && source[cursor] !== "`") {
          if (source[cursor] === "\\") cursor += 1;
          cursor += 1;
        }
      }
      continue;
    }
    text += character;
  }
  return text;
}

function looksLikeCode(text) {
  if (/\n/.test(text)) return true;
  if (/=>|&&|\|\||\bconst\s+[A-Za-z_$][\w$]*\s*[=;:]|\bfunction\b\s*[\w(]/.test(text)) return true;
  if (/\b(?:import|export)\s+(?:function|const|default|\{|\*)/.test(text)) return true;
  if (/\b(?:document|window|localStorage|sessionStorage)\.(?:getElementById|querySelector|addEventListener|createElement|location|documentElement|write|open)\b/.test(text)) return true;
  if (/[A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*)*\s*=\s*[^=][^;]*;\s*$/.test(text)) return true;
  return false;
}

function translatable(text) {
  return /[\p{L}]/u.test(text) && text.length > 1;
}

const DATA_FILES = /^src\/(?:data\/[^/]+|lib\/(?:useStudio|site|schema))\.ts$/;

/*
 * For a data file in the client closure, the runtime can ask for any of its
 * values by name (`t(module.name)`), so the subset is JavaScript-string
 * literals between 2 and 160 chars that hold at least one letter — the same
 * sentences the extractor's prose heuristics flag, shaped much simpler: a
 * mined string that is not in the dictionary sits inert in the overlay, and
 * a mined string that is, is a translation a visitor can receive.
 */
const DATA_STRING = /(?<=[[(:,\n][ \t]*)(?:"((?:[^"\\\n]|\\.){1,160})"|'((?:[^'\\\n]|\\.){1,160})')/gm;

function mineKeysIn(files) {
  const keys = new Set();
  for (const file of files) {
    const source = readFileSync(file, "utf8");
    CALL.lastIndex = 0;
    for (const match of source.matchAll(CALL)) {
      const raw = match[1] ?? match[2] ?? match[3] ?? "";
      const key = raw.replace(/\\`/g, "`").replace(/\\"/g, '"').replace(/\$\{([^}]*)\}/g, (_, name) => `{${name.trim()}}`);
      if (key.trim() && !looksLikeCode(key) && translatable(key)) keys.add(key);
    }
    TAGGED.lastIndex = 0;
    for (const match of source.matchAll(TAGGED)) {
      const key = taggedKey(source, match.index + match[0].length);
      if (key.trim() && !looksLikeCode(key) && translatable(key)) keys.add(key);
    }
    if (DATA_FILES.test(file)) {
      DATA_STRING.lastIndex = 0;
      for (const match of source.matchAll(DATA_STRING)) {
        const raw = match[1] ?? match[2] ?? "";
        let value;
        try {
          value = JSON.parse(`"${raw.replace(/"/g, '\\"').replace(/\\\\/g, "\\\\")}"`);
        } catch {
          continue;
        }
        if (typeof value === "string" && /[\p{L}]/u.test(value) && /[\s.!?,;:\u2014-]/.test(value)) keys.add(value);
      }
    }
  }
  return keys;
}

/* ── dictionaries ───────────────────────────────────────────────────── */

/** `"key": "value"` pairs, one per line — the format the fill scripts write. */
function dictionary(locale) {
  const source = readFileSync(`src/i18n/dict/${locale}.ts`, "utf8");
  const entries = new Map();
  for (const match of source.matchAll(/^\s*"((?:[^"\\]|\\.)*)":\s*"((?:[^"\\]|\\.)*)",?$/gm)) {
    entries.set(JSON.parse(`"${match[1]}"`), JSON.parse(`"${match[2]}"`));
  }
  return entries;
}

/* ── anchors: the sentences a human WILL notice in English ──────────── */

const ANCHORS = {
  __chrome: [
    "Request a Demo",
    "Close support panel",
    "Theme and colour",
    "Mobile",
    "Close",
  ],
  "": ["Try it on this page"],
  contact: ["Website (leave this empty)"],
};

function assertAnchors(routeKey, keys) {
  for (const anchor of ANCHORS[routeKey] ?? []) {
    if (!keys.has(anchor)) {
      throw new Error(
        `route "${routeKey}" is missing its anchor sentence "${anchor}" — ` +
        `the overlay for this route would render it in English. The miner or ` +
        `the import walker has narrowed; fix that, not the anchor.`,
      );
    }
  }
}

/* ── generate ───────────────────────────────────────────────────────── */

const entries = routeEntries();
const routeKeySets = new Map();

{
  const chromeClosure = closureOf([join(LOCALE_DIR, "layout.tsx")]);
  const chromeSeeds = [...chromeClosure].filter(isClientFile);
  const chromeClientClosure = closureOf(chromeSeeds);
  routeKeySets.set("__chrome", mineKeysIn(chromeClientClosure));
  console.log(`chrome: ${chromeSeeds.length} client files → ${routeKeySets.get("__chrome").size} sentences`);
}

for (const [routeKey, file] of entries) {
  if (routeKey === "__chrome") continue;
  const routeClosure = closureOf([file]);
  /* Client seeds the layout already owns are still re-seeded: a set is cheap. */
  const seeds = [...routeClosure].filter(isClientFile);
  const clientClosure = closureOf(seeds);
  const keys = mineKeysIn(clientClosure);
  routeKeySets.set(routeKey, keys);
  console.log(`${routeKey || "(home)"}: ${seeds.length} client files → ${keys.size} sentences`);
}

for (const [routeKey, keys] of routeKeySets) assertAnchors(routeKey, keys);

/* Overlays, per locale: chrome, and each route UNIONED with chrome so any
   client component mounted under the page's nested provider still resolves
   the chrome sentences it shares with the layout. */
const overlays = {};
for (const locale of LOCALES) {
  if (locale === "en-US") continue; // the key IS the English sentence
  const dict = dictionary(locale);
  const translate = (keys) => {
    const out = {};
    for (const key of keys) {
      const value = dict.get(key);
      if (typeof value === "string" && value.trim() !== "") out[key] = value;
    }
    return out;
  };
  const chrome = translate(routeKeySets.get("__chrome"));
  overlays[locale] = { __chrome: chrome };
  for (const [routeKey, keys] of routeKeySets) {
    if (routeKey === "__chrome") continue;
    const merged = new Set([...keys, ...Object.keys(chrome)]);
    overlays[locale][routeKey] = translate(merged);
  }
}

const report = [];
for (const locale of LOCALES) {
  if (locale === "en-US") continue;
  const per = overlays[locale];
  const fullSize = dictionary(locale).size;
  let subsetSize = 0;
  for (const ov of Object.values(per)) subsetSize += Object.keys(ov).length;
  report.push(`${locale}: full ${fullSize} → per-route total ${subsetSize} keys`);
}
console.log("\nper-route overlay sizes (keys shipped per locale):");
for (const line of report.slice(0, 3)) console.log("  " + line);

writeFileSync("src/i18n/route-overlays.json", JSON.stringify(overlays));
console.log(`\n  written to src/i18n/route-overlays.json (${(JSON.stringify(overlays).length / 1024).toFixed(0)}K serialized)`);
