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
import { readFileSync } from "node:fs";
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
