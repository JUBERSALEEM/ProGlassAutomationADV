// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from "vitest";
import { applyStudioVars, defaultStudio } from "@/lib/useStudio";

const CLAY_TOKENS = [
  "--plate-1", "--plate-2", "--plate-3", "--plate-4", "--plate-5", "--plate-6",
  "--clay-lavender", "--clay-sky", "--clay-mint", "--clay-peach", "--clay-lemon", "--clay-pink",
  "--panel-face", "--band-face", "--card-face", "--data-face", "--overlay-face",
  "--clay-drop-h", "--card-tint-h", "--card-tint-h2", "--clay-wash",
  "--band-rich-h", "--band-deep-h", "--band-glow-h",
  "--clay-coral", "--clay-coral-h", "--clay-foil",
];

/* P14 helpers — local copies so the contract holds the numbers itself. */
const hslOf = (tp: string) => {
  const [r255 = 0, g255 = 0, b255 = 0] = tp.split(" ").map(Number);
  const r = r255 / 255, g = g255 / 255, b = b255 / 255;
  const max = Math.max(r, g, b), min = Math.min(r, g, b);
  const l = (max + min) / 2;
  const d = max - min;
  const s = d === 0 ? 0 : d / (1 - Math.abs(2 * l - 1));
  let h = 0;
  if (d !== 0) {
    if (max === r) h = 60 * (((g - b) / d) % 6);
    else if (max === g) h = 60 * ((b - r) / d + 2);
    else h = 60 * ((r - g) / d + 4);
  }
  return { h: (h + 360) % 360, s, l };
};
const whiteCR = (tp: string) => {
  const [r = 0, g = 0, b = 0] = tp.split(" ").map(Number);
  const f = (c: number) => { const s = c / 255; return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4; };
  const L = 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
  return 1.05 / (L + 0.05);
};

beforeEach(() => {
  window.localStorage.clear();
  document.documentElement.dataset.theme = "daylight";
  document.documentElement.removeAttribute("data-appearance");
  for (const key of CLAY_TOKENS) document.documentElement.style.removeProperty(key);
  document.documentElement.style.removeProperty("--ink-850");
});

describe("clay bench — the adoption contract (custom support is gone)", () => {
  it("defaults write nothing — the stylesheet owns the shipped palette", () => {
    applyStudioVars(defaultStudio);
    expect(document.documentElement.style.getPropertyValue("--plate-1")).toBe("");
    expect(document.documentElement.style.getPropertyValue("--clay-lavender")).toBe("");
    expect(document.documentElement.style.getPropertyValue("--panel-face")).toBe("");
    expect(document.documentElement.style.getPropertyValue("--card-face")).toBe("");
  });

  it("a picked accent re-derives the whole bench at once", () => {
    applyStudioVars({ ...defaultStudio, accent: "#0891b2" });
    for (const key of CLAY_TOKENS.slice(0, 12)) {
      expect(document.documentElement.style.getPropertyValue(key)).not.toBe("");
    }
  });

  it("the bench stays pastel — a dark accent moves the plates but never screams", () => {
    applyStudioVars({ ...defaultStudio, accent: "#3b1d6e" });
    const written = document.documentElement.style.getPropertyValue("--clay-lavender");
    const [r = 0, g = 0, b = 0] = written.split(" ").map(Number);
    expect(0.2126 * r + 0.7152 * g + 0.0722 * b).toBeGreaterThanOrEqual(140);
    expect(b).toBeGreaterThan(r); /* the violet stays violet */
  });

  it("bench tokens persist under the studio replay key", () => {
    applyStudioVars({ ...defaultStudio, accent: "#f97316" });
    const replay = JSON.parse(window.localStorage.getItem("proglass-studio-vars") ?? "{}");
    expect(replay["--plate-3"]).not.toBeUndefined();
    expect(replay["--plate-1"]).toBe(document.documentElement.style.getPropertyValue("--plate-1"));
  });
});

describe("tonal ladder — nesting reads as material, not shadows", () => {
  it("content card < data card < overlay in lightness, same hue family", () => {
    applyStudioVars({ ...defaultStudio, accent: "#2f9e44" });
    const lum = (k: string) => {
      const [r = 0, g = 0, b = 0] = document.documentElement.style.getPropertyValue(k).split(" ").map(Number);
      return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    };
    expect(lum("--data-face")).toBeGreaterThan(lum("--card-face"));
    expect(lum("--overlay-face")).toBeGreaterThan(lum("--data-face"));
  });

  it("the two rows dress cards and feature card independently", () => {
    applyStudioVars({ ...defaultStudio, accent: "#2f9e44", clayPlate: 2, clayCards: 4 });
    expect(document.documentElement.style.getPropertyValue("--card-face")).not.toBe(
      document.documentElement.style.getPropertyValue("--panel-face"),
    );
    applyStudioVars({ ...defaultStudio, accent: "#2f9e44" });
    /* default rows are the same plate — the zones match when untouched */
    expect(document.documentElement.style.getPropertyValue("--card-face")).toBe(
      document.documentElement.style.getPropertyValue("--panel-face"),
    );
  });

  it("card faces twist toward the bench hue without losing their lightness", () => {
    applyStudioVars({ ...defaultStudio, accent: "#0891b2" });
    /* hue direction is read on a pastel channel (daylight faces sit at
       L1.0 where hue mathematically cannot move) */
    const [lr = 0, lg = 0] = document.documentElement.style.getPropertyValue("--clay-lavender").split(" ").map(Number);
    expect(lg).toBeGreaterThan(lr);
    /* …and the midnight faces (the hued ones) keep their own luminance */
    document.documentElement.dataset.theme = "midnight";
    applyStudioVars({ ...defaultStudio, accent: "#0891b2" });
    const written = document.documentElement.style.getPropertyValue("--ink-850");
    expect(written).not.toBe("");
    const [r = 0, g = 0, b = 0] = written.split(" ").map(Number);
    expect(g).toBeGreaterThanOrEqual(r);
    const twisted = 0.2126 * r + 0.7152 * g + 0.0722 * b;
    const base = 0.2126 * 40 + 0.7152 * 28 + 0.0722 * 56;
    expect(Math.abs(twisted - base) / base).toBeLessThan(0.35);
  });

  it("the drop shadow and wash hues leave the house violet with the bench", () => {
    applyStudioVars({ ...defaultStudio, accent: "#0891b2" });
    expect(document.documentElement.style.getPropertyValue("--clay-drop-h")).not.toBe("140 108 255");
    expect(document.documentElement.style.getPropertyValue("--clay-drop-h")).not.toBe("");
    expect(document.documentElement.style.getPropertyValue("--card-tint-h")).not.toBe("");
    expect(document.documentElement.style.getPropertyValue("--card-tint-h2")).not.toBe("");
  });

  it("the wash depth rides the tint dial at 0.18×", () => {
    applyStudioVars({ ...defaultStudio, accent: "#f97316", tint: 0 });
    expect(document.documentElement.style.getPropertyValue("--clay-wash")).toBe("0.000");
    applyStudioVars({ ...defaultStudio, accent: "#f97316", tint: 2 });
    expect(document.documentElement.style.getPropertyValue("--clay-wash")).toBe("0.360");
  });

  it("P10 paper mode: clay ambient desaturates while the cards keep the plate", () => {
    document.documentElement.dataset.appearance = "clay";
    applyStudioVars({ ...defaultStudio, accent: "#166534" });
    const pur = (k: string) => {
      const v = document.documentElement.style.getPropertyValue(k).split(" ").map(Number);
      return (Math.max(...v) - Math.min(...v)) / 255;
    };
    /* the band is near-neutral paper; the card family keeps the hue */
    expect(pur("--card-face")).toBeGreaterThan(pur("--band-face") * 2);
  });

  it("P13 one hue family: the zone bands leave their violet literals for the accent", () => {
    document.documentElement.dataset.appearance = "clay";
    document.documentElement.dataset.theme = "midnight";
    applyStudioVars({ ...defaultStudio, accent: "#f97316" });
    const rich = document.documentElement.style.getPropertyValue("--band-rich-h");
    const deep = document.documentElement.style.getPropertyValue("--band-deep-h");
    const glow = document.documentElement.style.getPropertyValue("--band-glow-h");
    expect(rich).not.toBe("");
    expect(deep).not.toBe("");
    expect(glow).not.toBe("");
    const [rr = 0, , rb = 0] = rich.split(" ").map(Number);
    /* warm accent → warm lit band; the stranded violet slab is gone */
    expect(rr).toBeGreaterThan(rb);
    /* the zone ladder holds: sunken below lit band below the card face */
    const lum = (k: string) => {
      const [r = 0, g = 0, b = 0] = document.documentElement.style.getPropertyValue(k).split(" ").map(Number);
      return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    };
    expect(lum("--band-rich-h")).toBeGreaterThan(lum("--band-deep-h"));
    expect(lum("--card-face")).toBeGreaterThan(lum("--band-rich-h"));
    /* and the lit band still answers 7:1 to the white band ink */
    const lin = (c: number) => { const s = c / 255; return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4; };
    const [r2 = 0, g2 = 0, b2 = 0] = rich.split(" ").map(Number);
    const L = 0.2126 * lin(r2) + 0.7152 * lin(g2) + 0.0722 * lin(b2);
    expect(1.05 / (L + 0.05)).toBeGreaterThanOrEqual(7);
  });

  it("P14 the action hue is the bench hue — coral, ladder and plates all follow, all floored", () => {
    document.documentElement.dataset.appearance = "clay";
    document.documentElement.dataset.theme = "midnight";
    for (const accent of ["#f97316", "#22c55e", "#3b82f6"]) {
      applyStudioVars({ ...defaultStudio, accent });
      const coral = document.documentElement.style.getPropertyValue("--clay-coral");
      /* the verb left its house literal and its white label still holds 3.2:1 */
      expect(coral).not.toBe("");
      expect(coral).not.toBe("230 110 96");
      expect(whiteCR(coral)).toBeGreaterThanOrEqual(3.2);
      /* the midnight ladder: calm clay, never neon brick — white value text ≥4.5:1 */
      expect(whiteCR(document.documentElement.style.getPropertyValue("--card-face"))).toBeGreaterThanOrEqual(4.5);
      expect(whiteCR(document.documentElement.style.getPropertyValue("--data-face"))).toBeGreaterThanOrEqual(4.5);
      expect(whiteCR(document.documentElement.style.getPropertyValue("--overlay-face"))).toBeGreaterThanOrEqual(4.5);
      /* the ladder keeps its order in luminance */
      const lumToken = (k: string) => { const [r = 0, g = 0, b = 0] = document.documentElement.style.getPropertyValue(k).split(" ").map(Number); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
      expect(lumToken("--data-face")).toBeGreaterThan(lumToken("--card-face"));
      expect(lumToken("--card-face")).toBeGreaterThan(lumToken("--overlay-face"));
      /* the noria narrowed to the accent's analogous arc: no tile may sit
         more than 96° away from the bench hue (fuchsia-on-salmon dies) */
      const hub = hslOf(document.documentElement.style.getPropertyValue("--clay-lavender")).h; /* channel 0 rides the hub */
      for (let i = 1; i <= 6; i++) {
        const ph = hslOf(document.documentElement.style.getPropertyValue(`--plate-${i}`)).h;
        const dist = Math.abs(((ph - hub + 540) % 360) - 180);
        expect(dist).toBeLessThanOrEqual(96);
      }
      /* the navy foil joined the family too */
      const foil = document.documentElement.style.getPropertyValue("--clay-foil");
      expect(foil).not.toBe("");
      expect(foil).not.toBe("rgb(46 34 62)");
    }
  });

  it("green accent → green shadows (the mould follows the bench)", () => {
    applyStudioVars({ ...defaultStudio, accent: "#2f9e44" });
    const drop = document.documentElement.style.getPropertyValue("--clay-drop-h");
    const [r = 0, g = 0] = drop.split(" ").map(Number);
    expect(g).toBeGreaterThan(r);
  });

  it("daylight Clay accent tints --clay-raised and separates band-rich-h/band-deep-h from --card-face", () => {
    document.documentElement.dataset.appearance = "clay";
    document.documentElement.dataset.theme = "daylight";
    applyStudioVars({ ...defaultStudio, accent: "#f97316" });
    const raised = document.documentElement.style.getPropertyValue("--clay-raised");
    expect(raised).not.toBe("");
    expect(raised).not.toBe("255 255 255");
    const [rr = 0, , rb = 0] = raised.split(" ").map(Number);
    expect(rr).toBeGreaterThan(rb);
    const cardFace = document.documentElement.style.getPropertyValue("--card-face");
    const bandRich = document.documentElement.style.getPropertyValue("--band-rich-h");
    const bandDeep = document.documentElement.style.getPropertyValue("--band-deep-h");
    expect(bandRich).not.toBe(cardFace);
    expect(bandDeep).not.toBe(cardFace);
    const ink850 = document.documentElement.style.getPropertyValue("--ink-850");
    expect(ink850).not.toBe("255 255 255");
    const [ir = 0, , ib = 0] = ink850.split(" ").map(Number);
    expect(ir).toBeGreaterThan(ib);
    const foil = document.documentElement.style.getPropertyValue("--clay-foil");
    expect(foil).not.toBe("rgb(239 234 252)");
    const lineStrong = document.documentElement.style.getPropertyValue("--line-strong");
    expect(lineStrong).not.toBe("");
    expect(lineStrong).not.toBe("188 174 216");
  });
});

describe("phase 2+3 — studio v2 contract", () => {
  it("setStudio applies on the next frame, not per input event", async () => {
    const raf = window.requestAnimationFrame;
    expect(typeof raf).toBe("function");
    applyStudioVars(defaultStudio);
    expect(document.documentElement.style.getPropertyValue("--tint-mix")).toBe("");
    const { setStudio } = await import("@/lib/useStudio");
    setStudio({ tint: 2 });
    await new Promise((resolve) => raf(() => resolve(null)));
    expect(document.documentElement.style.getPropertyValue("--tint-mix")).toBe("calc(var(--tint-base, 0.1) * 2.00)");
  });

  it("the depth dial writes --clay-depth only when off recipe", () => {
    applyStudioVars(defaultStudio);
    expect(document.documentElement.style.getPropertyValue("--clay-depth")).toBe("");
    applyStudioVars({ ...defaultStudio, clayDepth: 1.3 });
    expect(document.documentElement.style.getPropertyValue("--clay-depth")).toBe("1.30");
  });

  it("a bench link round-trips through encode/decode without loss", async () => {
    const { encodeStudio, decodeStudio } = await import("@/lib/useStudio");
    const bench = { ...defaultStudio, clayPlate: 4, clayCards: 2, clayDepth: 1.2, tint: 1.5, accent: "#166534" };
    const decoded = decodeStudio(encodeStudio(bench));
    expect(decoded).not.toBeNull();
    expect(decoded?.clayPlate).toBe(4);
    expect(decoded?.clayCards).toBe(2);
    expect(decoded?.clayDepth).toBe(1.2);
    expect(decoded?.accent).toBe("#166534");
  });

  it("a hostile share link degrades to null, never to a broken page", async () => {
    const { decodeStudio } = await import("@/lib/useStudio");
    expect(decodeStudio("not-base64-at-all!!!")).toBeNull();
    expect(decodeStudio("")).toBeNull();
  });
});
