import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

/**
 * A slowly moving row of facts.
 *
 * The mechanism is the one that can loop without a seam: the children are
 * rendered twice inside a track that is exactly twice their width, and the
 * animation translates it by `-50%`. At that point the second copy is standing
 * where the first one started, so the restart is invisible. Anything else —
 * a percentage of the viewport, a duplicated-but-not-measured list — jumps.
 *
 * The second copy is `aria-hidden` and the whole track is `inert` to the
 * tab order, because the same five facts twice is the same five facts: a
 * screen reader should read the list once and a keyboard should not have to
 * tab through a duplicate.
 *
 * Pausing on hover is in the CSS (`.marquee:hover .marquee-track`), and
 * `prefers-reduced-motion` stops the track with the first copy on screen and
 * hides the second, so the facts are still there and still readable.
 *
 * @param duration  Seconds for one full pass. Longer reads calmer; the default
 *                  is tuned for a row of five or six short items.
 */
export function Marquee({
  children,
  className,
  duration = 46,
}: {
  children: ReactNode;
  className?: string;
  duration?: number;
}) {
  /*
   * No ARIA role, deliberately. `role="marquee"` is a live-region role: it
   * tells an assistive technology that the content changes on its own and
   * should be announced, which is the opposite of what this is. The row is
   * static content that moves; a screen reader reads it once, in order, from
   * the first copy, and the duplicate beside it is hidden.
   */
  return (
    <div
      className={cn("marquee", className)}
      style={{ "--marquee-duration": `${duration}s` } as React.CSSProperties}
    >
      <div className="marquee-track">
        <div className="flex shrink-0 items-center">{children}</div>
        <div aria-hidden="true" className="flex shrink-0 items-center">
          {children}
        </div>
      </div>
    </div>
  );
}

/**
 * One item in the row.
 *
 * The separator is part of the item rather than part of the track, so a row
 * built from a `.map()` does not need to know whether it is the last one — and
 * the seam at the loop point carries the same diamond as every other gap, which
 * is what stops the eye finding where the copy ends.
 */
export function MarqueeItem({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <span className={cn("flex items-center gap-6 whitespace-nowrap pe-6", className)}>
      {children}
      <span aria-hidden="true" className="h-1.5 w-1.5 rotate-45 bg-brand-400/70" />
    </span>
  );
}
