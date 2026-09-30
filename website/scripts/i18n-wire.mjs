#!/usr/bin/env node
/**
 * Put every page under the translator.
 *
 * The site was written in one language by one author, so its sentences sit
 * directly in the markup. This codemod moves them — mechanically, and only the
 * mechanical parts — into the translator, and does the four things a page needs
 * in order to exist in fifteen languages:
 *
 *   1. **A language comes from the route.** The page and its `generateMetadata`
 *      both receive `params`, and both start by asking for a translator.
 *   2. **Its title and description are translated.** They are read by a search
 *      engine and a chat client, which is exactly where a translated page
 *      matters most: a German result that opens an English page is a bounce.
 *   3. **Its links keep the reader's language.** `next/link` becomes
 *      `LocaleLink`, which applies the address prefix in one place.
 *   4. **Its visible text goes through `t()`.** Headings, paragraphs, captions,
 *      labels, placeholders and the `label:`/`value:` pairs in the data blocks
 *      the pages carry.
 *
 * What it deliberately does not touch: className strings, hrefs, ids, icon
 * names, code paths, numbers and anything with a brace in it. A mechanical
 * rewrite that misfires here is worse than a manual pass, so the grammar is
 * conservative and the script prints what it changed.
 *
 * Run:  node scripts/i18n-wire.mjs [--check]
 */
import { readFileSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const website = join(here, "..");
const appDir = join(website, "src", "app", "[locale]");

const check = process.argv.includes("--check");

/* ── what gets wrapped ─────────────────────────────────────────────── */

/** JSX attributes whose value a visitor reads. */
const PROSE_ATTRIBUTES = [
  "title",
  "description",
  "eyebrow",
  "lead",
  "aria-label",
  "placeholder",
  "alt",
  "heading",
  "caption",
  "empty",
  "body",
  "note",
];

/** Object-literal keys inside a page's data blocks whose value is read. */
const PROSE_KEYS = [
  "label",
  "title",
  "value",
  "detail",
  "body",
  "text",
  "name",
  "summary",
  "note",
  "caption",
  "question",
  "answer",
  "status",
  "unit",
  "stage",
  "tag",
  "kind",
];

/** Text that must not be wrapped: identifiers, code, units, brand marks. */
function isProse(value) {
  const text = value.trim();
  if (text.length < 3) return false;
  if (!/[A-Za-z]{2}/.test(text)) return false;
  if (/[<>{}$`\\]/.test(text)) return false;
  if (/&[a-z]+;|&#\d+;/.test(text)) return false;
  if (/^(https?:|mailto:|\/|#)/.test(text)) return false;
  if (/^(className|href|src|id|role|viewBox|data-|aria-|xmlns|key)/.test(text)) return false;
  if (/^[a-z0-9.\-\s:]+$/.test(text)) return false;
  if (/\.(tsx?|mjs|cs|xaml|png|svg|woff2|css|json)\b/.test(text)) return false;
  if (/^[A-Z0-9_]{1,8}$/.test(text)) return false;
  /* A Tailwind class list is space-separated lower-case tokens, most of which
     carry a dash or a colon. A lower-case sentence has neither. */
  const tokens = text.split(/\s+/);
  if (tokens.every((token) => /^[a-z0-9:/\][.%-]+$/.test(token)) && tokens.some((token) => /[-:[\]]/.test(token))) {
    return false;
  }
  /* Anything that looks like a measurement, a version or a range rather than a
     sentence. `3.84 m²`, `2.0.11`, `1,000`, `0.5 m²`. */
  if (/^[\d.,\s]+(?:m²|mm|kg|%|×|x)?$/.test(text)) return false;
  return true;
}

/** Every span of the file that is already inside a `t(…)` call. */
function translatedSpans(text) {
  const spans = [];
  const call = /(?<![A-Za-z0-9_$.])t\s*\(/g;
  let match;
  while ((match = call.exec(text))) {
    let depth = 0;
    let index = match.index + match[0].length - 1;
    for (; index < text.length; index += 1) {
      const ch = text[index];
      if (ch === "(") depth += 1;
      else if (ch === ")") {
        depth -= 1;
        if (depth === 0) break;
      }
    }
    spans.push([match.index, index + 1]);
  }
  return spans;
}

const insideAny = (spans, start, end) => spans.some(([a, b]) => start >= a && end <= b);

/* ── the passes ────────────────────────────────────────────────────── */

function wrapJsxText(text, log) {
  return text.replace(/(?<![=-])(>)([^<>{}\n]*[A-Za-z]{2}[^<>{}\n]*)(<)/g, (whole, open, body, close, offset) => {
    const trimmed = body.trim();
    if (!trimmed) return whole;
    if (/[()=;:,"`]/.test(trimmed)) return whole;
    if (!isProse(trimmed)) return whole;
    if (insideAny(translatedSpans(text), offset, offset + whole.length)) return whole;
    log.push(`text “${trimmed.slice(0, 48)}”`);
    const leading = body.slice(0, body.indexOf(trimmed));
    const trailing = body.slice(body.indexOf(trimmed) + trimmed.length);
    return `${open}${leading}{t(${JSON.stringify(trimmed)})}${trailing}${close}`;
  });
}

/** A paragraph written across several source lines is one sentence on screen. */
function wrapMultilineText(text, log) {
  return text.replace(
    /(?<![=-])(>)\s*\n\s*([A-Z][^<>{}\n]*(?:\n\s*[^<>{}\n]+)*)\s*\n\s*(<)/g,
    (whole, open, body, close) => {
      const collapsed = body.replace(/\s*\n\s*/g, " ").trim();
      if (/[()=;:,"`]/.test(collapsed)) return whole;
      if (!isProse(collapsed)) return whole;
      log.push(`para “${collapsed.slice(0, 48)}”`);
      return `${open}\n          {t(${JSON.stringify(collapsed)})}\n        ${close}`;
    },
  );
}

function wrapAttributes(text, log) {
  const names = PROSE_ATTRIBUTES.join("|");
  const pattern = new RegExp(`\\b(${names})=("((?:[^"\\\\]|\\\\.)*)")`, "g");
  return text.replace(pattern, (whole, name, quoted, value) => {
    if (!isProse(value)) return whole;
    log.push(`${name}="${value.slice(0, 40)}"`);
    return `${name}={t(${JSON.stringify(value)})}`;
  });
}

/** `{ label: "Editions", value: "5 (Trial → Enterprise)" }` in a page's data. */
function wrapObjectText(text, log) {
  const keys = PROSE_KEYS.join("|");
  const pattern = new RegExp(`\\b(${keys}):\\s*("((?:[^"\\\\]|\\\\.)*)")`, "g");
  return text.replace(pattern, (whole, key, quoted, value) => {
    if (!isProse(value)) return whole;
    log.push(`${key}: "${value.slice(0, 40)}"`);
    return `${key}: t(${JSON.stringify(value)})`;
  });
}

/* ── the four structural changes ───────────────────────────────────── */

function swapLinkImport(text, log) {
  if (!/import Link from "next\/link";/.test(text)) return text;
  log.push("next/link → LocaleLink");
  return text.replace(
    /import Link from "next\/link";/,
    'import { LocaleLink as Link } from "@/components/ui/LocaleLink";',
  );
}

function addImports(text, needsSchema) {
  const imports = ['import { translatorFor } from "@/i18n/server";'];
  if (!/from "@\/i18n\/server"/.test(text)) {
    const last = [...text.matchAll(/^import .*;$/gm)].pop();
    if (last) {
      const at = last.index + last[0].length;
      text = `${text.slice(0, at)}\n${imports.join("\n")}${text.slice(at)}`;
    }
  }
  if (needsSchema && !/import type \{ Locale \}/.test(text)) {
    /* `locale` travels as `t.locale`, typed by the translator itself — nothing
       extra to import. Kept for symmetry with the components that need it. */
  }
  return text;
}

/**
 * `export const metadata = pageMetadata({…})` becomes a `generateMetadata`
 * function that can read the route's language.
 */
function convertMetadata(text, log) {
  const start = text.indexOf("export const metadata");
  if (start === -1) return text;
  const open = text.indexOf("pageMetadata({", start);
  if (open === -1) return text;

  /* The call ends at the first `});` that closes the object — the pages always
     write it at the start of a line, which is what makes this safe. */
  const bodyStart = open + "pageMetadata({".length;
  const bodyEnd = text.indexOf("\n});", bodyStart);
  if (bodyEnd === -1) return text;
  let body = text.slice(bodyStart, bodyEnd);
  const tail = text.slice(bodyEnd + "\n});".length);

  /* Strings inside the metadata object: the title, the description, the share
     eyebrow and the keyword list are all read by a person or a crawler. */
  body = body.replace(/\b(title|ogEyebrow|ogAccent)\?*\s*:\s*"((?:[^"\\]|\\.)*)"/g, (whole, key, value) => {
    if (key === "ogAccent") return whole;
    if (!isProse(value)) return whole;
    log.push(`metadata ${key}`);
    return `${key}: t(${JSON.stringify(value)})`;
  });
  body = body.replace(/\b(description)\?*\s*:\s*\n?\s*"((?:[^"\\]|\\.)*)"/g, (whole, key, value) => {
    if (!isProse(value)) return whole;
    log.push("metadata description");
    return `${key}: t(${JSON.stringify(value)})`;
  });
  body = body.replace(/\bkeywords\?*\s*:\s*\[([\s\S]*?)\]/, (whole, list) => {
    const items = list.replace(/^\s*\[/, "")
      .split(",")
      .map((entry) => entry.trim())
      .filter(Boolean)
      .map((entry) => {
        const match = entry.match(/^"((?:[^"\\]|\\.)*)"$/);
        if (!match || !isProse(match[1])) return entry;
        log.push(`keyword “${match[1].slice(0, 32)}”`);
        return `t(${JSON.stringify(match[1])})`;
      });
    return `keywords: [${items.join(", ")}]`;
  });

  const replacement = `type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,${body}
  });
}`;
  log.push("metadata → generateMetadata");
  return text.slice(0, start) + replacement + tail;
}

/** `export default function XPage() {` → `async … ({ params })` + translator. */
function convertPageFunction(text, log) {
  const pattern = /export default function ([A-Za-z_$][\w$]*)\s*\(\s*\)\s*\{/;
  const match = text.match(pattern);
  if (!match) return text;
  const usesSchema = /<RouteSchema/.test(text);
  const inject = `export default async function ${match[1]}({ params }: PageProps) {\n  const t = await translatorFor(params);${
    usesSchema ? "\n  const locale = t.locale;" : ""
  }`;
  log.push(`page “${match[1]}” → async`);
  return text.replace(pattern, inject);
}

/** The structured-data node needs to know which language's URLs to write. */
function wireRouteSchema(text, log) {
  if (!/<RouteSchema/.test(text)) return text;
  return text.replace(/<RouteSchema(?![^>]*\blocale=)/g, () => {
    log.push("RouteSchema locale");
    return "<RouteSchema\n      locale={locale}";
  });
}

/* ── run ───────────────────────────────────────────────────────────── */

function walk(dir, out = []) {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) walk(full, out);
    else if (entry.endsWith(".tsx")) out.push(full);
  }
  return out;
}

const files = walk(appDir).sort();
let totalEdits = 0;
let changed = 0;

for (const file of files) {
  const before = readFileSync(file, "utf8");
  const rel = relative(website, file).split("\\").join("/");
  const isPage = /page\.tsx$/.test(rel);
  const isSpecial = /(not-found|error|loading)\.tsx$/.test(rel);
  if (!isPage && !isSpecial) continue;
  if (/translatorFor\(/.test(before)) continue;

  const log = [];
  let text = before;
  text = addImports(text, /<RouteSchema/.test(text));
  if (isPage) {
    text = swapLinkImport(text, log);
    text = convertMetadata(text, log);
    text = convertPageFunction(text, log);
    text = wireRouteSchema(text, log);
  }
  text = wrapAttributes(text, log);
  text = wrapMultilineText(text, log);
  text = wrapJsxText(text, log);
  text = wrapObjectText(text, log);

  if (text === before) continue;
  changed += 1;
  totalEdits += log.length;
  if (check) {
    console.log(`needs wiring: ${rel} (${log.length})`);
  } else {
    writeFileSync(file, text);
    console.log(`${String(log.length).padStart(4)} edits  ${rel}`);
  }
}

console.log(
  check ? `${changed} pages still to wire` : `${totalEdits} edits across ${changed} page files`,
);
