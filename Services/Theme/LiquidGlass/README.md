# Liquid Glass — design notes

Research record for the Liquid Glass family. Kept in-repo because the
sources are web pages that will change or disappear, and because the
distinction between Liquid Glass and Glassmorphism is the thing most
likely to be lost by a future editor who assumes they are synonyms.

## What this family is

Apple introduced Liquid Glass at WWDC25 (June 2025) and shipped it in
iOS 26, iPadOS 26, macOS Tahoe, watchOS 26 and visionOS 26. Its
defining property is **lensing**: the surface behaves as a lens that
bends what is behind it, rather than simply blurring it.

The material is composed of three cooperating layers:

- **Highlight** — light casting and movement across the surface.
- **Shadow** — depth and separation from the content layer.
- **Illumination** — the flexible properties of the material itself.

Each layer shifts continuously as content scrolls beneath, so tint and
dynamic range adapt to keep text legible while letting as much of the
underlying content through as possible.

## Liquid Glass is not Glassmorphism

Both families exist in this engine. They are not synonyms and they are
not variants of each other. Getting this wrong is the single most
likely way to implement this family badly.

| Axis | Glassmorphism | Liquid Glass |
| --- | --- | --- |
| Blurs the backdrop | Yes | Yes |
| Refracts (bends) the backdrop | No | Yes |
| Reflects surroundings | No | Yes |
| Specular highlights | Static border at best | Respond to geometry |
| Tint adapts to content | Manual | Automatic |
| Depth approach | Static see-through layering | Dynamic |
| Motion | Rare, decorative | Reactive |

Glassmorphism is a **recipe**. Liquid Glass is a **simulation**. A
single shared palette would collapse the two into "glassmorphism with a
brighter border", which is exactly what both systems are designed to
avoid. The palettes are therefore kept separate.

## The rules this family follows

**1. Navigation layer only.** Glass belongs on chrome that floats above
content — the bar, the rail, the floating button, the sheet. Not on the
content itself, and never on top of another glass surface. Glass on
glass flattens: the inner surface refracts the outer one's rim, the rim
refracts the content, and the result is a milky slab with two borders
and no hierarchy. `LiquidGlassPlacement` and `LiquidGlassDepth` expose
this as an executable decision rather than a comment.

**2. The specular edge is the highest-value element in the family.**
Without it a panel reads as tinted film. It is contrast-enforced to the
WCAG 1.4.11 non-text floor (3:1), because a rim is a component
boundary and not decoration.

**3. The rim survives reduced transparency; the tint does not.** A user
who asks for opaque surfaces still needs to see where one surface ends
and the next begins, and the rim is the only affordance left carrying
that information. Blur drops to zero under reduced transparency.

**4. Press does not darken.** The intuitive treatment is wrong here.
On a lens, darkening means less light entering the material, which
reads as the glass thickening rather than being pushed. The press
signal is the shadow collapsing and the refraction relaxing, which is
what a finger pressing a slab of glass actually does to it.

**5. Morphing thickens the material.** When a control expands into a
larger shape it simulates a thicker slab: deeper shadow, more
pronounced lensing, softer scattering. One rule, one knob
(`LiquidMetrics.MorphShadowScale` / `MorphRefractionScale`).

**6. Reduced motion removes travel, not feedback.** A user who asks for
less motion still needs to know the control responded, so the shadow
collapse on press survives and only the animation is suppressed.

## WPF constraints

WPF has no Acrylic or Mica brush of its own — the platform ships Mica
and Acrylic for WinUI and for window-level DWM backdrops only. The
window-level backdrop route (`DwmSetWindowAttribute` with
`DWMWA_SYSTEMBACKDROP_TYPE`) applies to a `Window`, not to arbitrary
elements, and this family needs its material on panels, rails and
buttons inside the app frame.

So the optics are **simulated with composition**, not acquired from the
system:

- tint and alpha for what the lens carries
- a diagonal specular gradient along the rim for the lens edge
- inner glow for illumination
- `DropShadowEffect` for the shadow layer
- refraction and dispersion as values the templates consume

This is a deliberate trade. It costs the true pixel displacement that
a Metal or WebGL implementation would give, and it buys a material that
composes in WPF, honours reduced transparency, and needs no native
dependency.

## Sources

- Apple, *Meet Liquid Glass*, WWDC25 —
  https://developer.apple.com/videos/play/wwdc2025/219/
- Apple Newsroom, *Apple introduces a delightful and elegant new
  software design* —
  https://www.apple.com/newsroom/2025/06/apple-introduces-a-delightful-and-elegant-new-software-design/
- CSS-Tricks, *Getting Clarity on Apple's Liquid Glass* —
  https://css-tricks.com/getting-clarity-on-apples-liquid-glass/
- theplusaddons, *Liquid Glass UI* (Glassmorphism vs Liquid Glass,
  and the specular edge) —
  https://theplusaddons.com/blog/liquid-glass-ui/
- setproduct, *Glassmorphism, neumorphism, liquid glass* (comparison
  table and anti-patterns) —
  https://www.setproduct.com/blog/liquid-glass-vs-glassmorphism
- dotnet/wpf, *Using Fluent* (WPF has no Acrylic/Mica; DWM backdrop
  only, and the app-context switch that disables it) —
  https://github.com/dotnet/wpf/blob/main/Documentation/docs/using-fluent.md
- Microsoft, *Acrylic material* (the recipe: background, blur,
  exclusion blend, tint, noise) —
  https://docs.microsoft.com/en-us/windows/uwp/design/style/acrylic
- Microsoft, *Materials overview* (Mica vs Acrylic and when each is
  appropriate) —
  https://learn.microsoft.com/windows/apps/develop/ui/materials