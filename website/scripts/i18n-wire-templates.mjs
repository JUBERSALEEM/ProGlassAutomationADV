#!/usr/bin/env node
/**
 * Give sentences that carry values inside them a key.
 *
 * A sentence built with a template literal —
 *
 *     `No row here is under the floor: the smallest panel is ${smallest} m².`
 *
 * — renders as three text nodes, and the translator only ever sees the two
 * fragments around the value. No dictionary can hold the sentence, so a
 * language that orders its clauses differently cannot translate it at all; the
 * fragments that do get keys are translated word-for-word and end up reading
 * like the machine output they are.
 *
 * The fix is one token: the literal becomes a *tagged* template.
 *
 *     t.template`No row here is under the floor: the smallest panel is ${smallest} m².`
 *
 * `t.template` (see `src/i18n/core.ts`) builds the key with the values replaced
 * by `{v0}`, `{v1}`, in order, looks that up and puts the values back into the
 * translated sentence. The sentence is one key, the placeholder moves wherever
 * the target language needs it, and an untranslated template renders exactly
 * what it rendered before.
 *
 * This script performs that edit mechanically, because the alternative is a
 * person remembering to do it for every sentence and getting it right every
 * time. It only touches a literal that reads as prose, judged by the same rules
 * the extractor uses — a class list, a path, a URL, a key name or a unit string
 * is left alone.
 *
 * Usage:
 *   node scripts/i18n-wire-templates.mjs            # dry run, lists the sites
 *   node scripts/i18n-wire-templates.mjs --write    # rewrite the files
 */

import { readFileSync, writeFileSync } from "node:fs";
import { readdir, stat } from "node:fs/promises";
import path from "node:path";

const write = process.argv.includes("--write");

/* ── the extractor's prose rules, mirrored ───────────────────────────────
 * Deliberately a copy of the tests in `scripts/i18n-extract.mjs` rather than a
 * shared import: the extractor is the file that decides what a key is. A
 * migration that quietly disagreed with it would tag literals the extractor
 * ignores and leave the dictionary short of keys the renderer asks for.
 */
const NOT_PROSE =
  /^(?:https?:|\/|#|data:|\.{1,2}\/|--|@)|\.(?:tsx?|json|css|svg|png|webp|woff2?)$|[{}]|<[A-Za-z/!]|=>|&&|\|\||\bpx\b|\bvar\(|\$\{|api\/|\/api/i;
const CSS_VALUE =
  /^(?:\(|["']?[A-Za-z ]+["']?,\s*)+(?:serif|sans-serif|monospace|system-ui)$|prefers-|^\(|^#[0-9a-fA-F]{3,8}$/;
const NOT_TRANSLATABLE = [
  /^[A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$])+\.?\s*(?:[=:]|$)/,
  /^[\w.]+\s*=\s*/,
  /[,.]\s*(?:serif|sans-serif|monospace|system-ui)\b/,
  /^\(|^═══|^\.[A-Za-z]+$|\.(?:tsx?|json|cs|xaml)\b/,
  /^(?:AED|USD|EUR|GBP)\s?[\d.,]+$/,
  /^\{[A-Za-z0-9_, ]+\}$/,
  /^M[\d.]+[\s\d]/,
];
const REPO_PATH =
  /(?:^|\s|\()(?:Views|ViewModels|Services|Themes|Utilities|Licensing|Resources|docs|Models)\/|[\w-]+\.(?:cs|xaml|tsx?|json|md|png)\b/;
const CLASSISH = /^[a-z0-9\s:.\-\[\]/%(),!]+$/;
const FORMAT_TOKENS = /\b(?:DXF|PDF|Excel|CSV|SQLite|SGU|DGU|LAM|OPT|ISO|DAT)\b/g;

function looksLikeCode(text) {
  if (/\n/.test(text)) return false; // a multi-line sentence is still a sentence
  if (/=>|&&|\|\||\bconst\s+[A-Za-z_$][\w$]*\s*[=;:]|\bfunction\b\s*[\w(]/.test(text)) return true;
  if (/\b(?:import|export)\s+(?:function|const|default|\{|\*)/.test(text)) return true;
  if (/\b(?:document|window|localStorage|sessionStorage)\.(?:getElementById|querySelector|addEventListener|createElement|location|documentElement|write|open)\b/.test(text)) return true;
  if (/[A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*)*\s*=\s*[^=][^;]*;\s*$/.test(text)) return true;
  return false;
}

function isFormatList(text) {
  const tokens = text.match(FORMAT_TOKENS);
  if (!tokens || tokens.length < 2) return false;
  const rest = text.replace(FORMAT_TOKENS, " ").replace(/[\s/·,.;:()+&—–-]+/g, "");
  return rest.length < 8;
}

function isProse(value) {
  /* The value that arrives here has its substitutions already replaced by a
     placeholder, so a sentence that is mostly a value is judged on its words. */
  const text = value.replace(/\{v\d+\}/g, " 1 ").trim();
  if (text.length < 12 || text.length > 900) return false;
  if (CSS_VALUE.test(text)) return false;
  if (looksLikeCode(text)) return false;
  if (NOT_PROSE.test(text)) return false;
  if (NOT_TRANSLATABLE.some((pattern) => pattern.test(text))) return false;
  if (isFormatList(text)) return false;
  if (REPO_PATH.test(text)) return false;
  const letters = (text.match(/[A-Za-z]/g) ?? []).length;
  const digits = (text.match(/\d/g) ?? []).length;
  if (digits >= letters) return false;
  /* Two words, one of them lower-case: `SomeComponent` and `SQM` are values,
     `the job draws from stock` is prose. */
  if (!/[A-Za-z]{2,}\s+[a-z]{2,}/.test(text)) return false;
  if (CLASSISH.test(text) && (text.includes("-") || text.includes(":"))) return false;
  if (/#[0-9a-fA-F]{3,8}|\b(?:radial|linear|conic)-gradient\(|\brgba?\(|\bhsla?\(/.test(text)) return false;
  return true;
}

/* ── finding template literals ───────────────────────────────────────── */

/**
 * Walk a source file and return every template literal, with the index of its
 * opening backtick and the text with substitutions replaced by `{vN}`.
 *
 * A plain regex would stop at the first nested backtick, and `${...}` may hold
 * another template; the scanner tracks both, plus escapes, so a literal is
 * reported whole or not at all.
 */
function templateLiterals(source) {
  const found = [];
  for (let index = 0; index < source.length; index += 1) {
    if (source[index] !== "`") continue;
    let depth = 0;
    let text = "";
    let values = 0;
    let cursor = index + 1;
    for (; cursor < source.length; cursor += 1) {
      const character = source[cursor];
      if (character === "\\") {
        text += source[cursor + 1] ?? "";
        cursor += 1;
        continue;
      }
      if (depth === 0 && character === "`") break;
      if (depth === 0 && character === "$" && source[cursor + 1] === "{") {
        depth = 1;
        text += `{v${values}}`;
        values += 1;
        cursor += 1;
        continue;
      }
      if (depth > 0) {
        if (character === "{") depth += 1;
        else if (character === "}") depth -= 1;
        else if (character === "`") {
          /* A nested template inside the substitution — skip it wholesale. */
          cursor += 1;
          while (cursor < source.length && source[cursor] !== "`") {
            if (source[cursor] === "\\") cursor += 1;
            cursor += 1;
          }
        }
        continue;
      }
      text += character;
    }
    if (cursor >= source.length) break;
    /* The character before the backtick decides whether it is already tagged or
       otherwise owned by something: `cn(...)`, `t.template\`...\``, `Styles.x`. */
    const before = source.slice(0, index).trimEnd().slice(-1);
    found.push({ start: index, end: cursor, text, values, tagged: /[\w$)\]}]/.test(before) });
    index = cursor;
  }
  return found;
}

async function* walk(dir) {
  for (const entry of await readdir(dir)) {
    if (entry.startsWith(".") || entry === "node_modules") continue;
    const full = path.join(dir, entry);
    const info = await stat(full);
    if (info.isDirectory()) yield* walk(full);
    else if (/\.tsx?$/.test(entry)) yield full;
  }
}

let files = 0;
let sites = 0;
const skipped = new Map();

for await (const file of walk("src")) {
  const source = readFileSync(file, "utf8");
  const literals = templateLiterals(source);
  const edits = [];
  for (const literal of literals) {
    if (literal.tagged) continue;
    if (literal.values === 0) continue; // nothing spliced into it
    if (!isProse(literal.text)) continue;
    /* `t` has to be in scope where the literal sits. The files that render text
       get it from `useT()` or `translatorFor()`; a file without either is
       reported instead of guessed at. */
    if (!/\bconst t = (?:useT\(\)|await translatorFor\(|useTranslator\(\))/.test(source)) {
      skipped.set(file, (skipped.get(file) ?? 0) + 1);
      continue;
    }
    edits.push(literal);
  }
  if (!edits.length) continue;
  files += 1;
  sites += edits.length;
  if (!write) {
    console.log(`${file}: ${edits.length}`);
    for (const edit of edits) console.log(`   ${edit.text.replace(/\s+/g, " ").slice(0, 100)}`);
    continue;
  }
  let out = "";
  let cursor = 0;
  for (const edit of edits) {
    out += source.slice(cursor, edit.start) + "t.template" + source.slice(edit.start, edit.end);
    cursor = edit.end;
  }
  out += source.slice(cursor);
  writeFileSync(file, out);
}

console.log(
  write
    ? `t.template: ${sites} literals tagged in ${files} files`
    : `t.template: ${sites} literals in ${files} files would be tagged (dry run)`,
);
for (const [file, count] of skipped) console.log(`   no translator in scope: ${file} (${count})`);
