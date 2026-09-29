"use client";

import { useSyncExternalStore } from "react";
import {
  APPEARANCE_STORAGE_KEY,
  DEFAULT_APPEARANCE,
  DEFAULT_THEME_MODE,
  THEME_STORAGE_KEY,
  isAppearanceFamily,
  isThemeMode,
  resolveTheme,
  type AppearanceFamily,
  type ThemeMode,
} from "@/data/themes";

/**
 * Appearance state as an external store.
 *
 * The DOM is the source of truth — the inline boot script has already written
 * `data-theme-mode` and `data-appearance` on <html> before React runs, so
 * reading the attributes here keeps server and client markup identical and
 * avoids a state-in-effect round trip.
 */

const listeners = new Set<() => void>();

function emit() {
  for (const listener of listeners) listener();
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  const media = window.matchMedia("(prefers-color-scheme: dark)");
  const onSystemChange = () => {
    if (readMode() === "system") writeDom(readMode(), readAppearance());
  };
  media.addEventListener("change", onSystemChange);
  return () => {
    listeners.delete(listener);
    media.removeEventListener("change", onSystemChange);
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

function serverMode(): ThemeMode {
  return DEFAULT_THEME_MODE;
}

function serverAppearance(): AppearanceFamily {
  return DEFAULT_APPEARANCE;
}

/** Writes both attributes on <html> and resolves the lightness. */
function writeDom(mode: ThemeMode, appearance: AppearanceFamily) {
  const root = document.documentElement;
  const resolved = resolveTheme(mode, window.matchMedia("(prefers-color-scheme: dark)").matches);
  root.setAttribute("data-theme", resolved);
  root.setAttribute("data-theme-mode", mode);
  root.setAttribute("data-appearance", appearance);
  root.style.colorScheme = resolved === "daylight" ? "light" : "dark";
}

export function applyTheme(mode: ThemeMode, appearance: AppearanceFamily = readAppearance()) {
  writeDom(mode, appearance);
  safeStore(THEME_STORAGE_KEY, mode);
  emit();
}

export function applyAppearance(appearance: AppearanceFamily, mode: ThemeMode = readMode()) {
  writeDom(mode, appearance);
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

function safeStore(key: string, value: string) {
  try {
    window.localStorage.setItem(key, value);
  } catch {
    /* Storage can be unavailable; the choice still applies for this session. */
  }
}

export function useThemeMode(): ThemeMode {
  return useSyncExternalStore(subscribe, readMode, serverMode);
}

export function useAppearance(): AppearanceFamily {
  return useSyncExternalStore(subscribe, readAppearance, serverAppearance);
}
