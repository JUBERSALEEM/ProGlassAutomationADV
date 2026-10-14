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

/**
 * §Q · the top-panel rebuild: the hero's background decor can never
 * strand alone in an empty field on small screens, the frame reveal
 * never scales the page's heaviest subtree, the app rail yields the
 * workspace to phones, and the dashboard tiles/wells cannot crush or
 * collapse inside the inline frame.
 */
describe("§Q top panel (hero frame rebuild)", () => {
  const read = (rel: string) => readFileSync(resolve(process.cwd(), rel), "utf8");

  it("hero decor (sphere, rings) is gated to the wide composition", () => {
    const hero = read("src/components/hero/ProductHero.tsx");
    expect(hero).toContain('className="glass-sphere max-lg:hidden"');
    expect(hero.match(/decor-ring max-lg:hidden/g)?.length).toBe(2);
  });

  it("the frame reveal does not scale the heaviest subtree", () => {
    const hero = read("src/components/hero/ProductHero.tsx");
    expect(hero).not.toContain('variant="scale"');
  });

  it("the app rail yields below md so the workspace owns small screens", () => {
    const frame = read("src/components/product/AppFrame.tsx");
    expect(frame).toContain("app-rail hidden w-44");
    expect(frame).toContain("md:flex");
    expect(frame).not.toContain("sm:flex");
  });

  it("dashboard tiles keep two readable columns until the frame is wide", () => {
    const live = read("src/components/product/LiveSurface.tsx");
    expect(live).toContain('<dl className="grid grid-cols-2 gap-2 lg:grid-cols-4">');
  });

  it("no fragile height chain remains in the inline dashboard path", () => {
    const live = read("src/components/product/LiveSurface.tsx");
    expect(live).not.toContain('cn("flex h-full flex-col", className)');
    expect(live).not.toContain("min-h-0 flex-1 pt-3");
    expect(live).toContain("max-h-[22rem] overflow-auto");
  });

  it("MotionReveal carries the near-viewport failsafe", () => {
    const src = read("src/components/motion/MotionReveal.tsx");
    expect(src).toContain("setForced");
    expect(src).toContain("window.innerHeight * 1.5");
    expect(src).toContain("}, 1600);");
  });
});

/**
 * §R · the coarse-pointer stability lane + studio paint stack: the
 * liquid and the card water rest on touch devices (tiles paint once
 * instead of chasing a fling), and the studio's frosted shell never
 * shares a box with its scroller.
 */
describe("§R stability lane (coarse pointers)", () => {
  const read = (rel: string) => readFileSync(resolve(process.cwd(), rel), "utf8");

  it("the engine carries the coarse lane: liquid stills, water thins", () => {
    const engine = read("src/app/crystal-engine.css");
    const lane = engine.slice(engine.indexOf("@media (pointer: coarse)"));
    expect(lane).toContain(".liquid-scene span");
    expect(lane).toContain("animation: none");
    expect(lane).toContain("box-shadow: none");
  });

  it("the studio shell and its scroller are different boxes", () => {
    const studio = read("src/components/layout/ThemeStudio.tsx");
    expect(studio).not.toContain("menu-card animate-scale-in fixed");
    expect(studio).not.toContain("overflow-y-auto rounded-panel");
    expect(studio).toContain("animate-fade-in max-h-[78vh] overflow-y-auto");
  });
});
