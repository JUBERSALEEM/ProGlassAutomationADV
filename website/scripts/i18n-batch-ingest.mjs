#!/usr/bin/env node
import { readFileSync, writeFileSync, existsSync, readdirSync } from "node:fs";
import { execFileSync } from "node:child_process";

const [locale, dir] = process.argv.slice(2);
if (!locale || !dir) {
  console.error("usage: node scripts/i18n-batch-ingest.mjs <locale> <dir>");
  process.exit(1);
}

const files = readdirSync(dir).filter(f => f.startsWith(`${locale}-c`) && f.endsWith(".json"));
files.sort((a, b) => {
  const na = parseInt(a.match(/-c(\d+)\./)[1]);
  const nb = parseInt(b.match(/-c(\d+)\./)[1]);
  return na - nb;
});

for (const f of files) {
  const chunk = f.match(/-c(\d+)\./)[1];
  const fullPath = `${dir}/${f}`;
  console.log(`Ingesting ${locale} chunk ${chunk}...`);
  try {
    const out = execFileSync(
      process.execPath,
      ["scripts/i18n-machine-fill.mjs", "--ingest", "--locale", locale, "--chunk", chunk],
      { input: readFileSync(fullPath), encoding: "utf8" }
    );
    console.log(out.trim());
  } catch (err) {
    console.error(`Error ingesting chunk ${chunk}:`, err.message);
  }
}
