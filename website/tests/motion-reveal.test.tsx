// @vitest-environment jsdom

import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { render, screen } from "@testing-library/react";
import { MotionConfig } from "motion/react";
import { MotionReveal } from "@/components/motion/MotionReveal";

/**
 * The hero rule (§N+): above-the-fold content must never be gated on a
 * scroll observer. MotionReveal's `immediate` mode plays on mount; the
 * hero's blocks use it exclusively — these tests pin both halves.
 */
describe("MotionReveal immediate (hero)", () => {
  it("renders children inside .motion-reveal without observer gating", () => {
    render(
      <MotionConfig reducedMotion="never">
        <MotionReveal immediate>
          <p>Hero block</p>
        </MotionReveal>
      </MotionConfig>,
    );
    expect(screen.getByText("Hero block")).toBeTruthy();
    expect(document.querySelector(".motion-reveal")).not.toBeNull();
  });

  it("reduced-motion renders content plainly present", () => {
    render(
      <MotionConfig reducedMotion="always">
        <MotionReveal immediate delay={120}>
          <p>Calm hero block</p>
        </MotionReveal>
      </MotionConfig>,
    );
    expect(screen.getByText("Calm hero block")).toBeTruthy();
  });

  it("the homepage hero uses immediate reveals exclusively", () => {
    const hero = readFileSync(
      resolve(process.cwd(), "src/components/hero/ProductHero.tsx"),
      "utf8",
    );
    const usages = hero.match(/<MotionReveal[^>]*>/g) ?? [];
    expect(usages.length).toBeGreaterThan(0);
    for (const usage of usages) {
      expect(usage, `hero reveal is observer-gated again: ${usage}`).toContain("immediate");
    }
  });
});
