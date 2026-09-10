# Screen Recording — deep analysis, duration fix & 4K UHD support
_2026-09-10_

## The reported symptom
> "screen recording time is 3 minutes but later in the video it's only 1 minute"

A recording that ran for 3 real minutes saves as an MP4 that plays for ~1
minute (everything moves too fast). The effect is worst at high quality
and is *guaranteed* at the shipped **4K / 60 fps** default.

## Root cause (verified by reading the whole pipeline)

The capture pipeline is:

```
GdiWindowCapture (screen BitBlt, background thread)
    → raw BGRA over stdin → FfmpegRawPipe (ffmpeg -framerate N, CFR H.264)
    → capture.mp4 → save: mux audio → ScreenRecording_*.mp4
```

`FfmpegRawPipe` starts ffmpeg with a **fixed nominal `-framerate N`** and a
constant-frame-rate stream. So the encoded file's duration is *defined* as:

```
claimedDuration = framesWritten / N        (NOT wall-clock time)
```

To keep that honest, `WindowScreenRecorder.RawLoopAsync` used to **duplicate
the latest frame into every elapsed nominal slot** ("wall-clock fill") and
then **clamp** the overflow:

```csharp
if (pending > fps) { droppedFrames += pending - fps; pending = fps; } // real time discarded
```

Two things make this collapse the duration on real hardware:

1. **Encoder back-pressure.** One 4K BGRA frame is ~33 MB — far bigger than
   the OS pipe buffer — so `WriteFrame` **blocks** until x264 drains it.
   x264 cannot swallow 60 fps of 4K on a CPU, so the loop only writes
   frames as fast as the encoder consumes them (say ~20 equivalent fps).
   Over 180 real seconds it writes ~3600 frames → `3600 / 60 = 60 s`
   claimed. **3 minutes → 1 minute. Exactly the report.**
2. **The clamp threw real time away.** Every second the encoder fell
   behind, `pending > fps` discarded the surplus as "dropped", shortening
   the video further.

Two related defects were found in the same read:

- **Demo Center videos depended on dead code.** `DemoDirector` produced its
  MP4 from `recorder.Frames` (JPEGs) / `recorder.LiveVideoPath`, but the
  current raw pipeline writes **neither** by default — so demo output relied
  on a fallback that is normally empty.
- **The "fast path" stream-copied without re-timing**, inheriting the wrong
  `frames / nominalFps` duration.

## The fix

**1. One frame per real capture** (`WindowScreenRecorder.RawLoopAsync`)
No dup-fill, no clamp, nothing discarded. Every captured frame is a real
moment. This also removes the biggest source of encoder load, so 4K is
actually feasible and smooth.

**2. Re-time at save** (`FfmpegMp4Composer.RetimeCopyAsync`)
The file still claims nominal-fps timing, so on stop we rescale its
timestamps onto the **real elapsed duration** with ffmpeg `-itsscale`,
**stream-copying** the video (`-c:v copy`, no re-encode, no generation
loss) and muxing audio. The scale factor is:

```
RetimeScale = realElapsedSeconds / (framesWritten / nominalFps)
```

(pure, clamped, unit-tested as `WindowScreenRecorder.ComputeRetimeScale`).
When the encoder *did* keep up (scale ≈ 1) it transparently falls back to a
plain mux, so we never pay for a no-op.

Both the user recorder (`UserScreenRecordService`) and the demo recorder
(`DemoDirector`) now use this path; `DemoDirector` gains a **primary**
raw-capture branch so demo videos encode from `RawVideoPath` directly.

**Result:** duration is correct at any resolution / fps on any machine,
however far the encoder fell behind, and the save is a fast stream-copy.

## 4K UHD support

- 4K (3840×2160) is the **shipped default**, fully wired through settings,
  config persistence and both recorder UIs (720p / 1080p / 2K / 4K ×
  30/40/50/60 fps all remain available).
- `FfmpegRawPipe` now selects a **resolution-aware x264 preset**
  (`ultrafast` @2160p, `superfast` @1440p, `veryfast` ≤1080p) with
  `-threads 0`, so the CPU encoder drains each large frame fast enough to
  sustain a high capture rate. Low CRF (12 @4K) keeps text crisp; BT.709
  tagging + lanczos scaling preserve colour and sharpness for YouTube.

## Tests
`Tests/Agent/RecordingRetimeTests.cs` pins the timing contract, including
the exact 3-min-as-1-min case, near-unity no-op boundary, and clamps.
