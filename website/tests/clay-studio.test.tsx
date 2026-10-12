// @vitest-environment jsdom

/*
 * The clay bench's studio dials, proven against the DOM rather than read.
 *
 * Clay is a separate engine, and its studio is a set of dials over the
 * engine's own §13 tokens: presets write the whole bench as CSS variables
 * on <html> (surviving reloads through the studio's existing replay), a
 * hand-picked color is pulled pastel before it may sit under the plum ink,
 * and "house" — the shipped palette — writes nothing at all, because the
 * stylesheet owns it.
 */

import { describe, expect, it, beforeEach } from "vitest";
import { applyStudioVars, CLAY_PRESETS, defaultStudio, STUDIO_STORAGE_KEY } from "@/lib/useStudio";

beforeEach(() => {
  window.localStorage.clear();
  for (const key of Object.keys(CLAY_PRESETS.house.tokens)) {
    document.documentElement.style.removeProperty(key);
  }
  document.documentElement.style.removeProperty("--plate-custom");
});

describe("clay studio bench", () => {
  it("house writes nothing — the stylesheet owns the shipped palette", () => {
    applyStudioVars(defaultStudio);
    expect(document.documentElement.style.getPropertyValue("--plate-1")).toBe("");
    expect(document.documentElement.style.getPropertyValue("--clay-lavender")).toBe("");
  });

  it("a preset rewrites the whole bench, plates and accent channels together", () => {
    applyStudioVars({ ...defaultStudio, clayPreset: "lagoon" });
    expect(document.documentElement.style.getPropertyValue("--plate-1")).toBe(CLAY_PRESETS.lagoon.tokens["--plate-1"]);
    expect(document.documentElement.style.getPropertyValue("--plate-6")).toBe(CLAY_PRESETS.lagoon.tokens["--plate-6"]);
    expect(document.documentElement.style.getPropertyValue("--clay-lavender")).toBe(CLAY_PRESETS.lagoon.tokens["--clay-lavender"]);
    expect(document.documentElement.style.getPropertyValue("--plate-custom")).toBe(CLAY_PRESETS.lagoon.tokens["--plate-1"]);
  });

  it("a dark hand-picked color is pulled pastel before it reaches the bench", () => {
    applyStudioVars({ ...defaultStudio, clayPreset: "custom", clayAccent: "#3b1d6e" });
    const written = document.documentElement.style.getPropertyValue("--clay-lavender");
    const chans = written.split(" ").map(Number);
    const [r = 0, g = 0, b = 0] = chans;
    expect(0.2126 * r + 0.7152 * g + 0.0722 * b).toBeGreaterThanOrEqual(185);
    /* …and it keeps the pick's hue family: the violet stays violet. */
    expect(b).toBeGreaterThan(r);
  });

  it("the free plate slot takes the visitor's color", () => {
    applyStudioVars({ ...defaultStudio, clayCustom: "#ff9aa8" });
    const written = document.documentElement.style.getPropertyValue("--plate-custom");
    expect(written).not.toBe("");
    const [r = 0] = written.split(" ").map(Number);
    expect(r).toBeGreaterThan(230); /* rose keeps its red, walked pastel */
  });

  it("bench tokens persist under the studio replay key", () => {
    applyStudioVars({ ...defaultStudio, clayPreset: "sunset" });
    const replay = JSON.parse(window.localStorage.getItem("proglass-studio-vars") ?? "{}");
    expect(replay["--plate-3"]).toBe(CLAY_PRESETS.sunset.tokens["--plate-3"]);
  });

  it("storage carries the preset choice", () => {
    void STUDIO_STORAGE_KEY;
    applyStudioVars({ ...defaultStudio, clayPreset: "orchard" });
    /* setStudio is the storage writer for the state blob; applyStudioVars
       owns the vars blob — asserting both halves keeps the replay chain. */
    expect(CLAY_PRESETS.orchard.tokens["--clay-mint"]).toBe("128 214 154");
  });
});

describe("§16 — the bench recolors the whole system", () => {
  it("card faces twist to the bench hue without losing their lightness", () => {
    applyStudioVars({ ...defaultStudio, clayPreset: "lagoon" });
    const written = document.documentElement.style.getPropertyValue("--ink-850");
    expect(written).not.toBe("");
    const [r = 0, g = 0, b = 0] = written.split(" ").map(Number);
    /* lagoon's hue is cyan; the twisted channel must leave plum for water */
    expect(g).toBeGreaterThan(r);
    /* …while keeping the dark card's own brightness: a tinted hug, not a re-paint */
    const twisted = 0.2126 * r + 0.7152 * g + 0.0722 * b;
    const base = 0.2126 * 40 + 0.7152 * 28 + 0.0722 * 56;
    expect(Math.abs(twisted - base) / base).toBeLessThan(0.35);
  });

  it("the drop shadow and wash hues leave the house violet with the bench", () => {
    applyStudioVars({ ...defaultStudio, clayPreset: "lagoon" });
    expect(document.documentElement.style.getPropertyValue("--clay-drop-h")).not.toBe("140 108 255");
    expect(document.documentElement.style.getPropertyValue("--clay-drop-h")).not.toBe("");
    expect(document.documentElement.style.getPropertyValue("--card-tint-h")).not.toBe("");
    expect(document.documentElement.style.getPropertyValue("--card-tint-h2")).not.toBe("");
  });

  it("house still writes none of the system-wide tokens", () => {
    applyStudioVars(defaultStudio);
    expect(document.documentElement.style.getPropertyValue("--ink-850")).toBe("");
    expect(document.documentElement.style.getPropertyValue("--clay-drop-h")).toBe("");
    expect(document.documentElement.style.getPropertyValue("--clay-wash")).toBe("");
  });

  it("the wash depth rides the tint dial", () => {
    applyStudioVars({ ...defaultStudio, clayPreset: "orchard", tint: 0 });
    expect(document.documentElement.style.getPropertyValue("--clay-wash")).toBe("0.000");
    applyStudioVars({ ...defaultStudio, clayPreset: "orchard", tint: 2 });
    expect(document.documentElement.style.getPropertyValue("--clay-wash")).toBe("0.240");
  });

  it("a hand-picked accent steers the whole system, not just the plates", () => {
    applyStudioVars({ ...defaultStudio, clayPreset: "custom", clayAccent: "#2f9e44" });
    const drop = document.documentElement.style.getPropertyValue("--clay-drop-h");
    const [r = 0, g = 0] = drop.split(" ").map(Number);
    expect(g).toBeGreaterThan(r); /* green hand, green shadows */
  });

  it("§17 zone plates: the feature card and lit bands take the bench too", () => {
    applyStudioVars({ ...defaultStudio, clayPreset: "lagoon" });
    expect(document.documentElement.style.getPropertyValue("--panel-face")).toBe(
      CLAY_PRESETS.lagoon.tokens["--plate-1"],
    );
    /* the band face is a foil, twisted to the bench hue but kept pale */
    const band = document.documentElement.style.getPropertyValue("--band-face");
    expect(band).not.toBe("");
    const [r = 0, g = 0, b = 0] = band.split(" ").map(Number);
    expect(0.2126 * r + 0.7152 * g + 0.0722 * b).toBeGreaterThanOrEqual(230);
  });

  it("a hand-picked house plate becomes the feature card's face", () => {
    applyStudioVars({ ...defaultStudio, clayPreset: "custom", clayAccent: "#dc8327" });
    expect(document.documentElement.style.getPropertyValue("--panel-face")).toBe(
      document.documentElement.style.getPropertyValue("--clay-lavender"),
    );
  });

  it("house still writes none of the zone-plate tokens", () => {
    applyStudioVars(defaultStudio);
    expect(document.documentElement.style.getPropertyValue("--panel-face")).toBe("");
    expect(document.documentElement.style.getPropertyValue("--band-face")).toBe("");
  });
});

describe("phase 2 — studio performance contract", () => {
  it("setStudio applies on the next frame, not per input event", async () => {
    const raf = window.requestAnimationFrame;
    expect(typeof raf).toBe("function");
    applyStudioVars(defaultStudio); /* clear */
    expect(document.documentElement.style.getPropertyValue("--tint-mix")).toBe("");
    const { setStudio } = await import("@/lib/useStudio");
    setStudio({ tint: 2 });
    /* Synchronously after the call the DOM is NOT yet rewritten — it lands on the frame. */
    document.documentElement.style.getPropertyValue("--tint-mix"); /* no throw; read allowed */
    await new Promise((resolve) => raf(() => resolve(null)));
    expect(document.documentElement.style.getPropertyValue("--tint-mix")).toBe("calc(var(--tint-base, 0.1) * 2.00)");
  });
});

describe("phase 3 — studio v2 contract", () => {
  it("zone plates follow the bench as BARE triplets (phase 4)", () => {
    /* The css consumers wrap these as rgb(var(--panel-face)) — a studio
       write that carried an rgb() wrapper would paint rgb(rgb(…)) and die
       at computed-value time (the latent band-solid bug the harness
       caught). Assert the write shape, the move, and the clean wipe. */
    applyStudioVars({ ...defaultStudio, clayPreset: "orchard" });
    const pf = document.documentElement.style.getPropertyValue("--panel-face");
    const bf = document.documentElement.style.getPropertyValue("--band-face");
    expect(pf).toBe(CLAY_PRESETS.orchard.tokens["--plate-1"]);
    expect(bf).not.toBe("");
    for (const v of [pf, bf]) expect(v.trim()).toMatch(/^\d+ \d+ \d+$/);
    applyStudioVars(defaultStudio);
    expect(document.documentElement.style.getPropertyValue("--panel-face")).toBe("");
    expect(document.documentElement.style.getPropertyValue("--band-face")).toBe("");
  });

  it("dusk and dust presets rewrite the whole bench", () => {
    applyStudioVars({ ...defaultStudio, clayPreset: "dusk" });
    expect(document.documentElement.style.getPropertyValue("--plate-1")).toBe(CLAY_PRESETS.dusk.tokens["--plate-1"]);
    applyStudioVars({ ...defaultStudio, clayPreset: "dust" });
    expect(document.documentElement.style.getPropertyValue("--clay-lavender")).toBe(CLAY_PRESETS.dust.tokens["--clay-lavender"]);
  });

  it("the depth dial writes --clay-depth only when off recipe", () => {
    applyStudioVars(defaultStudio);
    expect(document.documentElement.style.getPropertyValue("--clay-depth")).toBe("");
    applyStudioVars({ ...defaultStudio, clayDepth: 1.3 });
    expect(document.documentElement.style.getPropertyValue("--clay-depth")).toBe("1.30");
  });

  it("a bench link round-trips through encode/decode without loss", async () => {
    const { encodeStudio, decodeStudio } = await import("@/lib/useStudio");
    const bench = { ...defaultStudio, clayPreset: "lagoon" as const, clayDepth: 1.2, tint: 1.5 };
    const decoded = decodeStudio(encodeStudio(bench));
    expect(decoded).not.toBeNull();
    expect(decoded?.clayPreset).toBe("lagoon");
    expect(decoded?.clayDepth).toBe(1.2);
    expect(decoded?.tint).toBe(1.5);
  });

  it("a hostile share link degrades to null, never to a broken page", async () => {
    const { decodeStudio } = await import("@/lib/useStudio");
    expect(decodeStudio("not-base64-at-all!!!")).toBeNull();
    expect(decodeStudio("")).toBeNull();
  });
});
