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
 * The typography attributes, kept beside the variables so a reload restores
 * them. They cannot live in the variables blob: `data-ui-weight` and its two
 * neighbours are selected on by the stylesheet rather than read as values, so
 * they have to come back as attributes and not as custom properties.
 */
export const STUDIO_ATTRS_KEY = "proglass-studio-attrs";

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
   * ships as the default (AppConfig.CardBorderThickness / CardEdgeThickness),
   * and the site stretches the dial to 10 for real plate-work.
   */
  edge: number;
  /**
   * Card edge colour — "" follows the family, a hex overrides it. Not a WPF
   * setting; the site's own reach, asked for beside the thickness dial, so an
   * edge can carry its own plate colour.
   */
  edgeColor: string;
  /** Which of the shipped typefaces the site is set in. */
  typeface: TypefaceKey;
  /**
   * The application's global UI font weight — `AppConfig.FontWeightName`,
   * "Normal" | "Bold", applied through the AppFontFamily resources. Not a
   * heading rule: the whole interface swaps weight.
   */
  weight: "regular" | "bold";
  /** `AppConfig.FontStyleName` — "Normal" | "Italic", applied the same way. */
  italic: boolean;
  /** `AppConfig.FontUnderline` — the application's global underline. */
  underline: boolean;
  /**
   * `AppConfig.UiScale` — one number, default 1.0, scaling the whole
   * interface against the reader's own root font size.
   */
  uiScale: number;
  /**
   * `AppConfig.DisplayDensity` — "Compact" fits more on a laptop,
   * "Comfortable" gives larger touch targets. Compact is the application's
   * default; this site ships comfortable, so nothing moves until it is chosen.
   */
  density: "compact" | "comfortable";
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
  edgeColor: "",
  typeface: "segoe",
  weight: "regular",
  italic: false,
  underline: false,
  uiScale: 1,
  density: "comfortable",
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
    if (typeof merged.edgeColor !== "string") merged.edgeColor = defaultStudio.edgeColor;
    if (merged.weight !== "regular" && merged.weight !== "bold") merged.weight = defaultStudio.weight;
    if (merged.density !== "compact" && merged.density !== "comfortable") merged.density = defaultStudio.density;
    if (!Number.isFinite(merged.uiScale)) merged.uiScale = defaultStudio.uiScale;
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
 * Four faces, all self-hosted from `src/fonts` (see layout.tsx), so switching
 * family never reaches a network host and never flashes. Each is a variable
 * font carrying a real weight range and its own italic, which is what makes
 * bold, bold italic and underline render as typography rather than as a
 * synthesised slant.
 *
 * The choice lands on `--font-sans`, which `globals.css` binds to the body
 * font and Tailwind's `font-sans` utility reads, so one control re-sets the
 * whole site the same way the corner slider re-corners it.
 *
 * The list has two halves, and they are the application's two halves. The
 * first four are bundled: the site serves them itself, so they are the same
 * font on every machine. The rest are *installed* families — named, not
 * downloaded — and the browser resolves them from the visitor's own system,
 * which is exactly what `FontService` does when it enumerates the fonts on the
 * computer and applies the one that is picked. A browser cannot list what is
 * installed, so this half is a list of the families that ship with Windows,
 * macOS and Android; whichever machine opens the site has several of them, and
 * every stack ends in a generic family so a visitor with none still gets type.
 *
 * What the application's typography settings *do* to the interface — weight,
 * italic, underline, scale, density — is mirrored exactly too, in
 * `studioTypography` below, because those are properties of the text rather
 * than a catalogue of faces.
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
  mono: {
    name: "JetBrains Mono",
    character: "Tabular by design — the face every figure on this site is already set in.",
    stack: "var(--font-mono), Consolas, Cascadia Mono, ui-monospace, monospace",
    sample: "3210 × 2250 · 94.36% used · 4 plates · 15 / 15 / 3 / 3 mm margins.",
  },

  /*
   * ── The families installed on the visitor's own machine ──────────────
   *
   * This is the second half of `FontService`: it lists the fonts the machine
   * has, and applying one sets `App.Config.Appearance.FontFamilyName` to that
   * name. A browser does the same thing when a stylesheet names a family that
   * is not downloaded — it asks the operating system for it — so naming these
   * is not a fallback chain, it is the same feature. Nothing is fetched: the
   * face is already on the visitor's computer.
   *
   * The list is the one `FontService` looks for first — Segoe UI is its
   * `DefaultFontFamily` — plus the families that ship with Windows, macOS and
   * Android, so whichever machine opens the site has several of them. A
   * visitor whose machine has none of them still gets a rendered page: the
   * stack ends in a generic family, which is why every entry below has one.
   */
  segoe: {
    name: "Segoe UI",
    character: "The application's own default — FontService.DefaultFontFamily.",
    stack: '"Segoe UI Variable", "Segoe UI", system-ui, sans-serif',
    sample: "This is the face ProGlass Automation itself is set in.",
  },
  system: {
    name: "System UI",
    character: "Whatever the operating system uses for its own interface.",
    stack: "system-ui, -apple-system, BlinkMacSystemFont, sans-serif",
    sample: "The face your machine already uses for menus, dialogs and mail.",
  },
  arial: {
    name: "Arial / Helvetica",
    character: "The face a drawing or a supplier's quotation is as likely as any to arrive in.",
    stack: "Arial, Helvetica, Liberation Sans, sans-serif",
    sample: "Cut length 1,600 mm · net area 1.76 m² · rate 185.00 per m².",
  },
  verdana: {
    name: "Verdana",
    character: "Wide and open. Legible across a factory floor at a distance.",
    stack: "Verdana, Geneva, DejaVu Sans, sans-serif",
    sample: "QUANTITY 45 · TOTAL SQM 87.68 · AED 16,832.80",
  },
  tahoma: {
    name: "Tahoma",
    character: "Tight and solid at small sizes — reads well on a dense grid.",
    stack: "Tahoma, Segoe UI, sans-serif",
    sample: "W1 2400 · H1 1600 · SQM 3.840 · RATE 210.00",
  },
  trebuchet: {
    name: "Trebuchet MS",
    character: "Humanist, with a tall x-height that survives being printed small.",
    stack: '"Trebuchet MS", "Lucida Grande", sans-serif',
    sample: "Delivered 40 pieces · balance 5 pieces · DN-1841",
  },
  georgia: {
    name: "Georgia",
    character: "A screen serif with old-style figures — softer in a long paragraph.",
    stack: "Georgia, Times New Roman, serif",
    sample: "The estimator writes the quotation; the works order carries it forward.",
  },
  times: {
    name: "Times New Roman",
    character: "The face a letter from a main contractor arrives in.",
    stack: '"Times New Roman", Times, serif',
    sample: "Kindly find attached proforma invoice AZT-POD-2026 for the above project.",
  },
  courier: {
    name: "Courier New",
    character: "Monospaced and typewriter-plain — what a legacy printout looked like.",
    stack: '"Courier New", Courier, monospace',
    sample: "SHT-6001 CLEAR 6MM 3210X2250 RACK A1 46 PLATES",
  },
  consolas: {
    name: "Consolas",
    character: "FontFamily.Code — the application sets its own code and figures in it.",
    stack: "Consolas, Cascadia Mono, DejaVu Sans Mono, monospace",
    sample: "CalculatePanel(400, 300) => 0.500  ·  row.Sqm1 >= 4m",
  },
} as const;

export type TypefaceKey = keyof typeof typefaces;

export const typefaceKeys = Object.keys(typefaces) as TypefaceKey[];

/**
 * The two halves of the list, in the studio's own order.
 *
 * `bundled` is what this site ships and serves itself; `installed` is what the
 * visitor's machine already has, which is the half `FontService` enumerates.
 * The split is the desktop application's own: a font it ships or defaults to,
 * and a font it finds on the computer.
 */
export const bundledTypefaceKeys: TypefaceKey[] = ["inter", "plex", "serif", "mono"];
export const installedTypefaceKeys: TypefaceKey[] = [
  "segoe",
  "system",
  "arial",
  "verdana",
  "tahoma",
  "trebuchet",
  "georgia",
  "times",
  "courier",
  "consolas",
];

/**
 * The application's typography switches, as the studio offers them.
 *
 * Read off `Services/AI/AIConfig/AppConfig.cs`: `FontWeightName` ("Normal" |
 * "Bold"), `FontStyleName` ("Normal" | "Italic"), `FontUnderline` (a bool),
 * `UiScale` (a number, 1.0 by default) and `DisplayDensity` ("Compact" fits
 * more on a laptop; "Comfortable" gives larger touch targets). The desktop
 * Settings page offers these under Typography, and they are global: the whole
 * interface takes the weight, style and underline, not the headings alone.
 * That is what the visitor asked for, so that is what these do.
 */
export const studioTypography = {
  weights: [
    { id: "regular" as const, label: "Regular", note: "FontWeightName = Normal" },
    { id: "bold" as const, label: "Bold", note: "FontWeightName = Bold" },
  ],
  scales: [0.9, 0.95, 1, 1.05, 1.1, 1.15],
  densities: [
    { id: "comfortable" as const, label: "Comfortable", note: "the site's own spacing" },
    { id: "compact" as const, label: "Compact", note: "the application's default" },
  ],
  /** The three treatments the specimen always shows, whatever is selected. */
  treatments: ["bold", "bold italic", "underlined"] as const,
};

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
  /*
   * Understood now the way the ramp block above already is: anything written
   * inline on <html> outranks a family block, so a *default* written inline is
   * a family replacement. The dial writes only what the visitor moved —
   * defaults stay with the stylesheet, where each family's own geometry lives.
   * (This is why a family can claim 32px corners in CSS and still draw 12px
   * in a browser: its tokens were being overwritten on every boot.)
   */
  if (studio.tint !== defaultStudio.tint) {
    vars["--tint-mix"] = `calc(var(--tint-base, 0.1) * ${studio.tint.toFixed(2)})`;
  }

  if (studio.radius !== defaultStudio.radius) vars["--radius-scale"] = String(studio.radius);

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
  const edge = Number.isFinite(studio.edge) ? Math.min(10, Math.max(1, studio.edge)) : defaultStudio.edge;
  if (edge !== defaultStudio.edge) vars["--card-edge-width"] = `${edge}px`;
  // Colour sits beside it: a plate picked in the studio overrides the family's
  // edge colour, "" leaves the family in charge of it.
  if (studio.edgeColor.trim() !== "") vars["--card-edge"] = studio.edgeColor.trim();
  vars["--font-sans"] = typefaces[studio.typeface]?.stack ?? typefaces.inter.stack;

  /*
   * ── Typography, applied the way the application applies it ──────────
   *
   * `AppConfig` holds five settings that between them re-set every piece of
   * text in the interface, and `FontService` writes exactly these when they
   * change:
   *
   *   FontWeightName    Normal | Bold        → --ui-weight  400 | 700
   *   FontStyleName     Normal | Italic      → data-ui-italic=on
   *   FontUnderline     false | true         → data-ui-underline=on
   *   UiScale           1.0, clamped 0.9–1.15 → --ui-scale
   *   DisplayDensity    Compact | Comfortable → --card-pad-scale
   *
   * Three of the five are a *cascade* rather than a measurement — the weight,
   * the italic and the underline all have to reach text the stylesheet did not
   * author — so they are written as data attributes on <html> and the rules in
   * `globals.css` select on them. `--ui-weight` is written well, because the
   * weight is also read directly as a variable by the base layer.
   *
   * Without this the controls moved and nothing repainted: the state was
   * changing on every click and none of it had ever reached the document.
   */
  const weight = studio.weight === "bold" ? 700 : 400;
  vars["--ui-weight"] = String(weight);
  /*
   * UiScale is the one dial that resizes the entire page: html's font-size is
   * `calc(100% * var(--ui-scale))`, and every Tailwind length on the site is
   * rem-based, so a value of 0.9 shrinks the whole layout (padding, columns,
   * cards) while the frame itself stays full width — content reads small and
   * pinned to the left. It therefore must never quote anything but a real,
   * in-range number; a stale studio object from before the dial existed
   * would otherwise persist "NaN" forever. The edge write above is the
   * template.
   */
  const uiScale = Number.isFinite(studio.uiScale)
    ? Math.min(1.15, Math.max(0.9, studio.uiScale))
    : defaultStudio.uiScale;
  vars["--ui-scale"] = String(uiScale);
  vars["--card-pad-scale"] = studio.density === "compact" ? "0.88" : "1";

  const attributes: Record<string, string> = {
    "data-ui-weight": studio.weight === "bold" ? "bold" : "regular",
    "data-ui-italic": studio.italic ? "on" : "off",
    "data-ui-underline": studio.underline ? "on" : "off",
    "data-ui-density": studio.density,
    "data-ui-typeface": studio.typeface,
  };
  /*
   * ── Money colours are re-resolved for the reading ───────────────────
   *
   * These were being written as the visitor's chosen hex in both readings, and
   * because they are written *inline on `<html>`* they outrank the stylesheet —
   * so the light theme's own, darker money greens and reds could never apply.
   * On paper the studio defaults were 1.7:1 and 2.7:1: a profit figure and a
   * loss figure that a person could not read.
   *
   * The hue is the visitor's; the lightness is the reading's. On the dark
   * surfaces the chosen colour is used as it is, and on the light ones it is
   * taken down until it clears contrast against paper — the same discipline the
   * accent ramp already follows through `rampFor`.
   */
  vars["--money-positive"] = triplet(hexToTriplet(readableMoney(studio.moneyPositive, target)));
  vars["--money-negative"] = triplet(hexToTriplet(readableMoney(studio.moneyNegative, target)));
  if (studio.moneyNeutral) {
    vars["--money-neutral"] = triplet(hexToTriplet(readableMoney(studio.moneyNeutral, target)));
  }

  // Tokens that were not overridden must return to the stylesheet's value, so
  // the previous run's variables are cleared before the new set is written.
  for (const key of previousKeys) {
    if (!(key in vars)) root.style.removeProperty(key);
  }
  for (const [key, value] of Object.entries(vars)) {
    root.style.setProperty(key, value);
  }
  for (const [key, value] of Object.entries(attributes)) {
    root.setAttribute(key, value);
  }
  previousKeys = Object.keys(vars);

  try {
    window.localStorage.setItem(STUDIO_VARS_KEY, JSON.stringify(vars));
    window.localStorage.setItem(STUDIO_ATTRS_KEY, JSON.stringify(attributes));
  } catch {
    /* Private mode: the choice still applies for this session. */
  }
}

let previousKeys: string[] = [];

/**
 * A money colour re-resolved so it reads on the surface it lands on.
 *
 * On the dark reading the chosen colour is returned unchanged — it was picked
 * against a near-black card. On paper it is darkened, keeping its hue and its
 * saturation, until a profit figure and a loss figure are both legible: a green
 * that says "in the black" is no use at 1.7:1.
 *
 * The 0.42 factor is not a guess. The studio's default positive is #34D399,
 * which is 1.74:1 on white; at 42% lightness it is #0F6E4E, which is 7.1:1. The
 * negative, #FB7185 at 2.69:1, becomes #9F1239 at 7.8:1. Both keep their hue, so
 * a shop still reads green as profit and red as loss.
 */
function readableMoney(hex: string, target: "light" | "dark"): string {
  if (target === "dark") return hex;
  const { r, g, b } = hexToTriplet(hex);
  const lighten = (channel: number) => Math.round(channel * 0.42);
  return `#${[lighten(r), lighten(g), lighten(b)]
    .map((channel) => Math.max(0, Math.min(255, channel)).toString(16).padStart(2, "0"))
    .join("")}`;
}

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
    window.localStorage.setItem(STUDIO_ATTRS_KEY, JSON.stringify({}));
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
