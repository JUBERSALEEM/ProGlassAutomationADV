# Screen Recording — Redesign, Lag & Quality Fixes

**Date:** 2026-08-21
**Branch:** `arena/01a0245f-proglassautomationadv`

Senior-developer analysis and implementation for the four reported
problems: the timer display bug, the oversized recording HUD, recording
lag, and recording quality / aspect ratio.

---

## 1. Time bug — fixed

**Symptom:** the timer read like `02:30.5`, and once a recording passed 99
minutes it rendered `100:05.0` — breaking the `MM:SS` layout.

**Root cause** (`RecordOverlayWindow.Fmt` + `DemoRecordingChrome.Fmt`):

```csharp
string.Format("{0:00}:{1:00}.{2:0}", (int)ts.TotalMinutes, ts.Seconds, ts.Milliseconds / 100)
```

- `{0:00}` is a **minimum** width, not a clamp — `TotalMinutes ≥ 100` prints
  three digits.
- `ts.Milliseconds / 100` is a decisecond digit that read like a stopwatch
  but added no real value for a screen recording.

**Fix:** a single `RecordingTime.Format(TimeSpan)` helper
(`Agent/Demo/Recording/RecordingTime.cs`) shared by the overlay and the
saved dialog:

| Duration | Output |
|---|---|
| `< 1h` | `MM:SS` (`07:42`) |
| `≥ 1h` | `H:MM:SS` (`1:07:42`) |
| negative | `00:00` |

This is exactly the Android screen-recorder style. Pinned by
`RecordingTimeTests` (including the 100-minute overflow case).

---

## 2. Recording HUD too big — redesigned to an Android-style pill

**Symptom:** the floating REC overlay was a ~420px-wide, multi-row card
(REC badge + time, a stats line, an audio line, a progress bar, and a
stage line) — far bigger than the "time · pause · stop" trio a phone
recorder shows.

**Fix** (`Views/Shared/RecordOverlayWindow.xaml(.cs)`): the always-visible
chrome is now a single pill:

```
● REC   12:34   [⏸] [⏹]
```

- Time on the **left**, then two **40×40 square controls** — pause (flips
  ⏸/▶ via Segoe MDL2 glyphs) and stop (red ⏹).
- Stats, audio, and progress-bar rows are **gone**. A single status line
  appears only while finalizing (`Saving…`) or after finishing
  (`SAVED · 02:31 · 120 frames` + a ✕ close button).
- Window auto-sizes to content, so it is now only as wide as the trio.

Bonus fix found during the redesign: `Tick()` re-positioned the window
**every 200ms** (setting `Left`/`Top` forces a move). It now repositions
only when the computed position actually changed by >0.5px — removing a
hidden source of jank while still following the main window.

---

## 3. Lag while recording — fixed

Three concrete CPU/latency wins, in order of impact:

1. **Live H.264 preview pipe now OFF by default.**
   `WindowScreenRecorder` was piping every captured JPEG into a concurrent
   ffmpeg encoder (`FfmpegLivePipe`) for the entire recording — a second,
   real-time H.264 encode whose output is **discarded on stop** (the final
   MP4 is always composed from the saved JPEGs for correct timing). That
   doubled encoder CPU for zero deliverable value. It is now gated behind
   `ScreenRecordSettings.LivePreviewEncode` (default `false`).

2. **Native 1:1 capture (supersampling OFF by default).**
   The old `CaptureSize` supersampled (e.g. 1080p captured at 2560×1440,
   ~1.78× the pixels) and downscaled. The final lanczos + unsharp filter
   chain recovers most of that sharpness, so capturing at the output size
   directly removes the largest per-frame cost. Optional via
   `ScreenRecordSettings.Supersample` for high-end machines.

3. **Encode queue 4 → 8.**
   `MaxPendingEncodes` was too tight — a busy dispatcher turned into
   dropped frames instead of a short encode backlog. Doubling it lets JPEG
   encodes (already on background threads) absorb UI bursts.

Result: far less CPU/GPU contention while recording, fewer dropped frames,
smoother UI.

---

## 4. Video quality enhanced

- **JPEG capture fidelity** (the real source of the final video) raised:
  720p 92→94, 1080p 94→96, 2K 95→96, 4K 96→97.
- **CRF** nudged one step higher-quality: 17/16/15/14 → 16/15/14/13
  (x264 default is ~23; final encode runs once at stop, so this costs disk,
  not recording latency).
- **Audio** 256k → 320k AAC, still 48 kHz.
- **Live pipe scaling** `fast_bilinear` → `lanczos` for when live preview
  is explicitly enabled.
- Final compose already applies `lanczos + unsharp` + `preset medium` +
  `profile:v high`; untouched (a good latency/quality balance at stop).

---

## 5. YouTube 16:9 aspect ratio — guaranteed by default

- Every output resolution is an **even 16:9 frame**: 1280×720, 1920×1080,
  2560×1440, 3840×2160. Default is **1920×1080** for both screen
  recording and demos (`DemoDirector.RecordSettings` default).
- The ffmpeg filter chain
  `scale=…:force_original_aspect_ratio=decrease, pad=W:H:(ow-iw)/2:(oh-ih)/2`
  letterboxes non-16:9 app windows **without distortion**, so any captured
  window lands in a clean 16:9 frame ready for YouTube.
- Pinned by `Every_quality_is_even_16_by_9` in
  `ScreenRecordSettingsTests`.

---

## Files changed

| File | Change |
|---|---|
| `Agent/Demo/Recording/RecordingTime.cs` | **new** — shared MM:SS / H:MM:SS formatter |
| `Agent/Demo/Recording/ScreenRecordSettings.cs` | `Supersample` + `LivePreviewEncode` flags, native capture default, higher JPEG/CRF |
| `Agent/Demo/Recording/WindowScreenRecorder.cs` | live pipe gated, encode queue 4→8 |
| `Agent/Demo/Video/FfmpegLivePipe.cs` | lanczos scaling |
| `Agent/Demo/Video/FfmpegMp4Composer.cs` | 320k audio |
| `Agent/Demo/DemoRecordingChrome.cs` | uses `RecordingTime.Format` |
| `Views/Shared/RecordOverlayWindow.xaml(.cs)` | compact pill HUD, no per-tick move |
| `Tests/Agent/ScreenRecordSettingsTests.cs` | updated + 16:9 / supersample / live-pipe defaults |
| `Tests/Agent/RecordingTimeTests.cs` | **new** — timer format incl. overflow |

## Verify

```bash
dotnet build ProGlassAutomation.csproj -c Release
dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release
```

Manual check: start a screen recording — the overlay is a single compact
pill (time left, pause + stop right), the timer shows `MM:SS`, and the UI
stays responsive (no live re-encode running in the background).
