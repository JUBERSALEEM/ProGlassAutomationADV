"use client";

/**
 * §C5 — the crystal catch light's pointer half.
 *
 * The engine owns the paint (`--glass-catch`, radial at var(--mx)/var(--my));
 * this module owns the coordinates. One pointermove listener on the window
 * serves every lifted surface in the document — the roadmap's "one shared
 * rAF-throttled handler", not a handler per card. Coordinates are written as
 * percentages of the element's own box (the useSpotlight grammar), so a card
 * that resizes keeps its light with no listener to resize.
 *
 * Gated twice, on purpose:
 *   JS-side — (hover: hover) and (pointer: fine) only, and never under
 *             prefers-reduced-motion: reduce (watched live: flipping the OS
 *             setting mid-session clears a lit card instead of freezing it);
 *   CSS-side — the engine's own §C5 block repaints without the catch layer
 *             under reduce, so even a stale --mx/--my paints nothing.
 *
 * Budget discipline, measured (scripts measure at 45/60fps, 26 routes):
 *   zero style writes ≈ 0.05ms/frame baseline; two setProperty writes ≈
 *   0.13ms/frame. The frame is never the constraint — the gating is a
 *   behavior constraint (a light chasing a thumb is a defect, not a cost).
 *
 * Nothing here touches React state. The pointer-state-guard pattern —
 * setProperty/removeProperty only — is followed verbatim.
 */

const SELECTOR =
  '[class*="bg-ink-"], [class*="bg-white"], [class*="bg-line"], [class*="bg-glass"]';

let cleanup: (() => void) | null = null;

export function initGlassLife(): () => void {
  if (typeof window === "undefined" || cleanup) return () => {};
  if (typeof window.matchMedia !== "function") return () => {};

  const hoverQuery = window.matchMedia("(hover: hover) and (pointer: fine)");
  const motionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");

  let lit: Element | null = null;
  let pending: { x: number; y: number } | null = null;
  let raf = 0;

  const clearLit = () => {
    if (!lit) return;
    (lit as HTMLElement).style.removeProperty("--mx");
    (lit as HTMLElement).style.removeProperty("--my");
    lit = null;
  };

  const active = () => hoverQuery.matches;

  const flush = () => {
    raf = 0;
    if (!pending) return;
    const { x, y } = pending;
    pending = null;

    if (!active() || motionQuery.matches) {
      clearLit();
      return;
    }
    if (document.documentElement.dataset.appearance !== "liquid-glass") {
      clearLit();
      return;
    }

    const top = document.elementFromPoint(x, y);
    const card = top ? top.closest(SELECTOR) : null;
    if (!card) {
      clearLit();
      return;
    }

    if (lit && lit !== card) clearLit();
    lit = card;

    const box = (card as HTMLElement).getBoundingClientRect();
    if (box.width === 0 || box.height === 0) return;

    const px = Math.min(Math.max((x - box.left) / box.width, 0), 1);
    const py = Math.min(Math.max((y - box.top) / box.height, 0), 1);
    const el = card as HTMLElement;
    el.style.setProperty("--mx", `${(px * 100).toFixed(2)}%`);
    el.style.setProperty("--my", `${(py * 100).toFixed(2)}%`);
  };

  const onMove = (e: PointerEvent) => {
    pending = { x: e.clientX, y: e.clientY };
    if (raf === 0) raf = window.requestAnimationFrame(flush);
  };
  const onEnd = () => {
    pending = null;
    clearLit();
  };
  const onPrefChange = () => {
    if (motionQuery.matches) onEnd();
  };

  window.addEventListener("pointermove", onMove, { passive: true });
  window.addEventListener("blur", onEnd);
  document.documentElement.addEventListener("mouseleave", onEnd);
  motionQuery.addEventListener?.("change", onPrefChange);

  cleanup = () => {
    window.removeEventListener("pointermove", onMove);
    window.removeEventListener("blur", onEnd);
    document.documentElement.removeEventListener("mouseleave", onEnd);
    motionQuery.removeEventListener?.("change", onPrefChange);
    if (raf) window.cancelAnimationFrame(raf);
    raf = 0;
    onEnd();
    cleanup = null;
  };
  return cleanup;
}
