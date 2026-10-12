"use client";

import { useEffect } from "react";
import { initGlassLife } from "@/lib/glassLife";

/**
 * §C5 — mounts the crystal catch-light listener once for the whole app.
 * Renders nothing; the paint lives in the engine (crystal-engine.css §C5)
 * and the coordinates live in lib/glassLife.ts.
 */
export function GlassLife() {
  useEffect(() => {
    // §F6 — the economy lane: a weak device gets the frost halved and the
    // per-pixel brightness tax removed. The glass itself is untouched.
    const nav = navigator as Navigator & { deviceMemory?: number };
    const weak = (nav.deviceMemory !== undefined && nav.deviceMemory <= 4)
      || (navigator.hardwareConcurrency !== undefined && navigator.hardwareConcurrency <= 4);
    if (weak) document.documentElement.classList.add("glass-econ");
    return initGlassLife();
  }, []);
  return null;
}
