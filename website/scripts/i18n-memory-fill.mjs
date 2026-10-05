#!/usr/bin/env node
/**
 * Fill the website dictionary from the application's own language files.
 *
 * The website's keys are English sentences, and the application ships the same
 * concepts in fifteen languages (`Themes/Languages/*.xaml`, 2,750 strings each,
 * paired by `x:Key`). Where the two agree, the application's wording is not an
 * approximation of what should appear on the site — it *is* the wording, in the
 * language the customer already reads inside the software.
 *
 * Three ways a website key can be answered by the memory, in decreasing order
 * of confidence:
 *
 *   · **exact** — the key is byte-for-byte an English string in `en-US.xaml`,
 *     so the value is the same `x:Key` in the target language file.
 *   · **normalised** — the same, after case, trailing punctuation and whitespace
 *     are levelled ("Cutting plans" ← "Cutting plans:").
 *   · **composed** — the key is several app strings joined by a separator the
 *     site uses everywhere (` · `, ` — `, ` / `, `, `). Every part has to
 *     resolve; one unknown part and the whole key is refused, because half a
 *     translated sentence is worse than an English one. Numbers and proper
 *     nouns pass through untouched and count as resolved.
 *
 * Anything left over is a translation gap for `i18n-machine-fill.mjs`, not a
 * memory hit. Nothing here is invented: a value is only written when the
 * application already contains that exact string for that language.
 *
 * Usage:
 *   node scripts/i18n-memory-fill.mjs --locale fr-FR [--out /tmp/i18n/mt]
 *   node scripts/i18n-memory-fill.mjs --all                       # every locale
 *   node scripts/i18n-memory-fill.mjs --locale fr-FR --dry        # report only
 *
 * The TSV it writes is applied with the ordinary applier, so there is exactly
 * one writer into `src/i18n/dict/<locale>.ts`:
 *
 *   node scripts/i18n-memory-fill.mjs --locale fr-FR
 *   node --experimental-strip-types scripts/i18n-apply.mjs fr-FR /tmp/i18n/mt/fr-FR-mem.tsv
 */

import { readFileSync, writeFileSync, existsSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const site = join(here, "..");
const root = join(site, "..");

const args = process.argv.slice(2);
const flag = (name, fallback) => {
  const index = args.indexOf(name);
  return index === -1 ? fallback : args[index + 1];
};
const has = (name) => args.includes(name);

const LOCALES = [
  "ar-AE", "bn-BD", "de-DE", "es-ES", "fr-FR", "hi-IN", "id-ID",
  "ja-JP", "ko-KR", "pt-BR", "ru-RU", "tr-TR", "ur-PK", "zh-CN",
];

const outDir = flag("--out", "/tmp/i18n/mt");
const dry = has("--dry");
const targets = has("--all") ? LOCALES : [flag("--locale", "fr-FR")];

/* ── the application's language files ──────────────────────────────────── */

const entities = (text) =>
  text
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'");

function readXaml(locale) {
  const file = join(root, "Themes", "Languages", `${locale}.xaml`);
  if (!existsSync(file)) return null;
  const text = readFileSync(file, "utf8");
  const map = new Map();
  for (const match of text.matchAll(/<sys:String x:Key="([^"]+)"[^>]*>([\s\S]*?)<\/sys:String>/g)) {
    map.set(match[1], entities(match[2]).trim());
  }
  return map;
}

const en = readXaml("en-US");

/* value (as the site would write it) → x:Key, for both lookup styles */
const byValue = new Map();
const byNormalised = new Map();
const normalise = (text) =>
  text
    .replace(/\s+/g, " ")
    .trim()
    .replace(/[\s.:!…·—–-]+$/u, "")
    .trim()
    .toLowerCase();

for (const [key, value] of en) {
  if (!value) continue;
  byValue.set(value, key);
  const normalised = normalise(value);
  if (normalised && !byNormalised.has(normalised)) byNormalised.set(normalised, key);
}

/* ── the keys the site asks for ────────────────────────────────────────── */

const KEYS = [...readFileSync(join(site, "src/i18n/dict/keys.ts"), "utf8").matchAll(/^ {2}("(?:[^"\\]|\\.)*"),$/gm)]
  .map((match) => JSON.parse(match[1]));

/* ── what each language already has ────────────────────────────────────── */

function existing(locale) {
  const file = join(site, "src/i18n/dict", `${locale}.ts`);
  const source = readFileSync(file, "utf8");
  const cut = source.lastIndexOf("\n};");
  const head = source.slice(0, cut === -1 ? undefined : cut);
  const found = new Set();
  const line = /^\s*(?:"((?:[^"\\]|\\.)*)"|'((?:[^'\\]|\\.)*)')\s*:/gm;
  for (const match of head.matchAll(line)) {
    found.add(match[1] !== undefined ? JSON.parse(`"${match[1]}"`) : JSON.parse(`'${match[2]}'`));
  }
  return found;
}

/* ── composed keys ─────────────────────────────────────────────────────── */

const SEPARATORS = [" · ", " — ", " – ", " / ", ", "];

/** Split on every separator the site uses, keeping the separator with its part. */
function pieces(text) {
  const out = [];
  let rest = text;
  let guard = 0;
  while (rest && guard < 40) {
    guard += 1;
    let cut = -1;
    let width = 0;
    for (const separator of SEPARATORS) {
      const at = rest.indexOf(separator);
      if (at !== -1 && (cut === -1 || at < cut)) {
        cut = at;
        width = separator.length;
      }
    }
    if (cut === -1) {
      out.push({ lead: "", text: rest });
      break;
    }
    out.push({ lead: "", text: rest.slice(0, cut) });
    out.push({ lead: rest.slice(cut, cut + width), text: "" });
    rest = rest.slice(cut + width);
  }
  return out.filter((piece) => piece.lead || piece.text);
}

/** A part that is only digits, units or a proper noun needs no translation. */
const NEUTRAL = /^[\d\s.,%×+~≈<>/\\²°'"()–—-]*[A-Za-z]?[\d\s.,%×+~≈<>/\\²°'"()–—-]*$/;
const NEUTRAL_UNITS = /^[\d\s.,%×+~≈<>/\\²°'"()–—-]*(?:mm|cm|m|m²|kg|AED|GB|MB|KB|px|DPI|DWG|OPT|ISO|DAT|CSV|PDF|QR|PCB)?[\d\s.,%×+~≈<>/\\²°'"()–—-]*$/i;

function translatablePart(part) {
  const text = part.trim();
  if (!text) return true;
  if (/^\[\[[PG]\d+\]\]$/.test(text)) return true;
  if (/^[\d\s.,%×+~≈<>/\\²°'"()–—·-]+$/.test(text)) return true;
  if (NEUTRAL.test(text) && NEUTRAL_UNITS.test(text)) return true;
  return false;
}

function compose(key, locale) {
  const parts = pieces(key);
  const words = parts.filter((part) => !part.lead);
  if (words.length < 2) return null;
  const translated = [];
  for (const part of parts) {
    if (part.lead) {
      translated.push(part.lead);
      continue;
    }
    const raw = part.text;
    const trimmed = raw.trim();
    if (translatablePart(raw)) {
      translated.push(raw);
      continue;
    }
    const keyFor = byValue.get(trimmed) ?? byNormalised.get(normalise(trimmed));
    if (!keyFor) return null;
    const value = locale.get(keyFor);
    if (!value) return null;
    /* A part that came from the site may carry its own spacing. */
    translated.push(raw.replace(trimmed, value));
  }
  const joined = translated.join("");
  return joined === key ? null : joined;
}

/* ── run ───────────────────────────────────────────────────────────────── */

if (!dry && !existsSync(outDir)) mkdirSync(outDir, { recursive: true });

console.log("locale  exact  normalised  composed  skipped(existing)  written");
for (const locale of targets) {
  const memory = readXaml(locale);
  if (!memory) {
    console.log(`${locale}: no language file in Themes/Languages — skipped`);
    continue;
  }
  const already = existing(locale);
  const rows = [];
  let exact = 0;
  let normalised = 0;
  let composed = 0;
  let skipped = 0;

  for (const key of KEYS) {
    if (already.has(key)) {
      skipped += 1;
      continue;
    }
    if (byValue.has(key)) {
      const value = memory.get(byValue.get(key));
      if (value) {
        rows.push([key, value]);
        exact += 1;
      }
      continue;
    }
    const trimmed = key.trim();
    const keyFor = byNormalised.get(normalise(trimmed));
    if (keyFor && memory.get(keyFor)) {
      rows.push([key, key.trim() === key ? memory.get(keyFor) : key.replace(trimmed, memory.get(keyFor))]);
      normalised += 1;
      continue;
    }
    const built = compose(key, memory);
    if (built) {
      rows.push([key, built]);
      composed += 1;
    }
  }

  if (!dry && rows.length) {
    const file = join(outDir, `${locale}-mem.tsv`);
    writeFileSync(file, rows.map(([key, value]) => `${key}\t${value}`).join("\n") + "\n", "utf8");
  }
  console.log(
    `${locale.padEnd(6)}  ${String(exact).padStart(5)}  ${String(normalised).padStart(10)}  ${String(composed).padStart(8)}  ${String(skipped).padStart(17)}  ${String(rows.length).padStart(7)}`,
  );
}
