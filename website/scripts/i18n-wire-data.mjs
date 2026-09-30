#!/usr/bin/env node
/**
 * Wire the data-driven text.
 *
 * The literal pass (`i18n-wire-components.mjs`) catches the wording written by
 * hand inside JSX. It cannot see the other half of the site's prose: the
 * sentences that live in `src/data/*.ts` and in the module-level tables of the
 * components, and arrive at the screen as `{entry.label}`, `{module.detail}`,
 * `title={guide.summary}`.
 *
 * This pass finds those read sites and puts them through the translator:
 *
 *     {entry.label}                  →  {t(entry.label)}
 *     title={card.title}             →  title={t(card.title)}
 *     {shot.assetNote ?? shot.title} →  {t(shot.assetNote ?? shot.title)}
 *
 * Four rules make it safe to run over the whole tree.
 *
 * **The data stays English.** No file under `src/data/` is edited. The English
 * sentence is the key, and the dictionary maps it, so a data file remains
 * readable, greppable and diffable, and a new row of data needs no translation
 * work to render.
 *
 * **Only prose properties are wrapped.** `{row.qty}`, `{sheet.height}`,
 * `{entry.href}` and every other number, dimension and URL are left alone:
 * `t()` takes a string, and passing it a width is a type error, not a
 * translation. The property list below is the whitelist, and it is the whole
 * reason this pass is not simply "wrap every curly brace".
 *
 * **Templates are not touched.** `` `${sheet.width} mm` `` is a number with a
 * unit, not a sentence: the file is split on backticks and only the parts
 * outside a template literal are wired, so `${…}` interpolations survive.
 *
 * **The value has to be a plain read.** An attribute is only wired when its
 * value is exactly `a.b` or `a.b ?? c` — never an expression, a call, or a
 * template. Anything more complicated is reported by `--check` for a human.
 *
 * Usage:
 *   node scripts/i18n-wire-data.mjs            # wrap
 *   node scripts/i18n-wire-data.mjs --check    # list what is left unwrapped
 */

import { readFileSync, writeFileSync } from "node:fs";
import { readdir, stat } from "node:fs/promises";
import path from "node:path";

/* The property names that carry prose. A property earns a place here by being
   read into text on screen — never by being a field on an object. */
const PROSE = new Set([
  "label", "title", "body", "detail", "summary", "evidence", "note", "heading",
  "hint", "description", "caption", "name", "category", "status", "step", "job",
  "text", "tagline", "blurb", "benefit", "outcome", "purpose", "reason", "alt",
  "placeholder", "eyebrow", "kicker", "quote", "author", "role",
  "deliverable", "feature", "gap", "answer", "question", "prompt", "why",
  "term", "meaning",
]);

/* The attributes that are read aloud or shown as text rather than drawn. */
const PROSE_ATTRS = new Set([
  "title", "alt", "placeholder", "aria-label", "aria-description", "label",
  "description", "summary", "caption", "heading", "note", "hint", "eyebrow",
  "kicker", "tagline", "aria-valuetext",
]);

const SKIP_DIRS = new Set(["node_modules", ".next", "dict", "app-strings"]);

async function* walk(dir) {
  for (const entry of await readdir(dir)) {
    if (SKIP_DIRS.has(entry) || entry.startsWith(".")) continue;
    const full = path.join(dir, entry);
    const info = await stat(full);
    if (info.isDirectory()) yield* walk(full);
    else if (entry.endsWith(".tsx")) yield full;
  }
}

/* A read: one property, or one property with a fallback. Nothing else. */
const READ = "([A-Za-z_$][\\w$]*(?:\\.[A-Za-z_$][\\w$]*)+(?:\\s*\\?\\?\\s*[A-Za-z_$][\\w$]*(?:\\.[A-Za-z_$][\\w$]*)*)*)";
const TEXT_NODE = new RegExp(`(?<![=\\w$])\\{(?!(?:t|tx)\\()${READ}\\s*\\}`, "g");
const ATTRIBUTE = new RegExp(`\\b([a-zA-Z-]+)=\\{(?!(?:t|tx)\\()(${READ})\\}`, "g");

/** The property a read ends on — the one that decides whether it is prose. */
function propertyOf(expression) {
  const last = expression.split("??").pop().trim();
  return last.slice(last.lastIndexOf(".") + 1);
}

/** Apply an edit only to the parts of a file that are not template literals. */
function outsideTemplates(text, edit) {
  return text
    .split("`")
    .map((piece, index) => (index % 2 === 0 ? edit(piece) : piece))
    .join("`");
}

function wire(file, edits) {
  const source = readFileSync(file, "utf8");

  const textNodes = outsideTemplates(source, (piece) =>
    piece.replace(TEXT_NODE, (whole, expression) => {
      if (!PROSE.has(propertyOf(expression))) return whole;
      edits.push(`{${expression}}`);
      return `{t(${expression})}`;
    }),
  );

  const attributes = outsideTemplates(textNodes, (piece) =>
    piece.replace(ATTRIBUTE, (whole, name, expression) => {
      if (!PROSE_ATTRS.has(name) || !PROSE.has(propertyOf(expression))) return whole;
      edits.push(`${name}={${expression}}`);
      return `${name}={t(${expression})}`;
    }),
  );

  if (attributes !== source) writeFileSync(file, attributes);
  return edits;
}

const check = process.argv.includes("--check");
const report = [];

for await (const file of walk("src")) {
  const edits = wire(file, []);
  if (edits.length > 0) report.push([file, edits]);
}

/* `--check` leaves the tree alone and lists what the pass would do. */
if (check) {
  let total = 0;
  for (const [file, edits] of report) {
    total += edits.length;
    console.log(`${file} — ${edits.length}`);
    for (const edit of edits) console.log(`    ${edit}`);
  }
  console.log(`${total} reads across ${report.length} files`);
} else {
  let total = 0;
  for (const [file, edits] of report) {
    console.log(`  ${String(edits.length).padStart(3)}  ${file}`);
    total += edits.length;
  }
  console.log(`${total} data reads wired across ${report.length} files`);
  console.log("now run: node scripts/i18n-fix-t-scope.mjs");
}
