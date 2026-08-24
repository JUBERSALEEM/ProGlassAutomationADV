// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Licensing/RegistryAnchor.cs
// PROJECT: ProGlassAutomation
// MODULE: Licensing — HKLM registry anchor (Windows-only)
//
// Anchor #3 in the multi-anchor trial ledger. HKLM_SOFTWARE is a
// machine-wide hive readable by any user without elevation, but
// writing HKLM SOFTWARE keys requires admin under standard UAC.
//
// GRACEFUL DEGRADATION
// ────────────────────
//   • Non-admin write ⇒ falls back to HKCU (per-user), which is
//     less strong but still adds ONE more anchor a user has to wipe.
//   • Non-Windows OS ⇒ every method is a no-op. The three other
//     anchors carry the load; the trial-lock still works, it just
//     has one fewer belt.
//
// WHY THE FALLBACK MATTERS
// ────────────────────────
// The AttackWith(admin=false) attacker can bypass HKLM but not the
// other three anchors. The AttackWith(admin=true) attacker can wipe
// HKLM too — at which point they must ALSO wipe %ProgramData%
// (usually restricted from non-admin writes for files owned by
// the installer) AND the DPAPI blob (LocalMachine scope — decryptable
// by any user but the ciphertext content is opaque without the DPAPI
// key stored in Windows' credential store). A user who runs an
// elevated cmd.exe and deletes everything the app owns is out of
// scope for this defence — at that point they own the machine and
// can install anything.
//
// The Windows-only surface is guarded via
// RuntimeInformation.IsOSPlatform(OSPlatform.Windows) so this file
// still compiles cleanly on non-Windows (the test project targets
// net8.0-windows so the compile side isn't a concern, but we keep
// the guard for defence in depth).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;

namespace ProGlassAutomation.Services.Licensing
{
    /// <summary>
    /// HKLM registry anchor for the trial ledger. See file header
    /// for the design rationale and graceful-degradation policy.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class RegistryAnchor
    {
        private const string KeyPath = @"SOFTWARE\ProGlassAutomation\Trial";
        private const string VFirstIssuedUtc = "FirstIssuedUtc";
        private const string VLastSeenUtc    = "LastSeenUtc";

        public sealed record Record
        {
            public DateTime FirstIssuedUtc { get; init; }
            public DateTime? LastSeenUtc   { get; init; }
        }

        /// <summary>
        /// Reads HKLM first, falls back to HKCU when HKLM has nothing.
        /// Returns null when neither hive has been written or the
        /// keys are missing. Never throws.
        /// </summary>
        public static Record? Read()
        {
            if (!IsWindows()) return null;

            var hklm = ReadFromHive(baseKey: Microsoft.Win32.Registry.LocalMachine);
            if (hklm != null) return hklm;

            return ReadFromHive(baseKey: Microsoft.Win32.Registry.CurrentUser);
        }

        private static Record? ReadFromHive(Microsoft.Win32.RegistryKey baseKey)
        {
            try
            {
                using var key = baseKey.OpenSubKey(KeyPath, writable: false);
                if (key == null) return null;

                string? issued = key.GetValue(VFirstIssuedUtc) as string;
                string? seen   = key.GetValue(VLastSeenUtc)    as string;

                if (string.IsNullOrEmpty(issued)) return null;

                if (!DateTime.TryParse(issued, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var firstIssued))
                    return null;

                DateTime? lastSeen = null;
                if (!string.IsNullOrEmpty(seen) &&
                    DateTime.TryParse(seen, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var s))
                {
                    lastSeen = s;
                }

                return new Record
                {
                    FirstIssuedUtc = firstIssued,
                    LastSeenUtc    = lastSeen
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Writes the "issued" record. Prefers HKLM; falls back to
        /// HKCU when the process cannot open HKLM for write (non-admin).
        /// Never throws — a failure is logged and returned false.
        /// </summary>
        public static bool Write(Record record)
        {
            if (!IsWindows()) return false;

            // Try HKLM first — machine-wide, strongest anchor.
            if (WriteToHive(Microsoft.Win32.Registry.LocalMachine, record, "HKLM"))
                return true;

            // Fall back to HKCU — per-user, but still adds an anchor
            // the attacker has to wipe.
            return WriteToHive(Microsoft.Win32.Registry.CurrentUser, record, "HKCU");
        }

        private static bool WriteToHive(
            Microsoft.Win32.RegistryKey baseKey, Record record, string label)
        {
            try
            {
                using var key = baseKey.CreateSubKey(KeyPath, writable: true);
                if (key == null) return false;

                key.SetValue(VFirstIssuedUtc,
                    record.FirstIssuedUtc.ToString("o"),
                    Microsoft.Win32.RegistryValueKind.String);
                if (record.LastSeenUtc.HasValue)
                {
                    key.SetValue(VLastSeenUtc,
                        record.LastSeenUtc.Value.ToString("o"),
                        Microsoft.Win32.RegistryValueKind.String);
                }

                AppLogService.Debug(
                    "[TrialLedger] Registry anchor written to " + label);
                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    "[TrialLedger] Registry anchor write to " + label + " failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Updates ONLY the LastSeenUtc value. If HKLM already has a
        /// FirstIssuedUtc, updates HKLM. Otherwise falls back to HKCU.
        /// Never throws.
        /// </summary>
        public static bool UpdateLastSeen(DateTime lastSeen)
        {
            if (!IsWindows()) return false;

            // Preserve whichever hive holds the current record.
            if (UpdateLastSeenInHive(Microsoft.Win32.Registry.LocalMachine, lastSeen))
                return true;
            return UpdateLastSeenInHive(Microsoft.Win32.Registry.CurrentUser, lastSeen);
        }

        private static bool UpdateLastSeenInHive(
            Microsoft.Win32.RegistryKey baseKey, DateTime lastSeen)
        {
            try
            {
                using var key = baseKey.OpenSubKey(KeyPath, writable: true);
                if (key == null) return false;
                // Only touch existing hive — do not create a new sub-key
                // for a heartbeat write.
                if (key.GetValue(VFirstIssuedUtc) == null) return false;
                key.SetValue(VLastSeenUtc, lastSeen.ToString("o"),
                    Microsoft.Win32.RegistryValueKind.String);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsWindows() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }
}
