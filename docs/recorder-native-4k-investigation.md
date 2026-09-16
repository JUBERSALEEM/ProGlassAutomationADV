# Native 4K Rendering — Investigation & Prototype Plan

**Status:** design only. **Nothing in this document is implemented.**
**Phase:** 11 of the crisp-demo pass.
**Rule this document exists to enforce:** do not integrate a native-4K render
path unless a prototype *measures* that it can sustain the requested frame rate
without freezing the application.

---

## 1. The current situation, verified from the code

| Stage | What actually happens | Where |
|---|---|---|
| Capture | GDI `BitBlt` (`SRCCOPY \| CAPTUREBLT`) from the desktop DC, at the **window's client size in physical pixels** | `Agent/Demo/Recording/GdiWindowCapture.cs` |
| Transport | BGRA byte buffer piped to ffmpeg's stdin | `Agent/Demo/Video/FfmpegRawPipe.cs` |
| Scale | `scale=outW:outH:flags=lanczos:force_original_aspect_ratio=decrease` + `pad` | same |
| Encode | H.264 / HEVC at the requested CRF | `Agent/Demo/Video/VideoEncoder.cs` |

So when the output setting is 4K and the window is 1920×1080, the recorder
produces a **3840×2160 file whose detail content is 1920×1080**. Lanczos is a
good interpolator; it is not a detail generator. Small text does not become
sharper — it becomes a larger, softer image of the same text.

Since the crisp-demo pass, this is reported honestly rather than implied:

```
[RecResolution] 3840x2160 output UPSCALED from 1920x1080 capture
```

and the pipe exposes `CaptureWidth`/`CaptureHeight` separately from
`OutputWidth`/`OutputHeight`, plus `ResolutionProvenance` and `WasUpscaled`.
**Never describe an upscaled result as "native 4K capture."**

The `Supersample` setting in `ScreenRecordSettings` is documented as legacy for
exactly this reason — it no longer changes capture geometry.

---

## 2. What "native 4K" would actually require

There is exactly one condition: **3840×2160 distinct physical pixels must exist
before the encoder sees them.** Two fundamentally different ways to get there.

### Route A — the window really is 4K on screen, and we capture it

The app renders at 4K because the display and DPI scaling make it so. Capture
then returns native 4K frames with **zero extra rendering cost** — this is the
same BitBlt/WGC path that works today, just with a bigger window.

Prerequisites:
- a 3840×2160 display (or a 4K-scaled one), and
- Windows scaling such that the window's client area is 3840×2160 **physical**
  pixels. At 150% scaling on a 4K panel the client area is 2560×1440 physical —
  still not native 4K. Check with `VisualTreeHelper.GetDpi(visual)` and
  `PresentationSource.CompositionTarget.TransformToDevice`, not with
  `ActualWidth` (which is in DIPs).

**Upgrade worth evaluating independently of this phase:** replace GDI `BitBlt`
with **Windows.Graphics.Capture (WGC)**. WGC pulls frames from the compositor on
the GPU, delivers them as `IDirect3DSurface`, handles DPI and occlusion
correctly, and does not require the window to be unoccluded the way `BitBlt`
does. It needs Windows 10 1803+ and a D3D11 device, and it changes the pixel
transport (surface → CPU buffer, or surface → NVENC directly). This is the
single highest-leverage change for *both* quality and CPU load, and it is a
prerequisite for feeding a hardware encoder without a CPU round-trip.

Cost/risk: low rendering cost; medium integration cost; the frame still has to
reach the CPU (or the encoder) somehow.

### Route B — re-rasterize the WPF visual tree at 4K off-screen

The window stays 1080p on screen; we render the visual tree a second time into
a 3840×2160 bitmap. Because WPF renders from **vector** instructions, this
genuinely produces sharper small text than upscaling a bitmap — it is the only
route that creates real detail that is not on screen.

This is the route the brief asks about (`RenderTargetBitmap`, `VisualBrush`,
thread affinity). It is also the expensive one, and ProGlass is close to a
worst case for it, because the app's whole identity is glass: blur, acrylic,
semi-transparency, shadows and gradients are precisely the effects that fall
back to costly software rasterization when rendered off-screen.

---

## 3. Route B in detail — the constraints that decide feasibility

### 3.1 Thread affinity is absolute

`Visual`, `UIElement`, `DependencyObject` and everything derived from them have
**strict thread affinity**. You cannot touch the live UI tree from a background
thread; WPF throws `InvalidOperationException` ("The calling thread cannot
access this object because a different thread owns it").

Consequences:

- `RenderTargetBitmap.Render(visual)` **must run on the thread that owns the
  visual** — i.e. the UI thread. There is no way around this for the live tree.
- A dedicated render thread therefore cannot render *the app's* visual tree. It
  can only render a tree **that it created itself**, on its own `Dispatcher`.
  That means duplicating the UI, which for ProGlass means duplicating the entire
  glass material system — not a small thing, and it would drift from the real UI.
- `VisualBrush` does **not** escape affinity either: its `Visual` source must be
  on the same thread as the brush's consumer.

What *does* cross threads: `Freezable` objects once frozen. `Freeze()` makes a
`Brush`, `Geometry`, `Drawing` or `DrawingGroup` immutable and thread-agnostic.
So the only viable off-thread shape is:

```
UI thread (every frame):   walk tree → produce a frozen DrawingGroup snapshot
Render thread:             Render(frozen DrawingGroup) into a 4K RenderTargetBitmap
```

The UI-thread half is still per-frame work on the thread that must also stay
responsive. There is no `VisualTreeHelper.GetDrawing`-style public API that
hands you the tree's drawing for free, so that snapshot has to be built by
walking (`VisualTreeHelper.GetDrawingContent` is internal; the practical
approach is `VisualTreeHelper.GetChild` + per-visual `DrawingContext` replay, or
`RenderTargetBitmap` at window size then re-scale — which defeats the purpose).

**Honest assessment:** for a complex themed WPF app, building a faithful frozen
drawing snapshot per frame is a large, fragile piece of work. Budget it as such.

### 3.2 The straightforward variant, and why it still costs

The simple prototype is:

```csharp
var dv = new DrawingVisual();
using (var dc = dv.RenderOpen())
{
    var vb = new VisualBrush(rootVisual) { Stretch = Stretch.None };
    dc.PushTransform(new ScaleTransform(sx, sy));   // sx = 3840 / windowWidth
    dc.DrawRectangle(vb, null, new Rect(0, 0, windowWidth, windowHeight));
    dc.Pop();
}
var rtb = new RenderTargetBitmap(3840, 2160, 96, 96, PixelFormats.Pbgra32);
rtb.Render(dv);
```

This runs **on the UI thread**. It re-rasterizes the vectors at 4K, so text is
genuinely sharper. It also blocks the UI for the whole render.

### 3.3 Memory arithmetic

| Item | Size |
|---|---|
| One 3840×2160 Pbgra32 frame | 3840 × 2160 × 4 = **33.18 MB** |
| At 30 fps, allocated per frame | ~995 MB/s of allocation |
| At 60 fps, allocated per frame | ~1.99 GB/s of allocation |
| 4-frame bounded queue at 4K | ~133 MB retained |

`RenderTargetBitmap` is **immutable** — you cannot reuse one. Every frame is a
fresh 33 MB object on the Large Object Heap. At 30–60 fps that is guaranteed
LOH churn and Gen2 collections, which show up as UI stalls — the exact failure
mode the brief forbids.

Mitigations to prototype:
- render at 4K every *N*-th frame and reuse the previous bitmap in between
  (acceptable for a mostly-static UI, terrible for animation);
- keep a persistent `WriteableBitmap` and `CopyPixels` from a smaller RTB
  (does not solve the re-rasterization goal);
- accept 4K/30 rather than 4K/60.

### 3.4 GPU vs software

`RenderTargetBitmap` renders through the **software** rasterizer. Effects that
are GPU-accelerated on screen — `BlurEffect`, `AcrylicBrush`/glass materials,
drop shadows, large gradients — are re-evaluated on the CPU at 4K, i.e. at 4× the
pixel count of 1080p. This is the single biggest feasibility risk for ProGlass
specifically, and it must be measured on a glass-heavy screen, not a plain one.

### 3.5 Animations and timing

An off-screen render is a *snapshot*, not a composition. Anything driven by
`CompositionTarget.Rendering`, storyboards, or the compositor's own clock will be
captured at whatever instant the render happens. If the render takes 80 ms, the
animation has moved on by the time the bitmap is done, and consecutive frames are
sampled at irregular intervals — which produces judder that no encoder setting
can fix. Capture must be driven from a consistent point in the frame
(`CompositionTarget.Rendering` on the UI thread) and the interval measured, not
assumed.

---

## 4. Prototype plan

Build it **outside the shipping recorder**, in a scratch window, with no
integration until the gates pass.

### Stage 0 — instrument what we have (½ day, zero risk)
Measure the current path's real numbers on the target machine with
`tools/RecordingPresetBenchmark.ps1 -Matrix`. Establish the baseline: capture ms
per frame (`GdiWindowCapture.LastCaptureMs` already measures this), actual
capture fps, encode fps. **Without this baseline, no later comparison means
anything.**

### Stage 1 — RTB feasibility spike (1–2 days)
Prototype §3.2 on the UI thread against three ProGlass screens, chosen to be
representative rather than convenient:
1. a dense DataGrid/report screen (small text, thin 1px borders, table lines);
2. a **glass-heavy** screen (acrylic cards, blur, shadows, gradients);
3. an animated screen (transitions, scrolling).

Record, per screen, at 1920×1080 and at 3840×2160:
- `rtb.Render()` wall-clock ms (min / median / p95 over ≥600 frames);
- resulting sustained fps;
- UI-thread stall observed (dispatcher frame gap);
- LOH / Gen2 collection count and peak working set (`GC.GetGCMemoryInfo()`);
- whether the app remained responsive (a queued input event must be serviced
  within, say, 100 ms).

### Stage 2 — quality verification (½ day)
Render the same frame at native 1080p-upscaled-to-4K (today's path) and at
native 4K (RTB). Compare **200% and 400% crops** of the smallest text, thin
borders and coloured icon strokes, side by side (Phase 18 procedure).

If the 4K re-rasterization is not *visibly* better on small text, the entire
route is worthless regardless of how fast it is — stop here.

### Stage 3 — only if Stages 1 and 2 both pass
Prototype a dedicated STA render thread with its own `Dispatcher`, fed by frozen
`DrawingGroup` snapshots (§3.1), and re-measure. Also prototype the
every-N-frames reuse strategy (§3.3).

---

## 5. Acceptance gates — all must pass, or do not integrate

| # | Gate | Threshold | Rationale |
|---|---|---|---|
| G1 | Sustained render fps at 4K | ≥ requested fps (30 or 60), measured over ≥60 s | the whole point |
| G2 | UI responsiveness | no dispatcher gap > 100 ms; no user-visible freeze | the brief forbids freezing the app |
| G3 | Memory | no unbounded growth; Gen2 collections < 1/min; working-set rise acceptable on an 8 GB machine | 33 MB/frame LOH churn |
| G4 | Visual quality | visibly sharper small text than the upscaled path at 200%/400% | otherwise pointless |
| G5 | Fidelity | glass/blur/shadow render correctly, no missing effects, no black regions | ProGlass's identity |
| G6 | No regression | existing recording path, audio, cursor, save location, validation, licensing all still work | Phase 20 |
| G7 | Measured, not assumed | every claim backed by a number from `tools/` output committed alongside | the master rule |

**If any gate fails, ship the honest upscale** — which is already correctly
reported — and record the failure with its numbers. A failed prototype that is
documented is a better outcome than an integrated one that freezes the app.

---

## 6. Recommendation

Ordered by (value ÷ risk), on the evidence available *before* any measurement:

1. **Route A with Windows.Graphics.Capture.** Real native 4K when the display is
   4K, no re-render cost, correct DPI and occlusion handling, GPU-side frames
   that can feed NVENC directly. This is where the effort belongs.
2. **Report the upscale honestly.** Already done (`[RecResolution]`,
   `ResolutionProvenance`, `WasUpscaled`). Costs nothing and removes the false
   "native 4K" claim immediately.
3. **Route B (RTB at 4K)** only for the specific case where the display is *not*
   4K but 4K detail is still wanted — and only if Stages 1–3 pass every gate.
   For a glass-heavy WPF app this is the high-risk option; the blur/acrylic
   software-rasterization cost (§3.4) is the thing most likely to kill it.

Do not start Route B without Stage 0 and Stage 1 numbers in hand.

---

## 7. What this document deliberately does **not** do

- It does not implement `RenderTargetBitmap` anywhere in the recorder.
- It does not change capture geometry, `Supersample`, or `CaptureSize`.
- It does not claim any frame-time, memory or quality number — every quantity
  above is either arithmetic (33.18 MB = 3840×2160×4) or a *threshold to be
  measured*.
- It does not assert that Route B will work. The honest position is that it
  probably will not sustain 4K/60 on a glass-heavy tree, and that only a
  measurement on the target machine can settle it.
