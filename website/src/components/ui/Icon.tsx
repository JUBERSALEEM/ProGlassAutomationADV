import { cn } from "@/lib/cn";

/**
 * The small glyph renderer — the same 16-unit grid and 1.6px stroke the nav
 * glyphs and the hero-banner renderer draw on, so icons read as one language
 * everywhere they appear. Pure presentation: no client hooks, no state.
 */
export function Icon({ path, className, viewBox = "0 0 16 16" }: { path: string; className?: string; viewBox?: string }) {
  return (
    <svg
      aria-hidden="true"
      viewBox={viewBox}
      className={cn("h-4 w-4 shrink-0", className)}
      fill="none"
      stroke="currentColor"
      strokeWidth={1.6}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d={path} />
    </svg>
  );
}
