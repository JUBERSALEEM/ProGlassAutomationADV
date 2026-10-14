"use client";

/**
 * §H2 — the scroll half of Crystal's living water.
 *
 * The engine owns the paint (crystal-engine.css §H: the §H ::before water
 * layer reads --glass-surge / --glass-drift); this module owns the physics.
 * One passive scroll listener on the window serves every pane in the
 * document — the glassLife.ts grammar, not a handler per card:
 *
 *   surge  0..1   how hard the water is disturbed. Attacks fast (0.38 of
 *                 the gap per frame) and releases slowly (0.055), so a
 *                 flick blooms and then settles like real liquid instead
 *                 of snapping off with the scroll.
 *   drift  -1..1  the signed lean of the flick. The side pools in §H slide
 *                 toward the trailing edge while it lasts, then ease home.
 *
 * The rAF loop is demand-driven: it starts on the first scroll event and
 * parks itself once surge and drift have both settled to zero, so an idle
 * page pays nothing — no loop, no style writes, no repaints.
 *
 * Gated like the rest of the living glass:
 *   · crystal only — read live from <html data-appearance> on every event,
 *     so a family switch in the studio stills the water immediately;
 *   · prefers-reduced-motion: reduce — never writes (and a live OS flip
 *     mid-scroll clears the channels instead of freezing them mid-slosh);
 *   · the §F6 economy lane (html.glass-econ) — weak devices keep the
 *     still water; the CSS view() flow ride is theirs to keep.
 *
 * Nothing here touches React state — setProperty/removeProperty on the
 * root element only, the pointer-state-guard pattern verbatim.
 */

let cleanup: (() => void) | null = null;

export function initGlassScroll(): () => void {
  if (typeof window === "undefined" || cleanup) return () => {};
  if (typeof window.matchMedia !== "function") return () => {};

  const root = document.documentElement;
  /* Weak devices keep the still water (§F6 economy lane). GlassLife adds
     the class before mounting this, so the check is honest at init. */
  if (root.classList.contains("glass-econ")) return () => {};

  const motionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");

  let raf = 0;
  let lastY = window.scrollY;
  let surge = 0;
  let drift = 0;
  let writtenSurge = -1;
  let writtenDrift = -2;

  const write = () => {
    const s = Math.round(surge * 1000) / 1000;
    const d = Math.round(drift * 1000) / 1000;
    if (Math.abs(s - writtenSurge) > 0.002) {
      root.style.setProperty("--glass-surge", String(s));
      writtenSurge = s;
    }
    if (Math.abs(d - writtenDrift) > 0.002) {
      root.style.setProperty("--glass-drift", String(d));
      writtenDrift = d;
    }
  };

  /* §M — while this loop runs (a scroll is being stirred through), the
     §L liquid drift parks via [data-liquid-rest]: panes repaint for the
     scroll anyway, and two motion systems compositing over the same
     backdrop-filter stack at once is how a page starts to flicker. The
     attribute is written once per stir, never per frame. */
  let liquidRested = false;
  const restLiquid = (rest: boolean) => {
    if (rest === liquidRested) return;
    liquidRested = rest;
    if (rest) root.dataset.liquidRest = "1";
    else delete root.dataset.liquidRest;
  };

  const clear = () => {
    surge = 0;
    drift = 0;
    writtenSurge = -1;
    writtenDrift = -2;
    root.style.removeProperty("--glass-surge");
    root.style.removeProperty("--glass-drift");
    restLiquid(false);
  };

  const frame = () => {
    raf = 0;
    if (motionQuery.matches || root.dataset.appearance !== "crystal") {
      clear();
      return;
    }
    const y = window.scrollY;
    const dy = y - lastY;
    lastY = y;

    /* Normalised per frame: ~90px/frame is a hard flick, ~55px/frame
       leans the water fully to its trailing edge. */
    const targetSurge = Math.min(Math.abs(dy) / 90, 1);
    const targetDrift = Math.max(-1, Math.min(dy / 55, 1));
    surge += (targetSurge - surge) * (targetSurge > surge ? 0.38 : 0.055);
    drift += (targetDrift - drift) * 0.16;

    if (Math.abs(dy) < 0.01 && surge < 0.004 && Math.abs(drift) < 0.004) {
      /* Settled: write the resting zero once and park the loop. */
      surge = 0;
      drift = 0;
      write();
      restLiquid(false);
      return;
    }
    restLiquid(true);
    write();
    raf = window.requestAnimationFrame(frame);
  };

  const onScroll = () => {
    if (raf !== 0 || motionQuery.matches) return;
    if (root.dataset.appearance !== "crystal") return;
    /* lastY deliberately NOT reset here: it still holds the position
       where the water last settled, so the first frame measures the
       full opening delta of the flick. */
    raf = window.requestAnimationFrame(frame);
  };

  const onMotionChange = () => {
    if (motionQuery.matches) {
      if (raf) window.cancelAnimationFrame(raf);
      raf = 0;
      clear();
    } else {
      lastY = window.scrollY;
    }
  };

  window.addEventListener("scroll", onScroll, { passive: true });
  motionQuery.addEventListener?.("change", onMotionChange);

  cleanup = () => {
    window.removeEventListener("scroll", onScroll);
    motionQuery.removeEventListener?.("change", onMotionChange);
    if (raf) window.cancelAnimationFrame(raf);
    raf = 0;
    clear();
    cleanup = null;
  };
  return cleanup;
}
