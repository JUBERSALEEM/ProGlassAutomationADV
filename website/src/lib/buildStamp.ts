/**
 * What this build is, in the form the interface shows it.
 *
 * The values arrive as string literals: `next.config.ts` publishes them through
 * Next's `env` setting, which substitutes them into the bundle at build time
 * (see the note there). Nothing in this file reads the clock or asks git — that
 * happens once, in `scripts/build-stamp.mjs`, and what is left here is a pure
 * reader. It is deliberate that this module is safe to import from a client
 * component: no `node:` import may reach the browser bundle, and a stamped
 * component that cannot render in a client tree would be no use.
 *
 * Formatting is done by slicing the ISO string rather than through
 * `Intl.DateTimeFormat`. Two reasons, one of them a bug avoided: an ISO instant
 * is already UTC, so the stamp reads the same in all fifteen languages without
 * loading locale data; and a formatter would produce a different string on the
 * server than in the browser wherever the time zone or the locale data differs,
 * which React reports as a hydration mismatch. A stamp whose whole purpose is
 * to be trustworthy cannot be the thing that breaks the page.
 */

export type BuildEnvironment = "production" | "preview" | "development" | "local";

/**
 * The English phrase each environment is described by.
 *
 * A key, not a printed label: the translator is the only thing that decides
 * what a visitor reads, and these four sentences go through it like every
 * other sentence on the site. Chosen so nothing collides with an application
 * string that means something else by the same word.
 */
const ENVIRONMENT_KEY: Record<BuildEnvironment, string> = {
  production: "Live site",
  preview: "Preview build",
  development: "Development build",
  local: "Local build",
};

/**
 * The five values, read as static member expressions.
 *
 * `process.env.PROGLASS_BUILD_TIME` is not a lookup, it is a substitution:
 * Next replaces that whole expression with a string literal while it builds
 * (see the `env` block in `next.config.ts`). A computed read — `process.env[name]`
 * — is invisible to that substitution, so a helper taking the name as an
 * argument would return `undefined` in production while working perfectly in
 * `next dev`. The reads therefore stay written out, one per value.
 */
const rawBuiltAt = (process.env.PROGLASS_BUILD_TIME ?? "").trim();
const rawRevision = (process.env.PROGLASS_BUILD_REVISION ?? "").trim();
const rawRef = (process.env.PROGLASS_BUILD_REF ?? "").trim();
const rawEnvironment = (process.env.PROGLASS_BUILD_ENV ?? "").trim();
const rawVersion = (process.env.PROGLASS_BUILD_VERSION ?? "").trim();

/**
 * The build instant, ISO 8601 in UTC.
 *
 * The shape is checked rather than trusted: if the stamp were ever empty or
 * malformed — a bundle built before this file existed, a hand-edited value —
 * the interface should show something readable instead of `undefined` in a
 * panel that is supposed to be the honest one.
 */
const builtAt = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(rawBuiltAt) ? rawBuiltAt : "";

export const buildStamp = {
  /** ISO 8601 instant, UTC. Empty when the build could not state one. */
  builtAt,
  /** `2026-10-07`, or an em dash when unknown. */
  date: builtAt ? builtAt.slice(0, 10) : "—",
  /** `11:41 UTC` — the zone is always stated, because two hosts are compared. */
  time: builtAt ? `${builtAt.slice(11, 16)} UTC` : "—",
  /** `2026-10-07 11:41 UTC` — the one-line form. */
  line: builtAt ? `${builtAt.slice(0, 10)} ${builtAt.slice(11, 16)} UTC` : "unknown",
  /** The short revision, or null when git could not answer. */
  revision: rawRevision || null,
  /** The branch the build came from, or null on a detached checkout. */
  ref: rawRef || null,
  /** Where this build is running. */
  environment: (
    rawEnvironment === "production" ||
    rawEnvironment === "preview" ||
    rawEnvironment === "development" ||
    rawEnvironment === "local"
      ? rawEnvironment
      : "local"
  ) as BuildEnvironment,
  /** The site's own version — the same string the application ships. */
  version: rawVersion || null,
} as const;

/** `Live site` · `Preview build` · `Development build` · `Local build` — pass through `t()`. */
export const buildEnvironmentKey = ENVIRONMENT_KEY[buildStamp.environment];

/**
 * `2026-10-07 11:41 UTC · c70e7d0` — the long form, used where there is room.
 *
 * The revision is what makes the line worth printing: two builds can be
 * minutes apart, and a time alone cannot say whether they are the same code.
 */
export const buildStampLine = buildStamp.revision
  ? `${buildStamp.line} · ${buildStamp.revision}`
  : buildStamp.line;
