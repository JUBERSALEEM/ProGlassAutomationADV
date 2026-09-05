// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/WallpaperProviderService.cs
// PROJECT: ProGlassAutomation
// MODULE: Wallpaper system — online 4K provider + local library
//         (2026-09-05)
//
// WHAT THIS IS (no marketing, just mechanics):
//   Two KEYLESS public providers:
//     1. Wallhaven (https://wallhaven.cc) — a community wallpaper index
//        with a public JSON search API. We query SFW (purity=100),
//        category-filtered, ≥2560×1440 results and pick a random hit,
//        so "download a 4K wallpaper" always returns something fresh
//        from a catalogue of hundreds of thousands of community walls.
//     2. Picsum Photos (https://picsum.photos) — keyless photography
//        CDN; used as the universal fallback when Wallhaven is slow,
//        rate-limited, or offline-user has already seen every result.
//
//   Everything downloads to a LOCAL LIBRARY on disk
//   (%LocalAppData%\ProGlassAutomation\Wallpapers). Applying a wallpaper
//   is then instant and OFFLINE; the library persists across restarts.
//
// HARD RULES:
//   • content-type must be image/* and ≤ 30 MB (4K JPEG is 2–8 MB);
//   • every failure → AppLogService warning + null return, NEVER a throw
//     into the settings UI;
//   • no API keys, no account, no tracking — direct HTTPS GETs only.
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
        /// Categories shown in Settings → Appearance → Wallpaper Studio.
        /// Key = stable id, Label = UI text, Query = Wallhaven search
        /// terms (loose full-text; the catalogue decides the matches).
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

        /// <summary>
        /// Downloads one random wallpaper for a category into the local
        /// library and returns its file path; null on any failure (already
        /// logged). Never throws past the UI boundary. `report` receives
        /// short UI status lines at every stage so the user can SEE the
        /// pipeline working (searching → downloading → saved/failed).
        /// </summary>
        public static async Task<string?> FetchRandomAsync(
            string categoryKey, Action<string>? report = null,
            CancellationToken ct = default)
        {
            var cat = Categories.FirstOrDefault(c => c.Key == categoryKey);
            if (string.IsNullOrEmpty(cat.Key))
                cat = Categories[0];

            try
            {
                Directory.CreateDirectory(CacheDir);

                // "Surprise Me" skips Wallhaven entirely.
                if (cat.Query != "__picsum__")
                {
                    report?.Invoke($"Searching Wallhaven for \"{cat.Label}\" 4K wallpapers…");
                    var wallhaven = await TryWallhavenAsync(cat.Query, ct)
                        .ConfigureAwait(false);
                    if (wallhaven != null) return wallhaven;
                    report?.Invoke("Wallhaven returned nothing — trying the photo service…");
                }

                report?.Invoke("Fetching a 4K photo wallpaper…");
                return await TryPicsumAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Wallpaper download failed ({cat.Key}): {ex.Message}",
                    new LogContext { Module = "Wallpapers", Operation = "Fetch" });
                report?.Invoke($"Download failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>The local library, newest first (or an empty list).</summary>
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
            catch
            {
                return Array.Empty<FileInfo>();
            }
        }

        // ── Providers ─────────────────────────────────────────────────

        /// <summary>Wallhaven public JSON API → one random result →
        /// download to the library. Null when search or download fails.</summary>
        private static async Task<string?> TryWallhavenAsync(
            string query, CancellationToken ct)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "ProGlassAutomation-WallpaperStudio/1.0");

                int page = 1 + _rng.Next(0, 20);
                string url =
                    "https://wallhaven.cc/api/v1/search?q=" +
                    Uri.EscapeDataString(query) +
                    "&categories=100&purity=100&atleast=2560x1440" +
                    "&sorting=random&page=" + page;

                using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return null;

                await using var json = await response.Content
                    .ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(
                    json, cancellationToken: ct).ConfigureAwait(false);

                if (!doc.RootElement.TryGetProperty("data", out var data)
                    || data.GetArrayLength() == 0)
                    return null;

                var hit = data[_rng.Next(0, data.GetArrayLength())];
                if (!hit.TryGetProperty("path", out var pathEl)) return null;
                string imageUrl = pathEl.GetString() ?? "";
                string id = hit.TryGetProperty("id", out var idEl)
                    ? idEl.GetString() ?? "wall"
                    : "wall";
                if (string.IsNullOrWhiteSpace(imageUrl)) return null;

                string ext = Path.GetExtension(
                    new Uri(imageUrl).AbsolutePath);
                if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".jpg";
                string file = Path.Combine(CacheDir,
                    $"wallhaven-{Sanitize(id)}{ext}");

                return await DownloadAsync(http, imageUrl, file, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Wallhaven provider failed: {ex.Message}",
                    new LogContext { Module = "Wallpapers", Operation = "Wallhaven" });
                return null;
            }
        }

        /// <summary>Picsum — keyless photography CDN, one redirecting
        /// 4K request. Universal fallback (and the "Surprise Me" path).</summary>
        private static async Task<string?> TryPicsumAsync(CancellationToken ct)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
                string file = Path.Combine(CacheDir,
                    $"picsum-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{_rng.Next(1000, 9999)}.jpg");
                return await DownloadAsync(
                    http, "https://picsum.photos/3840/2160", file, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Picsum provider failed: {ex.Message}",
                    new LogContext { Module = "Wallpapers", Operation = "Picsum" });
                return null;
            }
        }

        /// <summary>Streams an HTTPS image to disk with validation:
        /// image/* content type (when declared) and ≤ the size ceiling.
        /// Deletes partial files. Returns the path, or null.</summary>
        private static async Task<string?> DownloadAsync(
            HttpClient http, string url, string file, CancellationToken ct)
        {
            using var response = await http.GetAsync(
                url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var type = response.Content.Headers.ContentType?.MediaType;
            if (type != null && !type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return null;
            if (response.Content.Headers.ContentLength is > MaxWallpaperBytes)
                return null;

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
                            return null;
                        }
                        await output.WriteAsync(
                            buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    }
                }

                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
                return file;
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
                throw;
            }
        }

        private static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '-');
            return name;
        }
    }
}
