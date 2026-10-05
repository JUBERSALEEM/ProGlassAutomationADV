"use client";

import type { ElementType, ReactNode } from "react";
import { useReveal } from "@/lib/useReveal";
import { cn } from "@/lib/cn";

/**
 * Declarative reveal wrapper.
 *
 * `<Reveal delay={120}>` fades and lifts its child into view once. The motion
 * itself is CSS (see `.reveal` in globals.css), so a visitor with
 * `prefers-reduced-motion: reduce` sees the content immediately instead of
 * waiting on a transition that has been switched off.
 */
export function Reveal({
  children,
  delay = 0,
  className,
  variant = "up",
  as: Tag = "div",
  id,
}: {
  children: ReactNode;
  delay?: number;
  className?: string;
  variant?: "up" | "scale" | "none";
  as?: ElementType;
  id?: string;
}) {
  const { ref, style } = useReveal<HTMLDivElement>({ delay });

  return (
    <Tag
      ref={ref}
      id={id}
      style={style}
      className={cn(variant === "up" && "reveal", variant === "scale" && "reveal-scale", className)}
    >
      {children}
    </Tag>
  );
}
