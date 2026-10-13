/**
 * The motion token scale — JavaScript mirror of the CSS `--dur-*`/`--ease-*`
 * tokens in `globals.css` (Phase 2). CSS custom properties cannot drive
 * `motion/react` springs/transitions, so the values live in both places and
 * `scripts/motion-tokens.test.mjs` fails the build if they drift apart.
 *
 * Lane rule (Gate G-B, owner-approved 2026-10-13): `motion/react` is used
 * ONLY where sequencing, layout, or state transitions justify it — the hero
 * sequence, filter/state changes, product-demo transitions. Plain scroll
 * reveals stay with the CSS `Reveal` primitive (zero JS per reveal beyond
 * the one shared observer), which is what keeps the page budgets intact.
 */

/** Durations in seconds (motion/react unit) = CSS ms tokens ÷ 1000. */
export const dur = {
  press: 0.14,
  hover: 0.2,
  card: 0.22,
  lift: 0.28,
  theme: 0.26,
  reveal: 0.38,
  revealScale: 0.34,
  settle: 0.55,
} as const;

/** Easings = the CSS `--ease-*` cubic-beziers, as motion easing arrays. */
export const ease = {
  standard: [0.22, 0.61, 0.36, 1] as const,
  out: [0.16, 1, 0.3, 1] as const,
  /** The liquid-press spring curve; for transitions, not looping motion. */
  spring: [0.3, 0.1, 0.4, 1.4] as const,
} as const;

/** Spring configurations for interactive/layout motion. Restrained by design:
 *  no overshoot on content, slight settle on controls. */
export const springs = {
  /** Cards and panels changing layout state (filtering, sheet focus). */
  layout: { type: "spring", stiffness: 320, damping: 32, mass: 0.9 } as const,
  /** Small controls answering a press/hover. */
  control: { type: "spring", stiffness: 420, damping: 30, mass: 0.7 } as const,
};

/** Stagger step (seconds) for sequenced groups — hero beats, card grids. */
export const staggerStep = 0.06;

/** Scroll-reveal variants matching the CSS `.reveal` grammar (12px rise,
 *  `--dur-reveal`, `--ease-out`) so both lanes read as one system. */
export const revealVariants = {
  hidden: { opacity: 0, y: 12 },
  visible: { opacity: 1, y: 0 },
} as const;

export const revealScaleVariants = {
  hidden: { opacity: 0, scale: 0.975 },
  visible: { opacity: 1, scale: 1 },
} as const;
