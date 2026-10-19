# Glassmorphism — research record

The eighth and last family, and the one with the least room for
ambiguity, because the research reaches an uncomfortable conclusion
that has to be designed around rather than noted and ignored.

---

## 1. The finding that shapes this entire family

> "There is no version of glassmorphism — as it's aesthetically defined —
>  that reliably passes WCAG 2.2 for text over a dynamic background,
>  **without eliminating the transparency that defines the effect**."
>
> "Axess Lab tested 14 common glassmorphic UI implementations and found
>  that **none** of them maintained consistent WCAG 2.2 compliance across
>  varied backgrounds. The same component that passed at 5.2:1 over a
>  dark wallpaper failed at **1.8:1** over a light one."

And the two "fixes" are documented as not being fixes:

| Proposed fix | Why it is not one |
| --- | --- |
| Dark scrim behind the text | "You're not making accessible glass. You're using glass as decoration around an opaque text area." |
| Raise panel opacity to 85–90% | "The translucency effect is imperceptible. You've solved the contrast problem by not using glass." |

This is not a problem to be minimised. It is a direct conflict between
the style's defining property and WCAG 1.4.3.

### How this engine resolves it

Not by weakening either side. By **restricting where glass is allowed**,
which is what the sources that ship it actually do:

> "Use glass for only a few key elements. Stick to navigation bars,
>  primary buttons, or important cards, not every component."
>
> "Apple puts glass on **chrome, not content**: the nav scrim is nearly
>  opaque…"

**So: glass is a CHROME material in this engine, and content surfaces
are opaque.** `GlassSurface` therefore carries an enforced minimum
opacity (`GlassMetrics.MinimumSurfaceOpacity`) that no caller can go
below, and the fully-translucent treatment is reachable only through
the explicitly-named chrome path. The result still reads as glassmorphism
— chrome is where you see it, and that is where the eye looks first —
without putting body text on a 12%-alpha panel over a user's photo.

This is a deliberate, recorded trade. It is the only resolution in the
research that does not require pretending one of the two requirements
away.

## 2. Contrast is measured at the worst moment, not the average

> "WCAG 1.4.3 requires 4.5:1 **at the worst moment, not on average**. A
>  12% white fill over a user photo can swing from 8:1 contrast to under
>  2:1 as content scrolls behind it. If users scroll arbitrary content
>  under your glass, **you cannot certify contrast**."

And separately:

> "Contrast-check your text against the **blurred background**, not the
>  panel fill. The actual readability depends on what's bleeding through
>  underneath."

`GlassPalette` enforces both. Colours are validated against the
**composited** result (the same rule as `SpatialPalette`, and for the
same reason — an alpha colour has no meaningful contrast ratio on its
own), and the floor is applied against the **worst-case** backdrop the
plane can sit on, not the average one.

## 3. The recipe, and the numbers

The canonical recipe is four properties:

> `rgba(255,255,255,0.12)` fill, `blur(12px) saturate(160%)`, 1px light
> border, 16px radius, one soft shadow.

Per-element guidance:

| Element | Alpha | Blur | Radius |
| --- | --- | --- | --- |
| Standard cards | 0.08–0.15 | 8–12px | 16px |
| Text-heavy cards | 0.40–0.55 | 10–16px | 16–20px |
| Navigation bars | 0.25–0.35 | 12–16px | 0–12px |
| Modals | 0.15–0.25 | 12–20px | 20px |
| Buttons | 0.10–0.20 | 8–10px | 12px |

"Changing a glass layer from 20% to 30% opacity can dramatically
improve readability without breaking visual softness."

**Blur stays low.** "Start with low blur values. Something around
blur(4–6px) is usually enough. Larger values often look muddy", and
values above 24px make the background unreadable — "which may be
intentional for modal overlays but problematic for persistent UI
panels".

## 4. Uniform blur — and why that is the whole difference from Spatial

`EightMaterialKind.Frosted` is documented as *"Translucent, uniform
backdrop blur, hairline rim, static. The 2020 recipe."*

**Uniform.** That single word is what separates Glassmorphism from
Spatial in this engine, and it is worth being blunt about the
consequence: a uniform blur means the style has **no depth ladder at
all**. Everything is at the same distance. That is the 2020 recipe's
actual character — it creates depth through *material* (frosted,
lit-from-behind) rather than through *position* (a Z hierarchy).

`GlassMetrics` therefore has ONE blur radius and no per-tier variation,
and this is enforced rather than merely documented: every tier resolves
to the same value. The 2020 look is flat on purpose.

## 5. The 1px rim is an accessibility feature, not decoration

> "The 1px light border is an accessibility feature, not decoration:
>  **Windows forced-colors mode strips transparency** and leaves that
>  border as the only visible edge."

This is the highest-value single line in the research for a WPF
desktop app, because **WPF is Windows** — `SystemParameters.HighContrast`
is the real switch here, and it is not hypothetical. When high contrast
is on, the translucency is gone and the rim is the only thing
describing the panel.

`GlassPalette` reads high-contrast mode and collapses the fill to opaque
in that case. The rim is always drawn.

## 6. Rules adopted, and the traps

| Rule | Reason |
| --- | --- |
| Max 3 glass levels | "More than that and you're not creating hierarchy, you're creating confusion." |
| Never animate the blur radius | "Blur cost scales with blurred area." Animating it repaints the backdrop every frame. |
| Glass needs a busy backdrop | "Over a flat single color, blur has nothing to do and the panel just looks washed out." |
| Border 1px light, always | §5 — the forced-colors fallback. |
| Users can reduce transparency | "Apple's accessibility features allow users to reduce transparency or increase contrast, minimizing or removing the blur." |

The `backdrop-filter` containing-block trap is CSS-only (a `position:
fixed` descendant gets trapped), but the *reason* transfers: any
approach that repaints a full-page frosted surface on scroll is a
performance problem in WPF too, which is a real constraint on where
this material may be used.

## 7. WPF-specific notes

- **No `backdrop-filter`.** As with every other family here, there is no
  backdrop blur in WPF: `BlurEffect` blurs the element's OWN CONTENT.
  The "blur" in this family is expressed as fill opacity plus the
  compositing model, and the recipe does not pretend otherwise.
- **High contrast is directly reachable.** `SystemParameters.HighContrast`
  is the WPF-side equivalent of the forced-colors note in §5, and this
  family is the only one in the engine that must treat it as a
  first-class mode rather than an edge case.
- **Reduced transparency is reachable.** Same mechanism as
  `SpatialPalette.TransparencySuppressed`, and per NN/G it is a
  requirement rather than a nicety: *"give users the option to control
  contrast or transparency settings… for low-vision users."*

## 8. Sources

- codexical.com — the WCAG 2.2 analysis: 14 implementations tested, the
  5.2:1 → 1.8:1 swing, and why the scrim and opacity "fixes" are not
  fixes. This is the source the family is designed around.
- axesslab.com — 4.5:1 body / 3:1 large, semi-opaque fills behind text,
  and "do not rely solely on blur".
- superdesign.dev — the canonical four-property recipe, the per-element
  alpha/blur/radius table, "contrast at the worst moment", the
  forced-colors border note, and the blur-cost warning.
- NN/G — "text readability problems" as the primary failure, and the
  requirement to let users reduce transparency.
- uxpilot.ai — glass on a few key elements only; the 10–30% overlay;
  starting at 4–6px blur.
- wpdean.com — the per-element table and the 20%→30% opacity finding.
- designmd.app — 8–20% background opacity band, the 1px rim as the thing
  that "sells the physicality", and max three depth levels.
- superdesign.dev / inspoai — "Glass needs a busy backdrop"; blur above
  24px making the background unreadable; white or very light solid
  backgrounds making the effect disappear entirely.
