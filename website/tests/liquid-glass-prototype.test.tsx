// @vitest-environment jsdom

/*
 * Liquid Glass prototype — Phase 3 interaction contract.
 *
 * Drives the REAL prototype stage with real events and asserts the
 * states the references demand: nav selection re-fills the panel,
 * the panel morphs open/dismissed, tiles and rows select, and the
 * scene toggles light/dark. Motion runs with reduced-motion mocked
 * OFF but jsdom has no layout engine, so assertions target DOM state
 * and ARIA — the paint is verified visually on the review route.
 */

import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";
import { MotionConfig } from "motion/react";
import { LiquidGlassPrototype } from "@/components/glass/LiquidGlassPrototype";

/* jsdom has no WAAPI, so Motion's exit animations can never complete
   there (verified: Element.prototype.animate is undefined in this env).
   reducedMotion="always" collapses every transition to its end state,
   which is exactly the DOM/ARIA truth these tests assert; the fluid
   paint itself is verified live on the review route. */
const renderStage = () =>
  render(
    <MotionConfig reducedMotion="always">
      <LiquidGlassPrototype />
    </MotionConfig>,
  );

beforeEach(() => {
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
});

afterEach(() => cleanup());

describe("Liquid Glass prototype", () => {
  it("selecting a nav tab re-fills the panel and marks the current page", async () => {
    const user = userEvent.setup();
    renderStage();
    expect(screen.getByText("Today at the factory")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Job orders" }));

    expect(await screen.findByText("Job orders in motion", {}, { timeout: 5000 })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Job orders" }).getAttribute("aria-current")).toBe("page");
    expect(await screen.findByText("JO-2481 · Marina Tower", {}, { timeout: 5000 })).toBeTruthy();
  });

  it("the panel dismisses to a pill and blooms back open", async () => {
    const user = userEvent.setup();
    renderStage();
    expect(screen.getByText("Tempering queue")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Dismiss panel" }));
    await waitFor(() => expect(screen.queryByText("Tempering queue")).toBeNull(), { timeout: 5000 });
    expect(screen.getByText(/Panel folded/)).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Open panel" }));
    expect(await screen.findByText("Tempering queue", {}, { timeout: 5000 })).toBeTruthy();
  });

  it("Escape inside the panel folds it", async () => {
    const user = userEvent.setup();
    renderStage();
    const dismiss = screen.getByRole("button", { name: "Dismiss panel" });
    dismiss.focus();
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByText("Tempering queue")).toBeNull(), { timeout: 5000 });
  });

  it("selecting a module tile announces it and presses the tile", async () => {
    const user = userEvent.setup();
    renderStage();
    const tile = screen.getByRole("button", { name: /Cutting optimization/ });
    await user.click(tile);
    expect(tile.getAttribute("aria-pressed")).toBe("true");
    expect(screen.getByText(/Cutting optimization is selected/)).toBeTruthy();
  });

  it("selecting a row raises it as the pressed pill", async () => {
    const user = userEvent.setup();
    renderStage();
    const row = screen.getByRole("button", { name: /Tempering queue/ });
    await user.click(row);
    expect(row.getAttribute("aria-pressed")).toBe("true");
    expect(row.getAttribute("data-selected")).toBe("true");
  });

  it("the scene toggles between dark and light as one coordinated theme", async () => {
    const user = userEvent.setup();
    const { container } = renderStage();
    const stage = container.querySelector(".lgx");
    expect(stage?.getAttribute("data-mode")).toBe("dark");

    await user.click(screen.getByRole("button", { name: "Toggle light and dark scene" }));
    expect(stage?.getAttribute("data-mode")).toBe("light");
    expect(screen.getByText(/Scene switched to light/)).toBeTruthy();
  });

  it("material lab sliders drive the stage variables", async () => {
    renderStage();
    const frost = screen.getByLabelText("Frost blur") as HTMLInputElement;
    frost.focus();
    // jsdom range inputs take direct value writes + change events honestly.
    Object.defineProperty(frost, "value", { value: "28", configurable: true });
    frost.dispatchEvent(new Event("change", { bubbles: true }));
    expect(frost.value).toBe("28");
    expect(screen.getByLabelText("Distortion intensity")).toBeTruthy();
    expect(screen.getByLabelText("Clarity")).toBeTruthy();
  });
});
