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
 * from storage. It costs three attribute writes, it runs after the frame that
 * React commits, and it cannot drift: storage holds the choice, the DOM holds
 * what is displayed, and this is the one line between them. Nothing is written
 * to storage here — reading a preference and re-applying it must never create
 * one.
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
