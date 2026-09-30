#!/usr/bin/env node
/**
 * Wire the shared components into the translator.
 *
 * The pages were wired first (`i18n-wire.mjs`); this is the other half — the
 * navbar, the footer, the studio, the galleries, the surfaces. Two shapes of
 * component need two different treatments, and the difference matters:
 *
 *   **Client components** (they already say `"use client"`) get the translator
 *   from the provider: `const t = useT();` at the top of each exported
 *   component, and their visible text wrapped in `t("…")`.
 *
 *   **Server components** get it as a prop. They render on the server, where
 *   the page already has a translator, and passing the function down as a prop
 *   keeps them on the server — which matters: converting the footer or a hero
 *   into a client component would ship every word of those components to every
 *   browser, to translate text the server had already rendered.
 *
 * A component that is already wired is left exactly as it is: this runs on a
 * tree that has been half-wired before, and it has to be safe to run twice.
 *
 * Run:  node scripts/i18n-wire-components.mjs [--check]
 */
import { readFileSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const website = join(here, "..");
const components = join(website, "src", "components");
const appDir = join(website, "src", "app", "[locale]");

const check = process.argv.includes("--check");

/** Attributes whose value a visitor reads. */
const PROSE_ATTRIBUTES = [
  "title",
  "description",
  "eyebrow",
  "lead",
  "aria-label",
  "placeholder",
  "heading",
  "caption",
  "empty",
  "body",
  "note",
  "label",
];

/** Object keys inside a component's own data whose value a visitor reads. */
const PROSE_KEYS = [
  "label",
  "title",
  "value",
  "detail",
  "body",
  "text",
  "note",
  "caption",
  "question",
  "answer",
  "status",
  "unit",
  "kind",
  "tag",
];

const SKIP_FILES = [
  "i18n/provider",
  "components/ui/LocaleLink",
  "components/seo/RouteSchema",
  "components/brand/SvgDefs",
  /* A brand mark is not a sentence: "ProGlass Automation" is the same words in
     every language, and the logo's own default subtitle is part of the lockup. */
  "components/brand/Logo",
];

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
  if (/^[\d.,\s]+(?:m²|mm|kg|%|×|x)?$/.test(text)) return false;
  /* A single bare word is usually a technical key, a CSS-ish token or a
     field name (`Sheet`, `None`, `Primary`). The exceptions are handled by
     hand where they matter, and the dict falls back to English otherwise. */
  if (!/\s/.test(text) && !/^[A-Z][a-z]+$/.test(text)) return false;
  return true;
}

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

/* ── wrapping ──────────────────────────────────────────────────────── */

/**
 * A line of text on its own, where the line above it closed a tag.
 *
 * This is line-based rather than regex-over-the-whole-file on purpose. A
 * previous attempt matched `>text<` anywhere in the file and produced garbage
 * in half a dozen components — it walked into `/>` inside a `<path>` and into
 * a `>` inside a comparison. The shape this site actually writes is one text
 * node per line, and that is what this recognises.
 */
const TEXT_LINE = /^(\s*)([A-Za-z][^<>{}"`]*[A-Za-z,.!?…])\s*$/;
const NOT_TEXT = /[=;()\[\]]|!==|===|&&|\|\||=>|^\s*\/\//;

function wrapTextLines(text, log) {
  const lines = text.split("\n");
  let inComment = false;
  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i];
    if (/\/\*/.test(line)) inComment = true;
    if (inComment) {
      if (/\*\//.test(line)) inComment = false;
      continue;
    }
    if (/^\s*\/\//.test(line)) continue;
    const match = TEXT_LINE.exec(line);
    if (!match) continue;
    const body = match[2].trim();
    if (NOT_TEXT.test(body)) continue;
    if (!isProse(body)) continue;
    const previous = [...lines.slice(0, i)].reverse().find((entry) => entry.trim() !== "");
    if (!previous) continue;
    const closesTag = previous.trim().endsWith(">") || previous.trim().endsWith(")}") || previous.trim().endsWith('{" "}');
    if (!closesTag) continue;
    log.push(`line “${body.slice(0, 40)}”`);
    lines[i] = `${match[1]}{t(${JSON.stringify(body)})}`;
  }
  return lines.join("\n");
}

function wrapAttributes(text, log) {
  const names = PROSE_ATTRIBUTES.join("|");
  const pattern = new RegExp(`\\b(${names})=("((?:[^"\\\\]|\\\\.)*)")`, "g");
  return text.replace(pattern, (whole, name, quoted, value) => {
    if (!isProse(value)) return whole;
    log.push(`${name}="${value.slice(0, 36)}"`);
    return `${name}={t(${JSON.stringify(value)})}`;
  });
}

function wrapObjectText(text, log) {
  const keys = PROSE_KEYS.join("|");
  const pattern = new RegExp(`\\b(${keys}):\\s*("((?:[^"\\\\]|\\\\.)*)")`, "g");
  return text.replace(pattern, (whole, key, quoted, value) => {
    if (!isProse(value)) return whole;
    log.push(`${key}: "${value.slice(0, 36)}"`);
    return `${key}: t(${JSON.stringify(value)})`;
  });
}


/**
 * Where the components start.
 *
 * Everything above the first function declaration in the file is module scope:
 * tables of wording, navigation entries, banner geometry. Those are read by a
 * component, not rendered by one, and they have no translator in scope — a
 * `t()` there is a syntax-valid bug that only shows up when the build runs. So
 * the wrapping passes work strictly below this line, and anything already
 * wrapped above it is unwrapped again.
 */
function componentRegionStart(text) {
  const match = /^(?:export\s+)?(?:default\s+)?function\s+[A-Za-z_$]/m.exec(text);
  return match ? match.index : 0;
}

function unwrapModuleScope(text, log) {
  const cut = componentRegionStart(text);
  if (cut === 0) return text;
  const head = text.slice(0, cut);
  const tail = text.slice(cut);
  const unwrapped = head.replace(/\bt\(("(?:[^"\\]|\\.)*")\)/g, (whole, literal) => {
    log.push(`module scope ${literal.slice(0, 34)}`);
    return literal;
  });
  return unwrapped + tail;
}

/* ── installing the translator ─────────────────────────────────────── */

/** The index just inside an exported component's body. */
function bodyStart(text, from) {
  let depth = 0;
  for (let i = from; i < text.length; i += 1) {
    const ch = text[i];
    if (ch === "(") depth += 1;
    else if (ch === ")") depth -= 1;
    else if (ch === "{" && depth === 0) return i + 1;
  }
  return -1;
}

/** Add `const t = useT();` to every exported component that needs it. */
function installHook(text, log) {
  if (/\bconst\s*\{[^}]*\bt\b[^}]*\}\s*=\s*useI18n\(/.test(text)) return text;

  /*
   * The import goes in first, and the hooks are then found by scanning the
   * text *as it now is*. Scanning first and inserting after is the bug that
   * mangled half a dozen components: the import shifts every later offset by
   * its own length, and the hook lands in the middle of a parameter list.
   */
  if (!/from "@\/i18n\/provider"/.test(text)) {
    const last = [...text.matchAll(/^import .*;$/gm)].pop();
    if (!last) return text;
    const at = last.index + last[0].length;
    text = `${text.slice(0, at)}\nimport { useT } from "@/i18n/provider";${text.slice(at)}`;
  }

  const exported = /export (?:default )?function ([A-Za-z_$][\w$]*)\s*(?:<[^>]*>)?\s*\(/g;
  const inserts = [];
  let match;
  while ((match = exported.exec(text))) {
    const open = text.indexOf("(", match.index);
    let depth = 0;
    let i = open;
    for (; i < text.length; i += 1) {
      if (text[i] === "(") depth += 1;
      else if (text[i] === ")") {
        depth -= 1;
        if (depth === 0) break;
      }
    }
    const body = bodyStart(text, i + 1);
    if (body < 0) continue;
    if (/^\s*const t = useT\(\);/.test(text.slice(body, body + 40))) continue;
    inserts.push(body);
  }
  if (!inserts.length) return text;
  for (const at of inserts.reverse()) {
    text = `${text.slice(0, at)}\n  const t = useT();${text.slice(at)}`;
  }
  log.push(`useT ×${inserts.length}`);
  return text;
}

/** The `t` prop, for a component that has to stay on the server. */
function installProp(text, log) {
  if (/\bt\??:\s*Translator\b/.test(text)) return text;
  if (!/from "@\/i18n\/core"/.test(text)) {
    const last = [...text.matchAll(/^import .*;$/gm)].pop();
    if (!last) return text;
    const at = last.index + last[0].length;
    text = `${text.slice(0, at)}\nimport type { Translator } from "@/i18n/core";${text.slice(at)}`;
  }
  const pattern = /export function ([A-Za-z_$][\w$]*)\s*\(\s*\{/g;
  const matches = [...text.matchAll(pattern)];
  for (const match of matches.reverse()) {
    const at = match.index + match[0].length;
    text = `${text.slice(0, at)}\n  t,${text.slice(at)}`;
  }
  /* …and the type of that destructured parameter. */
  const typed = /(\}: \{\n)(?=  [a-zA-Z])/g;
  text = text.replace(/(\}:\s*\{)/g, (whole) => `${whole}\n  /** The page's translator — this component renders on the server. */\n  t: Translator;`);
  log.push(`t prop ×${matches.length}`);
  return text;
}


/* ── the call sites ─────────────────────────────────────────────────
 *
 * A server component that needs the translator takes it as a prop, so every
 * place that renders it has to hand one over. That is a mechanical edit too —
 * and it is the edit that keeps the component on the server, which is the
 * whole point of doing it this way.
 */
function addPropToUsages(text, names, log) {
  for (const name of names) {
    const open = new RegExp(`<${name}(?=[\\s/>])`, "g");
    const inserts = [];
    let match;
    while ((match = open.exec(text))) {
      const start = match.index + match[0].length;
      let depth = 0;
      let quote = null;
      let i = start;
      for (; i < text.length; i += 1) {
        const ch = text[i];
        if (quote) {
          if (ch === quote) quote = null;
          continue;
        }
        if (ch === '"' || ch === "'" || ch === "`") quote = ch;
        else if (ch === "{") depth += 1;
        else if (ch === "}") depth -= 1;
        else if (ch === ">" && depth === 0) break;
      }
      const span = text.slice(start, i);
      if (/\bt=\{t\}/.test(span)) continue;
      if (/\{\.\.\./.test(span)) continue;
      inserts.push(start);
    }
    if (inserts.length) log.push(`${name} ×${inserts.length}`);
    for (const at of inserts.reverse()) text = `${text.slice(0, at)}\n      t={t}${text.slice(at)}`;
  }
  return text;
}

/** Which components now ask for a translator as a prop. */
function componentsWantingT() {
  const names = [];
  for (const file of walk(components)) {
    const text = readFileSync(file, "utf8");
    if (!/\bt\??:\s*Translator\b/.test(text)) continue;
    for (const match of text.matchAll(/export function ([A-Za-z_$][\w$]*)/g)) names.push(match[1]);
  }
  return [...new Set(names)].sort();
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

if (process.argv.includes("--callers")) {
  const names = componentsWantingT();
  const appFiles = [...walk(components), ...walk(appDir)];
  let touched = 0;
  for (const file of appFiles) {
    const before = readFileSync(file, "utf8");
    const log = [];
    const after = addPropToUsages(before, names, log);
    if (after === before) continue;
    touched += 1;
    if (check) console.log(`needs t={t}: ${relative(website, file)} — ${log.join(", ")}`);
    else {
      writeFileSync(file, after);
      console.log(`${String(log.length).padStart(4)}  ${relative(website, file)}  ${log.join(", ")}`);
    }
  }
  console.log(check ? `${touched} files still to update` : `${touched} call sites updated`);
  process.exit(0);
}

const files = walk(components).sort();
let edits = 0;
let changed = 0;

for (const file of files) {
  const rel = relative(website, file).split("\\").join("/");
  if (SKIP_FILES.some((skip) => rel.includes(skip))) continue;
  const before = readFileSync(file, "utf8");
  const isClient = /^\s*"use client";/.test(before);

  const log = [];
  let text = before;
  if (isClient) {
    text = installHook(text, log);
  }
  const wantsProp = !isClient && /function [A-Z]/.test(before);
  text = unwrapModuleScope(text, log);
  const cut = componentRegionStart(text);
  const head = text.slice(0, cut);
  text = head + wrapAttributes(text.slice(cut), log);
  text = head + wrapTextLines(text.slice(cut), log);
  const cut2 = componentRegionStart(text);
  text = text.slice(0, cut2) + wrapObjectText(text.slice(cut2), log);
  /* The prop comes last, and only when the wrapping passes found something:
     a component with nothing to translate should not make every call site
     pass it a translator it never uses. */
  if (wantsProp && /\bt\(/.test(text) && !/\bt\??:\s*Translator\b/.test(text)) {
    text = installProp(text, log);
  }

  /* A wrapping pass inside a server component with no translator to call is a
     bug, not a translation: refuse to write it. */
  const needsTranslator = /\bt\(/.test(text);
  if (needsTranslator && !isClient && !/\bt\??:\s*Translator\b/.test(text)) {
    console.log(`skipped (server component, no t prop): ${rel}`);
    continue;
  }
  if (text === before) continue;
  changed += 1;
  edits += log.length;
  if (check) {
    console.log(`needs wiring: ${rel} (${log.length})`);
  } else {
    writeFileSync(file, text);
    console.log(`${String(log.length).padStart(4)}  ${rel}${isClient ? "" : "  (t prop)"}`);
  }
}

console.log(check ? `${changed} components still to wire` : `${edits} edits across ${changed} components`);
