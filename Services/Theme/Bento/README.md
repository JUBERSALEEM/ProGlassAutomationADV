# Bento Grid — research record

Research captured *before* implementation, per the engine's standing rule.
Every decision in `BentoRecipe.cs`, `BentoPanel.cs` and `BentoSurface.cs`
traces back to a line in this file. Where the sources disagreed, the
disagreement and its resolution are recorded too, because the losing
argument is usually the one that gets reintroduced later.

---

## 1. The defining rule

A bento grid is **not** a card grid. A card grid puts N equal cells in a
grid and calls it modular. A bento grid puts **unequal** cells in a grid.

> "At least two distinct cell footprints (say 1×1 and 2×1) must appear,
> or the grid is just a uniform card grid with a different name."

So the failure mode to design against is the *uniform grid*, not the
crowded one. This is stated bluntly in the research: an LLM asked for a
bento grid tends to produce a uniform grid with icons, because that is
the most common labelled example in the training data. The engine's
countermeasure is to make the footprint **declarative and enumerable** —
`BentoSpan` values, not free-form sizes — so a uniform layout cannot be
expressed without deliberately choosing it.

## 2. Footprints and the anchor

Allowed footprints: **1×1, 2×1, 1×2, 2×2**. Exactly one 2×2 per grid.

Two separate rules converge on the same thing:

> "Exactly one 2×2 hero cell."
> "The anchor cell is the largest, usually placed left or upper-left…
>  A bento without a clear anchor reads as a flat card row."

and

> "Too many large tiles: when everything is big, nothing is important.
>  Maximum 1–2 hero tiles per viewport height."

`BentoPanel` therefore enforces **at most one** `BentoSpan.Hero` (2×2).
A second one is not a styling problem to be resolved by the caller — it
is the composition failing its own premise, and the panel reports it.

> "Tile size reflects data priority, not content volume."

This is the rule that makes `BentoSurface` refuse to grow. A tile's span
is set by rank; the tile does not measure itself into a size. See §6.

## 3. Spacing — three distances, not one

The spacing rules are more specific than "use a consistent gap":

| Distance | Value | Job |
| --- | --- | --- |
| **Gutter** | base unit × 2 (16px at unit 8) | Separates cells. Tells the eye these are cells of one composition. |
| **Inner padding** | ~2× gutter (24–32px) | Room for content to breathe inside a cell. |
| **Section margin** | ≥ 1.5× gutter | Vertical breathing so the section reads as discrete. |

> "The gutter is roughly half the inner padding."
> "Use a uniform gap between all grid cells… if your grid gap is 20px,
>  your card inner padding should ideally be 24–32px."

> "One gap and radius system. Every cell shares the same gap value and
>  the same corner radius, so the eye reads the tiles as one object
>  instead of unrelated cards."

Two failure modes are named: gutter too tight reads as one block, too
generous reads as disconnected cards. And **the gap must be half the
radius or less** — a gap larger than the corner radius visually detaches
the tiles from each other.

## 4. The ordering trap — the single most important rule

> "Never use `grid-auto-flow: dense`."
> "The dense algorithm 'attempts to fill in holes earlier in the grid, if
>  smaller items come up later. This **may cause items to appear
>  out-of-order**, when doing so would fill in holes left by larger
>  items.'"
> "Ban it outright: no `grid-auto-flow: dense`, ever."

> "The visual order on desktop does not automatically match DOM order.
>  And DOM order is what screen readers and keyboard navigation follow.
>  … In poorly structured bento layouts, those are two different
>  dashboards."

> "A sighted user reads in visual order. A screen reader user navigates in
>  DOM order."

> "The safest approach: structure the DOM in the same sequence the grid
>  renders visually. Avoid using `tabindex` values above 0 to force tab
>  order; this creates accessibility debt that compounds every time the
>  grid layout changes."

### How this maps to WPF — and why it is worse, not better

WPF's `Grid` does **not** have `grid-auto-flow: dense`, so the trap
cannot arrive by transcription. It arrives the same way anyway, and this
engine has to defend against both doors:

1. **Authoring order.** `Panel.Children` order drives layout order, hit
   testing order, tab order, and automation tree order. A bento panel
   that "helpfully" back-fills a hole with a later, smaller child
   reproduces `dense` exactly — and `BentoPanel` must not do this.

2. **`TabIndex`.** Setting `TabIndex` to force an order is the WPF
   spelling of `tabindex > 0`, and it is the same accumulating debt. A
   later edit that reorders `Children` silently desynchronises the tab
   order from the visual order, and nothing warns.

**Decisions:**

- `BentoPanel` arranges in **declaration order**, strictly. Hole-filling
  is refused. The panel reports leftover cells instead of hiding them,
  because a hole is a layout authoring error that should be visible.
- `BentoSurface` sets **no `TabIndex`** and `BentoPanel` sets
  `KeyboardNavigation.TabNavigation = Continue`, so tab order follows
  declaration order, which follows reading order by construction.
- Layout order is not permitted to be separable from reading order. One
  ordering, used by everything.

**Responsive note.** Collapsing to a single column restores plain
declaration order, which is why the sources stress that the *authoring*
order must already be the intended mobile order. `BentoPanel` collapses
in declaration order for the same reason.

## 5. Accessibility requirements

From WCAG 2.1 AA, as applied to bento:

| # | Requirement | Source |
| --- | --- | --- |
| 1 | Tab order follows visual reading order | §4 |
| 2 | Every tile has a label **at container level** | below |
| 3 | Contrast ≥ 4.5:1 body, ≥ 3:1 large text | below |
| 4 | Keyboard-navigable tiles with visible focus rings | below |
| 5 | `prefers-reduced-motion` fallback | below |

**(2) is the one that is missed.** A tile showing `$142,000` means
nothing to a screen reader without context:

> "Apply `aria-label="Monthly Recurring Revenue: $142,000"` on the tile
>  container element, **not just on the number inside it**."

WPF spelling: `AutomationProperties.Name` on the `BentoSurface`, which
is the tile container — not on the inner `TextBlock`. The two are
different elements and setting it on the inner one is the same mistake.

**(3) contrast.** Dark bento grids fail more often than light:

> "Light gray text on a dark gray tile common in glassmorphism
>  implementations frequently falls below the 4.5:1 threshold."

`BentoPalette` enforces the floor the way `SwissPalette` does — derived
colours are pushed until they clear it — rather than trusting the caller.

**(4) focus.** Every tile that is interactive gets a real ring. This is
not optional here specifically because `BentoSurface` is usually a large
target whose *size* already implies interactivity, so the ring is the only
thing confirming it is actually focusable.

**(5) reduced motion.** WPF's equivalent of `prefers-reduced-motion` is
`SystemParameters.ClientAreaAnimation`, with
`SystemParameters.AnimationDuration` as the duration source. `BentoStack`
consults both; it does not animate unconditionally.

## 6. Controls: self-contained cells

> "Make blocks self-contained — each bento cell should be a complete unit
>  with its own content hierarchy, not a fragment that depends on
>  adjacent cells."

> "If small-tile content stays to one label or one number."

> "No nested cards (cards inside cards)."

Two WPF consequences:

- **`BentoSurface` does not autosize to its content.** The span sets the
  box. An over-long child is the caller's layout problem, and `BentoSurface`
  clips rather than growing, because a cell that grew would break the row
  grid — which is exactly the "uniform grid" failure in slow motion.
- **`BentoStack` inside a tile is disallowed by guidance, not by the
  type system.** Documented; the panel reports a second anchored span
  inside a `Hero`, which is the detectable case.

## 7. Motion

Bento motion is minimal by construction — the grid is a fixed
composition and moving tiles destroys the alignment that makes it read
as one system. Permitted motion, all of it subordinate to §4's ordering
rule:

| Trigger | Motion | Constraint |
| --- | --- | --- |
| Tile hover | Border + fill change | **No translate, no scale.** Geometry is the grid. |
| Tile press | Fill shift ≤ `SoftUiAccessibility.PressedFillShift` | Never depth-only (WCAG 1.4.1). |
| Focus | Ring fade in | Disabled entirely under reduced motion. |
| Layout | **None** | Tiles never animate to a new cell. |

The no-translate rule is inherited from Swiss, where the argument is
stated at its strongest: *"The grid is a rational system and its GEOMETRY
MUST NOT BE DISTURBED BY INTERACTION. A card that rises on hover has
stopped sitting on the grid, and the grid is the entire composition."*
Bento is the same argument with a grid of cells rather than a grid of
rules, so the rule transfers unchanged.

## 8. Anti-patterns adopted as rules

| Rule | Reason |
| --- | --- |
| No nested cards | Explicitly listed as a bento anti-pattern. |
| No `grid-auto-flow: dense` / no hole-filling | Breaks reading order. |
| No raw grid line numbers | Use named areas / spans, so reflow is safe. |
| No fractional spans | "Keep every cell a whole number of units." |
| ≤ 3 distinct cell ratios per grid | Keeps the composition legible. |
| Max 1 hero per grid | §2. |
| No tiny uppercase tracked labels | Lists as a visual-avoidance rule. |
| No side-stripe accent borders | Listed among the anti-patterns. |

## 9. WPF-specific notes

- **Custom `Panel`, not `UniformGrid`.** `UniformGrid` cannot express a
  2×2 cell at all. A custom panel is required, and is also the only way
  to *enforce* the ordering rule rather than merely requesting it.
- **`MeasureOverride`/`ArrangeOverride`, not `Grid`.** Subclassing `Grid`
  would inherit its span machinery but also its freedom to place children
  out of order. The engine implements placement directly so the ordering
  guarantee is structural.
- **No WPF material brushes.** Bento is surface + border + fill. It has no
  dependency on `AcrylicBrush`/Mica (which WPF lacks — see the Liquid
  Glass research in `../LiquidGlass/README.md`) and needs no backdrop
  blur. The cells are opaque, which is also what makes the contrast floor
  in §5(3) achievable.

## 10. Sources

- setproduct.com — bento grid layout guide: the "five ways agents get bento
  wrong" table, the rules-file excerpt (4 units across, fixed row height,
  allowed footprints, no dense), and the DOM/mobile ordering note.
- orbix.studio — bento dashboard design: the WCAG breakdown, tile-span table,
  and the container-level label requirement.
- brainy.ink — bento spacing guide: the anchor/gutter/margin vocabulary and
  the three-distance spacing rule.
- effect-labs.com — bento layouts: reduced-motion and visible-focus
  accessibility blocks.
- stylekit.top — bento checklist: nested cards, contrast, reduced-motion,
  uppercase tracking, side-stripe borders.
- MDN `grid-template-areas` — named areas must form a rectangle or the whole
  declaration is invalid; and the `dense` ordering behaviour quoted above.
