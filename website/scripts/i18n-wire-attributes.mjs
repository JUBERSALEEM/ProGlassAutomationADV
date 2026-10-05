#!/usr/bin/env node
/**
 * Wire the text written as an attribute.
 *
 * A surprising amount of the site's prose never appears between two tags. It is
 * an attribute of a component:
 *
 *     <StatBadge label="Quoted before VAT" hint="Line values rounded individually, then summed." />
 *     <StatCounter hint={`[Fact] and [Theory] methods across ${site.testFiles} test files.`} />
 *
 * Both are sentences a reader sees, printed beside the figures they explain, and
 * neither is visible to a pass that looks for text between `>` and `<`. This one
 * wraps the prose attributes:
 *
 *     <StatBadge label={t("Quoted before VAT")} hint={t("Line values rounded individually, then summed.")} />
 *     <StatCounter hint={t("[Fact] and [Theory] methods across {files} test files.", { files: site.testFiles })} />
 *
 * Three rules, and they are all about not touching what is not prose:
 *
 * **A named list of attributes.** `label`, `hint`, `title`, `description`,
 * `caption`, `summary`, `aria-label`, `alt`, `placeholder` and their neighbours —
 * the properties that hold a sentence. `className`, `href`, `id`, `key`, `value`
 * and `unit` are not on the list; a class list is not a sentence and a value is
 * not words.
 *
 * **Words, not code.** A string only qualifies when it reads as one: two words,
 * twelve characters, three fifths letters, no braces, no angle brackets. A
 * format like `"AED 34,762.19"` stays where it is.
 *
 * **Templates keep their values.** `` `… ${x} test files` `` becomes a key with a
 * placeholder and the value moves into the placeholders map, so the number stays
 * a number. An interpolation that is itself a call with arguments is reported
 * rather than guessed at.
 *
 * Usage:
 *   node scripts/i18n-wire-attributes.mjs            # wrap
 *   node scripts/i18n-wire-attributes.mjs --check    # report without writing
 */
import { readFileSync, writeFileSync } from "node:fs";
import { execFileSync } from "node:child_process";

const check = process.argv.includes("--check");

/* Attributes that hold a sentence. Anything else — a class list, a URL, an
   identifier, a number — is left exactly as it is. */
const PROSE_ATTRIBUTES = [
  "label", "labels", "title", "subtitle", "hint", "note", "notes", "description",
  "caption", "summary", "eyebrow", "detail", "details", "question", "answer",
  "tagline", "headline", "kicker", "placeholder", "aria-label", "alt", "empty",
  "footnote", "message", "badge", "conclusion", "observation", "recommendation",
];

const NAMES = PROSE_ATTRIBUTES.join("|");

const FILES = execFileSync("git", ["ls-files", "src"], { encoding: "utf8" })
  .split("\n")
  .filter((file) => file.endsWith(".tsx"))
  .filter((file) => !file.includes("/dict/") && !file.includes("/app-strings/"))
  /* The global error boundary renders without a provider: no translator to call. */
  .filter((file) => !file.endsWith("global-error.tsx"))
  /*
   * The brand mark is not a sentence: `aria-label="ProGlass Automation"` names
   * the product in the alphabet it is written in everywhere, and the site's own
   * rule is that a brand stays Latin in every language.
   */
  .filter((file) => !file.endsWith("brand/Logo.tsx"));

function readsAsProse(text) {
  const value = text.replace(/\s+/g, " ").trim();
  if (value.length < 12) return false;
  if (!/[A-Za-z]{2,}\s+[A-Za-z]{2,}/.test(value)) return false;
  if (/[{}<>`]|\$\{/.test(value)) return false;
  const letters = (value.match(/[A-Za-z]/g) ?? []).length;
  return letters / value.length >= 0.6;
}

/** The pieces of a template literal: text and `${…}` interpolations. */
function templateParts(body) {
  const parts = [];
  let text = "";
  let index = 0;
  while (index < body.length) {
    if (body[index] === "\\") {
      text += body[index + 1] ?? "";
      index += 2;
      continue;
    }
    if (body[index] === "$" && body[index + 1] === "{") {
      let depth = 1;
      let cursor = index + 2;
      let quote = null;
      while (cursor < body.length && depth > 0) {
        const inner = body[cursor];
        if (quote) {
          if (inner === "\\") cursor += 1;
          else if (inner === quote) quote = null;
        } else if (inner === '"' || inner === "'" || inner === "`") quote = inner;
        else if (inner === "{") depth += 1;
        else if (inner === "}") depth -= 1;
        cursor += 1;
      }
      if (depth !== 0) return null;
      parts.push({ kind: "text", value: text });
      parts.push({ kind: "value", value: body.slice(index + 2, cursor - 1).trim() });
      text = "";
      index = cursor;
      continue;
    }
    text += body[index];
    index += 1;
  }
  parts.push({ kind: "text", value: text });
  return parts;
}

/* A value put into a template has to be a plain read or a call with simple
   arguments; anything cleverer is left for a person. */
const SIMPLE = /^[A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*|\[[^\]]*\])*(?:\([^()]*\))?$/;

let files = 0;
let wrapped = 0;
const skipped = [];

for (const file of FILES) {
  const source = readFileSync(file, "utf8");
  if (!new RegExp(`${NAMES}=`).test(source)) continue;

  let next = source;
  const edits = [];

  /* Plain strings: label="a sentence". */
  const plain = new RegExp(`\\b(${NAMES})=("((?:[^"\\\\\\n]|\\\\.)*)")`, "g");
  for (const match of source.matchAll(plain)) {
    const [whole, name, , body] = match;
    const value = body.replace(/\\(["\\])/g, "$1");
    if (!readsAsProse(value)) continue;
    if (source.slice(Math.max(0, match.index - 2), match.index).includes("//")) continue;
    edits.push([match.index, match.index + whole.length, `${name}={t(${JSON.stringify(value.replace(/\s+/g, " ").trim())})}`]);
  }

  /* Templates: hint={`… ${value} …`}. */
  const templated = new RegExp(`\\b(${NAMES})=\\{\`([\\s\\S]*?)\`\\}`, "g");
  for (const match of source.matchAll(templated)) {
    const [whole, name, body] = match;
    const parts = templateParts(body);
    if (!parts) continue;
    const text = parts.filter((part) => part.kind === "text").map((part) => part.value).join(" ");
    if (!readsAsProse(text)) continue;
    const values = parts.filter((part) => part.kind === "value");
    if (values.some((part) => !SIMPLE.test(part.value.replace(/\s+/g, " ")))) {
      skipped.push(`${file}: ${text.replace(/\s+/g, " ").trim().slice(0, 70)}`);
      continue;
    }
    let key = "";
    const vars = [];
    for (const part of parts) {
      if (part.kind === "text") {
        const piece = part.value.replace(/\s+/g, " ").trim();
        if (!piece) continue;
        if (key && !key.endsWith(" ")) key += " ";
        key += piece;
        continue;
      }
      const expression = part.value.replace(/\s+/g, " ").trim();
      const name2 = `v${vars.length + 1}`;
      vars.push([name2, expression]);
      if (key && !key.endsWith(" ")) key += " ";
      key += `{${name2}}`;
    }
    key = key.replace(/\s+/g, " ").trim();
    if (!readsAsProse(key)) continue;
    const replacement =
      vars.length === 0
        ? `${name}={t(${JSON.stringify(key)})}`
        : `${name}={t(${JSON.stringify(key)}, { ${vars.map(([key2, value]) => `${key2}: ${value}`).join(", ")} })}`;
    if (source.slice(Math.max(0, match.index - 2), match.index).includes("//")) continue;
    edits.push([match.index, match.index + whole.length, replacement]);
  }

  if (edits.length === 0) continue;
  edits.sort((a, b) => b[0] - a[0]);
  for (const [from, to, replacement] of edits) {
    next = next.slice(0, from) + replacement + next.slice(to);
  }
  wrapped += edits.length;
  files += 1;
  if (!check) writeFileSync(file, next);
}

console.log(`${wrapped} attributes wired across ${files} files`);
if (skipped.length > 0) {
  console.log(`${skipped.length} left alone (an interpolation a person should look at):`);
  for (const item of skipped.slice(0, 15)) console.log(`  ${item}`);
}
