#!/usr/bin/env node
/**
 * Is the live site running this code?
 *
 * One question, asked of every origin that serves the site, with an answer that
 * cannot be fudged: each origin is asked for `/api/health` — the build stamp it
 * baked in when it was built — and that revision is held against the revision
 * this working tree is on.
 *
 * Why it exists: this repository is deployed by more than one Vercel project,
 * and a push produces a *preview* deployment on every project while the domain
 * only moves when the project's production branch is pushed or a deployment is
 * promoted. The result is the worst kind of failure — a live site that is
 * healthy, styled, and silently months behind — and a build log that says
 * `Ready` either way. `scripts/audit-site.py --fresh` checks one origin from
 * inside a full site audit; this checks all of them from a standing start, and
 * prints the verdict in a line.
 *
 *     npm run check:live
 *     node scripts/check-live.mjs https://some-preview.vercel.app
 *
 * Exit code 0 when every origin is current, 1 when any origin is behind or
 * cannot say what it is running. So it is usable as a gate as well as a report.
 *
 * A note on what "behind" can mean, because the three answers are fixed here
 * rather than left to interpretation:
 *
 *   current    the origin reports the revision the branch is on;
 *   behind     the origin reports an older revision — the deployment exists,
 *              production just has not been pointed at it;
 *   no stamp   the origin answers with the site's own 404 for `/api/health`,
 *              which means the build predates the stamp entirely. The oldest
 *              possible answer, and the one that looks like a healthy site.
 */
import { execFileSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const websiteRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

/**
 * The origins the site is meant to be reachable at.
 *
 * Both Vercel projects are listed rather than only the one that works: the
 * point of this check is to make the difference between them visible. Add a URL
 * on the command line to check one that is not here.
 */
const ORIGINS = [
  { label: "live", origin: "https://proglassautomationadv-website-tkim.vercel.app" },
  { label: "second project", origin: "https://proglassautomationadv-website.vercel.app" },
];

const REQUEST_TIMEOUT_MS = 15_000;

function git(args) {
  try {
    return (
      execFileSync("git", args, {
        cwd: websiteRoot,
        encoding: "utf8",
        stdio: ["ignore", "pipe", "ignore"],
      }).trim() || null
    );
  } catch {
    return null;
  }
}

/** The revision this tree is on, and the revision the branch has been pushed to. */
function localState() {
  const head = git(["rev-parse", "--short=7", "HEAD"]);
  const branch = git(["rev-parse", "--abbrev-ref", "HEAD"]);
  const upstream = branch && branch !== "HEAD" ? git(["rev-parse", "--short=7", `origin/${branch}`]) : null;
  return { head, branch: branch === "HEAD" ? null : branch, upstream };
}

/**
 * How far behind, in commits, when the two revisions are on the same line.
 *
 * `git rev-list --count a..b` only means anything when `a` is an ancestor of
 * `b`, so it is asked only then; the count is the missing commits, and it turns
 * "behind" into a number the reader can weigh.
 */
function commitsBehind(from, to) {
  if (!from || !to) return null;
  try {
    execFileSync("git", ["merge-base", "--is-ancestor", from, to], {
      cwd: websiteRoot,
      stdio: "ignore",
    });
  } catch {
    return null;
  }
  const count = git(["rev-list", "--count", `${from}..${to}`]);
  return count === null ? null : Number(count);
}

async function ask(origin) {
  const url = `${origin.replace(/\/+$/, "")}/api/health`;
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);
  try {
    const response = await fetch(url, { signal: controller.signal, redirect: "follow" });
    const text = await response.text();
    let health = null;
    try {
      health = JSON.parse(text);
    } catch {
      health = null;
    }
    return { url, status: response.status, health };
  } catch (error) {
    return { url, status: 0, health: null, error: error instanceof Error ? error.message : String(error) };
  } finally {
    clearTimeout(timer);
  }
}

function verdictFor(health, revision) {
  const live = typeof health?.revision === "string" && health.revision !== "" ? health.revision : null;
  if (!live) return { state: "no-revision", live };
  if (!revision) return { state: "unknown-tree", live };
  if (live.slice(0, 7) === revision.slice(0, 7)) return { state: "current", live };
  return { state: "behind", live, behind: commitsBehind(live, revision) };
}

const extra = process.argv.slice(2).filter((argument) => !argument.startsWith("-"));
const origins = extra.length
  ? [
      ...ORIGINS,
      ...extra.map((origin, index) => ({ label: `given ${index + 1}`, origin })),
    ]
  : ORIGINS;

const { head, branch, upstream } = localState();
const target = upstream ?? head;

console.log(`\nthe tree:      ${head ?? "no revision"} on ${branch ?? "a detached checkout"}`);
console.log(`the branch:    ${upstream ?? "not pushed"}${upstream && head && upstream !== head ? ` (the tree is ahead of it)` : ""}`);
console.log("");

const results = [];
for (const { label, origin } of origins) {
  const { status, health, error } = await ask(origin);
  const verdict = health ? verdictFor(health, target) : { state: "unreachable" };

  const what = (() => {
    if (!health) {
      if (status === 404) return "no stamp — this build predates /api/health";
      if (status === 0) return `unreachable — ${error ?? "no answer"}`;
      return `answered ${status} — no build stamp there`;
    }
    /*
     * Both clocks, because the panel prints the UAE one and the deploy log will
     * be read in UTC: `2026-10-07T12:08:56Z (16:08 GST)` is the same instant
     * twice, and nobody has to convert anything to compare the line with a
     * screenshot of the panel.
     */
    const uae = health.builtAtUae ? ` (${String(health.builtAtUae).slice(11, 16)} GST)` : "";
    return `${health.revision ?? "no revision"} · ${health.builtAt ?? "no time"}${uae} · ${health.environment ?? "?"}`;
  })();

  const where = (() => {
    switch (verdict.state) {
      case "current":
        return "CURRENT — it is running the revision the branch is on";
      case "behind":
        return verdict.behind === null
          ? `BEHIND — the branch is on ${target ?? "another line"}, and this is not an ancestor of it`
          : `BEHIND — ${verdict.behind} commit${verdict.behind === 1 ? "" : "s"} behind ${target}`;
      case "no-revision":
        return "NO REVISION — the endpoint answers but cannot name a build";
      case "unknown-tree":
        return "UNKNOWN — the tree states no revision, so nothing can be compared";
      default:
        return "NO ANSWER — see the line above";
    }
  })();

  results.push({ label, origin, verdict: verdict.state });
  const indent = " ".repeat(16);
  console.log(`${label.padEnd(16)}${origin}`);
  console.log(`${indent}${what}`);
  console.log(`${indent}${where}`);
  console.log("");
}

const behind = results.filter((result) => result.verdict !== "current");

if (behind.length === 0) {
  console.log("Every origin is running this branch. Nothing to promote.\n");
  process.exit(0);
}

console.log("Not every origin is current. In the Vercel project for that origin:");
console.log("  Settings → Git → Production Branch  →  set it to the branch the work lands on");
console.log("  (a push to that branch deploys the domain; a push to any other branch only");
console.log("   produces a preview, and the deploy log says `Ready` either way)");
console.log("  Deployments → the newest build → Promote to Production, for a one-off catch-up\n");
process.exit(1);
