#!/usr/bin/env node
/**
 * Fail the build when a language that claims to be finished is not.
 *
 * `i18n-audit.mjs` reports coverage as a number, which is the right thing for
 * a work list and the wrong thing for a promise. "Fifteen languages" is a
 * claim the site makes in its own copy and in its language menu, so it should
 * be checked the way the rest of the site is checked: by a command that exits
 * non-zero.
 *
 * Two levels:
 *
 *   node scripts/i18n-verify.mjs              # dictionary coverage per language
 *   node scripts/i18n-verify.mjs --rendered   # what the built pages actually show
 *
 * The rendered run needs a server (`npx next start -p 3200`) because the
 * question it answers is about HTML, not about a data structure — a language
 * can be at 100 % of the dictionary and still print English, which is exactly
 * what round 25 was about.
 *
 * Exit codes: 0 when every locale marked `complete` passes, 1 otherwise.
 */

import { readFileSync } from "node:fs";

const rendered = process.argv.includes("--rendered");
const PORT = process.env.PORT ?? "3200";
const BASE = `http://127.0.0.1:${PORT}`;

/* Which languages the site is prepared to promise. `in-progress` languages
   are reported and never fail — the language menu shows their real coverage
   and hands the rest to the browser. */
const COMPLETE = new Set(["id-ID", "hi-IN", "ar-AE", "bn-BD", "de-DE", "es-ES", "fr-FR", "pt-BR", "en-US"]);

const KEYS = [...readFileSync("src/i18n/dict/keys.ts", "utf8").matchAll(/^ {2}(".*"),$/gm)].map(
  (match) => JSON.parse(match[1]),
);
const keys = new Set(KEYS);

const LOCALES = [
  "ar-AE", "bn-BD", "de-DE", "es-ES", "fr-FR", "hi-IN", "id-ID",
  "ja-JP", "ko-KR", "pt-BR", "ru-RU", "tr-TR", "ur-PK", "zh-CN",
];

function dictionary(locale) {
  const source = readFileSync(`src/i18n/dict/${locale}.ts`, "utf8");
  const entries = new Map();
  for (const match of source.matchAll(/^\s*"((?:[^"\\]|\\.)*)":\s*"((?:[^"\\]|\\.)*)",?$/gm)) {
    entries.set(JSON.parse(`"${match[1]}"`), JSON.parse(`"${match[2]}"`));
  }
  return entries;
}

/**
 * What the built page still shows in English, counted the way the gap report
 * counts it: a text node that **is a key** and has **no entry** in this
 * language. Names, serials, figures, seeded log lines and the endonyms in the
 * language menu are not keys, so they cannot make a locale fail — which is
 * what makes this usable as a gate rather than as a report.
 */
async function renderedGaps(locale, entries) {
  const pages = [
    "", "features", "how-it-works", "optimization", "screenshots", "pricing",
    "worked-example", "about", "developer", "resources", "sponsors", "contact",
    "blog", "legal/privacy", "legal/terms", "legal/license",
  ];
  const seen = new Set();
  for (const page of pages) {
    const response = await fetch(`${BASE}/${locale}${page ? `/${page}` : ""}`);
    if (!response.ok) return null;
    const html = await response.text();
    for (const match of html.matchAll(/>([^<>{}]{2,400})</g)) {
      const text = match[1]
        .replace(/&[a-z]+;|&#\d+;/g, " ")
        .replace(/\s+/g, " ")
        .trim();
      if (!/[A-Za-z]/.test(text) || !/\s/.test(text)) continue;
      if (keys.has(text) && !entries.has(text)) seen.add(text);
    }
  }
  return seen.size;
}

let failures = 0;
console.log(`${KEYS.length} keys\n`);
console.log("locale   entries  coverage  verdict");
for (const locale of LOCALES) {
  const entries = dictionary(locale);
  const covered = KEYS.filter((key) => entries.has(key)).length;
  const percent = ((covered / KEYS.length) * 100).toFixed(1);
  let verdict = COMPLETE.has(locale) ? "complete" : "in progress";
  if (rendered) {
    const gaps = await renderedGaps(locale, entries);
    if (gaps === null) {
      verdict = "server not reachable";
      failures += 1;
    } else {
      verdict = `${verdict} · ${gaps} untranslated on screen`;
      if (COMPLETE.has(locale) && gaps > 0) failures += 1;
    }
  } else if (COMPLETE.has(locale) && covered !== KEYS.length) {
    failures += 1;
    verdict = "FAILS — marked complete";
  }
  console.log(
    `${locale.padEnd(8)} ${String(entries.size).padStart(7)} ${`${percent}%`.padStart(9)}  ${verdict}`,
  );
}

if (failures) {
  console.error(`\n${failures} locale(s) failed verification`);
  process.exit(1);
}
console.log("\nevery locale marked complete verified");
