#!/usr/bin/env node
/**
 * Wire the prose fields of a data table into the dictionary at the render site.
 *
 * The site keeps most of its copy in `src/data/*.ts`: the editions, the eight
 * workflow stages, the optimization checks, the report surfaces. Those files
 * are scanned by the extractor, so the sentences are keys and the dictionary
 * holds them — and the page still prints English, because the component reads
 * the field straight into JSX:
 *
 *     <h3>{t(edition.name)}</h3>      ← translated
 *     <p>{edition.positioning}</p>    ← not, in the same block
 *
 * Round 23 wired the pages that were obvious this way; a scan of the rendered
 * Arabic found 188 strings still sitting there, every one of them already in
 * the dictionary. This script wraps the remaining field reads.
 *
 * `t()` is safe to apply generously: a value with no entry falls back to the
 * value itself. So the rule is a field allowlist, not a string match — a
 * field whose values are prose in every table that uses it. `key`, `id`,
 * `slug`, `href` and anything else that is an identifier is left alone, as is
 * `site.name` (a brand) and `values.*` / `errors.*` on the form (the first is
 * what the visitor typed).
 *
 * Usage: node scripts/i18n-wire-fields.mjs [--write]
 */

import { readFileSync, writeFileSync } from "node:fs";
import { readdir, stat } from "node:fs/promises";
import path from "node:path";

const write = process.argv.includes("--write");

/* A field that holds a sentence, a label or a stage name — never an id. */
const PROSE_FIELDS = new Set([
  "railLabel", "after", "anchor", "before", "guarantee", "handoff", "breakout",
  "checks", "edges", "range", "step", "category", "specification", "title",
  "uom", "footnote", "label", "section", "value", "headline", "module",
  "openFolder", "retention", "definition", "group", "positioning", "rail",
  "ask", "name", "positioning", "summary", "detail", "capability",
]);
/* A field read that is deliberately not prose. */
const NEVER = /^(?:site|config)\.|\.(?:key|id|slug|href|pattern|code|tone|status)$/;

async function* walk(dir) {
  for (const entry of await readdir(dir)) {
    if (entry.startsWith(".") || entry === "node_modules") continue;
    const full = path.join(dir, entry);
    const info = await stat(full);
    if (info.isDirectory()) yield* walk(full);
    else if (/\.tsx$/.test(entry)) yield full;
  }
}

/* `{item.title}` or `{item.title ?? ""}` — a whole JSX child, nothing else. */
const EXPR = /\{\s*([A-Za-z_$][\w$]*)\??\.([A-Za-z_$][\w$]*)\s*(?:\?\?\s*""\s*)?\}/g;

const wired = [];
for await (const file of walk("src")) {
  const source = readFileSync(file, "utf8");
  if (!/\bt\(/.test(source)) continue;
  let changed = false;
  const next = source.replace(EXPR, (match, object, field) => {
    if (!PROSE_FIELDS.has(field)) return match;
    if (NEVER.test(`${object}.${field}`)) return match;
    /* The form's own state: what the visitor typed is not a key. */
    if (object === "values" || object === "errors") return match;
    changed = true;
    wired.push({ file, expression: `${object}.${field}` });
    /* Keep an optional read optional — `t(x?.y)` still has to prove x. */
    return match.includes(`${object}?.`)
      ? `{t(${object}?.${field} ?? "")}`
      : `{t(${object}.${field})}`;
  });
  if (changed && write) writeFileSync(file, next);
}

const counts = new Map();
for (const row of wired) counts.set(row.file, (counts.get(row.file) ?? 0) + 1);
console.log(`${wired.length} field reads to wrap across ${counts.size} files`);
for (const [file, n] of [...counts].sort((a, b) => b[1] - a[1])) {
  console.log(`  ${String(n).padStart(3)}  ${file}`);
}
if (!write) console.log("\nre-run with --write to apply");
