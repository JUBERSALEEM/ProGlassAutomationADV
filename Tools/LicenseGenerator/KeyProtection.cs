// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/KeyProtection.cs
// PROJECT: ProGlassAutomation.LicenseGenerator
// MODULE: Fail-closed signing gate + rate limiter + audit trail
//
// The previous production private key was exposed in tracked source and
// is treated as compromised. This generator must not read either the old
// DPAPI cache or an embedded fallback. License issuance stays disabled
// until an owner-approved replacement signing process is provisioned.
// Rate-limit and audit helpers remain for a future approved signer.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Tools.LicenseGenerator;

/// <summary>
/// Fail-closed production signing gate with retained rate-limit/audit helpers.
/// </summary>
internal static class KeyProtection
{
    private static readonly string GeneratorFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ProGlassAutomation", "Generator");

    private static readonly string RateLimitPath =
        Path.Combine(GeneratorFolder, "ratelimit.json");

    private static readonly string AuditLogPath =
        Path.Combine(GeneratorFolder, "audit.log");

    // Maximum key generations per hour
    private const int MaxGenerationsPerHour = 10;

    /// <summary>
    /// Fails closed while the exposed production key is being rotated.
    /// Existing DPAPI caches are deliberately ignored because they contain
    /// the same compromised key and must not re-enable license issuance.
    /// </summary>
    public static string GetPrivateKey()
    {
        throw new InvalidOperationException(
            "License issuance is disabled: the embedded production signing key was exposed. " +
            "Provision and approve a replacement signing key before issuing licenses.");
    }

    /// <summary>
    /// Checks the rate limiter. Returns true if key generation is
    /// allowed, false if the hourly limit has been reached.
    /// </summary>
    public static bool CheckRateLimit(out string? reason)
    {
        reason = null;
        try
        {
            var entries = LoadRateLimitEntries();
            var cutoff = DateTime.UtcNow.AddHours(-1);

            // Remove entries older than 1 hour
            entries.RemoveAll(e => e.Timestamp < cutoff);

            if (entries.Count >= MaxGenerationsPerHour)
            {
                reason = $"Rate limit reached: {entries.Count}/{MaxGenerationsPerHour} " +
                         $"keys generated in the last hour. Wait before generating more.";
                return false;
            }

            return true;
        }
        catch
        {
            return true; // Fail open — don't block on rate limit errors
        }
    }

    /// <summary>
    /// Records a key generation event for rate limiting and audit.
    /// </summary>
    public static void RecordGeneration(string planName, string armoredKeyPreview)
    {
        try
        {
            Directory.CreateDirectory(GeneratorFolder);

            // Rate limit entry
            var entries = LoadRateLimitEntries();
            entries.Add(new RateLimitEntry { Timestamp = DateTime.UtcNow });
            File.WriteAllText(RateLimitPath,
                JsonSerializer.Serialize(entries,
                    new JsonSerializerOptions { WriteIndented = true }));

            // Audit log entry
            string auditLine = string.Format(
                "{0:O} | PLAN={1} | MACHINE={2} | KEY={3}\n",
                DateTime.UtcNow,
                planName,
                MachineHash(),
                armoredKeyPreview.Length > 40
                    ? armoredKeyPreview.Substring(0, 40) + "..."
                    : armoredKeyPreview);
            File.AppendAllText(AuditLogPath, auditLine);
        }
        catch
        {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("RecordGeneration suppressed");
            }
    }

    private static List<RateLimitEntry> LoadRateLimitEntries()
    {
        try
        {
            if (File.Exists(RateLimitPath))
            {
                string json = File.ReadAllText(RateLimitPath);
                return JsonSerializer.Deserialize<List<RateLimitEntry>>(json)
                    ?? new List<RateLimitEntry>();
            }
        }
        catch (Exception ex)
        {
            // Rate-limit ledger is best-effort: a corrupt/unreadable file
            // degrades to an empty ledger (never blocks key generation),
            // but the failure must not be silent — the vendor must know
            // the limiter stopped tracking.
            AppLogService.Warning(
                "KeyProtection: rate-limit ledger unreadable, limiter degraded to empty — " + ex.Message);
        }
        return new List<RateLimitEntry>();
    }

    private static string MachineHash()
    {
        try
        {
            string machineId = Environment.MachineName + "|" +
                Environment.UserName + "|" +
                Environment.GetEnvironmentVariable("COMPUTERNAME");
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(machineId));
            return Convert.ToHexString(hash).Substring(0, 16);
        }
        catch (Exception ex)
        {
            // Fingerprint failure degrades to a shared "UNKNOWN" bucket so
            // key generation still works, but it must be observable — a
            // silent UNKNOWN collapses every machine into one rate bucket.
            AppLogService.Warning(
                "KeyProtection: machine fingerprint unavailable, using UNKNOWN — " + ex.Message);
            return "UNKNOWN";
        }
    }

    private sealed class RateLimitEntry
    {
        public DateTime Timestamp { get; set; }
    }
}
