/**
 * Types for scripts/build-stamp.mjs, which next.config.ts imports. The stamp is
 * plain JavaScript on purpose — `node scripts/build-stamp.mjs` prints one — so
 * the shape it exposes is declared here rather than inferred.
 */

/** Where a build ran, as far as it can tell. */
export type BuildEnvironment = "production" | "preview" | "development" | "local";

export interface BuildStamp {
  /** ISO 8601 instant, UTC — the moment the build started. */
  builtAt: string;
  /** The full commit the build came from, or null when git could not answer. */
  revision: string | null;
  /** The same commit at seven characters, or null. */
  revisionShort: string | null;
  /** The branch or tag, or null on a detached checkout. */
  ref: string | null;
  /** Where the build ran. */
  environment: BuildEnvironment;
  /** The site's version, from website/package.json. */
  version: string;
}

/** Read the stamp for this build. Never throws, never returns a partial shape. */
export function buildStamp(): BuildStamp;
