#!/usr/bin/env node
/**
 * Merge a translation batch into a language file.
 *
 * The batch is a tab-separated text file — `English<TAB>translation` — one
 * entry per line. The format is deliberately dull: it survives copy, paste and
 * diff, it needs no escaping beyond the quote the writer of the file already
 * has to think about, and a missing tab is a visible mistake rather than a
 * silently dropped entry.
 *
 * What this script guarantees before it writes:
 *
 *   · **Nothing is lost.** Existing entries are read first and kept, so a batch
 *     can arrive in any number of pieces and in any order.
 *   · **Every key is real.** A line whose English half is not in `keys.ts` is
 *     reported and skipped — a dictionary entry for a sentence the site never
 *     renders is dead weight that hides the real gaps.
 *   · **Nothing is empty.** A blank translation is rejected, because an empty
 *     value renders as nothing at all, which is worse than English.
 *   · **The file stays valid.** Entries are written sorted and escaped, and the
 *     file must still end with `};`.
 *
 * A batch may also be written by index — `123<TAB>translation`, where 123 is
 * the key's position in `keys.ts`. That form exists because copying a long
 * English sentence by hand is how a batch acquires a key nobody renders, and
 * because translating a numbered line is one less thing to get wrong.
 *
 * Usage:
 *   node scripts/i18n-apply.mjs de-DE /tmp/i18n/de-DE.tsv
 *   node scripts/i18n-apply.mjs de-DE /tmp/i18n/de-DE.tsv --replace
 *   node scripts/i18n-apply.mjs de-DE --index /tmp/i18n/de-DE-index.tsv
 */

import { readFileSync, writeFileSync } from "node:fs";
import { KEYS } from "../src/i18n/dict/keys.ts";

const [locale, batch, ...flags] = process.argv.slice(2);
if (!locale || !batch) {
  console.error("usage: node scripts/i18n-apply.mjs <locale> <batch.tsv> [--replace]");
  process.exit(1);
}
const replace = flags.includes("--replace");
const byIndex = flags.includes("--index");
const file = `src/i18n/dict/${locale}.ts`;

/* ── read what is there ─────────────────────────────────────────────── */

const source = readFileSync(file, "utf8");
const cut = source.lastIndexOf("\n};");
if (cut === -1) {
  console.error(`${file} does not end with }; — refusing to write`);
  process.exit(1);
}
const head = source.slice(0, cut);
const body = source.slice(cut);

const existing = new Map();
const line = /^\s*(?:"((?:[^"\\]|\\.)*)"|'((?:[^'\\]|\\.)*)')\s*:\s*(?:"((?:[^"\\]|\\.)*)"|'((?:[^'\\]|\\.)*)')\s*,\s*$/gm;
for (const match of head.matchAll(line)) {
  const key = match[1] !== undefined ? JSON.parse(`"${match[1]}"`) : JSON.parse(`'${match[2]}'`);
  const value = match[3] !== undefined ? JSON.parse(`"${match[3]}"`) : JSON.parse(`'${match[4]}'`);
  existing.set(key, value);
}

/* ── read the batch ─────────────────────────────────────────────────── */

const known = new Set(KEYS);
const additions = new Map();
const unknown = [];
let blank = 0;

for (const raw of readFileSync(batch, "utf8").split("\n")) {
  if (!raw.trim() || raw.startsWith("#")) continue;
  const tab = raw.indexOf("\t");
  if (tab === -1) {
    console.error(`  no tab: ${raw.slice(0, 60)}`);
    continue;
  }
  const first = raw.slice(0, tab).trim();
  const key = byIndex ? KEYS[Number(first)] : first;
  const value = raw.slice(tab + 1).trim();
  if (byIndex && key === undefined) {
    console.error(`  no key at index ${first}`);
    continue;
  }
  if (!key) continue;
  if (!value) {
    blank += 1;
    continue;
  }
  if (!known.has(key)) {
    unknown.push(key);
    continue;
  }
  if (existing.has(key) && !replace) continue;
  additions.set(key, value);
}

for (const [key, value] of additions) existing.set(key, value);

/* ── write ──────────────────────────────────────────────────────────── */

const sorted = [...existing.entries()].sort(([a], [b]) => a.localeCompare(b, "en"));
const entries = sorted
  .map(([key, value]) => `  ${JSON.stringify(key)}: ${JSON.stringify(value)},`)
  .join("\n");

const next = `${head.slice(0, head.indexOf("\n  "))}\n${entries}${body}`;
writeFileSync(file, next.endsWith("\n") ? next : `${next}\n`);

console.log(
  `${locale}: +${additions.size} (${existing.size} entries)${blank ? `, ${blank} blank skipped` : ""}${unknown.length ? `, ${unknown.length} unknown` : ""}`,
);
for (const key of unknown.slice(0, 10)) console.log(`  not in keys.ts: ${key.slice(0, 70)}`);
