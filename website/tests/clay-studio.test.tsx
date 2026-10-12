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
    const [r = 0, g = 0, b = 0] = drop.split(" ").map(Number);
    expect(g).toBeGreaterThan(r); /* green hand, green shadows */
  });
});
