import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const layout = readFileSync(new URL("../src/app/[locale]/layout.tsx", import.meta.url), "utf8");
const css = readFileSync(new URL("../src/app/globals.css", import.meta.url), "utf8");
const motion = readFileSync(new URL("../src/lib/motion.ts", import.meta.url), "utf8");
const tailwind = readFileSync(new URL("../tailwind.config.ts", import.meta.url), "utf8");
const layoutPrimitives = readFileSync(new URL("../src/components/ui/Layout.tsx", import.meta.url), "utf8");
const motionStagger = readFileSync(new URL("../src/components/motion/MotionStagger.tsx", import.meta.url), "utf8");
const manifest = readFileSync(new URL("../src/app/manifest.ts", import.meta.url), "utf8");
const sitemap = readFileSync(new URL("../src/app/sitemap.ts", import.meta.url), "utf8");
const robots = readFileSync(new URL("../src/app/robots.ts", import.meta.url), "utf8");

test("the localized shell provides a keyboard bypass to the main landmark", () => {
  assert.match(layout, /href="#main" className="skip-link"/);
  assert.match(layout, /<main id="main" className="site-main[^\"]*" tabIndex=\{-1\}>/);
  assert.match(layout, /<div className="site-stage">/);
  assert.match(layout, /t\("Skip to content"\)/);
  assert.match(css, /\.skip-link:focus-visible\s*{[^}]*transform:\s*translateY\(0\)/s);
});

test("components consume semantic theme colors rather than a second fixed palette", () => {
  assert.match(tailwind, /ink:\s*{/);
  assert.match(tailwind, /text:\s*{/);
  assert.match(tailwind, /state:\s*{/);
  assert.match(tailwind, /rgb\(var\(--ink-950\)/);
  assert.match(tailwind, /rgb\(var\(--text-1\)/);
});

test("the design system exposes shared flow, cluster, and count-aware grid primitives", () => {
  assert.match(layoutPrimitives, /export function Flow/);
  assert.match(layoutPrimitives, /export function Cluster/);
  assert.match(layoutPrimitives, /export function AutoGrid/);
  assert.match(css, /--space-flow-md:\s*clamp\(/);
  assert.match(css, /repeat\(auto-fit, minmax\(min\(100%, var\(--auto-grid-min\)\), 1fr\)\)/);
});

test("the two motion lanes retain centralized timing and capped stagger", () => {
  assert.match(motion, /capMs:\s*200/);
  assert.match(motion, /revealFailSafeMs\s*=\s*1000/);
  assert.match(motion, /presets\s*=\s*{/);
  assert.match(motionStagger, /staggerChildren:\s*staggerStep/);
  assert.match(motionStagger, /viewport=.*once:\s*true/s);
  assert.match(motionStagger, /if \(reduced\)/);
  assert.match(css, /--dur-reveal:\s*380ms/);
  assert.match(css, /--ease-out:\s*cubic-bezier\(0\.16, 1, 0\.3, 1\)/);
});

test("SEO discovery surfaces include manifest, sitemap, robots, and localized metadata", () => {
  assert.match(layout, /manifest:\s*"\/manifest\.webmanifest"/);
  assert.match(manifest, /start_url:\s*"\/"/);
  assert.match(manifest, /display:\s*"standalone"/);
  assert.match(sitemap, /alternates/);
  assert.match(robots, /sitemap/);
});

test("responsive accessibility contracts cover touch, forced colors, and localized overflow", () => {
  assert.match(css, /@media \(pointer: coarse\)[\s\S]*min-block-size:\s*2\.75rem/);
  assert.match(css, /@media \(forced-colors: active\)/);
  assert.match(css, /overflow-wrap:\s*break-word/);
});

test("reduced motion disables smooth scrolling and entrance displacement", () => {
  assert.match(css, /@media\s*\(prefers-reduced-motion:\s*reduce\)/);
  assert.match(css, /scroll-behavior:\s*auto/);
  assert.match(css, /\.reveal[\s\S]*?transform:\s*none\s*!important/);
});
