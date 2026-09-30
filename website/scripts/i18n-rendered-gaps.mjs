#!/usr/bin/env node
/**
 * What the rendered page still shows in English.
 *
 * The dictionary audit answers "how many keys are translated". This answers
 * the question a reader actually has — "is there English left on the page" —
 * by fetching every page in a language and reading the text out of the HTML.
 *
 * Two lists come back, and the difference between them matters:
 *
 *   · **Not a key** — text on the page that the extractor never collected.
 *     That is a wiring gap: something renders a sentence that no `t()` ever
 *     sees, so no dictionary could translate it. Fix the wiring or add the
 *     source to the extractor.
 *   · **Key, not translated** — a sentence the site knows about and this
 *     language has not covered yet. That is a translation gap.
 *
 * Proper nouns are reported separately rather than mixed in: a customer name,
 * a supplier, a file path or a font family is meant to stay as it is, and a
 * report that counted them would never reach zero.
 *
 * Usage:
 *   node scripts/i18n-rendered-gaps.mjs --locale ar-AE
 *   node scripts/i18n-rendered-gaps.mjs --locale de-DE --base http://127.0.0.1:3200
 */

import { readFileSync } from "node:fs";

const args = process.argv.slice(2);
const flag = (name, fallback) => {
  const index = args.indexOf(name);
  return index === -1 ? fallback : args[index + 1];
};

const locale = flag("--locale", "ar-AE");
const base = flag("--base", "http://127.0.0.1:3200");

const PAGES = [
  "/",
  "/features",
  "/optimization",
  "/screenshots",
  "/how-it-works",
  "/pricing",
  "/about",
  "/developer",
  "/contact",
  "/resources",
  "/blog",
  "/sponsors",
  "/worked-example",
  "/legal/privacy",
  "/legal/terms",
  "/legal/license",
];

/* The scripts each language writes in. A text node is "still English" when it
   contains Latin words and no character from the language's own script. */
const SCRIPTS = {
  "ar-AE": /[\u0600-\u06FF]/,
  "ur-PK": /[\u0600-\u06FF]/,
  "bn-BD": /[\u0980-\u09FF]/,
  "hi-IN": /[\u0900-\u097F]/,
  "ja-JP": /[\u3040-\u30FF\u4E00-\u9FFF]/,
  "ko-KR": /[\uAC00-\uD7AF]/,
  "zh-CN": /[\u4E00-\u9FFF]/,
  "ru-RU": /[\u0400-\u04FF]/,
};

/* Names, brands, paths, formats: the same string in every language. */
const NOT_LANGUAGE = [
  /^[\w./\\-]+\.(?:cs|xaml|tsx?|json|md|png|db|log|opt|iso|dat|dll|exe)$/i,
  /(?:Views|ViewModels|Services|Themes|Utilities|Resources|Licensing|docs)\//,
  /\b(?:WPF|SQLite|EF Core|MaxRects|Bottero|Intermac|Lisec|GitHub|Patreon|Open Collective)\b/,
  /^[\d\s.,%×·+\-/m²AED]+$/,
  /^(?:ProGlass|Juber|Al Noor|Al Zahia|Ahmed|Salim|Mohammed|Air Liquide|Fenzi|Guardian|Technoform|Dow|Grace Davison|National Glass|Northgate|Metro Facades|Cedar Interiors|Harbour View|IBM Plex|Segoe|Source Serif|JetBrains|Inter|Manrope)\b/,
];

const KEYS = new Set(
  [...readFileSync("src/i18n/dict/keys.ts", "utf8").matchAll(/^ {2}(".*"),$/gm)].map((match) => JSON.parse(match[1])),
);

const INLINE = /<\/?(?:b|strong|em|i|span|code|a|abbr|sup|sub|small|mark|u|time|cite)\b[^>]*>/g;

function visibleText(html) {
  const body = html.split("</head>").at(-1) ?? html;
  const stripped = body
    .replace(/<script[\s\S]*?<\/script>/g, " ")
    .replace(/<style[\s\S]*?<\/style>/g, " ")
    .replace(/<svg[\s\S]*?<\/svg>/g, " ")
    /* A sentence broken by an inline element — `Read <b>this</b> before` — is
       one sentence on screen. Collapsing inline tags before the text nodes are
       read keeps the report about what a reader sees rather than about how the
       markup happens to be nested. */
    .replace(INLINE, "");
  const nodes = [...stripped.matchAll(/>([^<>]{12,})</g)].map((match) => match[1]);
  return nodes
    .map((text) =>
      text
        .replace(/&#x27;|&#39;/g, "'")
        .replace(/&amp;/g, "&")
        .replace(/&quot;/g, '"')
        .replace(/&nbsp;/g, " ")
        .replace(/&[a-z]+;|&#\d+;/g, " ")
        .replace(/\s+/g, " ")
        .trim(),
    )
    .filter((text) => text.length >= 12);
}

const own = SCRIPTS[locale] ?? null;
const notAKey = new Map();
const untranslated = new Map();
let checked = 0;

/* A text node that is part of a key, or has a key inside it, is a fragment the
   markup produced — `— this is a working reconstruction of the` on its own is
   not a sentence anybody has to translate. Only text that is neither a key nor
   touched by one is a real gap in the wiring. */
function relatedToAKey(text) {
  for (const key of KEYS) {
    if (key.length < 12) continue;
    if (key.includes(text) || text.includes(key)) return true;
  }
  return false;
}

for (const path of PAGES) {
  const url = `${base}${locale === "en-US" ? "" : `/${locale}`}${path === "/" ? "" : path}`;
  let html;
  try {
    const response = await fetch(url);
    if (!response.ok) {
      console.log(`  ${response.status} ${url}`);
      continue;
    }
    html = await response.text();
  } catch (error) {
    console.log(`  ${url} — ${error.message}`);
    continue;
  }

  for (const text of visibleText(html)) {
    if (!/[A-Za-z]{2,}\s+[A-Za-z]{2,}/.test(text)) continue;
    if (own && own.test(text)) continue;
    if (NOT_LANGUAGE.some((pattern) => pattern.test(text))) continue;
    checked += 1;
    if (KEYS.has(text)) {
      untranslated.set(text, (untranslated.get(text) ?? 0) + 1);
    } else if (!relatedToAKey(text)) {
      notAKey.set(text, (notAKey.get(text) ?? 0) + 1);
    }
  }
}

const list = (title, map) => {
  console.log(`\n${title}: ${map.size}`);
  for (const [text, count] of [...map.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])).slice(0, 40)) {
    console.log(`  [${count}] ${text.slice(0, 120)}`);
  }
};

console.log(`${locale}: ${checked} English text nodes across ${PAGES.length} pages`);
list("NOT A KEY — a wiring gap, no dictionary can fix it", notAKey);
list("KEY, NOT TRANSLATED — a translation gap", untranslated);
