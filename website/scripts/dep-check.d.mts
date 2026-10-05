/**
 * Types for scripts/dep-check.mjs, which next.config.ts imports. The check is
 * plain JavaScript on purpose — `node scripts/check-deps.mjs` runs it directly
 * — so the shape it exposes is declared here rather than inferred.
 */

/** A package that is imported but not declared, and the file doing the import. */
export type UndeclaredImport = [name: string, file: string];

export interface DepProblems {
  /** Declared dependencies that are not present in node_modules. */
  missing: string[];
  /** Imports no declared dependency can satisfy. */
  undeclared: UndeclaredImport[];
}

/** Inspect a website/ directory. `root` is the directory holding package.json. */
export function inspectDeps(root: string): DepProblems;

/** The human answer to those problems, as one string ending in a newline. */
export function formatProblems(problems: DepProblems, root: string): string;
