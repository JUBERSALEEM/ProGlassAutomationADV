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
 * §R+§S · the touch budget + studio paint stack.
 *
 * §R stilled the liquid on touch and the witness still failed; the §S
 * audit (full read of the paint path) found the real scroll-coupled
 * spend — the sticky bar's backdrop sampling — and corrected two §R
 * positions OPENLY: the transform-only brand disc drifts again (it
 * never repainted; stilling it bought nothing), and the studio's
 * entrance fade rides the SHELL so panel and content paint as one
 * surface from frame 0 (fading the content inside the scroller is
 * what let the shell present empty). The §R separations that were
 * right stand: backdrop-filter never shares a box with the scroller,
 * and the card water stays thinned on touch.
 */
describe("§R+§S touch budget (coarse pointers)", () => {
  const read = (rel: string) => readFileSync(resolve(process.cwd(), rel), "utf8");

  it("the engine carries the touch budget: water thins, one disc drifts", () => {
    const engine = read("src/app/crystal-engine.css");
    const lane = engine.slice(engine.indexOf("@media (pointer: coarse)"));
    expect(lane).toContain(".liquid-scene span");
    expect(lane).toContain("animation: none");
    expect(lane).toContain("box-shadow: none");
    expect(lane).toContain("liquid-drift-a 20s");
  });

  it("the sticky bar is solid film on touch — it never samples the page", () => {
    const engine = read("src/app/crystal-engine.css");
    const lane = engine.slice(engine.indexOf("@media (pointer: coarse)"));
    expect(lane).toContain(".nav-glass");
    /* the §F1 progressive-frost pseudo is the bar's second sampler;
       filming the bar without it leaves the top panel re-blurring
       over the drifting liquid even at rest (the §S first-cut miss) */
    expect(lane).toContain(".nav-glass::before");
    expect(lane).toContain("backdrop-filter: none");
  });

  it("the studio entrance fades the shell; the scroller never animates", () => {
    const studio = read("src/components/layout/ThemeStudio.tsx");
    expect(studio).not.toContain("menu-card animate-scale-in fixed");
    expect(studio).not.toContain("overflow-y-auto rounded-panel");
    expect(studio).not.toContain("animate-fade-in max-h-[78vh]");
    expect(studio).toContain("menu-card animate-fade-in fixed inset-x-3");
    expect(studio).toContain("max-h-[78vh] overflow-y-auto");
  });

  it("hero residency (§W): backdrop + preview cluster promoted, banner gated, text unpromoted", () => {
    /* §S gated every hero hint under [data-live]; the device witness
       (Galaxy/Chrome: hero blinks ONCE on return from a deep scroll)
       proved the first screen needs tile residency, not just honest
       hints. Renegotiated in the open: the artwork layer and the
       preview cluster stay promoted; the banner remains gated, and
       the promotion never touches a text-bearing block (composited
       text loses subpixel antialiasing). */
    const globals = read("src/app/globals.css");
    expect(globals).toContain(".hero-backdrop-parallax { will-change: transform; }");
    expect(globals).toContain(".hero-field .motion-reveal.relative { will-change: transform; }");
    expect(globals).not.toContain(".hero-banner-parallax { will-change: transform, opacity;");
    expect(globals).toContain(".hero-field[data-live] .hero-banner-parallax");
  });

  it("the top boundary is a wall — no overscroll flash or pull-to-refresh", () => {
    const globals = read("src/app/globals.css");
    expect(globals).toContain("overscroll-behavior: none");
  });
});
