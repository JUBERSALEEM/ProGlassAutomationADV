"use client";

import { useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { motion, useReducedMotion } from "motion/react";
import { dur, ease, revealFailSafeMs, revealScaleVariants, revealVariants, stagger } from "@/lib/motion";
import { cn } from "@/lib/cn";

/**
 * The `motion/react` scroll reveal — sibling of the CSS `Reveal`, used only
 * where motion's sequencing/layout engine earns its bundle (Gate G-B lane
 * rule, see `lib/motion.ts`): the hero sequence and stateful sections.
 *
 * Grammar is deliberately identical to `.reveal` (12px rise, `--dur-reveal`
 * 380ms, `--ease-out`; scale variant 340ms) so the two lanes read as one
 * system. One-time `whileInView` at the same threshold the shared
 * IntersectionObserver uses (0.12). With `prefers-reduced-motion` the content
 * renders simply present — no hidden initial state at all.
 *
 * SSR note: motion writes the hidden variant inline on the server, so JS is
 * required for the entrance to resolve. Durations are short and content is
 * never gated behind interaction; the CSS `Reveal` remains the default for
 * plain reveals precisely because it degrades without a library runtime.
 *
 * §Q failsafe (the CSS lane's contract, brought to this lane): if the
 * observer-driven entrance has not fired within `revealFailSafeMs`
 * of mount while the block sits near the viewport, the reveal is
 * forced. A loaded main thread must never strand server-rendered
 * content at opacity 0 — the "cards appear when I stop scrolling"
 * witness was this gap.
 */
export function MotionReveal({
  children,
  delay = 0,
  className,
  variant = "up",
  id,
  immediate = false,
}: {
  children: ReactNode;
  /** Stagger delay in ms (same unit/call-sites as the CSS `Reveal`). */
  delay?: number;
  className?: string;
  variant?: "up" | "scale" | "none";
  id?: string;
  /**
   * Above-the-fold content (the hero rule): play on mount instead of
   * waiting for the scroll observer. `whileInView` gates first paint on
   * an IntersectionObserver callback after hydration — on a heavy page
   * that strands the hero invisible, then pops it (the "cards
   * disappearing" witness). Mount-triggered entrances resolve the
   * moment React hydrates, in stagger order, every time.
   */
  immediate?: boolean;
}) {
  const reduced = useReducedMotion();
  const ref = useRef<HTMLDivElement>(null);
  const [forced, setForced] = useState(false);
  /* §AB: touch entrances are fades, not rises (the CSS lane's coarse
     rule in globals.css makes the same choice). A 12px rise on every
     card entering during a fling reads as bounce layered on the
     scroll; on coarse pointers the hidden variant keeps only its
     opacity, so the entrance is a settle, never a climb. */
  const [coarse, setCoarse] = useState(false);

  useEffect(() => {
    if (typeof window.matchMedia !== "function") return;
    const mq = window.matchMedia("(pointer: coarse)");
    const sync = () => setCoarse(mq.matches);
    sync();
    mq.addEventListener?.("change", sync);
    return () => mq.removeEventListener?.("change", sync);
  }, []);

  useEffect(() => {
    if (immediate || reduced || variant === "none") return;
    const timer = window.setTimeout(() => {
      const el = ref.current;
      if (!el) return;
      const rect = el.getBoundingClientRect();
      /* Near-viewport bound, the useReveal failsafe's own: at or above
         1.5 viewport heights out, or already scrolled partly past. */
      if (rect.top < window.innerHeight * 1.5 && rect.bottom > -window.innerHeight * 0.5) {
        setForced(true);
      }
    }, revealFailSafeMs);
    return () => window.clearTimeout(timer);
  }, [immediate, reduced, variant]);

  if (reduced || variant === "none") {
    return (
      <div id={id} className={className}>
        {children}
      </div>
    );
  }

  const laneVariants = variant === "scale" ? revealScaleVariants : revealVariants;
  const activeVariants = coarse
    ? { hidden: { opacity: 0 }, visible: laneVariants.visible }
    : laneVariants;

  const trigger = immediate || forced
    ? { animate: "visible" as const }
    : {
        whileInView: "visible" as const,
        /* Same trigger geometry as the CSS lane's shared observer
           (12% visible, root shrunk 4% at the foot) so both lanes
           start their entrances at the same scroll position. */
        viewport: { once: true, amount: 0.12, margin: "0px 0px -4% 0px" },
      };

  return (
    <motion.div
      ref={ref}
      id={id}
      className={cn("motion-reveal", className)}
      variants={activeVariants}
      /* Above-fold content must be present in server HTML. A previous hidden
         initial variant made the entire hero disappear until Motion hydrated,
         which real-browser screenshots exposed as a blank first viewport. */
      initial={immediate ? false : "hidden"}
      {...trigger}
      transition={{
        duration: variant === "scale" ? dur.revealScale : dur.reveal,
        ease: ease.out,
        /* The stagger law (lib/motion.ts), same cap as the CSS lane:
           small groups sequence; a grid's tail is never held up. */
        delay: Math.min(delay, stagger.capMs) / 1000,
      }}
    >
      {children}
    </motion.div>
  );
}
