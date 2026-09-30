#!/usr/bin/env node
/**
 * Wire raw JSX text into the dictionary.
 *
 * `i18n-extract.mjs` can only find prose that reaches `t()` — a literal call,
 * a value in `src/data`, or a quoted value in a page. It cannot see the third
 * form the site uses, which is the oldest one:
 *
 *     <p className="eyebrow">Built up, not guessed at</p>
 *
 * That is a heading. It is rendered by the JSX compiler as a text child, it
 * is not a key, and it therefore stays English in all fifteen languages no
 * matter how complete the dictionary is. A sweep of the source found 142 of
 * them, and they are not spread evenly: they are concentrated in things a
 * reader notices first — section eyebrows, card headings, link labels, button
 * text ("Built up, not guessed at", "Meet the developer", "The most expensive
 * line is not the largest quantity.").
 *
 * This script rewrites those nodes as `{t("…")}` so that the extractor sees
 * them and the dictionaries can hold them.
 *
 * It is deliberately narrow, because it edits source:
 *
 *   • only a single-line text child, with no braces, operators, quotes,
 *     entities, brackets or numbers-as-code in it;
 *   • only text that reads as prose — it has a space or it is Title Case,
 *     and it is not an all-caps code, a class-name list or a bare number;
 *   • only files that already translate something, so `t` is in scope;
 *   • anything it is unsure about is left alone and listed.
 *
 * Usage: node scripts/i18n-wire-text.mjs [--write]
 */

import { readFileSync, writeFileSync } from "node:fs";
import { readdir, stat } from "node:fs/promises";
import path from "node:path";

const write = process.argv.includes("--write");

async function* walk(dir) {
  for (const entry of await readdir(dir)) {
    if (entry.startsWith(".") || entry === "node_modules") continue;
    const full = path.join(dir, entry);
    const info = await stat(full);
    if (info.isDirectory()) yield* walk(full);
    else if (/\.tsx$/.test(entry)) yield full;
  }
}

/* A text child: after a `>`, before a `<`, with no tag or expression in it. */
const TEXT_NODE = />([^<>{}]+)</g;

/* Things that are language, so nothing here is a class list. */
const CLASSISH = /^[a-z0-9\s:.\-\[\]/%(),!&]+$/;
/* One word, or an all-caps token: an identifier, a unit or a code. */
const CODEY = /^(?:[A-Z0-9][A-Z0-9+./·\s×-]*|\d[\d.,\s]*|[a-z0-9-]+)$/;
/* A file that is not itself translatable, or that is deliberately English. */
const SKIP_FILE = /(?:LanguageMenu|LocaleLink|Logo|language-menu)\.tsx$/;

function prose(text) {
  /* A text child can span lines in the source; a key is one line. */
  const value = text.trim().replace(/\s+/g, " ");
  if (value.length < 4 || value.length > 220) return null;
  if (!/[A-Za-z]/.test(value)) return null;
  if (CLASSISH.test(value)) return null;
  if (CODEY.test(value)) return null;
  /* Nothing that could be code, markup or an entity — this edits source. */
  if (/[{}<>"`;=()[\]&\\$]|=>|&&|\|\||\/\/|\/\*/.test(value)) return null;
  if (!/\s/.test(value) && !/^[A-Z][a-z]+$/.test(value)) return null;
  /* A measurement or a seeded figure: more digits than letters. */
  const letters = (value.match(/[A-Za-z]/g) ?? []).length;
  const digits = (value.match(/\d/g) ?? []).length;
  if (digits >= letters) return null;
  if (letters < 3) return null;
  /* A type specimen or an abbreviation: every token is three characters or
     fewer ("Aa Bb 0123", "W1 × H1", "SQM"). */
  const tokens = value.split(/\s+/);
  if (tokens.every((token) => token.replace(/[^A-Za-z0-9]/g, "").length <= 3)) return null;
  if (/\b(?:px|rem|em|vh|vw|var|calc|className|useState|useEffect|return|const|function)\b/.test(value)) return null;
  if (/^[a-z]/.test(value) && !/^[a-z]+ [a-z]/.test(value)) return null;
  return value;
}

const wired = [];

for await (const file of walk("src")) {
  if (SKIP_FILE.test(file)) continue;
  const source = readFileSync(file, "utf8");
  /* `t` has to be reachable from this module before this file is touched. */
  if (!/\bt\(|useT\(|:\s*\w*[Tt]ranslator|translatorFor/.test(source)) continue;

  let changed = false;
  const next = source.replace(TEXT_NODE, (match, text) => {
    const value = prose(text);
    if (!value) return match;
    /* Marked not-translatable on purpose (seeded data, brand, endonyms). */
    const context = source.slice(Math.max(0, source.indexOf(match) - 400), source.indexOf(match));
    if (/translate="no"|notranslate|NoTranslate/.test(context)) return match;
    /* An entity would have to survive the round trip through a Go string;
       leave those to a human rather than guess at the decoding. */
    const line = source.slice(0, source.indexOf(match)).split("\n").length;
    if (line > 0) {
      wired.push({ file, line, value });
      changed = true;
      const before = match.slice(0, 1);
      const after = match.slice(-1);
      return `${before}{t(${JSON.stringify(value)})}${after}`;
    }
    return match;
  });
  if (changed && write) writeFileSync(file, next);
}

const byFile = new Map();
for (const row of wired) {
  if (!byFile.has(row.file)) byFile.set(row.file, 0);
  byFile.set(row.file, byFile.get(row.file) + 1);
}
console.log(`${wired.length} text nodes to wire across ${byFile.size} files`);
for (const [file, count] of [...byFile].sort((a, b) => b[1] - a[1])) {
  console.log(`  ${String(count).padStart(3)}  ${file}`);
}
if (!write) {
  console.log("\n--dry run-- list:");
  for (const row of wired) console.log(`  ${row.file}:${row.line}  ${row.value}`);
  console.log("\nre-run with --write to apply");
}
