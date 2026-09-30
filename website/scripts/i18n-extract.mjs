#!/usr/bin/env node
/**
 * Collect every sentence the site can render, and write them to `dict/keys.ts`.
 *
 * The site is keyed by the English sentence, so the list of sentences is the
 * list of work: what a dictionary has to cover, and what the audit checks. It
 * is generated rather than maintained, because the one thing a hand-kept list
 * of keys does is go stale on the day someone adds a sentence.
 *
 * Two sources, because the site renders text from two places:
 *
 * **Call sites.** `t("Request a Demo")` in a component, including the template
 * form — `` t(`{count} languages`, { count }) `` is stored with the placeholder
 * intact, which is what the dictionary translates and `fill` substitutes into.
 *
 * **Data files.** A sentence in `src/data/*.ts` is rendered through `t()` at
 * the point it is read (`{module.detail}`), so it never appears in a call
 * site. Those strings are collected here, filtered: a data file is also full
 * of slugs, ids, CSS sizes and enum names, and none of those are sentences.
 * The filter is deliberately conservative in one direction — a stray single
 * word costs one empty line in an audit table, whereas a missed sentence is a
 * piece of the site that stays in English for every reader.
 *
 * Usage: node scripts/i18n-extract.mjs [--list]
 */

import { readFileSync, writeFileSync } from "node:fs";
import { readdir, stat } from "node:fs/promises";
import path from "node:path";

const DATA_FILES = /^src\/(?:data\/[^/]+|lib\/(?:useStudio|site|schema))\.ts$/;

/* Things that look like a sentence and are not one. */
const NOT_PROSE =
  /^(?:https?:|\/|#|data:|\d|\.|--|@)|\.(?:tsx?|json|css|svg|png|webp|woff2?)$|^[a-z0-9-]+$|^[A-Z0-9_]+$|[{}<>]|=>|&&|\|\||\bpx\b|\bvar\(|\$\{|api\/|\/api/i;

/** Code that leaked into a quoted span, not a sentence. */
function looksLikeCode(text) {
  return /[\n;]|=>|&&|\|\||\bwindow\.|\bdocument\.|\bconst\b|\bfunction\b/.test(text);
}

/* A CSS font stack is not a sentence, and neither is a media query. */
const CSS_VALUE = /^(?:\(|["']?[A-Za-z ]+["']?,\s*)+(?:serif|sans-serif|monospace|system-ui)$|prefers-|^\(|^#[0-9a-fA-F]{3,8}$/;

function isContent(value) {
  const text = value.trim();
  if (text.length < 2 || text.length > 400) return false;
  if (CSS_VALUE.test(text)) return false;
  if (looksLikeCode(text)) return false;
  if (NOT_PROSE.test(text)) return false;
  /* A sentence either contains a space or starts with a capital and is not a
     lone identifier — "Pricing" is a heading, "progressPercent" is a field. */
  if (!/\s/.test(text) && !/^[A-Z][a-z]+$/.test(text)) return false;
  return true;
}

async function* walk(dir) {
  for (const entry of await readdir(dir)) {
    if (entry.startsWith(".") || entry === "node_modules" || entry === "dict" || entry === "app-strings") continue;
    const full = path.join(dir, entry);
    const info = await stat(full);
    if (info.isDirectory()) yield* walk(full);
    else if (/\.tsx?$/.test(entry)) yield full;
  }
}

/*
 * Some things on the page are not language.
 *
 * A code line, a config setting, a file-format list, a font stack, a currency
 * code or a log banner is the same in every language — it is an identifier
 * that happens to be printed. Those are excluded from the key list rather than
 * translated, because a translated identifier stops matching the thing it
 * names, and because counting them as "untranslated" would make the coverage
 * report lie in the other direction.
 */
const NOT_TRANSLATABLE = [
  /^[A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*)+\.?/,          // AppConfig.FontStyleName
  /^[\w.]+\s*=\s*/,                                          // FontUnderline = true
  /^(?:DXF|PDF|Excel|CSV|SQLite|SGU|DGU|LAM|OPT|ISO|DAT)\b.*\b(?:·|\.)/, // format lists
  /[,.]\s*(?:serif|sans-serif|monospace|system-ui)\b/,       // font stacks
  /^\(|^═══|^\.[A-Z]|\.(?:tsx?|json|cs|xaml)\b/,            // CSS, banners, filenames
  /^(?:AED|USD|EUR|GBP)\s?[\d.,]+$/,                          // money as a symbol
  /^\{[A-Za-z0-9_, ]+\}$/,                                    // bare {placeholder}
];

const keys = new Set();
/* Which source contributed a key — used by the work plan below, not by the
   dictionary. Literal keys are the chrome and the page copy; data keys are the
   rows behind the galleries, guides and examples. */
const origin = new Map();

function translatable(value) {
  const text = value.trim();
  if (!text) return false;
  return !NOT_TRANSLATABLE.some((pattern) => pattern.test(text));
}

/* ── call sites ─────────────────────────────────────────────────────── */

const CALL = /\bt\(\s*(?:"((?:[^"\\]|\\.)*)"|'((?:[^'\\]|\\.)*)'|`((?:[^`\\]|\\.)*)`)/g;

for await (const file of walk("src")) {
  if (file.startsWith("src/data/")) continue;
  const source = readFileSync(file, "utf8");
  for (const match of source.matchAll(CALL)) {
    const raw = match[1] ?? match[2] ?? match[3] ?? "";
    /* `${count}` and `${count} languages` → `{count} languages`: the dictionary
       translates the sentence with its placeholder, not the value. */
    const key = raw.replace(/\\`/g, "`").replace(/\\"/g, '"').replace(/\$\{([^}]*)\}/g, (_, name) => `{${name.trim()}}`);
    if (key.trim() && !looksLikeCode(key) && translatable(key)) {
      keys.add(key);
      if (!origin.has(key)) origin.set(key, "literal");
    }
  }
}

/* ── data files ─────────────────────────────────────────────────────── */

/*
 * A quoted span in a data file is only a value when something puts it there:
 * it follows `:`, `[`, `,` or `(`, and it ends before `,`, `}`, `]`, `)` or the
 * end of the line. Without both halves of that test, the closing quote of one
 * string and the opening quote of the next become a "string" of their own —
 * `"bold" ? 700 : 400;` reads as prose, and a dictionary key called
 * ` ? 700 : 400;` is how you find out.
 */
const LITERAL = /(?<=[:\[,(]\s{0,3})(?:"((?:[^"\\\n]|\\.)*)"|'((?:[^'\\\n]|\\.)*)')\s*(?=[,}\])]|as const|$)/gm;

for await (const file of walk("src")) {
  if (!DATA_FILES.test(file)) continue;
  const source = readFileSync(file, "utf8");
  for (const match of source.matchAll(LITERAL)) {
    const value = (match[1] ?? match[2] ?? "").replace(/\\"/g, '"').replace(/\\'/g, "'");
    if (isContent(value) && translatable(value)) {
      keys.add(value);
      if (!origin.has(value)) origin.set(value, "data");
    }
  }
}

/* ── write ──────────────────────────────────────────────────────────── */

const sorted = [...keys].sort((a, b) => a.localeCompare(b, "en"));

if (process.argv.includes("--list")) {
  for (const key of sorted) console.log(key);
  console.log(`${sorted.length} keys`);
} else {
  const body = sorted.map((key) => `  ${JSON.stringify(key)},`).join("\n");
  const file = `/**
 * Every sentence the site renders through \`t()\`.
 *
 * Generated by \`scripts/i18n-extract.mjs\` from the source itself — do not edit
 * by hand. Regenerate with \`npm run i18n:keys\`, and check coverage with
 * \`npm run i18n:audit\`.
 *
 * ${sorted.length} keys.
 */
export const KEYS: string[] = [
${body}
];

export type DictKey = (typeof KEYS)[number];
`;
  writeFileSync("src/i18n/dict/keys.ts", file);

  /* The work plan. Translation batches address keys by number, so the numbers
     have to come from the same run that wrote `keys.ts` — otherwise a batch is
     written against a list that has since shifted, and the entries land on the
     wrong sentences. */
  const plan = sorted.map((key, index) => ({ index, key, source: origin.get(key) ?? "literal" }));
  writeFileSync("/tmp/proglass-i18n-plan.json", `${JSON.stringify(plan, null, 1)}\n`);
  console.log(`keys: ${sorted.length} written to src/i18n/dict/keys.ts`);
  console.log(`work plan: /tmp/proglass-i18n-plan.json (${plan.filter((row) => row.source === "literal").length} literal, ${plan.filter((row) => row.source === "data").length} data)`);
}
