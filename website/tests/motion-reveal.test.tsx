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
    expect(live).toContain('<dl className="kpi-row grid grid-cols-2 gap-2 lg:grid-cols-4">');
    /* The frame is embedded at a width no viewport query can see, so the tier
       is read from the workspace container instead: two columns until the
       window itself is 42rem wide, then four. */
    const grid = read("src/app/responsive-card-grid.css");
    expect(grid).toContain(".preview-workspace {");
    expect(grid).toContain("container-type: inline-size;");
    expect(grid).toContain("@container (min-width: 42rem)");
  });

  it("no fragile height chain remains in the inline dashboard path", () => {
    const live = read("src/components/product/LiveSurface.tsx");
    expect(live).not.toContain('cn("flex h-full flex-col", className)');
    expect(live).not.toContain("min-h-0 flex-1 pt-3");
    expect(live).toContain("max-h-[22rem] overflow-auto");
  });

  it("MotionReveal carries the near-viewport failsafe", () => {
    /* §AA: the ceiling moved from a hard-coded 1600ms to the shared
       `revealFailSafeMs` token (1000ms) — content may not sit
       invisible that long on a busy thread. */
    const src = read("src/components/motion/MotionReveal.tsx");
    expect(src).toContain("setForced");
    expect(src).toContain("window.innerHeight * 1.5");
    expect(src).toContain("revealFailSafeMs");
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

  it("touch entrances (§AB): coarse reveals are fades in both lanes, no rise, no scale", () => {
    /* The Crystal engine audit's A4: on a phone fling, a 12px rise on
       every entering card is a second motion layered over the scroll
       and reads as bounce. The CSS lane drops the geometry under
       (pointer: coarse); the Motion lane swaps to an opacity-only
       hidden variant on the same signal. Fine pointers keep the
       rise — this pins only the coarse behaviour. */
    const globals = read("src/app/globals.css");
    const coarseAt = globals.indexOf("@media (pointer: coarse)", globals.indexOf(".reveal-scale.is-visible"));
    expect(coarseAt).toBeGreaterThan(-1);
    expect(globals.slice(coarseAt, coarseAt + 200)).toContain("transform: none");
    const motionReveal = read("src/components/motion/MotionReveal.tsx");
    expect(motionReveal).toContain('window.matchMedia("(pointer: coarse)")');
    expect(motionReveal).toContain("hidden: { opacity: 0 }");
  });

  it("motion system (§AA): stagger capped in both lanes, failsafe prompt, one trigger geometry", () => {
    /* The motion-performance pass. Uncapped `index * 70` call sites
       left a big grid's last card waiting ~800ms after entering view
       ("cards appear late"); the cap now lives INSIDE both reveal
       lanes so no call site can regress it. The lanes also share
       their trigger geometry (12% / −4% foot) and the tightened
       failsafe ceiling, and the page transition programs against
       the token presets rather than a hand-rolled easing that
       matched nothing in the scale. */
    const tokens = read("src/lib/motion.ts");
    expect(tokens).toContain("capMs: 200");
    expect(tokens).toContain("revealFailSafeMs = 1000");
    expect(tokens).toContain("export const presets");
    const reveal = read("src/lib/useReveal.ts");
    expect(reveal).toContain("Math.min(options.delay, stagger.capMs)");
    expect(reveal).toContain('"0px 0px -4% 0px"');
    expect(reveal).toContain("revealFailSafeMs");
    expect(reveal).not.toContain("}, 1600)");
    const motionReveal = read("src/components/motion/MotionReveal.tsx");
    expect(motionReveal).toContain("Math.min(delay, stagger.capMs)");
    expect(motionReveal).toContain('margin: "0px 0px -4% 0px"');
    expect(motionReveal).not.toContain("}, 1600)");
    const template = read("src/app/[locale]/template.tsx");
    expect(template).toContain('className="route-shell"');
    expect(template).not.toContain('initial={{ opacity: 0');
    const globalCss = read("src/app/globals.css");
    expect(globalCss).toContain(".route-shell");
    expect(globalCss).toContain("opacity var(--dur-lift) var(--ease-standard)");
    expect(globalCss).toContain("@starting-style");
  });

  it("scroll rest (§Z): the liquid parks while scrolling; engine samplers filmed on touch", () => {
    /* Midnight panes sit at a 0.05 lift, so the drifting disc shows
       through every card — while the page scrolls, the colour swims
       behind the glass the whole way down (the splash witness). §Z
       parks the drift during scroll (resume 900ms after settle) via
       ONE attribute on the .liquid-scene element — never <html>,
       never per frame (§P's ban stands) — and films the three
       backdrop samplers the §S census missed because they live in
       the engine: table heads, form wells, the studio sheet. */
    const engine = read("src/app/crystal-engine.css");
    expect(engine).toContain('.liquid-scene[data-liquid-rest="1"] span');
    expect(engine).toContain("animation-play-state: paused");
    expect(engine).not.toContain("html[data-liquid-rest");
    const lane = engine.slice(engine.lastIndexOf("@media (pointer: coarse)"));
    expect(lane).toContain("table thead tr");
    expect(lane).toContain(":is(input, textarea, select)");
    expect(lane).toContain(".studio-sheet");
    const life = read("src/components/glass/GlassLife.tsx");
    expect(life).toContain('setAttribute("data-liquid-rest", "1")');
    expect(life).toContain("removeAttribute(\"data-liquid-rest\")");
    expect(life).toContain("{ passive: true }");
    expect(life).toContain("900");
  });

  it("small accent text (§Y): no sub-floor opacity modifiers on tiny labels", () => {
    /* Measured over the liquid's disc cores, brand-400 at /60–/70 and
       the state hues at /80 land at 2–3.5:1 — the murk the witness
       reported. The pattern now: tiny accent labels take solid
       brand-300 (or the solid state hue); translucency is for fills,
       never for words. */
    const files = [
      "src/components/deliveries/DeliveriesSummaryCards.tsx",
      "src/components/deliveries/DeliveriesBreakdowns.tsx",
      "src/components/job-orders/JobOrdersSummaryCards.tsx",
      "src/components/records/RecordsSummaryCards.tsx",
      "src/components/records/RecordsBreakdowns.tsx",
      "src/app/[locale]/how-it-works/page.tsx",
      "src/components/home/StatusFlows.tsx",
    ];
    for (const f of files) {
      const src = read(f);
      expect(src, f).not.toContain("text-brand-400/60");
      expect(src, f).not.toContain("text-brand-400/70");
      expect(src, f).not.toContain("text-state-warning/80");
      expect(src, f).not.toContain("text-state-success/80");
    }
    expect(read("src/components/deliveries/DeliveriesSummaryCards.tsx")).toContain(
      "text-brand-300",
    );
  });

  it("hero paint (§X): promotions gated, backdrop bounded, chart capped, menus filmed", () => {
    /* The device screenshots (an empty sheet of backdrop artwork
       showing through the unpainted preview frame, tile text sliced
       at a straight raster edge) convicted paint weight in the one
       region that stacks hero-unique costs: two backdrop layers
       spanning the hero's full ~2,600px height (one a full-bleed
       image) directly under the translucent frame, the drifting
       liquid beneath it, §W's promoted cluster layer on top, and a
       flex-1 chart panel ballooning into an empty glass box. §X
       reverts the promotions, bounds the layers to the first
       screens, caps the chart, and films the last chrome sampler.
       (§R's thinning keeps its `.section` scoping — the audit
       showed §H's heavy water only ever lived there.) */
    const globals = read("src/app/globals.css");
    expect(globals).not.toContain(".hero-backdrop-parallax { will-change: transform; }");
    expect(globals).not.toContain("motion-reveal.relative");
    expect(globals).not.toContain(".hero-banner-parallax { will-change: transform, opacity;");
    expect(globals).toContain(".hero-field[data-live] .hero-banner-parallax");
    const hero = read("src/components/hero/ProductHero.tsx");
    expect(hero).not.toContain("hero-backdrop-parallax pointer-events-none absolute inset-0");
    expect(hero).toContain("max-h-[180svh]");
    const live = read("src/components/product/LiveSurface.tsx");
    expect(live).not.toContain("flex flex-1 items-end");
    expect(live).toContain("h-44 items-end");
    const engine = read("src/app/crystal-engine.css");
    const lane = engine.slice(engine.lastIndexOf("@media (pointer: coarse)"));
    expect(lane).toContain(".menu-card");
    expect(lane).toContain("box-shadow: none");
  });

  it("the top boundary is a wall — no overscroll flash or pull-to-refresh", () => {
    const globals = read("src/app/globals.css");
    expect(globals).toContain("overscroll-behavior: none");
  });
});
