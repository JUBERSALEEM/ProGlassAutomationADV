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

    public sealed class ScreenRecordQualityOption
    {
        public ScreenRecordQuality Quality { get; init; }
        public string Title { get; init; } = "";
        public int Width { get; init; }
        public int Height { get; init; }
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

        public static readonly int[] AllowedFps = [30, 40, 50, 60];

        public ScreenRecordQuality Quality { get; set; } = ScreenRecordQuality.FullHd;
        public int Fps { get; set; } = 30;
        public bool CaptureLiveAudio { get; set; } = true;
        public bool CaptureMicrophone { get; set; } = true;
        public int JpegQuality { get; set; } = 98;

        /// <summary>
        /// When true, frames are captured at a supersampled size and
        /// ffmpeg lanczos-downscales them to the output for extra crispness.
        /// Defaults to FALSE: supersampling costs up to ~1.8× the pixels per
        /// frame (RenderTargetBitmap + JPEG + H.264), which is the dominant
        /// source of recording lag. 1:1 capture with the final lanczos +
        /// unsharp chain recovers most of the sharpness at a fraction of the
        /// CPU. Power users recording 4K on a fast machine can re-enable it.
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

        public int EncodeCrf => Quality switch
        {
            // Lower CRF = higher quality. Default x264 is ~23; these are
            // already aggressive, nudged one step further for cleaner
            // screen text. Final encode runs once at STOP, so the extra
            // bitrate costs disk, not recording latency.
            // Lowered further: size is explicitly not a constraint, and
            // CRF 12-14 is visually lossless for screen content.
            ScreenRecordQuality.Hd720 => 14,
            ScreenRecordQuality.FullHd => 13,
            ScreenRecordQuality.Qhd2K => 12,
            ScreenRecordQuality.Uhd4K => 12,
            _ => 13
        };

        public string Summary
        {
            get
            {
                var (w, h) = OutputSize;
                string audio = CaptureLiveAudio || CaptureMicrophone
                    ? (CaptureLiveAudio && CaptureMicrophone
                        ? " + internal+mic"
                        : CaptureLiveAudio ? " + internal audio" : " + mic")
                    : "";
                return $"{w}×{h} @ {Fps} fps{audio}";
            }
        }

        public ScreenRecordSettings Clone() => new()
        {
            Quality = Quality,
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
                Quality = ScreenRecordQuality.FullHd;

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
