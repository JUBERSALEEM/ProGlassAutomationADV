"use client";

import { useEffect } from "react";
import { initGlassLife } from "@/lib/glassLife";
import { initGlassScroll } from "@/lib/glassScroll";

/**
 * §C5 + §H2 — mounts the crystal living-glass listeners once for the
 * whole app: the pointer catch light (lib/glassLife.ts) and the scroll
 * surge that stirs the §H water (lib/glassScroll.ts). Renders nothing;
 * the paint lives in the engine (crystal-engine.css §C5/§H).
 */
export function GlassLife() {
  useEffect(() => {
    // §F6 — the economy lane: a weak device gets the frost halved and the
    // per-pixel brightness tax removed. The glass itself is untouched.
    // Set BEFORE the scroll stir mounts: glassScroll reads the class at
    // init and keeps weak devices on still water.
    const nav = navigator as Navigator & { deviceMemory?: number };
    const weak = (nav.deviceMemory !== undefined && nav.deviceMemory <= 2)
      || (navigator.hardwareConcurrency !== undefined && navigator.hardwareConcurrency <= 2);
    if (weak) document.documentElement.classList.add("glass-econ");
    const stopLife = initGlassLife();
    const stopScroll = initGlassScroll();
    return () => {
      stopScroll();
      stopLife();
    };
  }, []);
  return null;
}
