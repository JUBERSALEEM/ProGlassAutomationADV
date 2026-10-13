#!/usr/bin/env node
/*
 * Claymorphism coverage report — how much of the UI is sculpted clay and
 * how much is still the flat-UI floor.
 *
 * Method (string-level, deliberate and repeatable): every JSX class string
 * in the route pages and components is classified —
 *
 *   SCULPTED  carries a named clay-registry class (.surface, .stat-tile,
 *             .menu-card, .band-*, .btn-primary ...). The engine sculpts
 *             these with the full shadow recipe and the token contract.
 *   LIFTED    carries a raw card-shaped fill (bg-ink-9xx / bg-glass-* at
 *             card radius) — flattened to a foil before §9.7, now sculpted
 *             by the engine itself. Only counted once the §9.7 rule exists
 *             in clay-engine.css (detected below, never assumed).
 *   WELL      raw fill at a small radius — pressed things: code chips,
 *             table stripes, input shells. Flat is their design; they are
 *             not deficits and are reported separately.
 *
 * The number that matters: (sculpted + lifted) / (sculpted + lifted +
 * card-shaped-floor). Everything is counted, never sampled.
 */
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative } from "node:path";

const ROOT = new URL("..", import.meta.url).pathname;
const SCAN_DIRS = ["src/app/[locale]", "src/components"];

const REGISTRY = [
  "surface-raised", "surface", "stat-tile", "glass-primary", "menu-card",
  "nav-glass", "nav-sheet", "nav-pill", "band-deep", "band-rich", "band-solid",
  "plate", "float-card", "card-live", "card-glass", "panel-feature",
  "mpane__glass", "mat-rail__chip", "spine-node", "chip",
  "btn-primary", "btn-secondary", "btn",
];
const FLOOR_FILL = /bg-(?:ink-9\d{2}|glass-)/u;
const CARD_RADIUS = /rounded-(?:card|2xl|panel|xl)/u;
const ANY_RADIUS = /rounded/u;

const engine = readFileSync(join(ROOT, "src/app/clay-engine.css"), "utf8");
const ENGINE_LIFTS = /§9\.7/.test(engine) && /bg-ink-900/.test(engine.slice(engine.indexOf("§9.7")));

const files = [];
const walk = (dir) => {
  for (const entry of readdirSync(dir)) {
    const path = join(dir, entry);
    if (statSync(path).isDirectory()) walk(path);
    else if (/\.tsx?$/u.test(entry)) files.push(path);
  }
};
for (const dir of SCAN_DIRS) walk(join(ROOT, dir));

const CLASS_LITERAL = /className\s*=\s*(?:"([^"]*)"|`([^`]*)`|\{\s*"([^"]*)"|cn\(([^)]*)\))/gu;
const INNER_STRING = /"([^"]*)"/gu;

let sculpted = 0;
let cardShaped = 0;
let wells = 0;
const perFile = [];

for (const file of files.sort()) {
  const text = readFileSync(file, "utf8");
  let fSculpted = 0;
  let fCards = 0;
  let fWells = 0;
  for (const match of text.matchAll(CLASS_LITERAL)) {
    const raw = match[1] ?? match[2] ?? match[3] ?? match[4] ?? "";
    const strings = match[4] !== undefined ? [...raw.matchAll(INNER_STRING)].map((m) => m[1]).join(" ") : raw;
    const hasRegistry = REGISTRY.some((cls) => new RegExp(`(?:^|[\\s"'])${cls}(?:[\\s"':/]|$)`, "u").test(strings));
    if (hasRegistry) fSculpted += 1;
    else if (FLOOR_FILL.test(strings) && CARD_RADIUS.test(strings)) fCards += 1;
    else if (/bg-(?:ink-\d{3}|white\/|glass-)/u.test(strings) && ANY_RADIUS.test(strings)) fWells += 1;
  }
  if (fSculpted + fCards + fWells > 0) {
    perFile.push({ file: relative(ROOT, file), sculpted: fSculpted, cards: fCards, wells: fWells });
    sculpted += fSculpted;
    cardShaped += fCards;
    wells += fWells;
  }
}

const lifted = ENGINE_LIFTS ? cardShaped : 0;
const denominator = sculpted + cardShaped;
const pct = denominator === 0 ? 100 : Math.round(((sculpted + lifted) / denominator) * 1000) / 10;

console.log(`\nCLAY COVERAGE — sculpted clay vs flat utility floor`);
console.log(`  registry-sculpted: ${sculpted}`);
console.log(`  card-shaped foils: ${cardShaped}  (${ENGINE_LIFTS ? "ALL lifted by engine §9.7" : "NOT lifted — flat"})`);
console.log(`  wells (flat by design, not counted): ${wells}`);
console.log(`  COVERAGE: ${pct}%  (engine lift ${ENGINE_LIFTS ? "active" : "absent"})\n`);
if (lifted < cardShaped) {
  for (const row of perFile.filter((r) => r.cards > 0).sort((a, b) => b.cards - a.cards).slice(0, 16)) {
    console.log(`  cards ${String(row.cards).padStart(3)}  sculpted ${String(row.sculpted).padStart(3)}  ${row.file}`);
  }
  console.log("");
}
if (process.argv.includes("--check") && pct < 95) {
  console.error(`coverage ${pct}% is below the 95% gate`);
  process.exit(1);
}
