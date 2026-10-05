/**
 * `npm run fix` — repair a website/ copy that will not build.
 *
 * One command, no flags, for the state this tree most often gets into:
 *
 *   1. node_modules is missing        → Node from npx's cache starts, then dies
 *                                       on the first import it cannot resolve,
 *                                       naming that import instead of the cause;
 *   2. some file imports a package     → installing cannot fix it; the file is
 *      package.json does not declare     from an older revision of the tree;
 *   3. .next holds a stale build       → a dev server can replay the module
 *                                       graph it compiled earlier, even after
 *                                       the file it came from has changed.
 *
 * Steps 1 and 3 are repaired here. Step 2 is reported and left alone: only you
 * know whether that import is wanted.
 *
 * Cross-platform on purpose — Windows included, since that is where the desktop
 * application runs. Nothing outside Node's own standard library is used, so
 * this works when node_modules is empty, which is exactly when it is needed.
 */
import { existsSync, rmSync } from "node:fs";
import { execFileSync, spawnSync } from "node:child_process";
import path from "node:path";
import { inspectDeps, formatProblems } from "./dep-check.mjs";

const ROOT = path.resolve(import.meta.dirname, "..");
const NEXT_DIR = path.join(ROOT, ".next");

const say = (line = "") => console.log(line);

say();
say("  website repair");
say("  " + "-".repeat(60));

// ── 1 · Which copy is this? ────────────────────────────────────────────────
let revision = "(no git history here — this is not a checkout)";
try {
  const line = execFileSync("git", ["log", "-1", "--format=%h  %s"], {
    cwd: ROOT,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"],
  }).trim();
  if (line) revision = line;
} catch {
  // Not a git checkout. The default above explains that.
}
say(`  revision    ${revision}`);

// ── 2 · Dependencies ───────────────────────────────────────────────────────
let { missing, undeclared } = inspectDeps(ROOT);

if (missing.length > 0) {
  const lockfile = existsSync(path.join(ROOT, "package-lock.json"));
  const command = lockfile ? ["ci"] : ["install"];
  say(`  packages    ${missing.length} missing — running \`npm ${command.join(" ")}\``);
  say();

  const result = spawnSync("npm", command, {
    cwd: ROOT,
    stdio: "inherit",
    shell: true, // resolves npm.cmd on Windows without a shell-specific path
  });

  if (result.status !== 0) {
    say();
    say("  The install did not finish. That is the one thing this script cannot");
    say("  work around: the build needs these packages. Check the network, then");
    say("  run `npm ci` here yourself and read the first error npm prints.");
    say();
    process.exit(1);
  }

  say();
  const after = inspectDeps(ROOT);
  missing = after.missing;
  undeclared = after.undeclared;
  say(
    missing.length === 0
      ? "  packages    installed"
      : `  packages    still missing: ${missing.join(", ")}`,
  );
} else {
  say("  packages    all declared dependencies are installed");
}

// ── 3 · Imports nothing declares — reported, never guessed at ─────────────
if (undeclared.length > 0) {
  say();
  process.stderr.write(formatProblems({ missing: [], undeclared }, ROOT));
  say("  Nothing above was changed: an import nobody declares is either a file");
  say("  from an older revision or a dependency that was removed on purpose.");
  say();
  process.exit(1);
}

// ── 4 · The build cache ────────────────────────────────────────────────────
if (existsSync(NEXT_DIR)) {
  rmSync(NEXT_DIR, { recursive: true, force: true });
  say("  cache       .next cleared (Next rebuilds it on the next run)");
} else {
  say("  cache       no .next to clear");
}

// ── 5 · Done ───────────────────────────────────────────────────────────────
say("  " + "-".repeat(60));
say();
say("  The tree is repaired. Start it again:");
say();
say("      npm run dev        # then reload the browser tab completely");
say("      npm run build && npm start        # production build");
say();
say("  Reload the tab rather than reusing it: a development overlay is drawn by");
say("  the page that hit the error and stays on screen until the page is");
say("  reloaded, even once the cause is gone.");
say();
