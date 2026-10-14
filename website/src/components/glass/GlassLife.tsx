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

    /* §N — the liquid flows only while the page is IDLE. Any scroll or
       pointer activity parks the drift instantly ([data-liquid-rest],
       the gate crystal-engine §M already reads); 1.6s after the last
       activity it resumes. Rationale: a moving backdrop forces every
       backdrop-filtered pane to re-blur per frame, so motion must never
       compete with interaction for the frame budget — the liquid is
       the reward for reading, not a tax on scrolling. The glassScroll
       stir writes the same attribute while scrolling; both writers
       only ever SET it on activity and clear it on their own settle,
       so the worst interleaving is one resumed frame mid-scroll. */
    const rootEl = document.documentElement;
    let idleTimer = 0;
    let rested = false;
    const onActivity = () => {
      if (!rested) {
        rested = true;
        rootEl.dataset.liquidRest = "1";
      }
      window.clearTimeout(idleTimer);
      idleTimer = window.setTimeout(() => {
        rested = false;
        delete rootEl.dataset.liquidRest;
      }, 1600);
    };
    const activityEvents = ["scroll", "pointermove", "pointerdown", "wheel", "touchmove"] as const;
    for (const name of activityEvents) {
      window.addEventListener(name, onActivity, { passive: true });
    }

    return () => {
      for (const name of activityEvents) {
        window.removeEventListener(name, onActivity);
      }
      window.clearTimeout(idleTimer);
      delete rootEl.dataset.liquidRest;
      stopScroll();
      stopLife();
    };
  }, []);
  return null;
}
