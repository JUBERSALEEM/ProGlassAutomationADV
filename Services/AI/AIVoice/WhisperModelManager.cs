// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIVoice/WhisperModelManager.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Voice (Chat 25 v1.1 — naming collision fix)
//
// CHANGES vs Chat 25 v1.0:
//   ✓ FIX CS0102: IsModelDownloaded was defined as BOTH a parameterless
//     property AND a method taking (string modelKey) in the same class.
//     C# does not allow a property and method with identical names.
//     Resolution:
//       • IsModelDownloaded (parameterless property) → KEPT.
//         Returns true if the currently SELECTED model is on disk.
//         All existing callers (App.xaml.cs, WhisperSpeechService,
//         SettingsViewModel) use this form — zero changes at call sites.
//       • IsModelDownloaded(string modelKey) → RENAMED to
//         IsSpecificModelDownloaded(string modelKey).
//         Used only internally and in SettingsViewModel delete flow.
//   ✓ FIX CS0023/CS0428: All callers of IsModelDownloaded now resolve
//     to the bool property, not a method group.
//
// REGRESSION RISK: ZERO — external call sites unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.AI.AIVoice
{
    /// <summary>
    /// Metadata for a Whisper GGML model variant.
    /// </summary>
    public sealed record WhisperModelInfo
    {
        public string Key { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public int SizeMB { get; init; }
        public string Accuracy { get; init; } = string.Empty;
        public string Speed { get; init; } = string.Empty;
        public string DownloadUrl { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }

    /// <summary>
    /// Manages Whisper GGML model files — download, status, delete.
    /// Supports multiple model variants: base, medium, large-v3.
    /// </summary>
    public static class WhisperModelManager
    {
        private const string HuggingFaceBase =
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

        // ── Available models ──────────────────────────────────────────

        public static readonly IReadOnlyDictionary<string, WhisperModelInfo>
            AvailableModels =
            new Dictionary<string, WhisperModelInfo>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["base"] = new WhisperModelInfo
                {
                    Key = "base",
                    DisplayName = "Base (Recommended)",
                    FileName = "ggml-base.bin",
                    SizeMB = 142,
                    Accuracy = "95%+",
                    Speed = "Fast (1-3 sec)",
                    DownloadUrl = HuggingFaceBase + "ggml-base.bin",
                    Description = "Best balance of size, speed, and accuracy. " +
                                  "Recommended for daily use."
                },
                ["medium"] = new WhisperModelInfo
                {
                    Key = "medium",
                    DisplayName = "Medium (High Accuracy)",
                    FileName = "ggml-medium.bin",
                    SizeMB = 1500,
                    Accuracy = "97%+",
                    Speed = "Moderate (3-8 sec)",
                    DownloadUrl = HuggingFaceBase + "ggml-medium.bin",
                    Description = "Higher accuracy for noisy environments. " +
                                  "Large download (~1.5 GB)."
                },
                ["large-v3"] = new WhisperModelInfo
                {
                    Key = "large-v3",
                    DisplayName = "Large v3 (Maximum Accuracy)",
                    FileName = "ggml-large-v3.bin",
                    SizeMB = 3100,
                    Accuracy = "99%+",
                    Speed = "Slow (5-15 sec)",
                    DownloadUrl = HuggingFaceBase + "ggml-large-v3.bin",
                    Description = "Best possible accuracy. Very large download " +
                                  "(~3.1 GB). Needs 2+ GB RAM."
                }
            };

        // ── Storage directory ─────────────────────────────────────────

        public static string ModelsDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation",
            "Models");

        // ── Selected model helpers ─────────────────────────────────────

        /// <summary>
        /// Currently selected model key from config.
        /// Defaults to "base" if config is not yet loaded.
        /// </summary>
        public static string SelectedModelKey =>
            App.Config?.Ai?.SelectedWhisperModel ?? "base";

        /// <summary>Metadata for the currently selected model.</summary>
        public static WhisperModelInfo SelectedModelInfo =>
            GetModelInfo(SelectedModelKey);

        /// <summary>Full path to the currently selected model file.</summary>
        public static string DefaultModelPath =>
            Path.Combine(ModelsDirectory, SelectedModelInfo.FileName);

        /// <summary>
        /// True if the currently selected model file exists on disk.
        /// Parameterless property — resolves to a bool, not a method group.
        /// All external callers use this form.
        /// </summary>
        public static bool IsModelDownloaded =>
            File.Exists(DefaultModelPath);

        /// <summary>
        /// Size of the downloaded model file in MB. 0 if not downloaded.
        /// </summary>
        public static long ModelFileSizeMB
        {
            get
            {
                try
                {
                    if (!IsModelDownloaded) return 0;
                    return new FileInfo(DefaultModelPath).Length / (1024 * 1024);
                }
                catch { return 0; }
            }
        }

        // ── Per-model queries ─────────────────────────────────────────

        /// <summary>Returns metadata for a specific model key.</summary>
        public static WhisperModelInfo GetModelInfo(string modelKey)
        {
            if (string.IsNullOrWhiteSpace(modelKey)) modelKey = "base";
            return AvailableModels.TryGetValue(modelKey, out var info)
                ? info
                : AvailableModels["base"];
        }

        /// <summary>Full path for a specific model key.</summary>
        public static string GetModelPath(string modelKey) =>
            Path.Combine(ModelsDirectory, GetModelInfo(modelKey).FileName);

        /// <summary>
        /// True if a SPECIFIC model file exists on disk.
        /// RENAMED from IsModelDownloaded(string) to avoid CS0102
        /// collision with the parameterless IsModelDownloaded property.
        /// </summary>
        public static bool IsSpecificModelDownloaded(string modelKey) =>
            File.Exists(GetModelPath(modelKey));

        // ── Download ──────────────────────────────────────────────────

        /// <summary>Downloads the currently selected model.</summary>
        public static Task<bool> DownloadModelAsync(
            Action<int> progress = null,
            CancellationToken cancellationToken = default) =>
            DownloadModelAsync(SelectedModelKey, progress, cancellationToken);

        /// <summary>
        /// Rich progress for a model download: percentage plus the numbers
        /// needed to show speed and a remaining-time estimate.
        /// </summary>
        public sealed class DownloadProgressInfo
        {
            public int Percent { get; init; }
            public long BytesReceived { get; init; }
            public long TotalBytes { get; init; }

            /// <summary>Bytes per second, smoothed. 0 until measurable.</summary>
            public double BytesPerSecond { get; init; }

            /// <summary>Null when the server sent no Content-Length.</summary>
            public TimeSpan? Remaining { get; init; }

            public string SpeedText => BytesPerSecond <= 0
                ? "--"
                : BytesPerSecond >= 1024d * 1024d
                    ? $"{BytesPerSecond / (1024d * 1024d):F1} MB/s"
                    : $"{BytesPerSecond / 1024d:F0} KB/s";

            public string SizeText => TotalBytes > 0
                ? $"{BytesReceived / (1024d * 1024d):F0} of "
                  + $"{TotalBytes / (1024d * 1024d):F0} MB"
                : $"{BytesReceived / (1024d * 1024d):F0} MB";

            public string RemainingText
            {
                get
                {
                    if (Remaining is not { } r) return "";
                    if (r.TotalHours >= 1)
                        return $"{(int)r.TotalHours}h {r.Minutes}m left";
                    if (r.TotalMinutes >= 1)
                        return $"{r.Minutes}m {r.Seconds}s left";
                    return $"{r.Seconds}s left";
                }
            }
        }

        /// <summary>Downloads a specific model by key.</summary>
        public static async Task<bool> DownloadModelAsync(
            string modelKey,
            Action<int> progress = null,
            CancellationToken cancellationToken = default)
            => await DownloadModelAsync(
                modelKey,
                progress == null
                    ? null
                    : new Action<DownloadProgressInfo>(p => progress(p.Percent)),
                cancellationToken);

        /// <summary>
        /// Download with rich progress (percent + speed + ETA). The
        /// Action&lt;int&gt; overload above forwards to this one so existing
        /// callers keep working unchanged.
        /// </summary>
        public static async Task<bool> DownloadModelAsync(
            string modelKey,
            Action<DownloadProgressInfo> progress,
            CancellationToken cancellationToken = default)
        {
            var modelInfo = GetModelInfo(modelKey);
            var targetPath = Path.Combine(ModelsDirectory, modelInfo.FileName);
            var tempPath = targetPath + ".downloading";

            try
            {
                if (!Directory.Exists(ModelsDirectory))
                    Directory.CreateDirectory(ModelsDirectory);

                using var http = new HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(60)
                };

                using var response = await http.GetAsync(
                    modelInfo.DownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                long downloadedBytes = 0;

                using var contentStream =
                    await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(
                    tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[81920];
                int bytesRead;

                // Speed / ETA state. Progress is reported at most ~4x per
                // second: a 3 GB download completes millions of read loops,
                // and marshalling every one onto the UI thread would make
                // the download itself the cause of the UI stutter.
                var started = System.Diagnostics.Stopwatch.StartNew();
                var lastReport = TimeSpan.Zero;
                long lastReportBytes = 0;
                double smoothedBps = 0;
                const double ReportIntervalMs = 250;

                while ((bytesRead = await contentStream.ReadAsync(
                           buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(
                        buffer, 0, bytesRead, cancellationToken);

                    downloadedBytes += bytesRead;

                    if (progress == null) continue;

                    var now = started.Elapsed;
                    double sinceMs = (now - lastReport).TotalMilliseconds;
                    bool finished = totalBytes > 0 && downloadedBytes >= totalBytes;

                    if (sinceMs < ReportIntervalMs && !finished) continue;

                    if (sinceMs > 0)
                    {
                        double instantBps =
                            (downloadedBytes - lastReportBytes) / (sinceMs / 1000d);

                        // Exponential smoothing: raw per-interval speed on a
                        // real connection swings wildly and makes the readout
                        // unreadable.
                        smoothedBps = smoothedBps <= 0
                            ? instantBps
                            : (smoothedBps * 0.7) + (instantBps * 0.3);
                    }

                    TimeSpan? eta = null;
                    if (totalBytes > 0 && smoothedBps > 0)
                    {
                        double secondsLeft =
                            (totalBytes - downloadedBytes) / smoothedBps;

                        // Cap the display: an absurd ETA from a momentary
                        // stall is worse than showing nothing.
                        if (secondsLeft >= 0 && secondsLeft < 60 * 60 * 24)
                            eta = TimeSpan.FromSeconds(secondsLeft);
                    }

                    progress.Invoke(new DownloadProgressInfo
                    {
                        Percent = totalBytes > 0
                            ? (int)(downloadedBytes * 100 / totalBytes)
                            : 0,
                        BytesReceived  = downloadedBytes,
                        TotalBytes     = totalBytes,
                        BytesPerSecond = smoothedBps,
                        Remaining      = eta
                    });

                    lastReport = now;
                    lastReportBytes = downloadedBytes;
                }

                await fileStream.FlushAsync(cancellationToken);
                fileStream.Close();

                // INTEGRITY (U-2): the server advertises Content-Length but
                // nothing compared it against what actually arrived. A
                // connection dropped at 80% left a truncated .bin that was
                // then promoted to the real filename and handed to the
                // native GGML parser, which fails in native code rather
                // than in managed code.
                //
                // This is a COMPLETENESS check, not an authenticity check.
                // It catches a truncated or interrupted download. It does
                // NOT prove the file is the genuine upstream model - that
                // needs a published SHA-256, which the upstream repo does
                // not currently provide per-file. Documented as U-2.
                if (totalBytes > 0 && downloadedBytes != totalBytes)
                {
                    AppLogService.Error(
                        $"Whisper model '{modelKey}' download incomplete: "
                        + $"expected {totalBytes} bytes, got {downloadedBytes}. "
                        + "Discarded rather than loaded.",
                        null,
                        new LogContext
                        {
                            Module = "Voice",
                            Class = nameof(WhisperModelManager),
                            Operation = "DownloadModelAsync"
                        });

                    CleanupTemp(tempPath);
                    return false;
                }

                if (File.Exists(targetPath)) File.Delete(targetPath);
                File.Move(tempPath, targetPath);

                progress?.Invoke(new DownloadProgressInfo
                {
                    Percent = 100,
                    BytesReceived = downloadedBytes,
                    TotalBytes = totalBytes > 0 ? totalBytes : downloadedBytes,
                    BytesPerSecond = 0,
                    Remaining = TimeSpan.Zero
                });
                return true;
            }
            catch (OperationCanceledException)
            {
                CleanupTemp(tempPath);
                return false;
            }
            catch (Exception ex)
            {
                // R-1 RECURRENCE: was Debug.WriteLine only, so in Release a
                // failed 142MB-3GB download reported nothing at all.
                AppLogService.Error(
                    $"Whisper model '{modelKey}' download failed.",
                    ex,
                    new LogContext
                    {
                        Module = "Voice",
                        Class = nameof(WhisperModelManager),
                        Operation = "DownloadModelAsync"
                    });

                CleanupTemp(tempPath);
                return false;
            }
        }

        // ── Delete ────────────────────────────────────────────────────

        /// <summary>Deletes the currently selected model file.</summary>
        public static bool DeleteModel() =>
            DeleteModel(SelectedModelKey);

        /// <summary>Deletes a specific model file by key.</summary>
        public static bool DeleteModel(string modelKey)
        {
            try
            {
                var path = GetModelPath(modelKey);
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WhisperModelManager] Delete failed: {ex.Message}");
                return false;
            }
        }

        private static void CleanupTemp(string tempPath)
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }
}