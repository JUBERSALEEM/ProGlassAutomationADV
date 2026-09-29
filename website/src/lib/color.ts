/**
 * Colour arithmetic for the appearance studio.
 *
 * Deliberately small and dependency-free: the studio runs in the browser and
 * has to derive a whole ramp from one hex value on every drag of a slider, so
 * these are plain functions over sRGB and HSL.
 */

export type Rgb = { r: number; g: number; b: number };

/** `#RRGGBB` (or `#RGB`) → components. Falls back to the input default. */
export function hexToRgb(hex: string, fallback: Rgb = { r: 139, g: 92, b: 246 }): Rgb {
  const clean = hex.trim().replace(/^#/, "");
  const full =
    clean.length === 3
      ? clean
          .split("")
          .map((char) => char + char)
          .join("")
      : clean;
  if (!/^[0-9a-fA-F]{6}$/.test(full)) return fallback;
  return {
    r: Number.parseInt(full.slice(0, 2), 16),
    g: Number.parseInt(full.slice(2, 4), 16),
    b: Number.parseInt(full.slice(4, 6), 16),
  };
}

export function rgbToHex({ r, g, b }: Rgb): string {
  const part = (value: number) =>
    Math.max(0, Math.min(255, Math.round(value))).toString(16).padStart(2, "0");
  return `#${part(r)}${part(g)}${part(b)}`;
}

/** A CSS-ready triplet, which is the form every token in this site uses. */
export function triplet({ r, g, b }: Rgb): string {
  return `${Math.round(r)} ${Math.round(g)} ${Math.round(b)}`;
}

type Hsl = { h: number; s: number; l: number };

export function rgbToHsl({ r, g, b }: Rgb): Hsl {
  const rr = r / 255;
  const gg = g / 255;
  const bb = b / 255;
  const max = Math.max(rr, gg, bb);
  const min = Math.min(rr, gg, bb);
  const l = (max + min) / 2;
  if (max === min) return { h: 0, s: 0, l };

  const d = max - min;
  const s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
  let h: number;
  if (max === rr) h = ((gg - bb) / d + (gg < bb ? 6 : 0)) / 6;
  else if (max === gg) h = ((bb - rr) / d + 2) / 6;
  else h = ((rr - gg) / d + 4) / 6;
  return { h: h * 360, s, l };
}

export function hslToRgb({ h, s, l }: Hsl): Rgb {
  const hue = ((h % 360) + 360) % 360 / 360;
  if (s === 0) {
    const value = l * 255;
    return { r: value, g: value, b: value };
  }
  const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
  const p = 2 * l - q;
  const channel = (t: number) => {
    let tt = t;
    if (tt < 0) tt += 1;
    if (tt > 1) tt -= 1;
    if (tt < 1 / 6) return p + (q - p) * 6 * tt;
    if (tt < 1 / 2) return q;
    if (tt < 2 / 3) return p + (q - p) * (2 / 3 - tt) * 6;
    return p;
  };
  return {
    r: channel(hue + 1 / 3) * 255,
    g: channel(hue) * 255,
    b: channel(hue - 1 / 3) * 255,
  };
}

/** Nudges lightness and saturation, keeping hue. Positive = lighter/vivid. */
export function shift(hex: string, lightness: number, saturation = 0): Rgb {
  const hsl = rgbToHsl(hexToRgb(hex));
  return hslToRgb({
    h: hsl.h,
    s: Math.max(0, Math.min(1, hsl.s + saturation)),
    l: Math.max(0, Math.min(1, hsl.l + lightness)),
  });
}

export type Ramp = {
  "50": string;
  "200": string;
  "300": string;
  "400": string;
  "500": string;
  "600": string;
  "700": string;
};

/**
 * Builds the seven-step ramp the site's tokens are written against.
 *
 * On a dark surface the light end is the readable text colour, so 300 and 400
 * move *up*; on a light surface they move down and stay saturated enough to
 * hold contrast. That is why the ramp needs to know which mode is active.
 */
export function rampFor(hex: string, target: "dark" | "light", brightness = 0, vividness = 0): Ramp {
  const base = hexToRgb(hex);
  const hsl = rgbToHsl(base);
  const step = (lightness: number, saturation: number) =>
    triplet(
      hslToRgb({
        h: hsl.h,
        s: Math.max(0, Math.min(1, hsl.s + saturation + vividness)),
        l: Math.max(0.02, Math.min(0.98, hsl.l + lightness + brightness)),
      }),
    );

  if (target === "dark") {
    return {
      "50": step(0.44, -0.12),
      "200": step(0.3, -0.06),
      "300": step(0.2, -0.02),
      "400": step(0.09, 0),
      "500": step(0, 0),
      "600": step(-0.08, 0.02),
      "700": step(-0.16, 0.02),
    };
  }

  return {
    "50": step(0.34, -0.3),
    "200": step(0.24, -0.12),
    "300": step(-0.14, 0.04),
    "400": step(-0.08, 0.04),
    "500": step(-0.14, 0.02),
    "600": step(-0.2, 0),
    "700": step(-0.26, -0.02),
  };
}

/** Relative luminance, used to pick readable text on a filled swatch. */
export function luminance(hex: string): number {
  const { r, g, b } = hexToRgb(hex);
  const channel = (value: number) => {
    const v = value / 255;
    return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
}
