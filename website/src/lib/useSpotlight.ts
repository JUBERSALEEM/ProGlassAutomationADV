"use client";

import { useCallback, useRef } from "react";

/**
 * The pointer light behind `.spot` in `globals.css`.
 *
 * The class draws a radial gradient at `var(--mx) var(--my)`. This hook writes
 * those two properties as percentages of the element's own box, which is what
 * keeps the effect resolution-independent: a value in pixels would have to be
 * recomputed whenever the card changes size, and a percentage never does.
 *
 * Three things it deliberately does not do.
 *
 *   It does not read layout during the move. `getBoundingClientRect` is called
 *   once per event, which is unavoidable, but it is called inside the same
 *   frame as the write and nothing is read back afterwards — so there is no
 *   forced synchronous reflow between the two.
 *
 *   It does not run on a pointer that cannot hover. A touch device fires
 *   `pointermove` for a drag, and a light that follows a scrolling thumb
 *   across a card is not an effect anybody asked for. `hover: none` is the
 *   media query that says so, and it is tested once rather than per event.
 *
 *   It does not leave the light behind. `pointerleave` resets both properties
 *   so a card the cursor has passed is the same card the cursor never reached.
 *
 * Usage:
 *
 *   const { ref, onPointerMove, onPointerLeave } = useSpotlight<HTMLDivElement>();
 *   <div ref={ref} className="surface spot" onPointerMove={onPointerMove} onPointerLeave={onPointerLeave} />
 */
export function useSpotlight<T extends HTMLElement = HTMLDivElement>() {
  const ref = useRef<T | null>(null);
  const canHover = useRef<boolean | null>(null);

  const onPointerMove = useCallback((event: { currentTarget: T }) => {
    if (canHover.current === null) {
      canHover.current =
        typeof window !== "undefined" && window.matchMedia("(hover: hover) and (pointer: fine)").matches;
    }
    if (!canHover.current) return;

    const node = event.currentTarget;
    if (!node) return;

    const box = node.getBoundingClientRect();
    if (box.width === 0 || box.height === 0) return;

    const x = ((event as unknown as PointerEvent).clientX - box.left) / box.width;
    const y = ((event as unknown as PointerEvent).clientY - box.top) / box.height;

    node.style.setProperty("--mx", `${(Math.min(Math.max(x, 0), 1) * 100).toFixed(2)}%`);
    node.style.setProperty("--my", `${(Math.min(Math.max(y, 0), 1) * 100).toFixed(2)}%`);
  }, []);

  const onPointerLeave = useCallback((event: { currentTarget: T }) => {
    const node = event.currentTarget;
    if (!node) return;
    node.style.removeProperty("--mx");
    node.style.removeProperty("--my");
  }, []);

  return { ref, onPointerMove, onPointerLeave };
}
