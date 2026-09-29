"use client";

import { useEffect, useRef, useState, useSyncExternalStore } from "react";

/**
 * Reveal-on-scroll, one observer for the whole page.
 *
 * A long page used to create an `IntersectionObserver` per `<Reveal>` — 89 of
 * them on `/screenshots`, which is 89 browser objects doing one small job each.
 * They all watch for the same thing at the same margins, so they are collapsed
 * into a single observer shared by every mounted element, with the class
 * written straight onto the node rather than through React state (toggling a
 * class is not a reason to re-render a whole section).
 *
 * The animation itself stays in CSS (see `.reveal` in globals.css), so a
 * visitor with `prefers-reduced-motion: reduce` sees the content immediately
 * rather than waiting on a transition that has been switched off.
 *
 * Observers are kept per `rootMargin` — one shared observer per distinct
 * margin. In practice every caller uses the default, so the budget is one.
 */

type RevealOptions = {
  /** Delay in ms before the reveal class is applied. */
  delay?: number;
  rootMargin?: string;
};

const DEFAULT_ROOT_MARGIN = "0px 0px -10% 0px";
const THRESHOLD = 0.12;

/** The one class the CSS watches for. Applied once, never removed. */
const VISIBLE_CLASS = "is-visible";

function revealNow(node: Element) {
  node.classList.add(VISIBLE_CLASS);
}

type Bucket = {
  observer: IntersectionObserver;
  /** Callback set is unused — presence in the map is the subscription. */
  targets: Set<Element>;
};

const buckets = new Map<string, Bucket>();

function bucketFor(rootMargin: string): Bucket | null {
  if (typeof IntersectionObserver === "undefined") return null;

  const existing = buckets.get(rootMargin);
  if (existing) return existing;

  const targets = new Set<Element>();
  const observer = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        const target = entry.target;
        revealNow(target);
        // One-shot: once shown, it is dropped from both the observer and the
        // registry, so a long page never accumulates dead entries.
        observer.unobserve(target);
        targets.delete(target);
      }
      // Nothing left to watch — release the browser object. It is recreated on
      // demand by the next subscriber, so this costs nothing on navigation.
      if (targets.size === 0) {
        observer.disconnect();
        buckets.delete(rootMargin);
      }
    },
    { rootMargin, threshold: THRESHOLD },
  );

  const bucket: Bucket = { observer, targets };
  buckets.set(rootMargin, bucket);
  return bucket;
}

export function useReveal<T extends HTMLElement = HTMLDivElement>(options?: RevealOptions) {
  const ref = useRef<T | null>(null);
  const rootMargin = options?.rootMargin ?? DEFAULT_ROOT_MARGIN;

  useEffect(() => {
    const node = ref.current;
    if (!node) return;

    const bucket = bucketFor(rootMargin);
    if (!bucket) {
      // No IntersectionObserver (very old browser, or a test environment):
      // show the content rather than leaving it invisible forever.
      revealNow(node);
      return;
    }

    bucket.targets.add(node);
    bucket.observer.observe(node);

    return () => {
      bucket.targets.delete(node);
      bucket.observer.unobserve(node);
      if (bucket.targets.size === 0) {
        bucket.observer.disconnect();
        buckets.delete(rootMargin);
      }
    };
  }, [rootMargin]);

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
 * Kept on its own observer on purpose: it has a different threshold (0.4) from
 * the reveal one and it drives React state every frame, which is not something
 * to share with plain class toggling. There are only a handful of these per
 * page, unlike `<Reveal>`.
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
