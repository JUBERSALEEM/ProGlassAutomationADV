#!/usr/bin/env node
/**
 * Import the application's own translations.
 *
 * `Themes/Languages/*.xaml` inside the WPF application is where this product's
 * vocabulary actually lives: 2,750 keys per language, written by the same
 * author, covering the documents, statuses, stages, actions and settings the
 * software shows. Any sentence the site shares with the application should be
 * that sentence — not a second translation of it that can drift.
 *
 * This script does two jobs and writes two artefacts:
 *
 *   1. `src/i18n/app-strings/<locale>.json` — the curated view of the language
 *      file (navigation, document types, actions, common words, shell, status
 *      vocabulary) keyed by its XAML key, so the site can ask for `Dg.Base` and
 *      receive the application's translated string. Shipped in the repo.
 *
 *   2. `/tmp/app-values-<locale>.json` — *every* value in the file, indexed by
 *      its English original. This is what `scripts/i18n-merge-app.mjs` uses to
 *      fill dictionary entries; it is not shipped, because 2,600 values times
 *      fourteen languages on every page is a payload nobody asked for.
 *
 * Why the XAML and not the compiled `.resources`: the XAML is the source that
 * ships in the repository, it is what a translator edits, and it needs no .NET
 * toolchain to read.
 *
 * Run:  node scripts/build-app-strings.mjs
 */
import { readFileSync, mkdirSync, writeFileSync, existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { tmpdir } from "node:os";

const here = dirname(fileURLToPath(import.meta.url));
const website = join(here, "..");
const repo = join(website, "..");
const languages = join(repo, "Themes", "Languages");
const out = join(website, "src", "i18n", "app-strings");
const scratch = process.env.APP_STRINGS_CACHE || join(tmpdir(), "proglass-app-strings");

const LOCALES = [
  "en-US", "ar-AE", "bn-BD", "de-DE", "es-ES", "fr-FR", "hi-IN", "id-ID",
  "ja-JP", "ko-KR", "pt-BR", "ru-RU", "tr-TR", "ur-PK", "zh-CN",
];

/**
 * Which groups of keys the website has a use for.
 *
 * Coarser than "everything" on purpose: the settings pages alone carry 450
 * keys, and a website that claims to translate the settings screen it does not
 * render is a website lying about its coverage. These six groups are the ones
 * whose words appear on a page — a document type in the worked example, an
 * action on a button, a status in a table.
 */
const GROUPS = ["Nav.", "Dg.", "Action.", "Common.", "Dashboard.", "Shell."];

const ENTITIES = { "&amp;": "&", "&lt;": "<", "&gt;": ">", "&quot;": '"', "&apos;": "'", "&#x27;": "'", "&#39;": "'" };

function unescape(value) {
  return value.replace(/&(?:amp|lt|gt|quot|apos|#x27|#39);/g, (match) => ENTITIES[match] ?? match);
}

function readLanguage(locale) {
  const file = join(languages, `${locale}.xaml`);
  if (!existsSync(file)) throw new Error(`missing language file: ${file}`);
  const text = readFileSync(file, "utf8");
  const entries = new Map();
  const pattern = /<sys:String\s+x:Key="([^"]+)"[^>]*>([\s\S]*?)<\/sys:String>/g;
  let match;
  while ((match = pattern.exec(text))) {
    entries.set(match[1], unescape(match[2]).replace(/\s+/g, " ").trim());
  }
  return entries;
}

mkdirSync(out, { recursive: true });
mkdirSync(scratch, { recursive: true });

const english = readLanguage("en-US");

/*
 * Every sentence the website can render, generated into `dict/keys.ts` by
 * `scripts/i18n-extract.mjs`. Optional: the script is also useful before that
 * list exists, when only the curated groups are imported.
 */
const KEY_SENTENCES = new Set();
const keysFile = join(website, "src", "i18n", "dict", "keys.ts");
if (existsSync(keysFile)) {
  for (const match of readFileSync(keysFile, "utf8").matchAll(/^\s*"((?:[^"\\]|\\.)*)",?\s*$/gm)) {
    KEY_SENTENCES.add(JSON.parse(`"${match[1]}"`));
  }
}

const curated = [];
let curatedCount = 0;

for (const locale of LOCALES) {
  const entries = locale === "en-US" ? english : readLanguage(locale);

  /*
   * Shipped: the curated view, keyed by XAML key. Two ways in — the six groups
   * whose words appear on a page, and any key at all whose English value is a
   * sentence the site itself renders. The second rule is what lets the site
   * inherit a translation without writing it: if the application already says
   * "Delivery Note" as "Lieferschein" in a settings screen nobody visits, that
   * is still this product's word for it, and the site should use it rather
   * than a second translation that can drift.
   */
  const byKey = {};
  for (const [key, value] of entries) {
    const source = english.get(key) ?? "";
    if (!GROUPS.some((group) => key.startsWith(group)) && !(source && KEY_SENTENCES.has(source))) continue;
    byKey[key] = value;
  }
  mkdirSync(out, { recursive: true });
  writeFileSync(join(out, `${locale}.json`), `${JSON.stringify(byKey, null, 0)}\n`);
  if (locale !== "en-US") curatedCount = Object.keys(byKey).length;
  curated.push(`${locale}: ${Object.keys(byKey).length}`);

  /* Scratch: every value, indexed by the English original. Used by the merge
     step, never shipped. */
  if (locale !== "en-US") {
    const byEnglish = {};
    for (const [key, value] of entries) {
      const source = english.get(key);
      if (!source || !value) continue;
      if (source.length < 2) continue;
      /* A value identical to its English original is not a translation. */
      if (source === value) continue;
      byEnglish[source] = value;
    }
    writeFileSync(join(scratch, `${locale}.json`), `${JSON.stringify(byEnglish)}\n`);
  }
}

console.log(`app strings: ${curated.join(" · ")}`);
console.log(`             ${curatedCount} curated keys per language, written to src/i18n/app-strings/`);
console.log(`             ${KEY_SENTENCES.size} website sentences known, matched against every value in the file`);
console.log(`             full value index (${LOCALES.length - 1} languages) in ${scratch}`);
