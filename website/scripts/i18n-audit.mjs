#!/usr/bin/env node
/**
 * How much of the site speaks each language.
 *
 * The translator resolves a sentence in three steps — the site's dictionary,
 * the application's own language file, then English — so the honest measure of
 * coverage is the percentage of keys that resolve to something other than the
 * English key, whether that translation was written here or inherited from the
 * application.
 *
 * Two numbers per language, because they answer two different questions:
 *
 *   · `own` — entries in `src/i18n/dict/<locale>.ts`. This is the work this
 *     repository has done.
 *   · `total` — `own` plus the keys the application's language file already
 *     covers. This is what a reader sees. A module name, a stage, a document
 *     type or a base rate is translated the moment the site adopts the
 *     application's word for it, and it will never disagree with the software.
 *
 * `--gaps` writes the sentences still resolving to English to
 * `/tmp/proglass-i18n-gaps.json`, in the order they are listed in `keys.ts`,
 * ready to be worked through in batches.
 *
 * Usage:
 *   node scripts/i18n-audit.mjs            # the table
 *   node scripts/i18n-audit.mjs --gaps     # the table and the gap file
 */

import { readFileSync, writeFileSync, existsSync } from "node:fs";
import { LOCALE_CODES, DEFAULT_LOCALE } from "../src/i18n/locales.ts";
import { KEYS } from "../src/i18n/dict/keys.ts";

const here = new URL("..", import.meta.url).pathname;
const APP_INDEX = "/tmp/proglass-app-strings";

function ownEntries(locale) {
  const file = `${here}src/i18n/dict/${locale}.ts`;
  const source = readFileSync(file, "utf8");
  const body = source.slice(source.indexOf("const dict: Overlay = {") + 23);
  const cut = body.indexOf("\n};");
  const text = cut === -1 ? body : body.slice(0, cut);
  const entries = new Map();
  const entry = /^\s*(?:"((?:[^"\\]|\\.)*)"|'((?:[^'\\]|\\.)*)')\s*:\s*(?:"((?:[^"\\]|\\.)*)"|'((?:[^'\\]|\\.)*)')\s*,?\s*$/gm;
  for (const match of text.matchAll(entry)) {
    let key = match[1] ?? match[2] ?? "";
    let value = match[3] ?? match[4] ?? "";
    try {
      if (match[1]) key = JSON.parse(`"${match[1]}"`);
      if (match[3]) value = JSON.parse(`"${match[3]}"`);
    } catch {
      /* A malformed line is reported by the compiler, not here. */
    }
    entries.set(key, value);
  }
  return entries;
}

function appIndex(locale) {
  const file = `${APP_INDEX}/${locale}.json`;
  if (!existsSync(file)) return {};
  return JSON.parse(readFileSync(file, "utf8"));
}

/* A dictionary may hold an entry for a sentence that is no longer rendered —
   that is what happens when a page is rewritten. Only the entries whose key is
   still in the list count as coverage, so the report cannot be flattered by
   dead wood. */
const KEY_SET = new Set(KEYS);

const gaps = {};
const rows = [];

for (const locale of LOCALE_CODES) {
  const own = new Map([...ownEntries(locale)].filter(([key]) => KEY_SET.has(key)));
  const app = appIndex(locale);
  let inherited = 0;
  const missing = [];
  for (const key of KEYS) {
    if (own.has(key)) continue;
    if (app[key]) inherited += 1;
    else if (locale !== DEFAULT_LOCALE) missing.push(key);
  }
  const total = own.size + inherited;
  const percent = KEYS.length === 0 ? 100 : Math.round((total / KEYS.length) * 1000) / 10;
  rows.push({ locale, own: own.size, inherited, total, percent, missing: missing.length });
  gaps[locale] = missing;
}

const width = Math.max(...rows.map((row) => row.locale.length));
console.log(`${"locale".padEnd(width)}  own   app   total  keys    %`);
for (const row of rows) {
  console.log(
    `${row.locale.padEnd(width)}  ${String(row.own).padStart(4)}  ${String(row.inherited).padStart(4)}  ${String(row.total).padStart(5)}  ${String(KEYS.length).padStart(5)}  ${row.percent.toFixed(1).padStart(5)}`,
  );
}

if (process.argv.includes("--gaps")) {
  writeFileSync("/tmp/proglass-i18n-gaps.json", `${JSON.stringify(gaps, null, 1)}\n`);
  const unique = new Set(Object.values(gaps).flat());
  console.log(`\ngaps written to /tmp/proglass-i18n-gaps.json — ${unique.size} sentences still in English`);
}
