# Screen Recording — Phase-3 production repair: duration correctness, guaranteed

_2026-09-14_

Follow-up to `SCREEN_RECORDING_DURATION_FIX_2026-09-10.md` (one-frame-per-capture
+ `-itsscale` re-time). Phase 2 instrumentation
(`d36db10`) confirmed the re-time mechanism is in the normal save path;
this phase makes the result **correct and verifiable**, not merely present.

## The residual defects repaired

1. **The 50× re-time clamp silently under-corrected.** At the shipped 4K/60
   on a machine sustaining ~1 fps, a 5-minute recording produced a 5-second
   raw file needing ×60; the clamp delivered `5 × 50 = 250 s` — still
   materially short, reported as SUCCESS.
2. **`-shortest` let audio dictate video duration.** A dead or early-
   stopping loopback/mic track trimmed the container to the audio's length,
   cutting an otherwise-correct video.
3. **Silent encoder death.** If ffmpeg exited mid-recording the capture loop
   broke quietly; the HUD kept showing REC and the stop path could still
   report a partial file as SAVED.
4. **No verification of the delivered file.** Duration was never measured
   against wall clock before the SAVED state.

## The changes (smallest safe set — architecture preserved)

| Change | Where | What |
|---|---|---|
| Re-time bound 50× → **3600×** | `WindowScreenRecorder.MaxRetimeScale` (+ `ComputeRetimeScale`) | Full correction for any recording up to an hour, as long as a handful of frames exist. Pathological inputs stay bounded; the safety role of the old clamp is now covered by (a) the bound and (b) validation + encoder-death refusal below. |
| **Mandatory final-duration validation** | `UserScreenRecordService.StopAndSaveAsync` (both paths) | After the remux, the delivered MP4 is measured (bounded ffprobe) and compared to the real wall-clock duration. Tolerance = `max(2 s, 10%)` — 2 s for container/encoder granularity on short clips, 10% for tail jitter on long ones. FAIL → professional failure message, folder (incl. `capture.mp4`) retained, never SAVED. Logged as `[RecValidate]`. |
| **Video-authoritative muxing** | `FfmpegMp4Composer.RetimeCopyAsync` / `MuxVideoWithAudioAsync` | `-shortest` → `-t <realDuration>` when the real duration is known (the service always passes it). A slightly long audio tail is trimmed; a short/dead audio track can no longer truncate the video. Callers without a duration keep legacy `-shortest`. |
| **Encoder death is visible** | `WindowScreenRecorder` (`EncoderFailure`, `CaptureLoopFatal`) + service handler | A mid-recording ffmpeg death sets a user-facing failure, raises the fatal event, the service stops + reports the session immediately (HUD leaves REC), and the save path **refuses SUCCESS** while `EncoderFailure` is set — the professional message "The recording encoder stopped unexpectedly…" replaces any raw detail (technical detail stays in the log via the Phase-2 `[RecEncoder]` marker). Same auto-stop for the persistent-blank abort path. |
| Unit contract | `Tests/Agent/RecordingRetimeTests.cs` | New test: 300 frames @ 60 over 300 s → ×60 → exactly 300 s (the acceptance case). Clamp test updated to 3600×. |

**Unchanged by design:** GDI capture, ffmpeg pipeline, one-frame-per-capture
loop, re-time architecture, all Phase-2 diagnostics, cleanup behavior
(cleanup still runs only on success — failed saves retain `capture.mp4` and
logs), all unrelated modules. No frame duplication, no new queues, no
per-frame work, no UI-thread blocking (all new work is on the existing
thread-pool stop path; the fatal event handler schedules off-thread).

## Why this meets the acceptance test

Real = 300 s, encoder writes 300 frames (5 s claimed):
re-time ×60 → final 300 s → ffprobe 300 s → `|300−300| ≤ max(2, 30)` → PASS →
SAVED. Real = 300 s with an encoder that died at 1 s (60 frames): the
`EncoderFailure` gate refuses the save with the professional message (no
false success). Real = 300 s, any remux producing 5 s or 250 s: `[RecValidate]`
FAILs (250 s is 16.7% off > 10%) → professional failure, folder retained.

## Honest limitations

- At very low sustained throughput the video **duration** is correct but the
  **content** is choppy (it contains only the frames the encoder managed).
  Raising sustained 4K throughput is a separate workstream (Windows Graphics
  Capture, Slice B — already scoped in `SENIOR_ANALYSIS_SCREEN_RECORDING.md`).
- The 10 s `FinishAsync` bound can still kill a wedged encoder mid-flush
  (tail frames lost); the duration validation now turns that into a visible
  failure instead of a silent one.
