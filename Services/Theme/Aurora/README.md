# Aurora — the ninth family

## What makes this family different from the other eight

Every other family in this engine is **surface-first**: the control is the
subject and the backdrop is stage dressing. Aurora inverts that. The
backdrop is built first and the controls are described relative to it.

The consequence is a rule the other families do not have:

> **The mesh never touches a control.**

Not on a card, not on a button, not on a row. The research is blunt about
it — *"constrain the mesh to backgrounds and large surfaces. The moment
you put a mesh gradient on a card or button, you've lost the plot."* That
is not a taste rule. It is the same contrast failure that killed
glassmorphism, arriving from a different direction: the colours under a
label move, so the contrast under that label moves with them.

## The six rules

1. **Dark is not a mode, it is the substrate.** *"The dark void (#06060F)
   is the required base"* — *"aurora over white dies."* There is no light
   page in this family. `AuroraProfile.Light` exists only because the
   engine's switch is global, and it is a dimmed violet cast of the same
   dark base, never an inverted one.

2. **Three blobs is the floor.** *"Two reads as a gradient, three reads
   as weather."* `AuroraMetrics.BlobCount` is `3` and is not a hint.

3. **One dominant hue, the others orbit.** *"Never give three colors equal
   weight or you get mud."* Indigo is the anchor at 55% of the field;
   violet and teal orbit it.

4. **Blur is structural, not a filter.** The sources blur with
   `filter: blur(60–80px)` because a CSS div has a hard edge. A WPF
   `RadialGradientBrush` fading to a transparent stop is *already* the
   blur — so `AuroraMesh` uses **no `BlurEffect` at all**. That is both
   faster and more correct, and it is the reason this family can afford
   to move at all.

5. **Drift on transform, never on the gradient.** *"Three oversized
   radial-gradient blobs on their own layers, blurred once, drifting on
   transform-only keyframes (never background-position — that repaints
   the gradient every frame)."* `AuroraMesh.Drift` animates only
   `TranslateTransform` and `ScaleTransform`. Gradient stops never move.
   The gradient is rasterised once and then composited.

6. **Slow, or it is noise.** *"Animate at glacial speed. If a user can
   consciously track the movement, it's too fast. Think 20–60 second loops
   minimum."* `AuroraMetrics.DriftPeriod` is **42 seconds**. The controls
   do not drift at all — only the sky does.

## Accessibility

- **Text never sits on the raw gradient.** Every text-bearing surface is a
  `Panel` or `Content` placement, both of which composite to an opaque
  floor before the ink is ever measured. Contrast is validated against the
  *worst* backdrop, following `GlassPalette.EnsureWorstCase`.
- **The rim is always drawn, and it is the accessibility feature.** Same
  forced-colors argument as Glassmorphism — high contrast strips
  translucency and leaves the rim as the only edge.
- **The rim is a gradient** (teal → violet, matching the field's own
  orbit). That is this family's one structural signature and it is also
  what keeps a translucent panel findable against a moving backdrop.
- **No shadow anywhere.** `AuroraStack.Shadows` is empty and
  `HasShadow` is `false`, for the same reason it is in Glassmorphism: there
  is no elevation ladder to encode. Depth is the aurora's job.

## Reduced motion

`AuroraMesh.Drift` returns `null` when
`EightThemePainter.ReducedMotionRequested` is set — the sky renders as a
composed still, which is a designed image, not a degraded one. Controls
keep their own short press spring because that is a *feedback* animation,
not ambience, and WCAG's reduced-motion exemption covers exactly that.
