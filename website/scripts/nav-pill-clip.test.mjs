import assert from "node:assert/strict";
import test from "node:test";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

/*
 * Guard for the menu-clip defect shipped with the nav-pill redesign.
 *
 * The desktop bar is a `.nav-pill` card, and the dropdown panels it opens
 * are absolutely-positioned descendants of that pill. The moment the pill
 * carried `overflow: hidden` (it once did, to keep the 46 s photon streak
 * inside the rounded edge), every panel the bar opened was clipped before
 * it could paint: hover opened, clicks opened, nothing appeared, and the
 * defect was misread as "menus need a double click". The clip now lives on
 * the streak itself (clip-path on the ::after); this test fails if anyone
 * re-clips the pill's box or strips the streak's own guards.
 */

const globals = readFileSync(
  fileURLToPath(new URL("../src/app/globals.css", import.meta.url)),
  "utf8",
);

/* Rule blocks whose selector is exactly `.nav-pill` (the box itself, not
   pseudo-elements or family variants). */
const pillBlocks = [...globals.matchAll(/(?:^|\n)\s*\.nav-pill\s*\{([^}]*)\}/g)].map((m) => m[1]);

test("the desktop nav pill never clips its own dropdown panels", () => {
  assert.ok(pillBlocks.length > 0, "expected .nav-pill rule blocks in globals.css");
  for (const body of pillBlocks) {
    assert.equal(
      /overflow(-x|-y)?\s*:\s*(hidden|clip|scroll|auto)/.test(body),
      false,
      `a .nav-pill rule clips overflow — dropdown panels inside the pill would be cut off:\n${body.trim()}`,
    );
  }
});

/* The main streak block is the one that renders (content: ""); the
   reduced-motion override matches the selector too and is not the guard's
   concern. */
const photonBlocks = [...globals.matchAll(/\.nav-pill::after\s*\{([^}]*)\}/g)]
  .map((m) => m[1])
  .filter((body) => /content\s*:/.test(body));

test("the photon streak is pointer-transparent and clips itself", () => {
  assert.equal(photonBlocks.length, 1, "expected exactly one rendering .nav-pill::after block");
  const [body] = photonBlocks;
  assert.match(body, /pointer-events\s*:\s*none/, "the streak must never sit between the hand and the cards");
  assert.match(body, /clip-path\s*:/, "the streak keeps its lane with its own clip-path now that the pill does not clip");
});
