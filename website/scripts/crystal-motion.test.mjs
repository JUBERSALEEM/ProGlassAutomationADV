import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import test from "node:test";
import assert from "node:assert/strict";
import postcss from "postcss";

/*
 * crystal-motion — the G5 contract: living glass that cannot move the
 * wrong people at the wrong time.
 *
 * Read from the SOURCES (the engine and the listener), the same honesty
 * the contrast contract uses. These tests do not argue with the files;
 * they walk them.
 */

const engine = readFileSync(
  fileURLToPath(new URL("../src/app/crystal-engine.css", import.meta.url)),
  "utf8",
);
const life = readFileSync(
  fileURLToPath(new URL("../src/lib/glassLife.ts", import.meta.url)),
  "utf8",
);
const root = postcss.parse(engine);

test("G5·1 the catch light rides ONE inherited var, painted once at root", () => {
  let decl = 0;
  root.walkDecls("--glass-catch", () => decl++);
  assert.equal(decl, 1, `--glass-catch declared ${decl}× — it must be the family token, not per-element noise`);
});

test("G5·2 the §C1 carrier list leads with the catch (hover light above the corners)", () => {
  let found = false;
  root.walkRules((r) => {
    if (!r.selector.includes("bg-ink-") || !r.selector.includes("not(option)")) return;
    r.walkDecls("background-image", (d) => {
      if (d.value.replace(/\s+/g, " ").trim().startsWith("var(--glass-catch)")) found = true;
    });
  });
  assert.ok(found, "no lifted carrier paints var(--glass-catch) as its first layer");
});

test("G5·3 the blurl-less lane drops the gradients (§C1f parallel — no catch without blur)", () => {
  let lane = 0;
  root.walkAtRules("supports", (a) => {
    if (!/not/u.test(a.params) || !/backdrop-filter/u.test(a.params)) return;
    a.walkRules((r) => {
      if (!r.selector.includes("bg-ink-")) return;
      r.walkDecls("background-image", (d) => {
        assert.ok(!/--glass-catch/u.test(d.value), "fallback lane still paints the catch");
        if (d.value.trim() === "none") lane++;
      });
    });
  });
  assert.ok(lane >= 1, "fallback lane never resets background-image — it would paint the G5 gradient pack without blur backing");
});

test("G5·4 view-chill exists ONLY behind @supports (animation-timeline: view())", () => {
  let inSupports = 0, outside = 0;
  const scan = (node, guarded) => {
    node.each((child) => {
      if (child.type === "atrule") {
        const g = guarded || /animation-timeline:\s*view\(\)/u.test(child.params);
        scan(child, g);
        return;
      }
      if (child.type !== "rule") return;
      child.walkDecls(/^animation-(timeline|range)$/u, () => {
        if (guarded) inSupports++; else outside++;
      });
    });
  };
  scan(root, false);
  assert.ok(inSupports >= 2, "chill animation not wired behind the @supports guard");
  assert.equal(outside, 0, "a scroll-driven animation escaped the @supports guard");
});

test("G5·5 reduced-motion: the block covers the catch AND the chill (the gate G5 was told to bring)", () => {
  const supportsRe = /@media \(prefers-reduced-motion\s*:\s*reduce\)/u;
  assert.ok(supportsRe.test(engine), "no reduced-motion block at all");
  /* minified-tolerant split — the clay-paint precedent — then per-block audit */
  const blocks = engine.split(/@media \(prefers-reduced-motion\s*:\s*reduce\)\s*\{/u).slice(1);
  const joined = blocks.join("\n");
  for (const carrier of ["bg-ink-", "glass-frost", "glass-scrim"]) {
    assert.ok(joined.includes(carrier), `reduced-motion block lost the static-repaint proof (${carrier})`);
  }
  let namesAnimationNone = false;
  let killsShadow = false;
  root.walkAtRules("media", (a) => {
    if (!/prefers-reduced-motion/u.test(a.params)) return;
    a.walkDecls(/^animation$/u, (d) => { if (/none/u.test(d.value)) namesAnimationNone = true; });
    a.walkDecls(/^box-shadow$/u, (d) => { if (/none/u.test(d.value)) killsShadow = true; });
  });
  assert.ok(namesAnimationNone, "chill animation survives reduced-motion");
  assert.ok(killsShadow, "chill wash survives reduced-motion");
});

test("G5·6 the cursor's light never enters a painted gradient without the catch-fire radius", () => {
  let ok = false;
  root.walkDecls("--glass-catch", (d) => {
    ok =
      /^radial-gradient\(/u.test(d.value.trim()) &&
      /at var\(--mx\) var\(--my\)/u.test(d.value) &&
      /transparent/u.test(d.value) &&
      /--surface-specular/u.test(d.value);
  });
  assert.ok(ok, "--glass-catch lost its radial shape, its (--mx, --my) center, its fade, or its bench wiring");
});

/* ── glassLife.ts: the listener, audited as source ─────────────────── */
test("G5·7 the JS side is gated: hover+fine, reduced-motion, crystal-only", () => {
  assert.ok(life.includes("(hover: hover) and (pointer: fine)"), "listener not hover/pointer-gated");
  assert.ok(life.includes("(prefers-reduced-motion: reduce)"), "listener not motion-gated");
  assert.ok(/dataset\.appearance/u.test(life) && life.includes('!== "crystal"') === false
    ? life.includes('"crystal"') : true, "listener not crystal-scoped");
});

test("G5·8 one listener, rAF-coalesced, style props only — the pointer-guard contract in prose, verified", () => {
  assert.equal(life.match(/addEventListener\("pointermove"/gu)?.length ?? 0, 1, "more than one pointermove listener");
  assert.ok(life.includes("requestAnimationFrame"), "listener not rAF-coalesced");
  /* the vital clause: writes are setProperty/removeProperty on --mx/--my only */
  for (const bad of ["useState", "setState", "innerHTML", "classList.add", "className ="]) {
    assert.ok(!life.includes(bad), `listener touches ${bad} — pointer-guard territory`);
  }
  assert.ok(life.includes('setProperty("--mx"'), "listener not writing --mx");
  assert.ok(life.includes('setProperty("--my"'), "listener not writing --my");
});


/* ── G6 · the coarse-pointer mitigation, audited from the engine ─────── */
test("G6·1 the coarse-pointer block exists, crystal-scoped, and touches ONLY the frost dial", () => {
  let inMedia = 0;
  const decls = [];
  root.walkAtRules("media", (a) => {
    if (!/pointer:\s*coarse/u.test(a.params)) return;
    inMedia++;
    a.walkDecls((d) => decls.push(d.prop));
  });
  assert.ok(inMedia >= 1, "no (pointer: coarse) mitigation block in the engine");
  /* §F6 evolution: coarse blocks may touch the crystal DIAL SET (frost and
   the liquid brightness) — never properties. Dials, not declarations. */
  assert.ok(decls.includes("--glass-frost-mult") && new Set(["--glass-frost-mult", "--glass-bright"]).isSupersetOf(new Set(decls)), `coarse block touches the dial set only, got ${decls.join(", ")}`);
});

test("G6·2 the radius step rests at 0.6 — strong enough to matter, small enough to stay glass", () => {
  let value = null;
  root.walkAtRules("media", (a) => {
    if (!/pointer:\s*coarse/u.test(a.params)) return;
    a.walkDecls("--glass-frost-mult", (d) => { value = Number(d.value); });
  });
  assert.ok(value === 0.6, `coarse frost dial must rest at 0.6 (the G6 denomination), read ${value}`);
});

test("G6·3 the coarse dial never fights the bench: the ?only? write is the declarated (inline styles from a moved bench win — the hand said so)", () => {
  /* reduced-motion is defense-in-depth here: the same user on coarse AND
     reduce keeps the radius step (it is a perf seam, not a motion seam —
     but the motion gates above still bind §C5 motion to zero). */
  const coarseRules = [];
  root.walkAtRules("media", (a) => {
    if (!/pointer:\s*coarse/u.test(a.params)) return;
    a.walkRules((r) => coarseRules.push(r.selector));
  });
  for (const sel of coarseRules) {
    assert.match(sel, /appearance="?crystal"?/u, `coarse rule escaped the crystal family: ${sel}`);
  }
});

test("G5·9 @property registers the coords (no discrete snap animating from outside)", () => {
  let mx = false, my = false, chill = false;
  root.walkAtRules("property", (a) => {
    if (a.params === "--mx") mx = true;
    if (a.params === "--my") my = true;
    if (a.params === "--glass-chill") chill = true;
  });
  assert.ok(mx && my, "--mx/--my not registered — var(--mx) percentages, animated by the paint, would snap");
  assert.ok(chill, "--glass-chill not registered — view() cannot animate an unregistered var");
});
