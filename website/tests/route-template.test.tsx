// @vitest-environment jsdom

import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MotionConfig } from "motion/react";
import Template from "@/app/[locale]/template";

/**
 * Phase 5 route-entry shell. jsdom has no WAAPI, so (like the liquid-glass
 * prototype suite) we drive framer through MotionConfig. What these tests
 * pin is the contract that matters:
 *  - motion path: children render inside the .route-shell (the no-JS
 *    noscript force-visible rule targets this class);
 *  - reduced-motion path: children render BARE — no hidden/transformed
 *    wrapper can ever trap content invisible.
 */
describe("[locale] route template (Phase 5 motion)", () => {
  it("motion path wraps children in the .route-shell", () => {
    render(
      <MotionConfig reducedMotion="never">
        <Template>
          <main>Route body</main>
        </Template>
      </MotionConfig>,
    );
    expect(screen.getByText("Route body")).toBeTruthy();
    const shell = document.querySelector(".route-shell");
    expect(shell).not.toBeNull();
    expect(shell?.contains(screen.getByText("Route body"))).toBe(true);
  });

  it("reduced-motion path renders children bare (nothing hidden)", () => {
    render(
      <MotionConfig reducedMotion="always">
        <Template>
          <p>Visible content</p>
        </Template>
      </MotionConfig>,
    );
    expect(screen.getByText("Visible content")).toBeTruthy();
  });
});
