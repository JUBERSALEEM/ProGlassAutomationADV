#!/usr/bin/env node
/**
 * Fill a dictionary with the application's own translations.
 *
 * The site's key is the English sentence it renders. The application's language
 * file is keyed by identifiers (`Dg.Base`, `Nav.ProformaInvoice`) and carries the
 * English original only in `en-US.xaml`. Put those two together and you get the
 * map this script builds:
 *
 *     "Delivery Note"  →  "Lieferschein"
 *
 * — from the product itself, for the English sentence the site renders. Any key
 * the site has and the application has already named is filled in every language
 * the application ships, with no translation written twice and no chance of the
 * website calling a proforma something the software does not call it.
 *
 * It writes a batch file per language and hands it to `i18n-apply.mjs`, so the
 * dictionary writer is the same one a hand-written batch uses: existing entries
 * are kept, unknown keys are reported, nothing is silently dropped.
 *
 * Run:  node scripts/i18n-import-app.mjs [locale…]
 */
import { readFileSync, existsSync, mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { execFileSync } from "node:child_process";

const here = dirname(fileURLToPath(import.meta.url));
const website = join(here, "..");
const repo = join(website, "..");
const languages = join(repo, "Themes", "Languages");
const out = "/tmp/i18n/app-batches";

const LOCALES = [
  "ar-AE", "bn-BD", "de-DE", "es-ES", "fr-FR", "hi-IN", "id-ID",
  "ja-JP", "ko-KR", "pt-BR", "ru-RU", "tr-TR", "ur-PK", "zh-CN",
];

const ENTITIES = { "&amp;": "&", "&lt;": "<", "&gt;": ">", "&quot;": '"', "&apos;": "'", "&#x27;": "'", "&#39;": "'" };

function readLanguage(locale) {
  const file = join(languages, `${locale}.xaml`);
  if (!existsSync(file)) throw new Error(`missing language file: ${file}`);
  const text = readFileSync(file, "utf8");
  const entries = new Map();
  const pattern = /<sys:String\s+x:Key="([^"]+)"[^>]*>([\s\S]*?)<\/sys:String>/g;
  let match;
  while ((match = pattern.exec(text))) {
    const value = match[2]
      .replace(/&(?:amp|lt|gt|quot|apos|#x27|#39);/g, (entity) => ENTITIES[entity] ?? entity)
      .replace(/\s+/g, " ")
      .trim();
    if (value) entries.set(match[1], value);
  }
  return entries;
}

/** Every sentence the site can render — `dict/keys.ts`, written by the extractor. */
function readKeys() {
  const file = join(website, "src", "i18n", "dict", "keys.ts");
  const keys = [];
  for (const match of readFileSync(file, "utf8").matchAll(/^\s*"((?:[^"\\]|\\.)*)",?\s*$/gm)) {
    keys.push(JSON.parse(`"${match[1]}"`));
  }
  return keys;
}

const wanted = process.argv.slice(2).filter((arg) => !arg.startsWith("-"));
const list = wanted.length > 0 ? wanted : LOCALES;
const keys = readKeys();
const known = new Set(keys);
const english = readLanguage("en-US");
mkdirSync(out, { recursive: true });

let total = 0;
const summary = [];

for (const locale of list) {
  const entries = readLanguage(locale);
  const rows = [];
  for (const [key, value] of entries) {
    const source = english.get(key);
    if (!source || source === value) continue;
    if (!known.has(source)) continue;
    rows.push(`${source}\t${value}`);
  }
  const batch = join(out, `${locale}.tsv`);
  writeFileSync(batch, rows.length > 0 ? `${rows.join("\n")}\n` : "");
  let applied = 0;
  if (rows.length > 0) {
    const output = execFileSync(
      process.execPath,
      ["--experimental-strip-types", join(here, "i18n-apply.mjs"), locale, batch],
      { cwd: website, encoding: "utf8" },
    );
    applied = Number(/\+(\d+)/.exec(output)?.[1] ?? 0);
    total += applied;
  }
  summary.push(`${locale}: ${rows.length} in the application's file, ${applied} new to the dictionary`);
}

console.log(summary.join("\n"));
console.log(`${total} entries imported from Themes/Languages/ across ${list.length} language(s)`);
