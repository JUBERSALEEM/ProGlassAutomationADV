"use client";

import type { ElementType, ReactNode } from "react";
import { useSpotlight } from "@/lib/useSpotlight";
import { cn } from "@/lib/cn";

/**
 * A card whose face carries a light that follows the pointer.
 *
 * The visual half is `.spot` in `globals.css`; this is the half that has to run
 * in the browser to write `--mx` and `--my`. It exists as its own component so
 * the pages that use it stay server components — a marketing page should not
 * ship its layout in a client bundle to get one hover effect, and this keeps
 * the whole card's markup on the server with only the pointer handler sent
 * across.
 *
 * Everything it does degrades to nothing: with scripting off, on a touch
 * device, or with `prefers-reduced-motion`, the gradient simply stays at rest
 * at the top of the card and the card reads as the card it is.
 */
export function Spotlight({
  children,
  className,
  as: Tag = "div",
}: {
  children: ReactNode;
  className?: string;
  as?: ElementType;
}) {
  const { ref, onPointerMove, onPointerLeave } = useSpotlight<HTMLDivElement>();

  return (
    <Tag
      ref={ref}
      className={cn("spot", className)}
      onPointerMove={onPointerMove}
      onPointerLeave={onPointerLeave}
    >
      {children}
    </Tag>
  );
}
