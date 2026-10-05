/**
 * Tiny class-name joiner. Kept local instead of pulling in `clsx` +
 * `tailwind-merge` for a site that never composes conflicting utilities
 * from a variant matrix.
 */
export function cn(...parts: (string | false | null | undefined)[]): string {
  return parts.filter(Boolean).join(" ");
}
