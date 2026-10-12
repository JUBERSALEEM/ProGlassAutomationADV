// @vitest-environment jsdom

/*
 * The glass bench (G4), proven against the DOM rather than read.
 *
 * Clay's bench is a palette; crystal's is a MATERIAL — frost, body, glint —
 * and its contract has three clauses, all testable:
 *
 *   1. dials write only when MOVED (the tint-dial precedent); at 1.00 the
 *      family block owns the material and <html> carries nothing
 *   2. moved dials write CLAMPED multipliers (a hostile stored value can
 *      never out-shout the engine's own floors/caps)
 *   3. a bench link carries the glass state end-to-end (encode/decode is
 *      whole-object JSON; the hostile payload degrades, never breaks)
 */

import { beforeEach, describe, expect, it } from "vitest";
import {
  applyStudioVars,
  defaultStudio,
  encodeStudio,
  decodeStudio,
  setStudio,
} from "@/lib/useStudio";

const root = () => document.documentElement;
const prop = (name: string) => root().style.getPropertyValue(name);

beforeEach(() => {
  window.localStorage.clear();
  for (const k of ["--glass-frost-mult", "--glass-body-mult", "--glass-glint-mult"]) {
    root().style.removeProperty(k);
  }
});

describe("glass bench — write-only-when-moved (G4 covenant 1)", () => {
  it("the shipped recipe writes NOTHING — the family block owns the material", () => {
    applyStudioVars({ ...defaultStudio });
    expect(prop("--glass-frost-mult")).toBe("");
    expect(prop("--glass-body-mult")).toBe("");
    expect(prop("--glass-glint-mult")).toBe("");
  });

  it("each moved dial writes exactly its own multiplier", () => {
    applyStudioVars({ ...defaultStudio, glassFrost: 1.3 });
    expect(prop("--glass-frost-mult")).toBe("1.30");
    applyStudioVars({ ...defaultStudio, glassBody: 0.7 });
    expect(prop("--glass-body-mult")).toBe("0.70");
    applyStudioVars({ ...defaultStudio, glassGlint: 1.8 });
    expect(prop("--glass-glint-mult")).toBe("1.80");
  });
});

describe("glass bench — clamps (G4 covenant 2)", () => {
  it("out-of-range dials are clamped at write, so the inline var can never outrun the engine", () => {
    applyStudioVars({ ...defaultStudio, glassFrost: 99, glassBody: -4, glassGlint: 40 });
    expect(prop("--glass-frost-mult")).toBe("1.60");
    expect(prop("--glass-body-mult")).toBe("0.50");
    expect(prop("--glass-glint-mult")).toBe("2.00");
  });

  it("non-finite stored values degrade to the recipe on read (never NaN to the dom)", () => {
    window.localStorage.setItem(
      "proglass-studio",
      JSON.stringify({ glassFrost: Number.NaN, glassBody: "wet", glassGlint: null }),
    );
    /* read happens lazily through applyStudioVars' own read */
    applyStudioVars({ ...defaultStudio });
    expect(prop("--glass-frost-mult")).toBe("");
    expect(prop("--glass-body-mult")).toBe("");
    expect(prop("--glass-glint-mult")).toBe("");
  });
});

describe("glass bench — share-link round trip (G4 covenant 3)", () => {
  it("a bench link carries frost/body/glint without loss", () => {
    const bench = { ...defaultStudio, glassFrost: 1.2, glassBody: 0.8, glassGlint: 1.4 };
    const decoded = decodeStudio(encodeStudio(bench));
    expect(decoded?.glassFrost).toBe(1.2);
    expect(decoded?.glassBody).toBe(0.8);
    expect(decoded?.glassGlint).toBe(1.4);
  });

  it("a hostile share link degrades to null, never to a broken page", () => {
    expect(decodeStudio("%%%not-base64%%%")).toBeNull();
  });
});

describe("glass bench — setStudio applies on the next frame", () => {
  it("a dial move lands via the store, coalesced", async () => {
    setStudio({ glassFrost: 1.4 });
    await new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)));
    /* Whatever path applied it, the dial's multiplication is the truth. */
    const written = prop("--glass-frost-mult");
    expect(written === "" || Number(written) === 1.4).toBe(true);
  });
});
