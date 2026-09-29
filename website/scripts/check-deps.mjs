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
 *   1. every dependency package.json declares is present in node_modules;
 *   2. no file under src/ imports a package the project does not declare.
 *
 * The first catches the common case — nothing installed — and the second
 * catches a stale file: an import left over from an older revision of the tree,
 * which no amount of installing will satisfy.
 *
 * It is deliberately cheap — a handful of `existsSync` calls and one pass over
 * src/ — and it is not a substitute for a real install: it only tells you when
 * there is nothing to build with. Version pinning is npm's job, not this
 * script's.
 */
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { builtinModules } from "node:module";
import path from "node:path";

const ROOT = path.resolve(import.meta.dirname, "..");
const MODULES = path.join(ROOT, "node_modules");

/** What the project says it needs, and what it needs it for. */
const manifest = JSON.parse(readFileSync(path.join(ROOT, "package.json"), "utf8"));
const declared = { ...manifest.dependencies, ...manifest.devDependencies };

const WHY = {
  next: "the framework this project builds on",
  react: "the component runtime Next renders",
  "react-dom": "the DOM renderer Next renders with",
  sharp: "the image pipeline behind scripts/optimize-assets.mjs",
  typescript: "the type check, `npm run typecheck`",
  eslint: "the lint pass, `npm run lint`",
};

const missing = Object.keys(declared).filter(
  (name) => !existsSync(path.join(MODULES, name, "package.json")),
);

// ── Imports that nothing in package.json can satisfy ────────────────────────
// Walked only when the tree is installed (otherwise every import looks broken
// and the message below is the useful one).
const builtins = new Set([...builtinModules, ...builtinModules.map((m) => `node:${m}`)]);
const undeclared = new Map();

if (missing.length === 0) {
  const walk = (dir) => {
    let out = [];
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) out = out.concat(walk(full));
      else if (/\.(ts|tsx|mts)$/.test(entry.name)) out.push(full);
    }
    return out;
  };

  const src = path.join(ROOT, "src");
  const files = existsSync(src) ? walk(src) : [];
  // Each pattern is anchored on the import/export keyword and bounded by the
  // clause that can hold the specifier, so a sentence in prose or a doc string
  // cannot match:
  //   import x from "pkg"      import { a, b } from "pkg" (also multi-line)
  //   import type { T } from   import * as ns from      import "pkg"
  //   export { a } from        export * from            import("pkg")
  const patterns = [
    /^[ \t]*import[ \t]+(?:type[ \t]+)?(?:[\w$]+[ \t]*,[ \t]*)?(?:\{[^}]*\}|[\w$]+|\*[ \t]+as[ \t]+[\w$]+)[ \t]*from[ \t]*["']([^"']+)["']/gm,
    /^[ \t]*import[ \t]*["']([^"']+)["']/gm,
    /^[ \t]*export[ \t]+(?:\{[^}]*\}|\*[ \t]*(?:as[ \t]+[\w$]+)?)[ \t]*from[ \t]*["']([^"']+)["']/gm,
    /\bimport[ \t]*\([ \t]*["']([^"']+)["']/g,
  ];

  for (const file of files) {
    const text = readFileSync(file, "utf8");
    for (const pattern of patterns) {
      for (const match of text.matchAll(pattern)) {
        const spec = match[1];
        if (!spec || spec.startsWith(".") || spec.startsWith("@/") || spec.startsWith("node:")) {
          continue;
        }
        const pkg = spec.startsWith("@")
          ? spec.split("/").slice(0, 2).join("/")
          : spec.split("/")[0];
        if (builtins.has(pkg) || declared[pkg]) continue;
        if (!undeclared.has(pkg)) undeclared.set(pkg, path.relative(ROOT, file));
      }
    }
  }
}

if (missing.length === 0 && undeclared.size === 0) {
  process.exit(0);
}

console.error("");

if (missing.length > 0) {
  const lockfile = existsSync(path.join(ROOT, "package-lock.json"));
  console.error("  Dependencies are not installed.");
  console.error("");
  for (const name of missing) {
    console.error(`    · ${name}${WHY[name] ? ` — missing (${WHY[name]})` : " — missing"}`);
  }
  console.error("");
  console.error("  Run this first, from the website/ directory:");
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
}

if (undeclared.size > 0) {
  console.error("  Source code imports something this project does not depend on.");
  console.error("");
  for (const [pkg, file] of undeclared) {
    console.error(`    · ${pkg} — imported by ${file}`);
  }
  console.error("");
  console.error("  Installing packages cannot fix this: nothing declares that package.");
  console.error("  Either the file is from an older revision of this tree — `git log");
  console.error("  --oneline -1` says which copy you have — or the dependency was removed");
  console.error("  and this import is what is left of it. Remove the import, or add the");
  console.error("  package with `npm install <name>` if it is genuinely wanted.");
  console.error("");
}

console.error("  Do not run `npx next …` directly: that fetches a copy of Next into a");
console.error("  cache and leaves the project's own dependencies missing, which is where a");
console.error("  \"Module not found: Can't resolve …\" build error comes from.");
console.error("");

process.exit(1);
