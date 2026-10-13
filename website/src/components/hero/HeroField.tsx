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
 *   `--sy`          scroll progress of the first viewport (0 → 1), feeding the
 *                    backdrop drift and the banner's lift-away.
 *
 * Discipline, same as every interactive leaf on this site: the listeners go
 * up only for fine pointers with motion allowed (checked live on every
 * event batch, not once at mount), and the `data-live` attribute the CSS
 * gates everything behind is never set otherwise — a phone, a screen reader
 * or a `prefers-reduced-motion` visitor receives the hero exactly as the
 * server rendered it: complete, still, and correct. Scroll work runs in one
 * rAF-coalesced passive listener that never writes layout.
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

    const onScroll = () => {
      if (reduced.current) return;
      cancelAnimationFrame(rafId.current);
      rafId.current = requestAnimationFrame(() => {
        const el = ref.current;
        if (!el) return;
        const y = window.scrollY;
        const span = window.innerHeight * 1.25;
        el.style.setProperty("--sy", String(Math.min(1, Math.max(0, y / span)).toFixed(4)));
      });
    };

    /* The gate the CSS keys on, set imperative: setting it through state
       would trade one attribute for a whole second render pass on every
       page load. */
    gate?.setAttribute("data-live", "");
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => {
      window.removeEventListener("scroll", onScroll);
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
    el.style.setProperty("--hx", ((e.clientX - r.left) / r.width).toFixed(4));
    el.style.setProperty("--hy", ((e.clientY - r.top) / r.height).toFixed(4));
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
