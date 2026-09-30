#!/usr/bin/env node
/**
 * Give every wrapped string a translator in scope.
 *
 * The two wiring passes add `t(...)` around prose. What they cannot know is
 * whether the component that owns the call actually has a translator: a file
 * can hold several components, only the first of which the wiring pass
 * prepared, and the data pass can turn a component that had no wording of its
 * own into one that has plenty.
 *
 * Rather than guess with another regex, this pass asks the compiler. It runs
 * `tsc`, reads the `Cannot find name 't'` errors, finds the function that owns
 * each line, and binds the translator the way that file is allowed to have one:
 *
 *   · a client component (`"use client"`) takes `const t = useT()`
 *   · a server component takes `t: Translator` as a prop — the error then
 *     moves up to the call sites, which `i18n-wire-components.mjs --callers`
 *     answers with `t={t}`
 *
 * Props are written in the expanded form the rest of the tree uses, because
 * inserting a name into a one-line parameter list is how the first attempt
 * produced `function Table({ const t = useT(); view }`.
 *
 * Usage: node scripts/i18n-fix-t-scope.mjs [--rounds 4]
 */

import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";

const roundsFlag = process.argv.indexOf("--rounds");
const rounds = roundsFlag === -1 ? 4 : Number(process.argv[roundsFlag + 1]);

const IMPORT_PROVIDER = 'import { useT } from "@/i18n/provider";';
const IMPORT_TYPE = 'import type { Translator } from "@/i18n/core";';

function tscErrors() {
  try {
    return execFileSync("npx", ["tsc", "--noEmit", "-p", "tsconfig.json"], {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"],
    });
  } catch (error) {
    return `${error.stdout ?? ""}${error.stderr ?? ""}`;
  }
}

function matching(text, open, opener, closer) {
  let depth = 0;
  for (let i = open; i < text.length; i += 1) {
    if (text[i] === opener) depth += 1;
    else if (text[i] === closer) {
      depth -= 1;
      if (depth === 0) return i;
    }
  }
  return -1;
}

/** Add `t` to a component's props, expanded. Returns null if the shape is odd. */
function addProp(source, fnStart) {
  const openParen = source.indexOf("(", fnStart);
  if (openParen === -1) return null;
  const closeParen = matching(source, openParen, "(", ")");
  if (closeParen === -1) return null;

  const params = source.slice(openParen + 1, closeParen);
  const shape = /^\s*\{([\s\S]*?)\}\s*:\s*\{([\s\S]*)\}\s*$/.exec(params);
  if (!shape) return null;

  const names = shape[1]
    .split(",")
    .map((name) => name.trim())
    .filter(Boolean);
  if (names.includes("t")) return source;

  const types = shape[2].trim().split("\n").map((line) => line.trim());
  const expanded = [
    "({",
    "  t,",
    ...names.map((name) => `  ${name},`),
    "}: {",
    "  /** The page's translator — this component renders on the server. */",
    "  t: Translator;",
    ...types.map((line) => `  ${line}`),
    "})",
  ].join("\n");

  return source.slice(0, openParen) + expanded + source.slice(closeParen + 1);
}

/**
 * Put an import at the top of the import block.
 *
 * An import statement is not a line: `import type {` opens one that three
 * lines of names close with `} from "…";`. The end of the block is the last
 * line that *finishes* a statement, which is why this tracks whether it is
 * inside one rather than matching a single line.
 */
function addImport(source, statement) {
  if (source.includes(statement)) return source;
  const lines = source.split("\n");
  let last = -1;
  let inside = false;
  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i];
    if (!inside && last === -1 && line.trim() === "") continue;
    if (!inside && /^import\b/.test(line)) inside = true;
    else if (!inside) break;
    if (line.trimEnd().endsWith(";")) {
      last = i;
      inside = false;
    }
  }
  if (last === -1) return `${statement}\n${source}`;
  lines.splice(last + 1, 0, statement);
  return lines.join("\n");
}

for (let round = 1; round <= rounds; round += 1) {
  const output = tscErrors();
  const missing = output
    .split("\n")
    .filter((line) => /Cannot find name 't'/.test(line))
    .map((line) => {
      const match = /^([^(]+)\((\d+),\d+\)/.exec(line);
      return match ? { file: match[1], line: Number(match[2]) } : null;
    })
    .filter(Boolean);

  if (missing.length === 0) {
    console.log(`round ${round}: nothing left to bind`);
    break;
  }

  const byFile = new Map();
  for (const { file, line } of missing) {
    if (!byFile.has(file)) byFile.set(file, new Set());
    byFile.get(file).add(line);
  }

  for (const [file, lines] of byFile) {
    let source = readFileSync(file, "utf8");
    const isClient = /^\s*"use client";/m.test(source);
    /* Lines are handled bottom-up so an inserted binding never shifts the next
       line number — and `source` is the working copy throughout, because
       reassigning it from the original here is how the first insertion used to
       get thrown away by the second. */
    for (const line of [...lines].sort((a, b) => b - a)) {
      const head = source.split("\n").slice(0, line).join("\n");
      const functions = [...head.matchAll(/^(?:export\s+)?(?:default\s+)?function\s+([A-Za-z_$][\w$]*)/gm)];
      if (functions.length === 0) {
        console.log(`  ${file}:${line} — no component above this line, left alone`);
        continue;
      }
      const fn = functions[functions.length - 1];

      if (isClient) {
        const openParen = source.indexOf("(", fn.index + fn[0].length);
        const closeParen = matching(source, openParen, "(", ")");
        const after = source.indexOf("{", closeParen);
        if (/^\s*const t = useT\(\);/.test(source.slice(after + 1, after + 40))) continue;
        source = `${source.slice(0, after + 1)}\n  const t = useT();${source.slice(after + 1)}`;
        source = addImport(source, IMPORT_PROVIDER);
        console.log(`  ${file}:${line} → useT() in ${fn[1]}`);
      } else {
        const withProp = addProp(source, fn.index);
        if (withProp === null) {
          console.log(`  ${file}:${line} → ${fn[1]} has an unusual signature, left alone`);
          continue;
        }
        source = addImport(withProp, IMPORT_TYPE);
        console.log(`  ${file}:${line} → t prop on ${fn[1]} (callers need t={t})`);
      }
    }
    writeFileSync(file, source);
  }
}
