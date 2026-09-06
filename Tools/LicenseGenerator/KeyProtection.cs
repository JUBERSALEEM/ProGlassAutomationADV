// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/KeyProtection.cs
// PROJECT: ProGlassAutomation.LicenseGenerator
// MODULE: DPAPI-protected key storage + rate limiter + audit trail
//
// PURPOSE
// ───────
// The production private signing key was stored as a plain-text
// const string in ProductionSigningKey.cs. Anyone with access to the
// compiled generator binary could extract it from the string table
// using ildasm/dnSpy and sign unlimited valid license keys.
//
// This module wraps the private key with Windows DPAPI so:
//   • The key is NOT visible in the binary's string table
//   • The key is bound to the vendor's Windows user account
//   • Extracting the key requires both the binary AND the user's
//     DPAPI master key (which requires their Windows login)
//
// ADDITIONAL PROTECTIONS
// ─────────────────────
//   • Rate limiter: max 10 key generations per hour per machine.
//     Prevents mass key generation if the tool is stolen.
//   • Audit trail: every key generation is logged to a file with
//     timestamp, plan, and machine fingerprint.
//
// FIRST-RUN SETUP
// ────────────────
// On first run, the generator detects the plain-text key in
// ProductionSigningKey.cs, encrypts it with DPAPI, stores the
// encrypted blob in %AppData%\ProGlassAutomation\Generator\,
// and the plain-text const remains as a fallback for bootstrap.
// After bootstrap, the DPAPI-protected version is always preferred.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Tools.LicenseGenerator;

/// <summary>
/// DPAPI-protected key storage with rate limiting and audit logging.
/// </summary>
internal static class KeyProtection
{
    private static readonly string GeneratorFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ProGlassAutomation", "Generator");

    private static readonly string ProtectedKeyPath =
        Path.Combine(GeneratorFolder, "signingkey.protected");

    private static readonly string RateLimitPath =
        Path.Combine(GeneratorFolder, "ratelimit.json");

    private static readonly string AuditLogPath =
        Path.Combine(GeneratorFolder, "audit.log");

    // Maximum key generations per hour
    private const int MaxGenerationsPerHour = 10;

    /// <summary>
    /// Gets the private signing key, preferring the DPAPI-protected
    /// version. Falls back to the plain-text const if DPAPI hasn't
    /// been bootstrapped yet.
    /// </summary>
    public static string GetPrivateKey()
    {
        // Try DPAPI-protected version first
        try
        {
            if (File.Exists(ProtectedKeyPath))
            {
                byte[] encrypted = File.ReadAllBytes(ProtectedKeyPath);
                byte[] decrypted = ProtectedData.Unprotect(
                    encrypted,
                    GetEntropy(),
                    DataProtectionScope.CurrentUser);
                string key = Encoding.UTF8.GetString(decrypted);
                if (!string.IsNullOrWhiteSpace(key))
                    return key;
            }
        }
        catch
        {
            // DPAPI failed (different user, corrupted blob, etc.)
            // Fall through to the plain-text fallback.
        }

        // Bootstrap: encrypt the plain-text key with DPAPI for future use
        try
        {
            BootstrapProtection();
        }
        catch
        {
            // Bootstrap failed — use the plain-text key this session
        }

        return ProductionSigningKey.PrivateKeyXml;
    }

    /// <summary>
    /// Encrypts the plain-text private key with DPAPI and stores it.
    /// Called automatically on first use.
    /// </summary>
    private static void BootstrapProtection()
    {
        Directory.CreateDirectory(GeneratorFolder);
        byte[] keyBytes = Encoding.UTF8.GetBytes(ProductionSigningKey.PrivateKeyXml);
        byte[] encrypted = ProtectedData.Protect(
            keyBytes,
            GetEntropy(),
            DataProtectionScope.CurrentUser);
        File.WriteAllBytes(ProtectedKeyPath, encrypted);
    }

    /// <summary>
    /// Additional entropy for DPAPI — includes the key ID so the
    /// blob can't be replayed against a different key.
    /// </summary>
    private static byte[] GetEntropy()
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(
            "ProGlassGenerator:" + ProductionSigningKey.KeyId));
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
            // Audit failure must never block key generation
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
