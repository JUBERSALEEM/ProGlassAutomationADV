# Spatial / Immersive — research record

Research captured before implementation, per the engine's standing rule.
Spatial is the one family in the eight whose material vocabulary is
**borrowed from the operating system**, so most of this file is about
what Windows actually does, what WPF can actually reach, and what has
to be simulated when the real thing is unavailable.

---

## 1. The Z hierarchy is the design

The engine's own contract for Spatial (`EightThemeId.cs`) states it:

> Depth-first. A real Z hierarchy (environment → ambient → panel →
> navigation → overlay) where **blur radius itself is the depth signal**.
> Glass over opacity, always.

Five planes, not four. `SoftDepthTier` has four (`Well`, `Resting`,
`Raised`, `Floating`), so Spatial declares its own `SpatialPlane` and
maps the tier onto it rather than pretending the tier is expressive
enough. Pretending would be the more convenient choice and it would
break immediately at the top of the range.

The profile fixes the blur ladder:

| Plane | Blur | What it is |
| --- | --- | --- |
| Environment | 0 | The room. Not blurred — it is the thing being blurred. |
| Ambient | 20 | Far chrome. The backdrop the whole app sits on. |
| Navigation | 20 | Chrome — nav pane, rails. Same radius as ambient. |
| Panel | 40 | Cards. The working surface. |
| Overlay | 60 | Dialogs, sheets, flyouts. |

Chrome and ambient share 20 deliberately. They are different planes in
the hierarchy but they read at the same distance, and giving navigation
its own radius would imply a depth the user does not perceive.

**Blur is the signal, not decoration.** This is the whole reason
Spatial exists as a family distinct from Glassmorphism, which uses one
uniform blur for everything.

## 2. Occluding vs transparent — the taxonomy that matters

Fluent splits materials in two, and getting this wrong is the single
most common way a "Fluent" app ends up looking wrong:

- **Occluding** (Acrylic, Mica) — base layers *beneath* interactive UI.
- **Transparent** (Smoke) — used to *highlight* immersive surfaces.

And within Acrylic there are two genuinely different things:

- **Background acrylic** — reveals the desktop wallpaper and other
  windows. Cross-process.
- **In-app acrylic** — blurs XAML content *within the app frame only*.
  It does **not** show the desktop.

Spatial is an in-app material. The app never sees the desktop, so the
whole family is built on the in-app recipe.

### Mica is OPAQUE

Repeated across sources: *"Mica is a new **opaque** material"*,
*"designed for performance as it only captures the background wallpaper
once"*. This is the most common misconception and it changes the
design: Mica is not "translucent light blue", it is an opaque
wallpaper-tinted surface. Spatial's panels are acrylic-family
(translucent); its environment layer behaves like Mica and is opaque.

## 3. What WPF can actually reach — and what it cannot

This is the load-bearing section, and it is the reason the family is
built the way it is.

**Not available in WPF.** `AcrylicBrush`, `MicaBackdrop`,
`DesktopAcrylicBackdrop`, `SystemBackdrop`, `SystemBackdropElement`,
`ParallaxView`, `MicaController`, `DesktopAcrylicController` are all
WinAppSDK/WinUI 3. WPF has none of them. (`AcrylicBrush` is the one
people expect most and it does not exist in WPF at all.)

**Available, with P/Invoke.** The DWM does support it for any top-level
window:

| Attribute | Value | Notes |
| --- | --- | --- |
| `DWMWA_SYSTEMBACKDROP_TYPE` | 38 | The supported API, build 22523+ |
| `DWMWA_MIC_EFFECT` | 1029 | Deprecated; the BOOL form |

Set via `DwmSetWindowAttribute`. The window stays **opaque to
composition** — the correct approach is *not* to make the window
transparent, which is the mistake that causes lost shadows and black
toolbars:

> "The correct approach for Windows 11 is not to make the window
>  transparent, but to instruct the DWM to replace the window's
>  non-client area background with the system-managed Mica or Acrylic
>  material."

This engine does **not** ship the DWM P/Invoke. Reasons, in order:

1. It is a *window-level* effect. This engine's families are
   *control-level*, and eight of them cannot each own a window hook.
2. It changes DWM state that outlives the theme, and the engine has no
   story for restoring it on family switch.
3. It is unverifiable here — no Windows, no build, no DWM.

Spatial therefore **simulates** in-app depth: layered translucent fills
over a gradient environment, with the blur ladder expressed as graduated
shadow geometry. The layering is honest about being simulated — see
§6 — rather than pretending to be acrylic.

## 4. Shadows encode elevation, and there are two of them

> "Shadows are generated using a combination of sharp, directional key
>  shadows for nearby objects and soft, diffused ambient shadows for
>  distant ones — for instance, a low-elevation shadow might use 2px
>  blur, while high-elevation ones reach up to 64px."

**Two shadows per surface, not one.** This is what separates a Fluent
surface from a generic drop shadow, and it is the direct cause of the
two-Border-per-shadow structure in
`Controls/Spatial/SpatialSurfaceParts.cs` — WPF allows exactly one
`Effect` per element, so the second shadow needs its own host.

Key shadow: tight blur, directional, anchored to `SoftLight`.
Ambient shadow: wide blur, low opacity, no hard direction.

This also means Spatial genuinely needs a **shadow budget**, unlike
Swiss and Bento which forbid shadows outright. Two shadows per surface
across five planes is a budget, and exceeding it is what makes a
Fluent-like theme muddy.

## 5. Motion

| Trigger | Motion | Source |
| --- | --- | --- |
| Parallax | Background moves slower than content | Fluent, "Parallax moves objects at different rates" |
| Press | "Momentarily **recedes** into the background, then returns" | Fluent press feedback |
| Open/close | Overlay scales from its anchor | Fluent |

**Press recedes.** This is the opposite of every other family in this
engine, where press shifts the fill *forward*. Spatial follows the OS:
the pressed item moves away from the viewer. The same rule holds for
Z-index: press lowers the plane, it does not raise it.

Parallax is suppressed under reduced motion
(`EightThemePainter.ReducedMotionRequested`), same as everywhere else.

## 6. Accessibility — the one that bites hardest

This is where borrowed OS material usually fails, because the OS
material was designed for a case WPF developers forget.

> "Acrylic falls back to **solid colors** when transparency is
>  disabled, battery saver is on, or hardware is constrained."
> "Always provide fallback styling: set FallbackColor and test with
>  Transparency disabled — make sure the UI is legible and functional
>  with acrylic disabled."
> "Respect system accessibility policies."

So the requirements are:

1. **Every translucent surface has an opaque fallback that is tested.**
   Not a computed-on-demand fallback — a real one, and `SpatialPalette`
   enforces it.
2. **Transparency can be off at the OS level.** WPF exposes the
   animation equivalent (`SystemParameters.ClientAreaAnimation`); the
   transparency preference has no direct WPF surface, so the engine
   publishes an explicit opt-out and the recipe reads it.
3. **Contrast is measured against the COMPOSITED result, not the fill.**
   A 40%-alpha panel over a bright environment is not the colour the
   fill declares. This is the same lesson as `BentoPalette`'s
   tile-vs-canvas distinction, and it is why Spatial's contrast floor
   is enforced against a simulated composite rather than against the
   raw translucent colour — which on its own would be meaningless.

The last point is the most-missed item in the whole family. A contrast
check run against an alpha colour is not a contrast check.

## 7. WPF-specific notes

- **`BlurEffect` does not do this.** It blurs the *element's own
  content*, not the backdrop behind it. (Established during the Liquid
  Glass research.) So the blur ladder cannot be a `BlurEffect` at all;
  it has to be expressed as surface opacity plus shadow geometry, which
  is what `SpatialRecipe` does.
- **Per-plane brushes, not one translucent fill.** The profile's ramps
  are alpha-bearing (`A("#F4F7FF", 0.62)`) precisely so each plane can
  carry a different opacity. One flat alpha for all five planes is the
  Glassmorphism mistake.
- **The environment layer is a gradient, not a colour.** Light mode
  here means "a bright luminous room", not white paper — the profile is
  explicit about this, and a flat environment would collapse the depth
  the family exists to show.

## 8. Sources

- learn.microsoft.com — Windows apps design, materials overview: the
  occluding/transparent split, Mica opacity, Acrylic's transient-only
  role, and the in-app vs background acrylic distinction.
- learn.microsoft.com — system backdrops: `MicaBackdrop`,
  `DesktopAcrylicBackdrop`, `SystemBackdrop`, the `IsSupported`
  controller checks, and the fallback guidance.
- learn.microsoft.com — parallax: background slower than foreground, and
  the `ParallaxView` control (WinUI only).
- Wikipedia / HandWiki — Fluent Design System: light, depth, motion,
  material, scale; the five components; press feedback receding.
- Grokipedia — Fluent: the key/ambient shadow pairing and the 2px→64px
  blur ladder with elevation.
- windowsforum.com — per-element acrylic in Windows 11: the shift from
  whole-window backdrops to surface-level `SystemBackdrop`, and the
  in-app `AcrylicBrush` recipe.
- coldfusion-example — "not to make the window transparent, but to
  instruct the DWM", and `DWMWA_SYSTEMBACKDROP_TYPE = 38`.
- tvc-16.science — WPF/Win32: `DWMWA_MIC_EFFECT = 1029` deprecated in
  favour of `DWMWA_SYSTEMBACKDROP_TYPE = 38`.
