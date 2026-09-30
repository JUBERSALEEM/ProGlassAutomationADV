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

/**
 * Code that leaked into a quoted span, not a sentence.
 *
 * This filter used to reject any span containing a semicolon, and any span
 * containing the word "window." or "document." — which sounds careful and was
 * in fact the single largest reason sentences on this site stayed English. A
 * semicolon is ordinary punctuation in an English sentence ("it is cut,
 * arrised; it is toughened"), and "the delivery note document." is a noun with
 * a full stop. Sentences rejected here never become keys, so no dictionary
 * could ever translate them, however complete it was.
 *
 * The test is now about shape rather than vocabulary: a statement, an arrow
 * function, a DOM call or a declaration is code; a span with a semicolon in
 * the middle of prose is prose.
 */
function looksLikeCode(text) {
  if (/\n/.test(text)) return true;
  /* `import` and `export` are ordinary nouns in this domain — "BOQ import",
     "CNC export" — so only the statement forms are rejected. */
  if (/=>|&&|\|\||\bconst\b|\bfunction\b/.test(text)) return true;
  if (/\b(?:import|export)\s+(?:function|const|default|\{|\*)/.test(text)) return true;
  /* A real DOM or storage call, not the word "document." at the end of one. */
  if (/\b(?:document|window|localStorage|sessionStorage)\.(?:getElementById|querySelector|addEventListener|createElement|location|documentElement|write|open)\b/.test(text)) return true;
  /* `name = value;` — an assignment statement dressed as a sentence. */
  if (/[A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*)*\s*=\s*[^=][^;]*;\s*$/.test(text)) return true;
  return false;
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
  /^M[\d.]+[\s\d]/,                                           // SVG path data
];

/*
 * Names are not language.
 *
 * A customer, a project, a person, a supplier, a font family or a file path is
 * the same string in every language — it is a proper noun or an identifier
 * that happens to be printed, and translating it would be wrong in the way
 * that matters most: it would stop matching the record it came from. The list
 * below is curated, because no rule can tell "Al Noor Glazing Contracting LLC"
 * from "Delivery Note", and the audit should count only the second one.
 */
const NAMES = new Set([
  "Ahmed Rahman", "Air Liquide", "Al Gurg Glass", "Al Noor Glazing Contracting LLC",
  "Al Noor Glazing Contracting LLC · AZT-POD-2026", "Al Zahia Tower — Podium Glazing",
  "Al Zahia, Sharjah, UAE", "Cedar Interiors", "Demo User", "Dow Chemical",
  "Fenzi Gulf", "Grace Davison", "Guardian Glass", "Harbour View Tower",
  "Juber Saleem", "Metro Facades", "National Glass", "Northgate Contracting",
  "Salim Khan", "Sharjah, UAE", "Technoform Gulf", "Bottero", "Intermac", "Lisec",
  "Arial / Helvetica", "Courier New", "Segoe UI Variable", "Times New Roman",
  "Trebuchet MS", "ProGlass AI", "ProGlass Setup", "Windows", "SQLite",
]);

/** A path into the repository, or a list of them: an address, not a sentence. */
const REPO_PATH = /(?:^|\s|\()(?:Views|ViewModels|Services|Themes|Utilities|Licensing|Resources|docs|Models)\/|[\w-]+\.(?:cs|xaml|tsx?|json|md|png)\b/;

const keys = new Set();
/* Which source contributed a key — used by the work plan below, not by the
   dictionary. Literal keys are the chrome and the page copy; data keys are the
   rows behind the galleries, guides and examples. */
const origin = new Map();

function translatable(value) {
  const text = value.trim();
  if (!text) return false;
  if (NAMES.has(text)) return false;
  if (REPO_PATH.test(text)) return false;
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
 * A handful of strings are keys by decision rather than by shape: the values of
 * the Step-4 option arrays — a glass colour, a treatment, a polish. They are
 * single words, which the general rules skip on purpose (one lowercase word is
 * a field name, not a sentence), and they are rendered through `t()` by the
 * build-type selects, so the dictionary has to be able to hold them.
 *
 * They are listed here, explicitly, so that adding a colour to the application
 * is a one-line change in two places rather than a silent English option in the
 * middle of a translated dropdown.
 */
const OPTION_VALUES = [
  "Clear", "Bronze", "Grey", "Low-E", "Frosted",
  "FT", "Annealed", "Heat Soaked", "Polished",
  "Normal Mill Aluminium", "Black Aluminium", "Warm Edge (Super Spacer)", "Black Warm Edge", "Stainless Steel", "TGI Spacer",
];

/*
 * A quoted span in a data file is only a value when something puts it there:
 * it follows `:`, `[`, `,` or `(`, and it ends before `,`, `}`, `]`, `)` or the
 * end of the line. Without both halves of that test, the closing quote of one
 * string and the opening quote of the next become a "string" of their own —
 * `"bold" ? 700 : 400;` reads as prose, and a dictionary key called
 * ` ? 700 : 400;` is how you find out.
 */
const LITERAL = /(?<=[:\[,(][ \t]*(?:\n[ \t]*)?)(?:"((?:[^"\\\n]|\\.)*)"|'((?:[^'\\\n]|\\.)*)')\s*(?=[,}\])]|as const|$)/gm;

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

/* ── prose in page and component literals ─────────────────────────────
 *
 * The site says a great deal through values it holds in its own files: the
 * step list on the home page, the paragraph arrays on the legal pages, the
 * column descriptions inside a surface. Those render through `t()` at the
 * point of use — `{t(detail)}` — but a `t(variable)` call cannot be read
 * statically, and the earlier passes only looked at `t("literal")` calls and
 * at `src/data/*.ts`. Everything held in a page or a component was therefore
 * invisible: wired, rendered through `t()`, and impossible to translate
 * because no key existed for it.
 *
 * This pass reads them by shape. A quoted value that follows `:` `[` `,` `(`
 * and ends before `,` `}` `]` `)` `as const` or the end of the line is a value;
 * it is prose when it has a space, carries a capital letter and is not a
 * class-name list — the last guard is what keeps `mt-0.5 text-xs leading-none`
 * out of the dictionary, and it works because a styling string has no capitals
 * and a sentence has at least one.
 */
const PROSE_IN_TYPES = /\.tsx?$/;
const CLASSISH = /^[a-z0-9\s:.\-\[\]/%(),!]+$/;

for await (const file of walk("src")) {
  if (!PROSE_IN_TYPES.test(file)) continue;
  if (file.startsWith("src/data/")) continue;
  const source = readFileSync(file, "utf8");
  for (const match of source.matchAll(LITERAL)) {
    const value = (match[1] ?? match[2] ?? "").replace(/\\"/g, '"').replace(/\\'/g, "'");
    if (value.length < 8 || value.length > 400) continue;
    if (!/\s/.test(value) || !/[A-Z]/.test(value)) continue;
    if (CLASSISH.test(value)) continue;
    /* A colour or a CSS paint function is a value, not a sentence — the
       capital-letter rule above lets `radial-gradient(circle, #22D3EE33, …)`
       through, because a hex colour has capitals in it. */
    if (/#[0-9a-fA-F]{3,8}|\b(?:radial|linear|conic)-gradient\(|\brgba?\(|\bhsla?\(/.test(value)) continue;
    if (isContent(value) && translatable(value)) {
      keys.add(value);
      if (!origin.has(value)) origin.set(value, "table");
    }
  }
}

/* ── tables inside components ─────────────────────────────────────────
 *
 * Not all prose is a `t("…")` call or a row in `src/data`. The navigation
 * draws its own table of entries, the rail its own groups, the appearance
 * preview its own family names — module-level literals inside a component
 * file, read at the point of rendering (`{t(child.description)}`) and
 * therefore invisible to both scans above.
 *
 * They are found by the property they are stored under. A string is prose
 * here when it is the value of a property the site reads into text — `label`,
 * `description`, `detail`, `evidence` and the rest — which is also why a class
 * name is never picked up: `className` is not in the list, and neither is
 * anything else that carries styling.
 */
const PROSE_PROPERTIES =
  "label|title|description|detail|summary|evidence|note|body|heading|hint|caption|question|answer|name|category|status|step|job|text|tagline|blurb|benefit|outcome|purpose|reason|alt|placeholder|eyebrow|kicker|quote|author|role|deliverable|feature|gap|prompt|why|term|meaning|spec|subtitle|value|colour|treatment|polish|panelExtra|panes|long";
const INLINE_PROPERTY = new RegExp(`\\b(?:${PROSE_PROPERTIES})\\s*:\\s*(?:"((?:[^"\\\\]|\\\\.)*)"|'((?:[^'\\\\]|\\\\.)*)')`, "gm");

/* A style rule is a list of lowercase tokens and punctuation, not a sentence. */
function isStyling(value) {
  const tokens = value.split(/\s+/);
  if (tokens.length < 2) return false;
  if (!tokens.every((token) => /^[a-z0-9:.[\]()/%#_-]+$/.test(token) && !/[A-Z]/.test(token))) return false;
  /* A style rule is a list of class names, and a class name almost always
     carries a dash, a colon or a bracket. `hidden items-center gap-0.5 …` is
     styling; `Business at a glance` is not. */
  const classy = tokens.filter((token) => /[-:[\]]/.test(token)).length;
  return classy >= tokens.length / 2;
}

/* The first function in a file: everything above it is a table the component
   reads, which is where a map of titles or a list of navigation rows lives. */
function moduleScope(text) {
  const match = /^(?:export\s+)?(?:default\s+)?function\s+[A-Za-z_$]/m.exec(text);
  return match ? text.slice(0, match.index) : "";
}

for await (const file of walk("src")) {
  if (DATA_FILES.test(file)) continue;
  const source = readFileSync(file, "utf8");

  /* A string stored under a property the site reads as text. */
  for (const match of source.matchAll(INLINE_PROPERTY)) {
    const value = (match[1] ?? match[2] ?? "").replace(/\\"/g, '"').replace(/\\'/g, "'");
    if (!isContent(value) || !translatable(value)) continue;
    keys.add(value);
    if (!origin.has(value)) origin.set(value, "table");
  }

  /* A string in a lookup table — `Overview: "Business at a glance"` — where
     the key names the row rather than the kind of text it is. Only the module
     scope is scanned, because inside a component a bare string is as likely to
     be a class name as a sentence, and styling is not language. */
  for (const match of moduleScope(source).matchAll(/(?::|^\s*)[ \t]*(?:"((?:[^"\\\n]|\\.)*)")/gm)) {
    const value = (match[1] ?? "").replace(/\\"/g, '"');
    if (!isContent(value) || !translatable(value) || isStyling(value)) continue;
    keys.add(value);
    if (!origin.has(value)) origin.set(value, "table");
  }
}

/* ── write ──────────────────────────────────────────────────────────── */

for (const value of OPTION_VALUES) {
  keys.add(value);
  if (!origin.has(value)) origin.set(value, "data");
}

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
  const counts = ["literal", "data", "table"].map(
    (kind) => `${plan.filter((row) => row.source === kind).length} ${kind}`,
  );
  console.log(`work plan: /tmp/proglass-i18n-plan.json (${counts.join(", ")})`);
}
