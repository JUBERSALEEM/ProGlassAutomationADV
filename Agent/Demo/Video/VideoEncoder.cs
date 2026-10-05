// ═══════════════════════════════════════════════════════════════════════
//  FILE: Agent/Demo/Video/VideoEncoder.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-14 — PHASE 5: hardware H.264 encoding
//
//  Encoder selection model for the screen recorder.
//
//  H.264 stays the ONLY codec (Step 7: Android/Windows/browser/sharing
//  compatibility). The ENCODER behind it may be hardware-accelerated:
//
//      NVIDIA  → h264_nvenc
//      Intel   → h264_qsv
//      fallback→ libx264 (CPU — the pipeline that has always worked)
//
//  Everything in this file is PURE (no process, no IO) so the selection
//  and quality-mapping logic is unit-testable without ffmpeg. The
//  ffmpeg-dependent part (test-encoding to PROVE an encoder initializes)
//  lives in EncoderProbe.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Agent.Demo.Video
{
    /// <summary>A concrete ffmpeg H.264 encoder name + whether it is hardware.</summary>
    public sealed class VideoEncoderSelection
    {
        public string Name { get; }
        public bool Hardware { get; }
        public string Display { get; }

        /// <summary>The output codec this encoder produces (default H.264).</summary>
        public ScreenRecordCodec Codec { get; }

        public VideoEncoderSelection(string name, bool hardware, string display)
            : this(name, hardware, display, ScreenRecordCodec.H264)
        {
        }

        public VideoEncoderSelection(string name, bool hardware, string display, ScreenRecordCodec codec)
        {
            Name = name; Hardware = hardware; Display = display; Codec = codec;
        }

        public override string ToString() => Name;

        public static readonly VideoEncoderSelection Libx264 = new("libx264", false, "CPU (libx264)");
        public static readonly VideoEncoderSelection Nvenc   = new("h264_nvenc", true,  "NVIDIA (NVENC)");
        public static readonly VideoEncoderSelection Qsv     = new("h264_qsv",   true,  "Intel (QSV)");

        // HEVC counterparts. Not reachable from the Auto resolution path —
        // they are opt-in (ScreenRecordCodec) and their quality offset is
        // uncalibrated (see VideoEncoder.HevcQualityOffset). ByName maps them
        // so an opted-in choice round-trips through persistence.
        public static readonly VideoEncoderSelection Libx265   =
            new("libx265",    false, "CPU (libx265 / HEVC)",  ScreenRecordCodec.Hevc);
        public static readonly VideoEncoderSelection HevcNvenc =
            new("hevc_nvenc", true,  "NVIDIA (NVENC / HEVC)", ScreenRecordCodec.Hevc);
        public static readonly VideoEncoderSelection HevcQsv   =
            new("hevc_qsv",   true,  "Intel (QSV / HEVC)",    ScreenRecordCodec.Hevc);

        public static VideoEncoderSelection ByName(string? name) => name switch
        {
            "h264_nvenc" => Nvenc,
            "h264_qsv"   => Qsv,
            "libx265"    => Libx265,
            "hevc_nvenc" => HevcNvenc,
            "hevc_qsv"   => HevcQsv,
            _            => Libx264
        };
    }

    /// <summary>
    /// The user-facing encoder request (persisted in recording settings).
    /// </summary>
    public enum ScreenRecordEncoder
    {
        Auto = 0,      // best available: NVENC → QSV → libx264
        Cpu = 1,       // always libx264
        Nvenc = 2,     // NVIDIA — falls back per the chain if unavailable
        Qsv = 3        // Intel — falls back per the chain if unavailable
    }

    /// <summary>
    /// The OUTPUT CODEC (distinct from the ENCODER that implements it).
    /// Additive in the same style as <see cref="ScreenRecordEncoder"/>:
    /// existing values must never be renumbered, so persisted settings keep
    /// their meaning.
    ///
    /// H.264 is the DEFAULT and stays the default. HEVC is roughly 30-50%
    /// more efficient at equal quality, but (a) its CRF scale is NOT
    /// numerically equivalent to x264's, and (b) hardware and player support
    /// is narrower. Neither can be measured in this sandbox (no ffmpeg), so
    /// HEVC is implemented as an explicitly OPT-IN choice whose quality
    /// offset is UNCALIBRATED until tools/RecordingQualitySweep.ps1 has been
    /// run on the target machine.
    /// </summary>
    public enum ScreenRecordCodec
    {
        H264 = 0,
        Hevc = 1
    }

    /// <summary>
    /// Chroma subsampling for the encoded output (Phase 7 of the crisp-demo
    /// pass). yuv420p is the universal, player-safe default and is what the
    /// pipeline has always produced.
    ///
    /// yuv444p removes the chroma subsampling that smears COLOURED edges:
    /// red/blue text on white, thin coloured borders, coloured icon strokes
    /// and gradient banding all keep their true colour, which is a real win
    /// for screen content. The cost is compatibility: it needs the encoder's
    /// High 4:4:4 profile (software x264/x265 only — NVENC and QSV do not
    /// expose it for screen capture), materially more bitrate at equal
    /// quality, and several mainstream players/browsers will refuse the file.
    /// Therefore it is NEVER the default; see VideoEncoder.IsChromaCompatible.
    /// </summary>
    public enum ScreenRecordPixelFormat
    {
        Yuv420 = 0,
        Yuv444 = 1
    }

    /// <summary>Result of resolving a requested encoder against real capability.</summary>
    public readonly struct VideoEncoderResolution
    {
        public readonly VideoEncoderSelection Selection;
        public readonly bool Fallback;          // true when the requested encoder was unavailable
        public readonly string? Reason;         // human-readable, for [RecEncoderSelect]

        public VideoEncoderResolution(VideoEncoderSelection selection, bool fallback, string? reason)
        {
            Selection = selection; Fallback = fallback; Reason = reason;
        }
    }

    /// <summary>
    /// Pure selection + quality logic (Step 8: detection result in,
    /// concrete encoder out; Step 13: quality stays quality-based for
    /// EVERY encoder — CRF for libx264, VBR+CQ for NVENC, CQP for QSV;
    /// no fixed-bitrate mode is ever introduced).
    /// </summary>
    public static class VideoEncoder
    {
        /// <summary>
        /// Resolve a user request against the probed capabilities.
        /// Never throws; the libx264 fallback is always available (it is
        /// the existing pipeline — a build without it cannot record at
        /// all, so it is a hard prerequisite of the app, not of this
        /// method).
        /// </summary>
        public static VideoEncoderResolution Resolve(
            ScreenRecordEncoder requested,
            bool nvencUsable,
            bool qsvUsable)
        {
            // Fallback chain (Step 8): requested/preferred hardware
            // encoder → the OTHER hardware encoder → CPU libx264.
            // libx264 is the pipeline that has always worked, so the
            // chain ALWAYS terminates in a working encoder. An explicit
            // choice that falls back reports the reason; Auto walking
            // the chain is normal operation (no "fallback" flag).
            switch (requested)
            {
                case ScreenRecordEncoder.Cpu:
                    return new VideoEncoderResolution(VideoEncoderSelection.Libx264, false, null);

                case ScreenRecordEncoder.Nvenc:
                    if (nvencUsable) return new VideoEncoderResolution(VideoEncoderSelection.Nvenc, false, null);
                    if (qsvUsable) return new VideoEncoderResolution(
                        VideoEncoderSelection.Qsv, true, "NVENC initialization failed; using Intel (QSV)");
                    return new VideoEncoderResolution(
                        VideoEncoderSelection.Libx264, true, "NVENC initialization failed");

                case ScreenRecordEncoder.Qsv:
                    if (qsvUsable) return new VideoEncoderResolution(VideoEncoderSelection.Qsv, false, null);
                    if (nvencUsable) return new VideoEncoderResolution(
                        VideoEncoderSelection.Nvenc, true, "QSV initialization failed; using NVIDIA (NVENC)");
                    return new VideoEncoderResolution(
                        VideoEncoderSelection.Libx264, true, "QSV initialization failed");

                default: // Auto
                    if (nvencUsable) return new VideoEncoderResolution(VideoEncoderSelection.Nvenc, false, null);
                    if (qsvUsable) return new VideoEncoderResolution(VideoEncoderSelection.Qsv, false, null);
                    return new VideoEncoderResolution(VideoEncoderSelection.Libx264, false, null);
            }
        }

        /// <summary>
        /// The x264 CRF for the (tier, resolution) — the single quality
        /// number the UI tier system produces (see
        /// ScreenRecordSettings.EncodeCrf). All other encoders derive
        /// THEIR quality value from this one, so the tier keeps its
        /// meaning across hardware.
        /// </summary>
        public static int CrfForTier(int tierCrfBase, int resolutionOffset) =>
            Math.Clamp(tierCrfBase + resolutionOffset, 10, 28);

        /// <summary>
        /// PHASE 5 (Step 13): encoder-specific QUALITY-BASED rate
        /// control. Documented control methods:
        ///
        ///  libx264   : -crf {crf}                     (constant quality)
        ///  h264_nvenc: -rc vbr -b:v 0 -cq {cq}        (VBR with quality
        ///              target and no bitrate ceiling — the NVENC
        ///              equivalent of CRF)
        ///  h264_qsv  : -rc cqp -global_quality {gq}   (constant quality
        ///              per frame — the QSV equivalent of CRF)
        ///
        /// The cq/gq values run a few points above the x264 CRF at
        /// equivalent quality (cross-encoder practitioner equivalence,
        /// e.g. nvenc cq 20 ≈ x264 crf 18). These offsets are
        /// approximate and are exactly what the quality sweep / matrix
        /// tests exist to refine on real machines — they are NOT
        /// measured here (no ffmpeg in the dev sandbox).
        /// </summary>
        /// <summary>
        /// HEVC quality offset applied ON TOP of the x264-derived number when
        /// an HEVC encoder is selected. Deliberately 0 — HEVC currently uses
        /// the same numeric CRF as H.264 — because the correct value cannot be
        /// established without measurement, and inventing one would be a guess
        /// dressed up as a result. x265's CRF scale is known to sit at a
        /// different point than x264's, but the direction and magnitude depend
        /// on preset and content. Calibrate it against a reference H.264 encode
        /// (tools/RecordingQualitySweep.ps1) before enabling HEVC by default.
        /// </summary>
        public const int HevcQualityOffset = 0;

        public static (string Args, string RateControl, int QualityValue) BuildQualityArgs(
            VideoEncoderSelection enc, int crf)
        {
            int off = enc.Codec == ScreenRecordCodec.Hevc ? HevcQualityOffset : 0;
            if (enc == VideoEncoderSelection.HevcNvenc)
            {
                int cq = Math.Clamp(crf + 2 + off, 8, 46);
                return ($"-rc vbr -b:v 0 -cq {cq} -preset p5", "VBR+CQ", cq);
            }
            if (enc == VideoEncoderSelection.HevcQsv)
            {
                int gq = Math.Clamp(crf + 3 + off, 5, 50);
                return ($"-rc cqp -global_quality {gq}", "CQP", gq);
            }
            // Plain if/else (not a switch expression with block arms):
            // the arm-block form with local declarations is rejected by
            // some compiler versions — this form is unambiguous.
            if (enc == VideoEncoderSelection.Nvenc)
            {
                int cq = Math.Clamp(crf + 2, 8, 46);
                return ($"-rc vbr -b:v 0 -cq {cq} -preset p5", "VBR+CQ", cq);
            }
            if (enc == VideoEncoderSelection.Qsv)
            {
                int gq = Math.Clamp(crf + 3, 5, 50);
                return ($"-rc cqp -global_quality {gq}", "CQP", gq);
            }
            return ($"-crf {crf + off}", "CRF", crf + off);
        }

        /// <summary>
        /// Estimate the sustained encode fps at (width×height) from a
        /// MEASURED fps at a reference resolution. Encoding cost scales
        /// approximately with pixel count for all three encoders, so:
        ///
        ///     est = measured × (measuredPixels / targetPixels)
        ///
        /// clamped to a sane 1..240 range. This is an ESTIMATE used for
        /// the start-time FPS guard (Step 21) — never for the re-time
        /// math, which stays purely wall-clock based.
        /// </summary>
        public static double EstimateEncodeFps(
            double measuredFps, int measuredW, int measuredH, int targetW, int targetH)
        {
            if (measuredFps <= 0 || measuredW <= 0 || measuredH <= 0 || targetW <= 0 || targetH <= 0)
                return 0;
            double ratio = ((double)measuredW * measuredH) / ((double)targetW * targetH);
            return Math.Clamp(measuredFps * ratio, 1.0, 240.0);
        }

        // ================================================================
        //  CRISP-DEMO PASS — pure policy.
        //  Everything below is side-effect free and unit-testable without
        //  ffmpeg, a display, or Windows (Phase 16). Nothing here asserts a
        //  measured quality/bitrate claim: where a number would have to be
        //  benchmarked, the code takes it as an INPUT instead.
        // ================================================================

        /// <summary>
        /// x264 presets ordered CHEAPEST → MOST EXPENSIVE. The ladder stops at
        /// "medium" deliberately: screen capture has to encode in real time
        /// while the app is also rendering, and slow/slower/veryslow/placebo
        /// cannot hold 30-60 fps at 1440p-4K on a CPU encoder. No rung is
        /// claimed "best" — AdjustPreset picks the most expensive rung that a
        /// MEASUREMENT says can still hold the requested frame rate.
        /// </summary>
        public static readonly string[] X264PresetLadder =
        {
            "ultrafast", "superfast", "veryfast", "faster", "fast", "medium"
        };

        /// <summary>
        /// Spare-capacity factor required before the ladder steps UP to a
        /// richer preset. 1.15 means the encoder must demonstrably sustain 15%
        /// more than the requested frame rate before that headroom is spent on
        /// quality; the margin absorbs capture and UI work that the isolated
        /// probe does not see.
        /// </summary>
        public const double PresetHeadroom = 1.15;

        /// <summary>
        /// The STARTING preset rung for an output size (Phase 5). This is the
        /// same mapping the pipe has always used, lifted into a pure function so
        /// it can be unit-tested: the bigger the output, the cheaper the preset,
        /// because the encoder's only job is to keep up with real capture.
        ///
        /// These rungs are a STARTING POINT, not a verdict. AdjustPreset then
        /// moves along the ladder using the fps EncoderProbe actually measured.
        /// </summary>
        public static string DefaultPresetForOutput(long outPixels)
        {
            if (outPixels >= 3840L * 2160) return "ultrafast";
            if (outPixels >= 2560L * 1440) return "superfast";
            return "veryfast";
        }

        /// <summary>Index of a preset on the ladder, or -1 when not on it.</summary>
        public static int PresetIndex(string? preset)
        {
            if (string.IsNullOrWhiteSpace(preset)) return -1;
            for (int i = 0; i < X264PresetLadder.Length; i++)
            {
                if (string.Equals(X264PresetLadder[i], preset, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        /// <summary>Ladder rung, clamped to the ends (never throws).</summary>
        public static string PresetAt(int index) =>
            X264PresetLadder[Math.Clamp(index, 0, X264PresetLadder.Length - 1)];

        /// <summary>
        /// Measurement-driven preset step (Phase 5). Input is the sustained fps
        /// EncoderProbe MEASURED for the current preset, scaled to the output
        /// resolution by EstimateEncodeFps:
        ///
        ///   measured &lt; requested           → step CHEAPER (must hold the rate)
        ///   measured ≥ requested × headroom  → step RICHER  (spend spare capacity)
        ///   otherwise                          → hold
        ///
        /// With no measurement the current preset is returned unchanged: the
        /// policy never guesses. This is why the ladder is not hard-coded to
        /// "slow" — a preset that cannot hold the frame rate causes dropped
        /// frames, and the retime then stretches them into a duration-correct
        /// but motion-wrong recording, which is worse than any quality gain.
        /// </summary>
        public static string AdjustPreset(string? currentPreset, double? measuredFps, int requestedFps)
        {
            int idx = PresetIndex(currentPreset);
            if (idx < 0) return currentPreset ?? X264PresetLadder[0];
            if (!measuredFps.HasValue || measuredFps.Value <= 0 || requestedFps <= 0)
                return PresetAt(idx);
            double m = measuredFps.Value;
            if (m < requestedFps) return PresetAt(idx - 1);
            if (m >= requestedFps * PresetHeadroom) return PresetAt(idx + 1);
            return PresetAt(idx);
        }

        /// <summary>
        /// Keyframe interval in seconds (Phase 8). The pipeline previously
        /// emitted "-g {fps}", i.e. a 1-second GOP. Two seconds halves the
        /// keyframe count, which for mostly-static UI content removes a
        /// recurring bitrate spike and lets inter-frames carry the unchanged
        /// regions more efficiently — the same quality at less size, and a
        /// more stable bitrate for the 400-600 MB / 6 min target.
        /// </summary>
        public const double KeyframeIntervalSeconds = 2.0;

        /// <summary>GOP length in frames: 30 fps → 60, 60 fps → 120.</summary>
        public static int GopFor(int fps)
        {
            if (fps <= 0) fps = 30;
            return Math.Clamp(
                (int)Math.Round(fps * KeyframeIntervalSeconds, MidpointRounding.AwayFromZero), 2, 600);
        }

        /// <summary>B-frames requested on presets that actually use them.</summary>
        public const int BFrameCount = 3;

        /// <summary>
        /// x264's "ultrafast" preset sets bframes=0 internally, so an explicit
        /// "-bf 3" next to it is DEAD ARGUMENT TEXT — the command line claims a
        /// setting the encode never applied. Phase 8 requires such options to be
        /// removed or documented accurately; this removes it. Every other rung
        /// on the ladder does use B-frames, so those still emit "-bf".
        /// </summary>
        public static bool PresetSupportsBFrames(string? preset)
        {
            int idx = PresetIndex(preset);
            if (idx < 0) return false;   // unknown preset → emit nothing rather than lie
            return X264PresetLadder[idx] != "ultrafast";
        }

        /// <summary>"-bf 3", or "" when the preset makes it a no-op.</summary>
        public static string BFramesArg(string? preset) =>
            PresetSupportsBFrames(preset) ? $"-bf {BFrameCount}" : "";

        /// <summary>ffmpeg pixel-format name for a chroma setting.</summary>
        public static string PixelFormatName(ScreenRecordPixelFormat pf) =>
            pf == ScreenRecordPixelFormat.Yuv444 ? "yuv444p" : "yuv420p";

        /// <summary>
        /// 4:4:4 needs the encoder's High 4:4:4 profile. Software x264/x265
        /// have it; the NVENC and QSV screen-capture paths do not expose it, so
        /// asking for 444 there fails the encode outright instead of degrading
        /// gracefully. Never default to 444 — mainstream player and browser
        /// support is patchy — and when it is requested on hardware, fall back
        /// to 420 and report that (Phase 7).
        /// </summary>
        public static bool IsChromaCompatible(ScreenRecordPixelFormat pf, VideoEncoderSelection enc) =>
            pf != ScreenRecordPixelFormat.Yuv444 || !enc.Hardware;

        /// <summary>Maps an H.264 selection to its HEVC counterpart (Phase 4).</summary>
        public static VideoEncoderSelection WithCodec(VideoEncoderSelection sel, ScreenRecordCodec codec)
        {
            // Idempotent on purpose: an already-HEVC selection is returned
            // unchanged. Without this guard a second mapping pass would fall
            // through to libx265 and silently downgrade NVENC HEVC to a CPU
            // encode.
            if (codec != ScreenRecordCodec.Hevc || sel.Codec == ScreenRecordCodec.Hevc) return sel;
            if (sel == VideoEncoderSelection.Nvenc) return VideoEncoderSelection.HevcNvenc;
            if (sel == VideoEncoderSelection.Qsv) return VideoEncoderSelection.HevcQsv;
            return VideoEncoderSelection.Libx265;
        }

        /// <summary>
        /// True when the ENCODED output is larger than the PHYSICAL capture,
        /// i.e. the recorder UPSCALED. This is the normal case for a windowed
        /// capture at a 4K output setting: GDI captures the window's client
        /// size and ffmpeg's lanczos scaler enlarges it. Upscaling cannot
        /// invent detail — small text is not actually any sharper — so
        /// describing the result as "native 4K capture" would be false (Phase
        /// 10, the most important reporting fix in this pass).
        /// </summary>
        public static bool IsUpscaled(int captureW, int captureH, int outW, int outH) =>
            captureW > 0 && captureH > 0 && (outW > captureW || outH > captureH);

        /// <summary>
        /// The honest one-line provenance for the quality report, e.g.
        ///   "3840x2160 output UPSCALED from 1920x1080 capture"
        ///   "native 1920x1080 capture (1:1, no rescale)"
        /// </summary>
        public static string ResolutionProvenance(int captureW, int captureH, int outW, int outH)
        {
            if (captureW <= 0 || captureH <= 0 || outW <= 0 || outH <= 0)
                return "resolution provenance unknown";
            if (outW == captureW && outH == captureH)
                return $"native {outW}x{outH} capture (1:1, no rescale)";
            if (IsUpscaled(captureW, captureH, outW, outH))
                return $"{outW}x{outH} output UPSCALED from {captureW}x{captureH} capture";
            return $"{outW}x{outH} output downscaled from {captureW}x{captureH} capture";
        }
    }
}
