// @vitest-environment jsdom

/*
 * The Neumorphism engine, proven against the registry, DOM, and stylesheet.
 *
 * Its contract is intentionally stricter than “a new colour option exists”:
 *
 *   1. the retired Studio family migrates to Neumorphism everywhere a saved
 *      or DOM-carried choice can re-enter the runtime;
 *   2. at the shipped recipe the bench writes no dial tokens — moved dials
 *      write clamped multipliers and hostile stored values degrade safely;
 *   3. an accent may re-hue the material, but only at low saturation and on
 *      the engine's fixed lightness ladder;
 *   4. every engine selector stays inside the neumorphism scope and the CSS
 *      keeps the material solid: dual shadows, pressed wells, no blur.
 */

import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { beforeEach, describe, expect, it } from "vitest";
import {
  appearanceFamilies,
  isAppearanceFamily,
  normalizeAppearanceFamily,
  themeBootScript,
} from "@/data/themes";
import {
  applyStudioVars,
  decodeStudio,
  defaultStudio,
  encodeStudio,
  neuPresets,
  normalizeStudio,
} from "@/lib/useStudio";

const root = () => document.documentElement;
const prop = (name: string) => root().style.getPropertyValue(name);

const NEU_TOKENS = [
  "--neu-depth",
  "--neu-distance",
  "--neu-softness",
  "--neu-canvas",
  "--neu-face",
  "--neu-raised",
  "--neu-overlay",
  "--neu-inset",
  "--neu-light-source",
  "--neu-dark-source",
  "--neu-line",
  "--ink-950",
  "--ink-900",
  "--ink-850",
  "--ink-800",
  "--ink-700",
  "--line",
  "--line-soft",
  "--line-strong",
] as const;

function hslOf(triplet: string) {
  const [r255 = 0, g255 = 0, b255 = 0] = triplet.split(" ").map(Number);
  const r = r255 / 255;
  const g = g255 / 255;
  const b = b255 / 255;
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const lightness = (max + min) / 2;
  const delta = max - min;
  const saturation = delta === 0 ? 0 : delta / (1 - Math.abs(2 * lightness - 1));
  let hue = 0;
  if (delta !== 0) {
    if (max === r) hue = 60 * (((g - b) / delta) % 6);
    else if (max === g) hue = 60 * ((b - r) / delta + 2);
    else hue = 60 * ((r - g) / delta + 4);
  }
  return { h: (hue + 360) % 360, s: saturation, l: lightness };
}

function hueDistance(left: number, right: number) {
  const distance = Math.abs(left - right) % 360;
  return Math.min(distance, 360 - distance);
}

beforeEach(() => {
  window.localStorage.clear();
  root().removeAttribute("style");
  root().dataset.theme = "midnight";
  root().dataset.appearance = "neumorphism";
  for (const key of NEU_TOKENS) root().style.removeProperty(key);
});

describe("neumorphism family registry and migration", () => {
  it("replaces Studio in the public family list", () => {
    expect(appearanceFamilies.map((entry) => entry.id)).toEqual([
      "neumorphism",
      "monet",
      "industrial",
      "crystal",
      "clay",
    ]);
    expect(appearanceFamilies[0]).toMatchObject({
      id: "neumorphism",
      label: "Neumorphism",
      swatch: "#7180A8",
    });
    expect(isAppearanceFamily("neumorphism")).toBe(true);
    expect(isAppearanceFamily("studio")).toBe(false);
  });

  it("migrates only the retired Studio choice and falls back safely otherwise", () => {
    expect(normalizeAppearanceFamily("studio")).toBe("neumorphism");
    expect(normalizeAppearanceFamily("neumorphism")).toBe("neumorphism");
    expect(normalizeAppearanceFamily("clay")).toBe("clay");
    expect(normalizeAppearanceFamily("invented")).toBe("crystal");
    expect(normalizeAppearanceFamily(null)).toBe("crystal");
  });

  it("migrates Studio in the pre-paint boot script before validation", () => {
    expect(themeBootScript).toContain('if(a==="studio"){a="neumorphism";ss(ak,a);}');
    expect(themeBootScript).toContain('a!=="neumorphism"');
    expect(themeBootScript).not.toContain('a!=="studio"');
  });
});

describe("neumorphism bench — write-only-when-moved", () => {
  it("the shipped recipe writes no dial or adoption tokens", () => {
    applyStudioVars({ ...defaultStudio });

    expect(prop("--neu-depth")).toBe("");
    expect(prop("--neu-distance")).toBe("");
    expect(prop("--neu-softness")).toBe("");
    expect(prop("--neu-canvas")).toBe("");
    expect(prop("--neu-face")).toBe("");
    expect(prop("--neu-light-source")).toBe("");
  });

  it("each moved dial writes exactly its own multiplier", () => {
    applyStudioVars({ ...defaultStudio, neuDepth: 1.25 });
    expect(prop("--neu-depth")).toBe("1.25");

    applyStudioVars({ ...defaultStudio, neuDistance: 0.75 });
    expect(prop("--neu-distance")).toBe("0.75");

    applyStudioVars({ ...defaultStudio, neuSoftness: 1.6 });
    expect(prop("--neu-softness")).toBe("1.60");
  });

  it("clamps moved dials at the write path", () => {
    applyStudioVars({ ...defaultStudio, neuDepth: 99, neuDistance: -4, neuSoftness: 40 });

    expect(prop("--neu-depth")).toBe("1.80");
    expect(prop("--neu-distance")).toBe("0.50");
    expect(prop("--neu-softness")).toBe("2.20");
  });

  it("writes the surface-lift dial only when it is moved", () => {
    root().dataset.appearance = "neumorphism";
    root().dataset.theme = "midnight";

    applyStudioVars({ ...defaultStudio });
    expect(prop("--neu-lift")).toBe("");

    applyStudioVars({ ...defaultStudio, neuLift: 1.9 });
    expect(prop("--neu-lift")).toBe("1.90");
  });

  it("clamps a hostile surface-lift value and keeps the material ladder", () => {
    root().dataset.appearance = "neumorphism";
    root().dataset.theme = "midnight";
    applyStudioVars({ ...defaultStudio, accentKey: "Rose", accent: "#E11D48", neuLift: 9 });

    expect(prop("--neu-lift")).toBe("2.40");

    const canvas = hslOf(prop("--neu-canvas"));
    const raised = hslOf(prop("--neu-raised"));
    const inset = hslOf(prop("--neu-inset"));

    /* Lift moves the extrusions; the canvas the text is measured against
       never moves, the face stays welded to it, and the wells stay under. */
    expect(canvas.l).toBeGreaterThan(0.12);
    expect(canvas.l).toBeLessThan(0.2);
    expect(prop("--neu-face")).toBe(prop("--neu-canvas"));
    expect(raised.l).toBeGreaterThanOrEqual(canvas.l);
    expect(inset.l).toBeLessThan(canvas.l);
  });

  it("raises face separation with lift without touching the canvas", () => {
    root().dataset.appearance = "neumorphism";
    root().dataset.theme = "midnight";

    applyStudioVars({ ...defaultStudio, accentKey: "Rose", accent: "#E11D48", neuLift: 0.4 });
    const flatCanvas = hslOf(prop("--neu-canvas"));
    const flatInset = hslOf(prop("--neu-inset"));

    applyStudioVars({ ...defaultStudio, accentKey: "Rose", accent: "#E11D48", neuLift: 2.4 });
    const deepCanvas = hslOf(prop("--neu-canvas"));
    const deepInset = hslOf(prop("--neu-inset"));

    /* The page never moves — text contrast is measured against it. */
    expect(deepCanvas.l).toBeCloseTo(flatCanvas.l, 3);
    expect(prop("--neu-face")).toBe(prop("--neu-canvas"));
    /* …while the well sinks further with the dial. */
    expect(flatCanvas.l - deepInset.l).toBeGreaterThan(flatCanvas.l - flatInset.l);
  });

  it("ships recipes whose every value survives normalization unchanged", () => {
    for (const preset of neuPresets) {
      const healed = normalizeStudio({
        neuDepth: preset.neuDepth,
        neuDistance: preset.neuDistance,
        neuSoftness: preset.neuSoftness,
        neuLift: preset.neuLift,
      });

      expect(healed.neuDepth).toBe(preset.neuDepth);
      expect(healed.neuDistance).toBe(preset.neuDistance);
      expect(healed.neuSoftness).toBe(preset.neuSoftness);
      expect(healed.neuLift).toBe(preset.neuLift);
    }
  });

  it("normalizes hostile stored dial values back to the recipe", () => {
    const healed = normalizeStudio({
      neuDepth: Number.NaN,
      neuDistance: "far" as never,
      neuSoftness: null as never,
    });

    expect(healed.neuDepth).toBe(1);
    expect(healed.neuDistance).toBe(1);
    expect(healed.neuSoftness).toBe(1);
  });

  it("carries the bench through a share link", () => {
    const bench = {
      ...defaultStudio,
      neuDepth: 1.45,
      neuDistance: 0.8,
      neuSoftness: 1.9,
    };
    const decoded = decodeStudio(encodeStudio(bench));

    expect(decoded?.neuDepth).toBe(1.45);
    expect(decoded?.neuDistance).toBe(0.8);
    expect(decoded?.neuSoftness).toBe(1.9);
  });
});

describe("neumorphism accent adoption", () => {
  it("adopts hue at low saturation while preserving the midnight lightness ladder", () => {
    root().dataset.theme = "midnight";
    applyStudioVars({ ...defaultStudio, accentKey: "Rose", accent: "#E11D48" });

    const canvas = hslOf(prop("--neu-canvas"));
    const face = hslOf(prop("--neu-face"));
    const raised = hslOf(prop("--neu-raised"));
    const inset = hslOf(prop("--neu-inset"));

    expect(hueDistance(canvas.h, 347)).toBeLessThanOrEqual(3);
    expect(canvas.s).toBeLessThanOrEqual(0.26);
    expect(canvas.l).toBeGreaterThan(0.12);
    expect(canvas.l).toBeLessThan(0.2);
    /* THE canonical rule: a card is the page. Face and canvas must be the
       identical triplet, or the surface reads as a coloured tile and the
       whole family collapses into flat minimal UI. */
    expect(prop("--neu-face")).toBe(prop("--neu-canvas"));
    expect(face.l).toBeCloseTo(canvas.l, 4);
    expect(raised.l).toBeGreaterThanOrEqual(canvas.l);
    expect(inset.l).toBeLessThan(canvas.l);
    expect(prop("--ink-950")).toBe(prop("--neu-canvas"));
    expect(prop("--ink-900")).toBe(prop("--neu-face"));
  });

  it("adopts hue on the daylight ladder without brightening it into a coloured card system", () => {
    root().dataset.theme = "daylight";
    applyStudioVars({ ...defaultStudio, accentKey: "Blue", accent: "#3B82F6" });

    const canvas = hslOf(prop("--neu-canvas"));
    const face = hslOf(prop("--neu-face"));
    const raised = hslOf(prop("--neu-raised"));
    const inset = hslOf(prop("--neu-inset"));

    expect(hueDistance(canvas.h, 217)).toBeLessThanOrEqual(3);
    expect(canvas.s).toBeLessThanOrEqual(0.26);
    expect(canvas.l).toBeGreaterThan(0.9);
    expect(canvas.l).toBeLessThan(0.97);
    expect(prop("--neu-face")).toBe(prop("--neu-canvas"));
    expect(face.l).toBeCloseTo(canvas.l, 4);
    expect(raised.l).toBeGreaterThanOrEqual(canvas.l);
    expect(inset.l).toBeLessThan(canvas.l);
  });

  it("keeps achromatic accents achromatic", () => {
    applyStudioVars({ ...defaultStudio, accentKey: "White", accent: "#FFFFFF" });

    for (const key of ["--neu-canvas", "--neu-face", "--neu-raised", "--neu-inset"]) {
      expect(hslOf(prop(key)).s).toBeLessThan(0.01);
    }
  });

  it("persists adopted material and dial tokens under the studio replay key", () => {
    applyStudioVars({
      ...defaultStudio,
      accentKey: "Emerald",
      accent: "#059669",
      neuDepth: 1.3,
      neuDistance: 0.8,
      neuSoftness: 1.5,
    });

    const replay = JSON.parse(window.localStorage.getItem("proglass-studio-vars") ?? "{}") as Record<string, string>;
    expect(replay["--neu-canvas"]).toBe(prop("--neu-canvas"));
    expect(replay["--neu-light-source"]).toBe(prop("--neu-light-source"));
    expect(replay["--neu-depth"]).toBe("1.30");
    expect(replay["--neu-distance"]).toBe("0.80");
    expect(replay["--neu-softness"]).toBe("1.50");
  });

  it("does not adopt neumorphism surfaces under another family", () => {
    root().dataset.appearance = "crystal";
    applyStudioVars({ ...defaultStudio, accentKey: "Rose", accent: "#E11D48" });

    expect(prop("--neu-canvas")).toBe("");
    expect(prop("--neu-face")).toBe("");
    expect(prop("--neu-light-source")).toBe("");
  });
});

describe("neumorphism stylesheet contract", () => {
  const css = readFileSync(resolve(process.cwd(), "src/app/neumorphism-engine.css"), "utf8");

  it("owns dual-shadow, raised, and pressed material tokens", () => {
    expect(css).toContain("--neu-shadow-panel");
    expect(css).toContain("--neu-shadow-raised");
    expect(css).toContain("--neu-shadow-well");
    expect(css).toContain("--neu-depth");
    expect(css).toContain("--neu-distance");
    expect(css).toContain("--neu-softness");
    expect(css).toContain("--neu-light-source");
    expect(css).toContain("--neu-dark-source");
  });

  it("keeps the material solid and covers surfaces, navigation, buttons, and wells", () => {
    expect(css).toContain("backdrop-filter: none");
    expect(css).toContain('[class*="backdrop-blur"]');
    expect(css).toContain(".surface");
    expect(css).toContain(".surface-raised");
    expect(css).toContain(".nav-glass");
    expect(css).toContain(".btn-primary");
    expect(css).toContain(":is(input, textarea, select, .field-control)");
    expect(css).toContain("input[type=\"range\"]::-webkit-slider-thumb");
    expect(css).toContain("@media (forced-colors: active)");
    expect(css).toContain("@media (prefers-reduced-motion: reduce)");
  });

  it("keeps the canonical reference recipe: one colour, solid pair, 18/30, identity fill", () => {
    /* The plate this family is built from:
       bg #ECF0F3 · shadow 18px/30px #D1D9E6 · highlight -18px/30px #FFFFFF,
       with the card fill IDENTICAL to the page. */
    expect(css).toContain("--neu-canvas: 236 240 243");
    expect(css).toContain("--neu-face: 236 240 243");
    expect(css).toContain("--neu-dark-source: 209 217 230");
    expect(css).toContain("--neu-light-source: 255 255 255");
    expect(css).toContain("calc(18px * var(--neu-distance))");
    expect(css).toContain("calc(30px * var(--neu-softness))");

    /* Both shadow colours are carried at full strength. A 20–40% alpha grey
       is what made the pair invisible against the fill. */
    expect(css).toContain("--neu-light-alpha: 1;");

    /* A card must not repaint a tint or a glass stack over its own fill. */
    expect(css).toContain("background-image: none");

    /* The outer pair belongs to cards; inset belongs to wells. A card that
       carries an inset pair reads as a framed rectangle, not a pillow. */
    const surfaceBlock = css.slice(
      css.indexOf("--neu-shadow-surface:"),
      css.indexOf("--neu-shadow-panel:"),
    );
    expect(surfaceBlock).not.toContain("inset");
  });

  it("never leaks a selector outside the neumorphism family", () => {
    const selectorHeads = css
      .split("}")
      .map((chunk) => chunk.slice(chunk.lastIndexOf("/*") === -1 ? 0 : chunk.indexOf("*/") + 2))
      .map((chunk) => chunk.split("{")[0]?.trim() ?? "")
      .filter((head) => head && !head.startsWith("@media") && !head.startsWith("@supports"));

    for (const head of selectorHeads) {
      expect(head).toContain('[data-appearance="neumorphism"]');
    }
    expect(css).not.toContain('[data-appearance="studio"]');
  });
});
