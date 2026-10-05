/**
 * Build-cache freshness guard.
 *
 * Why this exists: `.next` is a cache, and in development Next keeps server
 * output in it between runs. Pull a newer revision while a dev server is
 * running — or pull and start one over a cache left by an older revision — and
 * you can end up serving HTML rendered from one revision of a component while
 * the browser loads client JavaScript compiled from the next. React then finds
 * markup that does not match what it just rendered and reports a hydration
 * mismatch: the class names in the error are one revision's strings on one side
 * and the other revision's strings on the other, and the component looks correct
 * in the source either way.
 *
 * That failure was hit once already, on `Website/Logo.tsx` — the server sent the
 * old `text-[0.5rem]` while the client had the new `text-[0.6875rem]` — so the
 * guard is here rather than in a note somewhere.
 *
 * What it does is small and deliberate: after every build it records the commit
 * that produced the build in `.next/build-commit`, and before `next dev` starts
 * it compares that marker with the commit currently checked out. If they differ,
 * the cache is stale by definition, so it is removed and the dev server rebuilds
 * from the working tree. Deleting `.next` is safe here for one reason only: the
 * command that follows is `next dev`, which rebuilds what it needs. This is
 * wired to `predev`, and deliberately *not* to `prestart`, where removing the
 * build would take a working site down.
 *
 * Without git — an archive, a container, a CI checkout with no history — the
 * guard does nothing at all. It is a convenience, not a dependency.
 */
import { existsSync, rmSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";

const NEXT_DIR = path.resolve(process.cwd(), ".next");
const MARKER = path.join(NEXT_DIR, "build-commit");

const action = process.argv[2] === "record" ? "record" : "check";

/** The commit currently checked out, or null when git cannot answer. */
function currentCommit() {
  try {
    const out = execFileSync("git", ["rev-parse", "HEAD"], {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"],
    });
    const sha = out.trim();
    return sha || null;
  } catch {
    // No git, no repository, or git refused. Say nothing and do nothing.
    return null;
  }
}

if (action === "record") {
  if (!existsSync(NEXT_DIR)) process.exit(0);
  const sha = currentCommit();
  if (!sha) process.exit(0);
  writeFileSync(MARKER, `${sha}\n`);
  console.log(`  build cache marked as ${sha.slice(0, 7)}`);
  process.exit(0);
}

// ── The check ───────────────────────────────────────────────────────────────
// Nothing built yet: there is no cache to be stale, and next dev is about to
// create one.
if (!existsSync(MARKER)) process.exit(0);

const sha = currentCommit();
if (!sha) process.exit(0);

let built = "";
try {
  built = readFileSync(MARKER, "utf8").trim();
} catch {
  process.exit(0);
}

if (built === sha) process.exit(0);

console.log(
  [
    "",
    "  ⚠ The .next cache was built from a different revision.",
    `      built from  ${built.slice(0, 7)}`,
    `      checked out ${sha.slice(0, 7)}`,
    "    Serving that cache can hydrate fresh JavaScript over stale HTML, which",
    "    React reports as a hydration mismatch. Clearing .next so the dev server",
    "    rebuilds from the working tree.",
    "",
  ].join("\n"),
);

rmSync(NEXT_DIR, { recursive: true, force: true });
