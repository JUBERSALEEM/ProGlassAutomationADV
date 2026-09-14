using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Agent.Demo.Recording
{
    public enum ScreenRecordQuality
    {
        Hd720 = 720,
        FullHd = 1080,
        Qhd2K = 1440,
        Uhd4K = 2160
    }

    /// <summary>
    /// PHASE 4 (2026-09-14) — encoder quality tier, independent of the
    /// output resolution. Maps to an x264 CRF baseline (lower = sharper,
    /// bigger); see <see cref="ScreenRecordSettings.EncodeCrf"/> for the
    /// per-resolution matrix.
    /// </summary>
    public enum ScreenRecordQualityTier
    {
        Standard = 0,
        High = 1,
        VeryHigh = 2
    }

    public sealed class ScreenRecordQualityOption
    {
        public ScreenRecordQuality Quality { get; init; }
        public string Title { get; init; } = "";
        public int Width { get; init; }
        public int Height { get; init; }
        public override string ToString() => Title;
    }

    public sealed class ScreenRecordQualityTierOption
    {
        public ScreenRecordQualityTier Tier { get; init; }
        public string Title { get; init; } = "";

        /// <summary>x264 CRF baseline before the per-resolution offset.</summary>
        public int CrfBase { get; init; }

        /// <summary>Fallback baseline when an unknown tier value is persisted.</summary>
        public const int DefaultBaseCrf = 18;

        public override string ToString() => Title;
    }

    public sealed class ScreenRecordFpsOption
    {
        public int Fps { get; init; }
        public string Title => Fps + " fps";
        public override string ToString() => Title;
    }

    /// <summary>
    /// In-app capture of the running main window only.
    /// 720 / 1080 / 2K / 4K at 30 / 40 / 50 / 60 fps.
    /// </summary>
    public sealed class ScreenRecordSettings
    {
        public static IReadOnlyList<ScreenRecordQualityOption> Qualities { get; } =
        [
            new() { Quality = ScreenRecordQuality.Hd720,  Title = "720p (1280 × 720)",   Width = 1280, Height = 720 },
            new() { Quality = ScreenRecordQuality.FullHd, Title = "1080p (1920 × 1080)", Width = 1920, Height = 1080 },
            new() { Quality = ScreenRecordQuality.Qhd2K,  Title = "2K (2560 × 1440)",    Width = 2560, Height = 1440 },
            new() { Quality = ScreenRecordQuality.Uhd4K,  Title = "4K (3840 × 2160)",    Width = 3840, Height = 2160 }
        ];

        public static IReadOnlyList<ScreenRecordFpsOption> FpsOptions { get; } =
        [
            new() { Fps = 30 },
            new() { Fps = 40 },
            new() { Fps = 50 },
            new() { Fps = 60 }
        ];

        // PHASE 4 (2026-09-14) — professional quality tiers (Step 18/19).
        //
        // x264 CRF is the right knob for screen content: static UI stays
        // near-lossless and cheap, motion gets its bitrate automatically.
        // The baselines were chosen so that:
        //   • VeryHigh reproduces EXACTLY the pre-Phase-4 CRFs (14/13/12/12
        //     per resolution) — the quality-first setting the app shipped
        //     with since 2026-09-04 remains one click away;
        //   • High (default) keeps ERP UI text, grid lines and icons
        //     sharp while cutting file size substantially — the
        //     production default for demos and support videos;
        //   • Standard is the smallest tier that still reads cleanly for
        //     long recordings and uploads.
        // Titles are UI strings (same pattern as the quality option
        // titles), not localised resources.
        public static IReadOnlyList<ScreenRecordQualityTierOption> QualityTiers { get; } =
        [
            new() { Tier = ScreenRecordQualityTier.Standard, Title = "Standard",   CrfBase = 22 },
            new() { Tier = ScreenRecordQualityTier.High,     Title = "High",       CrfBase = 18 },
            new() { Tier = ScreenRecordQualityTier.VeryHigh, Title = "Very High",  CrfBase = 13 }
        ];

        public static readonly int[] AllowedFps = [30, 40, 50, 60];

        // 2026-09-04 — default raised to 4K/60 on the user's explicit
        // quality-first instruction AND the encoding math behind it:
        //
        //   1. YOUTUBE CODEC LADDER. Every upload is re-encoded by
        //      YouTube. 1080p material lands on the low-bitrate AVC1
        //      ladder (≈2–6 Mbps), which is where fine ERP text and grid
        //      lines smear ("looks like 3gp" after upload). ≥1440p
        //      material is assigned the VP9/AV1 ladder at several times
        //      the bitrate; 2160p60 sits at the very top. Uploading the
        //      SAME window content as 3840×2160@60 is demonstrably
        //      sharper on YouTube than uploading it at native 1080p.
        //   2. COST IS ENCODER-SIDE, NOT UI-SIDE. The GDI blit still
        //      captures at the window's native size; the scale to 2160p
        //      happens inside ffmpeg's lanczos filter on its own thread.
        //      A weak CPU slows ENCODE (fewer frames land), which the
        //      one-frame-per-capture pipe absorbs; the save-time
        //      -itsscale re-time (WindowScreenRecorder.RetimeScale +
        //      FfmpegMp4Composer.RetimeCopyAsync) then stretches the file
        //      back to the real elapsed duration, so a 3-minute recording
        //      is always 3 minutes long regardless of encoder speed.
        //   3. EVERY SETTING REMAINS AVAILABLE — 720p→2K@30 in two
        //      clicks for users on small machines.
        public ScreenRecordQuality Quality { get; set; } = ScreenRecordQuality.Uhd4K;
        public int Fps { get; set; } = 60;
        public bool CaptureLiveAudio { get; set; } = true;
        public bool CaptureMicrophone { get; set; } = true;
        public int JpegQuality { get; set; } = 98;

        /// <summary>
        /// PHASE 4 — encoder quality tier (CRF baseline). Default
        /// <see cref="ScreenRecordQualityTier.High"/>: sharp enough for
        /// ERP UI text at any resolution while keeping file size
        /// reasonable. <see cref="ScreenRecordQualityTier.VeryHigh"/>
        /// reproduces the pre-Phase-4 CRFs exactly for users who want
        /// the near-lossless setting.
        /// </summary>
        public ScreenRecordQualityTier QualityTier { get; set; } = ScreenRecordQualityTier.High;

        /// <summary>
        /// LEGACY in the current GDI + raw-video pipeline: captured frames
        /// are the window's physical pixels and the output scale (incl.
        /// the 2160p upscale that earns YouTube's top codec ladder) is
        /// ffmpeg's lanczos filter at encode time, so this flag no longer
        /// changes recording output. It only persists so old configs keep
        /// round-tripping. <see cref="CaptureSize"/> below shows the
        /// historical bargain; do not wire it back in without profiling.
        /// </summary>
        public bool Supersample { get; set; } = true;

        /// <summary>
        /// Draw a synthetic cursor dot into each frame. The OS cursor is not
        /// part of a RenderTargetBitmap capture, so this is the only way to
        /// show it — but it costs a per-frame CPU loop over the pixel buffer
        /// and it is a DOT, not the real pointer shape. Off by default now
        /// that quality is the priority.
        /// </summary>
        public bool DrawCursor { get; set; }

        /// <summary>
        /// When true, frames are ALSO piped into a live H.264 encoder during
        /// recording (FfmpegLivePipe) as a HUD "live encode" indicator.
        /// Defaults to FALSE: the final MP4 is always composed from the saved
        /// JPEG frames, so this second, concurrent encoder burns CPU for a
        /// preview that is discarded on stop — a direct cause of recording
        /// lag. Leave off unless live-preview output is actually wanted.
        /// </summary>
        public bool LivePreviewEncode { get; set; }

        public (int Width, int Height) OutputSize
        {
            get
            {
                var p = Qualities.FirstOrDefault(q => q.Quality == Quality) ?? Qualities[1];
                return (p.Width, p.Height);
            }
        }

        /// <summary>
        /// Capture resolution. Default is the native output size (1:1) —
        /// see <see cref="Supersample"/> for the optional oversampled path.
        /// Every output size is an even 16:9 frame (YouTube default).
        /// </summary>
        public (int Width, int Height) CaptureSize => Supersample
            ? Quality switch
            {
                ScreenRecordQuality.Hd720 => (1920, 1080),
                ScreenRecordQuality.FullHd => (2560, 1440),
                ScreenRecordQuality.Qhd2K => (3200, 1800),
                ScreenRecordQuality.Uhd4K => (3840, 2160),
                _ => (1920, 1080)
            }
            : OutputSize;

        // 2026-09-02 — quality restored on explicit instruction ("no matter
        // the size ... just screen recording should be crispy clear").
        //
        // These were dropped to 84-88 to keep the JPEG encoder ahead of the
        // capture loop. That was treating a symptom: the real bottleneck is
        // the UI-thread RenderTargetBitmap capture, not JPEG. Lowering
        // quality cost sharpness on exactly the content screen recording is
        // for -- small text and thin UI strokes, which are what JPEG
        // degrades first -- while barely moving the frame budget.
        //
        // Encoding runs on the thread pool with a 24-deep queue, so the
        // extra milliseconds are absorbed there rather than on the UI thread.
        public int JpegQualityFor => Quality switch
        {
            ScreenRecordQuality.Hd720 => 95,
            ScreenRecordQuality.FullHd => 96,
            ScreenRecordQuality.Qhd2K => 97,
            ScreenRecordQuality.Uhd4K => 98,
            _ => 96
        };

        /// <summary>
        /// x264 CRF for the live raw-video encode.
        ///
        /// PHASE 4 (2026-09-14): tier × resolution matrix. The tier sets
        /// the CRF baseline; the resolution applies a small offset so
        /// higher resolutions keep the same visual sharpness-per-pixel
        /// the single-value table tuned for (higher resolutions were
        /// always encoded one CRF step tighter).
        ///
        ///   Standard : 23 / 22 / 21 / 21
        ///   High     : 19 / 18 / 17 / 17   (default)
        ///   VeryHigh : 14 / 13 / 12 / 12   (== pre-Phase-4 values exactly)
        ///
        /// (columns: 720p / 1080p / 2K / 4K). CRF stays inside the pipe's
        /// documented [10, 28] range for every combination.
        /// </summary>
        public int EncodeCrf
        {
            get
            {
                int baseCrf = QualityTiers.FirstOrDefault(t => t.Tier == QualityTier)?.CrfBase
                    ?? ScreenRecordQualityTierOption.DefaultBaseCrf;
                int offset = Quality switch
                {
                    ScreenRecordQuality.Hd720 => +1,
                    ScreenRecordQuality.FullHd => 0,
                    _ => -1   // 2K and 4K
                };
                return Math.Clamp(baseCrf + offset, 10, 28);
            }
        }

        public string Summary
        {
            get
            {
                var (w, h) = OutputSize;
                string tier = QualityTiers.FirstOrDefault(t => t.Tier == QualityTier)?.Title ?? "";
                string audio = CaptureLiveAudio || CaptureMicrophone
                    ? (CaptureLiveAudio && CaptureMicrophone
                        ? " + internal+mic"
                        : CaptureLiveAudio ? " + internal audio" : " + mic")
                    : "";
                return $"{w}×{h} @ {Fps} fps · {tier} CRF{EncodeCrf}{audio}";
            }
        }

        public ScreenRecordSettings Clone() => new()
        {
            Quality = Quality,
            QualityTier = QualityTier,
            Fps = Fps,
            CaptureLiveAudio = CaptureLiveAudio,
            CaptureMicrophone = CaptureMicrophone,
            JpegQuality = JpegQuality,
            Supersample = Supersample,
            DrawCursor = DrawCursor,
            LivePreviewEncode = LivePreviewEncode
        };

        public ScreenRecordSettings Clamp()
        {
            if (Qualities.All(q => q.Quality != Quality))
                Quality = ScreenRecordQuality.Uhd4K;

            if (QualityTiers.All(t => t.Tier != QualityTier))
                QualityTier = ScreenRecordQualityTier.High;

            if (Array.IndexOf(AllowedFps, Fps) < 0)
            {
                Fps = Fps <= 30 ? 30
                    : Fps <= 40 ? 40
                    : Fps <= 50 ? 50
                    : 60;
            }

            JpegQuality = Math.Clamp(JpegQuality, 50, 100);
            return this;
        }
    }
}
