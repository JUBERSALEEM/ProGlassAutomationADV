using ProGlassAutomation.Agent.Demo.Video;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ProGlassAutomation.Agent.Demo.Recording
{
    public sealed class RecordedFrame
    {
        public string Path { get; init; } = "";
        public TimeSpan Timestamp { get; init; }
        public int CursorX { get; init; }
        public int CursorY { get; init; }
    }

    /// <summary>
    /// Captures only the running main window (not the desktop) at a
    /// chosen quality and frame rate. Encode and disk I/O run off the
    /// UI thread. A full dispatcher is never pumped — that was freezing
    /// the demo walk.
    /// </summary>
    public sealed class WindowScreenRecorder : IDisposable
    {
        private readonly List<RecordedFrame> _frames = new();
        private readonly object _framesGate = new();
        private readonly WasapiLoopbackRecorder _audio = new();
        private readonly WasapiMicRecorder _mic = new();
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private DateTime _started;
        private DateTime _pauseStarted;
        private TimeSpan _pausedAccumulated;
        private ScreenRecordSettings _settings = new();
        private FfmpegLivePipe? _pipe;
        private string? _liveVideoPath;
        private volatile bool _paused;

        // ── OFF-UI-THREAD CAPTURE (2026-09-02) ───────────────────────
        // GDI PrintWindow against the app HWND, driven from a background
        // thread, encoded once as raw BGRA. Replaces the UI-thread
        // RenderTargetBitmap path that caused the stutter and could not
        // see popups. See GdiWindowCapture for the full rationale.
        private FfmpegRawPipe? _rawPipe;
        private string? _rawVideoPath;
        private IntPtr _targetHwnd = IntPtr.Zero;
        private volatile bool _rawActive;

        /// <summary>True when the fast off-thread pipeline produced the video.</summary>
        public bool RawCaptureActive => _rawActive;

        /// <summary>
        /// Non-null when capture could not start at all. The service reports
        /// this instead of pretending a recording is running — a HUD stuck
        /// on "REC" with no encoder behind it was part of why STOP appeared
        /// dead.
        /// </summary>
        public string? FatalStartError { get; private set; }

        /// <summary>
        /// PHASE 3 — non-null when the encoder DIED mid-recording (as
        /// opposed to a start-time failure, which uses FatalStartError).
        /// The save path must refuse to report SUCCESS while this is set:
        /// a silent partial "SAVED" is exactly the failure the recorder
        /// must no longer produce. The text is user-facing; technical
        /// detail stays in the app log.
        /// </summary>
        public string? EncoderFailure { get; private set; }

        /// <summary>
        /// PHASE 4 — non-null when the DISK ran out of space mid-
        /// recording (the in-loop [RecStorage] watcher saw the free
        /// space drop below the recorder's minimum floor). Same
        /// contract as <see cref="EncoderFailure"/>: the save path must
        /// refuse SUCCESS while this is set, with the professional
        /// message, files retained. Technical detail stays in the log.
        /// </summary>
        public string? StorageFailure { get; private set; }

        /// <summary>
        /// PHASE 3 — raised exactly once when the capture loop ends for a
        /// FATAL reason (encoder death, persistent blank output). The
        /// service stops + reports the session immediately so the HUD
        /// never keeps showing REC over a dead pipeline.
        /// </summary>
        public event Action? CaptureLoopFatal;

        private bool _loopFatalRaised;

        /// <summary>
        /// Raises <see cref="CaptureLoopFatal"/> once, from the capture
        /// loop's thread. Handlers must not block (the service schedules
        /// its teardown off-thread).
        /// </summary>
        private void NotifyCaptureLoopFatal()
        {
            if (_loopFatalRaised) return;
            _loopFatalRaised = true;
            try { CaptureLoopFatal?.Invoke(); }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
        }

        /// <summary>Final MP4 written directly by the raw pipeline, if any.</summary>
        public string? RawVideoPath => _rawActive ? _rawVideoPath : null;

        public IReadOnlyList<RecordedFrame> Frames
        {
            get { lock (_framesGate) return _frames.ToArray(); }
        }

        public bool IsRecording => _loop != null && !_loop.IsCompleted;
        public DateTime StartedUtc => _started;
        public TimeSpan Duration { get; private set; }

        /// <summary>
        /// Nominal frame rate the raw H.264 pipe was started with (the
        /// value baked into ffmpeg's <c>-framerate</c>). The encoded file
        /// therefore CLAIMS a duration of <see cref="EncodedFrames"/> /
        /// this value. Because captures happen at the real (throttled)
        /// wall-clock rate — not this nominal rate — that claimed duration
        /// is almost always shorter than reality, which is exactly why a
        /// 3-minute recording used to play back as ~1 minute. The save
        /// path uses this together with <see cref="EncodedFrames"/> and
        /// <see cref="Duration"/> to re-time the file to real duration.
        /// Zero until the pipe has started.
        /// </summary>
        public int CaptureNominalFps { get; private set; }

        /// <summary>
        /// Timestamp-rescale factor that stretches the raw capture's
        /// claimed duration (<see cref="EncodedFrames"/> / <see
        /// cref="CaptureNominalFps"/>) onto the real elapsed wall-clock
        /// time (<see cref="Duration"/>). Fed to ffmpeg's <c>-itsscale</c>
        /// during the stream-copy save, so the final MP4's duration equals
        /// the real recording length no matter how many frames the machine
        /// actually managed to encode. Returns 1.0 (no change) when the
        /// inputs are not yet meaningful. Clamped to a sane range so a
        /// pathological frame count can never produce an absurd duration.
        /// </summary>
        public double RetimeScale =>
            ComputeRetimeScale(EncodedFrames, CaptureNominalFps, Duration.TotalSeconds);

        /// <summary>
        /// Pure, side-effect-free re-time factor: how much the raw
        /// capture's claimed duration (<paramref name="encodedFrames"/> /
        /// <paramref name="nominalFps"/>) must be stretched to match the
        /// real elapsed <paramref name="realSeconds"/>. Returns 1.0 (no
        /// change) whenever the inputs are not yet meaningful, and clamps
        /// the result to [0.02, <see cref="MaxRetimeScale"/>] so a
        /// pathological frame count can never yield an absurd duration.
        /// PHASE 3: the upper bound was 50×, which silently under-
        /// corrected the 5-minutes-as-5-seconds case (×60 needed); it is
        /// now 3600×, and the save path MEASURES the final MP4 and
        /// refuses success when the duration is materially wrong.
        /// Extracted static so the timing contract behind the "3 minutes
        /// plays as 1 minute" fix is unit testable without a live capture.
        /// </summary>
        /// <summary>
        /// Upper bound for the re-time correction factor (PHASE 3, 2026-09-14).
        ///
        /// History: this was 50×. On a machine sustaining ~1 fps at the
        /// shipped 4K/60, a 5-minute recording produced a 5-second raw file
        /// needing ×60 — the old clamp silently delivered 5 × 50 = 250 s
        /// instead of 300 s. The clamp's original purpose is to keep
        /// pathological inputs (e.g. one frame over a long wall time) from
        /// producing absurd output. Phase 3 keeps that purpose but raises
        /// the bound to 3600×, which fully corrects any recording up to an
        /// hour as long as a handful of frames were written, AND the save
        /// path now MEASURES the resulting MP4 and refuses to report
        /// SUCCESS when the final duration is materially wrong (see
        /// UserScreenRecordService's duration validation). A 1-frame file
        /// can no longer be sold as a 5-minute video: the encoder-death
        /// check (EncoderFailure) fires first and the save is refused.
        /// </summary>
        public const double MaxRetimeScale = 3600.0;

        public static double ComputeRetimeScale(
            long encodedFrames, int nominalFps, double realSeconds)
        {
            if (encodedFrames <= 0 || nominalFps <= 0 || realSeconds <= 0.05)
                return 1.0;

            double claimed = encodedFrames / (double)nominalFps;
            if (claimed <= 0.001) return 1.0;

            return Math.Clamp(realSeconds / claimed, 0.02, MaxRetimeScale);
        }

        // ── ADAPTIVE THROTTLE CONTRACT (2026-09-16) ────────────────────────
        // The capture loop measures what each frame costs and gives time back
        // to the UI thread when capture is expensive. That protection is
        // correct and stays. What was WRONG is that it was a RATCHET:
        // effectiveFps could only ever be halved, never raised, so one slow
        // stretch - a GC pause, a PrintWindow fallback, one busy moment -
        // locked the whole remainder of the recording at the floor.
        //
        // That is the mechanism behind "a 6-minute 4K/60 recording produced
        // 33 MB". A session that dipped once spent the rest of its life at
        // 8 fps: ~2880 real frames instead of ~21600. Because the save path
        // retimes to the true elapsed duration (ComputeRetimeScale) the file
        // came out the right LENGTH, so the mandatory duration validation
        // passed it and it was reported as a success. File size followed the
        // frame count, not the CRF.
        //
        // Pure and static so the entire policy is unit-testable with no
        // window, no dispatcher and no ffmpeg.

        /// <summary>Fraction of one frame interval capture may spend before
        /// the frame counts as an overrun. 0.5 leaves the UI thread a real
        /// gap on every frame.</summary>
        public const double UiBudgetFraction = 0.5;

        /// <summary>A frame at or below this fraction of the interval counts
        /// toward RECOVERY. Deliberately tighter than
        /// <see cref="UiBudgetFraction"/> so a machine sitting right at the
        /// edge does not oscillate between two rates.</summary>
        public const double RecoveryBudgetFraction = 0.35;

        /// <summary>Floor for the adaptive throttle. Below this a recording is
        /// a slideshow whatever the requested rate claimed.</summary>
        public const int MinThrottledFps = 8;

        /// <summary>Consecutive overruns before the rate is halved.</summary>
        public const int SlowStreakToHalve = 5;

        /// <summary>Consecutive COMFORTABLE frames before the rate steps back
        /// up. Much larger than <see cref="SlowStreakToHalve"/> on purpose:
        /// back-off must stay faster than recovery or a marginal machine
        /// oscillates, which looks worse to a viewer than simply holding a
        /// lower rate. At the 8 fps floor this is ~11 s of clean capture
        /// before the first step up.</summary>
        public const int FastStreakToRecover = 90;

        /// <summary>Back-off step: halve, never below the floor.</summary>
        public static int StepThrottleDown(int currentFps) =>
            Math.Max(MinThrottledFps, currentFps / 2);

        /// <summary>
        /// Recovery step: one modest increase toward the requested rate and
        /// never past it. +50% rounded, so 8 -&gt; 12 -&gt; 18 -&gt; 27 -&gt; 41 -&gt; 60,
        /// with a floor of +1 so even a rate of 1 can climb.
        /// </summary>
        public static int StepThrottleUp(int currentFps, int requestedFps)
        {
            if (requestedFps <= 0) return Math.Max(1, currentFps);
            if (currentFps >= requestedFps) return requestedFps;
            // AwayFromZero is stated explicitly rather than left to
            // Math.Round's default (banker's / ToEven), which would make
            // 27 * 1.5 = 40.5 round DOWN to 40 while 12 * 1.5 = 18 rounds
            // exact - an inconsistent climb that is hard to reason about.
            int next = Math.Max(currentFps + 1,
                (int)Math.Round(currentFps * 1.5, MidpointRounding.AwayFromZero));
            return Math.Min(requestedFps, next);
        }

        // ── DELIVERED-FIDELITY VERDICT (PHASE 7, 2026-09-16) ────────────────
        // The save path already validated DURATION mandatorily, and duration
        // was always correct - the -itsscale retime guarantees it by
        // construction. That is precisely why a 4K/60 session that collapsed
        // to 8 real fps still PASSED and was reported as a success: it was
        // six minutes long, just not smooth, and nothing measured smoothness.
        //
        // These classify what ffprobe MEASURED in the delivered file against
        // what was requested. Unknown is a distinct outcome from Pass: when
        // ffprobe is missing a recording must not be silently promoted to a
        // pass, and must not be failed either.

        /// <summary>Verdict on the delivered file versus the request.</summary>
        public enum RecordingFidelity
        {
            /// <summary>Measured and within tolerance.</summary>
            Pass = 0,
            /// <summary>Measured and materially below the request. The file is
            /// still saved - duration validation owns the SAVED decision - but
            /// it is no longer reported as a clean success.</summary>
            Degraded = 1,
            /// <summary>Could not be measured (ffprobe absent, stream unreadable).
            /// Never treated as a pass.</summary>
            Unknown = 2
        }

        /// <summary>Fraction of the requested frame rate that still passes.
        /// 0.95 accepts 57-60 fps for a 60 fps request: container timestamp
        /// rounding and the final partial frame sit inside that band, while a
        /// throttled capture (30, 15, 8 fps) sits far outside it. Deliberately
        /// not tighter - failing a recording over 59.94 vs 60 would be noise.</summary>
        public const double FpsPassFraction = 0.95;

        /// <summary>Classifies a measured average frame rate against the request.</summary>
        public static RecordingFidelity ClassifyFrameRate(int requestedFps, double? actualFps)
        {
            if (requestedFps <= 0) return RecordingFidelity.Unknown;
            if (!actualFps.HasValue || actualFps.Value <= 0) return RecordingFidelity.Unknown;
            return actualFps.Value >= requestedFps * FpsPassFraction
                ? RecordingFidelity.Pass
                : RecordingFidelity.Degraded;
        }

        /// <summary>Classifies measured geometry against the requested output
        /// size. Exact match: a 1920x1080 file sold as 4K is a real defect,
        /// not a rounding difference.</summary>
        public static RecordingFidelity ClassifyResolution(
            int requestedWidth, int requestedHeight, int? actualWidth, int? actualHeight)
        {
            if (requestedWidth <= 0 || requestedHeight <= 0) return RecordingFidelity.Unknown;
            if (!actualWidth.HasValue || !actualHeight.HasValue) return RecordingFidelity.Unknown;
            return actualWidth.Value == requestedWidth && actualHeight.Value == requestedHeight
                ? RecordingFidelity.Pass
                : RecordingFidelity.Degraded;
        }

        /// <summary>Worst of two verdicts, treating Unknown as weaker than Pass
        /// but stronger than Degraded - so one measured failure is never
        /// masked by one unmeasurable check.</summary>
        public static RecordingFidelity WorstFidelity(
            RecordingFidelity a, RecordingFidelity b)
        {
            if (a == RecordingFidelity.Degraded || b == RecordingFidelity.Degraded)
                return RecordingFidelity.Degraded;
            if (a == RecordingFidelity.Unknown || b == RecordingFidelity.Unknown)
                return RecordingFidelity.Unknown;
            return RecordingFidelity.Pass;
        }

        /// <summary>Average bitrate of the delivered file in Mbps, from its
        /// size and measured duration - the number that explains a suspiciously
        /// small file. Null when either input is unusable, so a missing
        /// measurement can never be printed as 0.0 Mbps.</summary>
        public static double? AverageBitrateMbps(long fileSizeBytes, double durationSeconds)
        {
            if (fileSizeBytes <= 0 || durationSeconds <= 0) return null;
            return fileSizeBytes * 8.0 / durationSeconds / 1_000_000.0;
        }

        // Incremented from BOTH the capture loop and thread-pool encode
        // tasks. Plain ++ on an int is a read-modify-write and loses counts
        // under contention, which made the HUD's dropped/encoded figures
        // quietly wrong. Interlocked + Volatile.Read is the correct pair.
        private long _droppedFrames;
        private long _encodedFrames;

        public long DroppedFrames => Volatile.Read(ref _droppedFrames);
        public long EncodedFrames => Volatile.Read(ref _encodedFrames);

        // ── PHASE-2 DIAGNOSTICS (2026-09-14) ──────────────────────────
        // Captured-vs-written split, the effective fps the loop ended at,
        // and the audio stream durations — so the stop summary can say
        // exactly how many frames the machine actually produced and
        // whether the audio could have truncated the final file.
        private long _capturedFrames;

        public long CapturedFrames => Volatile.Read(ref _capturedFrames);

        private long _throttleDowns;
        private long _throttleUps;

        // Phase 5 (bottleneck attribution) — per-stage cost accumulators.
        // Written only from the capture loop thread and read after the loop
        // has ended, so they need no interlocking.
        private double _blitMsTotal;
        private double _copyMsTotal;
        private double _writeMsTotal;
        private double _writeMsMax;
        private long _writesBlocked;
        private long _stageSamples;

        /// <summary>How many times the adaptive throttle HALVED the rate and
        /// how many times it stepped back up. Downs far exceeding ups means
        /// the recording spent most of its life below the requested rate -
        /// the number that explains a small file far better than CRF does.</summary>
        public long ThrottleDowns => Volatile.Read(ref _throttleDowns);
        public long ThrottleUps => Volatile.Read(ref _throttleUps);

        // ── PHASE 5 STAGE ATTRIBUTION ─────────────────────────────────────────────────
        /// <summary>Frames whose stage costs were sampled.</summary>
        public long StageSamples => _stageSamples;

        /// <summary>Average BitBlt/PrintWindow cost per frame, ms.</summary>
        public double AvgBlitMs => _stageSamples > 0 ? _blitMsTotal / _stageSamples : 0;

        /// <summary>Average Marshal.Copy cost per frame, ms — memory bandwidth.</summary>
        public double AvgCopyMs => _stageSamples > 0 ? _copyMsTotal / _stageSamples : 0;

        /// <summary>
        /// Average synchronous pipe-write cost per frame, ms. This is encoder
        /// backpressure: with no buffering of our own, the loop stands here
        /// waiting for x264/NVENC to accept the frame.
        /// </summary>
        public double AvgWriteMs => _stageSamples > 0 ? _writeMsTotal / _stageSamples : 0;

        /// <summary>Worst single write, ms — the stall a user would feel.</summary>
        public double MaxWriteMs => _writeMsMax;

        /// <summary>Writes that cost at least one whole frame interval.</summary>
        public long WritesBlocked => _writesBlocked;

        /// <summary>
        /// Wall-clock per frame minus the three measured stages: scheduling,
        /// the deliberate inter-frame wait, JPEG persistence and loop overhead.
        /// Deriving it rather than timing it keeps the hot path cheap, and the
        /// four terms still sum to the real frame period.
        /// </summary>
        public double AvgOtherMs
        {
            get
            {
                if (_stageSamples <= 0) return 0;
                double wallPerFrame = Duration.TotalMilliseconds / _stageSamples;
                return Math.Max(0, wallPerFrame - (AvgBlitMs + AvgCopyMs + AvgWriteMs));
            }
        }

        /// <summary>The stage that actually consumed the frame budget.</summary>
        public string DominantStage =>
            DominantCaptureStage(AvgBlitMs, AvgCopyMs, AvgWriteMs, AvgOtherMs);

        /// <summary>
        /// Phase 5 — attributes the frame budget to a stage. PURE, so it is
        /// unit-testable without a display, a GPU or ffmpeg.
        ///
        /// Returns "distributed" unless one stage owns at least half the
        /// budget: naming a single bottleneck when the cost is spread evenly
        /// would send the next engineering phase after the wrong thing. Ties
        /// break toward the most actionable stage (encoder write first).
        /// </summary>
        public static string DominantCaptureStage(
            double blitMs, double copyMs, double writeMs, double otherMs)
        {
            double total = blitMs + copyMs + writeMs + otherMs;
            if (total <= 0) return "no-data";
            double max = Math.Max(Math.Max(blitMs, copyMs), Math.Max(writeMs, otherMs));
            if (max / total < 0.5) return "distributed";
            if (max == writeMs) return "encoder-write";
            if (max == blitMs) return "gdi-blit";
            if (max == copyMs) return "marshal-copy";
            return "loop-overhead";
        }

        public int FinalEffectiveFps { get; private set; }
        public TimeSpan? LoopbackAudioSeconds { get; private set; }
        public TimeSpan? MicAudioSeconds { get; private set; }
        public string? LiveAudioPath => _audio.Path;
        public string? MicAudioPath => _mic.Path;
        public string? AudioError =>
            string.Join(" · ", new[] { _audio.Error, _mic.Error }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        public bool LiveAudioStarted { get; private set; }
        public bool MicAudioStarted { get; private set; }
        public bool IsPaused => _paused;
        public bool LiveEncodeActive => _pipe != null && _pipe.IsOpen;
        public int LiveFramesPiped => _pipe?.FramesPiped ?? 0;
        public long LiveBytesPiped => _pipe?.BytesPiped ?? 0;

        // PHASE 4B — the ACTUAL encoder parameters the raw pipe started
        // with (post-clamp), so the service's [RecQuality] line can
        // attribute the measured final bitrate to a concrete config.
        public string EncodePreset => _rawPipe?.EncodePreset ?? "";
        public int EncodeCrfUsed => _rawPipe?.EncodeCrfUsed ?? 0;

        // PHASE 5 — concrete encoder identity for [RecQuality] (the pipe
        // owns the values; the recorder passes them through).
        public string EncodeName => _rawPipe?.EncodeName ?? "libx264";

        // ── CRISP-DEMO PASS proxies (Phases 4/7/8/10) ───────────────────────────────
        // These let the save-time quality report quote what was ACTUALLY
        // encoded instead of restating the requested settings.
        public string EncodeCodec => _rawPipe?.CodecName ?? "H.264";
        public string EncodePixelFormat => _rawPipe?.PixelFormat ?? "yuv420p";
        public int EncodeGop => _rawPipe?.Gop ?? 0;
        public int CaptureWidthUsed => _rawPipe?.CaptureWidth ?? 0;
        public int CaptureHeightUsed => _rawPipe?.CaptureHeight ?? 0;
        public int OutputWidthUsed => _rawPipe?.OutputWidth ?? 0;
        public int OutputHeightUsed => _rawPipe?.OutputHeight ?? 0;

        /// <summary>
        /// Phase 10 — the honest capture→output description. Distinguishes a
        /// physical capture resolution from an encoded output resolution so a
        /// lanczos-upscaled window can never be reported as a native 4K capture.
        /// </summary>
        public string ResolutionProvenance =>
            _rawPipe?.ResolutionProvenance ?? "resolution provenance unknown";

        public bool WasUpscaled => _rawPipe?.WasUpscaled ?? false;

        /// <summary>Non-null when a requested setting had to be reduced (Phase 7).</summary>
        public string? ConfigurationFallback => _rawPipe?.ConfigurationFallback;
        public bool HardwareEncoder => _rawPipe?.HardwareEncoder ?? false;
        public int CaptureNominalFpsUsed => _rawPipe?.NominalFps ?? 0;
        public int CaptureWidth { get; private set; }
        public int CaptureHeight { get; private set; }
        public string? LiveVideoPath => _liveVideoPath;
        public string? LiveEncodeError => _pipe?.Error;
        public int EncodePercent { get; set; }
        public string EncodeStage { get; set; } = "";
        public ScreenRecordSettings Settings => _settings;

        public TimeSpan Elapsed
        {
            get
            {
                var now = DateTime.UtcNow;
                var paused = _pausedAccumulated;
                if (_paused && _pauseStarted != default)
                    paused += now - _pauseStarted;
                var raw = now - _started - paused;
                return raw < TimeSpan.Zero ? TimeSpan.Zero : raw;
            }
        }

        public void Start(string framesFolder, int fps) =>
            Start(framesFolder, (new ScreenRecordSettings { Fps = fps }).Clamp());

        /// <summary>
        /// PHASE 5: start with a RESOLVED encoder selection (the service
        /// resolves + logs [RecEncoderSelect] before calling this). The
        /// 2-argument overload below resolves the same way for callers
        /// that do not go through the service (Demo Center).
        /// </summary>
        public void Start(
            string framesFolder, ScreenRecordSettings settings,
            ProGlassAutomation.Agent.Demo.Video.VideoEncoderSelection encoder,
            bool encoderFallback, string? encoderFallbackReason)
            => StartCore(framesFolder, settings, encoder, encoderFallback, encoderFallbackReason);

        public void Start(string framesFolder, ScreenRecordSettings settings)
        {
            var s = (settings ?? new ScreenRecordSettings()).Clone().Clamp();
            var cap = ProGlassAutomation.Agent.Demo.Video.EncoderProbe.GetSnapshot();
            var res = ProGlassAutomation.Agent.Demo.Video.VideoEncoder.Resolve(
                s.Encoder, cap.NvencUsable, cap.QsvUsable);
            StartCore(framesFolder, s, res.Selection, res.Fallback, res.Reason);
        }

        private ProGlassAutomation.Agent.Demo.Video.VideoEncoderSelection _encoderSelection =
            ProGlassAutomation.Agent.Demo.Video.VideoEncoderSelection.Libx264;

        // PHASE 5 — how the encoder was resolved (Step 30: a hardware
        // init failure must be visible, never silently claimed as HW).
        public bool EncoderUsedFallback { get; private set; }
        public string? EncoderFallbackReason { get; private set; }

        private void StartCore(
            string framesFolder, ScreenRecordSettings settings,
            ProGlassAutomation.Agent.Demo.Video.VideoEncoderSelection encoder,
            bool encoderFallback, string? encoderFallbackReason)
        {
            try { _cts?.Cancel(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            settings ??= new ScreenRecordSettings();
            _settings = settings.Clone().Clamp();
            _encoderSelection = encoder ?? ProGlassAutomation.Agent.Demo.Video.VideoEncoderSelection.Libx264;
            EncoderUsedFallback = encoderFallback;
            EncoderFallbackReason = encoderFallbackReason;
            Directory.CreateDirectory(framesFolder);
            lock (_framesGate) _frames.Clear();
            Interlocked.Exchange(ref _droppedFrames, 0);
            Interlocked.Exchange(ref _encodedFrames, 0);
            Interlocked.Exchange(ref _capturedFrames, 0);
            Interlocked.Exchange(ref _throttleDowns, 0);
            Interlocked.Exchange(ref _throttleUps, 0);
            _blitMsTotal = 0; _copyMsTotal = 0; _writeMsTotal = 0;
            _writeMsMax = 0; _writesBlocked = 0; _stageSamples = 0;
            FinalEffectiveFps = Math.Max(1, _settings.Fps);
            LoopbackAudioSeconds = null;
            MicAudioSeconds = null;
            Duration = TimeSpan.Zero;
            EncodePercent = 0;
            EncodeStage = "Recording";
            _paused = false;
            _pausedAccumulated = TimeSpan.Zero;
            _pauseStarted = default;
            _started = DateTime.UtcNow;
            _cts = new CancellationTokenSource();

            LiveAudioStarted = false;
            MicAudioStarted = false;
            string parent = Directory.GetParent(framesFolder)?.FullName ?? framesFolder;

            // ── Live H.264 preview pipe (lag fix) ─────────────────────
            // The final MP4 is always composed from the saved JPEG frames
            // (correct timing), so a concurrent live encoder is pure CPU
            // overhead — it re-encodes every frame in real time and its
            // output is discarded on stop. It now defaults OFF
            // (LivePreviewEncode). Turning it off removes the biggest
            // background CPU consumer and directly reduces recording lag.
            try { _pipe?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            _pipe = null;
            _liveVideoPath = null;

            if (_settings.LivePreviewEncode)
            {
                _liveVideoPath = Path.Combine(parent, "live.h264.mp4");
                var (outW, outH) = _settings.OutputSize;
                _pipe = FfmpegLivePipe.TryStart(
                    _liveVideoPath, _settings.Fps, outW, outH, _settings.EncodeCrf);
            }

            if (_settings.CaptureLiveAudio)
                LiveAudioStarted = _audio.Start(System.IO.Path.Combine(parent, "live.wav"));
            if (_settings.CaptureMicrophone)
                MicAudioStarted = _mic.Start(System.IO.Path.Combine(parent, "mic.wav"));

            // Resolve the target HWND ONCE, on the UI thread. After this
            // the capture loop never touches the dispatcher again — that
            // is the whole point of the new pipeline.
            _targetHwnd = ResolveMainWindowHandle();
            FatalStartError = null;
            _rawActive = false;
            _rawVideoPath = null;
            try { _rawPipe?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            _rawPipe = null;

            var ct = _cts.Token;

            if (_targetHwnd != IntPtr.Zero)
            {
                _rawVideoPath = Path.Combine(parent, "capture.mp4");
                _loop = Task.Run(() => RawLoopAsync(framesFolder, ct));
            }
            else
            {
                FatalStartError =
                    "The application window is not available for capture.";
                AppLogService.Warning(
                    FatalStartError,
                    new Services.Logging.LogContext { Module = "Recording", Operation = "Start" });
            }
        }

        /// <summary>
        /// Stops capture and returns AS SOON AS the loop is cancelled.
        ///
        /// The old implementation blocked the caller for up to EIGHT SECONDS
        /// draining the encode queue:
        ///
        ///     var until = DateTime.UtcNow.AddSeconds(8);
        ///     while (pendingEncodes > 0 &amp;&amp; Now &lt; until) await Task.Delay(40);
        ///
        /// STOP is a user gesture. Making it wait on background disk I/O is
        /// why stopping felt frozen. Outstanding encodes are frames already
        /// captured, so they can finish on their own; the composer reads
        /// whatever landed. The drain is now bounded and off the caller's
        /// path.
        /// </summary>
        public async Task StopAsync()
        {
            _cts?.Cancel();

            // Bounded join on the capture loop only. It checks the token
            // every iteration, so this returns almost immediately.
            if (_loop != null)
            {
                try
                {
                    var loop = _loop;
                    await Task.WhenAny(loop, Task.Delay(750)).ConfigureAwait(false);
                }
                catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            }
            _loop = null;

            Duration = Elapsed;

            // Finalise the raw encoder FIRST: it holds the actual video.
            // Bounded so a wedged ffmpeg cannot hold STOP open; +faststart
            // means even a killed process leaves a playable file.
            if (_rawPipe != null)
            {
                EncodeStage = "Finalising video…";
                EncodePercent = 45;
                try
                {
                    bool ok = await _rawPipe
                        .FinishAsync(TimeSpan.FromSeconds(10))
                        .ConfigureAwait(false);

                    if (!ok) _rawActive = false;
                }
                catch (Exception ex)
                {
                    _rawActive = false;
                    AppLogService.Warning(
                        $"Video finalise failed: {ex.Message}",
                        new Services.Logging.LogContext { Module = "Recording", Operation = "StopAsync" });
                }
            }

            // PHASE-2: capture the audio stream durations (WavDuration) —
            // a short live.wav relative to wall time is the -shortest
            // truncation signal.
            try { LoopbackAudioSeconds = await _audio.StopAsync().ConfigureAwait(false); }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: loopback is optional: " + ex.Message); }
            try { MicAudioSeconds = await _mic.StopAsync().ConfigureAwait(false); }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: mic is optional: " + ex.Message); }

            if (_pipe != null)
            {
                EncodeStage = "Closing live encoder…";
                EncodePercent = 40;
                try { await _pipe.FinishAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: fallback JPEG encode still available: " + ex.Message); }
            }

            // ── PHASE-2 DIAGNOSTIC: one grep-able stop summary ─────────
            // For the exact recording that just ended: wall time,
            // captured/written/dropped/expected frames, encoder liveness +
            // exit code, what the raw file CLAIMS vs what ffprobe reports
            // about its stream, how long the audio actually ran, and the
            // retime factor the save path is about to apply.
            try
            {
                double? rawClaimed = null;
                string? rawProbe = null;
                if (_rawActive && !string.IsNullOrWhiteSpace(_rawVideoPath))
                {
                    try { rawClaimed = FfmpegMp4Composer.TryReadDurationSeconds(_rawVideoPath); }
                    catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: raw duration probe optional: " + ex.Message); }
                    try { rawProbe = FfmpegMp4Composer.TryProbeVideoSummary(_rawVideoPath); }
                    catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: raw stream probe optional: " + ex.Message); }
                }

                string? pipeError = _rawPipe?.Error;
                if (!string.IsNullOrWhiteSpace(pipeError) && pipeError.Length > 200)
                    pipeError = "…" + pipeError[^200..];

                // PHASE 4: raw file size + free disk at stop, so a
                // failure can be correlated with a filling drive.
                long rawSizeAtStop = -1;
                try
                {
                    if (_rawActive && !string.IsNullOrWhiteSpace(_rawVideoPath) && File.Exists(_rawVideoPath))
                        rawSizeAtStop = new FileInfo(_rawVideoPath).Length;
                }
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: raw size probe optional: " + ex.Message); }
                long diskFreeAtStop = FfmpegMp4Composer.FreeDiskBytes(_rawVideoPath);

                AppLogService.Info(
                    $"[RecSummary] wall={Duration.TotalSeconds:F1}s " +
                    $"rawSize={rawSizeAtStop}B diskFree={diskFreeAtStop}B " +
                    $"nominal={_settings.Fps}fps effective={FinalEffectiveFps}fps " +
                    $"captured={Volatile.Read(ref _capturedFrames)} " +
                    $"written={Volatile.Read(ref _encodedFrames)} " +
                    $"dropped={Volatile.Read(ref _droppedFrames)} " +
                    $"expected={(int)Math.Round(Duration.TotalSeconds * _settings.Fps)} " +
                    $"ffmpeg pid={_rawPipe?.ProcessId ?? -1} " +
                    $"alive={(_rawPipe?.ProcessAlive ?? false)} " +
                    $"exitCode={_rawPipe?.ExitCode?.ToString() ?? "n/a"} " +
                    $"started={_rawPipe?.ProcessStartedUtc.ToString("HH:mm:ss") ?? "n/a"} " +
                    $"stderr={(string.IsNullOrWhiteSpace(pipeError) ? "clean" : pipeError!)} " +
                    $"rawClaimed={(rawClaimed?.ToString("0.###") ?? "n/a")}s " +
                    $"rawProbe={rawProbe ?? "n/a"} " +
                    $"audio loopback={(LoopbackAudioSeconds?.TotalSeconds.ToString("0.##") ?? "off")}s " +
                    $"mic={(MicAudioSeconds?.TotalSeconds.ToString("0.##") ?? "off")}s " +
                    $"audioError={(string.IsNullOrWhiteSpace(AudioError) ? "none" : AudioError)} " +
                    $"encoderFailure={(string.IsNullOrWhiteSpace(EncoderFailure) ? "none" : EncoderFailure)} " +
                    $"storageFailure={(string.IsNullOrWhiteSpace(StorageFailure) ? "none" : StorageFailure)} " +
                    $"retime={RetimeScale:0.0000} " +
                    $"rawActive={_rawActive}",
                    new Services.Logging.LogContext { Module = "Recording", Operation = "StopSummary" });
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: summary log optional: " + ex.Message); }

            // ── PHASE 5 (Steps 5/19/20): the THREE different fps ──────
            // requested (what the user chose), captured (frames the
            // screen actually delivered) and encoded (frames that
            // reached the encoder). Plus [RecFrameHealth]: when the
            // effective rate is far below the configured rate, the
            // recording is STARVED — retime will make it the right
            // LENGTH but not the right SMOOTHNESS. That is a warning
            // condition that must be visible in the log, per Step 20:
            // < 15 fps → Warning, < 5 fps → Critical. Diagnostics only —
            // it does not fail the save (duration validation still owns
            // the SAVED decision).
            try
            {
                double durSec = Duration.TotalSeconds;
                long encodedNow = Volatile.Read(ref _encodedFrames);
                long capturedNow = Volatile.Read(ref _capturedFrames);
                long droppedNow = Volatile.Read(ref _droppedFrames);
                double effFps = durSec > 0.05 ? encodedNow / durSec : 0;
                long expectedFrames = (long)Math.Round(durSec * _settings.Fps);

                AppLogService.Info(
                    $"[RecFPS] requested={_settings.Fps} captured={capturedNow} " +
                    $"encoded={encodedNow} dropped={droppedNow} effective={effFps:0.##}",
                    new Services.Logging.LogContext { Module = "Recording", Operation = "FpsReport" });

                // Phase 5 — WHERE the frame budget actually went, measured.
                // This is also what makes the throttle's blind spot visible:
                // the throttle above only reads LastCaptureMs, so encoder
                // backpressure appears in writeAvg/writesBlocked HERE and never
                // there. A high writeAvg with a low blitAvg+copyAvg means the
                // encoder, not the capture, is the limit.
                AppLogService.Info(
                    $"[RecStageTimings] samples={_stageSamples} " +
                    $"blitAvg={AvgBlitMs:0.##}ms copyAvg={AvgCopyMs:0.##}ms " +
                    $"writeAvg={AvgWriteMs:0.##}ms otherAvg={AvgOtherMs:0.##}ms " +
                    $"writeMax={MaxWriteMs:0.##}ms writesBlocked={WritesBlocked} " +
                    $"dominant={DominantStage}",
                    new Services.Logging.LogContext { Module = "Recording", Operation = "StageTimings" });

                string severity = effFps < 5 ? "Critical" : effFps < 15 ? "Warning" : "Normal";
                bool starved = severity != "Normal";
                if (starved)
                {
                    AppLogService.Warning(
                        $"[RecFrameHealth] expectedFrames={expectedFrames} actualFrames={encodedNow} " +
                        $"effectiveFps={effFps:0.##} starvation=true severity={severity} " +
                        $"encoder={_rawPipe?.EncodeName ?? "n/a"} " +
                        "note=duration is re-timed to wall clock (Phase 3) but the video " +
                        "contains far fewer real frames than the configured fps; " +
                        "smoothness suffers. Fix: lower resolution, or select a hardware " +
                        "encoder (NVIDIA/Intel) in Recording settings.",
                        new Services.Logging.LogContext { Module = "Recording", Operation = "FrameHealth" });
                }
                else
                {
                    AppLogService.Info(
                        $"[RecFrameHealth] expectedFrames={expectedFrames} actualFrames={encodedNow} " +
                        $"effectiveFps={effFps:0.##} starvation=false severity=Normal",
                        new Services.Logging.LogContext { Module = "Recording", Operation = "FrameHealth" });
                }
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: fps log optional: " + ex.Message); }

            EncodePercent = 55;
        }

        public void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            if (paused)
            {
                _paused = true;
                _pauseStarted = DateTime.UtcNow;
            }
            else
            {
                if (_pauseStarted != default)
                    _pausedAccumulated += DateTime.UtcNow - _pauseStarted;
                _pauseStarted = default;
                _paused = false;
            }
            _audio.IsPaused = paused;
            _mic.IsPaused = paused;
        }

        public void Stop() => _cts?.Cancel();


        /// <summary>
        /// Resolves MainWindow's HWND. The ONLY dispatcher touch in the new
        /// pipeline, done once at start.
        /// </summary>
        private static IntPtr ResolveMainWindowHandle()
        {
            try
            {
                var disp = Application.Current?.Dispatcher;
                if (disp == null) return IntPtr.Zero;

                return disp.Invoke(() =>
                {
                    var w = Application.Current?.MainWindow;
                    if (w == null) return IntPtr.Zero;
                    return new System.Windows.Interop.WindowInteropHelper(w).Handle;
                });
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Could not resolve window handle for capture: {ex.Message}",
                    new Services.Logging.LogContext { Module = "Recording", Operation = "ResolveHwnd" });
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// THE REAL CAPTURE LOOP.
        ///
        /// Runs entirely on a background thread: GDI PrintWindow reads the
        /// window's DWM-composited surface and the bytes go straight into
        /// x264. The UI thread is never involved, which is what removes the
        /// stutter, and DWM's surface includes owned popups, dropdowns and
        /// dialogs — content the old visual-tree render could not see.
        ///
        /// One frame is written per real capture (no CFR dup-fill); the
        /// file therefore claims nominal-fps timing but is rescaled to the
        /// real elapsed duration at save via -itsscale (see RetimeScale /
        /// FfmpegMp4Composer.RetimeCopyAsync), which is what keeps a
        /// 3-minute recording 3 minutes long even when the encoder cannot
        /// sustain the nominal frame rate (e.g. 4K/60 on a CPU encoder).
        /// </summary>
        private async Task RawLoopAsync(string framesFolder, CancellationToken ct)
        {
            int fps = Math.Max(1, _settings.Fps);
            var interval = TimeSpan.FromMilliseconds(1000.0 / fps);

            using var capture = new GdiWindowCapture();

            if (!capture.Prepare(_targetHwnd))
            {
                // DO NOT fall back to the UI-thread capture. That path is
                // what froze the app: it posts a full software re-render to
                // the dispatcher 30x a second, so module switches, dialogs
                // and even the STOP button stop responding. A recording
                // that fails loudly is strictly better than one that
                // hangs the application.
                FatalStartError =
                    "Window capture could not start. Recording was not begun.";
                AppLogService.Warning(
                    FatalStartError,
                    new Services.Logging.LogContext { Module = "Recording", Operation = "RawLoop" });
                return;
            }

            var (outW, outH) = _settings.OutputSize;

            // Phase 4: map the resolved H.264 encoder onto the requested CODEC
            // (no-op unless HEVC was opted into), and pass the chroma request
            // through. Both are logged by the pipe when they have to fall back,
            // so a reduced configuration is never silent.
            var codecSelection = ProGlassAutomation.Agent.Demo.Video.VideoEncoder.WithCodec(
                _encoderSelection ?? ProGlassAutomation.Agent.Demo.Video.VideoEncoderSelection.Libx264,
                _settings.Codec);

            _rawPipe = FfmpegRawPipe.TryStart(
                _rawVideoPath!, fps,
                capture.Width, capture.Height,
                outW, outH,
                _settings.EncodeCrf,
                codecSelection,
                _settings.PixelFormat);

            if (_rawPipe == null)
            {
                FatalStartError =
                    "Video encoder could not start (ffmpeg). Recording was not begun.";
                AppLogService.Warning(
                    FatalStartError,
                    new Services.Logging.LogContext { Module = "Recording", Operation = "RawLoop" });
                return;
            }

            _rawActive = true;
            CaptureNominalFps = fps;
            int srcW = capture.Width, srcH = capture.Height;
            CaptureWidth = srcW;   // PHASE 4B: actual captured geometry for [RecQuality]
            CaptureHeight = srcH;
            var buffer = new byte[srcW * 4 * srcH];
            int frameBytes = buffer.Length;

            // PHASE 4 — one-shot encoder telemetry (Step 17): the exact
            // parameters that produced this recording, so [RecSaved]'s
            // measured bitrate can be attributed. One line, never
            // periodic.
            try
            {
                AppLogService.Info(
                    $"[RecEncode] codec=h264 encoder={_rawPipe.EncodeName} " +
                    $"hardware={_rawPipe.HardwareEncoder} preset={_rawPipe.EncodePreset} " +
                    $"quality={_rawPipe.EncodeCrfUsed} " +
                    $"fallback={EncoderUsedFallback} reason={EncoderFallbackReason ?? ""} " +
                    $"in={srcW}x{srcH} out={outW}x{outH} fps={fps} " +
                    $"tier={_settings.QualityTier} " +
                    $"frameBuffer={frameBytes}B (single reused buffer, streamed)",
                    new Services.Logging.LogContext { Module = "Recording", Operation = "EncodeStart" });
            }
            catch (Exception ex) { AppLogService.Debug($"[RawLoop] encode log: {ex.Message}"); }

            var epoch = DateTime.UtcNow;
            long slot = 0;
            bool haveFrame = false;
            int blankStreak = 0;
            bool warnedFallback = false;

            // PHASE 4 — periodic storage telemetry (Steps 15/16): raw
            // file size, estimated final size and free disk space every
            // 20 s. One FileInfo + one DriveInfo probe per interval —
            // never per frame.
            var storageLogAt = DateTime.UtcNow;
            bool diskLowReported = false;

            // PHASE 5 — [RecPerformance] sampling state (Step 24):
            // periodic capture/encode rates, encoder CPU and app memory.
            long perfCapturedPrev = 0, perfEncodedPrev = 0;
            var perfAt = DateTime.UtcNow;
            TimeSpan encCpuPrev = TimeSpan.Zero;

            // ── ONE-FRAME-PER-CAPTURE + RE-TIME-AT-SAVE (2026-09-10) ─
            //
            // ROOT CAUSE OF "3 minutes records as 1 minute".
            // The ffmpeg raw pipe stamps every written frame at exactly
            // 1/nominal-fps, so the encoded file CLAIMS a duration of
            // framesWritten / nominal-fps. The previous loop tried to keep
            // that honest by DUPLICATING the latest frame into every
            // elapsed nominal slot ("wall-clock fill"). Two things made
            // that collapse the duration on any real machine, and worst of
            // all at the shipped 4K/60 default:
            //
            //   1. A 4K BGRA frame is ~33 MB — far larger than the OS pipe
            //      buffer — so WriteFrame BLOCKS on x264 backpressure.
            //      x264 cannot swallow 60 fps of 4K, so the loop can only
            //      write frames as fast as the encoder drains them (say
            //      ~20 equivalent fps). Over 180 real seconds it writes
            //      ~3600 frames -> 3600/60 = 60 s of claimed video. The
            //      exact reported 3:1 shrink.
            //   2. The dup-fill was then CLAMPED (`pending > fps`) and the
            //      overflow *discarded* as "dropped" — so whenever the
            //      encoder fell behind for even one second, that real wall
            //      time was thrown away and the video got shorter still.
            //
            // THE FIX. Stop pretending the stream is a fixed-rate CFR
            // stream at capture time. Write EXACTLY ONE frame per real
            // capture — no dup-fill, no clamp, nothing discarded — which
            // also removes the biggest source of encoder load so 4K is
            // actually feasible and smooth. The file still nominally
            // claims nominal-fps timing, but the SAVE path rescales its
            // timestamps (ffmpeg -itsscale, a stream-copy, no re-encode)
            // so the final MP4 duration equals the REAL elapsed wall-clock
            // time (see RetimeScale + FfmpegMp4Composer.RetimeCopyAsync).
            // Duration is then correct at any resolution, any fps, on any
            // machine, however far the encoder fell behind.

            // ── ADAPTIVE THROTTLE (Phase 1 freeze fix) ───────────────
            //
            // PrintWindow BLOCKS on the window's owning thread — our UI
            // thread (see GdiWindowCapture's header). Every capture is
            // therefore a UI stall, and issuing them at a fixed 30fps
            // regardless of cost is exactly what made the app
            // unresponsive: on a heavy screen each capture can take
            // longer than the frame interval, so the UI thread never gets
            // a gap in which to service input, and STOP cannot be
            // clicked.
            //
            // The loop now measures what it costs the UI thread and gives
            // time back when it is expensive. Recording at a lower frame
            // rate is a minor quality compromise; an unusable application
            // is not acceptable at any frame rate.
            int effectiveFps = fps;
            var effInterval = interval;
            double slowStreak = 0;
            double fastStreak = 0;

            // PHASE-2 DIAGNOSTIC: periodic health line — every 100 written
            // frames or 10 s, whichever comes first. Never per-frame, so it
            // cannot itself throttle recording.
            long healthLogWritten = 0;
            var healthLogAt = DateTime.UtcNow;

            while (!ct.IsCancellationRequested)
            {
                if (_paused)
                {
                    try { await Task.Delay(50, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    // Rebase the pacing clock on resume so the wait math
                    // does not try to "catch up" the paused interval with
                    // a burst of back-to-back captures. Duration excludes
                    // paused time via Elapsed, and the save-time rescale
                    // maps frames onto that real (un-paused) duration, so
                    // there is no separate write clock to rebase anymore.
                    epoch = DateTime.UtcNow - TimeSpan.FromTicks(effInterval.Ticks * slot);
                    continue;
                }

                // Re-prepare only reallocates when the window actually
                // resized. A size change mid-recording would break the
                // rawvideo contract (fixed -s), so the stream keeps the
                // original geometry and the new content is letterboxed by
                // the scale/pad filter instead of shearing.
                if (capture.Prepare(_targetHwnd)
                    && capture.Width == srcW && capture.Height == srcH)
                {
                    if (capture.TryCapture(buffer))
                    {
                        haveFrame = true;
                        Interlocked.Increment(ref _capturedFrames);
                        blankStreak = capture.LastFrameWasBlank ? blankStreak + 1 : 0;

                        // The screen blit costs nothing on the UI thread;
                        // the PrintWindow fallback does. Say so once, so a
                        // machine silently on the slow path is diagnosable
                        // instead of just "feeling laggy".
                        if (capture.UsedFallbackPath && !warnedFallback)
                        {
                            warnedFallback = true;
                            AppLogService.Warning(
                                "Screen capture fell back to PrintWindow, which " +
                                "blocks the UI thread. Recording will throttle to " +
                                "stay responsive.",
                                new Services.Logging.LogContext
                                { Module = "Recording", Operation = "RawLoop" });
                        }

                        // How much of this frame's budget did we take from
                        // the UI thread?
                        double budgetMs = effInterval.TotalMilliseconds * UiBudgetFraction;
                        if (capture.LastCaptureMs > budgetMs)
                        {
                            slowStreak++;

                            // Sustained overrun: halve the rate so the UI
                            // thread gets real gaps back. Recovery is
                            // deliberately slower than back-off, because
                            // oscillating between rates looks worse to the
                            // user than simply running at the lower one.
                            if (slowStreak >= SlowStreakToHalve && effectiveFps > MinThrottledFps)
                            {
                                effectiveFps = StepThrottleDown(effectiveFps);
                                effInterval = TimeSpan.FromMilliseconds(1000.0 / effectiveFps);
                                FinalEffectiveFps = effectiveFps;   // PHASE-2: record the throttle
                                slowStreak = 0;
                                fastStreak = 0;
                                Interlocked.Increment(ref _throttleDowns);

                                AppLogService.Debug(
                                    $"[Recording] capture {capture.LastCaptureMs:F0}ms " +
                                    $"exceeded budget; frame rate -> {effectiveFps}fps " +
                                    "to keep the UI responsive.");
                            }
                        }
                        else
                        {
                            slowStreak = Math.Max(0, slowStreak - 0.25);

                            // ── RECOVERY (2026-09-16) ────────────────────
                            // Only frames that were COMFORTABLE - not merely
                            // under budget - count toward recovery, and a long
                            // clean streak is required before stepping up.
                            // Without this branch the throttle was one-way and
                            // a single slow moment cost the rest of the file.
                            if (capture.LastCaptureMs <=
                                effInterval.TotalMilliseconds * RecoveryBudgetFraction)
                            {
                                fastStreak++;
                            }
                            else
                            {
                                fastStreak = Math.Max(0, fastStreak - 1);
                            }

                            if (fastStreak >= FastStreakToRecover && effectiveFps < fps)
                            {
                                int before = effectiveFps;
                                effectiveFps = StepThrottleUp(effectiveFps, fps);
                                effInterval = TimeSpan.FromMilliseconds(1000.0 / effectiveFps);
                                FinalEffectiveFps = effectiveFps;
                                fastStreak = 0;
                                slowStreak = 0;
                                Interlocked.Increment(ref _throttleUps);

                                AppLogService.Debug(
                                    $"[Recording] capture settled at " +
                                    $"{capture.LastCaptureMs:F0}ms; frame rate recovered " +
                                    $"{before}fps -> {effectiveFps}fps (requested {fps}fps).");
                            }
                        }
                    }
                }

                if (haveFrame)
                {
                    // ── Write exactly ONE frame per real capture ─────
                    // No dup-fill, no clamp: every captured frame is a
                    // real moment in time, and the save-time -itsscale
                    // rescale (see RetimeScale) stretches the stream back
                    // onto real wall-clock duration. This is what keeps a
                    // 3-minute recording 3 minutes long even when the
                    // encoder can only sustain a fraction of nominal fps.
                    if (!_rawPipe.WriteFrame(buffer, frameBytes))
                    {
                        haveFrame = false;   // encoder died -> break outer loop
                    }
                    else
                    {
                        Interlocked.Increment(ref _encodedFrames);

                        // Phase 5 — attribute this frame's cost to a stage.
                        // Placed inside the success arm because LastWriteMs is
                        // only refreshed by a successful write; reading it after
                        // a failure would double-count the previous frame.
                        double wms = _rawPipe.LastWriteMs;
                        _blitMsTotal += capture.LastBlitMs;
                        _copyMsTotal += capture.LastCopyMs;
                        _writeMsTotal += wms;
                        if (wms > _writeMsMax) _writeMsMax = wms;
                        if (wms >= FfmpegRawPipe.WriteBlockThresholdMs) _writesBlocked++;
                        _stageSamples++;
                    }

                    if (!_rawPipe.IsOpen)
                    {
                        // PHASE 3: encoder death mid-recording is FATAL and
                        // must be visible. Set the user-facing failure,
                        // end the session via the service, and break —
                        // the HUD must not keep showing REC over a dead
                        // encoder.
                        AppLogService.Warning(
                            "Video encoder died mid-recording; ending session.",
                            new Services.Logging.LogContext { Module = "Recording", Operation = "RawLoop" });
                        EncoderFailure =
                            "The recording encoder stopped unexpectedly, so the recording was ended.";
                        NotifyCaptureLoopFatal();
                        break;   // encoder died
                    }

                    // A written frame consumes the "have a fresh frame"
                    // flag so a stalled capture does not re-pipe the same
                    // stale buffer indefinitely; the next successful
                    // TryCapture re-arms it.
                    haveFrame = false;
                }
                else
                {
                    Interlocked.Increment(ref _droppedFrames);
                }

                // ── PHASE-2 DIAGNOSTIC: periodic encoder health ───────
                // Answers "is the encoder alive right now, and is anything
                // actually being written?" — the exact question the silent
                // HUD cannot answer.
                long writtenNow = Volatile.Read(ref _encodedFrames);
                if (writtenNow - healthLogWritten >= 100 || DateTime.UtcNow - healthLogAt >= TimeSpan.FromSeconds(10))
                {
                    healthLogWritten = writtenNow;
                    healthLogAt = DateTime.UtcNow;
                    var rp = _rawPipe;
                    AppLogService.Debug(
                        $"[RecHealth] t={(DateTime.UtcNow - _started).TotalSeconds:F0}s " +
                        $"captured={Volatile.Read(ref _capturedFrames)} " +
                        $"written={writtenNow} " +
                        $"dropped={Volatile.Read(ref _droppedFrames)} " +
                        $"effFps={effectiveFps} " +
                        $"ffmpeg={(rp == null ? "n/a" : rp.ProcessAlive ? "alive" : "DEAD (code=" + (rp.ExitCode?.ToString() ?? "?") + ")")} " +
                        $"pid={rp?.ProcessId ?? -1}",
                        new Services.Logging.LogContext { Module = "Recording", Operation = "Health" });
                }

                // ── PHASE 4: storage telemetry + disk-low guard ─────────
                // Every 20 s: where is this recording heading (raw size,
                // estimated final size) and is the drive still healthy?
                // Free space below the floor is FATAL — a partial file on
                // a full disk is exactly the "silent corruption" this
                // phase must not produce. The broken-pipe path (encoder
                // dies when its write fails) is the backstop if this
                // interval misses it.
                if (DateTime.UtcNow - storageLogAt >= TimeSpan.FromSeconds(20))
                {
                    storageLogAt = DateTime.UtcNow;
                    long rawSize = -1;
                    long availDisk = -1;
                    try
                    {
                        if (_rawActive && !string.IsNullOrWhiteSpace(_rawVideoPath) && File.Exists(_rawVideoPath))
                            rawSize = new FileInfo(_rawVideoPath).Length;
                        availDisk = FfmpegMp4Composer.FreeDiskBytes(_rawVideoPath ?? framesFolder);
                    }
                    catch (Exception ex) { AppLogService.Debug($"[RawLoop] storage probe: {ex.Message}"); }

                    // Final size ≈ raw video (the remux is a stream copy,
                    // no re-encode) + audio (WAV→AAC shrinks ~10×, so a
                    // small margin covers it).
                    long estFinal = rawSize > 0 ? (long)(rawSize * 1.05) : -1;

                    try
                    {
                        AppLogService.Info(
                            $"[RecStorage] t={(DateTime.UtcNow - _started).TotalSeconds:F0}s " +
                            $"rawSize={rawSize}B estFinal={estFinal}B availDisk={availDisk}B",
                            new Services.Logging.LogContext { Module = "Recording", Operation = "Storage" });
                    }
                    catch (Exception ex) { AppLogService.Debug($"[RawLoop] storage log: {ex.Message}"); }

                    // ── PHASE 5: [RecPerformance] — periodic health ────
                    // Real capture/encode rates over the interval,
                    // encoder CPU (percent of one core × cores, so 800%
                    // = 8 cores) and app memory. This is the live view
                    // of frame starvation BEFORE the stop report.
                    try
                    {
                        var perfNow = DateTime.UtcNow;
                        double perfInterval = Math.Max(0.5, (perfNow - perfAt).TotalSeconds);
                        perfAt = perfNow;
                        long capNow = Volatile.Read(ref _capturedFrames);
                        long encNow = Volatile.Read(ref _encodedFrames);
                        double capFps = (capNow - perfCapturedPrev) / perfInterval;
                        double encFps = (encNow - perfEncodedPrev) / perfInterval;
                        perfCapturedPrev = capNow;
                        perfEncodedPrev = encNow;

                        string cpuPercentStr = "n/a";
                        try
                        {
                            var fp = _rawPipe?.FfmpegProcess;
                            if (fp != null)
                            {
                                var cpuNow = fp.TotalProcessorTime;
                                double cpuPct = (cpuNow - encCpuPrev).TotalMilliseconds / (perfInterval * 1000.0) * 100.0;
                                encCpuPrev = cpuNow;
                                if (cpuPct > 0) cpuPercentStr = cpuPct.ToString("0.#") + "%";
                            }
                        }
                        catch { /* encoder already gone — fine */ }

                        string appMem = "n/a";
                        try { appMem = (Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024) + "MB"; }
                        catch { /* optional */ }

                        AppLogService.Info(
                            $"[RecPerformance] t={(perfNow - _started).TotalSeconds:F0}s " +
                            $"captureFps={capFps:0.##} encodedFps={encFps:0.##} " +
                            $"droppedFrames={Volatile.Read(ref _droppedFrames)} " +
                            $"encoderQueue=none(streaming, backpressure=blocking write) " +
                            $"cpuPercent={cpuPercentStr} memoryMB={appMem} " +
                            $"encoder={_rawPipe?.EncodeName ?? "n/a"} resolution={outW}x{outH}",
                            new Services.Logging.LogContext { Module = "Recording", Operation = "Performance" });
                    }
                    catch (Exception ex) { AppLogService.Debug($"[RawLoop] performance log: {ex.Message}"); }

                    if (!diskLowReported && availDisk >= 0
                        && availDisk < FfmpegMp4Composer.MinFreeDiskBytes)
                    {
                        diskLowReported = true;
                        AppLogService.Warning(
                            $"Disk space critically low during recording (avail={availDisk}B); ending session.",
                            new Services.Logging.LogContext { Module = "Recording", Operation = "DiskLow" });
                        StorageFailure =
                            "Free disk space ran out during the recording, so it was ended.";
                        NotifyCaptureLoopFatal();
                        break;
                    }
                }

                // Persistent black output means the driver ignored
                // PW_RENDERFULLCONTENT. Bail to the legacy path rather
                // than hand the user a black video.
                if (blankStreak > fps * 2)
                {
                    AppLogService.Warning(
                        "Window capture returned blank frames; using legacy path.",
                        new Services.Logging.LogContext { Module = "Recording", Operation = "RawLoop" });

                    // Blank output means the driver ignored
                    // PW_RENDERFULLCONTENT. Stop cleanly instead of
                    // switching to the UI-thread path and freezing the app.
                    _rawActive = false;
                    FatalStartError =
                        "The display driver returned blank frames for this window. "
                        + "Recording stopped.";

                    try { await _rawPipe.FinishAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
                    catch (Exception ex) { AppLogService.Debug($"[RawLoop] finish: {ex.Message}"); }

                    // PHASE 3: end the session now — do not leave the HUD
                    // on REC over a pipeline that stopped capturing. The
                    // save path already fails with FatalStartError (no
                    // EncoderFailure: this is a capture, not encoder, death).
                    NotifyCaptureLoopFatal();
                    return;
                }

                slot++;

                // Pace against the ADAPTIVE interval. Using the nominal one
                // here would defeat the throttle entirely: the loop would
                // keep firing at the original rate no matter how expensive
                // each capture had become.
                var wait = epoch + TimeSpan.FromTicks(effInterval.Ticks * slot) - DateTime.UtcNow;

                if (wait > TimeSpan.Zero)
                {
                    try { await Task.Delay(wait, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
                else
                {
                    // We are already late. YIELD ANYWAY — an unconditional
                    // pause is what guarantees the UI thread a gap to
                    // service input. Without it a slow machine spins
                    // capture-after-capture and STOP can never be clicked,
                    // which is precisely the reported "have to force-close"
                    // behaviour.
                    try { await Task.Delay(15, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }

                    if (-wait > effInterval)
                    {
                        long behind = -wait.Ticks / effInterval.Ticks;
                        slot += behind;
                        Interlocked.Add(ref _droppedFrames, (int)Math.Min(behind, int.MaxValue));
                    }
                }
            }
        }






        public void Dispose()
        {
            try { Stop(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            try { CaptureLoopFatal = null; } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            try { _cts?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            try { _pipe?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            try { _rawPipe?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            _audio.Dispose();
            _mic.Dispose();
        }
    }
}
