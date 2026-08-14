// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Security/SecretVault.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — secret storage (API keys)
//
// API keys are never stored in plain text. They are encrypted with
// Windows DPAPI (ProtectedData, CurrentUser scope) into secrets.dat:
// readable only by the same Windows user account on this machine.
//
// Behaviour:
//   • Store(key, secret)    — encrypt + persist
//   • TryRetrieve(key, out) — decrypt when needed
//   • Remove / RemoveAll    — user-driven cleanup (Settings)
//
// If "Keep keys saved" is turned off in Settings, the vault is cleared
// and keys exist only in memory for the running session.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProGlassAutomation.Services.Security
{
    /// <summary>DPAPI-backed secret store. Static facade, never throws.</summary>
    public static class SecretVault
    {
        private static readonly object _lock = new();

        // App-level entropy mixed into DPAPI so blobs from other apps
        // on the same machine cannot be replayed into this vault.
        private static readonly byte[] Entropy =
            Encoding.UTF8.GetBytes("ProGlassAutomation.v2.vault");

        private static string VaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "secrets.dat");

        /// <summary>Encrypts and stores one secret under a logical key.</summary>
        public static void Store(string key, string secret)
        {
            try
            {
                var map = Load();

                if (string.IsNullOrEmpty(secret))
                    map.Remove(key);
                else
                {
                    var plain = Encoding.UTF8.GetBytes(secret);
                    var cipher = ProtectedData.Protect(
                        plain, Entropy, DataProtectionScope.CurrentUser);
                    map[key] = Convert.ToBase64String(cipher);
                }

                Save(map);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"SecretVault.Store failed for '{key}': {ex.Message}",
                    new LogContext { Module = "Security", Class = nameof(SecretVault) });
            }
        }

        /// <summary>Decrypts one secret. Returns false when absent/unreadable.</summary>
        public static bool TryRetrieve(string key, out string secret)
        {
            secret = "";

            try
            {
                var map = Load();
                if (!map.TryGetValue(key, out var base64) ||
                    string.IsNullOrEmpty(base64))
                    return false;

                var cipher = Convert.FromBase64String(base64);
                var plain = ProtectedData.Unprotect(
                    cipher, Entropy, DataProtectionScope.CurrentUser);

                secret = Encoding.UTF8.GetString(plain);
                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"SecretVault.TryRetrieve failed for '{key}': {ex.Message}",
                    new LogContext { Module = "Security", Class = nameof(SecretVault) });
                return false;
            }
        }

        /// <summary>Deletes one secret.</summary>
        public static void Remove(string key)
        {
            try
            {
                var map = Load();
                if (map.Remove(key))
                    Save(map);
            }
            catch { }
        }

        /// <summary>Deletes all secrets (Settings → "Remove saved keys").</summary>
        public static void RemoveAll()
        {
            try
            {
                lock (_lock)
                {
                    if (File.Exists(VaultPath))
                        File.Delete(VaultPath);
                }
            }
            catch { }
        }

        // ── Persistence (encrypted values only — no plain text) ────────

        private static Dictionary<string, string> Load()
        {
            lock (_lock)
            {
                if (!File.Exists(VaultPath))
                    return new Dictionary<string, string>();

                try
                {
                    return JsonSerializer
                        .Deserialize<Dictionary<string, string>>(
                            File.ReadAllText(VaultPath))
                        ?? new Dictionary<string, string>();
                }
                catch
                {
                    return new Dictionary<string, string>();
                }
            }
        }

        private static void Save(Dictionary<string, string> map)
        {
            lock (_lock)
            {
                var folder = Path.GetDirectoryName(VaultPath)!;
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                File.WriteAllText(
                    VaultPath,
                    JsonSerializer.Serialize(map));
            }
        }
    }
}
