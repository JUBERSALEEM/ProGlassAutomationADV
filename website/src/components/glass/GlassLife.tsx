"use client";

import { useEffect } from "react";
import { initGlassLife } from "@/lib/glassLife";

/**
 * §C5 (+ §P) — mounts the crystal pointer catch light (lib/glassLife.ts)
 * once for the whole app. Renders nothing; the paint lives in the engine
 * (crystal-engine.css §C5).
 *
 * §P STABILITY DOCTRINE: scroll writes NOTHING, and nothing page-wide
 * restyles on interaction either. This mount used to also start the §H2
 * scroll stir (lib/glassScroll.ts: per-frame --glass-surge/--glass-drift
 * writes on <html>, repainting every pane's water layer per scroll
 * frame) and the §N idle watchdog (five activity listeners flipping
 * [data-liquid-rest] on <html> at every scroll boundary, restyling the
 * whole document). Both were Crystal-only, both fired exactly on
 * scroll — and cards flashed/blanked exactly on scroll while Clay,
 * which has neither, scrolled clean. The stir file is deleted and the
 * watchdog is gone; --glass-surge/--glass-drift rest at their
 * registered 0 (still water). The liquid drift itself is pure CSS
 * transform animation on three fixed spans (compositor work, no
 * main-thread cost), so it no longer needs parking — that gate existed
 * only to protect backdrop-filter panes, and §O removed those.
 */
export function GlassLife() {
  useEffect(() => {
    // §F6 — the economy lane: a weak device gets the frost halved and the
    // per-pixel brightness tax removed. The glass itself is untouched.
    const nav = navigator as Navigator & { deviceMemory?: number };
    const weak = (nav.deviceMemory !== undefined && nav.deviceMemory <= 2)
      || (navigator.hardwareConcurrency !== undefined && navigator.hardwareConcurrency <= 2);
    if (weak) document.documentElement.classList.add("glass-econ");
    const stopLife = initGlassLife();
    return () => {
      stopLife();
    };
  }, []);
  return null;
}
