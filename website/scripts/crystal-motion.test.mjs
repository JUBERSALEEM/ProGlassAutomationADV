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

/* §O RENEGOTIATED (the witness-driven repair; the §H2-era G5·4/G7·4 pinned
   these rides as virtues). The deep-read verdict: two registered custom
   properties animating on EVERY card/section against scroll timelines
   repainted every visible pane on every scroll frame (main-thread style,
   then paint of the 140px chill shadows and the 4-layer water), and that
   always-on churn — stacked on hundreds of backdrop-filter passes — is
   the lag/flicker class the user reported, repeatedly, across §H2–§N.
   The rides are retired: the DOCTRINE test now pins their ABSENCE so a
   future "living glass" idea must argue its way back in with measured
   frame data, not vibes. */
test("G5·4 (§O) the view-chill ride is RETIRED: no scroll-driven animation anywhere in the engine", () => {
  let animated = 0;
  const scan = (node) => {
    node.each((child) => {
      if (child.type === "atrule") {
        assert.ok(!/animation-timeline:\s*view\(\)/u.test(child.params), "a view() @supports guard reappeared — §O retired scroll-driven property rides");
        scan(child);
        return;
      }
      if (child.type !== "rule") return;
      child.walkDecls(/^animation-(timeline|range)$/u, () => { animated++; });
    });
  };
  scan(root);
  assert.equal(animated, 0, "a scroll-driven animation declaration reappeared in the engine");
  /* the token itself survives, resting at 0, so §C5 vocabulary is stable */
  assert.ok(/@property --glass-chill/u.test(engine), "--glass-chill registration dropped — the resting token must stay readable");
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
test("G6·1 the coarse-pointer blocks exist, crystal-scoped: dials + the §R lane + the §S bar film only", () => {
  let inMedia = 0;
  const decls = [];
  root.walkAtRules("media", (a) => {
    if (!/pointer:\s*coarse/u.test(a.params)) return;
    inMedia++;
    a.walkDecls((d) => decls.push(d.prop));
  });
  assert.ok(inMedia >= 1, "no (pointer: coarse) mitigation block in the engine");
  /* §F6 wrote the dial half (frost, brightness). §R — the checkerboard
     witness — extended the lane in the open: on coarse pointers the
     liquid stills (animation), the card water thins to the corner sheen
     (background) and its blurred inset glows go (box-shadow). §S — the
     full-audit release — extends it once more, again in the open: the
     sticky bar's backdrop sampling was the last scroll-coupled
     painter, so on touch the bar takes solid film (background-color at
     the no-backdrop fallback fill; backdrop-filter: none on both
     vendor lines). The allowance is exactly the dials + those two
     lanes; the lane must never grow into a second design system. */
  const ALLOWED = new Set(["--glass-frost-mult", "--glass-bright", "animation", "background", "box-shadow", "background-color", "backdrop-filter", "-webkit-backdrop-filter"]);
  assert.ok(decls.includes("--glass-frost-mult") && ALLOWED.isSupersetOf(new Set(decls)), `coarse lane touches its allowance only, got ${decls.join(", ")}`);
  assert.ok(decls.includes("animation"), "the coarse lane lost its animation control — §R's stills and §S's one-disc drift both ride it");
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
  assert.ok(chill, "--glass-chill not registered — §O keeps the token registered, resting at 0, so no reader ever sees an unregistered-var fallback snap");
});

/* ── G7 · §H2 living water: the scroll stir, RETIRED by §P ───────────
   The §H2-era G7·1–G7·3 pinned the stir's virtues (one listener, gated,
   self-parking). The §P witness overturned them: cards flashed and
   blanked EXACTLY on scroll, in both modes, Crystal-only, while Clay —
   which has no scroll writer at all — scrolled clean. A per-frame
   inherited-property write on <html> forces style + paint of every
   pane's water layer per scroll frame, starving raster and the reveal
   observer together. The stir module is deleted; these tests now pin
   the deletion so the pattern cannot creep back in as a "small" idea. */
import { existsSync } from "node:fs";
const glassLifeComponent = readFileSync(
  fileURLToPath(new URL("../src/components/glass/GlassLife.tsx", import.meta.url)),
  "utf8",
);
const heroField = readFileSync(
  fileURLToPath(new URL("../src/components/hero/HeroField.tsx", import.meta.url)),
  "utf8",
);

test("G7·1 (§P) the scroll stir is deleted and never mounted", () => {
  assert.equal(
    existsSync(fileURLToPath(new URL("../src/lib/glassScroll.ts", import.meta.url))),
    false,
    "glassScroll.ts is back — §P deleted the scroll stir; scroll writes nothing",
  );
  assert.ok(!glassLifeComponent.includes("initGlassScroll"), "GlassLife mounts the stir again");
  assert.ok(glassLifeComponent.includes("initGlassLife"), "GlassLife lost the catch-light mount");
});

test("G7·2 (§P) no scroll listener writes styles in the living-glass layer or the hero field", () => {
  for (const [name, src] of [["GlassLife", glassLifeComponent], ["HeroField", heroField]]) {
    assert.ok(!src.includes('addEventListener("scroll"'), `${name} listens to scroll again — scroll writes nothing`);
    assert.ok(!src.includes('"--glass-surge"') && !src.includes('"--glass-drift"'), `${name} writes the water channels again`);
  }
  assert.ok(!heroField.includes('"--sy"'), "HeroField writes --sy again — the banner parallax is scroll-coupled again");
});

test("G7·3 (§P) nothing restyles the document on interaction: no root data flips in GlassLife", () => {
  assert.ok(!glassLifeComponent.includes("liquidRest"), "the idle watchdog's root attribute flips are back");
  assert.ok(!glassLifeComponent.includes("dataset."), "GlassLife writes root dataset attributes again");
  /* the catch light's own module still scopes its writes to the hovered card */
  assert.ok(life.includes("closest"), "the catch light lost its per-card scoping");
});

test("G7·4 (§O) the per-card flow ride is RETIRED; the water still reads its resting token", () => {
  /* no card may ride a scroll timeline again (see the G5·4 header) */
  assert.ok(!/animation-timeline:\s*view\(\)/u.test(engine), "a view() timeline reappeared — §O retired scroll-driven property rides");
  /* the §H water layer still consumes --glass-flow at its rest value 0,
     so the retired ride leaves still water, not a broken gradient */
  let readsFlow = false;
  root.walkDecls((d) => { if (/var\(--glass-flow/u.test(d.value)) readsFlow = true; });
  assert.ok(readsFlow, "--glass-flow has no reader left — the water lost its flow channel entirely");
  /* reduced-motion still silences card animations outright */
  let killed = false;
  root.walkAtRules("media", (a) => {
    if (!/prefers-reduced-motion/u.test(a.params)) return;
    a.walkRules((r) => {
      if (!r.selector.includes(".section")) return;
      r.walkDecls("animation", (d) => { if (/none/u.test(d.value)) killed = true; });
    });
  });
  assert.ok(killed, "reduced-motion no longer silences card animations");
});

test("G7·5 the three water channels are registered <number>s (no discrete snaps)", () => {
  const registered = new Set();
  root.walkAtRules("property", (a) => registered.add(a.params.trim()));
  for (const channel of ["--glass-flow", "--glass-surge", "--glass-drift"]) {
    assert.ok(registered.has(channel), `${channel} not registered — the water would snap, not flow`);
  }
});

/* ── §N · the lag witness: austerity, idle-only liquid, reveal failsafe ── */
const reveal = readFileSync(
  fileURLToPath(new URL("../src/lib/useReveal.ts", import.meta.url)),
  "utf8",
);

test("§N·1 small carriers dropped backdrop-filter (superseded and extended by §O below)", () => {
  let austerity = 0;
  root.walkRules((r) => {
    if (!r.selector.includes('bg-ink-') || !r.selector.includes('.card-accent')) return;
    r.walkDecls("backdrop-filter", (d) => { if (d.value.trim() === "none") austerity++; });
  });
  assert.ok(austerity >= 2, "the utility-carrier austerity lanes are gone — the backdrop census is back");
});

test("§N·2 (§P renegotiated) the idle watchdog is retired; the drift is compositor-only", () => {
  /* the watchdog existed to protect backdrop-filter panes from a moving
     backdrop; §O removed the panes' backdrop sampling, and §P removed
     the watchdog's document-wide restyle flips with it */
  assert.ok(!glassLifeComponent.includes("liquidRest"), "the watchdog is back");
  /* the drift survives as pure transform keyframes on fixed spans */
  assert.ok(/@keyframes liquid-drift-a \{[\s\S]*?transform: translate3d/u.test(engine), "the liquid drift lost its transform-only keyframes");
});

test("§N·3 reveal can never strand a near-viewport card at opacity 0", () => {
  assert.ok(reveal.includes("failsafe"), "the reveal failsafe is gone");
  assert.ok(reveal.includes("window.innerHeight * 1.5"), "the failsafe lost its near-viewport bound");
});

/* ── §O · the clear-pane doctrine (the deep-read verdict, audited) ─────
   Content panes do NOT sample the backdrop at all: translucent fill +
   scrim + corner light renders the moving liquid through them, sharper
   than any blur, and a pane that never depends on a backdrop tile can
   never flash blank when the compositor thrashes. Backdrop blur survives
   only on bounded chrome (nav, menus, sheets, the studio) whose count
   does not scale with page content. These locks keep the census dead. */
test("§O·1 panes never sample the backdrop: every blur() in the engine sits on bounded chrome", () => {
  const CHROME = /(nav-pill|nav-sheet|menu-card|studio-sheet|nav-glass|glass-bar|eyebrow-chip|thead|input|textarea|select)/u;
  const offenders = [];
  root.walkRules((r) => {
    r.walkDecls("backdrop-filter", (d) => {
      if (!/blur\(/u.test(d.value)) return;
      if (!CHROME.test(r.selector)) offenders.push(`${r.selector} → ${d.value}`);
    });
  });
  assert.deepEqual(offenders, [], "a content pane regained backdrop blur — the flashing-card class is back");
});

test("§O·2 the chrome keeps its frost (blur survives where the census cannot scale)", () => {
  let chrome = 0;
  root.walkRules((r) => {
    if (!r.selector.includes(".nav-pill") || !r.selector.includes(".menu-card")) return;
    r.walkDecls("backdrop-filter", (d) => { if (/blur\(calc\(14px/u.test(d.value)) chrome++; });
  });
  assert.ok(chrome >= 1, "the chrome frost rule is gone — menus/nav lost the true material");
});

test("§O·3 chips are tint-only glass (no backdrop sampling on chip rows)", () => {
  let chipBlur = 0;
  root.walkRules((r) => {
    if (!/(^|,)\s*\[data-appearance="crystal"\][^{]*\.chip\b/u.test(r.selector)) return;
    r.walkDecls("backdrop-filter", (d) => { if (/blur\(/u.test(d.value)) chipBlur++; });
  });
  assert.equal(chipBlur, 0, "a chip regained backdrop blur");
});

/* ── §S · the touch budget (the full-audit release) ───────────────────
   §R stilled the liquid on coarse pointers and the witness STILL
   failed; the audit (docs/LIQUID_GLASS_PROTOTYPE.md §S) read the whole
   paint path and found the remaining scroll-coupled spend: the sticky
   bar sampling the page through backdrop-filter on every scroll frame
   (24px × the Frost dial, Crystal-only). These locks pin the budget:
   the bar is solid film on touch, ONE transform-only disc keeps
   drifting, the thinned water stays. The §O·1 census above stays
   green: the film rules carry `backdrop-filter: none`, never a blur. */
test("§S·1 on touch the sticky bar stops sampling the page (solid film)", () => {
  let bar = 0, pill = 0;
  root.walkAtRules("media", (a) => {
    if (!/pointer: coarse/u.test(a.params)) return;
    a.walkRules((r) => {
      r.walkDecls("backdrop-filter", (d) => {
        if (d.value.trim() !== "none") return;
        if (r.selector.includes(".nav-glass")) bar++;
        if (r.selector.includes(".nav-pill")) pill++;
      });
    });
  });
  assert.ok(bar >= 1, "the coarse lane lost the .nav-glass film — the bar samples the page at scroll again");
  assert.ok(pill >= 1, "the coarse lane lost the .nav-pill film");
});

test("§S·2 one liquid disc still drifts on touch — transform-only", () => {
  const lane = engine.slice(engine.indexOf("@media (pointer: coarse)"));
  assert.ok(
    /span:nth-child\(1\)\s*\{\s*animation: liquid-drift-a 20s/u.test(lane),
    "the brand disc no longer drifts on touch — the liquid is a photograph again",
  );
  assert.ok(
    /@keyframes liquid-drift-a \{[\s\S]*?transform: translate3d/u.test(engine),
    "drift-a is no longer transform-only — the disc would repaint, not composite",
  );
});
