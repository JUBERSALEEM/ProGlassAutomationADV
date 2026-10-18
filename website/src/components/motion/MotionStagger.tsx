"use client";

import { Children, type ReactNode } from "react";
import { motion, useReducedMotion } from "motion/react";
import { cn } from "@/lib/cn";
import { dur, ease, staggerStep } from "@/lib/motion";

/**
 * One observer and one coordinated timeline for a small related group.
 * This is intentionally not the default card-grid primitive: use it only for
 * hero facts or short process groups where sequence communicates hierarchy.
 */
export function MotionStagger({
  children,
  className,
  immediate = false,
}: {
  children: ReactNode;
  className?: string;
  immediate?: boolean;
}) {
  const reduced = useReducedMotion();
  const items = Children.toArray(children);

  if (reduced) return <div className={className}>{items}</div>;

  return (
    <motion.div
      className={cn("motion-stagger", className)}
      initial={immediate ? false : "hidden"}
      animate={immediate ? "visible" : undefined}
      whileInView={immediate ? undefined : "visible"}
      viewport={immediate ? undefined : { once: true, amount: 0.18, margin: "0px 0px -4% 0px" }}
      variants={{
        hidden: {},
        visible: { transition: { staggerChildren: staggerStep, delayChildren: immediate ? 0.12 : 0 } },
      }}
    >
      {items.map((child, index) => (
        <motion.div
          key={index}
          variants={{
            hidden: { opacity: 0, y: 10, scale: 0.985 },
            visible: { opacity: 1, y: 0, scale: 1 },
          }}
          transition={{ duration: dur.revealScale, ease: ease.out }}
        >
          {child}
        </motion.div>
      ))}
    </motion.div>
  );
}
