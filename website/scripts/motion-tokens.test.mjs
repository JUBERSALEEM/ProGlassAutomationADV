import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import test from "node:test";
import assert from "node:assert/strict";

/*
 * Motion-token contract (Phase 3): the CSS `--dur-*` tokens in globals.css
 * and the JavaScript mirror in src/lib/motion.ts are one scale in two
 * runtimes. If they drift, the CSS Reveal lane and the motion/react lane
 * stop reading as one system — this test fails first.
 */

const css = readFileSync(fileURLToPath(new URL("../src/app/globals.css", import.meta.url)), "utf8");
const js = readFileSync(fileURLToPath(new URL("../src/lib/motion.ts", import.meta.url)), "utf8");

function cssToken(name) {
  const m = css.match(new RegExp(`--dur-${name}:\\s*([0-9.]+)ms`));
  assert.ok(m, `--dur-${name} missing from globals.css`);
  return Number(m[1]) / 1000;
}

function jsToken(name) {
  const m = js.match(new RegExp(`${name}:\\s*([0-9.]+)`));
  assert.ok(m, `${name} missing from lib/motion.ts dur scale`);
  return Number(m[1]);
}

test("duration scale: CSS tokens and JS mirror agree", () => {
  const pairs = [
    ["press", "press"],
    ["hover", "hover"],
    ["card", "card"],
    ["lift", "lift"],
    ["theme", "theme"],
    ["reveal", "reveal"],
    ["reveal-scale", "revealScale"],
    ["settle", "settle"],
  ];
  for (const [cssName, jsName] of pairs) {
    assert.equal(jsToken(jsName), cssToken(cssName), `--dur-${cssName} drifted from motion.ts ${jsName}`);
  }
});

test("easing mirror: the three named curves exist on both sides", () => {
  for (const name of ["--ease-standard", "--ease-out", "--ease-spring"]) {
    assert.ok(css.includes(`${name}:`), `${name} missing from globals.css`);
  }
  for (const name of ["standard", "out", "spring"]) {
    assert.ok(new RegExp(`${name}:\\s*\\[`).test(js), `ease.${name} missing from lib/motion.ts`);
  }
});
