import { describe, expect, it } from "vitest";
import {
  APPEARANCE_STORAGE_KEY,
  DEFAULT_APPEARANCE,
  appearanceFamilies,
  isAppearanceFamily,
  themeModes,
} from "@/data/themes";

describe("eight-family appearance contract", () => {
  it("exposes exactly the eight approved selectable families", () => {
    expect(appearanceFamilies.map((family) => family.id)).toEqual([
      "bento",
      "spatial",
      "swiss",
      "holographic",
      "neumorphism",
      "clay",
      "liquid-glass",
      "glassmorphism",
    ]);
    expect(new Set(appearanceFamilies.map((family) => family.id)).size).toBe(8);
  });

  it("offers both Light and Dark for every family and keeps System as an optional mode", () => {
    expect(themeModes.map((mode) => mode.id)).toEqual(["light", "dark", "system"]);
    expect(appearanceFamilies).toHaveLength(8);
    for (const family of appearanceFamilies) {
      expect(isAppearanceFamily(family.id)).toBe(true);
      expect(family.label.length).toBeGreaterThan(0);
      expect(family.swatch).toMatch(/^#[0-9a-f]{6}$/i);
    }
  });

  it("defaults to Liquid Glass and uses a stable persistence key", () => {
    expect(DEFAULT_APPEARANCE).toBe("liquid-glass");
    expect(APPEARANCE_STORAGE_KEY).toBe("proglass-appearance");
  });

  it("rejects retired Crystal storage while keeping it out of the selectable card list", () => {
    expect(isAppearanceFamily("crystal")).toBe(false);
    expect(appearanceFamilies.some((family) => family.id === "crystal")).toBe(false);
  });
});
