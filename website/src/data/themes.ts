/**
 * Shared eight-family appearance registry. The selectable IDs intentionally
 * match the WPF ERP's persisted family identities by meaning, while the web
 * uses kebab-case IDs for DOM attributes. Legacy "crystal" is a type-only
 * compatibility alias and is not included in the selectable family list.
 */

export type ThemeMode = "light" | "dark" | "system";

/** The `data-theme` value each resolved mode writes to <html>. */
export type ResolvedTheme = "midnight" | "daylight";

/** The `data-appearance` value written to <html>. */
export type AppearanceFamily = "bento" | "spatial" | "swiss" | "holographic" | "neumorphism" | "clay" | "liquid-glass" | "glassmorphism" | "crystal";

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
  { id: "bento", label: "Bento Grid", origin: "A structured modular workspace with deliberate hierarchy, crisp spacing, and grouped information panels.", swatch: "#6478F2" },
  { id: "spatial", label: "Spatial / Immersive", origin: "Layered depth, atmospheric lighting, and restrained floating surfaces without sacrificing content clarity.", swatch: "#35B8C9" },
  { id: "swiss", label: "Swiss International", origin: "Editorial grid, precise alignment, high typographic contrast, and disciplined use of colour.", swatch: "#E5484D" },
  { id: "holographic", label: "Holographic / Iridescent", origin: "Controlled spectral accents and prismatic edge highlights on readable, mostly solid surfaces.", swatch: "#A66CFF" },
  { id: "neumorphism", label: "Neumorphism", origin: "Soft extruded controls and shallow inset wells; depth is subtle and never the only state indicator.", swatch: "#8195B4" },
  { id: "clay", label: "Claymorphism", origin: "Friendly sculpted surfaces, generous radii, and soft tinted shadows; no backdrop blur.", swatch: "#B8A6FF" },
  { id: "liquid-glass", label: "Liquid Glass", origin: "Near-clear layered panes, fine luminous edges, and restrained watery highlights with bounded blur.", swatch: "#1AC5E2" },
  { id: "glassmorphism", label: "Glassmorphism", origin: "Frosted translucent panels with clear separation and blur reserved for selected foreground surfaces.", swatch: "#48A9E8" },
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
/* Liquid Glass is the first-run family; legacy Crystal remains a non-selectable compatibility alias. */
export const DEFAULT_APPEARANCE: AppearanceFamily = "liquid-glass";

export function isThemeMode(value: unknown): value is ThemeMode {
  return value === "light" || value === "dark" || value === "system";
}

export function isAppearanceFamily(value: unknown): value is AppearanceFamily {
  return value === "bento" || value === "spatial" || value === "swiss" || value === "holographic" || value === "neumorphism" || value === "clay" || value === "liquid-glass" || value === "glassmorphism";
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
/* One-time hygiene clears stale studio dials before the eight-family recipes apply. */
var vk2="proglass-studio-attrs";
var hv="proglass-hygiene-v4";
if(sg(hv)!=="1"){sr(vk);sr(vk2);ss(hv,"1");}
var a=sg(ak);
if(a!=="bento"&&a!=="spatial"&&a!=="swiss"&&a!=="holographic"&&a!=="neumorphism"&&a!=="clay"&&a!=="liquid-glass"&&a!=="glassmorphism"){a="liquid-glass";ss(ak,a);}
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
