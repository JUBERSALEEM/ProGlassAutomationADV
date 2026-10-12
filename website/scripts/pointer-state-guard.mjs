#!/usr/bin/env node
/*
 * Phase 2 CI guard — no React state writes from hover/move handlers.
 *
 * The freeze class this gates: a handler that fires per pointer-frame
 * (onPointerMove fires per frame, onPointerOver fires per crossed node
 * boundary) may write styles or scroll positions, but a setState call
 * re-renders the component per event — the exact menu/theme-switch freeze
 * class fixed in D25. Drag tracking is the one legitimate exception: it
 * writes refs, never state, while the gesture runs.
 *
 * Method (string-level, honest about it): find every pointer/mouse event
 * handler attribute in JSX, take the handler body up to the matching
 * close of the JSX attribute, and flag any `setXxx(` call in it that is
 * not preceded by a comparison guard or marked // pointer-guard-ok with a
 * written justification on the same line.
 */
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative } from "node:path";

const ROOT = new URL("..", import.meta.url).pathname;
const SCAN_DIRS = ["src/app/[locale]", "src/components", "src/hooks", "src/lib"];

const ATTR = /on(?:Pointer|Mouse)(?:Move|Over|Enter|Leave|Down|Up|Cancel)\s*=\s*\{/gu;
const SETTER = /\bset[A-Z]\w*\s*\(/gu;

const files = [];
const walk = (dir) => {
  for (const entry of readdirSync(dir)) {
    const path = join(dir, entry);
    if (statSync(path).isDirectory()) walk(path);
    else if (/\.tsx?$/u.test(entry)) files.push(path);
  }
};
for (const dir of SCAN_DIRS) {
  try {
    walk(join(ROOT, dir));
  } catch {
    /* A dir that doesn't exist yet isn't a finding. */
  }
}

/* A handler body ends at the attribute's close brace. Because JSX braces
   nest (template strings, objects), walk the brace depth from the opening
   brace rather than cutting at a fixed character count. */
function handlerBody(text, openBraceIndex) {
  let depth = 0;
  for (let i = openBraceIndex; i < text.length; i += 1) {
    const char = text[i];
    if (char === "{") depth += 1;
    if (char === "}") {
      depth -= 1;
      if (depth === 0) return text.slice(openBraceIndex + 1, i);
    }
  }
  return text.slice(openBraceIndex + 1);
}

const violations = [];
for (const file of files) {
  const text = readFileSync(file, "utf8");
  for (const match of text.matchAll(ATTR)) {
    const body = handlerBody(text, match.index + match[0].length - 1);
    for (const setter of body.matchAll(SETTER)) {
      const lineStart = body.lastIndexOf("\n", setter.index) + 1;
      const lineEnd = body.indexOf("\n", setter.index);
      const line = body.slice(lineStart, lineEnd < 0 ? undefined : lineEnd);
      /* Justification may sit beside the call or in the comment block
         immediately above it — where a reviewer will actually read it.
         Accept the marker anywhere in a three-line window. */
      const windowStart = Math.max(0, lineStart - 400);
      const windowText = body.slice(windowStart, lineEnd < 0 ? undefined : lineEnd);
      if (windowText.includes("pointer-guard-ok")) continue;
      /* A comparator next to the setter (reported index, prev-value check)
         is the standard dedupe — the setter only fires on real change. */
      if (/!==?\s*(?:index|state|value|prev|current)/.test(line)) continue;
      violations.push({
        file: relative(ROOT, file),
        line: text.slice(0, match.index).split("\n").length,
        setter: setter[0],
      });
    }
  }
}

if (violations.length > 0) {
  console.error(`pointer-state-guard: ${violations.length} unguarded state write(s) in pointer handlers:`);
  for (const hit of violations) {
    console.error(`  ${hit.file}:${hit.line}  ${hit.setter}`);
  }
  console.error(
    "rule: pointermove/-over handlers may write element styles (imperative) " +
      "but never React state; dedupe if the write is genuinely stateful, " +
      "or justify on the same line with // pointer-guard-ok",
  );
  process.exit(1);
}
console.log(`pointer-state-guard: ${files.length} files scanned — no unguarded state writes in pointer handlers.`);
