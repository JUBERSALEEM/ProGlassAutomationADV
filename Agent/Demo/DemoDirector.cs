using ProGlassAutomation.Agent.Demo.Data;
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
        public const int RecordFps = 8;

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

        public void Cancel() => _cts?.Cancel();

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

                Set(DemoJobState.Preparing, "Entering isolated Demo Mode.");
                var entered = await DemoModeService.EnterAndSeedAsync(ct).ConfigureAwait(false);
                log.AppendLine(entered.Message);
                if (!entered.Ok)
                {
                    Fail(module, entered.Message, log, folder: null);
                    return Status;
                }

                string folder = NewJobFolder(module);
                LastOutputFolder = folder;
                log.AppendLine("Output: " + folder);

                Set(DemoJobState.Recording, "Recording the live " + module + " workflow.");
                var framesDir = Path.Combine(folder, "frames");
                Directory.CreateDirectory(framesDir);
                using var recorder = new WindowScreenRecorder();
                recorder.Start(framesDir, RecordFps);

                IReadOnlyList<VerifiedEvent> events;
                try
                {
                    events = await runner.RunAsync(ct).ConfigureAwait(false);
                }
                finally
                {
                    await recorder.StopAsync().ConfigureAwait(false);
                }

                VerifiedEventCount = events.Count;
                File.WriteAllText(
                    Path.Combine(folder, "events.json"),
                    JsonSerializer.Serialize(events.Select(e => new
                    {
                        e.Id, e.Title, e.Narration, e.Evidence, e.Utc
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
                    .Where(File.Exists)
                    .ToList();
                log.AppendLine($"Frames captured: {jpegs.Count}");
                if (jpegs.Count == 0)
                {
                    Fail(module,
                        "The recorder captured no frames. There is no video to encode.",
                        log, folder);
                    return Status;
                }

                Set(DemoJobState.Narrating, "Speaking only the verified events to WAV.");
                var audioDir = Path.Combine(folder, "audio");
                Directory.CreateDirectory(audioDir);
                var wavs = new List<string>();
                var beats = new List<(string Text, TimeSpan Duration)>();
                var narration = new StringBuilder();

                for (int i = 0; i < events.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var ev = events[i];
                    narration.AppendLine($"[{i + 1}] {ev.Title}");
                    narration.AppendLine(ev.Narration);
                    narration.AppendLine("evidence: " + ev.Evidence);
                    narration.AppendLine();

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
                            "A narration WAV is missing or has zero duration. Captions will not be faked.",
                            log, folder);
                        return Status;
                    }

                    wavs.Add(wav);
                    beats.Add((ev.Narration, dur));
                    log.AppendLine($"WAV {i + 1}: {dur.TotalSeconds:0.00}s");
                }

                File.WriteAllText(Path.Combine(folder, "narration.txt"), narration.ToString());
                CaptionWriter.Write(folder, beats);

                string narrationWav = Path.Combine(folder, "narration.wav");
                TimeSpan totalAudio;
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

                Set(DemoJobState.Rendering, "Encoding AVI then asking ffmpeg for MP4.");
                string avi = Path.Combine(folder, "recording.avi");
                try
                {
                    MjpegAviWriter.Write(avi, jpegs, RecordFps);
                    log.AppendLine("AVI written: " + new FileInfo(avi).Length + " bytes");
                }
                catch (Exception ex)
                {
                    Fail(module, "AVI encode failed: " + ex.Message, log, folder);
                    return Status;
                }

                string mp4Name = runner.OutputFileName;
                string mp4 = Path.Combine(folder, mp4Name);
                string? produced = await FfmpegMp4Composer
                    .ComposeAviWavAsync(avi, narrationWav, mp4, ct)
                    .ConfigureAwait(false);

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
                    Folder = folder,
                    VerifiedEvents = events.Count
                });

                Set(DemoJobState.Completed,
                    $"Wrote {mp4Name} ({info.Length} bytes). {durationNote}. Demo Mode is still on — production files were not opened.");
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
                Debug.WriteLine($"[DemoDirector] {ex}");
                Fail(LastArgument ?? "", "Demo stopped: " + ex.Message, log, LastOutputFolder);
                return Status;
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
                "SalesReport" => new StatementWorkflow(),
                "UserCenter" => new UserCenterWorkflow(),
                "CompanyCenter" => new CompanyCenterWorkflow(),
                "Dashboard" => new DashboardWorkflow(),
                "JobProfitability" => new JobProfitabilityWorkflow(),
                _ => null
            };
        }

        public static string NewJobFolder(string module)
        {
            string safe = string.Concat((module ?? "Demo").Where(ch =>
                char.IsLetterOrDigit(ch) || ch is '-' or '_'));
            if (safe.Length == 0) safe = "Demo";
            string root = Path.Combine(
                Services.Database.AppDataRoot.DemoFolder, "DemoVideos");
            string dir = Path.Combine(root, safe, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(dir);
            return dir;
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
                catch { /* best effort */ }
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
            catch { /* history is informational */ }
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
            try { Changed?.Invoke(); } catch { /* UI must not break the director */ }
        }
    }
}
