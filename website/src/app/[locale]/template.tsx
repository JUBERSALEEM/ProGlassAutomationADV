"use client";

/**
 * Phase 5 (Liquid Glass rollout) — route-entry motion.
 *
 * Every navigation inside a locale remounts this template, so the page
 * blooms in once per route change: a 10px rise + fade on the site's own
 * out-ease, transform/opacity only (no layout work, no paint storms).
 * The grammar is the prototype's: motion must be *caused* — here, by
 * the visitor's navigation — and must never gate content.
 *
 * Safety rails, deliberate:
 *   · `prefers-reduced-motion` renders children bare;
 *   · no-JS renders visible: the motion div carries `.route-shell`,
 *     and the layout's <noscript> sheet forces it opaque/untransformed
 *     when scripting is off (motion writes the hidden state inline, so
 *     the override needs !important — same pattern as `.reveal`);
 *   · the wrapper is a plain block div: sticky/fixed descendants keep
 *     their containing blocks once the 320ms rise settles (motion
 *     removes the transform at rest).
 */

import type { ReactNode } from "react";
import { motion, useReducedMotion } from "motion/react";
import { presets } from "@/lib/motion";

export default function LocaleTemplate({ children }: { children: ReactNode }) {
  const reduce = useReducedMotion();

  if (reduce) return <>{children}</>;

  return (
    <motion.div
      className="route-shell"
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={presets.page}
    >
      {children}
    </motion.div>
  );
}
