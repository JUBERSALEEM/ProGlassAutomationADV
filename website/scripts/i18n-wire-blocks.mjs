#!/usr/bin/env node
/**
 * Wire the mixed sentences: a paragraph, its values, and the translator.
 *
 * The other passes work line by line, and a sentence written the way a person
 * writes one — prose, a value spliced in, more prose, another value — defeats
 * them. What is left on the page is one translatable sentence printed as four
 * untranslatable fragments:
 *
 *     <p className="text-sm">
 *       {t(customer.name)} asked for clear tempered glass for the facade of
 *       {t(project.name)} in {project.site}. The enquiry arrived at AED{" "}
 *       {aed(seed.inquiryValue)}; by the time it was priced it had become{" "}
 *       {totals.lines} lines.
 *     </p>
 *
 * This pass finds those blocks and turns each into one key with named
 * placeholders:
 *
 *     <p className="text-sm">
 *       {t("{customer} asked for clear tempered glass for the facade of {project} in {site}. The enquiry arrived at AED {value}; by the time it was priced it had become {lines} lines.", {
 *         customer: t(customer.name), project: t(project.name), site: project.site,
 *         value: aed(seed.inquiryValue), lines: totals.lines,
 *       })}
 *     </p>
 *
 * Five rules keep it honest.
 *
 * **A matched pair of tags.** A block is considered only when an opening tag and
 * its own closing tag are on the same line of reasoning — `<p …>text</p>`, with
 * `text` holding no other tag. Code between two tags (`</Link>); } const open =`)
 * cannot match that shape, which is what keeps a scanner from deciding that a
 * statement is a sentence.
 *
 * **Nothing but text and values.** A nested element, a conditional, a `map`, a
 * comment or a template literal inside the block leaves it alone. It is reported
 * by the run, and handled by hand. A half-understood paragraph is worse than an
 * untranslated one.
 *
 * **Prose only.** The literal text has to read like a sentence: 25 characters,
 * four words, three fifths letters, no `=>`, no backtick, no `<` or `>`.
 *
 * **Values stay values.** `{aed(total)}`, `{row.qty}`, `{t(name)}` and
 * `{a ?? b}` move into the placeholders map untouched. A plain read of a prose
 * property — `{module.detail}` — is wrapped in `t()` because it is a sentence.
 *
 * **The rendered text is the key.** JSX entities are unescaped (`don&apos;t` →
 * `don't`), runs are joined exactly as a browser would join them — a space where
 * the source had one, none where it did not — and `{" "}` disappears, because
 * that is what a reader sees and therefore what the dictionary is keyed by.
 *
 * Usage:
 *   node scripts/i18n-wire-blocks.mjs            # wrap
 *   node scripts/i18n-wire-blocks.mjs --check    # report without writing
 */
import { readFileSync, writeFileSync } from "node:fs";
import { execFileSync } from "node:child_process";

const check = process.argv.includes("--check");

const FILES = execFileSync("git", ["ls-files", "src"], { encoding: "utf8" })
  .split("\n")
  .filter((file) => file.endsWith(".tsx"))
  .filter((file) => !file.includes("/dict/") && !file.includes("/app-strings/"))
  /*
   * The global error boundary renders when the layout that carries the language
   * provider has already failed, so there is nothing to translate with. It is
   * the one file in the site that stays English on purpose.
   */
  .filter((file) => !file.endsWith("global-error.tsx"));

/* Property names that hold a sentence rather than a value. The same list the
   data pass uses; a read outside it is a value and becomes a placeholder. */
const PROSE_PROPERTIES = new Set([
  "label", "labels", "title", "subtitle", "detail", "details", "description",
  "summary", "caption", "note", "notes", "body", "text", "hint", "question",
  "answer", "message", "name", "tagline", "headline", "eyebrow", "kicker",
  "stage", "status", "reason", "value", "unit", "asset", "conclusion",
  "observation", "recommendation",
]);

const ENTITIES = {
  "&apos;": "'", "&#39;": "'", "&#x27;": "'", "&amp;": "&", "&lt;": "<",
  "&gt;": ">", "&quot;": '"', "&nbsp;": " ", "&middot;": "·", "&mdash;": "—",
  "&ndash;": "–", "&times;": "×", "&hellip;": "…", "&rarr;": "→", "&deg;": "°",
  "&sup2;": "²",
};

function unescape(text) {
  return text.replace(/&(?:[a-z]+|#\d+|#x[0-9a-f]+);/gi, (entity) => ENTITIES[entity.toLowerCase()] ?? entity);
}

/*
 * A block: an opening tag, its children, and its own closing tag — with no
 * other tag inside, so the children are text and values and nothing else. The
 * braces are matched one level deep, which is what a `{value}` or a `{t("…")}`
 * needs and all a conditional would not fit into.
 */
const BRACES = String.raw`\{(?:[^{}]|\{[^{}]*\})*\}`;
const BLOCK = new RegExp(
  /* The attributes are plain text on purpose: an attribute holding JSX — 
     `trailing={<ArrowRight />}` — is left out of the match entirely, so a tag
     whose attributes are code can never be mistaken for a paragraph. */
  String.raw`<([A-Za-z][\w.$-]*)([^<>{}]*?)>((?:[^<>{}]|${BRACES})*?)<\/\1>`,
  "g",
);

/* Spans inside template literals and strings are not markup, however much they
   look like it: `SvgDefs` writes `<svg …>` into a template on purpose. */
function protectedSpans(source) {
  const spans = [];
  let index = 0;
  while (index < source.length) {
    const character = source[index];
    if (character === '"' || character === "'" || character === "`") {
      let cursor = index + 1;
      while (cursor < source.length && source[cursor] !== character) {
        if (source[cursor] === "\\") cursor += 1;
        cursor += 1;
      }
      if (character === "`") spans.push([index, cursor]);
      index = cursor + 1;
      continue;
    }
    index += 1;
  }
  return spans;
}

const READ = /^[A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*|\[[^\]]*\])*$/;
const CALL = /^[A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*)*\([^()]*\)$/;
const T_CALL = /^t\(\s*"(?:[^"\\]|\\.)*"(?:\s*,\s*\{[^{}]*\})?\s*\)$/;

/** A string written in braces — `{" "}`, `{"·"}` — is text, not a value. */
function stringLiteral(expression) {
  const value = expression.replace(/\s+/g, " ").trim();
  if (value.length < 2) return null;
  const first = value[0];
  if ((first !== '"' && first !== "'") || value.at(-1) !== first) return null;
  const body = value.slice(1, -1);
  if (body.includes(first) || body.includes("\\")) return null;
  return body;
}

function simpleValue(expression) {
  const value = expression.replace(/\s+/g, " ").trim();
  if (!value) return false;
  if (stringLiteral(value) !== null) return true;
  if (/^-?\d+(?:\.\d+)?$/.test(value)) return true;
  if (READ.test(value) || CALL.test(value) || T_CALL.test(value)) return true;
  if (value.includes("??")) {
    const [left, right] = value.split("??");
    return simpleValue(left) && simpleValue(right);
  }
  return false;
}

function proseOf(text) {
  return unescape(text).replace(/\s+/g, " ").trim();
}

function isProse(text) {
  if (text.length < 25) return false;
  if (!/[A-Za-z]{2,}\s+[A-Za-z]{2,}/.test(text)) return false;
  if (/`|\$\{|=>|&&|\|\||<|>/.test(text)) return false;
  if (text.split(/\s+/).length < 4) return false;
  const letters = (text.match(/[A-Za-z]/g) ?? []).length;
  return letters / text.length >= 0.6;
}

/** The children of a block: literal runs and `{…}` groups, in order. */
function childrenOf(text) {
  const children = [];
  let literal = "";
  let index = 0;
  while (index < text.length) {
    const character = text[index];
    if (character === "{") {
      let depth = 1;
      let cursor = index + 1;
      let quote = null;
      while (cursor < text.length && depth > 0) {
        const inner = text[cursor];
        if (quote) {
          if (inner === "\\") cursor += 1;
          else if (inner === quote) quote = null;
        } else if (inner === '"' || inner === "'" || inner === "`") quote = inner;
        else if (inner === "{") depth += 1;
        else if (inner === "}") depth -= 1;
        cursor += 1;
      }
      if (depth !== 0) return null;
      if (literal) {
        children.push({ kind: "text", value: literal });
        literal = "";
      }
      children.push({ kind: "value", value: text.slice(index + 1, cursor - 1).trim() });
      index = cursor;
      continue;
    }
    literal += character;
    index += 1;
  }
  if (literal) children.push({ kind: "text", value: literal });
  return children;
}

/* Joining is where punctuation is won or lost: `{site.developerName}.` is a
   value with a full stop after it, `catch</span> block.` a word with a space
   before it. The raw runs carry that, so a space goes in only where the source
   had one — or where a value is followed or preceded by more prose. */
const NO_SPACE_BEFORE = /^[.,;:!?)\]}%°²×·…\u2019]/;

let files = 0;
let blocks = 0;
const skipped = [];

for (const file of FILES) {
  const source = readFileSync(file, "utf8");
  if (!source.includes(">")) continue;
  const protected_ = protectedSpans(source);
  const inside = (from, to) => protected_.some(([start, end]) => from >= start && to <= end);

  let next = "";
  let cursor = 0;
  let changed = 0;

  for (const match of source.matchAll(BLOCK)) {
    const [whole, , , children] = match;
    const from = match.index;
    const to = from + whole.length;
    if (inside(from, to)) continue;
    if (whole.includes("//") || whole.includes("/*")) continue;
    if (whole.length > 4000) continue;

    const parsed = childrenOf(children);
    if (!parsed) continue;
    const text = parsed.filter((child) => child.kind === "text").map((child) => child.value).join(" ");
    const joined = proseOf(text);
    if (!isProse(joined)) continue;

    const values = parsed.filter((child) => child.kind === "value");
    if (values.some((child) => !simpleValue(child.value))) {
      skipped.push(`${file}: ${joined.slice(0, 70)}`);
      continue;
    }

    /*
     * JSX keeps a space only when it does not contain a newline — `</span> text`
     * has a space, `</span>\n  text` does not — so the same rule decides whether
     * the key has one. A `{" "}` between two values is a space that exists for
     * exactly this reason.
     */
    let key = "";
    const vars = [];
    let pendingSpace = false;
    const add = (piece, leading, previousTrailing) => {
      if (!piece) return;
      const spaced = pendingSpace || leading || previousTrailing;
      if (key && spaced && (pendingSpace || !NO_SPACE_BEFORE.test(piece))) key += " ";
      key += piece;
      pendingSpace = false;
    };
    let trailing = false;
    for (const child of parsed) {
      if (child.kind === "text") {
        /* JSX removes whitespace that contains a newline, so only a space or a
           tab between two pieces is a space the reader sees. */
        const leading = /^[ \t]/.test(child.value);
        const piece = proseOf(child.value);
        add(piece, leading, trailing);
        trailing = /[ \t]$/.test(child.value);
        continue;
      }
      const expression = child.value.replace(/\s+/g, " ").trim();
      const literal = stringLiteral(expression);
      if (literal !== null) {
        if (!literal.trim()) {
          pendingSpace = true;
          trailing = false;
          continue;
        }
        add(proseOf(literal), true, trailing);
        trailing = true;
        continue;
      }
      /* A read of a prose property is a sentence; wrap it so the value it
         carries is translated like the text around it. */
      const last = expression.split(".").at(-1);
      const value = expression.includes("(") || last === undefined || !PROSE_PROPERTIES.has(last)
        ? expression
        : `t(${expression})`;
      const name = `v${vars.length + 1}`;
      vars.push([name, value]);
      /* A value inherits the space its neighbour wrote, and nothing more. */
      add(`{${name}}`, false, trailing);
      trailing = false;
    }
    key = key.replace(/\s+/g, " ").trim();
    if (key.length < 25) continue;

    const literal = JSON.stringify(key);
    const replacement =
      vars.length === 0
        ? `{t(${literal})}`
        : `{t(${literal}, { ${vars.map(([name, value]) => `${name}: ${value}`).join(", ")} })}`;

    const open = whole.slice(0, whole.indexOf(">") + 1);
    const close = `</${match[1]}>`;
    /*
     * A space at the very edge of the block belongs to the sentence, not to the
     * paragraph — `catch</span> block.` — so it is emitted outside the call as
     * an explicit `{" "}` rather than trimmed away inside a key.
     */
    const edge = (text, side) => {
      const run = side === "start" ? (text.match(/^\s*/) ?? [""])[0] : (text.match(/\s*$/) ?? [""])[0];
      return run && !run.includes("\n") ? '{" "}' : "";
    };
    const before = edge(children, "start");
    const after = edge(children, "end");
    next += source.slice(cursor, from) + open + before + replacement + after + close;
    cursor = to;
    changed += 1;
  }

  if (changed === 0) continue;
  next += source.slice(cursor);
  files += 1;
  blocks += changed;
  if (!check) writeFileSync(file, next);
}

console.log(`${blocks} blocks wired across ${files} files`);
if (skipped.length > 0) {
  console.log(`${skipped.length} blocks left alone (a nested element or a complex value):`);
  for (const item of skipped.slice(0, 20)) console.log(`  ${item}`);
}
