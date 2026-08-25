using ProGlassAutomation.Agent.Demo.Data;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Agent.Demo.Recording;
using ProGlassAutomation.Agent.Demo.Tts;
using ProGlassAutomation.Agent.Demo.Video;
using ProGlassAutomation.Agent.Demo.Workflows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo
{
    public enum DemoJobState
    {
        Idle, Queued, Analyzing, Preparing, Recording, Narrating,
        Rendering, Validating, Completed, Failed, Cancelled
    }

    public sealed class DemoJobRecord
    {
        public DateTime StartedUtc { get; init; } = DateTime.UtcNow;
        public string Module { get; init; } = "";
        public string State { get; init; } = "";
        public string Status { get; init; } = "";
        public string? Mp4Path { get; init; }
        public string? Folder { get; init; }
        public int VerifiedEvents { get; init; }
    }

    /// <summary>
    /// Isolated demo folder → seed if empty → record the live wizard →
    /// speak only verified events → mux a real MP4. No MP4 means Failed.
    /// HTML / AVI / SRT are sidecars, never success.
    /// </summary>
    public sealed class DemoDirector
    {
        public ScreenRecordSettings RecordSettings { get; set; } = new();

        public DemoJobState State { get; private set; } = DemoJobState.Idle;
        public string Status { get; private set; } = "";

        /// <summary>
        /// True only while a job is actually in flight.
        /// Idle / Completed / Failed / Cancelled must NOT block Generate.
        /// </summary>
        public bool IsRunning =>
            State is DemoJobState.Analyzing or DemoJobState.Preparing
                or DemoJobState.Recording or DemoJobState.Narrating
                or DemoJobState.Rendering or DemoJobState.Validating;
        public string? LastOutputFolder { get; private set; }
        public string? LastMp4Path { get; private set; }
        public string? LastArgument { get; private set; }
        public int VerifiedEventCount { get; private set; }
        public string FfmpegHint { get; private set; } = "";

        public ObservableCollection<DemoJobRecord> History { get; } = new();

        public event Action? Changed;

        private CancellationTokenSource? _cts;

        // 2026-08-25: Cancel stops the WHOLE batch. Previously only the
        // current job's token was cancelled, so a Record-All run kept
        // starting the next module even after the user pressed Cancel.
        public void Cancel()
        {
            DemoJobQueue.Shared.StopAll();
            _cts?.Cancel();
        }

        public async Task<string> RunAsync(string? argument)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            var log = new StringBuilder();
            LastArgument = argument;
            LastMp4Path = null;
            VerifiedEventCount = 0;

            try
            {
                Set(DemoJobState.Analyzing, "Resolving a recorded workflow.");
                var runner = ResolveRunner(argument, out string module, out string reason);
                log.AppendLine(reason);
                if (runner == null)
                {
                    Fail(module, reason, log, folder: null);
                    return Status;
                }

                if (Services.Recording.UserScreenRecordService.Shared.IsBusy)
                {
                    Fail(module,
                        "A manual screen recording is running. Stop it from the REC button first — two recorders cannot share this window.",
                        log, folder: null);
                    return Status;
                }

                Set(DemoJobState.Preparing, "Entering isolated Demo Mode.");
                var entered = await DemoModeService.EnterAndSeedAsync(ct).ConfigureAwait(false);
                log.AppendLine(entered.Message);
                if (!entered.Ok)
                {
                    Fail(module, entered.Message, log, folder: null);
                    return Status;
                }

                // Fail before capture, not after leaving thousands of JPEGs.
                // Manual recording already had this preflight; Demo recording
                // did not, which was the main reason users received a frames/
                // live.wav package instead of one MP4 when ffmpeg was absent.
                FfmpegMp4Composer.InvalidateCache();
                if (FfmpegMp4Composer.Find() == null)
                {
                    Fail(module,
                        "ffmpeg is not installed, so a single MP4 cannot be packed. Install it from Settings → Demo Center and press Recheck.",
                        log, folder: null);
                    return Status;
                }

                string folder = NewJobFolder(module); // private working area
                string outputFolder = GetOutputFolder(module);
                LastOutputFolder = outputFolder;
                log.AppendLine("Working area: " + folder);
                log.AppendLine("MP4 output: " + outputFolder);

                var rec = (RecordSettings ?? new ScreenRecordSettings()).Clone().Clamp();
                Set(DemoJobState.Recording,
                    "Recording the live " + module + " window at " + rec.Summary + ".");
                var framesDir = Path.Combine(folder, "frames");
                Directory.CreateDirectory(framesDir);
                using var recorder = new WindowScreenRecorder();
                recorder.Start(framesDir, rec);
                DemoRecordingChrome.ShowRecording(module, recorder, Cancel);
                DemoStage.Report = msg =>
                {
                    Set(DemoJobState.Recording, msg);
                    DemoRecordingChrome.UpdateStage(msg);
                };

            // 2026-08-25: Record-All robustness — start every workflow
            // from a clean home screen. Without this, state left by the
            // previous job (an open PI wizard, a pending picker) leaked
            // into the next module and broke the batch recording.
            await ResetUiStateAsync(ct).ConfigureAwait(false);

            IReadOnlyList<VerifiedEvent> events;
            try
            {
                events = await runner.RunAsync(ct).ConfigureAwait(false);
            }
                finally
                {
                    DemoStage.Report = null;
                    await recorder.StopAsync().ConfigureAwait(false);
                }

                VerifiedEventCount = events.Count;
                File.WriteAllText(
                    Path.Combine(folder, "events.json"),
                    JsonSerializer.Serialize(events.Select(e => new
                    {
                        e.Id, e.Title, e.Narration, e.Evidence, e.Utc,
                        SpokenSeconds = e.SpokenDuration.TotalSeconds
                    }), new JsonSerializerOptions { WriteIndented = true }));
                log.AppendLine($"Verified events: {events.Count}");

                var deep = new HashSet<string>(
                    runner.DeepEvidenceIds ?? Array.Empty<string>(),
                    StringComparer.Ordinal);
                bool walkedWizard = events.Any(e => deep.Contains(e.Id));
                if (events.Count == 0 || deep.Count == 0 || !walkedWizard)
                {
                    Fail(module,
                        "The workflow did not run. Only a screen change is not a demo. " +
                        "No deep evidence event was verified, so I will not pretend a walkthrough happened.",
                        log, folder);
                    return Status;
                }

                var jpegs = recorder.Frames
                    .Select(f => f.Path)
                    .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                    .ToList();
                bool haveLiveVideo = !string.IsNullOrEmpty(recorder.LiveVideoPath)
                    && File.Exists(recorder.LiveVideoPath)
                    && new FileInfo(recorder.LiveVideoPath).Length > 32;
                log.AppendLine(
                    $"Frames captured: {recorder.EncodedFrames} (jpeg files={jpegs.Count}, live encode={haveLiveVideo})");
                if (recorder.EncodedFrames == 0 && !haveLiveVideo)
                {
                    Fail(module,
                        "The recorder captured no frames. There is no video to encode.",
                        log, folder);
                    return Status;
                }

                File.WriteAllText(Path.Combine(folder, "narration.txt"),
                    string.Join(Environment.NewLine, events.Select((ev, i) =>
                        $"[{i + 1}] {ev.Title}{Environment.NewLine}{ev.Narration}{Environment.NewLine}evidence: {ev.Evidence}{Environment.NewLine}")));

                string? liveWav = recorder.LiveAudioPath;
                string? micWav = recorder.MicAudioPath;
                string mixedWav = Path.Combine(folder, "mixed.wav");
                string? liveMix = await FfmpegMp4Composer
                    .MixAudioAsync(liveWav, micWav, mixedWav, ct)
                    .ConfigureAwait(false);
                TimeSpan liveDur = TimeSpan.Zero;
                if (!string.IsNullOrEmpty(liveMix) && File.Exists(liveMix))
                {
                    try { liveDur = WaveFileSpeech.WavDuration(liveMix); }
                    catch { liveDur = TimeSpan.Zero; }
                }
                bool liveOk = liveDur > TimeSpan.FromMilliseconds(250);

                var beats = new List<(string Text, TimeSpan Duration)>();
                TimeSpan totalAudio = TimeSpan.Zero;
                string? narrationWav = null;

                if (liveOk)
                {
                    beats = BeatsFromEvents(events, recorder.StartedUtc, recorder.Duration);
                    totalAudio = liveDur;
                    log.AppendLine(
                        $"live mix {liveDur.TotalSeconds:0.00}s" +
                        (recorder.LiveAudioStarted ? " internal" : "") +
                        (recorder.MicAudioStarted ? " mic" : "") +
                        " — skipping post-walk TTS because speech was already captured in sync.");
                }
                else
                {
                    Set(DemoJobState.Narrating,
                        "Live mix was silent. Speaking verified events to WAV.");
                    var audioDir = Path.Combine(folder, "audio");
                    Directory.CreateDirectory(audioDir);
                    var wavs = new List<string>();

                    for (int i = 0; i < events.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var ev = events[i];
                        string wav = Path.Combine(audioDir, $"n{i + 1:00}.wav");
                        TimeSpan dur;
                        try
                        {
                            dur = await WaveFileSpeech
                                .SpeakToWavAsync(ev.Narration, wav, ct)
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Fail(module,
                                "Windows SAPI could not write a WAV (" + ex.Message +
                                "). I will not invent speech or mute the video and call it done.",
                                log, folder);
                            return Status;
                        }

                        if (!File.Exists(wav) || dur <= TimeSpan.Zero)
                        {
                            Fail(module,
                                "A narration WAV is missing or has zero duration after a real SAPI write (" +
                                ev.Title + "). Captions will not be faked.",
                                log, folder);
                            return Status;
                        }

                        wavs.Add(wav);
                        beats.Add((ev.Narration, dur));
                        log.AppendLine($"WAV {i + 1}: {dur.TotalSeconds:0.00}s");
                    }

                    narrationWav = Path.Combine(folder, "narration.wav");
                    try
                    {
                        totalAudio = WaveConcat.Concat(wavs, narrationWav);
                    }
                    catch (Exception ex)
                    {
                        Fail(module, "Could not concatenate narration WAVs: " + ex.Message, log, folder);
                        return Status;
                    }
                    if (totalAudio <= TimeSpan.Zero)
                    {
                        Fail(module, "Concatenated narration has zero duration.", log, folder);
                        return Status;
                    }
                    log.AppendLine("TTS WAV (live mix unused: " +
                        (recorder.AudioError ?? "silent or off") + ")");
                }

                string audioNote = liveOk
                    ? $"live mix {liveDur.TotalSeconds:0.00}s" +
                      (recorder.LiveAudioStarted ? " internal" : "") +
                      (recorder.MicAudioStarted ? " mic" : "")
                    : "TTS WAV (live mix unused: " +
                      (recorder.AudioError ?? "silent or off") + ")";

                CaptionWriter.Write(folder, beats);

                var (outW, outH) = rec.OutputSize;
                string baseName = Path.GetFileNameWithoutExtension(runner.OutputFileName);
                string mp4Name = baseName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4";
                string mp4 = Path.Combine(outputFolder, mp4Name);
                string audioForMux = liveOk ? liveMix! : narrationWav!;
                string? produced = null;

                // Calculate actual achieved framerate — the nominal rec.Fps
                // is a UI-thread target that is rarely met. Using it directly
                // stamps frames at the wrong interval, making the video play
                // faster or slower than real time.
                double actualFps = Math.Clamp(
                    recorder.EncodedFrames / Math.Max(1, recorder.Duration.TotalSeconds),
                    1, 60);

                if (jpegs.Count > 0)
                {
                    Set(DemoJobState.Rendering,
                        $"{outW}×{outH} @ {actualFps:F1} fps — composing from {jpegs.Count} captured frames.");
                    recorder.EncodeStage = "Composing video from captured frames…";
                    recorder.EncodePercent = 50;
                    produced = await FfmpegMp4Composer
                        .ComposeFramesAudioAsync(
                            framesDir, "f%05d.jpg", audioForMux, mp4,
                            (int)Math.Round(actualFps), outW, outH, ct, rec.EncodeCrf)
                        .ConfigureAwait(false);
                    log.AppendLine($"Composed from {jpegs.Count} JPEG frames at {actualFps:F1} actual fps.");
                }

                log.AppendLine(
                    $"Recorder {rec.Summary}; frames={recorder.EncodedFrames} dropped={recorder.DroppedFrames}");

                Set(DemoJobState.Validating, "Checking the MP4 is a real ISO-BMFF file.");
                if (produced == null || !FfmpegMp4Composer.IsValidMp4(mp4))
                {
                    string why = FfmpegMp4Composer.Find() == null
                        ? "ffmpeg was not found on PATH or in %AppData%/ProGlassAutomation/tools. No MP4 was produced. AVI is not an MP4."
                        : "ffmpeg ran but did not leave a valid MP4 (missing ftyp box, empty file, or non-zero exit).";
                    Fail(module, why, log, folder);
                    return Status;
                }

                var info = new FileInfo(mp4);
                double? probed = FfmpegMp4Composer.TryReadDurationSeconds(mp4);
                bool durationOk = probed.HasValue ? probed.Value > 0 : totalAudio.TotalSeconds > 0;
                if (info.Length <= 0 || !durationOk)
                {
                    Fail(module,
                        "MP4 failed duration/size checks. " +
                        (probed.HasValue
                            ? $"ffprobe duration={probed.Value:0.00}s."
                            : $"ffprobe unavailable; narration WAV was {totalAudio.TotalSeconds:0.00}s but the file is unusable."),
                        log, folder);
                    return Status;
                }

                LastMp4Path = mp4;
                string durationNote = probed.HasValue
                    ? $"ffprobe duration {probed.Value:0.00}s"
                    : $"duration inferred from narration WAV {totalAudio.TotalSeconds:0.00}s (ffprobe not installed)";
                log.AppendLine($"MP4 {info.Length} bytes, ftyp ok, {durationNote}");
                File.WriteAllText(Path.Combine(folder, "director.log"), log.ToString());
                File.WriteAllText(Path.Combine(folder, "RESULT.txt"),
                    "COMPLETED\n" + mp4 + "\n" + durationNote + "\n");

                Remember(new DemoJobRecord
                {
                    Module = module,
                    State = nameof(DemoJobState.Completed),
                    Status = $"Wrote {mp4Name} ({info.Length} bytes). {durationNote}.",
                    Mp4Path = mp4,
                    Folder = outputFolder,
                    VerifiedEvents = events.Count
                });

                Set(DemoJobState.Completed,
                    $"Wrote {mp4Name} ({info.Length} bytes). {durationNote}. Demo Mode is still on — production files were not opened.");
                DemoRecordingChrome.ShowSaved(
                    mp4,
                    probed.HasValue ? TimeSpan.FromSeconds(probed.Value) : recorder.Duration,
                    jpegs.Count,
                    audioNote);

                // Phase 3 — cleanup temporary files after successful save.
                // The MP4 is the single deliverable; raw frames, mixed audio,
                // narration WAVs and the live H.264 pipe output are no longer
                // needed. Reuses the shared cleanup helper from UserScreenRecordService.
                Services.Recording.UserScreenRecordService
                    .CleanupFolderForDemo(folder);
                // Demo working files are never part of the deliverable. The
                // public DemoVideos folder contains only the packed MP4.
                try { Directory.Delete(folder, recursive: true); }
                catch (Exception ex) { AppLogService.Warning("Demo work cleanup: " + ex.Message); }
                return Status;
            }
            catch (OperationCanceledException)
            {
                Set(DemoJobState.Cancelled, "Demo cancelled.");
                Remember(new DemoJobRecord
                {
                    Module = LastArgument ?? "",
                    State = nameof(DemoJobState.Cancelled),
                    Status = Status,
                    Folder = LastOutputFolder
                });
                return Status;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[DemoDirector] {ex}");
                // Public DemoVideos is MP4-only. Diagnostics belong to the
                // private work area/log, never beside deliverables.
                Fail(LastArgument ?? "", "Demo stopped: " + ex.Message, log, folder: null);
                return Status;
            }
        }

        /// <summary>
        /// Returns the live UI to a neutral state before each recorded
        /// workflow: closes a wizard the previous job may have left open
        /// and navigates to the dashboard (the home screen every module
        /// navigation can reach). Never throws — a failed reset must not
        /// cancel the recording.
        /// </summary>
        private static async Task ResetUiStateAsync(CancellationToken ct)
        {
            try
            {
                var disp = System.Windows.Application.Current?.Dispatcher;
                if (disp == null) return;

                await disp.InvokeAsync(() =>
                {
                    try
                    {
                        if (System.Windows.Application.Current?.MainWindow?.DataContext
                                is ViewModels.MainViewModel main)
                        {
                            var pi = main.ProformaInvoiceVM;
                            if (pi != null && pi.IsWizardView &&
                                pi.BackToListCommand.CanExecute(null))
                            {
                                pi.BackToListCommand.Execute(null);
                            }
                        }

                        ProGlassAutomation.Agent.Core.AgentHost.Navigate?.Invoke("Dashboard");
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Debug(
                            "[DemoDirector] UI reset skipped: " + ex.Message);
                    }
                }).Task.ConfigureAwait(false);

                await Task.Delay(500, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    "[DemoDirector] ResetUiState skipped: " + ex.Message);
            }
        }

        public static IDemoWorkflowRunner? ResolveRunner(
            string? argument, out string module, out string reason)
        {
            string raw = (argument ?? "").Trim();
            if (raw.Length == 0)
            {
                module = "ProformaInvoice";
                reason = "No module named — using Proforma Invoice, the first complete recorded workflow.";
                return new ProformaInvoiceWorkflow();
            }

            if (string.Equals(raw, "All", StringComparison.OrdinalIgnoreCase))
            {
                module = "All";
                reason = "All is expanded by the job queue into one recorded workflow per catalog module.";
                return null;
            }

            var wf = DemoWorkflowCatalog.Find(raw);
            module = wf?.Id ?? raw;
            if (wf == null)
            {
                reason = $"No matching module for '{raw}'. I will not invent one.";
                return null;
            }

            IDemoWorkflowRunner? runner = CreateRunner(wf.Id);
            if (runner == null)
            {
                reason = $"{wf.Title} has no recorded wizard. I will not emit a fake video for this module.";
                return null;
            }

            reason = wf.Title + " recorded workflow selected.";
            return runner;
        }

        public static IDemoWorkflowRunner? CreateRunner(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return id.Trim() switch
            {
                "ProformaInvoice" => new ProformaInvoiceWorkflow(),
                "SheetStore" => new SheetStoreWorkflow(),
                "JobOrder" => new JobOrderWorkflow(),
                "DeliveryNote" => new DeliveryNoteWorkflow(),
                "TaxInvoice" => new TaxInvoiceWorkflow(),
                "ReportsHub" => Tour("ReportsHub", "SalesReport", "Reports Hub", "Open the unified reporting overview and access every operational and financial report."),
                "SalesReport" => new StatementWorkflow(),
                "UserCenter" => new UserCenterWorkflow(),
                "CompanyCenter" => new CompanyCenterWorkflow(),
                "Dashboard" => new DashboardWorkflow(),
                "JobProfitability" => new JobProfitabilityWorkflow(),
                "Optimization" => Tour("Optimization", "Optimization", "Optimization", "Plan efficient sheet yield and cutting layouts."),
                "Cutting" => Tour("Cutting", "Cutting", "Cutting", "Track glass cutting through production."),
                "HolesCutouts" => Tour("HolesCutouts", "HolesCutouts", "Holes and Cutouts", "Manage drilling and shaped cutouts."),
                "Polish" => Tour("Polish", "Polish", "Polish", "Track edge-polishing operations."),
                "EdgeDeletion" => Tour("EdgeDeletion", "EdgeDeletion", "Edge Deletion", "Track coating edge deletion."),
                "Mitring" => Tour("Mitring", "Mitring", "Mitring", "Manage mitred-edge fabrication."),
                "Tempering" => Tour("Tempering", "Tempering", "Tempering", "Track furnace and tempering work."),
                "Pairing" => Tour("Pairing", "Pairing", "Pairing", "Pair panes for insulated assembly."),
                "DoublingLamination" => Tour("DoublingLamination", "DoublingLamination", "Doubling and Lamination", "Assemble DGU and laminated safety units."),
                "PiReport" => Tour("PiReport", "PiReport", "Proforma Report", "Analyze quotation volume, values and status."),
                "JoReport" => Tour("JoReport", "JoReport", "Job Order Report", "Analyze production orders and progress."),
                "DeliveryReport" => Tour("DeliveryReport", "DeliveryReport", "Delivery Report", "Analyze dispatch quantities and balances."),
                "TaxInvoiceReport" => Tour("TaxInvoiceReport", "TaxInvoiceReport", "Tax Invoice Report", "Analyze invoicing and VAT values."),
                "StatementReport" => Tour("StatementReport", "StatementReport", "Statement Report", "Analyze generated customer statements."),
                "SystemCenter" => Tour("SystemCenter", "SystemCenter", "System Center", "Administer users and system access."),
                "Diagnostics" => Tour("Diagnostics", "Diagnostics", "Diagnostics", "Inspect application health and operational logs."),
                "Settings" => Tour("Settings", "Settings", "Settings", "Configure recording, services and appearance."),
                "About" => Tour("About", "About", "About ProGlass", "Review platform capabilities and release information."),
                _ => null
            };
        }

        private static IDemoWorkflowRunner Tour(
            string id, string navigate, string title, string purpose) =>
            new ModuleTourWorkflow(id, navigate, title, purpose);

        public static string NewJobFolder(string module)
        {
            string safe = string.Concat((module ?? "Demo").Where(ch =>
                char.IsLetterOrDigit(ch) || ch is '-' or '_'));
            if (safe.Length == 0) safe = "Demo";
            string root = Path.Combine(
                Services.Database.AppDataRoot.DemoFolder, ".recording-work");
            string dir = Path.Combine(root, safe + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(dir);
            try { new DirectoryInfo(root).Attributes |= FileAttributes.Hidden; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DemoDirector] Could not hide recording work folder: " + ex.Message); }
            return dir;
        }

        private static string GetOutputFolder(string module)
        {
            string safe = string.Concat((module ?? "Demo").Where(ch =>
                char.IsLetterOrDigit(ch) || ch is '-' or '_'));
            if (safe.Length == 0) safe = "Demo";
            string dir = Path.Combine(
                Services.Database.AppDataRoot.DemoFolder, "DemoVideos", safe);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static List<(string Text, TimeSpan Duration)> BeatsFromEvents(
            IReadOnlyList<VerifiedEvent> events, DateTime startedUtc, TimeSpan recorded)
        {
            var beats = new List<(string, TimeSpan)>();
            if (events == null || events.Count == 0) return beats;
            var end = startedUtc + (recorded > TimeSpan.Zero ? recorded : TimeSpan.FromSeconds(1));
            for (int i = 0; i < events.Count; i++)
            {
                var from = events[i].Utc < startedUtc ? startedUtc : events[i].Utc;
                TimeSpan span;
                if (events[i].SpokenDuration > TimeSpan.FromMilliseconds(200))
                    span = events[i].SpokenDuration;
                else
                {
                    var to = i + 1 < events.Count ? events[i + 1].Utc : end;
                    span = to > from ? to - from : TimeSpan.FromMilliseconds(400);
                }
                beats.Add((events[i].Narration, span));
            }
            return beats;
        }

        private void Fail(string module, string message, StringBuilder log, string? folder)
        {
            log.AppendLine("FAILED: " + message);
            if (!string.IsNullOrEmpty(folder))
            {
                try
                {
                    File.WriteAllText(Path.Combine(folder, "director.log"), log.ToString());
                    File.WriteAllText(Path.Combine(folder, "RESULT.txt"), "FAILED\n" + message + "\n");
                }
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DemoDirector] Fail: best effort: " + ex.Message); }
            }
            Remember(new DemoJobRecord
            {
                Module = module,
                State = nameof(DemoJobState.Failed),
                Status = message,
                Folder = folder,
                VerifiedEvents = VerifiedEventCount
            });
            Set(DemoJobState.Failed, message);
            DemoRecordingChrome.ShowFailed(message);
        }

        private void Remember(DemoJobRecord rec)
        {
            try
            {
                var disp = System.Windows.Application.Current?.Dispatcher;
                if (disp != null && !disp.CheckAccess())
                {
                    disp.Invoke(() => PushHistory(rec));
                    return;
                }
                PushHistory(rec);
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DemoDirector] Remember: history is informational: " + ex.Message); }
        }

        private void PushHistory(DemoJobRecord rec)
        {
            History.Insert(0, rec);
            while (History.Count > 20) History.RemoveAt(History.Count - 1);
        }

        private void Set(DemoJobState state, string status)
        {
            State = state;
            Status = status;
            FfmpegHint = FfmpegMp4Composer.Find() == null
                ? "ffmpeg not found — this job will fail (AVI is not accepted as an MP4)."
                : "ffmpeg found — MP4 encode will be attempted.";

            // The `Changed` event is consumed by UI code that may read
            // properties bound to ObservableCollection-backed CollectionViews.
            // Raising it from the demo's background thread will crash with
            // "CollectionView does not support changes to its source
            // collection from a thread different from the dispatcher thread."
            // Dispatch to the UI thread — same pattern as Remember() below.
            try
            {
                var disp = System.Windows.Application.Current?.Dispatcher;
                if (disp != null && !disp.CheckAccess())
                {
                    disp.Invoke(() =>
                    {
                        try { Changed?.Invoke(); }
                        catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[DemoDirector] Set: a subscriber must never break the director: " + ex.Message); }
                    });
                    return;
                }
                Changed?.Invoke();
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[DemoDirector] Set: UI must not break the director: " + ex.Message); }
        }
    }
}
