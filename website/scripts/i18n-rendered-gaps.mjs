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

/*
 * German, Spanish, French, Indonesian, Portuguese and Turkish are written in
 * the same alphabet as English, so "no Arabic character in it" proves nothing.
 * For those, English is recognised by its own words — a sentence with none of
 * `the`, `and`, `with`, `your`, `from`… in it is not English, however little of
 * it a reader of the language would recognise.
 */
const ENGLISH_TELLS =
  /\b(?:the|and|with|for|your|from|this|that|these|those|every|each|you|they|their|there|when|which|what|where|how|into|without|than|then|is|are|were|has|have|had|not|all|any|both|same|other|only|also|about|above|below|between|against|before|after|over|under|again|because|while|during|until)\b/i;
const LATIN = new Set(["en-US", "de-DE", "es-ES", "fr-FR", "id-ID", "pt-BR", "tr-TR"]);
function looksEnglish(text) {
  if (LATIN.has(locale)) return ENGLISH_TELLS.test(text);
  const script = SCRIPTS[locale];
  return script ? !script.test(text) : true;
}

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

/* An inline element is a word separator when it closes: `</span><span>` would
   otherwise glue two headings together into "DashboardSheet Store". */
const INLINE = /<\/?(?:b|strong|em|i|span|code|a|abbr|sup|sub|small|mark|u|time|cite)\b[^>]*>/g;
const INLINE_CLOSE = /<\/(?:b|strong|em|i|span|code|a|abbr|sup|sub|small|mark|u|time|cite)>/g;

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
    .replace(INLINE_CLOSE, " ")
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
const joined = new Map();
let checked = 0;

/*
 * Markup splits a sentence: `<Link>Dashboard</Link> <Link>Sheet Store</Link>`
 * arrives here as one node reading "Dashboard Sheet Store", which is not a key
 * and would otherwise be reported as a wiring gap for ever. If a node can be
 * taken apart into two or more keys with nothing but separators in between, it
 * is those keys rendered side by side — real text, already translatable — and
 * not a gap at all.
 */
const SEPARATOR = /^[\s·—–→>.,:;()\[\]/\\|+%&#\u00a0-]+/;
/*
 * Every string this language is allowed to be reading, beyond the English keys:
 * the site's own dictionary values and the application's vocabulary. A node
 * that is not a key but can be taken apart into whole keys, whole dictionary
 * values and whole application values has no English in it that a dictionary
 * could reach — the markup has simply printed three known words next to each
 * other, and reporting it as a gap would be an alarm that never clears.
 */
function phraseSet() {
  const phrases = new Set(KEYS);
  const add = (value) => {
    const text = `${value}`.trim();
    if (text.length >= 3) phrases.add(text);
  };
  try {
    const dict = readFileSync(`src/i18n/dict/${locale}.ts`, "utf8");
    for (const match of dict.matchAll(/^\s*"(?:[^"\\]|\\.)*":\s*"((?:[^"\\]|\\.)*)",?$/gm)) {
      add(JSON.parse(`"${match[1]}"`));
    }
  } catch {
    /* no dictionary for this language yet — the English keys are the vocabulary */
  }
  for (const file of [`src/i18n/app-strings/${locale}.json`, "src/i18n/app-strings/en-US.json"]) {
    try {
      for (const value of Object.values(JSON.parse(readFileSync(file, "utf8")))) add(value);
    } catch {
      /* not generated yet */
    }
  }
  return [...phrases].filter((phrase) => phrase.length >= 3).sort((a, b) => b.length - a.length);
}

const PHRASES = phraseSet();

function segmentsIntoKeys(text) {
  let rest = text;
  let found = 0;
  while (rest.length) {
    rest = rest.replace(SEPARATOR, "");
    if (!rest.length) break;
    let best = null;
    for (const phrase of PHRASES) {
      if (rest.startsWith(phrase) && (best === null || phrase.length > best.length)) best = phrase;
    }
    if (best === null) return rest.trim().length < 12 && found >= 2;

    rest = rest.slice(best.length);
    found += 1;
  }
  return found >= 2;
}

/* A text node that is part of a key, or has a key inside it, is a fragment the
   markup produced — `— this is a working reconstruction of the` on its own is
   not a sentence anybody has to translate. Only text that is neither a key nor
   touched by one is a real gap in the wiring. */
/*
 * Fragments, not sentences: a paragraph with a value spliced into it renders as
 * several text nodes, and one of them may be `: one row per glass line, priced
 * at the specification's rate, or at the` — a piece of a key, split where the
 * placeholder was filled. Every key is therefore compared by its literal runs
 * as well as whole: if a fragment sits inside the wording around a placeholder,
 * the sentence it belongs to is already a key and no dictionary is missing.
 */
const LITERAL_RUNS = (() => {
  const runs = [];
  for (const key of KEYS) {
    if (!key.includes("{")) continue;
    for (const run of key.split(/\{[a-zA-Z0-9_]+\}/)) {
      const text = run.trim();
      if (text.length >= 12) runs.push(text);
    }
  }
  return runs;
})();

function relatedToAKey(text) {
  for (const key of KEYS) {
    if (key.length < 12) continue;
    if (key.includes(text) || text.includes(key)) return true;
  }
  for (const run of LITERAL_RUNS) {
    if (run.includes(text) || text.includes(run)) return true;
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
    /*
     * A sentence the site knows, printed unchanged: that is English whatever
     * words it happens to use, and the tell-word test must not hide it. The
     * test is only for deciding whether a node that is *not* a key is English
     * or merely shares the alphabet with it.
     */
    if (!KEYS.has(text) && !looksEnglish(text)) continue;
    if (own && own.test(text)) continue;
    if (NOT_LANGUAGE.some((pattern) => pattern.test(text))) continue;
    checked += 1;
    if (KEYS.has(text)) {
      untranslated.set(text, (untranslated.get(text) ?? 0) + 1);
    } else if (segmentsIntoKeys(text)) {
      joined.set(text, (joined.get(text) ?? 0) + 1);
    } else if (!relatedToAKey(text)) {
      notAKey.set(text, (notAKey.get(text) ?? 0) + 1);
    }
  }
}

/* `--dump` writes the two lists to a file, ordered by how many pages show the
   text — which is also the order they are worth translating in: a sentence the
   navigation prints on every page is worth more than one paragraph on one. */
const dump = flag("--dump", null);
if (dump) {
  const { writeFileSync } = await import("node:fs");
  const sorted = (map) => [...map.entries()].sort((a, b) => b[1] - a[1]).map(([text, count]) => ({ text, count }));
  writeFileSync(
    dump,
    `${JSON.stringify(
      { locale, notAKey: sorted(notAKey), untranslated: sorted(untranslated), joined: sorted(joined) },
      null,
      1,
    )}\n`,
  );
  console.log(`written to ${dump}`);
}

const list = (title, map) => {
  console.log(`\n${title}: ${map.size}`);
  for (const [text, count] of [...map.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])).slice(0, 40)) {
    console.log(`  [${count}] ${text.slice(0, 120)}`);
  }
};

console.log(`${locale}: ${checked} English text nodes across ${PAGES.length} pages`);
list("NOT A KEY — a wiring gap, no dictionary can fix it", notAKey);
list("SEVERAL KEYS TOGETHER — markup glued them, not a gap", joined);
list("KEY, NOT TRANSLATED — a translation gap", untranslated);
