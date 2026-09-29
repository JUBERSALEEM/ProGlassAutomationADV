"use client";

import { useEffect, useRef, useState, useSyncExternalStore } from "react";

/**
 * Reveal-on-scroll.
 *
 * One IntersectionObserver per element, disconnected as soon as the element
 * has been shown. The observer writes the class straight onto the node rather
 * than going through React state, because this is exactly the "sync with an
 * external system" case an effect is for — and re-rendering a whole section to
 * toggle one class would be wasted work.
 *
 * The animation itself is CSS (see `.reveal` in globals.css), so a visitor
 * with `prefers-reduced-motion: reduce` sees the content immediately rather
 * than waiting on a transition that has been switched off.
 */
export function useReveal<T extends HTMLElement = HTMLDivElement>(options?: {
  /** Delay in ms before the reveal class is applied. */
  delay?: number;
  rootMargin?: string;
}) {
  const ref = useRef<T | null>(null);

  useEffect(() => {
    const node = ref.current;
    if (!node) return;

    if (typeof IntersectionObserver === "undefined") {
      node.classList.add("is-visible");
      return;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (!entry.isIntersecting) continue;
          node.classList.add("is-visible");
          observer.disconnect();
          return;
        }
      },
      { rootMargin: options?.rootMargin ?? "0px 0px -10% 0px", threshold: 0.12 },
    );

    observer.observe(node);
    return () => observer.disconnect();
  }, [options?.rootMargin]);

  const style = options?.delay ? { transitionDelay: `${options.delay}ms` } : undefined;

  return { ref, style };
}

/** Media query as an external store — no effect, no hydration surprise. */
function subscribeToMotionPreference(callback: () => void) {
  if (typeof window === "undefined" || !window.matchMedia) return () => {};
  const query = window.matchMedia("(prefers-reduced-motion: reduce)");
  query.addEventListener("change", callback);
  return () => query.removeEventListener("change", callback);
}

function getMotionPreference() {
  if (typeof window === "undefined" || !window.matchMedia) return false;
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

export function usePrefersReducedMotion(): boolean {
  return useSyncExternalStore(subscribeToMotionPreference, getMotionPreference, () => false);
}

/**
 * Counts up to a target once the element enters the viewport.
 *
 * Used only for static product facts (counts of modules, locales, databases),
 * never for a performance claim — the data layer has none. When the visitor
 * has asked for reduced motion, the target is written on the first frame
 * instead of being animated.
 */
export function useCountUp(target: number, durationMs = 1300) {
  const ref = useRef<HTMLSpanElement | null>(null);
  const [value, setValue] = useState(0);
  const reduced = usePrefersReducedMotion();

  useEffect(() => {
    const node = ref.current;
    if (!node) return;

    if (reduced || typeof IntersectionObserver === "undefined") {
      // Deferred by a frame so it is not a synchronous setState in the effect,
      // and so the value is painted rather than skipped.
      const frame = requestAnimationFrame(() => setValue(target));
      return () => cancelAnimationFrame(frame);
    }

    let started = false;
    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (!entry.isIntersecting || started) continue;
          started = true;
          observer.disconnect();

          const start = performance.now();
          const tick = (now: number) => {
            const progress = Math.min(1, (now - start) / durationMs);
            // Ease-out cubic — quick at first, settles gently.
            const eased = 1 - Math.pow(1 - progress, 3);
            setValue(Math.round(target * eased));
            if (progress < 1) requestAnimationFrame(tick);
          };
          requestAnimationFrame(tick);
        }
      },
      { threshold: 0.4 },
    );

    observer.observe(node);
    return () => observer.disconnect();
  }, [target, durationMs, reduced]);

  return { ref, value };
}
