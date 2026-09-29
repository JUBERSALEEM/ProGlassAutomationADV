/**
 * Dependency guard.
 *
 * The failure this exists for: someone clones the repository, runs
 * `npx next build` (or `npx next dev`) without ever running `npm install`, and
 * gets a "Module not found" error from deep inside a page — naming an import
 * rather than the actual cause, which is an empty node_modules. That message
 * sends people looking at the source file instead of at their terminal.
 *
 * So the checks below run before `dev` and before `build` (see package.json)
 * and say what to do instead:
 *
 *   `next` present?      the project's own Next, not a version npx fetched
 *   `react` present?     the framework itself, and its DOM renderer
 *   `sharp` present?     the asset pipeline's encoder
 *   lockfile present?    so the same tree is installed every time
 *
 * It is deliberately cheap — four `existsSync` calls — and it is not a
 * substitute for a real install: it only tells you when there is nothing to
 * build with. Version pinning is npm's job, not this script's.
 */
import { existsSync } from "node:fs";
import path from "node:path";

const ROOT = path.resolve(import.meta.dirname, "..");
const MODULES = path.join(ROOT, "node_modules");

/** Packages that must be present for a dev run or a build to work at all. */
const REQUIRED = [
  { name: "next", why: "the framework this project builds on" },
  { name: "react", why: "the component runtime Next renders" },
  { name: "react-dom", why: "the DOM renderer Next renders with" },
  { name: "sharp", why: "the image pipeline behind scripts/optimize-assets.mjs" },
];

const missing = REQUIRED.filter((entry) => !existsSync(path.join(MODULES, entry.name, "package.json")));

if (missing.length === 0) {
  process.exit(0);
}

const lockfile = existsSync(path.join(ROOT, "package-lock.json"));

console.error("");
console.error("  Dependencies are not installed.");
console.error("");
for (const entry of missing) {
  console.error(`    · ${entry.name} — missing (${entry.why})`);
}
console.error("");
console.error(`  Run this first, from ${path.relative(process.cwd(), ROOT) || "."} :`);
console.error("");
console.error(`    ${lockfile ? "npm ci" : "npm install"}`);
console.error("");
console.error(
  lockfile
    ? "  `npm ci` installs the exact tree in package-lock.json, which is what the\n" +
        "  deployment builds with too. If you have no internet access, `npm install\n" +
        "  --offline` will use the local cache."
    : "  No package-lock.json was found; `npm install` will resolve the ranges in\n" +
        "  package.json. Committing the lockfile is what makes a deploy repeatable.",
);
console.error("");
console.error("  Do not run `npx next …` directly: that fetches a copy of Next into a");
console.error("  cache and leaves the project's own dependencies missing, which is where a");
console.error("  \"Module not found: Can't resolve …\" build error comes from.");
console.error("");

process.exit(1);
