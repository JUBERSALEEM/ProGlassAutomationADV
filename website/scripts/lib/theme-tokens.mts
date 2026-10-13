/**
 * Palette reader for the static plate renderers.
 *
 * The hero backdrop (`scripts/render-hero-backdrop.mts`) and the brand banner
 * (`scripts/render-hero-banner.mts`) are drawn in the site's own colours — they
 * are plates *of* the design language, not illustrations beside it. Each of
 * them used to carry a hand-copied snapshot of `src/app/globals.css`, and the
 * copies went stale the moment the palette was deepened: the plates carried on
 * coming out of the old violet, so the artwork quietly disagreed with the page
 * it was sitting on.
 *
 * The copies are gone. This module reads the stylesheet and hands back the
 * values that are live in it, so a plate cannot be rendered in last week's
 * palette, and a token that is renamed or deleted stops the render loudly
 * instead of silently falling back to a stale hex.
 *
 * The parser is deliberately small: it understands plain
 * `--token: r g b;` declarations, which is the form every colour in this
 * stylesheet uses. Computed values (`rgb(var(--brand-600) / 0.5)`) are skipped.
 */
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

/** `src/app/globals.css`, resolved from this file rather than from the cwd. */
const STYLESHEET = fileURLToPath(new URL("../../src/app/globals.css", import.meta.url));

export type ThemeName = "midnight" | "daylight";

/** Renderer key → the custom property it reads. */
const TOKENS = {
  ink950: "--ink-950",
  ink900: "--ink-900",
  ink850: "--ink-850",
  ink800: "--ink-800",
  ink700: "--ink-700",
  line: "--line",
  lineSoft: "--line-soft",
  lineStrong: "--line-strong",
  brand200: "--brand-200",
  brand300: "--brand-300",
  brand400: "--brand-400",
  brand500: "--brand-500",
  brand600: "--brand-600",
  brand700: "--brand-700",
  teal: "--glass-teal",
  cyan: "--glass-cyan",
  green: "--glass-green",
  rose: "--glass-rose",
  text1: "--text-1",
  text2: "--text-2",
  text3: "--text-3",
} as const;

export type TokenName = keyof typeof TOKENS;
export type Palette = Record<TokenName, string>;

/** CSS comments out of the way, so brace matching cannot trip over one. */
const stripComments = (source: string) => source.replace(/\/\*[\s\S]*?\*\//g, "");

/**
 * The body of the rule that declares `theme`, matched by braces rather than by
 * a line, so it survives reformatting.
 */
function blockFor(source: string, theme: ThemeName): string {
  const at = source.indexOf(`[data-theme="${theme}"]`);
  if (at < 0) throw new Error(`theme-tokens: no [data-theme="${theme}"] rule in ${STYLESHEET}`);

  const open = source.indexOf("{", at);
  if (open < 0) throw new Error(`theme-tokens: [data-theme="${theme}"] has no block`);

  let depth = 0;
  for (let i = open; i < source.length; i += 1) {
    const char = source[i];
    if (char === "{") depth += 1;
    else if (char === "}") {
      depth -= 1;
      if (depth === 0) return source.slice(open + 1, i);
    }
  }
  throw new Error(`theme-tokens: [data-theme="${theme}"] block is unterminated`);
}

/** `8 5 22` → `#080516`. */
function hex(triplet: string): string {
  const parts = triplet.trim().split(/\s+/).map(Number);
  if (parts.length !== 3 || parts.some((value) => !Number.isInteger(value) || value < 0 || value > 255)) {
    throw new Error(`theme-tokens: not an rgb triplet — "${triplet}"`);
  }
  return `#${parts.map((value) => value.toString(16).padStart(2, "0")).join("")}`.toUpperCase();
}

/** Every `--token: r g b;` declared in the theme's own rule. */
export function themePalette(theme: ThemeName): Palette {
  const declared = new Map<string, string>();
  const pattern = /--([a-z0-9-]+)\s*:\s*(\d{1,3}\s+\d{1,3}\s+\d{1,3})\s*;/g;

  for (const match of blockFor(stripComments(readFileSync(STYLESHEET, "utf8")), theme).matchAll(pattern)) {
    declared.set(`--${match[1]}`, match[2]);
  }

  const missing: string[] = [];
  const palette = {} as Palette;

  for (const [key, property] of Object.entries(TOKENS) as [TokenName, string][]) {
    const value = declared.get(property);
    if (value === undefined) missing.push(property);
    else palette[key] = hex(value);
  }

  if (missing.length > 0) {
    throw new Error(`theme-tokens: [data-theme="${theme}"] declares no ${missing.join(", ")}`);
  }
  return palette;
}

/** The plates are drawn in the dark palette: `[data-theme="midnight"]`. */
export const palette: Palette = themePalette("midnight");

/**
 * The studio sheet's own tokens, which are not themed.
 *
 * Two of the plates carry the application's 2D view drawn the way the operator
 * sees it, and that drawing is deliberately the same in both of the site's
 * modes — a plate is a sheet of drawing paper on the page, not a dark card with
 * pale rectangles in it. Its colours therefore live in a plain `:root` block in
 * `globals.css` rather than under a `[data-theme]` selector, and they are read
 * from there the same way the themed tokens are: the renderer owns no colours
 * of its own, so a token that moves takes the plates with it.
 */
const STUDIO_TOKENS = {
  canvas: "--cad-canvas",
  surface: "--cad-surface",
  keyline: "--cad-keyline",
  line: "--cad-line",
  bed: "--cad-bed",
  piece: "--cad-piece",
  pieceRotated: "--cad-piece-rotated",
  ink: "--cad-ink",
  text: "--cad-text",
  muted: "--cad-muted",
  subtle: "--cad-subtle",
  trim: "--cad-trim",
  breakout: "--cad-breakout",
  accent: "--cad-accent",
  accentDeep: "--cad-accent-deep",
  accentSoft: "--cad-accent-soft",
  accentLine: "--cad-accent-line",
  offcutBg: "--cad-offcut-bg",
  offcutLine: "--cad-offcut-line",
  successBg: "--cad-success-bg",
  successLine: "--cad-success-line",
  successInk: "--cad-success-ink",
  warningBg: "--cad-warning-bg",
  warningLine: "--cad-warning-line",
  warningInk: "--cad-warning-ink",
} as const;

export type StudioTokenName = keyof typeof STUDIO_TOKENS;
export type StudioPalette = Record<StudioTokenName, string>;

/**
 * Every `--cad-*: #hex;` declared anywhere in the stylesheet.
 *
 * Matched over the whole file rather than inside one block, because the studio
 * tokens are declared in a `:root` of their own and there are several `:root`
 * blocks in this stylesheet.
 */
export function studioPalette(): StudioPalette {
  const declared = new Map<string, string>();
  const pattern = /--(cad-[a-z-]+)\s*:\s*#([0-9a-fA-F]{6})\s*;/g;

  for (const match of stripComments(readFileSync(STYLESHEET, "utf8")).matchAll(pattern)) {
    declared.set(`--${match[1]}`, `#${match[2].toUpperCase()}`);
  }

  const missing: string[] = [];
  const studio = {} as StudioPalette;

  for (const [key, property] of Object.entries(STUDIO_TOKENS) as [StudioTokenName, string][]) {
    const value = declared.get(property);
    if (value === undefined) missing.push(property);
    else studio[key] = value;
  }

  if (missing.length > 0) {
    throw new Error(`theme-tokens: the stylesheet declares no ${missing.join(", ")}`);
  }
  return studio;
}

/** The colours of the 2D view, as the stylesheet declares them. */
export const studio: StudioPalette = studioPalette();

/** `#6546B5`, 0.18 → `rgba(101, 70, 181, 0.18)`. */
export function rgba(hexColour: string, alpha: number): string {
  const value = hexColour.replace("#", "");
  const channel = (at: number) => parseInt(value.slice(at, at + 2), 16);
  return `rgba(${channel(0)}, ${channel(2)}, ${channel(4)}, ${Number(alpha.toFixed(3))})`;
}
