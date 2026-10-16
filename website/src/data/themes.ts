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
export type AppearanceFamily = "studio" | "monet" | "industrial" | "crystal" | "clay";

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
    swatch: "#E469A0",
  },
  {
    id: "industrial",
    label: "Industrial",
    origin: "The application's strict default family — crisp sky over slate, minimal tinting.",
    swatch: "#2DB0F0",
  },
  {
    id: "crystal",
    label: "Crystal",
    origin: "The application's Crystal Fluid family: see-through surfaces over a luminous environment.",
    swatch: "#1AC5E2",
  },
  {
    id: "clay",
    label: "Clay",
    origin:
      "Claymorphism — solid pastel surfaces with puffed corners and a soft tinted pillow-shadow, moulded rather than assembled. Rounded higher, and with no backdrop blur at all, it is also the cheapest family to paint.",
    swatch: "#B8A6FF",
  },
];

/**
 * Accent presets, transcribed from `Services/Theme/AccentPresets.cs`.
 * Published on the appearance page so the palette the product ships is
 * visible as a product fact rather than described in prose.
 */
export const accentPresets: { key: string; name: string; hex: string | null }[] = [
  { key: "GlassBlue", name: "Glass Blue", hex: null }, // theme default
  // Neutral and elementary plates are explicit choices, not hidden behind the
  // custom picker. White is a material palette (white faces + neutral shadow),
  // while the other basics provide familiar starting points by name.
  { key: "White", name: "White", hex: "#FFFFFF" },
  { key: "Silver", name: "Silver", hex: "#D1D5DB" },
  { key: "Gray", name: "Gray", hex: "#6B7280" },
  { key: "Red", name: "Red", hex: "#EF4444" },
  { key: "Blue", name: "Blue", hex: "#3B82F6" },
  { key: "Green", name: "Green", hex: "#16A34A" },
  { key: "Yellow", name: "Yellow", hex: "#FACC15" },
  { key: "Purple", name: "Purple", hex: "#A855F7" },
  { key: "Brown", name: "Brown", hex: "#92400E" },
  { key: "Navy", name: "Navy", hex: "#1E3A8A" },
  { key: "Turquoise", name: "Turquoise", hex: "#14B8A6" },
  { key: "Pink", name: "Pink", hex: "#F472B6" },
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
  /*
   * The website's own vivid plates — beyond the application's sixteen,
   * added for the readers who asked for more colour at full strength. Same
   * mechanics as the rest of the grid: one tap recolours the whole site,
   * tinted shadows and all.
   */
  { key: "Brick", name: "Brick", hex: "#DC2626" },
  { key: "Canary", name: "Canary", hex: "#EAB308" },
  { key: "Azure", name: "Azure", hex: "#0BA5F4" },
  { key: "Leaf", name: "Leaf", hex: "#22C55E" },
  { key: "Magenta", name: "Magenta", hex: "#EC4899" },
  { key: "Tangerine", name: "Tangerine", hex: "#FB923C" },
  { key: "Slate", name: "Slate", hex: "#475569" },
  /* Was #111827 — a dark slate BLUE (b=39 over r=17), so the "Black" plate
     applied as navy everywhere. True neutral black now. */
  { key: "Black", name: "Black", hex: "#0C0C0F" },
  { key: "Custom", name: "Custom", hex: null },
];

/** Storage keys. Kept together so the boot script and the store agree. */
export const THEME_STORAGE_KEY = "proglass-theme-mode";
export const APPEARANCE_STORAGE_KEY = "proglass-appearance";

export const DEFAULT_THEME_MODE: ThemeMode = "system";
/*
 * The first-run family. Crystal is the liquid-glass identity the site is
 * art-directed around; a stored choice (any family, including Clay) always
 * wins over this default — the default only greets a visitor who has never
 * picked.
 */
export const DEFAULT_APPEARANCE: AppearanceFamily = "crystal";

export function isThemeMode(value: unknown): value is ThemeMode {
  return value === "light" || value === "dark" || value === "system";
}

export function isAppearanceFamily(value: unknown): value is AppearanceFamily {
  return value === "studio" || value === "monet" || value === "industrial" || value === "crystal" || value === "clay";
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
var sg=function(k){try{return localStorage.getItem(k)}catch(e){return null}};
var ss=function(k,v){try{localStorage.setItem(k,v)}catch(e){}};
var sr=function(k){try{localStorage.removeItem(k)}catch(e){}};
var mk=${JSON.stringify(THEME_STORAGE_KEY)},ak=${JSON.stringify(APPEARANCE_STORAGE_KEY)},vk="proglass-studio-vars";
var m=sg(mk);
if(m!=="light"&&m!=="dark"&&m!=="system")m="system";
/* Hygiene v2 — rides BEFORE the appearance resolves, so it takes effect on
   this very boot: clears the stored studio dials (an old edge/scale dial
   could outrank the family recipe's own corners and strokes) AND lands
   the visitor on the Crystal family once — the liquid-glass identity
   (Phase 5 rollout, user-approved 14 Oct 2026; hygiene v3 had landed
   everyone on Clay). The palette picker still switches away — this
   just makes Crystal the first thing every visitor feels, including
   a returning one whose storage pinned an older family. */
var vk2="proglass-studio-attrs";
var hv="proglass-hygiene-v4";
if(sg(hv)!=="1"){sr(vk);sr(vk2);ss(ak,"crystal");ss(hv,"1");}
var a=sg(ak);
if(a!=="studio"&&a!=="monet"&&a!=="industrial"&&a!=="crystal"&&a!=="clay")a="crystal";
var d=window.matchMedia?window.matchMedia("(prefers-color-scheme: dark)").matches:true;
var t=m==="light"?"daylight":m==="dark"?"midnight":(d?"midnight":"daylight");
var e=document.documentElement;
e.setAttribute("data-theme",t);
e.setAttribute("data-theme-mode",m);
e.setAttribute("data-appearance",a);
e.style.colorScheme=t==="daylight"?"light":"dark";
var v=sg(vk);
if(v){var o=JSON.parse(v);for(var k in o){if(Object.prototype.hasOwnProperty.call(o,k)){
var w=o[k];
if(k==="--ui-scale"){var n2=parseFloat(w);if(!isFinite(n2)||n2<0.9||n2>1.15)continue;}
/* P18 — a poisoned blob (any "NaN"/"Infinity" string, from a stale build's
   write path or a hand edit) must never reach the DOM: calc() drops the
   whole declaration, which is "the frost/font/colour is gone forever".
   The studio now normalises + heals the state on read, so a skipped value
   here is re-painted from healthy defaults on the same load. */
if(/nan|infinity/i.test(w))continue;
if(typeof w!=="string"||w.length>96||k.slice(0,2)!=="--")continue;
e.style.setProperty(k,w);}}}
var v2=sg(vk2);
if(v2){var o2=JSON.parse(v2);for(var k2 in o2){if(Object.prototype.hasOwnProperty.call(o2,k2))e.setAttribute(k2,o2[k2]);}}
var cs=document.currentScript;if(cs&&cs.parentNode)cs.parentNode.removeChild(cs);
}catch(e){}})();`;
