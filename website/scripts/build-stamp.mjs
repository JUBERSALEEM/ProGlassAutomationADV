/**
 * The build stamp: when a build was made, and from what revision.
 *
 * The site is deployed from one repository to more than one host, and each host
 * keeps its own build. Two deploys of the same branch therefore differ by *when*
 * they were made, and nothing on the site said which build a visitor was
 * looking at — so a preview and the live site could disagree and neither would
 * admit it. The stamp answers the question directly: a date, a time in UTC, the
 * revision the build came from, and where the build ran.
 *
 * How it reaches the page: `next.config.ts` calls this and publishes the result
 * through Next's `env` setting, which substitutes the values into the compiled
 * bundle as literals. That matters — a literal is fixed at build time, so a
 * statically rendered page and a function that starts cold hours later report
 * the same stamp. Reading the clock at request time would make the stamp a lie
 * on exactly the pages that render on demand.
 *
 * Nothing here is required to be present. Without git — an archive, a CI
 * checkout with no history, a container — the revision is simply unknown, and
 * the stamp still carries a time. It never fails a build.
 *
 * Run it on its own to see what a build would report:
 *
 *     node scripts/build-stamp.mjs
 */
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const websiteRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

/** A trimmed environment value, or null when it is unset or empty. */
function env(name) {
  const value = process.env[name];
  return typeof value === "string" && value.trim() !== "" ? value.trim() : null;
}

/** Ask git a question. Null when git is missing, or the tree is not a repository. */
function git(args) {
  try {
    const out = execFileSync("git", args, {
      cwd: websiteRoot,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"],
    });
    return out.trim() || null;
  } catch {
    return null;
  }
}

/**
 * Where this build is running.
 *
 * `VERCEL_ENV` is `production` on the domain, `preview` on a branch URL and
 * `development` for the local `vercel dev` server. Anywhere else — a laptop,
 * a self-hosted runner — the answer depends on whether Next is building the
 * production output or running the development server, so `NODE_ENV` decides.
 */
function environmentOf() {
  const vercel = env("VERCEL_ENV");
  if (vercel === "production" || vercel === "preview" || vercel === "development") return vercel;
  return process.env.NODE_ENV === "development" ? "development" : "local";
}

/** The site's own version, from the package it declares. */
function versionOf() {
  try {
    const pkg = JSON.parse(readFileSync(path.join(websiteRoot, "package.json"), "utf8"));
    return typeof pkg.version === "string" ? pkg.version : "unknown";
  } catch {
    return "unknown";
  }
}

export function buildStamp() {
  /*
   * The revision. Vercel states the commit it checked out, which is the answer
   * even though the deployed function has no `.git` next to it; anywhere else
   * git itself is asked.
   */
  const revision = env("VERCEL_GIT_COMMIT_SHA") ?? git(["rev-parse", "HEAD"]);

  /*
   * The branch or tag. `--abbrev-ref` prints `HEAD` for a detached checkout,
   * which is not a branch name and would be misleading in a tooltip, so it is
   * discarded in favour of saying nothing.
   */
  const refRaw = env("VERCEL_GIT_COMMIT_REF") ?? git(["rev-parse", "--abbrev-ref", "HEAD"]);
  const ref = refRaw && refRaw !== "HEAD" ? refRaw : null;

  const envName = environmentOf();
  const commitIso = envName === "development" ? git(["show", "-s", "--format=%cI", "HEAD"]) : null;
  const builtAt =
    env("PROGLASS_BUILD_TIME") ??
    (commitIso ? new Date(commitIso).toISOString() : new Date().toISOString());
  process.env.PROGLASS_BUILD_TIME = builtAt;

  return {
    /*
     * `PROGLASS_BUILD_TIME` exists so a reproducible build (or a test) can pin
     * the stamp instead of racing the clock. In `next dev`, defaulting to HEAD's
     * commit timestamp prevents Turbopack's persistent client cache and a
     * restarted server process from disagreeing on the minute.
     */
    builtAt,
    revision,
    revisionShort: revision ? revision.slice(0, 7) : null,
    ref,
    environment: envName,
    version: versionOf(),
  };
}

/* `node scripts/build-stamp.mjs` prints one; imported by the config it stays quiet. */
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  console.log(JSON.stringify(buildStamp(), null, 2));
}
