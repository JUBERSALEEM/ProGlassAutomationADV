"use client";

import { useSyncExternalStore } from "react";
import { hslToRgb, rgbToHsl, rampFor, shift, triplet } from "@/lib/color";
import {
  accentPresets,
  appearanceFamilies,
  DEFAULT_APPEARANCE,
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
  /** Selected colour strength: 0 is neutral, 1 is original, 1.35 is richer. */
  richness: number;
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
  /**
   * Phase 3 — the depth dial. Multiplies the drop-shadow alpha of the clay
   * family's shadow tiers (1 = the shipped recipe; 0.7–1.35 in the UI).
   * Geometry and the light lift are never scaled, so cards deepen without
   * floating, and text-contrast pairs don't move at all.
   */
  clayDepth: number;
  /**
   * The Clay corner-depth dial. The persisted field keeps its original
   * `clayInnerEdge` name so existing saved studio settings migrate
   * without a schema break, but the material meaning is different:
   * Clay draws no card border, so this scales the blurred top-left
   * sheen, bottom-right shade and broad corner light that make the
   * rounded corner read as moulded clay. 1 = the shipped recipe,
   * 0 = no bevel, 10 = the deepest studio setting (alphas are capped
   * in the engine and the shadow geometry grows instead of clipping).
   */
  clayInnerEdge: number;
  /** Which plate the FEATURE card wears (1..6) — the main card is its own color. */ clayPlate: number;
  /** Which plate the on-screen .plate cards wear (1..6). Default = same as the feature card. */ clayCards: number;
  /**
   * The crystal bench (G4) — material multipliers for the glass family.
   * 1 is "the family's own recipe". Body < 1 ghosts the fills (floor 0.45
   * midnight / 0.80 daylight in the engine, never here); glint > 1 brightens
   * the corner light (capped 0.55 by the engine); frost scales every blur.
   * Multiplied in crystal-engine §C4 — never a family-token overwrite.
   */
  glassFrost: number;
  glassBody: number;
  glassGlint: number;
  /**
   * The Neumorphism bench — three material multipliers for the tonal
   * extruded family. Depth scales paired light/shade alpha, distance scales
   * their offset from the surface, and softness scales blur. They never move
   * surface lightness, so text contrast remains owned by the engine recipe.
   */
  neuDepth: number;
  neuDistance: number;
  neuSoftness: number;
};

export const defaultStudio: Studio = {
  accent: null,
  accentKey: FAMILY_ACCENT_KEY,
  brightness: 0,
  vividness: 0,
  richness: 1,
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
  clayPlate: 1,
  clayCards: 1,
  clayDepth: 1,
  clayInnerEdge: 1,
  glassFrost: 1,
  glassBody: 1,
  glassGlint: 1,
  neuDepth: 1,
  neuDistance: 1,
  neuSoftness: 1,
};

/*
 * The clay bench presets. Every preset carries the six §13 plates plus the
 * engine's six accent channels (--clay-lavender and kin the rest of the
 * engine reads), so one tap re-colors the whole family — plates, tiles and
 * accents together — while ink stays the plum bench by construction.
 * "house" is the shipped palette and writes nothing: the stylesheet owns it.
 */

/*
 * §16 The bench is a family, not a sextet. When the bench moves, the whole
 * card system must move with it — faces, shadows, interior wash, ambient
 * pools — or the site is left with green plates casting violet shadows,
 * which reads as broken, not themed. These are the engine's card channels
 * at rest, per mode (clay-engine.css §0 channels, midnight-first); the
 * twist keeps EACH channel's own lightness and saturation and takes the
 * bench's hue, so every contrast lock survives the recolor untouched.
 */
const CLAY_CARD_BASE: Record<"dark" | "light", Record<string, string>> = {
  dark: { "--ink-950": "22 15 33", "--ink-900": "30 21 42", "--ink-850": "40 28 52", "--ink-800": "51 36 64", "--ink-700": "64 46 82" },
  light: { "--ink-950": "238 232 248", "--ink-900": "232 225 246", "--ink-850": "225 216 244", "--ink-800": "218 208 242", "--ink-700": "210 198 238" },
};

const tripletToRgb = (str: string) => {
  const [r = 255, g = 255, b = 255] = str.trim().split(/\s+/).map(Number);
  return { r, g, b };
};

/**
 * Move `baseTriplet` to the exact target hue `toHue`, scaling saturation by
 * `blend` while preserving the base channel's lightness so contrast floors
 * stay intact and no partial hue-angle rotation lands on an unrelated hue.
 */
export function twistTriplet(baseTriplet: string, toHue: number, blend = 0.72): string {
  const hsl = rgbToHsl(tripletToRgb(baseTriplet));
  const targetH = ((toHue % 360) + 360) % 360;
  const targetS = Math.max(hsl.s * Math.min(1, blend), Math.min(0.18, 0.22 * blend));
  return triplet(hslToRgb({ h: targetH, s: targetS, l: hsl.l }));
}

const listeners = new Set<() => void>();
let cached: Studio | null = null;

function emit() {
  for (const listener of listeners) listener();
}

function read(): Studio {
  if (typeof document === "undefined") return defaultStudio;
  if (cached) return cached;
  /* A share link (?studio=…) brought this visitor's bench from somewhere
     else: it wins over whatever this browser remembered — that is the
     whole point of handing someone a link. */
  const shared = readSharedStudio();
  cached = shared ?? readStored();
  /* P18 self-heal: whatever storage or the link carried has just been
     normalised (readStored/decodeStudio run the full spectrum). If the
     normalisation CHANGED anything — a poisoned NaN, a null from an old
     build's JSON, a hand-edited value out of range — write the healed
     state back over the poison, so the next visit opens clean without
     the visitor ever having to find "Reset everything". */
  try {
    if (shared) {
      window.localStorage.setItem(STUDIO_STORAGE_KEY, JSON.stringify(cached));
    } else {
      const raw = window.localStorage.getItem(STUDIO_STORAGE_KEY);
      if (raw !== null && raw !== JSON.stringify(cached)) {
        window.localStorage.setItem(STUDIO_STORAGE_KEY, JSON.stringify(cached));
      }
    }
  } catch {
    /* Storage may be unavailable; the heal just rides this session. */
  }
  return cached;
}

/*
 * P18 — the bench spectrum.
 *
 * The write path (applyStudioVars) used to trust whatever it was handed:
 * any number that was not a finite in-range dial value became a literal
 * "NaN" string written into a custom property — `calc(24px * NaN)` —
 * which browsers drop as an invalid declaration, taking the ENTIRE
 * backdrop filter down with it (every frosted pane in the family), the
 * entire brand ramp with a NaN brightness/vividness (every accent plate
 * on the site suddenly painting nothing), the card radius with a NaN
 * radius. JSON turns NaN into null, so a poisoned bench could no longer
 * even remember being poisoned across a reload — it healed as `null →
 * not guarded on the dials → clamped somewhere arbitrary` (frost stuck
 * at 0.25 = "nearly clear", forever, until the visitor found Reset).
 *
 * Three holes let the poison in, all closed here:
 *   1. `decodeStudio` (share links) ran NO validation at all;
 *   2. `readStored` guarded a cherry-picked subset of the dials;
 *   3. the hygiene-v2 boot migration cleared the paint blobs but left
 *      this state blob, so benches from superseded builds rode forward.
 *
 * Every value the state can carry is now normalised in ONE place, on
 * every read path, and clamped to the dial's own range: an out-of-range
 * value is a stale schema, not a preference. The write path then runs
 * each numeric token through `finite()` again before touching the DOM,
 * so even a caller that bypassed the store can never paint "NaN".
 */
/** Clamp a dial: non-finite → fallback; finite → the dial's own range. */
const finite = (value: unknown, min: number, max: number, fallback: number): number => {
  const n = typeof value === "number" && Number.isFinite(value) ? value : NaN;
  return Number.isFinite(n) ? Math.min(max, Math.max(min, n)) : fallback;
};

/** Every bench a stored blob or share link can claim, made safe. */
export function normalizeStudio(raw: Partial<Studio> | null | undefined): Studio {
  const merged = { ...defaultStudio, ...(raw ?? {}) };

  /* The two strings that drive the paint. */
  if (typeof merged.accent === "string" && !/^#(?:[0-9a-fA-F]{3}){1,2}$/.test(merged.accent.trim())) {
    merged.accent = null;
  }
  if (typeof merged.accentKey !== "string" || merged.accentKey === "") {
    merged.accentKey = merged.accent === null ? FAMILY_ACCENT_KEY : "Custom";
  }
  if (typeof merged.edgeColor !== "string") merged.edgeColor = defaultStudio.edgeColor;
  if (merged.edgeColor !== "" && !/^#[0-9a-fA-F]{3,8}$/.test(merged.edgeColor)) merged.edgeColor = defaultStudio.edgeColor;

  /* The money hexes. The paint path forgives garbage (hexToTriplet falls
     back), but the studio UI echoes them into colour inputs, where a
     non-hex string is a broken control. */
  if (typeof merged.moneyPositive !== "string" || !/^#[0-9a-fA-F]{6}$/i.test(merged.moneyPositive)) merged.moneyPositive = defaultStudio.moneyPositive;
  if (typeof merged.moneyNegative !== "string" || !/^#[0-9a-fA-F]{6}$/i.test(merged.moneyNegative)) merged.moneyNegative = defaultStudio.moneyNegative;
  if (typeof merged.moneyNeutral !== "string" || (merged.moneyNeutral !== "" && !/^#[0-9a-fA-F]{6}$/i.test(merged.moneyNeutral))) merged.moneyNeutral = defaultStudio.moneyNeutral;

  /* The dials. Ranges are the sliders' own — anything outside is a stale
     schema (an older build once let brightness run ±0.08; its stored 0.09
     is now a 1-step clamp, never a crash). */
  merged.brightness = finite(merged.brightness, -0.09, 0.09, 0);
  merged.vividness = finite(merged.vividness, -0.16, 0.16, 0);
  merged.richness = finite(merged.richness, 0, 1.35, 1);
  merged.tint = finite(merged.tint, 0, 3, 1);
  merged.radius = finite(merged.radius, 0.3, 2.0, 1);
  merged.edge = finite(merged.edge, 1, 10, 2);
  merged.uiScale = finite(merged.uiScale, 0.9, 1.15, 1);
  merged.clayDepth = finite(merged.clayDepth, 0.5, 2, 1);
  merged.clayInnerEdge = finite(merged.clayInnerEdge, 0, 10, 1);
  merged.glassFrost = finite(merged.glassFrost, 0.25, 3, 1);
  merged.glassBody = finite(merged.glassBody, 0.25, 3, 1);
  merged.glassGlint = finite(merged.glassGlint, 0, 3, 1);
  merged.neuDepth = finite(merged.neuDepth, 0.35, 1.8, 1);
  merged.neuDistance = finite(merged.neuDistance, 0.5, 1.8, 1);
  merged.neuSoftness = finite(merged.neuSoftness, 0.5, 2.2, 1);
  merged.clayPlate = Number.isInteger(merged.clayPlate) && merged.clayPlate >= 1 && merged.clayPlate <= 6 ? merged.clayPlate : defaultStudio.clayPlate;
  merged.clayCards = Number.isInteger(merged.clayCards) && merged.clayCards >= 1 && merged.clayCards <= 6 ? merged.clayCards : defaultStudio.clayCards;

  /* The typographic enums and flags — strict membership, never truthiness. */
  if (!(merged.typeface in typefaces)) merged.typeface = defaultStudio.typeface;
  if (merged.weight !== "regular" && merged.weight !== "bold") merged.weight = defaultStudio.weight;
  if (merged.density !== "compact" && merged.density !== "comfortable") merged.density = defaultStudio.density;
  merged.italic = merged.italic === true;
  merged.underline = merged.underline === true;

  return merged;
}

function readStored(): Studio {
  try {
    const raw = window.localStorage.getItem(STUDIO_STORAGE_KEY);
    if (!raw) return defaultStudio;
    return normalizeStudio(JSON.parse(raw) as Partial<Studio>);
  } catch {
    return defaultStudio;
  }
}

/*
 * Phase 3 — a bench you can hand over. The whole studio state travels in
 * one URL parameter (?studio=…) so a tuned palette can be shared, reviewed
 * and returned to; encodeStudio/decodeStudio are the two honest halves of
 * that contract — everything read back in is run through the same
 * normalisation as localStorage, so a hostile or stale link degrades to
 * defaults, never to a broken page.
 */
export function encodeStudio(studio: Studio): string {
  try {
    return btoa(unescape(encodeURIComponent(JSON.stringify(studio))));
  } catch {
    return "";
  }
}

export function decodeStudio(encoded: string): Studio | null {
  try {
    const parsed = JSON.parse(decodeURIComponent(escape(atob(encoded.trim())))) as Partial<Studio>;
    if (typeof parsed !== "object" || parsed === null) return null;
    /* P18: a share link is the other write path into the bench — it runs
       the same spectrum as storage, so a hand-made or stale link degrades
       to defaults, never to a poisoned DOM (see normalizeStudio). */
    return normalizeStudio(parsed);
  } catch {
    return null;
  }
}

/** Read the bench from the URL once, if a share link brought one. */
export function readSharedStudio(): Studio | null {
  if (typeof window === "undefined") return null;
  try {
    const param = new URLSearchParams(window.location.search).get("studio");
    if (!param) return null;
    const shared = decodeStudio(param);
    return shared;
  } catch {
    return null;
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
  if (!sharedObserver) {
    /* One observer for every subscriber, not one each: ThemeStudio,
       AppearancePreview and the lazy keeper all subscribe, and a fresh
       observer per subscriber meant a single theme click re-ran
       applyStudioVars — forty-odd style writes plus two localStorage
       serialisations — once per mounted subscriber. The rAF coalescing is
       the second half of the same freeze: a family switch flips
       data-appearance AND (often) data-theme inside the same task, and one
       apply per frame is enough for both. */
    sharedObserver = new MutationObserver(() => {
      if (applyScheduled) return;
      applyScheduled = true;
      requestAnimationFrame(() => {
        applyScheduled = false;
        applyStudioVars(read());
      });
    });
    sharedObserver.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ["data-theme", "data-appearance"],
    });
    try {
      if (
        typeof window !== "undefined" &&
        (window.localStorage.getItem(STUDIO_STORAGE_KEY) !== null ||
          window.location.search.includes("studio="))
      ) {
        applyStudioVars(read());
      }
    } catch {
      /* Storage may be unavailable. */
    }
  }
  subscriberCount += 1;

  return () => {
    listeners.delete(listener);
    subscriberCount -= 1;
    if (subscriberCount <= 0 && sharedObserver) {
      sharedObserver.disconnect();
      sharedObserver = null;
      subscriberCount = 0;
    }
  };
}

let sharedObserver: MutationObserver | null = null;
let subscriberCount = 0;
let applyScheduled = false;

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

  const rawFamily = root.getAttribute("data-appearance") ?? DEFAULT_APPEARANCE;
  const family = rawFamily === "studio" ? "neumorphism" : rawFamily;
  const base = studio.accent ?? FAMILY_ACCENT[family] ?? FAMILY_ACCENT[DEFAULT_APPEARANCE]!;
  // A family on its own keeps its hand-tuned ramp; the moment the visitor
  // moves any ramp control, the ramp is rebuilt from that family's colour.
  /* P18 — the spectrum reaches the write path too. A bench is now always
     normalised on every read, but applyStudioVars is exported and directly
     callable (boot mirrors, observer passes, tests): numeric tokens that
     feed a calc() or a colour channel are clamped here as well, so even a
     bypassed store can never let a NaN near a custom property. */
  const bright = finite(studio.brightness, -0.09, 0.09, 0);
  const vivid = finite(studio.vividness, -0.16, 0.16, 0);
  const richness = finite(studio.richness, 0, 1.35, 1);
  const tinted = finite(studio.tint, 0, 3, 1);
  const tuned = bright !== 0 || vivid !== 0 || richness !== 1 || studio.accent !== null;

  if (tuned) {
    const ramp = rampFor(base, target, bright, vivid);
    vars["--brand-50"] = ramp["50"];
    vars["--brand-100"] = ramp["100"];
    vars["--brand-200"] = ramp["200"];
    vars["--brand-300"] = ramp["300"];
    vars["--brand-400"] = ramp["400"];
    vars["--brand-500"] = ramp["500"];
    vars["--brand-600"] = ramp["600"];
    vars["--brand-700"] = ramp["700"];

    const baseHsl = rgbToHsl(hexToTriplet(base));
    // Achromatic plates must remain neutral. The old saturation floor assigned
    // RGB white HSL's placeholder hue (0°) to every derived channel, turning
    // the White plate rose/pink across the entire page.
    const achromatic = (baseHsl.s < 0.02 || richness === 0) && Math.abs(vivid) < 0.02;
    const chroma = achromatic ? 0 : baseHsl.l < 0.2 ? Math.max(0.18, baseHsl.l / 0.22) : 1;
    const effS = achromatic ? 0 : Math.max(0.08, Math.min(0.96, (baseHsl.s * richness + vivid) * chroma));
    const rotTuned = (dh: number, s: number, l: number) =>
      triplet(hslToRgb({ h: (baseHsl.h + dh + 360) % 360, s, l }));

    // Keep secondary spectrum stops, card tints, hover edges, and grid lines
    // anchored in the chosen plate's colour family across all appearances.
    vars["--glass-cyan"] = rotTuned(10, Math.min(0.9, effS), target === "dark" ? 0.56 : 0.36);
    vars["--glass-teal"] = rotTuned(-10, Math.min(0.86, effS), target === "dark" ? 0.52 : 0.34);
    vars["--glass-rose"] = rotTuned(18, Math.min(0.88, effS), target === "dark" ? 0.6 : 0.4);
    vars["--card-tint"] = `rgb(${ramp["500"]} / ${target === "dark" ? 0.22 : 0.14})`;
    vars["--card-tint-2"] = `rgb(${rotTuned(12, Math.min(0.85, effS), target === "dark" ? 0.54 : 0.44)} / ${target === "dark" ? 0.12 : 0.09})`;
    vars["--card-edge-hover"] = `rgb(${ramp["400"]} / ${target === "dark" ? 0.85 : 0.65})`;
    vars["--glass-primary-glow"] = `rgb(${ramp["500"]} / ${target === "dark" ? 0.42 : 0.28})`;
    vars["--wall-grid"] = `rgb(${ramp["400"]} / ${target === "dark" ? 0.13 : 0.09})`;
    vars["--wall-grid-major"] = `rgb(${ramp["400"]} / ${target === "dark" ? 0.2 : 0.13})`;
    vars["--selection"] = `rgb(${ramp["400"]} / ${target === "dark" ? 0.38 : 0.26})`;

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
  if (tinted !== defaultStudio.tint) {
    vars["--tint-mix"] = `calc(var(--tint-base, 0.1) * ${tinted.toFixed(2)})`;
  }

  /* The crystal bench (G4). Three multipliers, written only when moved —
     the daytime doctrine lives in crystal-engine §C4 (floors and caps are
     math there, not promises here). Under every family but crystal the
     vars are inert: no consumer reads them. */
  if (studio.glassFrost !== defaultStudio.glassFrost) {
    vars["--glass-frost-mult"] = finite(studio.glassFrost, 0.25, 3, 1).toFixed(2);
  }
  if (studio.glassBody !== defaultStudio.glassBody) {
    vars["--glass-body-mult"] = finite(studio.glassBody, 0.25, 3, 1).toFixed(2);
  }
  if (studio.glassGlint !== defaultStudio.glassGlint) {
    vars["--glass-glint-mult"] = finite(studio.glassGlint, 0, 3, 1).toFixed(2);
  }

  /* The Neumorphism bench. The engine stylesheet owns the recipe at 1.00;
     moved dials write only their multiplier. Depth changes light/shade alpha,
     distance changes offset, and softness changes blur — none moves a surface
     lightness step, so the engine's text contrast ladder cannot drift. */
  if (studio.neuDepth !== defaultStudio.neuDepth) {
    vars["--neu-depth"] = finite(studio.neuDepth, 0.35, 1.8, 1).toFixed(2);
  }
  if (studio.neuDistance !== defaultStudio.neuDistance) {
    vars["--neu-distance"] = finite(studio.neuDistance, 0.5, 1.8, 1).toFixed(2);
  }
  if (studio.neuSoftness !== defaultStudio.neuSoftness) {
    vars["--neu-softness"] = finite(studio.neuSoftness, 0.5, 2.2, 1).toFixed(2);
  }

  /*
   * The corner dial writes ONLY the visitor's factor (`--radius-user`).
   * The effective `--radius-scale` is family × user, computed in
   * globals.css — writing `--radius-scale` itself here (the old behaviour)
   * put an inline value over the family's own roundness, so moving the
   * dial silently erased Crystal's ×1.5 and Clay's ×2 the moment it was
   * touched. A stored inline `--radius-scale` from an older build is
   * removed by the not-overridden cleanup below on the next apply.
   */
  if (studio.radius !== defaultStudio.radius) vars["--radius-user"] = String(finite(studio.radius, 0.3, 2.0, 1));

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
  const edge = finite(studio.edge, 1, 10, defaultStudio.edge);
  if (edge !== defaultStudio.edge) vars["--card-edge-width"] = `${edge}px`;
  // Colour sits beside it: a plate picked in the studio overrides the family's
  // edge colour, "" leaves the family in charge of it. P18: a tampered string
  // is dropped rather than shipped to the DOM.
  if (studio.edgeColor.trim() !== "" && /^#[0-9a-fA-F]{3,8}$/.test(studio.edgeColor.trim())) vars["--card-edge"] = studio.edgeColor.trim();
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


  /* Material-You adoption (user decree: color picked instantly from the MAIN
     color plates — the Clay bench's house/free/preset slots are retired):
     a picked accent re-derives the whole clay palette — six pastel plates,
     six channels, faces, shadows, pools — hue-rotated from the accent itself.
     clay-engine.css is untouched; the engine reads these tokens as its own. */
  /* Bench live-ness contract: the six switches must move cards IMMEDIATELY,
     even before a color plate is picked — so the block runs on the family
     accent too, and the panel/cards rows are inputs, not ornaments. The
     plates stay Material-You (hue-rotated from the effective accent); the
     two rows split them: --panel-face (feature cards) vs --card-face (the
     .plate cards on screen). Default: both wear plate 1. */
  if (studio.accent || studio.clayPlate !== 1 || studio.clayCards !== 1 || tuned) {
    const rawHsl = rgbToHsl(hexToTriplet(studio.accent ?? FAMILY_ACCENT[family]!));
    const hub = rawHsl.h;
    const achromatic = (rawHsl.s < 0.02 || richness === 0) && Math.abs(vivid) < 0.02;
    const chroma = achromatic ? 0 : rawHsl.l < 0.2 ? Math.max(0.18, rawHsl.l / 0.22) : 1;
    const effS = achromatic ? 0 : Math.max(0.08, Math.min(0.95, (rawHsl.s * richness + vivid) * chroma));
    const effL = Math.max(0.08, Math.min(0.92, rawHsl.l + bright));
    const pSat = achromatic ? 0 : Math.max(0.1, Math.min(0.84, effS));
    const pLight = Math.max(0.72, Math.min(0.8, 0.68 + effL * 0.18));
    const rot = (dh: number, s: number, l: number) =>
      triplet(hslToRgb({ h: (hub + dh + 360) % 360, s, l }));
    /* When an accent plate is picked (or sliders are tuned), keep all six
       plates tightly anchored to the picked plate's exact hue and saturation
       so every surface, tile, and divider row adapts to the visible plate.
       When running on the house palette (accent === null && !tuned) and only
       the clayPlate/clayCards bench numbers are switched, preserve the six
       house clay plates (lavender, mint, peach, sky, rose, lemon). */
    const plateHues: Array<[number, number, number]> = tuned
      ? achromatic
        ? [
            [0, 0, pLight], [0, 0, Math.max(0.68, pLight - 0.05)],
            [0, 0, Math.min(0.83, pLight + 0.03)], [0, 0, Math.max(0.7, pLight - 0.03)],
            [0, 0, Math.min(0.82, pLight + 0.02)], [0, 0, Math.max(0.68, pLight - 0.05)],
          ]
        : [
          [0, pSat, pLight],
          [8, Math.max(0.1, pSat - 0.04), Math.max(0.68, pLight - 0.05)],
          [-8, Math.min(0.88, pSat + 0.04), Math.min(0.83, pLight + 0.03)],
          [14, Math.max(0.1, pSat - 0.05), Math.max(0.7, pLight - 0.03)],
          [-14, pSat, Math.min(0.82, pLight + 0.02)],
          [20, Math.max(0.1, pSat - 0.04), Math.max(0.68, pLight - 0.05)],
        ]
      : [
          [0, 0.9, 0.88],
          [-112, 0.63, 0.82],
          [-230, 0.95, 0.88],
          [-43, 0.68, 0.84],
          [89, 0.95, 0.9],
          [-207, 0.88, 0.84],
        ];
    plateHues.forEach(([dh, s, l], i) => {
      vars[`--plate-${i + 1}`] = rot(dh, s, l);
      vars[`--plate-h-${i + 1}`] = rot(dh, Math.min(0.88, s + 0.12), 0.42);
    });
    const chanHues = tuned ? [0, 8, -8, 14, -14, 20] : [0, -43, -112, -230, -207, 89];
    const chanNames = ["lavender", "sky", "mint", "peach", "lemon", "pink"];
    chanHues.forEach((dh, i) => {
      let chL = Math.max(0.72, pLight);
      const chS = achromatic ? 0 : Math.max(0.12, Math.min(0.76, pSat));
      while (chL < 0.86) {
        const tp = rot(dh, chS, chL);
        const { r, g, b } = tripletToRgb(tp);
        if (0.2126 * r + 0.7152 * g + 0.0722 * b >= 146) break;
        chL += 0.01;
      }
      vars[`--clay-${chanNames[i]}`] = rot(dh, chS, chL);
    });

    for (const [channel, baseChannel] of Object.entries(CLAY_CARD_BASE[target])) {
      vars[channel] = twistTriplet(baseChannel, hub, 0.72 * chroma);
    }
    vars["--line"] = twistTriplet(target === "dark" ? "38 27 82" : "214 205 233", hub, 0.72 * chroma);
    vars["--line-soft"] = twistTriplet(target === "dark" ? "28 19 62" : "232 226 242", hub, 0.72 * chroma);
    vars["--line-strong"] = twistTriplet(target === "dark" ? "54 40 116" : "188 174 216", hub, 0.72 * chroma);
    /* P14a — the verb follows the bench. Start at the picked plate's actual
       saturation and lightness so primary buttons match the visible plate
       directly whenever it already clears the 3.2:1 white-label floor, and
       only deepen lightness when a bright yellow/lime plate needs contrast. */
    {
      const lumOf = (tp: string) => {
        const { r, g, b } = tripletToRgb(tp);
        const f = (c: number) => { const s = c / 255; return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4; };
        return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
      };
      let coralL = Math.min(0.62, Math.max(0.11, effL));
      const coralS = achromatic ? 0 : Math.max(0.12, Math.min(0.92, rawHsl.s * richness + vivid));
      while (coralL > 0.3 && 1.05 / (lumOf(triplet(hslToRgb({ h: hub, s: coralS, l: coralL }))) + 0.05) < 3.2) coralL -= 0.01;
      vars["--clay-coral"] = triplet(hslToRgb({ h: hub, s: coralS, l: coralL }));
      vars["--clay-coral-h"] = triplet(hslToRgb({ h: hub, s: Math.min(0.92, coralS + 0.06), l: Math.max(0.08, coralL - 0.07) }));
      vars["--clay-foil"] = `rgb(${target === "dark" ? twistTriplet("46 34 62", hub, 0.72 * chroma) : rot(0, Math.min(0.84, pSat + 0.02), Math.min(0.84, pLight + 0.025))})`;
    }
    vars["--clay-drop-h"] = rot(0, Math.min(0.85, pSat + 0.1), 0.35);
    vars["--card-tint-h"] = rot(0, Math.min(0.65, pSat), 0.8);
    vars["--card-tint-h2"] = rot(12, Math.min(0.65, pSat), 0.82);
    vars["--clay-wash"] = (0.18 * tinted).toFixed(3);
    vars["--panel-face"] = vars[`--plate-${studio.clayPlate}`] ?? vars["--plate-1"]!;
    {
      const cd = plateHues[studio.clayCards - 1] ?? plateHues[0]!;
      vars["--card-face"] = rot(cd[0], target === "dark" ? Math.min(cd[1], 0.45) : cd[1], target === "dark" ? 0.29 : cd[2]);
      vars["--data-face"] = rot(cd[0], target === "dark" ? Math.min(cd[1] + 0.1, 0.5) : Math.min(0.86, cd[1] + 0.06), target === "dark" ? 0.34 : Math.min(0.89, cd[2] + 0.05));
      vars["--overlay-face"] = rot(cd[0], target === "dark" ? Math.min(cd[1] + 0.05, 0.4) : Math.min(0.82, cd[1] + 0.03), target === "dark" ? 0.26 : Math.min(0.93, cd[2] + 0.09));
    }
    if (family === "clay") {
      vars["--clay-raised"] = rot(0, Math.min(0.68, pSat * 0.78), target === "dark" ? 0.88 : Math.min(0.88, pLight + 0.08));
      vars["--band-face"] = rot(0, Math.min(0.12, pSat * 0.2), target === "dark" ? 0.18 : 0.95);
      vars["--pool-1"] = `rgb(${rot(0, Math.min(0.55, pSat), 0.68)} / ${target === "dark" ? 0.1 : 0.18})`;
      vars["--pool-2"] = `rgb(${rot(12, Math.min(0.5, pSat), 0.74)} / ${target === "dark" ? 0.06 : 0.12})`;
      vars["--pool-3"] = `rgb(${rot(-12, Math.min(0.5, pSat), 0.76)} / ${target === "dark" ? 0.05 : 0.1})`;
      vars["--band-rich-h"] = rot(0, target === "dark" ? Math.min(0.32, pSat * 0.5) : Math.min(0.62, pSat * 0.75), target === "dark" ? 0.235 : Math.min(0.87, pLight + 0.07));
      vars["--band-deep-h"] = rot(0, target === "dark" ? Math.min(0.28, pSat * 0.45) : Math.min(0.56, pSat * 0.68), target === "dark" ? 0.19 : Math.min(0.84, pLight + 0.04));
      vars["--band-glow-h"] = rot(0, Math.min(0.85, pSat + 0.08), target === "dark" ? 0.5 : 0.42);

      // White is a complete neutral material, not merely a zero-saturation
      // accent. Give daylight Clay a pure-white floor and faces, then separate
      // its elevations with cool neutral whites and gray shadows. Without this
      // explicit material recipe, the family's lavender/brown ambient tokens
      // survive underneath an RGB-white accent and the page still looks beige.
      const blackPlate = rawHsl.s < 0.02 && rawHsl.l <= 0.08 && target === "dark";
      if (blackPlate) {
        Object.assign(vars, {
          "--ink-950": "2 2 3", "--ink-900": "6 6 7", "--ink-850": "10 10 12",
          "--ink-800": "15 15 18", "--ink-700": "23 23 27",
          "--clay-ink": "244 244 245", "--clay-raised": "20 20 23", "--clay-foil": "rgb(14 14 16)",
          "--plate-1": "12 12 14", "--plate-2": "16 16 19", "--plate-3": "20 20 23",
          "--plate-4": "24 24 28", "--plate-5": "28 28 32", "--plate-6": "32 32 37",
          "--plate-h-1": "92 92 100", "--plate-h-2": "100 100 108", "--plate-h-3": "108 108 116",
          "--plate-h-4": "116 116 124", "--plate-h-5": "124 124 132", "--plate-h-6": "132 132 140",
          "--panel-face": "12 12 14", "--card-face": "16 16 19", "--data-face": "20 20 23",
          "--overlay-face": "9 9 11", "--band-face": "7 7 9", "--band-rich-h": "17 17 20",
          "--band-deep-h": "9 9 11", "--band-glow-h": "125 125 132", "--clay-drop-h": "0 0 0",
          "--line": "38 38 43", "--line-soft": "24 24 28", "--line-strong": "58 58 65",
          "--card-tint-h": "16 16 19", "--card-tint-h2": "22 22 25", "--clay-wash": "0",
          "--pool-1": "rgb(255 255 255 / 0.025)", "--pool-2": "rgb(255 255 255 / 0.018)",
          "--pool-3": "rgb(255 255 255 / 0.012)", "--wall-wash-1": "rgb(255 255 255 / 0.018)",
          "--wall-wash-2": "rgb(255 255 255 / 0.012)", "--wall-mid-1": "transparent",
          "--wall-mid-2": "transparent", "--wall-grid": "rgb(255 255 255 / 0.025)",
          "--wall-grid-major": "rgb(255 255 255 / 0.04)", "--wall-vignette": "rgb(0 0 0 / 0.72)",
        });
      }
      const whitePlate = rawHsl.s < 0.02 && rawHsl.l >= 0.98 && target === "light";
      if (whitePlate) {
        Object.assign(vars, {
          "--ink-950": "255 255 255", "--ink-900": "252 252 253", "--ink-850": "248 249 250",
          "--ink-800": "244 245 247", "--ink-700": "237 239 242",
          "--clay-ink": "24 24 27", "--clay-raised": "255 255 255", "--clay-foil": "rgb(247 248 250)",
          "--plate-1": "255 255 255", "--plate-2": "252 252 253", "--plate-3": "249 250 251",
          "--plate-4": "246 247 249", "--plate-5": "243 244 247", "--plate-6": "239 241 244",
          "--plate-h-1": "168 171 178", "--plate-h-2": "158 161 168", "--plate-h-3": "148 151 158",
          "--plate-h-4": "138 141 148", "--plate-h-5": "128 131 138", "--plate-h-6": "118 121 128",
          "--panel-face": "255 255 255", "--card-face": "255 255 255", "--data-face": "248 249 250",
          "--overlay-face": "255 255 255", "--band-face": "255 255 255", "--band-rich-h": "248 249 250",
          "--band-deep-h": "242 244 247", "--band-glow-h": "125 128 135", "--clay-drop-h": "150 154 162",
          "--line": "218 221 226", "--line-soft": "235 237 240", "--line-strong": "195 199 207",
          "--card-tint-h": "250 250 251", "--card-tint-h2": "246 247 249", "--clay-wash": "0",
          "--pool-1": "rgb(210 214 220 / 0.08)", "--pool-2": "rgb(225 228 232 / 0.06)",
          "--pool-3": "rgb(190 195 203 / 0.05)", "--wall-wash-1": "rgb(230 232 236 / 0.12)",
          "--wall-wash-2": "rgb(245 246 248 / 0.16)", "--wall-mid-1": "transparent",
          "--wall-mid-2": "transparent", "--wall-grid": "rgb(120 124 132 / 0.045)",
          "--wall-grid-major": "rgb(120 124 132 / 0.07)", "--wall-vignette": "rgb(80 84 92 / 0.08)",
        });
      }
    } else {
      vars["--band-face"] = rot(0, Math.min(0.3, pSat * 0.45), 0.948);
      if (family === "crystal") {
        vars["--pool-1"] = `rgb(${rot(0, Math.min(0.86, effS), target === "dark" ? 0.56 : 0.58)} / ${target === "dark" ? 0.58 : 0.56})`;
        vars["--pool-2"] = `rgb(${rot(14, Math.min(0.84, effS), 0.62)} / ${target === "dark" ? 0.46 : 0.38})`;
        vars["--pool-3"] = `rgb(${rot(-14, Math.min(0.82, effS), 0.64)} / ${target === "dark" ? 0.4 : 0.48})`;
        vars["--wall-wash-1"] = `rgb(${rot(0, Math.min(0.84, effS), target === "dark" ? 0.58 : 0.78)} / ${target === "dark" ? 0.55 : 0.58})`;
        vars["--wall-wash-2"] = `rgb(${rot(14, Math.min(0.85, effS), target === "dark" ? 0.54 : 0.78)} / ${target === "dark" ? 0.34 : 0.5})`;
        vars["--wall-mid-1"] = `rgb(${rot(-10, Math.min(0.85, effS), target === "dark" ? 0.6 : 0.62)} / ${target === "dark" ? 0.4 : 0.34})`;
        vars["--wall-mid-2"] = `rgb(${rot(16, Math.min(0.82, effS), 0.66)} / ${target === "dark" ? 0.32 : 0.3})`;
        vars["--aurora-ball-1"] = rot(0, Math.min(0.88, effS), target === "dark" ? 0.6 : 0.62);
        vars["--aurora-ball-2"] = rot(14, Math.min(0.84, effS), target === "dark" ? 0.72 : 0.7);
        vars["--aurora-ball-3"] = rot(-14, Math.min(0.8, effS), target === "dark" ? 0.7 : 0.66);
        vars["--glass-relight-1"] = `rgb(${rot(-10, Math.min(0.9, effS), 0.72)} / ${target === "dark" ? 0.4 : 0.34})`;
        vars["--glass-relight-2"] = `rgb(${rot(14, Math.min(0.85, effS), 0.7)} / ${target === "dark" ? 0.34 : 0.4})`;
        vars["--glass-spectrum-1"] = `rgb(${vars["--brand-500"] ?? rot(0, effS, 0.46)})`;
        vars["--glass-spectrum-2"] = `rgb(${vars["--brand-600"] ?? rot(10, effS, 0.38)})`;
      } else {
        vars["--pool-1"] = `rgb(${rot(0, Math.min(0.78, effS), 0.54)} / ${target === "dark" ? 0.28 : 0.22})`;
        vars["--pool-2"] = `rgb(${rot(14, Math.min(0.74, effS), 0.64)} / ${target === "dark" ? 0.16 : 0.13})`;
        vars["--pool-3"] = `rgb(${rot(-14, Math.min(0.74, effS), 0.66)} / ${target === "dark" ? 0.13 : 0.11})`;
        vars["--wall-wash-1"] = `rgb(${rot(0, target === "dark" ? Math.min(0.7, effS) : Math.min(0.65, effS), target === "dark" ? 0.48 : 0.74)} / 0.42)`;
        vars["--wall-wash-2"] = `rgb(${rot(14, target === "dark" ? Math.min(0.72, effS) : Math.min(0.65, effS), target === "dark" ? 0.54 : 0.78)} / ${target === "dark" ? 0.18 : 0.3})`;
      }
    }
  }

  /*
   * Neumorphism adoption. This intentionally runs after the Clay derivation:
   * that shared bench writes generic ink/line/pool channels, and the tonal
   * material must have the final word when its own family is active. Only
   * hue is adopted from the effective accent. Saturation is capped at a
   * material tint and every lightness step remains on the engine ladder, so
   * a vivid plate can re-hue the room without moving text contrast.
   */
  if (family === "neumorphism" && tuned) {
    const neuHsl = rgbToHsl(hexToTriplet(base));
    const neuAchromatic = (neuHsl.s < 0.02 || richness === 0) && Math.abs(vivid) < 0.02;
    const neuSaturation = neuAchromatic
      ? 0
      : Math.min(0.22, Math.max(0.06, (neuHsl.s * richness + vivid) * 0.28));
    const neuLift = bright * 0.35;
    const neuColor = (lightness: number, saturationScale = 1) =>
      triplet(
        hslToRgb({
          h: neuHsl.h,
          s: Math.min(0.26, neuSaturation * saturationScale),
          l: Math.min(0.995, Math.max(0.03, lightness + neuLift)),
        }),
      );

    const neuCanvas = neuColor(target === "dark" ? 0.145 : 0.875);
    const neuFace = neuColor(target === "dark" ? 0.165 : 0.885);
    const neuRaised = neuColor(target === "dark" ? 0.19 : 0.905);
    const neuOverlay = neuColor(target === "dark" ? 0.215 : 0.93);
    const neuInset = neuColor(target === "dark" ? 0.115 : 0.815);
    const neuLightSource = neuColor(target === "dark" ? 0.34 : 0.995, 1.08);
    const neuDarkSource = neuColor(target === "dark" ? 0.045 : 0.66, 1.05);
    const neuLine = neuColor(target === "dark" ? 0.29 : 0.74);

    vars["--neu-canvas"] = neuCanvas;
    vars["--neu-face"] = neuFace;
    vars["--neu-raised"] = neuRaised;
    vars["--neu-overlay"] = neuOverlay;
    vars["--neu-inset"] = neuInset;
    vars["--neu-light-source"] = neuLightSource;
    vars["--neu-dark-source"] = neuDarkSource;
    vars["--neu-line"] = neuLine;

    vars["--ink-950"] = neuCanvas;
    vars["--ink-900"] = neuFace;
    vars["--ink-850"] = neuRaised;
    vars["--ink-800"] = neuOverlay;
    vars["--ink-700"] = neuLine;
    vars["--line"] = neuLine;
    vars["--line-soft"] = neuInset;
    vars["--line-strong"] = neuLightSource;

    vars["--card-tint"] = `rgb(${neuColor(target === "dark" ? 0.38 : 0.48, 1.2)} / 0.07)`;
    vars["--card-tint-2"] = `rgb(${neuColor(target === "dark" ? 0.48 : 0.58, 1.1)} / 0.045)`;
    vars["--card-edge"] = `rgb(${neuLightSource} / 0.16)`;
    vars["--card-edge-hover"] = `rgb(${vars["--brand-400"] ?? neuColor(0.52, 1.4)} / 0.55)`;
    vars["--pool-1"] = `rgb(${neuLightSource} / ${target === "dark" ? 0.1 : 0.12})`;
    vars["--pool-2"] = `rgb(${neuColor(0.62, 1.25)} / 0.07)`;
    vars["--pool-3"] = `rgb(${neuDarkSource} / ${target === "dark" ? 0.12 : 0.06})`;
    vars["--wall-wash-1"] = `rgb(${neuLightSource} / ${target === "dark" ? 0.07 : 0.12})`;
    vars["--wall-wash-2"] = `rgb(${neuColor(0.68, 1.15)} / ${target === "dark" ? 0.05 : 0.08})`;
    vars["--wall-grid"] = `rgb(${neuLightSource} / 0.045)`;
    vars["--wall-grid-major"] = `rgb(${neuLightSource} / 0.065)`;
    vars["--wall-sheen"] = `rgb(${neuLightSource} / ${target === "dark" ? 0.035 : 0.18})`;
    vars["--wall-vignette"] = `rgb(${neuDarkSource} / ${target === "dark" ? 0.5 : 0.1})`;
    vars["--wall-lamp"] = `rgb(${neuLightSource} / ${target === "dark" ? 0.08 : 0.34})`;
    vars["--wall-floor"] = `rgb(${neuDarkSource} / ${target === "dark" ? 0.16 : 0.08})`;
    vars["--viz-grid"] = `rgb(${neuLightSource} / 0.28)`;
    vars["--viz-plate"] = `rgb(${neuInset} / 0.98)`;
  }

  /*
   * Phase 3 — the depth dial is independent of the bench: a visitor may
   * want house plates that cast deeper shadows. Written like `--tint-mix`
   * (only when off default) so the shipped recipe stays stylesheet-owned,
   * and clamped server- and client-side to the dial's own bounds.
   */
  if (studio.clayDepth !== defaultStudio.clayDepth) {
    vars["--clay-depth"] = finite(studio.clayDepth, 0.5, 2, 1).toFixed(2);
  }

  /* The corner-depth dial rides beside it, same discipline: written
     only off default and clamped to the slider's own 0–10. It writes
     `--clay-corner-depth`, never a border token. At 0 the bevel's
     light/shade scale away; the card face and cast shadow remain. */
  if (studio.clayInnerEdge !== defaultStudio.clayInnerEdge) {
    vars["--clay-corner-depth"] = finite(studio.clayInnerEdge, 0, 10, 1).toFixed(2);
  }

  // Tokens that were not overridden must return to the stylesheet's value, so
  // the previous run's variables are cleared before the new set is written.
  //
  // Phase 2: the writes are FILTERED — a setProperty for a value the inline
  // style already holds still dirties style recalculation downstream, and a
  // removeProperty of an absent property is a wasted mutation record. Every
  // applicator path on this site (dial drags, observer passes, theme/family
  // switches) calls through here, so this one loop is the whole cost budget.
  if (previousKeys.length === 0) {
    try {
      const storedVars = JSON.parse(window.localStorage.getItem(STUDIO_VARS_KEY) ?? "{}") as Record<string, unknown>;
      previousKeys = Object.keys(storedVars);
    } catch {
      /* ignore */
    }
  }
  for (const key of previousKeys) {
    if (!(key in vars) && root.style.getPropertyValue(key) !== "") {
      root.style.removeProperty(key);
    }
  }
  for (const [key, value] of Object.entries(vars)) {
    if (root.style.getPropertyValue(key) !== value) root.style.setProperty(key, value);
  }
  for (const [key, value] of Object.entries(attributes)) {
    if (root.getAttribute(key) !== value) root.setAttribute(key, value);
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
  /* Phase 2: dial drags pour input events — dozens per second — and every
     one used to pay a full token application immediately. The write pass
     is coalesced to the next animation frame: sixty pointer events per
     second cost one application per frame, and the studio preview never
     reads stale (subscribers get the new state synchronously via emit).
     Direct applicators — boot, family/mode switches, observer passes —
     keep applyStudioVars() synchronous: one deliberate action, one apply. */
  scheduleApply(next);
  /* Persistence used to be a synchronous localStorage write on EVERY
     call — every tick of every drag paid a storage serialisation on the
     main thread, on top of the token apply that was already coalesced.
     It is now throttled: a deliberate single change (a click, a preset)
     still lands immediately, while a drag pays at most one write per
     150 ms plus a trailing write of the final state on the next frame
     (see scheduleApply), so nothing a visitor settles on is ever lost. */
  const now = performance.now();
  if (now - lastPersistAt > 150) {
    persistStudio(next);
  } else {
    persistDirty = true;
  }
  emit();
}

let lastPersistAt = 0;
let persistDirty = false;
function persistStudio(studio: Studio) {
  try {
    window.localStorage.setItem(STUDIO_STORAGE_KEY, JSON.stringify(studio));
    lastPersistAt = performance.now();
    persistDirty = false;
  } catch {
    /* Storage may be unavailable. */
  }
}

let applyFrame = 0;
function scheduleApply(studio: Studio) {
  if (applyFrame) return;
  if (typeof requestAnimationFrame !== "function") {
    applyStudioVars(studio);
    persistStudio(studio);
    return;
  }
  applyFrame = requestAnimationFrame(() => {
    applyFrame = 0;
    applyStudioVars(read());
    if (persistDirty) persistStudio(read());
  });
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
