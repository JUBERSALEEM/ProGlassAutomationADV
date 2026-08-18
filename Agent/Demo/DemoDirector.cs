using ProGlassAutomation.Agent.Core;
using ProGlassAutomation.Services.Export;
using ProGlassAutomation.Services.SampleData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ProGlassAutomation.Agent.Demo
{
    public enum DemoJobState
    {
        Queued, Analyzing, Preparing, Recording, Narrating,
        Rendering, Validating, Completed, Failed, Cancelled
    }

    /// <summary>
    /// Walks real Navigate() targets, captures the live window, writes a
    /// storyboard package. Never deletes ERP data. Seeds only when empty.
    /// </summary>
    public sealed class DemoDirector
    {
        public DemoJobState State { get; private set; } = DemoJobState.Queued;
        public string Status { get; private set; } = "";
        public string? LastOutputFolder { get; private set; }

        public event Action? Changed;

        private CancellationTokenSource? _cts;

        public void Cancel() => _cts?.Cancel();

        public async Task<string> RunAsync(string? argument)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            var log = new StringBuilder();

            try
            {
                Set(DemoJobState.Analyzing, "Discovering workflows from the Navigate switch.");
                var workflows = DemoWorkflowCatalog.Resolve(argument).ToList();
                if (workflows.Count == 0)
                {
                    Set(DemoJobState.Failed, "No matching module. I will not invent one.");
                    return Status;
                }
                log.AppendLine($"Workflows: {string.Join(", ", workflows.ConvertAll(w => w.Id))}");

                Set(DemoJobState.Preparing, "Checking demo isolation.");
                var (canSeed, seedReason) = await DemoIsolation.CanSeedAsync().ConfigureAwait(false);
                log.AppendLine(seedReason);
                if (canSeed)
                {
                    string seeded = await SampleDataService.SeedAsync().ConfigureAwait(false);
                    log.AppendLine(seeded);
                }

                string folder = DemoPackageComposer.NewJobFolder(
                    workflows.Count == 1 ? workflows[0].Id : "All");
                LastOutputFolder = folder;
                var steps = new List<DemoStepResult>();

                foreach (var wf in workflows)
                {
                    ct.ThrowIfCancellationRequested();
                    Set(DemoJobState.Recording, $"Opening {wf.Title}.");

                    bool navigated = false;
                    string navLog = "navigate not wired";
                    try
                    {
                        if (AgentHost.Navigate != null)
                        {
                            AgentHost.Navigate(wf.NavigateTarget);
                            navigated = true;
                            navLog = "navigated via AgentHost.Navigate";
                            await Task.Delay(450, ct).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        navLog = "navigate failed: " + ex.Message;
                    }

                    string frame = "";
                    try
                    {
                        frame = await CaptureFrameAsync(
                            Path.Combine(folder, "frames",
                                $"{steps.Count + 1:00}_{wf.Id}.png"),
                            ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        navLog += "; capture: " + ex.Message;
                    }

                    string beat = string.Join(" ", wf.NarrationBeats);
                    if (!navigated)
                        beat += " The screen could not be opened in this session, so this beat is the planned walkthrough only.";
                    if (string.IsNullOrEmpty(frame))
                        beat += " No screenshot was captured — I will not pretend a frame exists.";

                    steps.Add(new DemoStepResult
                    {
                        Title = wf.Title,
                        Narration = beat,
                        FramePath = frame,
                        Navigated = navigated,
                        Log = navLog
                    });
                    log.AppendLine($"{wf.Id}: {navLog}; frame={(frame.Length > 0 ? "yes" : "no")}");
                }

                Set(DemoJobState.Narrating, "Writing narration and captions from verified beats.");
                DemoPackageComposer.WritePackage(folder,
                    "ProGlass Automation — " +
                    (workflows.Count == 1 ? workflows[0].Title : "Module walkthrough"),
                    steps);

                Set(DemoJobState.Rendering, "Looking for ffmpeg (optional).");
                string? mp4 = await DemoPackageComposer.TryFfmpegAsync(folder, ct)
                    .ConfigureAwait(false);

                Set(DemoJobState.Validating, "Checking output files.");
                bool html = File.Exists(Path.Combine(folder, "index.html"));
                bool srt = File.Exists(Path.Combine(folder, "captions.srt"));
                if (!html || !srt)
                {
                    Set(DemoJobState.Failed, "Package incomplete — index.html or captions.srt missing.");
                    return Status;
                }

                File.WriteAllText(Path.Combine(folder, "director.log"), log.ToString());

                Set(DemoJobState.Completed,
                    $"Demo package written to {folder}." +
                    (mp4 != null
                        ? " ffmpeg produced video.mp4."
                        : " ffmpeg was not found, so there is no MP4 — open index.html."));
                return Status;
            }
            catch (OperationCanceledException)
            {
                Set(DemoJobState.Cancelled, "Demo cancelled.");
                return Status;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DemoDirector] {ex}");
                Set(DemoJobState.Failed, "Demo stopped: " + ex.Message);
                return Status;
            }
        }

        private void Set(DemoJobState state, string status)
        {
            State = state;
            Status = status;
            try { Changed?.Invoke(); } catch { /* UI must not break the director */ }
        }

        private static Task<string> CaptureFrameAsync(string path, CancellationToken ct)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return Task.FromResult("");

            return dispatcher.InvokeAsync(() =>
            {
                ct.ThrowIfCancellationRequested();
                var bmp = ScreenshotService.CaptureMainWindow(ScreenshotQuality.FullHD);
                if (bmp == null) return "";
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using var fs = File.Create(path);
                encoder.Save(fs);
                return File.Exists(path) ? path : "";
            }, System.Windows.Threading.DispatcherPriority.Render).Task;
        }
    }
}
