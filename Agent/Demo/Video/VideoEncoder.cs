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

        public VideoEncoderSelection(string name, bool hardware, string display)
        {
            Name = name; Hardware = hardware; Display = display;
        }

        public override string ToString() => Name;

        public static readonly VideoEncoderSelection Libx264 = new("libx264", false, "CPU (libx264)");
        public static readonly VideoEncoderSelection Nvenc   = new("h264_nvenc", true,  "NVIDIA (NVENC)");
        public static readonly VideoEncoderSelection Qsv     = new("h264_qsv",   true,  "Intel (QSV)");

        public static VideoEncoderSelection ByName(string? name) => name switch
        {
            "h264_nvenc" => Nvenc,
            "h264_qsv"   => Qsv,
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
        public static (string Args, string RateControl, int QualityValue) BuildQualityArgs(
            VideoEncoderSelection enc, int crf)
        {
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
            return ($"-crf {crf}", "CRF", crf);
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
    }
}
