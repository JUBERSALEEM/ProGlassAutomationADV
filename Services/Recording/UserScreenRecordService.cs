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
        public int EncodedFrames { get; private set; }
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

            string folder = NewJobFolder();
            string frames = Path.Combine(folder, "frames");
            Directory.CreateDirectory(frames);

            var recorder = new WindowScreenRecorder();
            try
            {
                recorder.Start(frames, rec);

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

                    string? merged = await FfmpegMp4Composer
                        .RetimeCopyAsync(recorder.RawVideoPath!, liveMix, mp4, retime, ct)
                        .ConfigureAwait(false);

                    if (merged != null && FfmpegMp4Composer.IsValidMp4(mp4))
                    {
                        FinishSaved(mp4, recorder, rec, folder);
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
