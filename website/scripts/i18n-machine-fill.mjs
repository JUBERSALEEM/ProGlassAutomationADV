#!/usr/bin/env node
/**
 * Translate the gaps in one run.
 *
 * The dictionary's remaining keys are the long tail of the site's prose — about
 * two and a half thousand sentences per language — and the honest scale of that
 * is data entry, not code. This fills them: it takes every key a language is
 * missing, sends it out in batches, and hands the result to the same
 * `i18n-apply.mjs` every hand-written batch goes through.
 *
 * Three things keep it from being raw machine output:
 *
 * **The product's own words win.** Before anything is sent, the document and
 * module names the desktop application already translates in
 * `Themes/Languages/<code>.xaml` — "Delivery Note", "Job Order", "Statement
 * Generator" — are lifted out of the sentence and replaced with a token. The
 * engine never sees them, so it cannot render one as "Anweisungsgenerator" (the
 * real output, before this existed) or "Arbeitsauftrag". The token comes back as
 * the application's own word.
 *
 * The terms protected are the ones that are unmistakably the product's:
 * multi-word names from the application's language files, plus a short list of
 * single words — Statement, Inquiry, Overview — that are nouns in the software
 * and verbs or adjectives in ordinary English. A word like "Clear" or "Polish"
 * is deliberately *not* protected: in "clear glass" it is an adjective, and
 * forcing the software's noun into the sentence makes worse prose than letting
 * the engine inflect it.
 *
 * **Placeholders are protected the same way.** `{v1} of {rows} rows` goes out as
 * `[[P0]] of [[P1]] rows`, and a row only lands if every placeholder comes back —
 * otherwise it is skipped and counted rather than written with a hole in it.
 *
 * **Sentences that fail are skipped, not pasted.** Empty results, lost tokens
 * and results that came back in the wrong script are counted and listed, and a
 * failed batch does not take the other thirty-nine rows with it.
 *
 * It runs anywhere there is egress. The development sandbox for this repository
 * has none, so it runs in GitHub Actions — see `.github/workflows/i18n-fill.yml`
 * — which is also why this is a script rather than a manual procedure.
 *
 * Usage:
 *   node scripts/i18n-machine-fill.mjs --all
 *   node scripts/i18n-machine-fill.mjs --locale de-DE --debug     # show the masked text
 *   node scripts/i18n-machine-fill.mjs --all --dry                # batch, send nothing
 *   node scripts/i18n-machine-fill.mjs --all --mock               # exercise the pipeline offline
 */
import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { execFileSync } from "node:child_process";

const argv = process.argv.slice(2);
const flag = (name, fallback = null) => {
  const index = argv.indexOf(name);
  return index === -1 ? fallback : argv[index + 1];
};
const has = (name) => argv.includes(name);

const ROOT = new URL("..", import.meta.url).pathname.replace(/\/$/, "");
const ALL = has("--all");
const DRY = has("--dry");
const MOCK = has("--mock");
const DEBUG = has("--debug");
const LIMIT = Number(flag("--limit", "0")) || 0;
const BATCH = Number(flag("--batch", "40"));
const OUT = flag("--out", "/tmp/i18n/mt");

/** Every language the application ships. English is the key set, not a target. */
const LOCALES = ["ar-AE", "bn-BD", "de-DE", "es-ES", "fr-FR", "hi-IN", "id-ID", "ja-JP", "ko-KR", "pt-BR", "ru-RU", "tr-TR", "ur-PK", "zh-CN"];

const chosen = ALL ? LOCALES : [flag("--locale")].filter(Boolean);
if (chosen.length === 0) {
  console.error("Pick languages: --all, or --locale de-DE");
  process.exit(2);
}

/*
 * Single words worth protecting.
 *
 * Each one is a noun in the application — a document, a module, a concept — and
 * each one is also a verb or an adjective in ordinary English, which is exactly
 * why an engine gets them wrong. "Statement" came back as "Anweisung" and
 * "Release" as "Freigabe" in contexts where the sentence meant something else
 * entirely. Anything not on this list is left to the translator's judgement.
 */
const SAFE_SINGLE = new Set([
  "statement", "inquiry", "enquiry", "quotation", "proforma", "invoice", "specification",
  "overview", "dashboard", "optimization", "optimisation", "edition", "licence", "license",
  "workstation", "offcut", "cutout", "spacer", "lite", "tempering", "mitring", "annealing",
  "breakout", "kerf", "guillotine", "nesting", "payload", "audit", "module", "revision",
  "statement generator", "proforma invoice", "tax invoice", "delivery note", "job order",
  "sheet store", "company center", "system center", "master data", "spec group",
]);

/* ── the key set, and what each language already has ─────────────────── */

const keys = [...readFileSync(`${ROOT}/src/i18n/dict/keys.ts`, "utf8").matchAll(/^  ("(?:[^"\\]|\\.)*"),$/gm)].map((match) => JSON.parse(match[1]));

function existing(locale) {
  const body = readFileSync(`${ROOT}/src/i18n/dict/${locale}.ts`, "utf8");
  return new Set([...body.matchAll(/^\s*"((?:[^"\\]|\\.)*)":/gm)].map((match) => JSON.parse(`"${match[1]}"`)));
}

/* ── the application's own vocabulary ────────────────────────────────── */

/** `<sys:String x:Key="…">value</sys:String>` pairs from a language file. */
function languageFile(locale) {
  const body = readFileSync(`${ROOT}/../Themes/Languages/${locale}.xaml`, "utf8");
  const pairs = [];
  for (const match of body.matchAll(/<sys:String x:Key="([^"]+)">([\s\S]*?)<\/sys:String>/g)) {
    const value = match[2]
      .replace(/&amp;/g, "&").replace(/&lt;/g, "<").replace(/&gt;/g, ">")
      .replace(/&quot;/g, '"').replace(/&apos;/g, "'")
      .replace(/\s+/g, " ").trim();
    pairs.push([match[1], value]);
  }
  return new Map(pairs);
}

const english = languageFile("en-US");

/**
 * The glossary for one language, as a matcher.
 *
 * Only what is unmistakably the product's: a name of two or more words, or a
 * single word on the safe list. Format-argument entries are skipped — they use
 * `{0}` where this site uses `{v1}`, and pasting one in drops a hole in the
 * sentence.
 */
function glossaryFor(locale) {
  const target = languageFile(locale);
  const lookup = new Map();
  for (const [key, source] of english) {
    const translated = target.get(key);
    if (!translated || translated === source) continue;
    if (source.length < 3 || translated.length < 2) continue;
    if (/[{}]/.test(source) || /[{}]/.test(translated)) continue;
    if (!/^[A-Za-z]/.test(source) || !/[A-Za-z]{2}/.test(source)) continue;
    const words = source.trim().split(/\s+/).length;
    if (words < 2 && !SAFE_SINGLE.has(source.toLowerCase())) continue;
    /* Two spellings of the same string: the longer one is the better match. */
    const previous = lookup.get(source.toLowerCase());
    if (!previous || previous.length < translated.length) lookup.set(source.toLowerCase(), translated);
  }
  const alternatives = [...lookup.keys()].sort((a, b) => b.length - a.length).map(escapeRe);
  const matcher = alternatives.length > 0
    ? new RegExp(`(?<![A-Za-z0-9])(?:${alternatives.join("|")})(?![A-Za-z0-9])`, "gi")
    : null;
  return { matcher, lookup };
}

function escapeRe(text) {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * Pull the glossary and the placeholders out of a sentence.
 *
 * The placeholders go first, and the glossary pass then runs on the segments
 * *between* them — so a term can never match inside `[[P3]]`, and the token
 * indices cannot drift as replacements change the string's length.
 */
function protect(text, glossary) {
  const table = [];
  const placeholders = [];

  const staged = text.replace(/\{(\w+)\}/g, (whole, name) => {
    table.push({ kind: "P", value: `{${name}}` });
    placeholders.push(`{${name}}`);
    return `[[P${table.length - 1}]]`;
  });

  const masked = staged
    .split(/(\[\[P\d+\]\])/)
    .map((segment) => {
      if (/^\[\[P\d+\]\]$/.test(segment) || !glossary.matcher) return segment;
      return segment.replace(glossary.matcher, (match) => {
        const translated = glossary.lookup.get(match.toLowerCase());
        if (!translated) return match;
        table.push({ kind: "G", value: translated });
        return `[[G${table.length - 1}]]`;
      });
    })
    .join("");

  return { masked, table, placeholders };
}

/** Put the tokens back, tolerating an engine that spaced them out. */
function restore(text, table) {
  const swap = (kind, index) => {
    const entry = table.find((row) => row.kind === kind && row.value && row.index === index);
    return entry ? ` ${entry.value} ` : null;
  };
  /* Number the table by position, which is how the tokens were minted. */
  let counter = 0;
  const numbered = table.map((row) => ({ ...row, index: counter++ }));

  const splice = (source, start, whole, kind, index) => {
    const entry = numbered.find((row) => row.kind === kind && row.index === Number(index));
    if (!entry) return whole;
    /* Keep the sentence's own spacing: the key is the text on screen, and a
       token glued to the next word ("{v1}above") was glued on purpose. Only
       word characters are separated, which is what the engine meant anyway. */
    const before = source[start - 1] ?? " ";
    const after = source[start + whole.length] ?? " ";
    const left = /[A-Za-z0-9]/.test(before) && /^[A-Za-z0-9]/.test(entry.value) ? " " : "";
    const right = /[A-Za-z0-9]/.test(after) && /[A-Za-z0-9]$/.test(entry.value) ? " " : "";
    return `${left}${entry.value}${right}`;
  };
  let out = text.replace(/\[\s*\[\s*([PG])\s*(\d+)\s*\]\s*\]/g, (whole, kind, index, offset, source) => splice(source, offset, whole, kind, index));
  /* The same, for an engine that dropped the brackets and left `P0` standing. */
  out = out.replace(/(?<![A-Za-z\[]{)([PG])(\d{1,2})(?![A-Za-z0-9\]])/g, (whole, kind, index, offset, source) => splice(source, offset, whole, kind, index));
  return out;
}

/** The tidying every translation gets: spacing and punctuation only. */
function tidy(text) {
  return text
    .replace(/\s+/g, " ")
    .replace(/\s+([,.;:!?%)\]}»”…])/g, "$1")
    .replace(/([(\[{«“])\s+/g, "$1")
    .replace(/\s+'\s*/g, "'")
    .trim();
}

/* ── the translator ──────────────────────────────────────────────────── */

const ENDPOINT = "https://clients5.google.com/translate_a/t?client=dict-chrome-ex";

/**
 * One request, and the recovery when it does not work.
 *
 * A URL has a length, and a batch of forty long paragraphs can reach it. An
 * over-long request does not come back with an error the caller can act on — it
 * comes back as a truncated list or not at all — so a failed batch of more than
 * one sentence is split in half and each half tried again. A single sentence
 * that still fails is reported and skipped, which is the point: one bad line
 * must not cost the other thirty-nine.
 */
async function translateBatch(sentences, locale) {
  if (sentences.length === 0) return [];
  if (DRY) return sentences.map(() => null);
  if (MOCK) return sentences.map((sentence) => sentence);

  const params = new URLSearchParams({ sl: "en", tl: locale.slice(0, 2) });
  for (const sentence of sentences) params.append("q", sentence);
  const url = `${ENDPOINT}&${params.toString()}`;

  for (let attempt = 0; attempt <= 2; attempt += 1) {
    try {
      const response = await fetch(url, { headers: { "user-agent": "ProGlassAutomation-site-i18n/1.0" } });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const body = await response.json();
      const list = Array.isArray(body) ? (Array.isArray(body[0]) ? body[0] : body) : null;
      if (!list || list.length !== sentences.length) throw new Error(`expected ${sentences.length}, got ${list ? list.length : "nothing"}`);
      return list;
    } catch (error) {
      if (attempt === 2) {
        if (sentences.length > 1) {
          const half = Math.ceil(sentences.length / 2);
          console.warn(`    batch of ${sentences.length} failed (${error.message}) — splitting`);
          const left = await translateBatch(sentences.slice(0, half), locale);
          const right = await translateBatch(sentences.slice(half), locale);
          return [...left, ...right];
        }
        console.warn(`    sentence failed: ${error.message}`);
        return [null];
      }
      await new Promise((resolve) => setTimeout(resolve, 700 * (attempt + 1)));
    }
  }
  return sentences.map(() => null);
}

/* A language written in its own script cannot come back in Latin letters and be
   right. A Latin-looking answer is a failed translation, not a short one. */
const SCRIPTS = {
  "ar-AE": /[\u0600-\u06FF]/, "bn-BD": /[\u0980-\u09FF]/, "hi-IN": /[\u0900-\u097F]/,
  "ja-JP": /[\u3040-\u30FF\u4E00-\u9FFF]/, "ko-KR": /[\uAC00-\uD7AF]/,
  "ru-RU": /[\u0400-\u04FF]/, "ur-PK": /[\u0600-\u06FF]/, "zh-CN": /[\u4E00-\u9FFF]/,
};

const looksRight = (text, locale) => {
  const script = SCRIPTS[locale];
  if (!script) return true;
  if (!/[a-z]{3}/.test(text)) return true;
  return script.test(text);
};

/* ── the same work, when the tool cannot call out ────────────────────── */

/*
 * This sandbox has no egress at all, and the translator lives on the far side
 * of a socket that will not open. So the script also runs in two halves that a
 * person (or an assistant) drives by hand: `--plan` writes the batch out as
 * request URLs and a source list, and `--ingest` reads the response back and
 * does everything the direct path does — validate, restore, apply.
 *
 *   node scripts/i18n-machine-fill.mjs --plan --locale de-DE --wave 1 --size 60
 *   … one request per printed URL, pasted back …
 *   node scripts/i18n-machine-fill.mjs --ingest --locale de-DE --wave 1 < response.json
 */
/**
 * Sentences a previous wave refused.
 *
 * A row lands only if every placeholder survived. One that does not is a real
 * defect in the answer, so it is remembered and left out of the next wave —
 * otherwise a handful of stubborn rows would sit at the head of the queue and
 * be re-sent on every pass.
 */
function refusals(locale) {
  try {
    return new Set(JSON.parse(readFileSync(`${OUT}/${locale}.refused.json`, "utf8")));
  } catch {
    return new Set();
  }
}

function candidatesFor(locale, source) {
  const have = existing(locale);
  const refused = refusals(locale);
  const missing = keys.filter((key) => !have.has(key) && !refused.has(key));
  if (source === "dict") return missing;
  /* Text the rendered page actually shows comes first — that is the English a
     visitor can still see — then the rest of the key set in its own order. */
  let dump = [];
  try {
    dump = JSON.parse(readFileSync(`${OUT}/${locale}.json`, "utf8")).untranslated ?? [];
  } catch {
    dump = [];
  }
  const seen = new Map();
  for (const row of dump) if (row.text && !seen.has(row.text)) seen.set(row.text, seen.size);
  const fallback = seen.size + keys.length;
  const rank = new Map(keys.map((key, index) => [key, seen.has(key) ? seen.get(key) : fallback + index]));
  return missing.slice().sort((a, b) => rank.get(a) - rank.get(b));
}

/*
 * A batch has to survive the trip as a URL, and a URL has a ceiling. Rather
 * than guess, the plan shrinks the slice until the request fits — long
 * paragraphs travel in smaller groups than short labels, on their own.
 */
const MAX_URL = Number(flag("--max-url", "7000"));

function planBatch(locale, wave, size, source) {
  const candidates = candidatesFor(locale, source);
  const start = wave > 0 ? (wave - 1) * size : 0;
  const slice = candidates.slice(start, start + size);
  const glossary = glossaryFor(locale);
  let prepared = slice.map((key) => ({ key, ...protect(key, glossary) }));
  let params = new URLSearchParams({ sl: "en", tl: locale.slice(0, 2) });
  for (const row of prepared) params.append("q", row.masked);
  while (prepared.length > 5 && `${ENDPOINT}&${params.toString()}`.length > MAX_URL) {
    prepared = prepared.slice(0, -1);
    params = new URLSearchParams({ sl: "en", tl: locale.slice(0, 2) });
    for (const row of prepared) params.append("q", row.masked);
  }
  const tag = flag("--tag", `w${wave}`);
  writeFileSync(`${OUT}/${locale}-${tag}.src.json`, JSON.stringify(prepared, null, 1), "utf8");
  console.log(`${ENDPOINT}&${params.toString()}`);
  console.error(`${prepared.length} of ${candidates.length} sentences still missing · ${`${ENDPOINT}&${params.toString()}`.length} chars · ${locale}-${tag}.src.json`);
}

function ingestBatch(locale, wave) {
  const tag = flag("--tag", flag("--chunk") ? `c${flag("--chunk")}` : `w${wave}`);
  const prepared = JSON.parse(readFileSync(`${OUT}/${locale}-${tag}.src.json`, "utf8"));
  const parts = [];
  const rest = argv.slice(argv.indexOf("--parts") + 1);
  if (argv.includes("--parts")) for (const file of rest) parts.push(readFileSync(file, "utf8"));
  else parts.push(readFileSync(0, "utf8"));
  const raw = parts.join("");
  const body = JSON.parse(raw.replace(/^\s*```(?:json)?/m, "").replace(/```\s*$/m, "").trim());
  const list = Array.isArray(body) ? (Array.isArray(body[0]) ? body[0] : body) : null;
  if (!list || list.length !== prepared.length) {
    console.error(`expected ${prepared.length} translations, got ${list ? list.length : "nothing"}`);
    process.exit(1);
  }
  const rows = [];
  const refused = [];
  let skipped = 0;
  for (let index = 0; index < prepared.length; index += 1) {
    const row = prepared[index];
    if (typeof list[index] !== "string") { skipped += 1; refused.push(row.key); continue; }
    const text = tidy(restore(list[index], row.table));
    const complete = row.placeholders.every((token) => text.includes(token));
    if (text.length === 0 || !complete || !looksRight(text, locale) || /\[\[/.test(text)) { skipped += 1; refused.push(row.key); continue; }
    rows.push([row.key, text]);
  }
  if (refused.length > 0) {
    const previous = [...refusals(locale)];
    writeFileSync(`${OUT}/${locale}.refused.json`, JSON.stringify([...new Set([...previous, ...refused])], null, 1), "utf8");
  }
  const file = `${OUT}/${locale}-${tag}.tsv`;
  writeFileSync(file, rows.map(([key, value]) => `${key}\t${value}\n`).join(""), "utf8");
  console.log(`${locale} ${tag}: ${rows.length} rows written, ${skipped} skipped`);
  if (rows.length > 0) {
    execFileSync(process.execPath, ["--experimental-strip-types", `${ROOT}/scripts/i18n-apply.mjs`, locale, file], { stdio: "inherit", cwd: ROOT });
  }
}

/*
 * The queue, frozen.
 *
 * Working a language means many requests, and a request has to be reproducible
 * later: the response is pasted back after the fact and matched to what was
 * sent. So the whole remaining queue is cut into chunks once — each chunk as
 * large as a request may be — and every chunk is named. Fetching, printing and
 * applying then all refer to the same frozen list, in any order, in parallel,
 * and a chunk already applied is simply recognised as done.
 *
 *   node scripts/i18n-machine-fill.mjs --split --locale de-DE --fresh
 *   node scripts/i18n-machine-fill.mjs --print --locale de-DE --chunk 3
 *   node scripts/i18n-machine-fill.mjs --ingest --locale de-DE --chunk 3 < response
 */
/*
 * A response has a ceiling too. The chunks are sized so one request's answer
 * arrives whole: the source a chunk may carry is bounded as well as its URL,
 * because the answer is about the same length as the question and an answer
 * that arrives in two halves has to be stitched back together by hand.
 */
const MAX_SOURCE = Number(flag("--max-source", "14000"));

function splitQueue(locale, size, source) {
  const file = `${OUT}/${locale}.plan.json`;
  let queue;
  if (has("--fresh") || !existsSync(file)) {
    queue = candidatesFor(locale, source);
    writeFileSync(file, JSON.stringify(queue, null, 1), "utf8");
  } else {
    queue = JSON.parse(readFileSync(file, "utf8"));
  }

  const glossary = glossaryFor(locale);
  const chunks = [];
  let current = [];
  let currentLength = 0;
  let currentSource = 0;
  const base = `${ENDPOINT}&sl=en&tl=${locale.slice(0, 2)}`.length;
  for (const key of queue) {
    const row = { key, ...protect(key, glossary) };
    const cost = new URLSearchParams({ q: row.masked }).toString().length + 1;
    const tooLong = current.length > 0 && (currentLength + cost + base > size || currentSource + key.length > MAX_SOURCE);
    if (tooLong) {
      chunks.push(current);
      current = [];
      currentLength = 0;
      currentSource = 0;
    }
    current.push(row);
    currentLength += cost;
    currentSource += key.length;
  }
  if (current.length > 0) chunks.push(current);

  const have = existing(locale);
  chunks.forEach((rows, index) => {
    const tag = `c${index + 1}`;
    writeFileSync(`${OUT}/${locale}-${tag}.src.json`, JSON.stringify(rows, null, 1), "utf8");
    const params = new URLSearchParams({ sl: "en", tl: locale.slice(0, 2) });
    for (const row of rows) params.append("q", row.masked);
    writeFileSync(`${OUT}/${locale}-${tag}.url`, `${ENDPOINT}&${params.toString()}\n`, "utf8");
    const done = rows.every((row) => have.has(row.key));
    console.log(`${tag}\t${rows.length}\t${done ? "done" : "todo"}\t${rows[0].key.slice(0, 46)}`);
  });
  console.error(`${locale}: ${queue.length} queued in ${chunks.length} chunks of at most ${size} chars`);
}

if (has("--split")) {
  splitQueue(flag("--locale"), Number(flag("--max-url", "12000")), flag("--source", "gaps"));
  process.exit(0);
}
if (has("--print")) {
  process.stdout.write(readFileSync(`${OUT}/${flag("--locale")}-c${flag("--chunk")}.url`, "utf8"));
  process.exit(0);
}

if (has("--plan")) {
  const wave = flag("--wave") === null ? 0 : Number(flag("--wave"));
  planBatch(flag("--locale"), wave, Number(flag("--size", "60")), flag("--source", "gaps"));
  process.exit(0);
}
if (has("--ingest")) {
  ingestBatch(flag("--locale"), Number(flag("--wave", "1")));
  process.exit(0);
}

/* ── run ─────────────────────────────────────────────────────────────── */

mkdirSync(OUT, { recursive: true });
const summary = [];

for (const locale of chosen) {
  const have = existing(locale);
  const missing = keys.filter((key) => !have.has(key));
  const todo = LIMIT > 0 ? missing.slice(0, LIMIT) : missing;
  console.log(`\n${locale}: ${missing.length} to translate${LIMIT > 0 ? ` (doing ${todo.length})` : ""}`);

  const glossary = glossaryFor(locale);
  console.log(`  glossary: ${glossary.lookup.size} terms from Themes/Languages/${locale}.xaml`);

  const rows = [];
  let skipped = 0;
  let done = 0;

  for (let start = 0; start < todo.length; start += BATCH) {
    const slice = todo.slice(start, start + BATCH);
    const prepared = slice.map((key) => ({ key, ...protect(key, glossary) }));
    const results = (await translateBatch(prepared.map((row) => row.masked), locale)) ?? prepared.map(() => null);

    if (DEBUG && start === 0) {
      for (const row of prepared.slice(0, 8)) console.log(`  [masked] ${row.masked}`);
    }

    for (let index = 0; index < prepared.length; index += 1) {
      const row = prepared[index];
      const raw = results[index];
      if (typeof raw !== "string") {
        skipped += 1;
        continue;
      }
      const text = tidy(restore(raw, row.table));
      const complete = row.placeholders.every((token) => text.includes(token));
      if (text.length === 0 || !complete || !looksRight(text, locale) || /\[\[/.test(text)) {
        skipped += 1;
        continue;
      }
      rows.push([row.key, text]);
      done += 1;
    }

    const batches = Math.floor(start / BATCH) + 1;
    if (batches % 10 === 0 || start + BATCH >= todo.length) {
      process.stdout.write(`  ${Math.min(start + BATCH, todo.length)}/${todo.length} — ${done} translated, ${skipped} skipped\n`);
    }
    if (!DRY && !MOCK) await new Promise((resolve) => setTimeout(resolve, 120));
  }

  const file = `${OUT}/${locale}.tsv`;
  writeFileSync(file, rows.map(([key, value]) => `${key.replace(/\t/g, " ")}\t${value.replace(/\t/g, " ")}\n`).join(""), "utf8");
  console.log(`  written: ${file} (${rows.length} rows, ${skipped} skipped)`);
  summary.push([locale, rows.length, skipped, missing.length]);

  if (!DRY && !MOCK && rows.length > 0) {
    execFileSync(process.execPath, ["--experimental-strip-types", `${ROOT}/scripts/i18n-apply.mjs`, locale, file], {
      stdio: "inherit",
      cwd: ROOT,
    });
  }
}

console.log("\nlanguage   written  skipped  of missing");
for (const [locale, written, skipped, missing] of summary) {
  console.log(`${locale.padEnd(10)} ${String(written).padStart(7)} ${String(skipped).padStart(8)} ${String(missing).padStart(11)}`);
}
