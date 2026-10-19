# Neo-Brutalism — the tenth family

## The three rules, and that is the whole style

Strip away the colour and taste and this family is three declarations:

1. **A thick border.** 2–6px solid, near-black. *"At 1px a border is a
   hairline that separates two regions. At 4px it becomes a graphic
   element in its own right."*
2. **A hard offset shadow with zero blur.** `5px 5px 0 0 #000`.
   *"The zero blur radius is the load-bearing detail... Leaving even 2px
   of blur is the difference between the style reading as intentional and
   reading as a mistake."*
3. **Radius zero.** *"Square corners. That's the point."*

Everything else — colour, type, layout — serves those three.

## How this differs from Swiss, which also has radius 0

This family and Swiss both refuse rounding, which makes them easy to
confuse from a distance. They are opposites on the axis that matters:

| | Swiss | Neo-Brutalism |
|---|---|---|
| Radius | 0 | 0 |
| Shadow | **forbidden** | **load-bearing** |
| Palette | muted, refined, one accent | saturated, clashing, loud |
| Press | a fill shift | a physical stamp |
| Purpose | calm clarity | raw legibility |

`NeoBrutalMetrics.Radius` is `0` and `NeoBrutalStack.HasShadow` is
`true` — the inverse of Swiss on both, asserted in the tests.

## The shadow is an OFFSET PLATE, NOT AN EFFECT

`box-shadow: 5px 5px 0 0 #000` is a translated copy of the element's own
silhouette. So `PART_NeoShadow` is a plain `Border` behind the fill,
carrying the same geometry, moved by a `TranslateTransform`.

It is deliberately **not** a `DropShadowEffect`. Three reasons:

- An effect re-rasterises through the GPU effect pass; an offset Border is
  composited.
- Every element here is a rectangle with radius 0, so a Border is an
  exact silhouette. There is nothing an effect would do better.
- BlurRadius is what makes an effect soft. The one thing this family
  cannot have is softness, so the machinery built to produce softness is
  the wrong tool.

## The press is a stamp, and impact has no curve

*"Do not let a default transition near the press. The gesture is impact,
and impact has no curve."* `NeoBrutalMetrics.PressDuration` is **0**.

The gesture itself is the documented one: *"the element translates by
exactly the shadow offset while the shadow collapses to zero, so it reads
as being physically pressed."* Hover inverts it — the block lifts by 2px
and the shadow grows by 2px.

**And the press translates. It never adjusts margin or padding**, because
*"the press must translate, not adjust margin or padding, or the whole
board jumps on every click."* The movement is applied as a
`TranslateTransform` on the fill's `RenderTransform`.

## The gutter is an ENGINEERING constraint, not taste

*"Equal-offset hard shadows on a dense grid collide where two shadows meet
a gap. Keep the grid gap larger than the offset."*

`NeoBrutalMetrics.MinimumGutter` is 12 and the largest offset is 8, and
the tests assert `MinimumGutter > ShadowLarge` — because unlike a soft
shadow, a hard one does not fade. Two overlapping shadow plates just
merge into a black wedge.

## Accessibility

- **Ink is black on every accent fill.** *"Black text on acid yellow
  passes, white text on acid does not."* Measured on the shipped palette:
  Measured against this app's own ramps rather than quoted from the
  source — black-on-yellow **14.80:1**, black-on-pink **7.93:1**. Both
  clear AAA. (The source put white-on-yellow at 3.09:1; measured here it
  is **1.34:1**, so the real failure is worse than the one reported.)
- **The border is a non-text element and needs 3:1 against BOTH the fill
  and the surface behind it.** The classic failure is a black border on a
  dark section, so `NeoBrutalPalette` measures the ink against the fill
  *and* the page independently.
- **Focus states get eaten, so this family draws its own.** A 4px black
  border means the default focus ring lands on an existing black outline
  and disappears. `PART_NeoFocus` is an explicit offset outline in the
  accent, sitting OUTSIDE the border.
- **State is never the shadow alone.** *"Offset shadows must not be the
  only difference between resting and pressed; move the block and change
  the border weight together."* Press moves the block AND thickens the
  border. That second signal is also what survives reduced motion.
- **Disabled removes the shadow entirely**, so the block reads flat and
  inert rather than merely quiet.
