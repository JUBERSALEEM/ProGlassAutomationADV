#!/usr/bin/env node
/*
 * Phase 1 pixel pass — the 25-route audit, per route, no sampling.
 *
 * The coverage report was written for the whole site at once; the pixel
 * pass needs ROUTE attribution, and it needs it from what the browser
 * actually ships — the built HTML — not from the JSX source (a component
 * used on three pages means three route debits when it ships raw).
 *
 * Method: crawl every page the built app exports, read each element's
 * class list, and classify card-shaped elements exactly the way
 * clay-coverage.mjs does (SCULPTED / LIFTED / raw card / well). Every raw
 * card the engine doesn't own becomes a line with route + classes + the
 * element's text (so the fix lands in the engine, not page by page).
 *
 * Exits non-zero if any raw card-shaped element is found on any route —
 * that is Phase 1's exit line made executable.
 */
import { readFileSync, readdirSync, writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const ROOT = new URL("..", import.meta.url).pathname;
const SERVER_APP = join(ROOT, ".next/server/app");

/* Same registry and classification rules as scripts/clay-coverage.mjs —
   the two scripts are one fact. Registry classes are considered sculpted;
   bg-ink-9xx at card radius counts as lifted ONLY because §9.7 exists
   (asserted against the engine file itself). */
const REGISTRY = [
  "surface-raised", "surface", "stat-tile", "glass-primary", "menu-card",
  "nav-glass", "nav-sheet", "nav-pill", "band-deep", "band-rich", "band-solid",
  "plate", "float-card", "card-live", "card-glass", "panel-feature",
  "mpane__glass", "mat-rail__chip", "spine-node", "chip",
  "btn-primary", "btn-secondary", "btn",
];
const FLOOR_FILL = /bg-(?:ink-9\d{2}|glass-)/u;
const CARD_RADIUS = /rounded-(?:card|2xl|panel|xl)/u;
const engine = readFileSync(join(ROOT, "src/app/clay-engine.css"), "utf8");
const ENGINE_LIFTS = /§9\.7/.test(engine) && /bg-ink-900/.test(engine.slice(engine.indexOf("§9.7")));

/* ── Collect every exported page from `.next/server/app/<locale>` ─────── */
const pages = [];
const walk = (dir) => {
  let entries;
  try {
    entries = readdirSync(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const entry of entries) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) walk(path);
    else if (entry.name.endsWith(".html") && !entry.name.startsWith("_")) pages.push(path);
  }
};
/* The locale tree holds the real pages; internals (_global-error etc.) are skipped. */
/* The locale tree holds the real pages; internals (_global-error, api,
   favicon …) are skipped. Prefer en-US — one canonical body is enough,
   translated overlays share the same markup. */
const localeDir = readdirSync(SERVER_APP, { withFileTypes: true }).find(
  (e) => e.isDirectory() && e.name === "en-US",
);
if (localeDir) walk(join(SERVER_APP, localeDir.name));

if (pages.length === 0) {
  console.error("pixel-pass: no pages found — run `npm run build` first (needs .next/server/app)");
  process.exit(2);
}

/* The element inventory of one built page: elements that could carry a
   card. Class lists, plus the first chars of inner text for context. */
const EL = /<([a-z][a-z0-9\-]*)\b([^>]*)>((?:(?!<\/?[a-z]).){0,80})/gsu;
const CLASS_OF = /\bclass="([^"]*)"/u;

const rows = [];
let totalCards = 0, sculpted = 0, lifted = 0, wells = 0;
const rawFindings = [];

for (const file of pages.sort()) {
  const route = "/" + file
    .slice(SERVER_APP.length + 1)
    .replace(/^[^/]+\//, "")        /* strip the locale segment */
    .replace(/\.html$/, "")
    .replace(/\/index$/, "");
  const html = readFileSync(file, "utf8");
  let rSculpted = 0, rLifted = 0, rRaw = 0, rWells = 0;
  for (const match of html.matchAll(EL)) {
    const cls = (match[2].match(CLASS_OF) ?? [])[1];
    if (!cls) continue;
    const text = (match[3] ?? "").replace(/<[^>]*>/gu, "").replace(/\s+/gu, " ").trim().slice(0, 48);
    const hasRegistry = REGISTRY.some((c) => cls.split(/[\s:/]+/u).includes(c));
    if (hasRegistry) {
      rSculpted += 1;
      continue;
    }
    if (FLOOR_FILL.test(cls)) {
      if (CARD_RADIUS.test(cls)) {
        if (ENGINE_LIFTS) rLifted += 1;
        else {
          rRaw += 1;
          rawFindings.push({ route, classes: cls, text });
        }
      }
      continue;
    }
    if (/bg-(?:ink-\d{3}|white\/)/u.test(cls) && /rounded/u.test(cls)) rWells += 1;
  }
  sculpted += rSculpted;
  lifted += rLifted;
  totalCards += rSculpted + rLifted + rRaw;
  wells += rWells;
  rows.push({ route, sculpted: rSculpted, lifted: rLifted, raw: rRaw, wells: rWells });
}

/* Route table + totals. Raw cards are the only failure; wells are design. */
const pct = (n, d) => (d === 0 ? "100.0" : ((n / d) * 100).toFixed(1));
console.log("route".padEnd(42), "sculpted  lifted  raw  wells");
for (const row of rows) {
  console.log(
    row.route.padEnd(42),
    String(row.sculpted).padStart(7),
    String(row.lifted).padStart(7),
    String(row.raw).padStart(4),
    String(row.wells).padStart(6),
  );
}
console.log("─".repeat(70));
console.log(
  "TOTAL".padEnd(42),
  String(sculpted).padStart(7),
  String(lifted).padStart(7),
  String(rows.reduce((n, r) => n + r.raw, 0)).padStart(4),
  String(wells).padStart(6),
);
console.log(`measured over ${rows.length} routes: ${pct(sculpted + lifted, totalCards)}% clay (sculpted + lifted / card-shaped)`);

mkdirSync(join(ROOT, "artifacts"), { recursive: true });
writeFileSync(
  join(ROOT, "artifacts/clay-pixel-pass.json"),
  JSON.stringify({ routes: rows, totals: { sculpted, lifted, raw: rows.reduce((n, r) => n + r.raw, 0), wells }, rawFindings }, null, 2),
);
console.log("machine report: artifacts/clay-pixel-pass.json");

if (rawFindings.length > 0) {
  console.error(`\nFAIL: ${rawFindings.length} raw card-shaped elements found:`);
  for (const hit of rawFindings.slice(0, 40)) {
    console.error(`  ${hit.route}  ${hit.classes}\n      ...${hit.text}`);
  }
  process.exit(1);
}
console.log("\nPASS: every card-shaped element on every route is sculpted clay.");
