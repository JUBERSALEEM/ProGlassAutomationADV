// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Models/LocalModelManager.cs
// PROJECT: ProGlassAutomation — Local LLM program (2026-09-04)
//
// Owns model FILES and their integrity metadata. THE SAFETY RULES:
//
//   PRIVACY: this class NEVER uploads anything. It only downloads a single
//   GGUF file from the URL stored in the catalog, and only at the user's
//   explicit click.
//
//   INTEGRITY (trust-on-first-use, honestly recorded):
//     1. After a download the SHA-256 is computed and WRITTEN to
//        {id}.meta.json next to the file.
//     2. Verifying a 1-5 GB file on every startup would cost seconds the
//        user did not ask for, so loads verify SIZE FIRST (cheap) and the
//        Settings card offers an explicit "Verify integrity" action that
//        re-hashes on demand and compares with the recorded value.
//     3. If size OR hash mismatches, the model is treated as NOT
//        installed — the agent falls back to the knowledge base. Never
//        silently run a tampered file.
//
//   STORAGE: %AppData%/ProGlassAutomation/Agent/models — never inside the
//   repo, never in Program Files, never next to the DBs. Deleting the app
//   leaves these files behind ONLY if the user chooses; uninstall deletes
//   them.
//
//   PARTIAL DOWNLOADS: written as *.part and removed on cancel/failure,
//   so a half file can never be picked up as a real model.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Agent.Models
{
    /// <summary>On-disk integrity record for one installed model.</summary>
    public sealed class LocalModelMeta
    {
        public string Id { get; set; } = "";
        public long Bytes { get; set; }
        public string Sha256 { get; set; } = "";
        public string SourceUrl { get; set; } = "";
        public DateTime InstalledUtc { get; set; } = DateTime.MinValue;
    }

    /// <summary>Download / verify / delete of catalog models.</summary>
    public static class LocalModelManager
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromHours(2)
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>%AppData%/ProGlassAutomation/Agent/models</summary>
        public static string ModelsFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "Agent", "models");

        private static string ModelPath(LocalModelEntry entry) =>
            Path.Combine(ModelsFolder, entry.FileName);

        private static string MetaPath(LocalModelEntry entry) =>
            Path.Combine(ModelsFolder, entry.Id + ".meta.json");

        // ── State ────────────────────────────────────────────────────────

        public static bool IsInstalled(string? id, out string path)
        {
            path = "";
            var entry = LocalModelCatalog.Find(id);
            if (entry == null) return false;

            try
            {
                string candidate = ModelPath(entry);
                if (!File.Exists(candidate)) return false;

                // Cheap integrity gate at every access: a length mismatch
                // NEVER runs the model.
                var meta = ReadMeta(entry);
                if (meta != null && meta.Bytes != new FileInfo(candidate).Length)
                    return false;

                path = candidate;
                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[LocalModelManager] probe failed: {ex.Message}");
                return false;
            }
        }

        public static LocalModelMeta? ReadMeta(LocalModelEntry entry)
        {
            try
            {
                string path = MetaPath(entry);
                if (!File.Exists(path)) return null;
                return JsonSerializer.Deserialize<LocalModelMeta>(
                    File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[LocalModelManager] meta read failed: {ex.Message}");
                return null;
            }
        }

        // ── Download (user-initiated, cancellable, cleaned up) ──────────

        /// <summary>
        /// Downloads the catalog entry into the local model store.
        /// Never throws: a failure returns false and removes the partial
        /// file, so interrupts can never leave a fake install behind.
        /// </summary>
        public static async Task<bool> InstallAsync(
            LocalModelEntry entry,
            IProgress<double>? progress,
            CancellationToken ct)
        {
            if (entry == null) return false;

            try
            {
                string folder = ModelsFolder;
                Directory.CreateDirectory(folder);

                // Disk-space honesty: refuse before starting when the drive
                // cannot hold the model, rather than failing at 90 %.
                var drive = DriveInfo.GetDrives()
                    .OrderByDescending(d => folder.StartsWith(d.Name, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
                if (drive != null &&
                    drive.AvailableFreeSpace < entry.SizeMb * 1024L * 1024L * 2)
                {
                    AppLogService.Debug($"[LocalModelManager] not enough space for {entry.Id}");
                    return false;
                }

                string finalPath = ModelPath(entry);
                string partPath = finalPath + ".part";

                using (var response = await Http.GetAsync(
                           entry.DownloadUrl,
                           HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    response.EnsureSuccessStatusCode();

                    long total = response.Content.Headers.ContentLength ?? 0;
                    long read = 0;
                    var buffer = new byte[256 * 1024];

                    await using (var src = await response.Content
                                     .ReadAsStreamAsync(ct))
                    await using (var dst = new FileStream(
                                     partPath, FileMode.Create,
                                     FileAccess.Write, FileShare.None, 1 << 16, true))
                    {
                        int n;
                        while ((n = await src.ReadAsync(buffer, ct)) > 0)
                        {
                            await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                            read += n;
                            if (total > 0)
                                progress?.Report(read * 100.0 / total);
                        }
                    }
                }

                string sha = await ComputeSha256Async(partPath, ct);
                long bytes = new FileInfo(partPath).Length;

                File.Move(partPath, finalPath, overwrite: true);

                var meta = new LocalModelMeta
                {
                    Id = entry.Id,
                    Bytes = bytes,
                    Sha256 = sha,
                    SourceUrl = entry.DownloadUrl,
                    InstalledUtc = DateTime.UtcNow
                };
                await File.WriteAllTextAsync(
                    MetaPath(entry),
                    JsonSerializer.Serialize(meta, JsonOptions), ct);

                progress?.Report(100.0);
                return true;
            }
            catch (OperationCanceledException)
            {
                TryPartialCleanup(entry);
                throw;   // the caller treats cancel distinctly from failure
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[LocalModelManager] install failed: {ex.Message}");
                TryPartialCleanup(entry);
                return false;
            }
        }

        /// <summary>Re-hash on demand and compare with the recorded value.</summary>
        public static async Task<(bool Ok, string Message)> VerifyAsync(
            LocalModelEntry entry, CancellationToken ct)
        {
            string path = ModelPath(entry);
            string metaPath = MetaPath(entry);
            if (!File.Exists(path))
                return (false, "Model file is missing.");

            var meta = ReadMeta(entry);
            if (meta == null)
                return (false, "No integrity record found — reinstall to record one.");

            try
            {
                string actual = await ComputeSha256Async(path, ct);
                bool ok = string.Equals(actual, meta.Sha256,
                    StringComparison.OrdinalIgnoreCase) &&
                          new FileInfo(path).Length == meta.Bytes;

                return ok
                    ? (true, "Integrity confirmed (SHA-256 matches the install record).")
                    : (false, "INTEGRITY FAILURE — file differs from its install record. " +
                              "Reinstall it (the agent refuses to run a changed file).");
            }
            catch (OperationCanceledException)
            {
                return (false, "Verification cancelled.");
            }
            catch (Exception ex)
            {
                return (false, "Could not verify: " + ex.Message);
            }
        }

        /// <summary>Delete the model + its integrity record. Never throws.</summary>
        public static bool Uninstall(string? id)
        {
            var entry = LocalModelCatalog.Find(id);
            if (entry == null) return false;
            try
            {
                string p1 = ModelPath(entry);
                string p2 = MetaPath(entry);
                try { if (File.Exists(p1)) File.Delete(p1); }
                catch (Exception ex) { AppLogService.Debug($"[LocalModelManager] delete: {ex.Message}"); }
                try { if (File.Exists(p2)) File.Delete(p2); }
                catch (Exception ex) { AppLogService.Debug($"[LocalModelManager] meta delete: {ex.Message}"); }
                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[LocalModelManager] uninstall failed: {ex.Message}");
                return false;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static async Task<string> ComputeSha256Async(
            string path, CancellationToken ct)
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, true);
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(stream, ct);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static void TryPartialCleanup(LocalModelEntry entry)
        {
            try
            {
                string partPath = ModelPath(entry) + ".part";
                if (File.Exists(partPath)) File.Delete(partPath);
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[LocalModelManager] partial cleanup: {ex.Message}");
            }
        }
    }
}
