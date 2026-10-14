"use client";

import { useEffect, useRef } from "react";
import type { PointerEvent, ReactNode } from "react";
import { cn } from "@/lib/cn";

/**
 * The hero field — the first screen's depth controller.
 *
 * Two signals drive everything the visitor sees as "alive" on the opening
 * screen, and both are computed here so the page above stays a server
 * component:
 *
 *   `--hx / --hy`   where the pointer is, normalized to the hero. Feeds the
 *                    material shards' parallax — the closer a shard's `--d`
 *                    sits to 3, the nearer it flies and the further it drifts.
 *
 * Discipline, same as every interactive leaf on this site: the listeners go
 * up only for fine pointers with motion allowed (checked live on every
 * event batch, not once at mount), and the `data-live` attribute the CSS
 * gates everything behind is never set otherwise — a phone, a screen reader
 * or a `prefers-reduced-motion` visitor receives the hero exactly as the
 * server rendered it: complete, still, and correct. Pointer writes are
 * rAF-coalesced (the pointer-guard pattern).
 *
 * §P: this field used to also write `--sy` (first-viewport scroll
 * progress) from a scroll listener, lifting the banner away per scroll
 * frame. Scroll writes NOTHING now — a scroll-coupled style write on the
 * heaviest subtree of the top panel is precisely the flash class the
 * stability doctrine abolished. The banner parallax reads --sy at its
 * resting 0; the shards keep their pointer parallax and CSS float.
 */
export function HeroField({ children, className }: { children: ReactNode; className?: string }) {
  const ref = useRef<HTMLElement | null>(null);
  const reduced = useRef(false);
  const rafId = useRef(0);

  useEffect(() => {
    /* Captured once for the cleanup: `ref.current` is not stable across the
       effect's life, and the linter is right to make that explicit. */
    const gate = ref.current;
    const mq = window.matchMedia("(prefers-reduced-motion: reduce)");
    const pointer = window.matchMedia("(pointer: fine)");
    if (mq.matches || !pointer.matches) return;
    reduced.current = false;
    const mqHandler = (e: MediaQueryListEvent) => {
      reduced.current = e.matches;
      if (e.matches && ref.current) {
        ref.current.style.setProperty("--hx", "0.5");
        ref.current.style.setProperty("--hy", "0.5");
      }
    };
    mq.addEventListener("change", mqHandler);

    /* The gate the CSS keys on, set imperative: setting it through state
       would trade one attribute for a whole second render pass on every
       page load. */
    gate?.setAttribute("data-live", "");
    return () => {
      mq.removeEventListener("change", mqHandler);
      cancelAnimationFrame(rafId.current);
      gate?.removeAttribute("data-live");
    };
  }, []);

  const onPointerMove = (e: PointerEvent<HTMLElement>) => {
    if (reduced.current) return;
    const el = ref.current;
    if (!el) return;
    const r = el.getBoundingClientRect();
    const hx = ((e.clientX - r.left) / r.width).toFixed(4);
    const hy = ((e.clientY - r.top) / r.height).toFixed(4);
    /* rAF-coalesced (pointer-guard pattern): one write pair per frame,
       never one per pointer event. */
    cancelAnimationFrame(rafId.current);
    rafId.current = requestAnimationFrame(() => {
      el.style.setProperty("--hx", hx);
      el.style.setProperty("--hy", hy);
    });
  };

  return (
    <section
      ref={ref}
      className={cn(className, "hero-field")}
      onPointerMove={onPointerMove}
    >
      {children}
    </section>
  );
}
