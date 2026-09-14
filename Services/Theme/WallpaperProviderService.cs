// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/WallpaperProviderService.cs
// PROJECT: ProGlassAutomation
// MODULE: Wallpaper system — online 4K providers + local library
//         (2026-09-05 DEEP-DEBUG REWRITE — owner: "internet still not
//         downloading anything, no mystery answers")
//
// THREE KEYLESS public providers (three DIFFERENT hosts on purpose —
// ISP/Windows-firewall allowlists often pass one CDN while silently
// resetting another; three hosts give a provably independent surface):
//   1. Wallhaven  (https://wallhaven.cc) — JSON search API, SFW only,
//      category words, ≥1440p, random ordering.
//   2. Picsum     (https://picsum.photos) — keyless 4K photography CDN.
//   3. LoremFlickr(https://loremflickr.com) — keyless keyword photo CDN,
//      different origin from the other two.
//
// 2026-09-05 traced defects that were fixed in this pass:
//   • PAGE-LOTTERY: Wallhaven was called with sorting=random AND a random
//     page=1..20. Past the catalogue's real page count the API returns an
//     EMPTY data array → provider gave up. Random ordering alone is the
//     shuffle; no page parameter = page 1, always populated.
//   • RETRY-DEPTH: any single hiccup = permanent "download failed". Now
//     Wallhaven retries with a widened query, Picsum tries two different
//     photo IDs, and a third host sits behind both.
//   • SILENCE: provider failures only reached the app log. Every attempt
//     now returns a precise human reason (status code, TLS error, timeout)
//     that the UI status line shows AND the log records — the owner can
//     READ exactly where the chain broke, no guessing.
//   • HTTP HYGIENE: per-call `new HttpClient` discarded DNS/TLS state;
//     now one pooled client with an explicit User-Agent + Accept header
//     (some CDNs 403 empty-agent requests outright).
//
// HARD RULES (unchanged):
//   • content-type must be image/* and ≤ 30 MB (4K JPEG is 2–8 MB);
//   • failures NEVER throw into the settings UI;
//   • no API keys, no accounts — direct HTTPS GETs only.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>Keyless online wallpaper provider + on-disk library.</summary>
    public static class WallpaperProviderService
    {
        /// <summary>Absolute ceiling for one wallpaper download (bytes).</summary>
        public const long MaxWallpaperBytes = 30L * 1024 * 1024;

        /// <summary>
        /// The result of one provider attempt: either a downloaded file
        /// path, or a HUMAN-READABLE failure reason that the settings UI
        /// shows verbatim (no silent nulls — "nothing downloads" must be
        /// diagnosable on-screen, not just in the log).
        /// </summary>
        public readonly record struct ProviderOutcome(
            string? Path, string? Failure)
        {
            public bool Ok => Path != null;
            public string FailText => Failure ?? "unknown failure";
        }

        /// <summary>
        /// Categories shown in Settings → Appearance → Wallpaper Studio.
        /// Key = stable id, Label = UI text, Query = Wallhaven/LoremFlickr
        /// search terms.
        /// </summary>
        public static readonly (string Key, string Label, string Query)[] Categories =
        {
            ("glass",     "Glass & Abstract",   "abstract glass minimal"),
            ("gradient",  "Minimal Gradient",   "gradient minimal"),
            ("aurora",    "Aurora & Space",     "aurora night sky"),
            ("ocean",     "Ocean & Water",      "ocean underwater"),
            ("nature",    "Nature 4K",          "nature landscape"),
            ("city",      "City & Architecture","architecture cityscape night"),
            ("dark",      "Dark Elegance",      "dark minimal"),
            ("corporate", "Corporate Blue",     "blue abstract wallpaper"),
            ("photo",     "Surprise Me (Photo)","__picsum__"),
        };

        /// <summary>Local wallpaper library directory (created lazily).</summary>
        public static string CacheDir =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProGlassAutomation", "Wallpapers");

        private static readonly Random _rng = new();

        /// <summary>One pooled client for the whole app (DNS/TLS pooled,
        /// explicit modern agent header — some CDNs reject empty-agent
        /// requests with 403 outright). 30 s ceiling covers 4K payloads on
        /// slow hotel links.</summary>
        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ProGlassAutomation-WallpaperStudio/2.0 (+https://wallhaven.cc)");
            http.DefaultRequestHeaders.Accept.ParseAdd("image/*,application/json;q=0.9,*/*;q=0.1");
            return http;
        }

        /// <summary>
        /// Downloads one random wallpaper for a category into the local
        /// library and returns its file path; null on total failure (every
        /// stage already reported). `report` receives short UI status lines
        /// at EVERY stage so the user can see the pipeline working — and,
        /// on failure, the exact reason each provider gave up.
        /// </summary>
        public static async Task<string?> FetchRandomAsync(
            string categoryKey, Action<string>? report = null,
            CancellationToken ct = default)
        {
            var cat = Categories.FirstOrDefault(c => c.Key == categoryKey);
            if (string.IsNullOrEmpty(cat.Key))
                cat = Categories[0];

            var failures = new List<string>();

            try
            {
                Directory.CreateDirectory(CacheDir);

                // "Surprise Me" skips the search providers entirely.
                if (cat.Query != "__picsum__")
                {
                    report?.Invoke($"Searching Wallhaven for \"{cat.Label}\" 4K wallpapers…");
                    AppLogService.Debug(
                        $"[Wallpapers] Fetch start: category={cat.Key} query=\"{cat.Query}\"",
                        new LogContext { Module = "Wallpapers", Operation = "Fetch" });

                    var wh = await TryWallhavenAsync(cat.Query, ct)
                        .ConfigureAwait(false);
                    if (wh.Ok) return LogAndReturn(wh);
                    failures.Add(wh.FailText);
                    report?.Invoke($"Wallhaven: {wh.Failure} — trying the photo service…");
                }

                report?.Invoke("Fetching a 4K photo wallpaper (Picsum)…");
                var ps = await TryPicsumAsync(ct).ConfigureAwait(false);
                if (ps.Ok) return LogAndReturn(ps);
                failures.Add($"Picsum: {ps.Failure}");
                report?.Invoke($"Picsum: {ps.Failure} — trying the keyword photo service…");

                var lf = await TryLoremFlickrAsync(cat.Query, ct)
                    .ConfigureAwait(false);
                if (lf.Ok) return LogAndReturn(lf);
                failures.Add($"LoremFlickr: {lf.Failure}");

                // Total failure: ONE honest line the user can act on.
                string summary =
                    "Download blocked for ALL providers — " +
                    string.Join(" · ", failures) +
                    ". Check internet/proxy/firewall (the app needs plain HTTPS to " +
                    "wallhaven.cc, picsum.photos, loremflickr.com).";
                report?.Invoke(summary);
                AppLogService.Warning(
                    "ALL wallpaper providers failed — " + string.Join(" | ", failures) +
                    " (scenes + library remain fully usable offline).",
                    new LogContext { Module = "Wallpapers", Operation = "Fetch" });
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Fetch pipeline fault: {ex.Message}",
                    new LogContext { Module = "Wallpapers", Operation = "Fetch" });
                report?.Invoke($"Download failed: {ex.Message}");
                return null;
            }

            static string LogAndReturn(in ProviderOutcome o)
            {
                AppLogService.Info(
                    $"Wallpaper downloaded: {o.Path}",
                    new LogContext { Module = "Wallpapers", Operation = "Fetch" });
                return o.Path;
            }
        }

        /// <summary>The local library, newest first (or an empty list —
        /// with a LOG entry on failure instead of silent emptiness).</summary>
        public static IReadOnlyList<FileInfo> Library()
        {
            try
            {
                if (!Directory.Exists(CacheDir))
                    return Array.Empty<FileInfo>();
                return new DirectoryInfo(CacheDir)
                    .EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                             || f.Extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                             || f.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                             || f.Extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .ToList();
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Wallpaper library scan failed ({CacheDir}): {ex.Message}",
                    new LogContext { Module = "Wallpapers", Operation = "Library" });
                return Array.Empty<FileInfo>();
            }
        }

        // ── Provider 1: Wallhaven ────────────────────────────────────

        private static async Task<ProviderOutcome> TryWallhavenAsync(
            string query, CancellationToken ct)
        {
            // sorting=random shuffles the catalogue server-side — NO page
            // parameter (the old "random page 1..20" walked past the real
            // catalogue for long queries and got an empty page back).
            var failed = await WallhavenAttemptAsync(
                WallhavenUrl(query), query, ct).ConfigureAwait(false);
            if (failed.Ok || failed.Failure?.StartsWith("empty") != true)
                return failed;

            // Empty catalogue page: ONE retry with a safe widened query.
            AppLogService.Debug(
                $"[Wallpapers] Widening empty Wallhaven search \"{query}\" → \"abstract wallpaper hd\"",
                new LogContext { Module = "Wallpapers", Operation = "Wallhaven" });
            return await WallhavenAttemptAsync(
                WallhavenUrl("abstract wallpaper hd"), "abstract wallpaper hd",
                ct).ConfigureAwait(false);

            static string WallhavenUrl(string q) =>
                "https://wallhaven.cc/api/v1/search?q=" +
                Uri.EscapeDataString(q) +
                "&categories=100&purity=100&atleast=2560x1440&sorting=random";
        }

        private static async Task<ProviderOutcome> WallhavenAttemptAsync(
            string url, string query, CancellationToken ct)
        {
            try
            {
                using var response = await Http.GetAsync(url, ct)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return new ProviderOutcome(
                        null, $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})");

                await using var json = await response.Content
                    .ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(
                    json, cancellationToken: ct).ConfigureAwait(false);

                if (!doc.RootElement.TryGetProperty("data", out var data)
                    || data.GetArrayLength() == 0)
                    return new ProviderOutcome(null, $"empty catalogue for \"{query}\"");

                var hit = data[_rng.Next(0, data.GetArrayLength())];
                if (!hit.TryGetProperty("path", out var pathEl))
                    return new ProviderOutcome(null, "malformed catalogue row (no path)");
                string imageUrl = pathEl.GetString() ?? "";
                string id = hit.TryGetProperty("id", out var idEl)
                    ? idEl.GetString() ?? "wall"
                    : "wall";
                if (string.IsNullOrWhiteSpace(imageUrl))
                    return new ProviderOutcome(null, "catalogue row without image url");

                string ext = Path.GetExtension(new Uri(imageUrl).AbsolutePath);
                if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".jpg";
                string file = Path.Combine(CacheDir,
                    $"wallhaven-{Sanitize(id)}{ext}");

                return await DownloadAsync(imageUrl, file, "Wallhaven", ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return new ProviderOutcome(null, Describe(ex));
            }
        }

        // ── Provider 2: Picsum ───────────────────────────────────────

        private static async Task<ProviderOutcome> TryPicsumAsync(
            CancellationToken ct)
        {
            // Two different photo IDs = two independent attempts (one CDN
            // edge miss must not end the whole chain).
            foreach (var id in new[] { _rng.Next(0, 1080), _rng.Next(0, 1080) })
            {
                string file = Path.Combine(CacheDir,
                    $"picsum-{id}-{DateTime.UtcNow:yyyyMMddHHmmss}.jpg");
                var outcome = await DownloadAsync(
                    $"https://picsum.photos/id/{id}/3840/2160", file,
                    "Picsum", ct).ConfigureAwait(false);
                if (outcome.Ok) return outcome;
                AppLogService.Debug(
                    $"[Wallpapers] Picsum id {id} failed: {outcome.FailText}",
                    new LogContext { Module = "Wallpapers", Operation = "Picsum" });
            }
            return new ProviderOutcome(null, "both photo IDs failed");
        }

        // ── Provider 3: LoremFlickr (independent host) ───────────────

        private static async Task<ProviderOutcome> TryLoremFlickrAsync(
            string query, CancellationToken ct)
        {
            try
            {
                // Keyword comes from the category words; generic fallback
                // keeps "Surprise Me" interesting too.
                string kw = string.IsNullOrWhiteSpace(query)
                    || query == "__picsum__"
                        ? "nature"
                        : query.Split(' ')[0];
                string file = Path.Combine(CacheDir,
                    $"flickr-{Sanitize(kw)}-{DateTime.UtcNow:yyyyMMddHHmmss}.jpg");
                return await DownloadAsync(
                    $"https://loremflickr.com/3840/2160/{Uri.EscapeDataString(kw)}",
                    file, "LoremFlickr", ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return new ProviderOutcome(null, Describe(ex));
            }
        }

        // ── Shared download core ─────────────────────────────────────

        /// <summary>Streams an HTTPS image to disk with validation:
        /// image/* content type (when declared) and ≤ the size ceiling.
        /// Deletes partial files. NEVER swallows the reason — the outcome
        /// carries a precise failure string for UI + log.</summary>
        private static async Task<ProviderOutcome> DownloadAsync(
            string url, string file, string provider, CancellationToken ct)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await Http.GetAsync(
                    url, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return new ProviderOutcome(
                        null,
                        $"{provider}: HTTP {(int)response.StatusCode} ({response.ReasonPhrase})");

                var type = response.Content.Headers.ContentType?.MediaType;
                if (type != null && !type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    return new ProviderOutcome(null, $"{provider}: not an image (got {type})");
                if (response.Content.Headers.ContentLength is > MaxWallpaperBytes)
                    return new ProviderOutcome(null, $"{provider}: file larger than the 30 MB ceiling");

                string tmp = file + ".part";
                try
                {
                    await using (var input = await response.Content
                        .ReadAsStreamAsync(ct).ConfigureAwait(false))
                    await using (var output = new FileStream(
                        tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[64 * 1024];
                        long total = 0;
                        int read;
                        while ((read = await input.ReadAsync(
                            buffer.AsMemory(0, buffer.Length), ct)
                            .ConfigureAwait(false)) > 0)
                        {
                            total += read;
                            if (total > MaxWallpaperBytes)
                            {
                                output.Close();
                                File.Delete(tmp);
                                return new ProviderOutcome(
                                    null, $"{provider}: download exceeded 30 MB");
                            }
                            await output.WriteAsync(
                                buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                        }
                    }

                    if (File.Exists(file)) File.Delete(file);
                    File.Move(tmp, file);
                    return new ProviderOutcome(file, null);
                }
                catch
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("if suppressed");
            }
                    throw;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return new ProviderOutcome(null, $"{provider}: {Describe(ex)}");
            }
            finally
            {
                response?.Dispose();
            }
        }

        /// <summary>Human-diagnosable exception text: TLS reset, timeout,
        /// DNS — the words a firewall/proxy problem actually shows.</summary>
        private static string Describe(Exception ex) =>
            ex switch
            {
                HttpRequestException hre =>
                    $"network error ({hre.StatusCode?.ToString() ?? hre.Message})",
                TaskCanceledException => "timed out (30 s)",
                _ => $"{ex.GetType().Name}: {ex.Message}"
            };

        private static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '-');
            return name;
        }
    }
}
