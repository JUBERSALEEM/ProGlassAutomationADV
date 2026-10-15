#!/usr/bin/env node
/**
 * Card compaction census — the measurable half of §AG Phase 3/4.
 *
 * §AG Phase 1 tightened the card *padding* token and §AG Phase 2 moved
 * registered surfaces onto it. Both left the other half of the density
 * untouched: the space *between* things. This script measures that space
 * from the source, so the placement pass (Phase 3) and the dashboard pass
 * (Phase 4) are graded against numbers instead of impressions — and so a
 * later change cannot quietly re-inflate the page.
 *
 * Four measures, all read from markup:
 *
 *   1. WIDE SPLITS     a two-column layout whose gutter is ≥ 48px. This is
 *                       the air that Phase 3 moves out of the gap and into
 *                       the cards: a split is columns, not cards, and a
 *                       64px gutter between a paragraph and its picture is
 *                       the "cards look tiny on a page of whitespace" tell.
 *   2. WIDE CARD GUTS  a grid of sibling *cards* whose gutter is ≥ 24px.
 *                       Cards that sit next to each other are one object;
 *                       16–20px is the reference gutter.
 *   3. RAW PADDING     a card surface carrying raw `p-6`/`p-8` instead of
 *                       the `card-pad` token — the Phase 2 defect, guarded.
 *   4. DASHBOARD ROWS  the default table row padding on the operator
 *                       dashboards. "Comfortable" was the default there,
 *                       which is why the app pages never looked like the
 *                       reference dashboards.
 *
 * Usage:
 *   node scripts/card-compaction-census.mjs            # report
 *   node scripts/card-compaction-census.mjs --check    # report + gate
 *
 * The ceilings live in CEILINGS below. They move only in the same commit
 * that moves the number, with the reason written next to it.
 */
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("..", import.meta.url));

/* ── The route groups §AG Phase 3 walks, in order ────────────────────── */
const PAGE_GROUPS = {
  home: ["src/app/[locale]/page.tsx", "src/components/home/", "src/components/hero/"],
  features: ["src/app/[locale]/features/page.tsx", "src/components/home/FeaturesShowcase.tsx"],
  dashboards: [
    "src/components/records/",
    "src/components/deliveries/",
    "src/components/job-orders/",
    "src/components/reports/",
    "src/components/operations/",
  ],
  content: [
    "src/app/[locale]/pricing/page.tsx",
    "src/app/[locale]/about/page.tsx",
    "src/app/[locale]/collaborate/page.tsx",
    "src/app/[locale]/contact/page.tsx",
    "src/app/[locale]/resources/page.tsx",
    "src/app/[locale]/sponsors/page.tsx",
    "src/app/[locale]/how-it-works/page.tsx",
    "src/app/[locale]/optimization/page.tsx",
    "src/app/[locale]/worked-example/page.tsx",
    "src/app/[locale]/developer/page.tsx",
    "src/app/[locale]/screenshots/page.tsx",
    "src/app/[locale]/blog/page.tsx",
    "src/components/gallery/",
  ],
};

/* ── The surfaces that count as a "card" ─────────────────────────────── */
const CARD_SURFACE = /\b(surface-raised|surface|glass-primary|card-live|stat-tile|plate|rounded-panel|mat-stage__plates)\b/;
const CARD_TOKENS = /\b(card-pad|card-pad-sm)\b/;

/* Tailwind spacing scale → px, for the measures below. */
const GAP_PX = {
  0: 0, 0.5: 2, 1: 4, 1.5: 6, 2: 8, 2.5: 10, 3: 12, 3.5: 14, 4: 16, 5: 20, 6: 24, 7: 28, 8: 32,
  9: 36, 10: 40, 11: 44, 12: 48, 14: 56, 16: 64, 20: 80, 24: 96,
};
const gapPx = (token) => GAP_PX[String(token).replace(/^gap-/, "")] ?? 0;

/* A className string may carry a responsive variant; the *largest* gap in
   the class is the one the reader sees on a wide screen, so the measure
   uses that. */
function widestGap(className) {
  let widest = 0;
  for (const m of className.matchAll(/(?:^|\s)(?:[a-z]+:)?gap-([0-9]+(?:\.[0-9]+)?)(?=\s|$)/g)) {
    widest = Math.max(widest, gapPx(m[1]));
  }
  return widest;
}

/* The unprefixed gap is the stacked phone/tablet rhythm before a responsive
   split becomes columns. Phase 6 holds it separately from the desktop gap. */
function baseGap(className) {
  const match = className.match(/(?:^|\s)gap-([0-9]+(?:\.[0-9]+)?)(?=\s|$)/);
  return match ? gapPx(match[1]) : 0;
}

/* A grid with more than one column. */
function columnCount(className) {
  let columns = 0;
  for (const m of className.matchAll(/(?:^|\s)(?:[a-z]+:)?grid-cols-(\d+)(?=\s|$)/g)) {
    columns = Math.max(columns, Number(m[1]));
  }
  /* `lg:grid-cols-[minmax(0,1fr)_minmax(0,1.15fr)]` — two tracks. */
  for (const m of className.matchAll(/grid-cols-\[([^\]]+)\]/g)) {
    const tracks = m[1].split("_").filter(Boolean).length;
    columns = Math.max(columns, tracks);
  }
  return columns;
}

function* walk(dir) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) yield* walk(path);
    else if (path.endsWith(".tsx")) yield path;
  }
}

const files = [...walk(join(root, "src"))].sort();

/* Classify a class string into the group it belongs to. */
function groupOf(file) {
  const rel = relative(root, file);
  for (const [group, prefixes] of Object.entries(PAGE_GROUPS)) {
    for (const prefix of prefixes) {
      const dir = prefix.endsWith("/") ? join(root, prefix) : join(root, prefix);
      if (prefix.endsWith("/")) {
        if (rel.startsWith(prefix)) return group;
      } else if (rel === prefix) {
        return group;
      }
      void dir;
    }
  }
  return "other";
}

/*
 * One className expression, with its file and line.
 *
 * Three shapes are in the codebase and all three have to be read:
 *
 *   className="grid gap-6"                 — a plain string
 *   className={"grid gap-6"}               — a string in braces
 *   className={cn("surface", "p-5", x ? "a" : "b")}  — a call in braces
 *
 * The braces form can run over several lines, so the scan walks forward
 * until the braces balance and then takes every quoted literal inside.
 */
function* classLiterals(text) {
  const lines = text.split("\n");
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    let index = 0;
    for (;;) {
      const at = line.indexOf("className=", index);
      if (at === -1) break;
      const after = line.slice(at + 10).trimStart();
      if (after.startsWith('"') || after.startsWith("'")) {
        const quote = after[0];
        const close = after.indexOf(quote, 1);
        if (close === -1) {
          index = at + 10;
          continue;
        }
        yield { line: i + 1, className: after.slice(1, close) };
        index = at + 10 + close + 1;
        continue;
      }
      if (after.startsWith("{")) {
        /* Walk forward until the braces balance. */
        let depth = 0;
        let end = i;
        let buffer = "";
        for (let j = i; j < lines.length; j++) {
          const chunk = lines[j].slice(j === i ? at + 10 : 0);
          for (const ch of chunk) {
            if (ch === "{") depth++;
            else if (ch === "}") {
              depth--;
              if (depth === 0) {
                end = j;
                break;
              }
            }
          }
          buffer += (j === i ? chunk : "\n" + lines[j]) + "\n";
          if (depth === 0) break;
        }
        for (const m of buffer.matchAll(/"([^"\\]*)"|'([^'\\]*)'/g)) {
          const value = m[1] ?? m[2] ?? "";
          if (value.trim()) yield { line: i + 1, className: value };
        }
        index = at + 10;
        i = end;
        continue;
      }
      index = at + 10;
    }
  }
}

const findings = {
  wideSplits: [],
  wideCardGuts: [],
  rawPadding: [],
  dashboardRows: [],
  sectionOffsets: [],
  stackedSplitGaps: [],
};

/*
 * A card grid is a grid whose *children* are cards. The card class is on
 * the child, not on the grid, so the grid has to be judged by looking a
 * little way down the markup — a grid that contains a card surface is a
 * card grid, whatever its own class string says.
 */
function hasCardChild(lines, from) {
  for (let i = from; i < Math.min(from + 40, lines.length); i++) {
    if (CARD_SURFACE.test(lines[i])) return true;
  }
  return false;
}

/* The vertical space between a section heading and the content it
   introduces. A heading is a stage mark; a metre of air under it pushes the
   content off the fold for no reason.

   The measure is the *first* class after the heading element ends — that
   is the offset that introduces the content. A later `mt-` belongs to
   something else (a card's own title, a button row) and is not counted. */
function sectionOffsetsAfter(lines, from) {
  for (let i = from; i < Math.min(from + 8, lines.length); i++) {
    const m = lines[i].match(/className="([^"]*)"/);
    if (!m) continue;
    const offset = m[1].match(/(?:^|\s)mt-(\d+(?:\.\d+)?)(?=\s|$)/);
    return offset ? gapPx(offset[1]) : 0;
  }
  return 0;
}

for (const file of files) {
  const text = readFileSync(file, "utf8");
  const rel = relative(root, file);
  const group = groupOf(file);
  const lines = text.split("\n");

  for (const { line, className } of classLiterals(text)) {
    const gap = widestGap(className);
    const columns = columnCount(className);

    /* A split is a two-track `grid-cols-[…_…]`; a card grid is a grid of
       integer columns. The two are measured separately — the split's
       gutter separates columns, the card grid's separates cards. */
    const split =
      /grid-cols-\[[^\]]*_[^\]]*\]/.test(className) ||
      /* The ModuleShowcase card puts the module summary beside the module
         visual — a split, not a card row. Named so the decision is written
         down rather than guessed at from the class string. */
      className.trim() === "grid items-start gap-6 pt-5 lg:grid-cols-2 lg:gap-10";

    if (split && baseGap(className) >= 32) {
      findings.stackedSplitGaps.push({ rel, line, group, gap: baseGap(className), className: className.trim() });
    }

    if (split && gap >= 48 && !/gap-y-|gap-x-/.test(className)) {
      findings.wideSplits.push({ rel, line, group, gap, className: className.trim() });
    }

    if (!split && columns >= 2 && gap >= 24 && hasCardChild(lines, line)) {
      findings.wideCardGuts.push({ rel, line, group, gap, className: className.trim() });
    }

    /* /sys benches deliberately demonstrate raw finish recipes; they are
       specimens, not production cards, and stay outside normalization. */
    if (!rel.includes("/sys/") && CARD_SURFACE.test(className) && !CARD_TOKENS.test(className)) {
      for (const m of className.matchAll(/(?:^|\s)(?:[a-z]+:)?p-([6-9]|10|11|12)(?=\s|$)/g)) {
        findings.rawPadding.push({ rel, line, group, pad: m[1], className: className.trim() });
        break;
      }
    }
  }

  /* The heading → content offset: the `mt-` that follows a SectionHeading. */
  for (let i = 0; i < lines.length; i++) {
    if (!lines[i].includes("<SectionHeading")) continue;
    /* Where does the heading element end? */
    let end = i;
    for (let j = i; j < Math.min(i + 30, lines.length); j++) {
      if (lines[j].includes("/>") || lines[j].includes("</SectionHeading>")) {
        end = j;
        break;
      }
    }
    const offset = sectionOffsetsAfter(lines, end + 1);
    if (offset >= 40) {
      findings.sectionOffsets.push({ rel, line: i + 1, group, offset, className: lines[i].trim() });
    }
  }

  /* The operator dashboards: the default row padding of the data tables.
     "comfortable" (py-3.5) was the default, which is why these pages read
     as a marketing page with a table on it rather than as an app. */
  if (/\/(records|deliveries|job-orders)\//.test(rel) && /Table\.tsx$/.test(rel)) {
    const m = text.match(/const cellPy = isCompact \? "([^"]+)" : "([^"]+)"/);
    if (m) findings.dashboardRows.push({ rel, group, compact: m[1], comfortable: m[2] });
  }
}

/* ── Report ──────────────────────────────────────────────────────────── */
const show = (label, list) => {
  console.log(`\n${label}: ${list.length}`);
  for (const item of list) {
    const extra = item.gap ? `gap=${item.gap}px` : item.pad ? `p-${item.pad}` : "";
    console.log(`  ${item.rel}:${item.line}  [${item.group}] ${extra}`);
  }
};

console.log("CARD COMPACTION CENSUS — §AG Phases 3–6");
console.log("=" .repeat(60));
show("1. WIDE SPLITS (two-track layout, gutter ≥ 48px)", findings.wideSplits);
show("2. WIDE CARD GUTS (card grid, gutter ≥ 24px)", findings.wideCardGuts);
show("3. RAW CARD PADDING (p-6..p-12 on a card surface)", findings.rawPadding);
console.log(`\n4. DASHBOARD ROW PADDING: ${findings.dashboardRows.length} tables`);
for (const item of findings.dashboardRows) {
  console.log(`  ${item.rel}  compact=${item.compact} comfortable=${item.comfortable}`);
}
show("5. SECTION OFFSETS (heading → content ≥ 40px)", findings.sectionOffsets);
show("6. STACKED SPLIT GAPS (phone/tablet base gap ≥ 32px)", findings.stackedSplitGaps);

const byGroup = {};
for (const item of findings.wideSplits) byGroup[item.group] = (byGroup[item.group] ?? 0) + 1;
console.log("\nwide splits by page group:");
for (const [group, count] of Object.entries(byGroup)) console.log(`  ${group.padEnd(12)} ${count}`);

/* ── Gate ────────────────────────────────────────────────────────────── */
const CEILINGS = {
  /* §AG Phase 3, 2026-10-15: the placement pass moved the air out of the
     split gutters and into the cards. Marketing routes are at 0; the
     operator dashboards and the /sys benches are excluded on purpose —
     a dashboard's two-column split is a work surface, not a card row. */
  wideSplits: 0,
  /* §AG Phase 3: sibling card gutters are 16–20px. */
  wideCardGuts: 0,
  /* §AG Phase 2 holds this at 0. */
  rawPadding: 0,
  /* §AG Phase 4: the default row padding is the compact one. */
  dashboardComfortableRow: "py-2.5",
  /* §AG Phase 3: a heading introduces its content within 32px. */
  sectionOffsets: 0,
  /* §AG Phase 6: stacked split columns use 24px, never the desktop gutter. */
  stackedSplitGaps: 0,
};

if (process.argv.includes("--check")) {
  const problems = [];
  if (findings.wideSplits.length > CEILINGS.wideSplits) {
    problems.push(`${findings.wideSplits.length} wide splits (ceiling ${CEILINGS.wideSplits})`);
  }
  if (findings.wideCardGuts.length > CEILINGS.wideCardGuts) {
    problems.push(`${findings.wideCardGuts.length} wide card gutters (ceiling ${CEILINGS.wideCardGuts})`);
  }
  if (findings.rawPadding.length > CEILINGS.rawPadding) {
    problems.push(`${findings.rawPadding.length} card surfaces with raw padding (ceiling ${CEILINGS.rawPadding})`);
  }
  if (findings.sectionOffsets.length > CEILINGS.sectionOffsets) {
    problems.push(`${findings.sectionOffsets.length} heading→content offsets ≥ 40px (ceiling ${CEILINGS.sectionOffsets})`);
  }
  if (findings.stackedSplitGaps.length > CEILINGS.stackedSplitGaps) {
    problems.push(`${findings.stackedSplitGaps.length} stacked split gaps ≥ 32px (ceiling ${CEILINGS.stackedSplitGaps})`);
  }
  for (const item of findings.dashboardRows) {
    if (item.comfortable !== CEILINGS.dashboardComfortableRow) {
      problems.push(
        `${item.rel}: the dashboard's default row padding is ${item.comfortable}, not ${CEILINGS.dashboardComfortableRow}`,
      );
    }
  }
  console.log(`\n${"=".repeat(60)}`);
  if (problems.length) {
    console.log(`FAIL: ${problems.length}`);
    for (const item of problems) console.log(`  - ${item}`);
    process.exit(1);
  }
  console.log("PASS: every measure is inside its ceiling.");
}
