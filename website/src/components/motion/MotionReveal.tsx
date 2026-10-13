"use client";

import type { ReactNode } from "react";
import { motion, useReducedMotion } from "motion/react";
import { dur, ease, revealScaleVariants, revealVariants } from "@/lib/motion";
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
 */
export function MotionReveal({
  children,
  delay = 0,
  className,
  variant = "up",
  id,
}: {
  children: ReactNode;
  /** Stagger delay in ms (same unit/call-sites as the CSS `Reveal`). */
  delay?: number;
  className?: string;
  variant?: "up" | "scale" | "none";
  id?: string;
}) {
  const reduced = useReducedMotion();

  if (reduced || variant === "none") {
    return (
      <div id={id} className={className}>
        {children}
      </div>
    );
  }

  return (
    <motion.div
      id={id}
      className={cn("motion-reveal", className)}
      variants={variant === "scale" ? revealScaleVariants : revealVariants}
      initial="hidden"
      whileInView="visible"
      viewport={{ once: true, amount: 0.12 }}
      transition={{
        duration: variant === "scale" ? dur.revealScale : dur.reveal,
        ease: ease.out,
        delay: delay / 1000,
      }}
    >
      {children}
    </motion.div>
  );
}
