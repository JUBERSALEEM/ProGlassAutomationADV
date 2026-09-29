/**
 * The dependency check itself, kept in one place because two things call it:
 *
 *   scripts/check-deps.mjs  — runs from `npm run dev`, `npm run build` and
 *                             `npm run doctor`;
 *   next.config.ts          — runs however Next was started, including
 *                             `npx next build`, which bypasses the npm scripts
 *                             entirely and is the path a "Module not found:
 *                             Can't resolve …" error usually arrives by.
 *
 * It answers two questions and nothing else:
 *
 *   1. Is every dependency package.json declares actually installed?
 *      That is the common case: `npx next build` on a fresh clone runs Next
 *      from npx's cache while the project's own node_modules is empty, so the
 *      build dies on the first import and names that import — marked, when the
 *      blog still used it — rather than the missing install.
 *
 *   2. Does any file under src/ import a package package.json does not declare?
 *      That is the stale-file case: an import left behind by an older revision
 *      of the tree, which installing cannot satisfy and which therefore shows
 *      up as the same cryptic error however often you reinstall.
 *
 * Nothing here is a substitute for a real install, and nothing here pins
 * versions — that is npm's job. It only says, in words, when there is nothing
 * to build with and why.
 */
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { builtinModules } from "node:module";
import path from "node:path";

/** Why a package is here, for the packages where "missing" is not obvious. */
const WHY = {
  next: "the framework this project builds on",
  react: "the component runtime Next renders",
  "react-dom": "the DOM renderer Next renders with",
  sharp: "the image pipeline behind scripts/optimize-assets.mjs",
  typescript: "the type check, `npm run typecheck`",
  eslint: "the lint pass, `npm run lint`",
};

/**
 * Import specifiers, each anchored on the import/export keyword and bounded by
 * the clause that can hold the specifier, so a sentence inside a doc comment or
 * a string cannot match:
 *
 *   import x from "pkg"      import { a, b } from "pkg" (also multi-line)
 *   import type { T } from   import * as ns from      import "pkg"
 *   export { a } from        export * from            import("pkg")
 */
const IMPORT_PATTERNS = [
  /^[ \t]*import[ \t]+(?:type[ \t]+)?(?:[\w$]+[ \t]*,[ \t]*)?(?:\{[^}]*\}|[\w$]+|\*[ \t]+as[ \t]+[\w$]+)[ \t]*from[ \t]*["']([^"']+)["']/gm,
  /^[ \t]*import[ \t]*["']([^"']+)["']/gm,
  /^[ \t]*export[ \t]+(?:\{[^}]*\}|\*[ \t]*(?:as[ \t]+[\w$]+)?)[ \t]*from[ \t]*["']([^"']+)["']/gm,
  /\bimport[ \t]*\([ \t]*["']([^"']+)["']/g,
];

/** Bare file extensions worth reading. Anything else cannot hold an import. */
const SOURCE_FILE = /\.(ts|tsx|mts)$/;

const builtins = new Set([...builtinModules, ...builtinModules.map((name) => `node:${name}`)]);

/** Every .ts/.tsx file under a directory, recursively. */
function sourceFiles(dir) {
  let files = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) files = files.concat(sourceFiles(full));
    else if (SOURCE_FILE.test(entry.name)) files.push(full);
  }
  return files;
}

/** The bare package name a specifier belongs to: `next/link` → `next`. */
function packageName(specifier) {
  return specifier.startsWith("@")
    ? specifier.split("/").slice(0, 2).join("/")
    : specifier.split("/")[0];
}

/**
 * @param {string} root  the website/ directory — whatever Next was started in.
 * @returns {{ missing: string[], undeclared: Array<[string, string]> }}
 */
export function inspectDeps(root) {
  const manifestPath = path.join(root, "package.json");
  if (!existsSync(manifestPath)) return { missing: [], undeclared: [] };

  const manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
  const declared = { ...manifest.dependencies, ...manifest.devDependencies };
  const modules = path.join(root, "node_modules");

  const missing = Object.keys(declared).filter(
    (name) => !existsSync(path.join(modules, name, "package.json")),
  );

  // The import scan runs only when the tree is installed: with node_modules
  // empty, every import looks undeclared and the install message is the useful
  // one.
  const undeclared = [];
  if (missing.length === 0) {
    const src = path.join(root, "src");
    for (const file of existsSync(src) ? sourceFiles(src) : []) {
      const text = readFileSync(file, "utf8");
      for (const pattern of IMPORT_PATTERNS) {
        for (const match of text.matchAll(pattern)) {
          const specifier = match[1];
          if (!specifier || specifier.startsWith(".") || specifier.startsWith("@/")) continue;
          const name = packageName(specifier);
          if (builtins.has(name) || declared[name]) continue;
          const where = path.relative(root, file);
          if (!undeclared.some(([pkg, at]) => pkg === name && at === where)) {
            undeclared.push([name, where]);
          }
        }
      }
    }
  }

  return { missing, undeclared };
}

/** The human answer to the problems above. Ends with a newline. */
export function formatProblems(problems, root) {
  const lines = [];
  const lockfile = existsSync(path.join(root, "package-lock.json"));

  lines.push("");

  if (problems.missing.length > 0) {
    lines.push("  Dependencies are not installed.");
    lines.push("");
    for (const name of problems.missing) {
      lines.push(`    · ${name}${WHY[name] ? ` — missing (${WHY[name]})` : " — missing"}`);
    }
    lines.push("");
    lines.push("  Run this first, from the website/ directory:");
    lines.push("");
    lines.push(`    ${lockfile ? "npm ci" : "npm install"}`);
    lines.push("");
    lines.push(
      lockfile
        ? "  `npm ci` installs the exact tree in package-lock.json, which is what the"
        : "  No package-lock.json was found; `npm install` will resolve the ranges in",
    );
    lines.push(
      lockfile
        ? "  deployment builds with too. If you have no internet access, `npm install"
        : "  package.json. Committing the lockfile is what makes a deploy repeatable.",
    );
    if (lockfile) lines.push("  --offline` will use the local cache.");
    lines.push("");
  }

  if (problems.undeclared.length > 0) {
    lines.push("  Source code imports something this project does not depend on.");
    lines.push("");
    for (const [name, where] of problems.undeclared) {
      lines.push(`    · ${name} — imported by ${where}`);
    }
    lines.push("");
    lines.push("  Installing packages cannot fix this: nothing declares that package.");
    lines.push("  Either the file is from an older revision of this tree — `git log");
    lines.push("  --oneline -1` says which copy you have — or the dependency was removed");
    lines.push("  and this import is what is left of it. Remove the import, or add the");
    lines.push("  package with `npm install <name>` if it is genuinely wanted.");
    lines.push("");
  }

  lines.push("  Do not run `npx next …` directly: that fetches a copy of Next into a");
  lines.push("  cache and leaves the project's own dependencies missing, which is where a");
  lines.push('  "Module not found: Can\'t resolve …" build error comes from.');
  lines.push("");

  return lines.join("\n");
}
