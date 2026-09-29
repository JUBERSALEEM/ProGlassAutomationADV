"use client";

import { useSyncExternalStore } from "react";
import { rampFor, shift, triplet } from "@/lib/color";
import {
  accentPresets,
  appearanceFamilies,
  type AppearanceFamily,
} from "@/data/themes";
import { setAppearance } from "@/lib/useTheme";

/**
 * The appearance studio's state.
 *
 * The visitor can re-point the whole site while it is running: the accent, how
 * rich the surfaces are, how round the corners are, and — because figures are
 * what a shop owner actually reads — the three money colours. Everything is
 * written as inline custom properties on <html>, which outrank the stylesheet
 * blocks, so the change lands on the next frame with no re-render of the page.
 *
 * The computed variables are also stored as JSON under `STUDIO_VARS_KEY`, and
 * the boot script replays them before first paint, so a customised site opens
 * already customised rather than flashing the default palette first.
 */

export const STUDIO_STORAGE_KEY = "proglass-studio";
export const STUDIO_VARS_KEY = "proglass-studio-vars";

/**
 * The `accentKey` used while a palette family is running on its own accent.
 *
 * The colour plates and the family plates are two controls over one value, so
 * they need one vocabulary: "the accent is the family's own" is a state, not
 * the absence of a state, and naming it is what stops the plate group falling
 * back to a stale preset name after a family is chosen.
 */
export const FAMILY_ACCENT_KEY = "Family";

export type Studio = {
  /** Base accent hex, or null to leave the active family's own accent alone. */
  accent: string | null;
  /** Which preset the accent came from, for the chip state. */
  accentKey: string;
  /** Lightness offset applied to the whole ramp, −0.08 … +0.08. */
  brightness: number;
  /** Saturation offset, −0.15 … +0.15. */
  vividness: number;
  /** Strength of the violet wash inside cards, 0 … 1. */
  tint: number;
  /** Corner scale, 0.4 … 1.6. */
  radius: number;
  /**
   * Card edge weight in pixels — the application's "Define Card Edges" setting.
   * 1.5 is the subtle hairline the app uses, 2 is the defined card edge it
   * ships as the default (AppConfig.CardBorderThickness / CardEdgeThickness).
   */
  edge: number;
  /** Which of the shipped typefaces the site is set in. */
  typeface: TypefaceKey;
  /** Money colours, read wherever a figure is a number rather than a label. */
  moneyPositive: string;
  moneyNegative: string;
  moneyNeutral: string;
};

export const defaultStudio: Studio = {
  accent: null,
  accentKey: FAMILY_ACCENT_KEY,
  brightness: 0,
  vividness: 0,
  tint: 1,
  radius: 1,
  edge: 2,
  typeface: "inter",
  moneyPositive: "#34D399",
  moneyNegative: "#FB7185",
  moneyNeutral: "",
};

const listeners = new Set<() => void>();
let cached: Studio | null = null;

function emit() {
  for (const listener of listeners) listener();
}

function read(): Studio {
  if (typeof document === "undefined") return defaultStudio;
  if (cached) return cached;
  cached = readStored();
  return cached;
}

function readStored(): Studio {
  try {
    const raw = window.localStorage.getItem(STUDIO_STORAGE_KEY);
    if (!raw) return defaultStudio;
    const parsed = JSON.parse(raw) as Partial<Studio>;
    const merged = { ...defaultStudio, ...parsed };

    // A value stored by an older build — or edited by hand — must never reach
    // the components. An unknown typeface key would render no font and, in the
    // specimen, read a property off `undefined`.
    if (!(merged.typeface in typefaces)) merged.typeface = defaultStudio.typeface;

    // Same reasoning for the numeric dials: a stored value that is not a finite
    // number would turn a `calc()` into `NaN` and take the card frame with it.
    if (!Number.isFinite(merged.edge)) merged.edge = defaultStudio.edge;
    if (!Number.isFinite(merged.radius)) merged.radius = defaultStudio.radius;
    if (!Number.isFinite(merged.tint)) merged.tint = defaultStudio.tint;

    return merged;
  } catch {
    return defaultStudio;
  }
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);

  /*
   * Watch both attributes, not just the mode.
   *
   * A mode change repaints lightness, so the accent ramp has to be rebuilt for
   * the new contrast target. A *family* change does too, and for a while this
   * observer only watched `data-theme` — which was the bug behind the palette
   * plates appearing to work intermittently. Once a visitor picked a colour
   * plate, the derived `--brand-*` ramp was written as inline styles on <html>,
   * and inline styles outrank the stylesheet. Switching family then changed the
   * attribute but never re-ran this function, so the old ramp stayed on top and
   * the new family's colours were invisible. Choosing a family before choosing
   * a colour worked, which is exactly what "not working constantly" describes.
   */
  const observer = new MutationObserver((records) => {
    for (const record of records) {
      if (record.attributeName === "data-theme" || record.attributeName === "data-appearance") {
        applyStudioVars(read());
        break;
      }
    }
  });
  observer.observe(document.documentElement, {
    attributes: true,
    attributeFilter: ["data-theme", "data-appearance"],
  });

  return () => {
    listeners.delete(listener);
    observer.disconnect();
  };
}

/**
 * The typefaces the studio offers.
 *
 * All four are self-hosted from `src/fonts` (see layout.tsx), so switching
 * family never reaches a network host and never flashes. Each face is a
 * variable font carrying its own italic, which is what makes bold, bold italic
 * and underline render as real typography rather than a synthesised slant.
 *
 * The choice lands on `--font-sans`, which `globals.css` binds to the body
 * font and Tailwind's `font-sans` utility reads, so one control re-sets the
 * whole site the same way the corner slider re-corners it.
 */
export const typefaces = {
  inter: {
    name: "Inter",
    character: "Neutral, tight, made for interfaces. The default.",
    stack: "var(--font-inter), Segoe UI, system-ui, sans-serif",
    sample: "Every figure on this page is a number a shop floor works from.",
  },
  plex: {
    name: "IBM Plex Sans",
    character: "Engineering-console character, with an italic that leans cleanly.",
    stack: "var(--font-plex), Segoe UI, system-ui, sans-serif",
    sample: "Cut length, offcut area and kerf are read together, never alone.",
  },
  serif: {
    name: "Source Serif 4",
    character: "A reading face with real italic forms — good in long paragraphs.",
    stack: "var(--font-serif), Georgia, Times New Roman, serif",
    sample: "The estimator writes the quotation; the works order carries it forward.",
  },
} as const;

export type TypefaceKey = keyof typeof typefaces;

export const typefaceKeys = Object.keys(typefaces) as TypefaceKey[];

/**
 * The accent each family is built around, used when no accent is picked.
 *
 * Read from the family table itself rather than kept in a second list here: the
 * swatch on the studio's plate and the base of the ramp have to be the same
 * colour, and two tables is two chances for them to drift apart.
 */
const FAMILY_ACCENT: Record<string, string> = Object.fromEntries(
  appearanceFamilies.map((entry) => [entry.id, entry.swatch]),
);



/** Writes the derived variables onto <html> and remembers them for the boot. */
export function applyStudioVars(studio: Studio) {
  const root = document.documentElement;
  const target = root.getAttribute("data-theme") === "daylight" ? "light" : "dark";
  const vars: Record<string, string> = {};

  const family = root.getAttribute("data-appearance") ?? "studio";
  const base = studio.accent ?? FAMILY_ACCENT[family] ?? FAMILY_ACCENT["studio"]!;
  // A family on its own keeps its hand-tuned ramp; the moment the visitor
  // moves any ramp control, the ramp is rebuilt from that family's colour.
  const tuned = studio.brightness !== 0 || studio.vividness !== 0 || studio.accent !== null;

  if (tuned) {
    const ramp = rampFor(base, target, studio.brightness, studio.vividness);
    vars["--brand-50"] = ramp["50"];
    vars["--brand-200"] = ramp["200"];
    vars["--brand-300"] = ramp["300"];
    vars["--brand-400"] = ramp["400"];
    vars["--brand-500"] = ramp["500"];
    vars["--brand-600"] = ramp["600"];
    vars["--brand-700"] = ramp["700"];

    // The ambient pools follow the accent too, so the page never ends up with a
    // green accent sitting over a violet glow.
    const pool = shift(base, 0.08, 0.1);
    vars["--pool-1"] = `rgb(${triplet(pool)} / ${target === "dark" ? 0.2 : 0.16})`;
  }

  /*
   * The wash inside a card fill: the one place the tint dial lands.
   *
   * Written as a multiplier over `--tint-base` rather than as a fixed dark/light
   * number, so the dial means "how rich is this card, relative to how rich this
   * palette wants its cards to be". A family that is meant to be saturated —
   * Crystal — keeps its own depth, and one dial still spans from a card that is
   * exactly its surface colour (0) to a card that is plainly carrying the
   * accent (3×).
   */
  vars["--tint-mix"] = `calc(var(--tint-base, 0.1) * ${studio.tint.toFixed(2)})`;

  vars["--radius-scale"] = String(studio.radius);

  /*
   * The card edge — the application's "Define Card Edges" setting.
   *
   * Only the width is written here. The padding that follows it is already
   * expressed in the stylesheet as `calc(1.5rem + max(0px, var(--card-edge-width)
   * - 2px))`, which is the same arithmetic the application's
   * `ApplyCardEdgePaddingResources` applies, so a bolder edge never crowds the
   * content inside it. Writing the padding here as well would be a second copy
   * of one rule, and the two would drift.
   */
  const edge = Number.isFinite(studio.edge) ? Math.min(4, Math.max(1, studio.edge)) : defaultStudio.edge;
  vars["--card-edge-width"] = `${edge}px`;
  vars["--font-sans"] = typefaces[studio.typeface]?.stack ?? typefaces.inter.stack;
  vars["--money-positive"] = triplet(hexToTriplet(studio.moneyPositive));
  vars["--money-negative"] = triplet(hexToTriplet(studio.moneyNegative));
  if (studio.moneyNeutral) {
    vars["--money-neutral"] = triplet(hexToTriplet(studio.moneyNeutral));
  }

  // Tokens that were not overridden must return to the stylesheet's value, so
  // the previous run's variables are cleared before the new set is written.
  for (const key of previousKeys) {
    if (!(key in vars)) root.style.removeProperty(key);
  }
  for (const [key, value] of Object.entries(vars)) {
    root.style.setProperty(key, value);
  }
  previousKeys = Object.keys(vars);

  try {
    window.localStorage.setItem(STUDIO_VARS_KEY, JSON.stringify(vars));
  } catch {
    /* Private mode: the choice still applies for this session. */
  }
}

let previousKeys: string[] = [];

function hexToTriplet(hex: string) {
  const clean = hex.replace(/^#/, "");
  const full =
    clean.length === 3
      ? clean
          .split("")
          .map((char) => char + char)
          .join("")
      : clean;
  if (!/^[0-9a-fA-F]{6}$/.test(full)) return { r: 139, g: 92, b: 246 };
  return {
    r: Number.parseInt(full.slice(0, 2), 16),
    g: Number.parseInt(full.slice(2, 4), 16),
    b: Number.parseInt(full.slice(4, 6), 16),
  };
}

export function setStudio(patch: Partial<Studio>) {
  const next = { ...read(), ...patch };
  cached = next;
  applyStudioVars(next);
  try {
    window.localStorage.setItem(STUDIO_STORAGE_KEY, JSON.stringify(next));
  } catch {
    /* Storage may be unavailable. */
  }
  emit();
}

/**
 * Switch palette family — and re-base the palette on it.
 *
 * Each family ships its own hand-tuned accent ramp in the stylesheet. A colour
 * plate the visitor picked earlier overrides that ramp with inline styles, so
 * without clearing it, choosing a family would change the family's name and
 * nothing else. Clicking a family therefore means "show me this palette", and
 * the accent goes back to the family's own colour; the brightness and vividness
 * dials are the visitor's tuning and are left alone, so the ramp is rebuilt
 * from the new family's accent rather than discarded.
 *
 * Picking a colour plate afterwards overrides the family again. The two
 * controls read as one choice with a clear precedence, which is what makes both
 * of them always do something visible.
 */
export function setFamily(family: AppearanceFamily) {
  setAppearance(family);
  setStudio({ accent: null, accentKey: FAMILY_ACCENT_KEY });
}

export function isFamilyAccentKey(key: string): boolean {
  return key === FAMILY_ACCENT_KEY;
}

/** Preset accent, or a custom hex chosen by the visitor. */
export function setAccent(accentKey: string, accent: string | null) {
  setStudio({ accentKey, accent });
}

export function resetStudio() {
  cached = defaultStudio;
  applyStudioVars(defaultStudio);
  try {
    window.localStorage.removeItem(STUDIO_STORAGE_KEY);
    window.localStorage.setItem(STUDIO_VARS_KEY, JSON.stringify({}));
  } catch {
    /* Storage may be unavailable. */
  }
  emit();
}

export function useStudio(): Studio {
  return useSyncExternalStore(subscribe, read, () => defaultStudio);
}

/** The accent presets the application ships, as pickable chips. */
export const studioAccents = accentPresets.filter((entry) => entry.key !== "Custom");
