/**
 * Appearance registry — the website mirrors the application's own
 * Settings → Appearance, so a visitor can see the product's palette on the
 * site rather than being told about it.
 *
 * Ground truth, from the WPF repository:
 *
 *   VisualThemeEngine.Part1.cs   — the four selectable families:
 *                                  Industrial (default), Monet,
 *                                  Crystal Fluid (displayed "iPhone 17 Pro Max"),
 *                                  iOS 26
 *   AppConfig.cs                 — VisualTheme default and migration notes
 *   AccentPresets.cs             — the 16 accent presets + Custom, with hex
 *   BuiltinWallpaperScenes.cs    — Monet paints an accent-derived "Golden Hour"
 *
 * What the website does with that: two independent controls, one click each.
 *
 *   MODE      Light · Dark · System        (surface lightness)
 *   FAMILY    Studio · Monet · Industrial · Crystal
 *
 * `Studio` is this website's own palette — deep violet, derived from the
 * Violet accent preset. The other three follow the application's families so
 * the site and the product read as one piece of work. Mode and family are
 * independent, which is how the application treats them too (ModePairFor).
 */

export type ThemeMode = "light" | "dark" | "system";

/** The `data-theme` value each resolved mode writes to <html>. */
export type ResolvedTheme = "midnight" | "daylight";

/** The `data-appearance` value written to <html>. */
export type AppearanceFamily = "studio" | "monet" | "industrial" | "crystal";

export type ThemeModeEntry = {
  id: ThemeMode;
  label: string;
  hint: string;
  icon: "sun" | "moon" | "system";
};

export type AppearanceFamilyEntry = {
  id: AppearanceFamily;
  label: string;
  /** One line: where this palette comes from. */
  origin: string;
  /** Swatch colour for the chip, in the family's own accent. */
  swatch: string;
};

export const themeModes: ThemeModeEntry[] = [
  { id: "light", label: "Light", hint: "Paper surfaces, same identity.", icon: "sun" },
  { id: "dark", label: "Dark", hint: "Deep shell, the product's own dark theme.", icon: "moon" },
  { id: "system", label: "System", hint: "Follows your operating system setting.", icon: "system" },
];

export const appearanceFamilies: AppearanceFamilyEntry[] = [
  {
    id: "studio",
    label: "Studio",
    origin: "The website's own palette — the application's Violet accent, taken deep.",
    swatch: "#7E57CE",
  },
  {
    id: "monet",
    label: "Monet",
    origin:
      "The application's Material-You family: surfaces derived from the accent's own dusk (its Golden Hour wallpaper).",
    swatch: "#E879A6",
  },
  {
    id: "industrial",
    label: "Industrial",
    origin: "The application's strict default family — crisp sky over slate, minimal tinting.",
    swatch: "#38BDF8",
  },
  {
    id: "crystal",
    label: "Crystal",
    origin: "The application's Crystal Fluid family: see-through surfaces over a luminous environment.",
    swatch: "#22D3EE",
  },
];

/**
 * Accent presets, transcribed from `Services/Theme/AccentPresets.cs`.
 * Published on the appearance page so the palette the product ships is
 * visible as a product fact rather than described in prose.
 */
export const accentPresets: { key: string; name: string; hex: string | null }[] = [
  { key: "GlassBlue", name: "Glass Blue", hex: null }, // theme default
  { key: "RoyalBlue", name: "Royal Blue", hex: "#2563EB" },
  { key: "Sky", name: "Sky", hex: "#0284C7" },
  { key: "Cyan", name: "Cyan", hex: "#0891B2" },
  { key: "Teal", name: "Teal", hex: "#0D9488" },
  { key: "Emerald", name: "Emerald", hex: "#059669" },
  { key: "Lime", name: "Lime", hex: "#65A30D" },
  { key: "Gold", name: "Gold", hex: "#CA8A04" },
  { key: "Amber", name: "Amber", hex: "#D97706" },
  { key: "Orange", name: "Orange", hex: "#EA580C" },
  { key: "Crimson", name: "Crimson", hex: "#B91C1C" },
  { key: "Rose", name: "Rose", hex: "#E11D48" },
  { key: "Fuchsia", name: "Fuchsia", hex: "#C026D3" },
  { key: "Violet", name: "Violet", hex: "#7C3AED" },
  { key: "Indigo", name: "Indigo", hex: "#4F46E5" },
  { key: "Slate", name: "Slate", hex: "#475569" },
  { key: "Black", name: "Black", hex: "#111827" },
  { key: "Custom", name: "Custom", hex: null },
];

/** Storage keys. Kept together so the boot script and the store agree. */
export const THEME_STORAGE_KEY = "proglass-theme-mode";
export const APPEARANCE_STORAGE_KEY = "proglass-appearance";

export const DEFAULT_THEME_MODE: ThemeMode = "system";
export const DEFAULT_APPEARANCE: AppearanceFamily = "studio";

export function isThemeMode(value: unknown): value is ThemeMode {
  return value === "light" || value === "dark" || value === "system";
}

export function isAppearanceFamily(value: unknown): value is AppearanceFamily {
  return value === "studio" || value === "monet" || value === "industrial" || value === "crystal";
}

/** Mode → the `data-theme` value to write on <html>. */
export function resolveTheme(mode: ThemeMode, systemPrefersDark: boolean): ResolvedTheme {
  if (mode === "light") return "daylight";
  if (mode === "dark") return "midnight";
  return systemPrefersDark ? "midnight" : "daylight";
}

/**
 * The inline boot script, injected in <head> and run before first paint.
 *
 * It is deliberately tiny and dependency-free: it reads both stored choices,
 * resolves the mode, and sets all three attributes so the very first frame is
 * already in the right palette. Without it a dark-mode visitor sees a white
 * flash. Wrapped in try/catch because localStorage throws in some privacy
 * modes.
 */
export const themeBootScript = `(function(){try{
var mk=${JSON.stringify(THEME_STORAGE_KEY)},ak=${JSON.stringify(APPEARANCE_STORAGE_KEY)},vk="proglass-studio-vars";
var m=localStorage.getItem(mk);
if(m!=="light"&&m!=="dark"&&m!=="system")m="system";
var a=localStorage.getItem(ak);
if(a!=="studio"&&a!=="monet"&&a!=="industrial"&&a!=="crystal")a="studio";
var d=window.matchMedia("(prefers-color-scheme: dark)").matches;
var t=m==="light"?"daylight":m==="dark"?"midnight":(d?"midnight":"daylight");
var e=document.documentElement;
e.setAttribute("data-theme",t);
e.setAttribute("data-theme-mode",m);
e.setAttribute("data-appearance",a);
e.style.colorScheme=t==="daylight"?"light":"dark";
var v=localStorage.getItem(vk);
if(v){var o=JSON.parse(v);for(var k in o){if(Object.prototype.hasOwnProperty.call(o,k))e.style.setProperty(k,o[k]);}}
var vk2="proglass-studio-attrs";
var v2=localStorage.getItem(vk2);
if(v2){var o2=JSON.parse(v2);for(var k2 in o2){if(Object.prototype.hasOwnProperty.call(o2,k2))e.setAttribute(k2,o2[k2]);}}
}catch(e){}})();`;
