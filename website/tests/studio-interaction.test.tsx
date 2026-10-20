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
  return { useT: () => t, useHref: () => (path: string) => path, useLocale: () => "en-US" };
});

import { ThemeStudio } from "@/components/layout/ThemeStudio";
import { ThemeStudioLazy } from "@/components/layout/ThemeStudioLazy";
import { SupportBubble } from "@/components/support/SupportBubble";
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
    render(<ThemeStudio />);
    const user = userEvent.setup();
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

describe("P21 · the neumorphism bench — depth / distance / softness", () => {
  it("is disabled off-family, goes live on Neumorphism, writes only moved dials, and resets cleanly", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user);

    const depth = () => within(panel).getByRole("slider", { name: /Depth — paired light and shade strength/i }) as HTMLInputElement;
    const distance = () => within(panel).getByRole("slider", { name: /Distance — shadow offset/i }) as HTMLInputElement;
    const softness = () => within(panel).getByRole("slider", { name: /Softness — shadow blur/i }) as HTMLInputElement;

    expect(depth().disabled).toBe(true);
    expect(distance().disabled).toBe(true);
    expect(softness().disabled).toBe(true);

    await user.click(within(panel).getByRole("button", { name: /^neumorphism$/i }));
    await settle();

    expect(html().getAttribute("data-appearance")).toBe("neumorphism");
    expect(window.localStorage.getItem(APPEARANCE_STORAGE_KEY)).toBe("neumorphism");
    expect(depth().disabled).toBe(false);
    expect(distance().disabled).toBe(false);
    expect(softness().disabled).toBe(false);
    expect(inline("--neu-depth")).toBe("");
    expect(inline("--neu-distance")).toBe("");
    expect(inline("--neu-softness")).toBe("");

    fireEvent.change(depth(), { target: { value: "1.45" } });
    fireEvent.change(distance(), { target: { value: "0.8" } });
    fireEvent.change(softness(), { target: { value: "1.9" } });
    await settle();

    expect(inline("--neu-depth")).toBe("1.45");
    expect(inline("--neu-distance")).toBe("0.80");
    expect(inline("--neu-softness")).toBe("1.90");
    const stored = JSON.parse(window.localStorage.getItem(STUDIO_STORAGE_KEY) ?? "{}");
    expect(stored.neuDepth).toBe(1.45);
    expect(stored.neuDistance).toBe(0.8);
    expect(stored.neuSoftness).toBe(1.9);

    await user.click(within(panel).getByRole("button", { name: /reset the neumorphism bench/i }));
    await settle();

    expect(inline("--neu-depth")).toBe("");
    expect(inline("--neu-distance")).toBe("");
    expect(inline("--neu-softness")).toBe("");
  });

  it("adopts a picked accent into the neumorphic material channels", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user);

    await user.click(within(panel).getByRole("button", { name: /^neumorphism$/i }));
    await settle();
    await user.click(within(panel).getByRole("button", { name: /rose colour plate/i }));
    await settle();

    expect(inline("--neu-canvas")).not.toBe("");
    expect(inline("--neu-face")).not.toBe("");
    expect(inline("--neu-light-source")).not.toBe("");
    expect(inline("--neu-dark-source")).not.toBe("");
    expect(inline("--ink-950")).toBe(inline("--neu-canvas"));
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

/* ──────────────────────────────────────────────────────────────────────── *
 * P19 — the senior-audit regressions. Witness: "theme & colour card plates,
 * theme studio not working … frostness also not working properly."
 * Code ledger findings and their locks:
 *   1. --pool-1 was declared 13× (every family block) and rotated 3× by the
 *      studio, but the wallpaper painted ZERO of it: the page's principal
 *      ambient light was dead paint, so a pick visibly failed to move the
 *      wall. Guard: body::after must consume var(--pool-1).
 *   2. The family gates were a visual veil only — pointer-events:none stops
 *      the mouse, never the keyboard. A veiled frost dial still wrote live
 *      tokens nothing consumed: "the dial moves and does nothing." Guard:
 *      gated controls carry a real `disabled`.
 *   3. The launcher's accent dot fell back to a house-violet literal on
 *      every family. Guard: with no pick, the dot IS the family swatch.
 * ──────────────────────────────────────────────────────────────────────── */
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { appearanceFamilies } from "@/data/themes";

describe("P19 · the wallpaper paints what the studio writes", () => {
  it("body::after consumes --pool-1 (the restored crown pool)", () => {
    const css = readFileSync(resolve(process.cwd(), "src/app/globals.css"), "utf8");
    const after = css.slice(css.indexOf("body::after"));
    expect(after).toContain("var(--pool-1, transparent)");
    /* And the repeat list keeps one entry per layer (the P16/P17-count class
       of drift: a layer whose repeat-state silently shifts the grain). */
    const repeatDecl = after.match(/background-repeat:\s*[^;]+;/);
    expect(repeatDecl).not.toBeNull();
    const entries = repeatDecl![0].split(",").length;
    const images = after
      .slice(after.indexOf("background-image:"), after.indexOf("background-repeat"))
      .split(/radial-gradient|linear-gradient|url\(/).length - 1;
    /* Sweep is one token layer; url( + gradients count the paint layers. */
    expect(entries).toBeGreaterThanOrEqual(images);
  });

  it("the launcher's accent dot is the family swatch when nothing is picked", async () => {
    render(<ThemeStudio />);
    const trigger = screen.getByRole("button", { name: /open the theme studio/i });
    const dots = Array.from(trigger.querySelectorAll("span[style]"));
    const claySwatch = appearanceFamilies.find((f) => f.id === "clay")?.swatch ?? "";
    const hexToRgb = (hex: string) => {
      const n = parseInt(hex.slice(1), 16);
      return `rgb(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255})`;
    };
    const accentDot = dots[dots.length - 1] as HTMLElement;
    const bg = accentDot.style.background?.replace(/\s+/g, " ");
    expect(bg).not.toContain("#7E57CE");
    expect(bg).not.toContain("rgb(126, 87, 206)");
    expect(bg).toBe(hexToRgb(claySwatch));
  });
});

describe("P19 · the family gates are real gates, not veils", () => {
  it("glass-bench sliders are disabled off-crystal — keyboard included", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user); // family = clay (beforeEach)
    const frost = within(panel).getByRole("slider", { name: /frost/i });
    const body = within(panel).getByRole("slider", { name: /body/i });
    const glint = within(panel).getByRole("slider", { name: /glint/i });
    for (const dial of [frost, body, glint]) {
      expect((dial as HTMLInputElement).disabled).toBe(true);
    }
    /* After the family plate flips to crystal the same dials go live. */
    await user.click(within(panel).getByRole("button", { name: /^crystal$/i }));
    expect((within(panel).getByRole("slider", { name: /frost/i }) as HTMLInputElement).disabled).toBe(false);
  });

  it("the clay plate rows are disabled off-clay — and live on clay", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user); // clay: live
    let rowSix = within(panel).getAllByRole("button", { name: /^6$/ })[0]!;
    expect((rowSix as HTMLButtonElement).disabled).toBe(false);
    /* Switch to crystal: the bench veils AND disables. */
    await user.click(within(panel).getByRole("button", { name: /^crystal$/i }));
    rowSix = within(panel).getAllByRole("button", { name: /^6$/ })[0]!;
    expect((rowSix as HTMLButtonElement).disabled).toBe(true);
  });
});

describe("P20 · live Chrome, GlassBlue plate, Frost IACVT guard & Chat bubble", () => {
  it("ThemeStudioLazy mounts the real launcher immediately and opens synchronously on click", async () => {
    const user = userEvent.setup();
    render(<ThemeStudioLazy />);
    const trigger = screen.getByRole("button", { name: /open the theme studio/i });
    await user.click(trigger);
    expect(screen.getByRole("dialog", { name: /theme and colour/i })).toBeTruthy();
  });

  it("the Glass Blue colour plate applies #0369A1 instead of resetting to Family", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user);
    const glassBlue = within(panel).getByRole("button", { name: /glass blue colour plate/i });
    await user.click(glassBlue);
    await settle();
    expect(glassBlue.getAttribute("aria-pressed")).toBe("true");
    expect(inline("--brand-400")).not.toBe("");
    const stored = JSON.parse(window.localStorage.getItem(STUDIO_STORAGE_KEY) ?? "{}");
    expect(stored.accentKey).toBe("GlassBlue");
    expect(stored.accent).toBe("#0369A1");
  });

  it("crystal-engine never writes --glass-bright: none (which invalidates backdrop-filter via IACVT)", () => {
    const css = readFileSync(resolve(process.cwd(), "src/app/crystal-engine.css"), "utf8");
    expect(css).not.toMatch(/--glass-bright\s*:\s*none/);
    expect(css).toMatch(/--glass-bright\s*:\s*1/);
  });

  it("SupportBubble does not steal focus on mount, opens on click, and restores focus on close", async () => {
    const user = userEvent.setup();
    render(<SupportBubble />);
    const trigger = screen.getByRole("button", { name: /chat with support/i });
    expect(document.activeElement).not.toBe(trigger);

    await user.click(trigger);
    expect(screen.getByRole("dialog", { name: /support/i })).toBeTruthy();

    await user.keyboard("{Escape}");
    expect(screen.queryByRole("dialog", { name: /support/i })).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it("colour plates apply their exact hue across daylight and midnight without drifting to pink/cyan/purple", () => {
    const parseTripletHue = (triplet: string): number => {
      const nums = triplet.match(/\d+/g)?.map(Number) ?? [0, 0, 0];
      const r = (nums[0] ?? 0) / 255;
      const g = (nums[1] ?? 0) / 255;
      const b = (nums[2] ?? 0) / 255;
      const max = Math.max(r, g, b);
      const min = Math.min(r, g, b);
      const d = max - min;
      if (d === 0) return 0;
      let h = 0;
      if (max === r) h = ((g - b) / d) % 6;
      else if (max === g) h = (b - r) / d + 2;
      else h = (r - g) / d + 4;
      return ((h * 60) + 360) % 360;
    };
    const hueDistance = (a: number, b: number): number => {
      const diff = Math.abs(a - b) % 360;
      return diff > 180 ? 360 - diff : diff;
    };

    html().setAttribute("data-theme", "daylight");
    html().setAttribute("data-appearance", "clay");

    // Pick Gold (#CA8A04, hue ~41°) in daylight
    applyStudioVars({ ...defaultStudio, accent: "#CA8A04", accentKey: "Gold" });
    const goldHue = parseTripletHue("202 138 4");
    expect(inline("--brand-100")).not.toBe("");
    expect(hueDistance(parseTripletHue(inline("--brand-400")), goldHue)).toBeLessThan(8);
    expect(hueDistance(parseTripletHue(inline("--brand-500")), goldHue)).toBeLessThan(8);
    expect(hueDistance(parseTripletHue(inline("--clay-coral")), goldHue)).toBeLessThan(8);
    expect(hueDistance(parseTripletHue(inline("--band-rich-h")), goldHue)).toBeLessThan(8);
    expect(hueDistance(parseTripletHue(inline("--card-face")), goldHue)).toBeLessThan(12);

    // Pick Emerald (#059669, hue ~161°) in daylight on Crystal
    html().setAttribute("data-appearance", "crystal");
    applyStudioVars({ ...defaultStudio, accent: "#059669", accentKey: "Emerald" });
    const emeraldHue = parseTripletHue("5 150 105");
    expect(hueDistance(parseTripletHue(inline("--brand-400")), emeraldHue)).toBeLessThan(8);
    expect(hueDistance(parseTripletHue(inline("--aurora-ball-1")), emeraldHue)).toBeLessThan(15);
    expect(hueDistance(parseTripletHue(inline("--aurora-ball-2")), emeraldHue)).toBeLessThan(20);
    expect(hueDistance(parseTripletHue(inline("--aurora-ball-3")), emeraldHue)).toBeLessThan(20);
  });

  it("colour swatch buttons carry data-color-plate='true' so clay border rules preserve their exact swatch fill", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user);
    const gold = within(panel).getByRole("button", { name: /gold colour plate/i });
    expect(gold.getAttribute("data-color-plate")).toBe("true");
  });
});

describe("P20 · the Clay-paused card edge answers, not freeze", () => {
  it("Border line is not hard-disabled in Clay; touching it explains and hands over to Corner depth", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user); // clay per beforeEach
    const borderLine = within(panel).getByRole("slider", { name: /border line/i }) as HTMLInputElement;
    /* The freeze this locks out: a hard `disabled` + pointer-events veil
       made the control indistinguishable from a hung UI. */
    expect(borderLine.disabled).toBe(false);
    fireEvent.pointerDown(borderLine);
    expect(await within(panel).findByText(/paused in Clay, not broken/i)).toBeTruthy();
    const cornerDepth = within(panel).getByRole("slider", { name: /corner depth/i }) as HTMLInputElement;
    expect(cornerDepth.disabled).toBe(false);
    await settle();
    expect(document.activeElement).toBe(cornerDepth);
    /* And the pause is real: no border token was written under Clay. */
    expect(inline("--card-edge-width")).toBe("");
  });
});

describe("P21 · the Neumorphism-paused card edge answers, not freeze", () => {
  it("Border line explains itself and hands over to Depth", async () => {
    const user = userEvent.setup();
    render(<ThemeStudio />);
    const panel = await openStudio(user);

    await user.click(within(panel).getByRole("button", { name: /^neumorphism$/i }));
    await settle();

    const borderLine = within(panel).getByRole("slider", { name: /border line/i }) as HTMLInputElement;
    expect(borderLine.disabled).toBe(false);

    fireEvent.pointerDown(borderLine);
    expect(await within(panel).findByText(/paused in Neumorphism, not broken/i)).toBeTruthy();

    const depth = within(panel).getByRole("slider", { name: /Depth — paired light and shade strength/i }) as HTMLInputElement;
    expect(depth.disabled).toBe(false);
    await settle();
    expect(document.activeElement).toBe(depth);
    expect(inline("--card-edge-width")).toBe("");
  });
});
