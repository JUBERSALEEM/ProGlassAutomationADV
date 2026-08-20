using ProGlassAutomation.Agent.Demo;
using ProGlassAutomation.Services.Logging;
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
        private ScreenRecordSettings _settings = new();

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
            set => _settings = (value ?? new ScreenRecordSettings()).Clone().Clamp();
        }

        public event Action? Changed;

        public bool DemoIsRunning => CreateDemoPackageTool.SharedDirector.IsRunning;

        public string? TryStart(ScreenRecordSettings? settings = null)
        {
            lock (_gate)
            {
                if (IsBusy)
                    return "A screen recording is already running. Press STOP first.";
                if (DemoIsRunning)
                    return "A demo walk is recording this window. Cancel the demo first.";
            }

            FfmpegMp4Composer.InvalidateCache();
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
                    Fail("No frames captured. Nothing to save.");
                    return;
                }

                string? produced = await FfmpegMp4Composer
                    .ComposeFramesAudioAsync(
                        Path.Combine(folder, "frames"), "f%05d.jpg", liveMix, mp4,
                        (int)Math.Round(actualFps), outW, outH, ct, rec.EncodeCrf)
                    .ConfigureAwait(false);

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
                catch { /* sidecar is optional */ }

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
                Process.Start(new ProcessStartInfo
                {
                    FileName = File.Exists(target) || Directory.Exists(target) ? target : folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Status = "Could not open recordings: " + ex.Message;
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
            catch { /* sidecar is optional */ }
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

                foreach (var file in new[] { "mixed.wav", "live.wav", "mic.wav", "narration.wav", "live.h264.mp4", "events.json", "director.log", "narration.txt" })
                {
                    string path = System.IO.Path.Combine(folder, file);
                    try { if (File.Exists(path)) File.Delete(path); }
                    catch { /* best-effort */ }
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
            catch { /* UI must not break the recorder */ }
        }
    }
}
