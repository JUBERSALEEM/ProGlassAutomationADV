"use client";

import { useRef } from "react";
import type { PointerEvent, ReactNode } from "react";
import { cn } from "@/lib/cn";

/**
 * Pointer-responsive depth.
 *
 * One hook that turns a container into a small window: moving the pointer
 * across it writes `--tx` and `--ty` (signed degrees, clamped by `max`); the
 * children that live in the scene below it can then lean toward the visitor.
 * The effect is deliberately narrow in whom it answers: only fine pointers
 * are acted on (a touch device wobbling under a thumb just eats frames and
 * feels jittery), and `prefers-reduced-motion` is checked on every event, so
 * the scene goes still the moment the operating system says it should.
 *
 * Everything here degrades to stillness — with scripting off, a coarse
 * pointer, or reduced motion, the container renders exactly the markup it
 * was given, in the arrangement CSS gives it at rest. The page above stays
 * a server component; only this leaf crosses to the browser. The pattern is
 * the same one `useSpotlight` and `useReveal` established for this site.
 */
export function useTilt<T extends HTMLElement>(max = 6) {
  const elRef = useRef<T | null>(null);

  const still = () =>
    window.matchMedia("(prefers-reduced-motion: reduce)").matches ||
    !window.matchMedia("(pointer: fine)").matches;

  return {
    ref: elRef,
    onPointerMove: (e: PointerEvent<T>) => {
      if (still()) return;
      const el = elRef.current;
      if (!el) return;
      const r = el.getBoundingClientRect();
      /* +0.5 bias for the pointer position so the stage tilts toward the
         visitor rather than climbing away from them. */
      const px = (e.clientX - r.left) / r.width - 0.5;
      const py = (e.clientY - r.top) / r.height - 0.5;
      el.style.setProperty("--ty", `${(px * 2 * max).toFixed(2)}deg`);
      el.style.setProperty("--tx", `${(-py * 2 * max).toFixed(2)}deg`);
    },
    onPointerEnter: (e: PointerEvent<T>) => {
      if (still()) return;
      e.currentTarget.classList.add("is-tilting");
    },
    onPointerLeave: () => {
      const el = elRef.current;
      if (!el) return;
      el.classList.remove("is-tilting");
      el.style.setProperty("--tx", "0deg");
      el.style.setProperty("--ty", "0deg");
    },
  };
}

/**
 * A scene frame that broadcasts depth to its children.
 *
 * Put the interactive face inside `<Tilt>` and give every child a plane with
 * `style={{ "--z": "48px" } as CSSProperties}`: all planes share one
 * `preserve-3d` scope, so when the pointer tilts the stage the planes
 * separate in depth rather than merely sliding across the surface. With no
 * `--z` set a child stays in the base plane, exactly as before the frame
 * existed.
 */
export function Tilt({
  children,
  className,
  max = 6,
  as: Tag = "div",
}: {
  children: ReactNode;
  className?: string;
  max?: number;
  as?: "div" | "figure";
}) {
  const { ref, onPointerMove, onPointerEnter, onPointerLeave } = useTilt<HTMLDivElement>(max);
  return (
    <Tag
      ref={ref}
      className={cn("tilt-stage", className)}
      onPointerMove={onPointerMove}
      onPointerEnter={onPointerEnter}
      onPointerLeave={onPointerLeave}
    >
      {children}
    </Tag>
  );
}
