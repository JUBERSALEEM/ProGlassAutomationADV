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
 *
 * §Z AMENDMENT (the scroll-splash witness): the doctrine above acquits
 * the WRITERS, but the drift itself is the remaining scroll-coupled
 * motion: midnight panes sit at a 0.05 lift, so the drifting disc is
 * visible THROUGH every card, and while the page scrolls the colour
 * swims behind the glass the whole way down — the page never reads
 * still. So the liquid now rests while the visitor scrolls and
 * resumes 900ms after the last scroll event. The implementation is
 * deliberately NOT the retired gate: the attribute lands on the
 * .liquid-scene element itself (the restyle touches three spans'
 * animation-play-state — no inherited property, no pane, no document
 * recalc), and it is written at most twice per gesture (first event,
 * settle timeout), never per frame. Scroll still writes nothing that
 * any content surface can feel.
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

    /* §Z: the scroll-quiet rest. One passive listener, two writes per
       gesture at most; the engine pauses the three drift spans while
       the attribute stands. Other families render the scene
       display:none, so the toggle costs them exactly nothing. */
    const scene = document.querySelector(".liquid-scene");
    let settle = 0;
    const onScroll = () => {
      if (scene && !scene.hasAttribute("data-liquid-rest")) {
        scene.setAttribute("data-liquid-rest", "1");
      }
      window.clearTimeout(settle);
      settle = window.setTimeout(() => scene?.removeAttribute("data-liquid-rest"), 900);
    };
    window.addEventListener("scroll", onScroll, { passive: true });

    return () => {
      window.removeEventListener("scroll", onScroll);
      window.clearTimeout(settle);
      stopLife();
    };
  }, []);
  return null;
}
