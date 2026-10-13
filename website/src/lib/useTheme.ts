"use client";

import { usePathname } from "next/navigation";
import { useEffect, useSyncExternalStore } from "react";
import {
  APPEARANCE_STORAGE_KEY,
  DEFAULT_APPEARANCE,
  DEFAULT_THEME_MODE,
  THEME_STORAGE_KEY,
  isAppearanceFamily,
  isThemeMode,
  resolveTheme,
  type AppearanceFamily,
  type ResolvedTheme,
  type ThemeMode,
} from "@/data/themes";

/**
 * The appearance engine.
 *
 * Two choices, one resolved state, and one place that writes it to the document:
 *
 *   mode      Light · Dark · System     — the lightness of every surface
 *   family    Studio · Monet · Industrial · Crystal — the palette
 *   resolved  midnight | daylight       — what the mode actually resolves to
 *
 * The DOM is the source of truth, and that is deliberate. The inline boot
 * script has already written `data-theme`, `data-theme-mode` and
 * `data-appearance` on <html> before React runs, so reading the attributes back
 * here keeps server and client markup identical and avoids a state-in-effect
 * round trip. Effects are for writing, never for reading what is already there.
 *
 * What the engine writes, per change:
 *
 *   `data-theme`         the resolved lightness — every token block selects on it
 *   `data-theme-mode`    the visitor's choice, including `system`
 *   `data-appearance`    the family
 *   `color-scheme`       so form controls and scrollbars follow
 *   theme-color          the browser/OS chrome, read from the live `--ink-950`
 *   `data-theme-switch`  on for one animation frame-set, so the stylesheet can
 *                        cross-fade the surfaces it is about to repaint
 *
 * That last one is the difference between a theme change that reads as a
 * considered transition and one that reads as a flicker. It is applied only
 * when the visitor has not asked their system not to animate things.
 */

const listeners = new Set<() => void>();

function emit() {
  for (const listener of listeners) listener();
}

/**
 * The switch flash's own timer.
 *
 * Held at module scope rather than in a ref because the flag belongs to the
 * document, not to any one component: two controls can be clicked in quick
 * succession (a mode switch and a family switch are one click apart), and the
 * second click has to extend the window rather than leave the attribute up
 * forever.
 */
let switchTimer: ReturnType<typeof setTimeout> | null = null;

function withoutAnimations(): boolean {
  return (
    typeof window !== "undefined" &&
    typeof window.matchMedia === "function" &&
    window.matchMedia("(prefers-reduced-motion: reduce)").matches
  );
}

/** Puts the surfaces mid-transition for the length of one palette change. */
function flagSwitch() {
  if (typeof document === "undefined" || withoutAnimations()) return;
  const root = document.documentElement;
  root.setAttribute("data-theme-switch", "on");
  if (switchTimer) clearTimeout(switchTimer);
  switchTimer = setTimeout(() => root.removeAttribute("data-theme-switch"), 300);
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  const media =
    typeof window.matchMedia === "function"
      ? window.matchMedia("(prefers-color-scheme: dark)")
      : null;
  const onSystemChange = () => {
    /* Only the mode that defers to the system reacts to a system change. */
    if (readMode() === "system") writeDom(readMode(), readAppearance(), { flash: true });
  };
  media?.addEventListener("change", onSystemChange);

  /*
   * A second tab is the same visitor.
   *
   * Storage events do not fire in the tab that wrote the value, so this never
   * double-applies locally; it exists so that switching the theme in one tab
   * and walking to another does not leave the site wearing two different
   * palettes. Nothing is written back to storage here.
   */
  const onStorage = (event: StorageEvent) => {
    if (event.key !== THEME_STORAGE_KEY && event.key !== APPEARANCE_STORAGE_KEY && event.key !== null) return;
    writeDom(storedMode(), storedAppearance());
    emit();
  };
  window.addEventListener("storage", onStorage);

  return () => {
    listeners.delete(listener);
    media?.removeEventListener("change", onSystemChange);
    window.removeEventListener("storage", onStorage);
  };
}

function readMode(): ThemeMode {
  const value = document.documentElement.getAttribute("data-theme-mode");
  return isThemeMode(value) ? value : DEFAULT_THEME_MODE;
}

function readAppearance(): AppearanceFamily {
  const value = document.documentElement.getAttribute("data-appearance");
  return isAppearanceFamily(value) ? value : DEFAULT_APPEARANCE;
}

function readResolved(): ResolvedTheme {
  const value = document.documentElement.getAttribute("data-theme");
  return value === "daylight" ? "daylight" : "midnight";
}

function serverMode(): ThemeMode {
  return DEFAULT_THEME_MODE;
}

function serverAppearance(): AppearanceFamily {
  return DEFAULT_APPEARANCE;
}

/** The server has no system to ask, so it renders the dark reading. */
function serverResolved(): ResolvedTheme {
  return "midnight";
}

/**
 * The chrome's own colour.
 *
 * Read from the live `--ink-950` rather than kept in a table here, so the
 * address bar in a mobile browser follows the palette the visitor actually
 * built — accent, brightness and reading included — instead of a hard-coded
 * pair that goes stale the moment a token moves. The token is a bare `r g b`
 * triplet, which is what `rgb()` wants.
 *
 * Next renders two of these tags, one per `prefers-color-scheme`, so the first
 * paint has a colour before any script runs. Once the visitor has *chosen* a
 * reading, that choice is the one that matters and a media-scoped pair is
 * wrong: on a light-preference machine the browser would keep reading the light
 * tag while the page it is framing is dark. So the first tag is kept, its
 * `media` is dropped, and the rest are removed — one authoritative colour.
 */
function syncChromeColour() {
  if (typeof document === "undefined") return;
  const root = document.documentElement;
  const triplet = (
    root.style.getPropertyValue("--ink-950") ||
    getComputedStyle(root).getPropertyValue("--ink-950")
  ).trim();
  if (!triplet) return;
  const color = `rgb(${triplet.split(/\s+/).join(" ")})`;
  const metas = Array.from(document.querySelectorAll<HTMLMetaElement>('meta[name="theme-color"]'));
  for (const meta of metas) {
    meta.content = color;
  }
}

/** Writes every attribute the document needs, and the chrome colour with them. */
function writeDom(mode: ThemeMode, appearance: AppearanceFamily, options: { flash?: boolean } = {}) {
  const root = document.documentElement;
  /* Read before the write: what is on the document now is the old palette. */
  const wasResolved = readResolved();
  const wasAppearance = readAppearance();

  const prefersDark =
    typeof window.matchMedia === "function"
      ? window.matchMedia("(prefers-color-scheme: dark)").matches
      : true;
  const resolved = resolveTheme(mode, prefersDark);
  root.setAttribute("data-theme", resolved);
  root.setAttribute("data-theme-mode", mode);
  root.setAttribute("data-appearance", appearance);
  root.style.colorScheme = resolved === "daylight" ? "light" : "dark";
  /*
   * The chrome colour is read after the attributes are written, because the
   * tokens it reads are selected by them: on the same tick, before the write,
   * `--ink-950` is still the old palette's.
   */
  syncChromeColour();

  /* Only a change that will repaint something is worth a transition. */
  if (options.flash && (wasResolved !== resolved || wasAppearance !== appearance)) flagSwitch();
}

export function applyTheme(mode: ThemeMode, appearance: AppearanceFamily = readAppearance()) {
  writeDom(mode, appearance, { flash: true });
  safeStore(THEME_STORAGE_KEY, mode);
  emit();
}

export function applyAppearance(appearance: AppearanceFamily, mode: ThemeMode = readMode()) {
  writeDom(mode, appearance, { flash: true });
  safeStore(APPEARANCE_STORAGE_KEY, appearance);
  emit();
}

/** One-click mode switch. */
export function setThemeMode(mode: ThemeMode) {
  applyTheme(mode, readAppearance());
}

/** One-click palette switch. */
export function setAppearance(appearance: AppearanceFamily) {
  applyAppearance(appearance, readMode());
}

/** Light → Dark → System → Light, for a control that has room for one button. */
export function cycleThemeMode() {
  setThemeMode(nextThemeMode(readMode()));
}

/** The mode after this one, in the order the controls present them. */
export function nextThemeMode(mode: ThemeMode): ThemeMode {
  return mode === "light" ? "dark" : mode === "dark" ? "system" : "light";
}

function safeStore(key: string, value: string) {
  try {
    window.localStorage.setItem(key, value);
  } catch {
    /* Storage can be unavailable; the choice still applies for this session. */
  }
}

/** The stored mode, or the DOM's current one when storage cannot be read. */
function storedMode(): ThemeMode {
  try {
    const value = window.localStorage.getItem(THEME_STORAGE_KEY);
    if (isThemeMode(value)) return value;
  } catch {
    /* Storage can be blocked — a privacy mode, a partitioned iframe. Falling
       back to what is already on <html> changes nothing, which is the point. */
  }
  return readMode();
}

/** The stored palette, or the DOM's current one. */
function storedAppearance(): AppearanceFamily {
  try {
    const value = window.localStorage.getItem(APPEARANCE_STORAGE_KEY);
    if (isAppearanceFamily(value)) return value;
  } catch {
    /* As above. */
  }
  return readAppearance();
}

/**
 * Keep <html> wearing the visitor's choice.
 *
 * The inline boot script sets the theme before first paint, and on a page load
 * that is the whole story. Navigation is the other story: `<html>` is rendered
 * by the root layout, so any navigation that re-renders that layout — changing
 * language re-renders it, because the locale is one of its params — hands React
 * a fresh set of `<html>` attributes at their defaults, and whatever the script
 * wrote is gone. That is what made the theme appear to change by itself when a
 * visitor switched language: the page came back in the default dark palette
 * instead of the light one they had chosen.
 *
 * So after every navigation, and once on mount, the choice is written again
 * from storage. It costs three attribute writes and one property read, it runs
 * after the frame that React commits, and it cannot drift: storage holds the
 * choice, the DOM holds what is displayed, and this is the one line between
 * them. Nothing is written to storage here — reading a preference and
 * re-applying it must never create one.
 */
export function useThemeGuard(): void {
  const pathname = usePathname();
  useEffect(() => {
    writeDom(storedMode(), storedAppearance());
  }, [pathname]);
}

export function useThemeMode(): ThemeMode {
  return useSyncExternalStore(subscribe, readMode, serverMode);
}

export function useAppearance(): AppearanceFamily {
  return useSyncExternalStore(subscribe, readAppearance, serverAppearance);
}

/** Which reading the visitor is actually in, with `system` already resolved. */
export function useResolvedTheme(): ResolvedTheme {
  return useSyncExternalStore(subscribe, readResolved, serverResolved);
}

/**
 * All three values at once.
 *
 * The snapshot is cached and rebuilt only when one of the three strings
 * changes: `useSyncExternalStore` compares snapshots by identity, so returning
 * a fresh object on every read would re-render forever.
 */
let cachedSnapshot: { mode: ThemeMode; family: AppearanceFamily; resolved: ResolvedTheme } | null = null;

function readSnapshot() {
  const mode = readMode();
  const family = readAppearance();
  const resolved = readResolved();
  if (
    !cachedSnapshot ||
    cachedSnapshot.mode !== mode ||
    cachedSnapshot.family !== family ||
    cachedSnapshot.resolved !== resolved
  ) {
    cachedSnapshot = { mode, family, resolved };
  }
  return cachedSnapshot;
}

function serverSnapshot() {
  return { mode: DEFAULT_THEME_MODE, family: DEFAULT_APPEARANCE, resolved: "midnight" as ResolvedTheme };
}

export function useTheme(): {
  mode: ThemeMode;
  family: AppearanceFamily;
  resolved: ResolvedTheme;
} {
  return useSyncExternalStore(subscribe, readSnapshot, serverSnapshot);
}
