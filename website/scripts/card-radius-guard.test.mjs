import assert from "node:assert/strict";
import test from "node:test";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

/*
 * Guard for the "rounded card renders as a rectangle" defect.
 *
 * Two mechanisms shipped it, and both were silent:
 *
 *   1. `.studio-sheet` declared `border-radius: calc(var(--r-card) * …)`
 *      with `--r-card` defined nowhere. A custom property with no fallback
 *      makes the whole declaration invalid at computed-value time, so the
 *      radius fell back to 0 — and because the class lives in the utilities
 *      layer after the generated utilities, no `rounded-*` class written in
 *      markup could rescue it. Every studio card (SheetCard, PlateSchedule,
 *      PlateInspector, the LiveSurface sheets…) drew square corners while
 *      carrying what looked like a radius rule.
 *   2. `AppFrame` and the image form of `ScreenshotFrame` are rounded
 *      `.surface-raised` figures whose first/last children are flush,
 *      square-cornered fills (window chrome bar, screenshot well, caption).
 *      Without `overflow: hidden` on the figure those fills paint straight
 *      over the corners, so the card reads rectangular however large the
 *      radius is.
 *
 * This test fails if either mechanism comes back: an undefined variable in
 * any `border-radius` declaration, a flush-filled frame without its clip,
 * or a `rounded-<key>` utility whose key Tailwind is never told about
 * (`rounded-input` shipped exactly that way).
 */

const root = fileURLToPath(new URL("..", import.meta.url));
const cssFiles = ["src/app/globals.css", "src/app/clay-engine.css", "src/app/crystal-engine.css"];
const css = Object.fromEntries(cssFiles.map((f) => [f, readFileSync(join(root, f), "utf8")]));
const allCss = Object.values(css).join("\n");

/* Custom properties defined anywhere in the site's CSS (`--x:`). */
const defined = new Set([...allCss.matchAll(/(--[a-zA-Z0-9_-]+)\s*:/g)].map((m) => m[1]));

function* walk(dir) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) yield* walk(path);
    else if (path.endsWith(".tsx") || path.endsWith(".ts")) yield path;
  }
}
const tsxFiles = [...walk(join(root, "src"))];
const tsx = tsxFiles.map((f) => [f, readFileSync(f, "utf8")]);

/* Properties set from markup (`"--x": …` style objects) also count. */
for (const [, text] of tsx) {
  for (const m of text.matchAll(/["'](--[a-zA-Z0-9_-]+)["']\s*:/g)) defined.add(m[1]);
}

test("no border-radius declaration reads an undefined custom property without a fallback", () => {
  for (const [file, text] of Object.entries(css)) {
    for (const m of text.matchAll(/border-radius\s*:\s*([^;]+);/g)) {
      for (const v of m[1].matchAll(/var\((--[a-zA-Z0-9_-]+)(\s*,)?/g)) {
        assert.ok(
          defined.has(v[1]) || v[2],
          `${file}: border-radius reads ${v[1]}, which is defined nowhere and has no fallback — ` +
            `the declaration computes to 0 and the card renders rectangular`,
        );
      }
    }
  }
});

test("the studio sheet's radius resolves to the defined, studio-scaled card token", () => {
  for (const file of ["src/app/globals.css", "src/app/clay-engine.css"]) {
    const blocks = [...css[file].matchAll(/\.studio-sheet\s*\{([^}]*)\}/g)].map((m) => m[1]);
    assert.ok(blocks.length > 0, `expected a .studio-sheet block in ${file}`);
    for (const body of blocks) {
      assert.match(body, /border-radius\s*:\s*var\(--card-radius\)/, `.studio-sheet in ${file} no longer uses var(--card-radius)`);
    }
  }
});

test("flush-filled rounded frames clip their children to the corner", () => {
  const cases = [
    ["src/components/product/AppFrame.tsx", /<figure className=\{cn\("surface-raised overflow-hidden"/],
    ["src/components/gallery/ScreenshotFrame.tsx", /"surface-raised overflow-hidden"/],
  ];
  for (const [file, pattern] of cases) {
    const text = readFileSync(join(root, file), "utf8");
    assert.match(text, pattern, `${file}: the rounded frame lost its overflow-hidden — flush chrome/caption fills will paint square corners over the radius`);
  }
});

/* ── Phase 2: the dynamic, two-factor corner scale ──────────────────────
 * "Crystal cards do not accept the corner radius" had a subtler mechanism
 * than the rectangle bug above, and these tests hold its fix:
 *
 *   · The family factor and the visitor's dial used to write the SAME
 *     `--radius-scale`: the dial's inline write outranked and erased
 *     Crystal's ×1.25 the moment it moved, and raw `rounded-*` utility
 *     cards never read the variable at all. The effective scale is now
 *     always `--radius-family × --radius-user`, computed once in
 *     globals.css; only those two inputs may ever be written. */

test("the effective radius scale is always family × user, and only the two factors are writable", () => {
  const globals = css["src/app/globals.css"];
  assert.match(globals, /--radius-scale:\s*calc\(var\(--radius-family\) \* var\(--radius-user\)\)/, "globals.css no longer computes --radius-scale as family × user");
  assert.match(globals, /--radius-family:\s*1;/, "globals.css lost the base family factor");
  assert.match(globals, /--radius-user:\s*1;/, "globals.css lost the rest position of the user's factor");

  /* No other stylesheet may seize the effective scale. (Comments stripped:
     the engines explain the rule in prose that names the variable.) */
  for (const [file, text] of Object.entries(css)) {
    if (file === "src/app/globals.css") continue;
    const declarations = text.replace(/\/\*[\s\S]*?\*\//g, "");
    assert.doesNotMatch(declarations, /--radius-scale\s*:/, `${file} writes --radius-scale directly — that erases whichever of the two factors it disagrees with`);
  }

  /* …and the studio may only ever write the user's factor. */
  const studio = readFileSync(join(root, "src/lib/useStudio.ts"), "utf8");
  assert.match(studio, /vars\["--radius-user"\]/, "useStudio.ts no longer writes the user's radius factor");
  assert.doesNotMatch(studio, /vars\["--radius-scale"\]/, "useStudio.ts writes --radius-scale again — the inline write erases the family's roundness");
});

test("Crystal and Clay declare their family roundness as factors", () => {
  const crystal = css["src/app/crystal-engine.css"];
  const clay = css["src/app/clay-engine.css"];
  assert.match(crystal, /--radius-family:\s*1\.5/, "Crystal lost its enhanced ×1.5 family roundness");
  assert.match(clay, /--radius-family:\s*2/, "Clay lost its ×2 family roundness");
});

test("the stock utility radii are re-based on the dynamic scale for every theme", () => {
  const globals = css["src/app/globals.css"];
  for (const klass of ["rounded-md", "rounded-xl", "rounded-2xl"]) {
    assert.match(
      globals,
      new RegExp(`\\.${klass}[\\s,{][^}]*--corner-radius\\s*:\\s*calc\\([^)]*var\\(--radius-scale\\)[^}]*border-radius\\s*:\\s*var\\(--corner-radius\\)`),
      `globals.css no longer scales .${klass} by --radius-scale — utility cards in Crystal/Studio are fixed-cornered again`,
    );
  }
});

test("Crystal's wallet takes the dynamic card token, not a fixed corner", () => {
  assert.match(
    css["src/app/crystal-engine.css"],
    /\.glass-rows\s*\{[^}]*border-radius\s*:\s*var\(--card-radius\)/,
    ".glass-rows in Crystal no longer uses var(--card-radius)",
  );
});

/* ── Phase 3: corner-safe content insets ────────────────────────────────
 * A dynamic radius without a matching inset puts the first line of text
 * in the arc's path: at the dial's far end the corner swept through
 * left-aligned card text and it visibly spilled out. The fix is a floor —
 * inline padding ≥ 58% of the radius the card actually drew (published as
 * `--corner-radius` by the rule that drew it), block ≥ 38%, capped — and
 * these tests hold its pieces in place. */

test("every card surface publishes the radius it drew", () => {
  const globals = css["src/app/globals.css"];
  assert.match(globals, /\.rounded-2xl\s*\{\s*--corner-radius:/, ".rounded-2xl no longer publishes --corner-radius");
  assert.match(globals, /\.rounded-card\s*\{\s*--corner-radius:/, ".rounded-card no longer publishes --corner-radius");
  assert.match(globals, /--corner-radius:\s*var\(--card-radius\)/, "named card surfaces no longer publish --corner-radius");
  assert.match(globals, /--corner-radius:\s*var\(--card-radius-large\)/, "raised surfaces no longer publish --corner-radius-large");
});

test("card padding floors against the published radius, capped", () => {
  const globals = css["src/app/globals.css"];
  assert.match(globals, /--corner-safe-cap-inline:\s*2\.25rem/, "the inline corner-safe cap is gone");
  assert.match(
    globals,
    /padding-inline:\s*max\(1rem, min\(calc\(var\(--corner-radius, var\(--card-radius\)\) \* 0\.58\)/,
    "the p-4 inline corner-safe floor is gone — card text can spill out of a max-radius corner again",
  );
  assert.match(
    globals,
    /padding-block:\s*max\(1\.25rem, min\(calc\(var\(--corner-radius, var\(--card-radius\)\) \* 0\.38\)/,
    "the p-5 block corner-safe floor is gone",
  );
  assert.match(globals, /--card-pad:\s*calc\(var\(--card-pad-scale, 1\) \* max\(/, "--card-pad no longer carries its radius floor");
  assert.match(
    globals,
    /--card-pad-sm:\s*calc\(var\(--card-pad-scale, 1\) \* max\([^;]*calc\(var\(--card-radius\) \* 0\.44\)/,
    "--card-pad-sm lost its radius floor — compact cards can put text in the dynamic corner arc",
  );
  assert.match(globals, /\.card-pad-sm\s*\{\s*padding:\s*var\(--card-pad-sm\);/, ".card-pad-sm no longer applies its radius-safe token");
});

test("responsive paddings are floored at their own breakpoint", () => {
  const globals = css["src/app/globals.css"];
  assert.match(globals, /@media \(min-width: 40rem\) \{/, "the sm breakpoint floor block is gone");
  assert.match(globals, /:is\(\.sm\\:p-10\) \{ padding-inline: max\(2\.5rem/, "the sm:p-10 corner-safe floor is gone");
  assert.match(globals, /@media \(min-width: 64rem\) \{/, "the lg breakpoint floor block is gone");
  assert.match(globals, /:is\(\.lg\\:px-8\) \{ padding-inline: max\(2rem/, "the lg:px-8 corner-safe floor is gone");
});

test("flush bands and flush tables keep their text inside the corner", () => {
  const globals = css["src/app/globals.css"];
  assert.match(globals, /:is\(\.overflow-hidden\) > :is\(\.p-3, \.px-3\)/, "clipped frames lost their flush-band corner floor");
  assert.match(globals, /:is\(th, td\):first-child/, "flush tables lost their edge-cell corner floor");
  assert.match(css["src/app/crystal-engine.css"], /\.glass-row \{[^}]*max\(1\.1rem, min\(calc\(var\(--corner-radius/, "Crystal's glass rows lost their corner-safe padding");
  assert.match(css["src/app/clay-engine.css"], /\.pricing-clay__card \{ padding-inline: max\(30px/, "Clay's bespoke cards lost their corner-safe padding");
});

test("every rounded-<key> utility used in markup is a radius Tailwind knows", () => {
  const config = readFileSync(join(root, "tailwind.config.ts"), "utf8");
  const radiusBlock = config.match(/borderRadius\s*:\s*\{([^}]*)\}/)?.[1] ?? "";
  const known = new Set(["sm", "md", "lg", "xl", "2xl", "3xl", "full", "none", "t", "b", "l", "r", "s", "e", "tl", "tr", "br", "bl", "ss", "se", "ee", "es"]);
  for (const m of radiusBlock.matchAll(/(\w+)\s*:/g)) known.add(m[1]);
  for (const [file, text] of tsx) {
    for (const m of text.matchAll(/rounded-([a-z][a-z0-9]*)(?![\w-])/g)) {
      assert.ok(known.has(m[1]), `${file}: rounded-${m[1]} is used but tailwind.config.ts defines no such borderRadius key — Tailwind emits nothing and the corners stay square`);
    }
  }
});
