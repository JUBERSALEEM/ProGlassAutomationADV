// @vitest-environment jsdom

/*
 * P18 — THE STUDIO CLICK-THROUGH.
 *
 * The visitor's complaint: "theme & colour card plates, theme studio not
 * working … frostness also not working properly." Phases of engine reads say
 * the wiring should work; "reading the source" is exactly what was called out
 * by the navbar lesson. So this suite drives the REAL panel with REAL pointer
 * events against the REAL stores — no mock between the click and the <html>
 * attributes — and asserts the DOM state the paint actually reads:
 *
 *   1. the panel opens from the launcher and closes on outside-Escape;
 *   2. the mode plates flip data-theme / data-theme-mode immediately;
 *   3. an accent plate writes the brand ramp AND the clay bench tokens
 *      inline on <html> (what every engine surface reads that same frame);
 *   4. a family switch AFTER an accent pick re-bases the ramp — the
 *      historic "plates work intermittently" bug (see useStudio.subscribe)
 *      can never come back silently;
 *   5. the frost dial writes --glass-frost-mult and the reset returns the
 *      recipe to the stylesheet;
 *   6. the clay bench row re-points the panel/card faces on the accent hue,
 *      even with no accent picked (the bench liveness contract).
 */

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";

vi.mock("next/navigation", () => ({
  usePathname: () => "/en",
}));

/* jsdom ships no matchMedia; the engine needs prefers-color-scheme only. */
window.matchMedia = ((query: string) =>
  ({
    matches: false,
    media: query,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    onchange: null,
    dispatchEvent: () => false,
  }) as MediaQueryList) as unknown as typeof window.matchMedia;

/* Words pass through; t.template is the tagged-template form the panel uses. */
vi.mock("@/i18n/provider", () => {
  const t = (copy: string): string => copy;
  t.template = (strings: TemplateStringsArray, ...values: unknown[]) =>
    strings.reduce((acc, s, i) => acc + s + (i < values.length ? String(values[i]) : ""), "");
  return { useT: () => t };
});

import { ThemeStudio } from "@/components/layout/ThemeStudio";
import {
  applyStudioVars,
  decodeStudio,
  defaultStudio,
  normalizeStudio,
  resetStudio,
  setStudio,
  STUDIO_STORAGE_KEY,
} from "@/lib/useStudio";
import { THEME_STORAGE_KEY, APPEARANCE_STORAGE_KEY } from "@/data/themes";

const html = () => document.documentElement;
const inline = (prop: string) => html().style.getPropertyValue(prop);

/* rAF-coalesced apply: let two animation frames + the observer pass over. */
const settle = async () =>
  act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 120));
  });

async function openStudio(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole("button", { name: /open the theme studio/i }));
  return await screen.findByRole("dialog", { name: /theme and colour/i });
}

beforeEach(() => {
  /* The one API the module exposes for a clean bench. */
  try {
    resetStudio();
  } catch {
    /* first run in the process — nothing to reset yet */
  }
  window.localStorage.clear();
  const e = html();
  e.setAttribute("data-theme", "midnight");
  e.setAttribute("data-theme-mode", "dark");
  e.setAttribute("data-appearance", "clay");
  e.removeAttribute("style");
});

afterEach(cleanup);

describe("P18 · the launcher and the drawer", () => {
  it("opens on click and stays open while the click lands on the trigger", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    expect(screen.queryByRole("dialog")).toBeNull();
    const panel = await openStudio(user);
    expect(panel.getAttribute("id")).toBe("theme-studio");
    /* The same pointerdown that opened it must not count as an outside click. */
    await settle();
    expect(screen.getByRole("dialog")).toBeTruthy();
  });

  it("closes on its Close button and on Escape", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    await openStudio(user);
    await user.click(screen.getByRole("button", { name: /^close$/i }));
    expect(screen.queryByRole("dialog")).toBeNull();
    await openStudio(await userEvent.setup());
    await user.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).toBeNull();
  });
});

describe("P18 · the mode plates", () => {
  it("flip data-theme synchronously and persist the choice", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user);
    const dark = within(panel).getByRole("button", { name: /light/i, pressed: false });
    await user.click(dark);
    expect(html().getAttribute("data-theme")).toBe("daylight");
    expect(html().getAttribute("data-theme-mode")).toBe("light");
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe("light");
    /* The plate group re-reads the DOM in the same commit — the pressed
       plate must be the one just clicked. */
    expect(dark.getAttribute("aria-pressed")).toBe("true");
    /* …and the studio re-applies its tokens for the new reading (observer). */
    await settle();
  });

  it("system follows the engine resolve (jsdom = light system)", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user);
    await user.click(within(panel).getByRole("button", { name: /^system$/i }));
    expect(html().getAttribute("data-theme-mode")).toBe("system");
    expect(html().getAttribute("data-theme")).toBe("daylight");
  });
});

describe("P18 · the colour plates", () => {
  it("one tap writes the ramp AND the clay bench inline on <html>", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user);
    await user.click(within(panel).getByRole("button", { name: /rose colour plate/i }));
    await settle();
    expect(inline("--brand-400")).not.toBe("");
    expect(inline("--plate-1")).not.toBe("");
    expect(inline("--card-face")).not.toBe("");
    expect(inline("--band-rich-h")).not.toBe("");
    const stored = JSON.parse(window.localStorage.getItem(STUDIO_STORAGE_KEY) ?? "{}");
    expect(stored.accentKey).toBe("Rose");
    expect(stored.accent).toMatch(/^#e11d48$/i);
  });

  it("the Family plate re-bases everything — the intermittent-plates bug stays dead", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user);
    await user.click(within(panel).getByRole("button", { name: /rose colour plate/i }));
    await settle();
    expect(inline("--brand-400")).not.toBe("");
    /* Family plate = "let the family's own accent run" — inline ramp is
       handed back to the stylesheet on the NEXT apply. */
    await user.click(within(panel).getByRole("button", { name: /^family colour plate$/i }));
    await settle();
    expect(inline("--brand-400")).toBe("");
    expect(inline("--plate-1")).toBe("");
    expect(inline("--card-face")).toBe("");
  });

  it("switching FAMILY after a pick rebuilds the ramp on the new family (observer path)", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user);
    await user.click(within(panel).getByRole("button", { name: /rose colour plate/i }));
    await settle();
    const rose400 = inline("--brand-400");
    expect(rose400).not.toBe("");
    /* The palette-family plates: click Crystal. setFamily clears the pick,
       data-appearance flips, the observer re-applies. */
    await user.click(within(panel).getByRole("button", { name: /^crystal$/i }));
    await settle();
    expect(html().getAttribute("data-appearance")).toBe("crystal");
    expect(window.localStorage.getItem(APPEARANCE_STORAGE_KEY)).toBe("crystal");
    /* Family pick clears the explicit accent → stylesheet owns the ramp. */
    expect(inline("--brand-400")).toBe("");
  });
});

describe("P18 · the crystal bench — frost / body / glint", () => {
  it("the frost dial writes --glass-frost-mult and backtracks to the stylesheet at 1.00", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user);
    const frost = within(panel).getByRole("slider", { name: /frost/i });
    expect(frost.getAttribute("value") ?? (frost as HTMLInputElement).value).toBe("1");
    fireEvent.change(frost, { target: { value: "2.1" } });
    await settle();
    expect(inline("--glass-frost-mult")).toBe("2.10");
    /* And it lands in the bench's storage, so the boot script replays it. */
    expect(JSON.parse(window.localStorage.getItem(STUDIO_STORAGE_KEY) ?? "{}").glassFrost).toBe(2.1);
    /* Back to the shipped recipe: the token is REMOVED, not zeroed —
       the stylesheet's own blur must own the panes again. */
    fireEvent.change(frost, { target: { value: "1" } });
    await settle();
    expect(inline("--glass-frost-mult")).toBe("");
  });

  it("clamps garbage and never writes NaN into the DOM", async () => {
    setStudio({ glassFrost: Number.NaN });
    await settle();
    const v = inline("--glass-frost-mult");
    expect(v === "" || !v.includes("NaN")).toBe(true);
    applyStudioVars({ ...defaultStudio, glassFrost: 9 });
    expect(inline("--glass-frost-mult")).toBe("3.00");
  });
});

describe("P18 · the clay plate bench", () => {
  it("the numbered rows move the faces even with no accent picked", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user);
    /* Row one (clayCards) button 6: cards adopt the sixth bench plate. */
    const rowButton = within(panel).getAllByRole("button", { name: /^6$/ })[0];
    expect(rowButton).toBeTruthy();
    await user.click(rowButton!);
    await settle();
    expect(inline("--plate-6")).not.toBe("");
    /* panel-face follows clayPlate (untouched: row two, default plate 1),
       the card ladder re-derives from the chosen sixth plate instead. */
    expect(inline("--panel-face")).toBe(inline("--plate-1"));
    expect(inline("--card-face")).not.toBe("");
    expect(inline("--card-face")).not.toBe(inline("--data-face"));
    expect(inline("--card-face")).not.toBe(inline("--overlay-face"));
    const stored = JSON.parse(window.localStorage.getItem(STUDIO_STORAGE_KEY) ?? "{}");
    expect(stored.clayCards).toBe(6);
  });
});

describe("P18 · persistence — survive a reload", () => {
  it("resetStudio returns the paint to the stylesheet wholesale", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user);
    await user.click(within(panel).getByRole("button", { name: /rose colour plate/i }));
    await settle();
    await user.click(within(panel).getByRole("button", { name: /reset everything/i }));
    await settle();
    expect(inline("--brand-400")).toBe("");
    expect(inline("--plate-1")).toBe("");
    expect(window.localStorage.getItem(STUDIO_STORAGE_KEY)).toBeNull();
  });
});

/* ── P18 hardening — the spectrum can never let poison paint ──────────── */

describe("P18 · the spectrum (normalizeStudio)", () => {
  it("storage-shape poison degrades field by field, never to a broken bench", () => {
    const healed = normalizeStudio({
      glassFrost: null as unknown as number,          // JSON's NaN→null wound
      glassBody: "2.50" as unknown as number,          // stringified number
      brightness: 42,                                   // out of dial range
      radius: Number.NaN,
      accent: "portobello",                             // not a hex
      accentKey: "",
      clayPlate: 42,
      typeface: "wingdings" as never,
      italic: "yes" as unknown as boolean,
      moneyPositive: "green",
      edgeColor: "hover:text-brand-400",
    });
    expect(healed.glassFrost).toBe(1);
    expect(healed.glassBody).toBe(1);
    expect(healed.brightness).toBe(0.09);               // clamped to the dial
    expect(healed.radius).toBe(1);
    expect(healed.accent).toBeNull();
    expect(healed.accentKey).toBe("Family");
    expect(healed.clayPlate).toBe(1);
    expect(healed.typeface).toBe(defaultStudio.typeface);
    expect(healed.italic).toBe(false);
    expect(healed.moneyPositive).toBe(defaultStudio.moneyPositive);
    expect(healed.edgeColor).toBe("");
  });

  it("a poisoned share link normalises the same way (decodeStudio contract)", () => {
    const poison = btoa(unescape(encodeURIComponent(JSON.stringify({
      glassFrost: null,
      vividness: -999,
      clayCards: 0,
      accent: "#EA580C",
      weight: "heavy",
    }))));
    const healed = decodeStudio(poison)!;
    expect(healed).not.toBeNull();
    expect(healed!.glassFrost).toBe(1);
    expect(healed!.vividness).toBe(-0.16);              // clamped to the dial
    expect(healed!.clayCards).toBe(1);
    expect(healed!.accent).toMatch(/^#ea580c$/i);       // valid hex rides through
    expect(healed!.weight).toBe("regular");
    expect(decodeStudio("!!!not-base64!!!")).toBeNull();
  });

  it("no inline property on <html> ever carries NaN/Infinity, from any path", () => {
    applyStudioVars({
      ...defaultStudio,
      glassFrost: Number.NaN,
      glassBody: Number.POSITIVE_INFINITY,
      clayDepth: Number.NaN,
      tint: Number.NaN,
      brightness: Number.NaN,
      vividness: Number.NaN,
      radius: Number.NaN,
      uiScale: Number.NaN,
      edge: Number.NaN,
      accent: "#EA580C",
    });
    const style = html().getAttribute("style") ?? "";
    expect(/nan|infinity/i.test(style)).toBe(false);
    /* …and the ramps DID paint — poison kills the dial, not the bench. */
    expect(inline("--brand-400").split(" ")).toHaveLength(3);
  });
});

describe("P18 · crystal wall adoption", () => {
  it("a picked accent rotates washes, mid pools, aurora balls and the relight", () => {
    html().setAttribute("data-appearance", "crystal");
    html().setAttribute("data-theme", "midnight");
    applyStudioVars({ ...defaultStudio, accent: "#14B06A" });
    for (const key of [
      "--wall-wash-1", "--wall-wash-2", "--wall-mid-1", "--wall-mid-2",
      "--aurora-ball-1", "--aurora-ball-2", "--aurora-ball-3",
      "--glass-relight-1", "--glass-relight-2",
    ]) {
      expect(inline(key), key).not.toBe("");
    }
    /* Green hub: the dominant walls must answer with green-dominant rgb —
       ball-2 is rot(+60°) which may carry blue, so assert the hub-side
       token instead: wash-1/mid-1 hold green > red and green > untouched. */
    const wash = inline("--wall-wash-1").match(/rgb\((\d+) (\d+) (\d+)/)!;
    expect(Number(wash[2])).toBeGreaterThan(Number(wash[1]));
    /* The clay paper grammar stays clay's: no band-rich token leaks across. */
    expect(inline("--band-rich-h")).toBe("");
    /* Reset: the sheet owns the walls again. */
    applyStudioVars(defaultStudio);
    for (const key of ["--wall-wash-1", "--wall-mid-1", "--aurora-ball-1", "--glass-relight-1"]) {
      expect(inline(key), key).toBe("");
    }
  });

  it("clay family picks do NOT write the crystal wall tokens (family isolation)", () => {
    html().setAttribute("data-appearance", "clay");
    applyStudioVars({ ...defaultStudio, accent: "#14B06A" });
    expect(inline("--plate-1")).not.toBe("");         // clay bench adopts ✓
    expect(inline("--wall-wash-1")).toBe("");         // crystal walls don't ✗
    expect(inline("--glass-relight-1")).toBe("");
  });
});

describe("P18 · benches outside their family explain themselves", () => {
  it("the crystal bench renders inert under clay (and live under crystal)", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user);
    const frost = () => within(panel).getByRole("slider", { name: /frost/i });
    /* Inert copy chip appears when off-family. The gate wrapper is what
       carries pointer-events-none — the slider itself must be inside it. */
    let gate = frost().closest("div[class*='pointer-events-none']");
    expect(gate).not.toBeNull();
    /* Switch the family plate → the same slider unwraps and works. */
    await user.click(within(panel).getByRole("button", { name: /^crystal$/i }));
    gate = frost().closest("div[class*='pointer-events-none']");
    expect(gate).toBeNull();
    fireEvent.change(frost(), { target: { value: "1.8" } });
    await settle();
    expect(inline("--glass-frost-mult")).toBe("1.80");
  });
});
