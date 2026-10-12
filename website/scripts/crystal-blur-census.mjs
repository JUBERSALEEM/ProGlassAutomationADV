#!/usr/bin/env node
/*
 * crystal-blur-census (G6 · performance certification, S5)
 *
 * What the glasses a sheet carries are the GPU's — every backdrop-filter
 * is a readback of the page behind it, per layer, per frame, and G1/G2/G3
 * + G5 put them everywhere a carrier sits. The honest question is not "is
 * blur slow?" (it costed 26 route-paints at the clay harness without a
 * complaint) but "how MANY layers does each route ask for, at most?" —
 * the number a low-end GPU meets, route by route, and thus the number a
 * budget gate has to hold still so nobody rewrites a quiet page into a
 * stutter over lunch.
 *
 * Method: a route's raw HTML is crawled and every card-shaped carrier the
 * §C1/§C2/§C3 grammar matches — bg-ink-* / bg-white / bg-line /
 * bg-glass-*, chip / eyebrow-chip / form fields — is counted as a
 * backdrop-filter surface. Raw-class counting is exact for lift lanes
 * (baseline), conservative for the nested (cards inside cards — the count
 * is the count, stacked or not). It is not a mobile timing proof; the
 * timing proof stays a device job (and the budget's whole argument is
 * that the count does not grow between audits).
 *
 * Conventions: the measured-trail comment convention from audit-site.py —
 * every budget line records the reading it defends, and a bump moves in
 * the same commit as the bumping change.
 *
 * Usage:
 *   node scripts/crystal-blur-census.mjs [base-url] [--json] [--budget]
 */

import http from "node:http";
import process from "node:process";

const BASE = process.argv.find((a) => a.startsWith("http")) ?? "http://127.0.0.1:3000";
const AS_JSON = process.argv.includes("--json");
const CHECK_BUDGET = process.argv.includes("--budget");

/* The audit's crawl list — a census of fewer routes than the audit's would
   silently miss the blog; keep these copies in step. */
const PAGES = [
  "/", "/features", "/optimization", "/how-it-works", "/screenshots", "/pricing",
  "/about", "/developer", "/sponsors", "/collaborate", "/contact", "/resources",
  "/blog", "/worked-example",
  "/blog/why-the-price-basis-is-stamped", "/blog/guillotine-versus-free-nesting",
  "/blog/why-delivery-balance-is-hard",
  "/legal/privacy", "/legal/terms", "/legal/license",
  "/reports", "/job-orders", "/deliveries",
];

/* The route that carries the most — the budget lives at the max, not the
   median. Measured 2026-10-12 (G6 census): /features = 522 surfaces
   (glass lift + chips + form fields + table wells, nested counts counted
   nested — the stack is the cost, not a bug). Budgets are ceilings 'a
   little above the figure the site actually ships' (the audit convention)
   with a measured trail, not plans: 522 → 600. The coarse-pointer radius
   step-down (§C6) ships with this census as the engine half of the
   answer; the census is the census half. */
const BUDGET = 600;

const LIFTED_CLASS = /(?:^|\s)(?:bg-ink-[^\s]*|bg-white|bg-line|bg-glass-[^\s]*)/u;
const CHIP_CLASS = /(?:^|\s)(?:chip|eyebrow-chip)(?:\s|$|:)/u;
const FIELD_TAG = /<(input|textarea|select)\b/gu;
const TABLE_WELL = /<table\b/gu;

const getLine = (html, clsRe) => {
  const attrs = [...html.matchAll(/class="([^"]*)"/gu)].map((m) => m[1]);
  return attrs.length ? attrs.filter((v) => clsRe.test(v)).length : 0;
};

const fetchHtml = (route) =>
  new Promise((resolve, reject) => {
    http
      .get(BASE + route, (res) => {
        if (res.statusCode !== 200) {
          reject(new Error(`${route}: HTTP ${res.statusCode}`));
          return;
        }
        let body = "";
        res.on("data", (d) => (body += d));
        res.on("end", () => resolve(body));
      })
      .on("error", reject);
  });

const rows = [];
let worst = { route: "/", count: 0 };

for (const route of PAGES) {
  const html = await fetchHtml(route);
  const lifted = getLine(html, LIFTED_CLASS);
  const chips = getLine(html, CHIP_CLASS);
  const fields = (html.match(FIELD_TAG) ?? []).length;
  const tables = (html.match(TABLE_WELL) ?? []).length;
  /* the courts the GPU actually pays: lifted carriers + chips (their own
     backdrop-filter) + each form field + one per table well (thead row
     chills). Nested lift simply counts — the stack is the cost. */
  const surfaces = lifted + chips + fields + tables;
  rows.push({ route, lifted, chips, fields, tables, surfaces });
  if (surfaces > worst.count) worst = { route, count: surfaces };
}

if (AS_JSON) {
  console.log(JSON.stringify({ routes: rows, worst, budget: BUDGET }, null, 2));
} else {
  const pad = (n, w = 5) => String(n).padStart(w);
  console.log("   route" + " ".repeat(38) + "lift  chip  field table  TOTAL");
  for (const r of rows) {
    console.log(
      `   ${r.route.padEnd(42)} ${pad(r.lifted)} ${pad(r.chips)} ${pad(r.fields)} ${pad(r.tables)} ${pad(r.surfaces)}`,
    );
  }
  console.log(`\n   PAGES ${rows.length} · worst ${worst.route} = ${worst.count} surfaces · budget ${BUDGET}`);
}

if (CHECK_BUDGET && worst.count > BUDGET) {
  console.error(`\ncrystal-blur-census: ${worst.route} wants ${worst.count} blurred surfaces — over the ${BUDGET} cap.`);
  process.exit(1);
}
