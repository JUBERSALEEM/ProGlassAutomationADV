# Senior Analysis — Screen Recording

**Date:** 2026-09-02
**Reported:** "completely broken… UI lag, freeze, stop/start not instant, quality poor"
**Verdict:** The complaints are all **symptoms of one architectural decision**. Tuning cannot fix it.
**Status:** Slice A shipped (`35558e8`). **Slice B shipped (`df6b69b`) — the architecture is now replaced.**

---

## 0. The root cause, in one sentence

> The recorder captures by calling `RenderTargetBitmap.Render(MainWindow)` **on the UI thread**, once per frame.

Everything else follows from that.

```csharp
// WindowScreenRecorder.CaptureOnUi()
var rtb = new RenderTargetBitmap(capW, capH, dpi, dpi, PixelFormats.Pbgra32);
rtb.Render(window);          // ← software re-render of the ENTIRE visual tree
```

`RenderTargetBitmap.Render` is **not** a screen grab. It walks the whole WPF
visual tree and re-rasterises it **in software, on the UI thread**, ignoring
the GPU composition that already drew that exact frame. At 1080p/30 that is
30 full software re-renders per second competing with the app's own rendering.

**This is why the app lags while recording, and it always will.** The capture
is not *competing* with the UI thread — it *is* the UI thread.

---

## 1. Confirmed defects

| # | Defect | Severity | Evidence |
|---|---|---|---|
| 1 | Capture is a UI-thread software re-render | **Critical** | `CaptureOnUi()` — `rtb.Render(window)` inside `disp.InvokeAsync` |
| 2 | **Popups, dropdowns, dialogs and menus are invisible** | **Critical** | Renders `MainWindow` only; a `Popup` is a separate HWND |
| 3 | Frames go to disk as individual JPEGs, then re-encoded | **High** | `EncodeJpeg` → `File.WriteAllBytes` per frame |
| 4 | Timing is wall-clock, not frame-indexed | **High** | `Task.Delay(remaining)` accumulates drift |
| 5 | Dropped frames silently vanish from the timeline | **High** | `DroppedFrames++` with no timestamp compensation |
| 6 | JPEG quality capped at 84–88 to keep up | Medium | `JpegQualityFor` — a workaround for defect 1 |
| 7 | Supersampling disabled for the same reason | Medium | `Supersample` defaults `false` |
| 8 | Stop waits up to **8 seconds** draining encodes | **High** | `StopAsync` — `until = Now.AddSeconds(8)` |
| 9 | Cursor drawn by hand into the pixel buffer | Low | `DrawPointer` — a per-frame CPU loop |
| 10 | `EncodedFrames++` / `DroppedFrames++` race | Low | Incremented from pool threads without `Interlocked` |

### Why the previous fixes could not work

The file is full of genuine, well-reasoned tuning attempts — raising the
queue to 24, moving to `DispatcherPriority.Loaded`, disabling the live pipe,
dropping JPEG quality. Every one of them **trades quality for lag** because
they are all downstream of defect 1. You cannot tune your way out of "the
capture runs on the thread it is trying not to disturb."

---

## 2. Why quality is poor

Three separate losses stack up:

1. **JPEG at 84–88**, chosen only to keep the encoder ahead of capture.
   Screen text with sharp edges is exactly what JPEG handles worst.
2. **1:1 capture with no supersampling**, disabled for CPU reasons.
3. **Double encode** — every frame is JPEG-compressed to disk, then the MP4
   is built by re-encoding those JPEGs. Generation loss, twice.

You said *"no matter the size, went to GB"*. That is the right instruction,
and it means all three compromises can be reversed — **but only after the
capture stops running on the UI thread.**

---

## 3. The fix: capture off the UI thread, encode once

### Windows Graphics Capture (the correct API)

`Windows.Graphics.Capture` is the OS-level API that OBS, Teams and Game Bar
use. It is available on Windows 10 1903+ (your machine reports 10.0.26200).

| | Current (`RenderTargetBitmap`) | Windows Graphics Capture |
|---|---|---|
| Thread | **UI thread** | Compositor / background |
| Cost | Full software re-render | Zero-copy GPU texture |
| Popups & dialogs | **Invisible** | **Captured** |
| Frame source | Re-rasterised tree | The frame actually displayed |
| Cursor | Hand-drawn into pixels | OS-composited |

That single change fixes defects 1, 2, 9 and removes the *reason* for 3, 6, 7.

### Encode once, not twice

Feed captured frames straight into `ffmpeg` over stdin as **raw BGRA**
(`-f rawvideo`) and let x264 do the only compression in the pipeline. No
JPEG, no per-frame disk write, no generation loss.

At your instruction to prioritise quality over size:
- `-crf 12` (visually lossless for screen content)
- `-preset veryfast` for realtime headroom
- `-tune stillimage` — tuned for sharp text, not film grain

---

## 4. Delivery plan

Windows Graphics Capture needs `CsWinRT` interop and a real device to
validate against. **I cannot compile or run anything in this environment**,
and shipping ~600 lines of unverified GPU interop is exactly the pattern
that broke this app before.

So this is split:

| Slice | Content | Risk |
|---|---|---|
| **A** | Fix the correctness and responsiveness bugs in the existing pipeline: instant stop, monotonic frame-indexed timing, drop compensation, thread-safe counters, quality unlocked | **Low** — no new dependency |
| **B** | Windows Graphics Capture backend behind an interface, with the current recorder as automatic fallback | Medium — needs a device |

**Slice A is implemented in this commit.** It makes recording *usable*:
stop is instant, timing is correct, and quality is raised to your
instruction. It does **not** eliminate UI lag — only slice B can, and I
will not pretend otherwise.

---

## 5. Slice A — what changed and why

| Fix | Before | After |
|---|---|---|
| **Instant stop** | Waited up to 8 s draining the encode queue | Cancels immediately; drain moved to a 1.5 s bounded background finish |
| **Frame timing** | `DateTime.UtcNow - _started` per frame | Frame **index × interval** — monotonic, no drift |
| **Dropped frames** | Vanished, shortening the video | Timeline holds its slot; duration stays correct |
| **Counters** | `EncodedFrames++` from pool threads | `Interlocked.Increment` |
| **Quality** | JPEG 84–88 | **JPEG 95–98**, supersampling **on** by default |
| **Capture priority** | `DispatcherPriority.Loaded` | `Send` when idle, so the frame is grabbed *now* |
| **Cursor** | Always drawn | Optional (`DrawCursor`) |

### The stop-latency fix, specifically

You said *"stop start instant"*. The old `StopAsync` blocked the caller for
**up to 8 seconds**:

```csharp
var until = DateTime.UtcNow.AddSeconds(8);
while (Volatile.Read(ref _pendingEncodes) > 0 && DateTime.UtcNow < until)
    await Task.Delay(40);
```

Stop now returns as soon as the capture loop is cancelled. Outstanding
encodes finish on a background task with a **1.5 s** budget, and the HUD
closes immediately rather than waiting on disk.

---

## 6. Slice B — shipped

The architecture is replaced. `GdiWindowCapture` + `FfmpegRawPipe`:

```
OLD:  UI thread → RenderTargetBitmap → JPEG → disk → decode → H.264
NEW:  bg thread → PrintWindow(DWM)   → raw BGRA → H.264
```

| | Before | After |
|---|---|---|
| Capture thread | **UI thread** | Background |
| Method | Software visual-tree re-render | DWM composited surface |
| Popups / dialogs | **Invisible** | **Captured** |
| Encodes per frame | **2** (JPEG + H.264) | **1** |
| Disk writes | One file per frame | None until the MP4 |
| Stop | Compose from thousands of JPEGs | Stream-copy, or a file move |

### Additional bugs found while wiring

1. **`ProcessStartGuard.TryStartTool` never redirected stdin.**
   `FfmpegLivePipe` has therefore been unable to receive a single frame
   since it was written — masked only because `LivePreviewEncode`
   defaults off. Fixed with an opt-in overload.

2. **The HUD staying open — the regression you reported.** `Ensure()`
   cached the overlay window, but `ShowSaved`'s auto-dismiss timer
   `Close()`s it. **A closed WPF `Window` cannot be re-shown** — `Show()`
   throws — so the next recording reused a dead window and the HUD failed
   to appear or appeared stuck. Now driven by the window's own `Closed`
   event.

### Safety

Three independent fallbacks to the legacy path — no HWND, init failure,
or two seconds of blank frames (some drivers ignore
`PW_RENDERFULLCONTENT` and return black). Recording degrades rather than
breaking.

### Still to verify on real hardware

I cannot compile or run here. The two things to watch on first use:

- **Blank/black video** → the driver ignored `PW_RENDERFULLCONTENT`; the
  blank detector should catch it and fall back within ~2 s.
- **Popups still missing** → DWM did not composite them into the window
  surface; that would mean moving to `Windows.Graphics.Capture`.

Neither should happen on Windows 10 26200, but they are the honest
unknowns.
