#!/usr/bin/env node
/* P06 — application-wide color-literal + shadow-syntax audit.
   Every color source is classified; the report fails on UNRESOLVED items.
   Buckets: token-declaration · accent-registry · social-svg (documented) ·
   inline-style (dynamic) · engine-literal (declaration class) · unresolved. */
import { readdirSync, readFileSync, statSync } from "node:fs";
import path from "node:path";

const SRC = new URL("../src", import.meta.url).pathname;
const files = [];
(function walk(d) {
  for (const e of readdirSync(d)) {
    const p = path.join(d, e);
    if (statSync(p).isDirectory()) walk(p);
    else if (/\.(tsx?|css)$/u.test(p)) files.push(p);
  }
})(SRC);

const HEX = /#[0-9a-fA-F]{6}\b/g;
const SHADOW_OK = /inset var\(--[\w-]+\)rgb\(|inset [a-z]:+var\(--/g;
const problems = [];
const tally = { tokenDecl: 0, accentRegistry: 0, socialOg: 0, inlineStyle: 0, cssRule: 0, cssTokenValue: 0 };

for (const f of files) {
  const rel = f.replace(SRC + "/", "");
  const raw = readFileSync(f, "utf8");
  /* comment-stripped working copy (doc strings quoting colors are not colors) */
  const t = raw.replace(/\/\*[\s\S]*?\*\//gu, "");

  const lines = t.split("\n");

  /* 1 · shadow-item syntax guard (P05 lesson): a color var immediately
     followed by an rgb() color is a malformed item — never allowed. */
  for (const [i, line] of lines.entries()) {
    if (/\)rgb\(/u.test(line)) {
      problems.push(`${rel}:${i + 1} malformed shadow item: ${line.trim().slice(0, 90)}`);
    }
    const insetGeo = /(inset|-?[0-9.]+px|[0-9]+|var\(--[\w-]+\))rgb\(/u;
    if (/box-shadow/u.test(line) && insetGeo.test(line) && !/var\(--[\w-]+\)\s*$/u.test(line)) {
      /* shadow with a bare color glued to a var — grammar */
      if (/(inset|-?[0-9.]+px [0-9.]+px [0-9.]+px)[ ,]?var\(--[a-z-]+\)rgb/u.test(line)) {
        problems.push(`${rel}:${i + 1} malformed shadow geometry: ${line.trim().slice(0, 90)}`);
      }
    }
  }

  /* 2 · literal hex census + classification */
  for (const m of t.matchAll(HEX)) {
    const upto = t.slice(0, m.index);
    const lineNo = upto.split("\n").length;
    const line = lines[lineNo - 1] ?? "";
    if (/^\s*--[\w-]+:/u.test(line)) { tally.tokenDecl++; continue; }              /* token declaration = home */
    if (/api\/og/u.test(rel)) { tally.socialOg++; continue; }                       /* ImageResponse SVG — documented */
        const printLine = raw.split("\n").findIndex((l) => /@media print/u.test(l));
        if (printLine >= 0 && lineNo > printLine) { tally.socialOg++; continue; } /* print paper grade - documented */
    if (/themeColor|color: "/u.test(line) && /layout/u.test(rel)) { tally.accentRegistry++; continue; } /* PWA theme-color meta — documented */
    if (/money(Positive|Negative)[:="]/u.test(line) && /useStudio/u.test(rel)) { tally.accentRegistry++; continue; } /* money palette static defaults */
    if (/(key|hex): "/u.test(line) && /ThemeStudio/u.test(rel)) { tally.accentRegistry++; continue; } /* the plates' own input data */
    /* registries & documented whites: plate/brush palette DATA (what the
       tokens must paint), the logo marks' specular stops, the studio's
       own default picks — never presentation escapes. */
    if (/data\/(themes|activityLog)\.ts/u.test(rel) || /SvgDefs/u.test(rel) || /operations-pdf/u.test(rel)) { tally.accentRegistry++; continue; }
    if (/useState\("#/u.test(line) || /\?\? "#/u.test(line) || /\|\| "#/u.test(line)) { tally.accentRegistry++; continue; }
    if (/style=/u.test(line) || /React.CSSProperties/u.test(line)) { tally.inlineStyle++; continue; }
    if (rel.endsWith(".css")) {
      tally.cssRule++;
      problems.push(`${rel}:${lineNo} unbucketed css literal ${m[0]}: ${line.trim().slice(0, 80)}`);
      continue;
    }
    tally.cssTokenValue++;
    problems.push(`${rel}:${lineNo} unbucketed TSX literal ${m[0]}: ${line.trim().slice(0, 80)}`);
  }
}

console.log("color-literal census:");
for (const [k, v] of Object.entries(tally)) console.log(`  ${k}: ${v}`);
console.log(`# unresolved problems: ${problems.length}`);
for (const p of problems.slice(0, 24)) console.log(" •", p);
process.exitCode = problems.length ? 1 : 0;
