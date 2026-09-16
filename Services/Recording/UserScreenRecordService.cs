using ProGlassAutomation.Agent.Demo;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Agent.Demo.Recording;
using ProGlassAutomation.Agent.Demo.Video;
using ProGlassAutomation.Agent.Tools.Demo;
using ProGlassAutomation.Services.Database;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ProGlassAutomation.Services.Recording
{
    /// <summary>
    /// Independent in-app screen recorder. Captures only the running
    /// main window (not the desktop). Demo Center uses the same capture
    /// engine for automated walks; this service is the user-facing
    /// start/stop path and never enters Demo Mode or touches ERP files.
    /// </summary>
    public sealed class UserScreenRecordService
    {
        public static UserScreenRecordService Shared { get; } = new();

        public static string OutputRoot => Path.Combine(
            AppDataRoot.ProductionFolder, "Recordings");

        private readonly object _gate = new();
        private WindowScreenRecorder? _recorder;
        private string? _jobFolder;
        private IDisposable? _critical;
        private CancellationTokenSource? _cts;
        private ScreenRecordSettings _settings = LoadPersistedSettings();

        /// <summary>
        /// Hydrates the recording settings from
        /// <see cref="AppConfig.Recording"/> on first construction so
        /// the user's last quality / fps / mic / speaker choices
        /// survive an app restart (Aug 2026 senior audit fix). Before
        /// this, every launch reset to defaults.
        /// </summary>
        private static ScreenRecordSettings LoadPersistedSettings()
        {
            try
            {
                var prefs = App.Config?.Recording;
                if (prefs == null) return new ScreenRecordSettings();

                return new ScreenRecordSettings
                {
                    Quality = prefs.QualityHeight switch
                    {
                        720  => ScreenRecordQuality.Hd720,
                        1440 => ScreenRecordQuality.Qhd2K,
                        2160 => ScreenRecordQuality.Uhd4K,
                        _    => ScreenRecordQuality.FullHd
                    },
                    QualityTier = (ProGlassAutomation.Agent.Demo.Recording.ScreenRecordQualityTier)
                        System.Math.Clamp(prefs.QualityTier, 0, 2),
                    Encoder = (ProGlassAutomation.Agent.Demo.Video.ScreenRecordEncoder)
                        System.Math.Clamp(prefs.Encoder, 0, 3),
                    Fps = prefs.Fps,
                    CaptureLiveAudio = prefs.CaptureLiveAudio,
                    CaptureMicrophone = prefs.CaptureMicrophone,
                    Supersample = prefs.Supersample
                }.Clamp();
            }
            catch
            {
                return new ScreenRecordSettings();
            }
        }

        public bool IsRecording { get; private set; }
        public bool IsEncoding { get; private set; }
        public bool IsPaused => _recorder?.IsPaused == true;
        public bool IsBusy => IsRecording || IsEncoding;
        public string Status { get; private set; } =
            "Idle. Records only this app window — not the desktop.";
        public string? LastMp4Path { get; private set; }
        public string? LastFolder { get; private set; }
        public long EncodedFrames { get; private set; }
        public DateTime StartedUtc { get; private set; }

        public ScreenRecordSettings Settings
        {
            get => _settings;
            set
            {
                _settings = (value ?? new ScreenRecordSettings()).Clone().Clamp();
                PersistSettings(_settings);
            }
        }

        /// <summary>
        /// Writes the current settings into <see cref="AppConfig.Recording"/>
        /// and saves the config file. Best-effort: a failed save logs a
        /// warning but does not gate the recording — the in-memory value
        /// still takes effect for the current session.
        /// </summary>
        private static void PersistSettings(ScreenRecordSettings rec)
        {
            try
            {
                if (App.Config == null) return;
                App.Config.Recording ??= new RecordingPreferences();
                App.Config.Recording.QualityHeight = (int)rec.Quality;
                App.Config.Recording.QualityTier = (int)rec.QualityTier;
                App.Config.Recording.Encoder = (int)rec.Encoder;
                App.Config.Recording.Fps = rec.Fps;
                App.Config.Recording.CaptureLiveAudio = rec.CaptureLiveAudio;
                App.Config.Recording.CaptureMicrophone = rec.CaptureMicrophone;
                App.Config.Recording.Supersample = rec.Supersample;
                ConfigService.Save(App.Config);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not persist recording settings: " + ex.Message);
            }
        }

        public event Action? Changed;

        public bool DemoIsRunning => CreateDemoPackageTool.SharedDirector.IsRunning;

        /// <summary>
        /// Shared success tail for both the fast (direct-capture) and the
        /// legacy (frame-compose) paths, so the two cannot drift in what
        /// they report, clean up or persist.
        /// </summary>
        private void FinishSaved(
            string mp4,
            WindowScreenRecorder recorder,
            ScreenRecordSettings rec,
            string folder)
        {
            var info = new FileInfo(mp4);
            double? probed = FfmpegMp4Composer.TryReadDurationSeconds(mp4);

            if (info.Length <= 0 || (probed.HasValue && probed.Value <= 0))
            {
                Fail("MP4 failed size/duration checks.");
                return;
            }

            LastMp4Path = mp4;
            LastFolder = folder;

            string audioNote = (recorder.LiveAudioStarted ? "internal" : "") +
                (recorder.LiveAudioStarted && recorder.MicAudioStarted ? "+" : "") +
                (recorder.MicAudioStarted ? "mic" : "");
            if (string.IsNullOrEmpty(audioNote)) audioNote = "no live audio";

            string durationNote = probed.HasValue
                ? $"ffprobe {probed.Value:0.00}s"
                : $"recorder {recorder.Duration.TotalSeconds:0.00}s";

            Status = $"Saved {Path.GetFileName(mp4)} ({info.Length} bytes). {durationNote}.";

            // PHASE 4B — one consolidated quality/size telemetry line per
            // saved recording (Step 17): every future tuning decision
            // (CRF, preset, profile default) can be made from these
            // measured values. One bounded ffprobe extra, at stop only.
            double? durationSec = probed ?? (recorder.Duration.TotalSeconds > 0 ? recorder.Duration.TotalSeconds : null);
            double? videoKbps = FfmpegMp4Composer.TryReadVideoBitrateKbps(mp4);
            double? finalKbps = durationSec != null && durationSec > 0
                ? info.Length * 8.0 / 1000.0 / durationSec.Value
                : null;
            double? mbPerMinute = durationSec != null && durationSec > 0
                ? info.Length / 1024.0 / 1024.0 / durationSec.Value * 60.0
                : null;
            double realEncodedFps = durationSec != null && durationSec > 0.05
                ? recorder.EncodedFrames / durationSec.Value
                : 0;
            bool audioOn = recorder.LiveAudioStarted || recorder.MicAudioStarted;
            var (outW, outH) = rec.OutputSize;
            // PHASE 5 (Step 23): the rate-control name follows the
            // concrete encoder — CRF (libx264), VBR+CQ (NVENC), CQP
            // (QSV). The quality VALUE is the encoder-mapped one the
            // pipe actually used (see FfmpegRawPipe.EncodeCrfUsed).
            string rateControl = recorder.EncodeName switch
            {
                "h264_nvenc" => "VBR+CQ",
                "h264_qsv" => "CQP",
                _ => "CRF"
            };
            AppLogService.Info(
                $"[RecQuality] resolution={outW}x{outH} " +
                $"captureResolution={recorder.CaptureWidth}x{recorder.CaptureHeight} " +
                $"encodedResolution={outW}x{outH} " +
                $"fps={rec.Fps} realEncodedFps={realEncodedFps:0.##} " +
                $"codec=h264 encoder={recorder.EncodeName} hardware={recorder.HardwareEncoder} " +
                $"preset={recorder.EncodePreset} rateControl={rateControl} " +
                $"CRFOrQuality={recorder.EncodeCrfUsed} tier={rec.QualityTier} " +
                $"pixelFormat=yuv420p " +
                $"videoKbps={(videoKbps?.ToString("0.#") ?? "n/a")} " +
                $"audioKbps={(audioOn ? "160" : "none")} " +
                $"finalKbps={(finalKbps?.ToString("0.#") ?? "n/a")} " +
                $"fileSize={info.Length}B duration={(durationSec?.ToString("0.###") ?? "n/a")}s " +
                $"MBPerMinute={(mbPerMinute?.ToString("0.##") ?? "n/a")}",
                new LogContext { Module = "Recording", Operation = "QualityTelemetry" });

            // ── PHASE 7 (2026-09-16): MEASURED fidelity verdict ──────────
            // [RecQuality] above reports the REQUESTED geometry - outW/outH
            // come from the settings object - so it printed 3840x2160 even
            // for a file that came out smaller, and nothing compared the
            // frame rate a viewer actually receives against the one asked
            // for. Duration WAS already validated mandatorily, and duration
            // is correct by construction: the -itsscale retime stretches
            // whatever arrived onto the real elapsed time. That is precisely
            // how a 4K/60 request that collapsed to 8 real fps produced a
            // six-minute file which passed every existing check and was
            // reported as a success while containing ~2880 frames instead of
            // ~21600.
            //
            // One extra bounded ffprobe measures the delivered stream. A
            // degraded verdict does NOT refuse the save - duration validation
            // still owns the SAVED decision, and refusing would break
            // existing recordings on slower machines - but it is no longer
            // silent: Warning severity, an explicit verdict token in the
            // record, and the user-facing Status says so.
            var facts = FfmpegMp4Composer.TryProbeVideoFacts(mp4);
            var fpsVerdict = WindowScreenRecorder.ClassifyFrameRate(rec.Fps, facts?.AvgFrameRate);
            var resVerdict = WindowScreenRecorder.ClassifyResolution(
                outW, outH, facts?.Width, facts?.Height);
            var fidelity = WindowScreenRecorder.WorstFidelity(fpsVerdict, resVerdict);

            double? measuredDuration = facts?.DurationSeconds ?? durationSec;
            double? avgMbps = WindowScreenRecorder.AverageBitrateMbps(
                info.Length, measuredDuration ?? 0.0);
            double wallSeconds = recorder.Duration.TotalSeconds;
            double? captureFps = wallSeconds > 0.05
                ? recorder.CapturedFrames / wallSeconds : null;
            double? encodedFps = measuredDuration > 0.05
                ? recorder.EncodedFrames / measuredDuration.Value : null;

            string verdictText = fidelity switch
            {
                WindowScreenRecorder.RecordingFidelity.Pass => "PASS",
                WindowScreenRecorder.RecordingFidelity.Degraded => "DEGRADED",
                _ => "UNKNOWN (ffprobe unavailable)"
            };

            // PHASE 2: the concise completion record - one line per recording,
            // built from counters, never one message per frame.
            AppLogService.Info(
                $"[RecCompleted] requested={outW}x{outH}@{rec.Fps}fps " +
                $"delivered={(facts?.Width?.ToString() ?? "?")}x{(facts?.Height?.ToString() ?? "?")}" +
                $"@{(facts?.AvgFrameRate?.ToString("0.##") ?? "?")}fps " +
                $"captured={recorder.CapturedFrames} encoded={recorder.EncodedFrames} " +
                $"dropped={recorder.DroppedFrames} " +
                $"probeFrames={(facts?.FrameCount?.ToString() ?? "n/a")} " +
                $"captureFps={(captureFps?.ToString("0.##") ?? "n/a")} " +
                $"encodedFps={(encodedFps?.ToString("0.##") ?? "n/a")} " +
                $"throttleDowns={recorder.ThrottleDowns} throttleUps={recorder.ThrottleUps} " +
                $"finalFps={recorder.FinalEffectiveFps} retime={recorder.RetimeScale:0.0000} " +
                $"duration={(measuredDuration?.ToString("0.##") ?? "n/a")}s wall={wallSeconds:0.##}s " +
                $"size={info.Length}B avgBitrate={(avgMbps?.ToString("0.###") ?? "n/a")}Mbps " +
                $"codec=h264 encoder={recorder.EncodeName} rateControl={rateControl} " +
                $"quality={recorder.EncodeCrfUsed} preset={recorder.EncodePreset} " +
                $"tier={rec.QualityTier} pixfmt=yuv420p audio={audioNote} " +
                $"validation={verdictText}",
                new LogContext { Module = "Recording", Operation = "CompletedTelemetry" });

            if (fidelity == WindowScreenRecorder.RecordingFidelity.Degraded)
            {
                string detail =
                    fpsVerdict == WindowScreenRecorder.RecordingFidelity.Degraded
                        ? $"frame rate {(facts?.AvgFrameRate?.ToString("0.##") ?? "?")}fps " +
                          $"against {rec.Fps}fps requested"
                        : $"resolution {(facts?.Width?.ToString() ?? "?")}x" +
                          $"{(facts?.Height?.ToString() ?? "?")} against {outW}x{outH} requested";

                Status = $"Saved {Path.GetFileName(mp4)} - DEGRADED: {detail}. " +
                         "The file is the right length but does not hold the requested frame rate.";

                AppLogService.Warning(
                    $"[RecDegraded] {Path.GetFileName(mp4)}: {detail}. " +
                    $"captured={recorder.CapturedFrames} encoded={recorder.EncodedFrames} " +
                    $"dropped={recorder.DroppedFrames} throttleDowns={recorder.ThrottleDowns} " +
                    $"throttleUps={recorder.ThrottleUps} finalFps={recorder.FinalEffectiveFps} " +
                    $"retime={recorder.RetimeScale:0.0000} " +
                    $"avgBitrate={(avgMbps?.ToString("0.###") ?? "n/a")}Mbps. " +
                    "Duration validation passed because the save path retimes onto real " +
                    "elapsed time; the frame deficit is the actual defect. Saved anyway, " +
                    "flagged degraded rather than reported as a clean success.",
                    new LogContext { Module = "Recording", Operation = "FidelityDegraded" });
            }

            // PHASE-2 DIAGNOSTIC: the delivered file vs wall clock — the
            // final comparison line for the "5 minutes → 5 seconds" question.
            // PHASE 4: + measured average bitrate, so quality/filesize
            // tuning can use real recordings ([RecSummary] carries the
            // encoder parameters that produced it).
            double? avgKbps = (probed ?? recorder.Duration.TotalSeconds) > 0
                ? info.Length * 8.0 / 1000.0 / (probed ?? recorder.Duration.TotalSeconds)
                : null;
            AppLogService.Info(
                $"[RecSaved] {Path.GetFileName(mp4)} size={info.Length} bytes " +
                $"probed={(probed?.ToString("0.###") ?? "n/a")}s " +
                $"recorderWall={recorder.Duration.TotalSeconds:0.###}s " +
                $"frames={recorder.EncodedFrames} retime={recorder.RetimeScale:0.0000} " +
                $"avgKbps={(avgKbps?.ToString("0.#") ?? "n/a")} audio={audioNote}",
                new LogContext { Module = "Recording", Operation = "FinishSaved" });

            try
            {
                File.WriteAllText(Path.Combine(folder, "RESULT.txt"),
                    "COMPLETED\n" + mp4 + "\n" + durationNote + "\n");
            }
            catch (System.Exception ex)
            {
                AppLogService.Debug("[UserScreenRecordService] FinishSaved: sidecar is optional: " + ex.Message);
            }

            DemoRecordingChrome.ShowSaved(
                mp4,
                probed.HasValue ? TimeSpan.FromSeconds(probed.Value) : recorder.Duration,
                EncodedFrames,
                audioNote);

            CleanupFolderForDemo(folder);
        }

        /// <summary>
        /// Signals the capture loop to stop IMMEDIATELY and synchronously.
        ///
        /// Called first thing when STOP is pressed, before the async save
        /// pipeline is even scheduled. This matters because each capture
        /// blocks the UI thread (PrintWindow -> WM_PRINT): the sooner the
        /// loop stops issuing them, the sooner the UI thread is free to
        /// process the rest of the stop, repaint the HUD and accept input.
        ///
        /// Cheap, non-throwing, and safe to call when nothing is running.
        /// </summary>
        public void RequestStopNow()
        {
            WindowScreenRecorder? rec;
            lock (_gate) rec = _recorder;

            try { rec?.Stop(); }
            catch (Exception ex)
            {
                AppLogService.Debug($"[UserScreenRecordService] RequestStopNow: {ex.Message}");
            }
        }

        public string? TryStart(ScreenRecordSettings? settings = null)
        {
            lock (_gate)
            {
                if (IsBusy)
                    return "A screen recording is already running. Press STOP first.";
                if (DemoIsRunning)
                    return "A demo walk is recording this window. Cancel the demo first.";
            }

            // START MUST BE INSTANT.
            //
            // InvalidateCache() forced FindCore() to re-run on the UI
            // thread, and when ffmpeg is not in the tools folder FindCore
            // SPAWNS "ffmpeg -version" twice with a 1.5 s timeout each --
            // up to 3 seconds of blocking process launches on every START
            // click. That is the "stop start not instant" delay.
            //
            // The cache is only invalidated when the previous lookup FAILED,
            // which is the single case where re-checking can change the
            // answer (the user just installed ffmpeg). A successful lookup
            // is stable for the process lifetime.
            if (FfmpegMp4Composer.Find() == null)
            {
                FfmpegMp4Composer.InvalidateCache();
            }

            if (FfmpegMp4Composer.Find() == null)
            {
                return "ffmpeg is not installed, so no MP4 can be written. " +
                       "Open Settings → Demo Center and download ffmpeg, then Recheck.";
            }

            var rec = (settings ?? _settings ?? new ScreenRecordSettings()).Clone().Clamp();
            _settings = rec;

            // PHASE 4 — pre-flight disk check (Step 15). A nearly-full
            // drive is the classic "recording dies halfway for no
            // visible reason"; refuse to START when even a short
            // recording cannot be saved, and log the headroom estimate
            // for the chosen settings otherwise. The in-recording
            // [RecStorage] watcher (recorder side) covers the drive
            // filling up later.
            long freeBytes = FfmpegMp4Composer.FreeDiskBytes(OutputRoot);
            if (freeBytes >= 0 && freeBytes < FfmpegMp4Composer.MinFreeDiskBytes)
            {
                return "There is not enough free disk space to start a " +
                       "recording. Free up space and try again.";
            }

            double worstMbps = EstimateWorstCaseMbps(rec.Quality, rec.QualityTier);
            if (freeBytes >= 0)
            {
                double est30min = worstMbps * 1e6 / 8 * 1800;
                AppLogService.Info(
                    $"[RecStorage] preflight free={freeBytes}B " +
                    $"est30min={(long)est30min}B worstCase={worstMbps:0.#}Mbps " +
                    $"lowSpace={freeBytes < est30min}",
                    new LogContext { Module = "Recording", Operation = "DiskPreflight" });
            }

            // ── PHASE 5 (Steps 8/9/30): encoder resolution ────────────
            // Resolve the requested encoder against PROBED capability
            // (the probe proves an encoder by test-encoding; an unproven
            // hardware encoder is never selected). The fallback chain
            // always ends at libx264 — the pipeline that has always
            // worked — so a missing/broken GPU cannot break recording.
            var encSnap = ProGlassAutomation.Agent.Demo.Video.EncoderProbe.GetSnapshot();
            var encRes = ProGlassAutomation.Agent.Demo.Video.VideoEncoder.Resolve(
                rec.Encoder, encSnap.NvencUsable, encSnap.QsvUsable);
            AppLogService.Info(
                $"[RecEncoderSelect] requested={rec.Encoder} selected={encRes.Selection.Name} " +
                $"hardware={encRes.Selection.Hardware} fallback={encRes.Fallback} " +
                $"reason={encRes.Reason ?? ""}",
                new LogContext { Module = "Recording", Operation = "EncoderSelect" });
            if (encRes.Fallback && !string.IsNullOrWhiteSpace(encRes.Reason))
                Status = $"Requested encoder unavailable ({encRes.Reason}); using {encRes.Selection.Name}.";

            // ── PHASE 5 (Steps 11/21): FPS guard ──────────────────────
            // Never "record 60 FPS" by re-timing starved frames (that
            // produced the 1-fps 4K masters). When the probe has
            // MEASURED the selected encoder's sustained fps and it
            // cannot sustain ≥50% of the requested rate at the selected
            // resolution — while a smaller one can — downgrade cleanly
            // 4K → 2K → 1080p and communicate it. No probe data → no
            // downgrade (the [RecFrameHealth] line at stop still flags
            // starvation loudly).
            string? fpsGuardNote = null;
            if (encSnap.MeasuredFps.HasValue && encSnap.MeasuredEncoder == encRes.Selection.Name
                && encSnap.MeasuredWidth > 0 && encSnap.MeasuredHeight > 0)
            {
                var (gw, gh) = rec.OutputSize;
                double need = rec.Fps * 0.5;
                double est = ProGlassAutomation.Agent.Demo.Video.VideoEncoder.EstimateEncodeFps(
                    encSnap.MeasuredFps.Value, encSnap.MeasuredWidth, encSnap.MeasuredHeight, gw, gh);
                if (est < need)
                {
                    foreach (var lower in new[]
                             {
                                 ScreenRecordQuality.Qhd2K,
                                 ScreenRecordQuality.FullHd,
                                 ScreenRecordQuality.Hd720
                             })
                    {
                        if (lower >= rec.Quality) continue;
                        var opt = ScreenRecordSettings.Qualities.First(q => q.Quality == lower);
                        double le = ProGlassAutomation.Agent.Demo.Video.VideoEncoder.EstimateEncodeFps(
                            encSnap.MeasuredFps.Value, encSnap.MeasuredWidth, encSnap.MeasuredHeight,
                            opt.Width, opt.Height);
                        if (le >= need)
                        {
                            AppLogService.Warning(
                                $"[RecFpsGuard] encoder={encRes.Selection.Name} estimated {est:0.#}fps " +
                                $"at {gw}x{gh} (< {need:0.#} needed for {rec.Fps}fps); " +
                                $"downgrading to {opt.Width}x{opt.Height} (est {le:0.#}fps)",
                                new LogContext { Module = "Recording", Operation = "FpsGuard" });
                            rec.Quality = lower;
                            fpsGuardNote = $"Recording at {opt.Width}×{opt.Height} instead of {gw}×{gh}: " +
                                $"this machine cannot encode {gw}×{gh} smoothly with {encRes.Selection.Name}.";
                            break;
                        }
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(fpsGuardNote))
                Status = fpsGuardNote!;

            // Keep the probe fresh for the next start (background, idempotent).
            {
                var (pw, ph) = rec.OutputSize;
                ProGlassAutomation.Agent.Demo.Video.EncoderProbe.RequestProbe(encRes.Selection.Name, pw, ph, rec.Fps);
            }

            string folder = NewJobFolder();
            string frames = Path.Combine(folder, "frames");
            Directory.CreateDirectory(frames);

            var recorder = new WindowScreenRecorder();
            try
            {
                recorder.Start(frames, rec, encRes.Selection, encRes.Fallback, encRes.Reason);

                // PHASE 3: if the capture loop ends for a FATAL reason
                // mid-recording (encoder death, persistent blank output),
                // stop + report the session immediately. The HUD must
                // never keep showing REC over a dead pipeline. StopAndSaveAsync
                // is idempotent-gated (IsRecording), so a user STOP racing
                // this is a no-op.
                recorder.CaptureLoopFatal += () =>
                {
                    AppLogService.Warning(
                        "Capture loop reported a fatal stop; ending session.",
                        new LogContext { Module = "Recording", Operation = "LoopFatal" });
                    _ = Task.Run(async () =>
                    {
                        try { await StopAndSaveAsync().ConfigureAwait(false); }
                        catch (Exception ex) { AppLogService.Debug($"[Recording] fatal-stop save: {ex.Message}"); }
                    });
                };

                // Start() posts the capture loop and returns. If capture or
                // the encoder could not initialise, the loop reports it via
                // FatalStartError almost immediately. Surface that instead
                // of leaving a HUD stuck on "REC" with nothing behind it —
                // which is what made STOP look dead.
                _ = Task.Run(async () =>
                {
                    await Task.Delay(600).ConfigureAwait(false);
                    var fatal = recorder.FatalStartError;
                    if (string.IsNullOrWhiteSpace(fatal)) return;
                    if (!IsRecording) return;

                    AppLogService.Warning(
                        $"Recording aborted at start: {fatal}",
                        new LogContext { Module = "Recording", Operation = "StartWatchdog" });

                    try { await StopAndSaveAsync().ConfigureAwait(false); }
                    catch (Exception ex)
                    {
                        AppLogService.Debug($"[Recording] watchdog stop: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                recorder.Dispose();
                return "Recorder could not start: " + ex.Message;
            }

            lock (_gate)
            {
                _recorder = recorder;
                _jobFolder = folder;
                LastFolder = folder;
                LastMp4Path = null;
                EncodedFrames = 0;
                StartedUtc = DateTime.UtcNow;
                IsRecording = true;
                IsEncoding = false;
                Status = "Recording this window at " + rec.Summary + ".";
                _cts = new CancellationTokenSource();
                _critical = ProGlassAutomation.Services.Safety.CriticalOperationTracker.Begin("Screen recording");
            }

            DemoRecordingChrome.ShowRecording(
                "Screen", recorder, () => _ = StopAndSaveAsync(), "Screen");
            RaiseChanged();
            return null;
        }

        public void TogglePause()
        {
            lock (_gate)
            {
                if (!IsRecording || _recorder == null) return;
                _recorder.SetPaused(!_recorder.IsPaused);
                Status = _recorder.IsPaused
                    ? "Paused. Press RESUME on the overlay."
                    : "Recording this window at " + _settings.Summary + ".";
            }
            RaiseChanged();
        }

        public async Task StopAndSaveAsync()
        {
            WindowScreenRecorder? recorder;
            string? folder;
            ScreenRecordSettings rec;
            CancellationToken ct;
            lock (_gate)
            {
                if (!IsRecording || _recorder == null)
                    return;
                recorder = _recorder;
                folder = _jobFolder;
                rec = _settings.Clone().Clamp();
                ct = _cts?.Token ?? CancellationToken.None;
                IsRecording = false;
                IsEncoding = true;
                Status = "Finalizing…";
            }
            RaiseChanged();
            DemoRecordingChrome.UpdateStage("Finalizing…");

            try
            {
                recorder.EncodeStage = "Stopping capture…";
                recorder.EncodePercent = 20;
                await recorder.StopAsync().ConfigureAwait(false);
                EncodedFrames = recorder.EncodedFrames;

                if (string.IsNullOrEmpty(folder))
                {
                    Fail("Recording folder is missing.");
                    return;
                }

                // PHASE 3: an encoder that DIED mid-recording must never
                // produce a "SAVED" state — even if the partial file's
                // duration happens to validate. Report the professional
                // failure and keep the job folder (raw file + logs) for
                // forensics.
                // PHASE 4: a disk that filled up mid-recording is the
                // same contract (StorageFailure, set by the in-recording
                // [RecStorage] watcher).
                string? fatalReason =
                    string.IsNullOrWhiteSpace(recorder.EncoderFailure)
                        ? recorder.StorageFailure
                        : recorder.EncoderFailure;
                if (!string.IsNullOrWhiteSpace(fatalReason))
                {
                    AppLogService.Warning(
                        $"[RecSaved] REFUSED — {(recorder.EncoderFailure != null ? "encoder" : "storage")} failure: {fatalReason} " +
                        $"wall={recorder.Duration.TotalSeconds:0.###}s frames={recorder.EncodedFrames} folder={folder}",
                        new LogContext { Module = "Recording", Operation = "StopSave" });
                    Fail(fatalReason +
                        " The captured data was kept in the recordings folder for diagnostics.");
                    return;
                }

                recorder.EncodeStage = "Mixing audio…";
                recorder.EncodePercent = 70;
                DemoRecordingChrome.UpdateStage("Mixing audio… 70%");
                string? liveMix = await FfmpegMp4Composer
                    .MixAudioAsync(
                        recorder.LiveAudioPath,
                        recorder.MicAudioPath,
                        Path.Combine(folder, "mixed.wav"),
                        ct)
                    .ConfigureAwait(false);

                var (outW, outH) = rec.OutputSize;
                string mp4 = Path.Combine(
                    folder, "ScreenRecording_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4");

                // Calculate the actual achieved frame rate from the recorded data.
                // The nominal fps setting (rec.Fps) is a target that the UI
                // thread may not meet — using it directly produces a video
                // whose duration is shorter than real time (fast playback).
                double totalSecs = Math.Max(1, recorder.Duration.TotalSeconds);
                double actualFps = Math.Clamp(recorder.EncodedFrames / totalSecs, 1, 60);

                // ── FAST PATH ────────────────────────────────────────
                // The off-thread capture pipeline already produced a
                // finished H.264 MP4 while recording. Re-encoding it from
                // JPEGs would add generation loss AND several seconds of
                // waiting for no benefit.
                //
                // But that raw file's timestamps run at the NOMINAL fps,
                // so on any machine that could not sustain nominal fps its
                // duration is too short and playback races (the reported
                // "3 minutes plays as 1 minute"). RetimeCopyAsync stretches
                // the video timestamps onto the REAL elapsed duration via
                // -itsscale while STREAM-COPYING the video (no re-encode),
                // then muxes the audio. When the encoder DID keep up
                // (scale ≈ 1) it transparently falls back to a plain mux.
                if (recorder.RawCaptureActive
                    && !string.IsNullOrWhiteSpace(recorder.RawVideoPath)
                    && File.Exists(recorder.RawVideoPath))
                {
                    recorder.EncodeStage = "Finishing…";
                    recorder.EncodePercent = 80;
                    double retime = recorder.RetimeScale;
                    DemoRecordingChrome.UpdateStage(
                        retime > 1.02
                            ? $"Correcting duration ×{retime:0.0}…"
                            : "Finishing…");

                    // PHASE-2 DIAGNOSTIC: exactly what the re-time step will
                    // do — claimed vs real duration, the factor, whether
                    // -itsscale will run, and whether -shortest is active.
                    double claimedSecs = recorder.CaptureNominalFps > 0 && recorder.EncodedFrames > 0
                        ? recorder.EncodedFrames / (double)recorder.CaptureNominalFps
                        : -1;
                    bool willRescale = retime > 0 && Math.Abs(retime - 1.0) > 0.015;
                    bool audioPresent = !string.IsNullOrWhiteSpace(liveMix) && File.Exists(liveMix);
                    AppLogService.Info(
                        $"[RecRetime] writtenFrames={recorder.EncodedFrames} nominalFps={recorder.CaptureNominalFps} " +
                        $"claimed={claimedSecs:0.###}s real={recorder.Duration.TotalSeconds:0.###}s " +
                        $"factor={retime:0.0000} rescale={willRescale} " +
                        $"method={(willRescale ? "-itsscale + -c:v copy" : "plain mux (factor ~1)")} " +
                        $"audioLimit={(audioPresent ? recorder.Duration.TotalSeconds.ToString("0.###") + "s (video authoritative)" : "none")} " +
                        $"audio={(string.IsNullOrWhiteSpace(liveMix) ? "none" : Path.GetFileName(liveMix))}",
                        new LogContext { Module = "Recording", Operation = "StopRetime" });

                    // PHASE 3: pass the real duration as the mux authority
                    // (-t) so a short/dead audio track cannot truncate the
                    // video (the old -shortest could).
                    string? merged = await FfmpegMp4Composer
                        .RetimeCopyAsync(recorder.RawVideoPath!, liveMix, mp4, retime, ct,
                            recorder.Duration.TotalSeconds)
                        .ConfigureAwait(false);

                    if (merged != null && FfmpegMp4Composer.IsValidMp4(mp4))
                    {
                        // PHASE 3 — MANDATORY final-duration validation.
                        // The delivered MP4 is MEASURED (bounded ffprobe)
                        // and compared against the real wall-clock
                        // recording duration. Tolerance = max(2 s, 10 %):
                        // 2 s covers container/encoder timestamp
                        // granularity on short clips, 10 % covers tail
                        // jitter (audio trim, flush) on long ones. A
                        // 5-second file from a 300-second recording fails
                        // by any sane reading of these bounds; 250 s vs
                        // 300 s is 16.7 % off, which exceeds the 10 %
                        // tolerance and FAILs too (the old 50× clamp case).
                        double? probedFast = FfmpegMp4Composer.TryReadDurationSeconds(mp4);
                        double tolFast = Math.Max(2.0, recorder.Duration.TotalSeconds * 0.10);
                        bool durationOkFast = probedFast.HasValue && probedFast.Value > 0 &&
                            recorder.Duration.TotalSeconds > 0.05 &&
                            Math.Abs(probedFast.Value - recorder.Duration.TotalSeconds) <= tolFast;
                        AppLogService.Info(
                            $"[RecValidate] real={recorder.Duration.TotalSeconds:0.###}s " +
                            $"probed={(probedFast?.ToString("0.###") ?? "n/a")}s " +
                            $"tolerance={tolFast:0.###}s frames={recorder.EncodedFrames} " +
                            $"result={(durationOkFast ? "PASS" : "FAIL")}",
                            new LogContext { Module = "Recording", Operation = "DurationValidate" });

                        if (durationOkFast)
                        {
                            FinishSaved(mp4, recorder, rec, folder);
                            return;
                        }

                        // Measured duration is materially wrong. Do NOT
                        // claim success and do NOT fall back (the frame
                        // path is dead in the current pipeline). Keep the
                        // folder — capture.mp4 + logs — for forensics.
                        Fail("The saved video's length does not match the recording length, " +
                             "so it was not marked as saved successfully. " +
                             "The files were kept in the recordings folder for diagnostics.");
                        return;
                    }

                    AppLogService.Warning(
                        "Direct capture mux failed; falling back to frame compose.",
                        new LogContext { Module = "Recording", Operation = "StopAsync" });
                }

                recorder.EncodeStage = "Composing video from captured frames…";
                recorder.EncodePercent = 60;
                DemoRecordingChrome.UpdateStage($"Encoding at {actualFps:F1} fps…");

                // Always compoe from the saved JPEG frames using the ACTUAL
                // frame rate. The live H.264 pipe is only a HUD indicator; its
                // static -framerate produces wrong timing. The JPEGs exist —
                // compoing them correctly is the only path to correct duration.
                var framesList = recorder.Frames
                    .Select(f => f.Path)
                    .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                    .ToList();
                if (framesList.Count == 0)
                {
                    // The capture pipeline no longer writes intermediate
                    // JPEGs — it encodes H.264 directly — so reaching here
                    // means the direct path failed. Report THAT, not a
                    // misleading "no frames captured".
                    Fail(string.IsNullOrWhiteSpace(recorder.FatalStartError)
                        ? "The video encoder did not produce a file."
                        : recorder.FatalStartError!);
                    return;
                }

                // Concat-demuxer first: dropped captures leave holes in
                // f%05d.jpg and the numbered encoder then writes an empty
                // or truncated MP4 ("ffmpeg ran but did not leave a valid
                // MP4"). Demo Director already uses this path.
                string? produced = await FfmpegMp4Composer
                    .ComposeFrameListAudioAsync(
                        framesList, liveMix, mp4,
                        (int)Math.Round(actualFps), outW, outH, ct, rec.EncodeCrf)
                    .ConfigureAwait(false);

                if (produced == null)
                {
                    produced = await FfmpegMp4Composer
                        .ComposeFramesAudioAsync(
                            Path.Combine(folder, "frames"), "f%05d.jpg", liveMix, mp4,
                            (int)Math.Round(actualFps), outW, outH, ct, rec.EncodeCrf)
                        .ConfigureAwait(false);
                }

                if (produced == null || !FfmpegMp4Composer.IsValidMp4(mp4))
                {
                    Fail(FfmpegMp4Composer.Find() == null
                        ? "ffmpeg was not found. No MP4 was produced. AVI is not an MP4."
                        : "ffmpeg ran but did not leave a valid MP4.");
                    return;
                }

                var info = new FileInfo(mp4);
                double? probed = FfmpegMp4Composer.TryReadDurationSeconds(mp4);
                if (info.Length <= 0 || (probed.HasValue && probed.Value <= 0))
                {
                    Fail("MP4 failed size/duration checks.");
                    return;
                }

                // PHASE 3 — same mandatory duration gate as the fast path.
                double tol = Math.Max(2.0, recorder.Duration.TotalSeconds * 0.10);
                bool durationOk = probed.HasValue && probed.Value > 0 &&
                    recorder.Duration.TotalSeconds > 0.05 &&
                    Math.Abs(probed.Value - recorder.Duration.TotalSeconds) <= tol;
                AppLogService.Info(
                    $"[RecValidate] real={recorder.Duration.TotalSeconds:0.###}s " +
                    $"probed={(probed?.ToString("0.###") ?? "n/a")}s " +
                    $"tolerance={tol:0.###}s frames={recorder.EncodedFrames} " +
                    $"result={(durationOk ? "PASS" : "FAIL")}",
                    new LogContext { Module = "Recording", Operation = "DurationValidate" });
                if (!durationOk)
                {
                    Fail("The saved video's length does not match the recording length, " +
                         "so it was not marked as saved successfully. " +
                         "The files were kept in the recordings folder for diagnostics.");
                    return;
                }

                LastMp4Path = mp4;
                LastFolder = folder;
                string audioNote = (recorder.LiveAudioStarted ? "internal" : "") +
                    (recorder.LiveAudioStarted && recorder.MicAudioStarted ? "+" : "") +
                    (recorder.MicAudioStarted ? "mic" : "");
                if (string.IsNullOrEmpty(audioNote)) audioNote = "no live audio";
                string durationNote = probed.HasValue
                    ? $"ffprobe {probed.Value:0.00}s"
                    : $"recorder {recorder.Duration.TotalSeconds:0.00}s";
                Status = $"Saved {Path.GetFileName(mp4)} ({info.Length} bytes). {durationNote}.";
                try
                {
                    File.WriteAllText(Path.Combine(folder, "RESULT.txt"),
                        "COMPLETED\n" + mp4 + "\n" + durationNote + "\n");
                }
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[UserScreenRecordService] StopAndSaveAsync: sidecar is optional: " + ex.Message); }

                DemoRecordingChrome.ShowSaved(
                    mp4,
                    probed.HasValue ? TimeSpan.FromSeconds(probed.Value) : recorder.Duration,
                    EncodedFrames,
                    audioNote);

                // Phase 3 — cleanup temporary files after successful save.
                // The MP4 is the single deliverable; raw frames, mixed audio
                // and the live H.264 pipe output are no longer needed.
                CleanupFolderForDemo(folder);
            }
            catch (OperationCanceledException)
            {
                Status = "Recording cancelled.";
                DemoRecordingChrome.ShowFailed("Recording cancelled.");
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[UserScreenRecord] " + ex);
                Fail("Recording stopped: " + ex.Message);
            }
            finally
            {
                try { recorder.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
                lock (_gate)
                {
                    _recorder = null;
                    _jobFolder = null;
                    IsRecording = false;
                    IsEncoding = false;
                    try { _critical?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
                    _critical = null;
                    try { _cts?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
                    _cts = null;
                }
                RaiseChanged();
            }
        }

        public void OpenOutput()
        {
            string? file = LastMp4Path;
            string folder = LastFolder ?? OutputRoot;
            string target = file != null && File.Exists(file) ? file : folder;
            try
            {
                Directory.CreateDirectory(
                    Directory.Exists(target) ? target : (Path.GetDirectoryName(target) ?? folder));
                            ProcessStartGuard.TryOpenFolder(File.Exists(target) || Directory.Exists(target) ? target : folder, CorrelationContext.NewId());
            }
            catch (Exception ex)
            {
                Status = "Could not open recordings: " + ex.Message;
                RaiseChanged();
            }
        }

        /// <summary>
        /// Opens the top-level recordings ROOT folder in Explorer
        /// regardless of whether a recording has been made yet. Bound
        /// to the top-bar recorder popup's "OPEN RECORDINGS FOLDER"
        /// button (Aug 2026 senior audit).
        /// </summary>
        public void OpenRecordingsFolder()
        {
            try
            {
                Directory.CreateDirectory(OutputRoot);
                ProcessStartGuard.TryOpenFolder(OutputRoot,
                    CorrelationContext.NewId());
            }
            catch (Exception ex)
            {
                Status = "Could not open recordings folder: " + ex.Message;
                RaiseChanged();
            }
        }

        public static string NewJobFolder()
        {
            string dir = Path.Combine(OutputRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        // ── PHASE 4 STORAGE GUARDS (2026-09-14) ─────────────────────────
        // File-size math is 64-bit end to end (FileInfo.Length is long,
        // drive probes below are long). The floor itself lives on
        // FfmpegMp4Composer (FfmpegMp4Composer.MinFreeDiskBytes) so the
        // capture loop and the service share ONE threshold.

        /// <summary>
        /// CRF cannot predict a bitrate, so for space planning we use a
        /// worst-case ceiling per (resolution, tier) — motion-heavy
        /// 4K content at the chosen tier. Static ERP UI encodes far
        /// below these; the numbers only feed the pre-flight headroom
        /// warning and the [RecStorage] estimate, never the encoder.
        /// </summary>
        public static double EstimateWorstCaseMbps(
            ScreenRecordQuality quality, ScreenRecordQualityTier tier)
            => tier switch
            {
                ScreenRecordQualityTier.Standard => quality switch
                {
                    ScreenRecordQuality.Hd720 => 3,
                    ScreenRecordQuality.FullHd => 6,
                    ScreenRecordQuality.Qhd2K => 10,
                    _ => 16
                },
                ScreenRecordQualityTier.VeryHigh => quality switch
                {
                    ScreenRecordQuality.Hd720 => 10,
                    ScreenRecordQuality.FullHd => 16,
                    ScreenRecordQuality.Qhd2K => 25,
                    _ => 45
                },
                _ => quality switch
                {
                    ScreenRecordQuality.Hd720 => 6,
                    ScreenRecordQuality.FullHd => 10,
                    ScreenRecordQuality.Qhd2K => 18,
                    _ => 30
                }
            };

        private void Fail(string message)
        {
            Status = message;
            try
            {
                if (!string.IsNullOrEmpty(_jobFolder))
                    File.WriteAllText(Path.Combine(_jobFolder, "RESULT.txt"), "FAILED\n" + message + "\n");
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[UserScreenRecordService] Fail: sidecar is optional: " + ex.Message); }
            DemoRecordingChrome.ShowFailed(message);
        }

        /// <summary>
        /// Removes all temporary files (raw frames, mixed audio, live encode
        /// leftovers) after a successful MP4 save. The MP4 is the single
        /// deliverable; everything else can be cleaned up. Best-effort —
        /// failures are logged but never throw.
        /// </summary>
        public static void CleanupFolderForDemo(string? folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            try
            {
                string framesDir = System.IO.Path.Combine(folder, "frames");
                if (Directory.Exists(framesDir))
                {
                    try { Directory.Delete(framesDir, recursive: true); }
                    catch (Exception ex) { AppLogService.Warning($"Cleanup frames: {ex.Message}"); }
                }

                string audioDir = System.IO.Path.Combine(folder, "audio");
                if (Directory.Exists(audioDir))
                {
                    try { Directory.Delete(audioDir, recursive: true); }
                    catch (Exception ex) { AppLogService.Warning($"Cleanup audio: {ex.Message}"); }
                }

                // "capture.mp4" is the live H.264 raw-pipe output — its
                // contents were stream-copied into the final
                // ScreenRecording_*.mp4 (or MOVED there when there is no
                // audio). Leaving it behind produced the reported "two
                // video files" per recording. It is never the deliverable,
                // so it is always safe to delete once we reach cleanup.
                // "*.packing.mp4" covers a mux that was interrupted between
                // compose and rename.
                foreach (var file in new[] { "mixed.wav", "live.wav", "mic.wav", "narration.wav", "live.h264.mp4", "capture.mp4", "events.json", "director.log", "narration.txt" })
                {
                    string path = System.IO.Path.Combine(folder, file);
                    try { if (File.Exists(path)) File.Delete(path); }
                    catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[UserScreenRecordService] CleanupFolderForDemo: best-effort: " + ex.Message); }
                }

                foreach (string packing in Directory.EnumerateFiles(folder, "*.packing.mp4"))
                {
                    try { File.Delete(packing); }
                    catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[UserScreenRecordService] CleanupFolderForDemo: packing sweep: " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"Temp folder cleanup failed: {ex.Message}");
            }
        }

        private void RaiseChanged()
        {
            try
            {
                var disp = Application.Current?.Dispatcher;
                if (disp != null && !disp.CheckAccess())
                    disp.InvokeAsync(() => { try { Changed?.Invoke(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); } });
                else
                    Changed?.Invoke();
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[UserScreenRecordService] RaiseChanged: UI must not break the recorder: " + ex.Message); }
        }
    }
}
