"use client";

import { useSyncExternalStore } from "react";
import { hslToRgb, rgbToHsl, rampFor, shift, triplet } from "@/lib/color";
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
  /**
   * Phase 3 — the depth dial. Multiplies the drop-shadow alpha of the clay
   * family's shadow tiers (1 = the shipped recipe; 0.7–1.35 in the UI).
   * Geometry and the light lift are never scaled, so cards deepen without
   * floating, and text-contrast pairs don't move at all.
   */
  clayDepth: number;
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
  clayPlate: 1,
  clayCards: 1,
  clayDepth: 1,
  glassFrost: 1,
  glassBody: 1,
  glassGlint: 1,
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
  dark: { "--ink-950": "22 15 33", "--ink-900": "30 21 42", "--ink-850": "40 28 52", "--ink-800": "51 36 64" },
  light: { "--ink-950": "238 232 248", "--ink-900": "247 245 251", "--ink-850": "255 255 255", "--ink-800": "250 248 253" },
};

const tripletToRgb = (str: string) => {
  const [r = 255, g = 255, b = 255] = str.trim().split(/\s+/).map(Number);
  return { r, g, b };
};

/**
 * Move `baseTriplet`'s hue toward the bench hue by `blend`, around the
 * shortest arc, keeping its own saturation (floored so near-neutral foils
 * still take a whisper of the bench) and lightness. Luminance stays within
 * the tolerance the contrast floors carry (locks run 12–19:1; the twist
 * moves less than 15%).
 */
export function twistTriplet(baseTriplet: string, toHue: number, blend = 0.72): string {
  const hsl = rgbToHsl(tripletToRgb(baseTriplet));
  const delta = ((toHue - hsl.h + 540) % 360) - 180;
  return triplet(hslToRgb({ h: hsl.h + delta * blend, s: Math.max(hsl.s, 0.16), l: hsl.l }));
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

    /* The clay bench: an unknown preset or a hand-edited hex must never
       reach the dom — plates would go unpainted, or worse, half-painted. */
    if (!Number.isFinite(merged.clayPlate) || merged.clayPlate < 1 || merged.clayPlate > 6) merged.clayPlate = defaultStudio.clayPlate;
    if (!Number.isFinite(merged.clayCards) || merged.clayCards < 1 || merged.clayCards > 6) merged.clayCards = defaultStudio.clayCards;
    if (!Number.isFinite(merged.clayDepth)) merged.clayDepth = defaultStudio.clayDepth;
    if (!Number.isFinite(merged.glassFrost)) merged.glassFrost = defaultStudio.glassFrost;
    if (!Number.isFinite(merged.glassBody)) merged.glassBody = defaultStudio.glassBody;
    if (!Number.isFinite(merged.glassGlint)) merged.glassGlint = defaultStudio.glassGlint;

    return merged;
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
    return { ...defaultStudio, ...parsed };
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

  /* The crystal bench (G4). Three multipliers, written only when moved —
     the daytime doctrine lives in crystal-engine §C4 (floors and caps are
     math there, not promises here). Under every family but crystal the
     vars are inert: no consumer reads them. */
  if (studio.glassFrost !== defaultStudio.glassFrost) {
    const v = Math.min(3, Math.max(0.25, studio.glassFrost));
    vars["--glass-frost-mult"] = v.toFixed(2);
  }
  if (studio.glassBody !== defaultStudio.glassBody) {
    const v = Math.min(3, Math.max(0.25, studio.glassBody));
    vars["--glass-body-mult"] = v.toFixed(2);
  }
  if (studio.glassGlint !== defaultStudio.glassGlint) {
    const v = Math.min(3, Math.max(0, studio.glassGlint));
    vars["--glass-glint-mult"] = v.toFixed(2);
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


  /* Material-You adoption (user decree: color picked instantly from the MAIN
     color plates — the Clay bench's house/free/preset slots are retired):
     a picked accent re-derives the whole clay palette — six pastel plates,
     six channels, faces, shadows, pools — hue-rotated from the accent itself.
     clay-engine.css is untouched; the engine reads these tokens as its own. */
  /* Bench live-ness contract: the six switches must move cards IMMEDIATELY,
     even before a color plate is picked ΓÇö so the block runs on the family
     accent too, and the panel/cards rows are inputs, not ornaments. The
     plates stay Material-You (hue-rotated from the effective accent); the
     two rows split them: --panel-face (feature cards) vs --card-face (the
     .plate cards on screen). Default: both wear plate 1. */
  if (studio.accent || studio.clayPlate !== 1 || studio.clayCards !== 1) {
    const { h: hub } = rgbToHsl(hexToTriplet(studio.accent ?? FAMILY_ACCENT[family]!));
    const rot = (dh: number, s: number, l: number) =>
      triplet(hslToRgb({ h: (hub + dh + 360) % 360, s, l }));
    const plateHues: Array<[number, number, number]> = [[0, 0.62, 0.8], [55, 0.58, 0.72], [110, 0.6, 0.82], [180, 0.56, 0.74], [245, 0.62, 0.8], [310, 0.58, 0.72]];
    plateHues.forEach(([dh, s, l], i) => { vars[`--plate-${i + 1}`] = rot(dh, s, l); });
    const chanHues = [0, 42, 88, 160, 220, 300];
    const chanNames = ["lavender", "sky", "mint", "peach", "lemon", "pink"];
    chanHues.forEach((dh, i) => { vars[`--clay-${chanNames[i]}`] = rot(dh, 0.68, 0.72); });

    /* LAYER SEPARATION (user decree: "main card color vs cards — both should
       be different"): the Material-You discipline is surfaces at a WHISPER of
       the hue (blend 0.16, luminance untouched — the contrast locks hold),
       chroma in the plates + wallpaper. The 0.72 blend was washing every face
       to the accent's own color — the monoculture the user saw. */
    /* Faces restored to clay's pastel identity (0.72 — the richness);
       separation is done by HUE-STEPPING the ambient, not by bleaching the
       material: pools and band drift −30°/+38° so plates and wallpaper read
       as family, not clone. */
    for (const [channel, base] of Object.entries(CLAY_CARD_BASE[target])) {
      vars[channel] = twistTriplet(base, hub, 0.72);
    }
    /* Hardness + richness of the mould: the drop grounds at its own hue,
       DEEPER (0.78/0.38 — the pillow reads weight, not mist); the interior
       wash deepens with the dial (0.16× — the plate's puff is felt); the
       band pulls NEUTRAL so plates and bands never share one wash. */
    vars["--clay-drop-h"] = rot(0, 0.85, 0.35);
    vars["--card-tint-h"] = rot(0, 0.5, 0.82);
    vars["--card-tint-h2"] = rot(38, 0.55, 0.82);
    vars["--clay-wash"] = (0.18 * studio.tint).toFixed(3);
    vars["--panel-face"] = vars[`--plate-${studio.clayPlate}`] ?? vars["--plate-1"]!;
    /* The on-screen cards: the chosen plate softened to air — it keeps the
       plate's hue/saturation at a near-surface light, so cards read as clay
       (pastel body) yet never fight the ink. */
    {
      /* RICHNESS DECREE: cards wear the PLATE ITSELF (full pastel body —
       L~0.78–0.82, the same palette the house patterns ship), not the
       near-white wash. The ladder steps work ON that: data cards raise
       toward light, overlays above them again — the material steps, never
       blanks. Midnight inverts: deeper + denser. */
      const cd = plateHues[studio.clayCards - 1] ?? plateHues[0]!;
      /* D1 recalibration (P02): paler steps KEEP SATURATION — hue stays,
         +0.10/+0.05 s as lightness rises; a lighter-but-pale tile read
         as "white card with blue shadow" (user screenshots 10:42).
         Midnight dims slightly instead — plastic glare guard. */
      vars["--card-face"] = rot(cd[0], cd[1], target === "dark" ? 0.3 : cd[2]);
      vars["--data-face"] = rot(cd[0], Math.min(0.78, cd[1] + 0.1), target === "dark" ? 0.37 : Math.min(0.95, cd[2] + 0.055));
      vars["--overlay-face"] = rot(cd[0], Math.min(0.72, cd[1] + 0.05), target === "dark" ? 0.26 : Math.min(0.97, cd[2] + 0.115));
    }
    /* P10 PAPER MODE (ref: ClayDash cream / ClayHub fog): the ambient goes
       neutral paper so the plates IGNITE — band at s0.12 (a hue whisper),
       pools desaturated + gossamer. Cards/channels/tints keep the serif
       saturation. Crystal/glass wallpaper ("the floors breathe the accent")
       is untouched: this branch is clay-family grammar only. */
    if (family === "clay") {
      vars["--band-face"] = rot(-24, 0.12, target === "dark" ? 0.18 : 0.955);
      vars["--pool-1"] = `rgb(${rot(0, 0.35, 0.72)} / ${target === "dark" ? 0.07 : 0.09})`;
      vars["--pool-2"] = `rgb(${rot(38, 0.35, 0.78)} / ${target === "dark" ? 0.045 : 0.07})`;
      vars["--pool-3"] = `rgb(${rot(330, 0.35, 0.8)} / ${target === "dark" ? 0.04 : 0.06})`;
      /* P13 ONE HUE FAMILY (ref: user 12:44/12:46 screenshots — violet
         islands under an orange/green bench): the zone bands finally read
         the adoption. Their faces used to be house-violet literals that
         no token reached (.band-rich 60 42 94, .band-deep/glass-primary
         50 34 76 at midnight; full-saturation --clay-lavender channel by
         day), so every picked accent stranded violet slabs on the page.
         Now the zones hue-rotate with the bench like cards and pools:
         sunken deep, lit band one step under the card face at midnight,
         paper-lit whispers by day — the purple the user asked about is
         gone unless purple is the pick. */
      vars["--band-rich-h"] = rot(-24, 0.3, target === "dark" ? 0.235 : 0.93);
      vars["--band-deep-h"] = rot(-24, 0.26, target === "dark" ? 0.19 : 0.9);
      vars["--band-glow-h"] = rot(0, 0.8, target === "dark" ? 0.5 : 0.42);
    } else {
      vars["--band-face"] = rot(-24, 0.3, 0.948);
      vars["--pool-1"] = `rgb(${rot(0, 0.7, 0.5)} / ${target === "dark" ? 0.1 : 0.13})`;
      vars["--pool-2"] = `rgb(${rot(38, 0.7, 0.7)} / ${target === "dark" ? 0.06 : 0.12})`;
      vars["--pool-3"] = `rgb(${rot(330, 0.7, 0.72)} / ${target === "dark" ? 0.05 : 0.1})`;
    }
  }

  /*
   * Phase 3 — the depth dial is independent of the bench: a visitor may
   * want house plates that cast deeper shadows. Written like `--tint-mix`
   * (only when off default) so the shipped recipe stays stylesheet-owned,
   * and clamped server- and client-side to the dial's own bounds.
   */
  if (studio.clayDepth !== defaultStudio.clayDepth) {
    vars["--clay-depth"] = Math.min(2, Math.max(0.5, studio.clayDepth)).toFixed(2);
  }

  // Tokens that were not overridden must return to the stylesheet's value, so
  // the previous run's variables are cleared before the new set is written.
  //
  // Phase 2: the writes are FILTERED — a setProperty for a value the inline
  // style already holds still dirties style recalculation downstream, and a
  // removeProperty of an absent property is a wasted mutation record. Every
  // applicator path on this site (dial drags, observer passes, theme/family
  // switches) calls through here, so this one loop is the whole cost budget.
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
  try {
    window.localStorage.setItem(STUDIO_STORAGE_KEY, JSON.stringify(next));
  } catch {
    /* Storage may be unavailable. */
  }
  emit();
}

let applyFrame = 0;
function scheduleApply(studio: Studio) {
  if (applyFrame) return;
  if (typeof requestAnimationFrame !== "function") {
    applyStudioVars(studio);
    return;
  }
  applyFrame = requestAnimationFrame(() => {
    applyFrame = 0;
    applyStudioVars(read());
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
