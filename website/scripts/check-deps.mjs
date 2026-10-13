/**
 * `npm run dev`, `npm run build` and `npm run doctor` all land here.
 *
 * The check itself lives in scripts/dep-check.mjs, because next.config.ts runs
 * the same one — that is what catches `npx next build`, which never runs an npm
 * script. This file only decides what to print and what to exit with:
 *
 *   npm run doctor        always prints the state of the tree
 *   npm run dev|build     prints nothing when the tree is fine, and the remedy
 *                         with a non-zero exit when it is not
 */
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import { inspectDeps, formatProblems } from "./dep-check.mjs";

const ROOT = path.resolve(import.meta.dirname, "..");
const report = process.argv.includes("--report");

const problems = inspectDeps(ROOT);
const unhappy = problems.missing.length > 0 || problems.undeclared.length > 0;

if (unhappy) {
  process.stderr.write(formatProblems(problems, ROOT));
  process.exit(1);
}

// Generated files the build imports. `src/i18n/coverage-table.json` feeds the
// language menu, and `src/i18n/route-overlays.json` feeds the per-route
// translation overlays — both are written by `npm run i18n:keys`; a stock `.gitignore`
// code-coverage rule (`coverage*.json`) once swallowed it so a clean checkout
// built fine locally and failed on Vercel with a module-not-found two pages
// deep into Turbopack's resolver. Naming the missing file here is the message
// that failure should have been.
const absentGenerated = ["src/i18n/coverage-table.json", "src/i18n/route-overlays.json"].filter(
  (file) => !existsSync(path.join(ROOT, file)),
);
if (absentGenerated.length > 0) {
  process.stderr.write(
    `\n  Generated files are missing from the checkout:\n\n${absentGenerated
      .map((file) => `    ✗ ${file}`)
      .join("\n")}\n\n  They are produced by the i18n pipeline and committed to the repository —\n  regenerate them with:\n\n    npm run i18n:keys\n\n`,
  );
  process.exit(1);
}

if (!report) process.exit(0);

// ── npm run doctor ──────────────────────────────────────────────────────────
const manifest = JSON.parse(readFileSync(path.join(ROOT, "package.json"), "utf8"));
const count = Object.keys({ ...manifest.dependencies, ...manifest.devDependencies }).length;

let revision = "(not a git checkout)";
try {
  const sha = execFileSync("git", ["log", "-1", "--format=%h  %s"], {
    cwd: ROOT,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"],
  }).trim();
  if (sha) revision = sha;
} catch {
  // No git, or no repository. The line above already says so.
}

console.log("");
console.log("  website dependency check");
console.log("");
console.log(`    revision   ${revision}`);
console.log(`    installed  all ${count} declared dependencies present`);
console.log("    imports    no import of a package package.json does not declare");
console.log("");
console.log("  The tree is ready: `npm run dev` or `npm run build`.");
console.log("");
