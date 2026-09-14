# Screen Recording — Phase-4 production optimization: quality, compression, large-file reliability

_2026-09-14_

Follow-up to `SCREEN_RECORDING_DURATION_FIX_2026-09-14.md` (Phase 3:
duration correctness, encoder-death visibility, measured validation).
Phase 4 makes the recording **sharp at reasonable size** and **safe at
long durations / large files**, without touching the duration logic.

## The current encoder (audited, not assumed)

`FfmpegRawPipe.TryStart` executes:

```
ffmpeg -y -f rawvideo -pix_fmt bgra -s {srcW}x{srcH} -framerate {fps}
       -i - -an
       -vf "scale={outW}:{outH}:flags=lanczos:force_original_aspect_ratio=decrease,
            pad={outW}:{outH}:(ow-iw)/2:(oh-ih)/2:color=black,format=yuv420p"
       -c:v libx264 -preset {preset} -tune stillimage -threads 0
       -crf {crf} -g {fps} -pix_fmt yuv420p
       -color_primaries bt709 -color_trc bt709 -colorspace bt709
       -movflags +faststart "{videoPath}"
```

- Codec/encoder: H.264 via **libx264** (compatibility-first — retained;
  HEVC/AV1 intentionally NOT switched to: the output must play on normal
  Windows systems and upload cleanly).
- Quality mode: **CRF-based** (quality-based, not fixed bitrate) —
  already the right mode for screen content.
- Preset: resolution-aware — ≤1080p `veryfast`, 1440p `superfast`,
  4K `ultrafast` (encoder must keep up with capture; a slow preset
  causes frame starvation, which the re-time then stretches into a
  choppy-but-correct video).
- GOP: one keyframe/second (`-g fps`). Audio: mixed separately, AAC-LC
  48 kHz (160 kbps from Phase 4; was 320 kbps).
- Container: MP4, `+faststart` (moov up front — share/upload friendly).
- No hardware acceleration (CPU x264) — stable across machines.

## Changes (smallest safe set — duration logic untouched)

| Area | Change |
|---|---|
| **Quality tiers** (`ScreenRecordSettings`) | New `QualityTier` { Standard, High (default), Very High } mapping to CRF baselines 22/18/13 with the existing per-resolution offset. **Very High = 14/13/12/12 — exactly the pre-Phase-4 CRFs**, so the near-lossless setting is one click away. New UI combo (Quality) in the record panel; the existing combo is relabelled Resolution. Persisted via `RecordingPreferences.QualityTier` (legacy configs hydrate to High). |
| **Audio** | 320 kbps → **160 kbps** AAC-LC in both mux paths — beyond speech transparency (the recorder's audio is voice), ~288 MB/hour saved. |
| **Large files** | All size math is 64-bit (long) end to end; the 256 MB disk floor and drive probe live on `FfmpegMp4Composer` (`MinFreeDiskBytes`, `FreeDiskBytes`) shared by service and capture loop. MP4 >4 GB is handled by FFmpeg's mov muxer (64-bit mdat automatically). |
| **Disk protection** | Pre-flight: refuse START below 256 MB free, log headroom estimate (`[RecStorage] preflight …`). In-recording: `[RecStorage] t= rawSize= estFinal= availDisk=` every 20 s (never per-frame); free space below the floor → `StorageFailure` + fatal stop → save refused with a professional message, files retained. |
| **Telemetry** | `[RecEncode]` one-shot line (codec/preset/crf/tier/in/out/fps) at pipe start; `[RecSummary]` gains `rawSize= diskFree= storageFailure=`; `[RecSaved]` gains measured `avgKbps=`. |

**Unchanged by design:** capture technology (GDI), re-time + `MaxRetimeScale`
+ `[RecValidate]` gate + encoder-death refusal (Phase 3), `-t` video-
authoritative mux, frame pipeline (one reused buffer, no queue),
lanczos/pad filter, BT.709 tagging, `+faststart`, preset selection,
resolution options, all unrelated modules.

## Large-file & memory audit (verified in code)

- File sizes: `FileInfo.Length` (long) everywhere; no 32-bit size casts.
- Durations: `TimeSpan` wall clock (10 000-year range); `slot` pacing is
  long ticks; ffprobe `-t` format is invariant decimal seconds.
- Frame counters: int — 2.1 B frames = 36 000 h at 60 fps (no realistic
  overflow; documented, API stable).
- Memory: **streamed** — one `byte[srcW×4×srcH]` buffer per recording
  (≤ ~33 MB at 4K), reused every frame; backpressure is ffmpeg stdin
  blocking on the capture thread (throttles capture, cannot grow RAM);
  no frame list / bitmap list / MemoryStream of video in the raw path.
- Finalization: file-based ffmpeg remux (stream copy), packing file
  deleted first, 12-byte MP4 signature check, bounded ffprobe.

## Honest limitations

- CRF/preset values are chosen from documented x264 screen-content
  behaviour + the Phase 2/3 field data, **not** from sandbox encoding
  (no ffmpeg/Windows here). The Step-20 matrix on the target machine is
  the measurement gate; `[RecEncode]`+`[RecSaved] avgKbps` make each
  result attributable.
- A disk can still fill in the gap between two 20 s storage checks; the
  broken-pipe path (encoder dies on a failed write) is the backstop and
  produces the same professional failure.
- At very low sustained throughput the video stays the right length but
  shows only as many frames as the encoder managed (Phase-3 behaviour;
  raising sustained 4K fps remains the Windows-Graphics-Capture
  workstream).
