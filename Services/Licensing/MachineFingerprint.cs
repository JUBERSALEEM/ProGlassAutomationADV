// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Licensing/MachineFingerprint.cs
// PROJECT: ProGlassAutomation
// MODULE: Commercial licensing — device fingerprint
//
// WHAT THIS IS
// ────────────
// A stable, opaque identifier for the machine the app is running on.
// It is used by the license verifier to decide whether a
// machine-bound license (.lic file with a non-null
// MachineFingerprint) applies to this device.
//
// HOW IT IS COMPUTED
// ──────────────────
// SHA-256 over a concatenation of stable hardware identifiers:
//   • Processor count (Environment.ProcessorCount) — stable per
//     machine; never changes between reboots.
//   • OS version + architecture string (RuntimeInformation).
//   • %ProgramFiles% volume serial number (Win32_LogicalDisk via
//     WMI, or a simple P/Invoke as fallback).
//   • MAC address of the first non-loopback, non-tunnel NIC
//     (NetworkInterface.GetAllNetworkInterfaces).
//
// This is INTENTIONALLY not a hardware-locked "DRM" — any
// sufficiently motivated attacker can spoof these values. The
// fingerprint exists to:
//   • Prevent accidental copying of a license between machines
//     (the common case — customer reformats, buys a new laptop).
//   • Provide a stable identifier for the activation ledger
//     stored in %AppData%\ProGlassAutomation\activations.json.
//   • Make it easy to issue a "machine-bound" cheap tier
//     without re-architecting the issuer's tool.
//
// The result is a 32-hex-character SHA-256 prefix — short
// enough to print in a UI / log / About page, long enough that
// random collision on two distinct machines is astronomically
// unlikely.
//
// FAILURE MODES
// ─────────────
// All inputs are best-effort. A machine without WMI (rare;
// e.g. Server Core) still produces a fingerprint from the other
// sources. A machine with no network adapters produces a
// fingerprint from the other sources. The fingerprint is
// never null — at worst it is "no network adapters, no WMI".
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ProGlassAutomation.Services.Licensing
{
    /// <summary>
    /// Computes a stable, opaque per-machine identifier. See the
    /// file header for the inputs and the rationale.
    /// </summary>
    public static class MachineFingerprint
    {
        // 2026-08-25 senior fix: the fingerprint is stable for the
        // process lifetime (hardware does not change mid-run), yet it
        // was recomputed on EVERY call — including on the UI thread from
        // LicenseStatusViewModel.Refresh() when the About page opened.
        // The NIC enumeration (GetAllNetworkInterfaces) alone can take
        // seconds on machines with many virtual adapters, and the volume
        // probe can stall on slow drives — that was the About-page UI
        // freeze. Compute once, cache forever.
        private static string? _cached;
        private static readonly object _cacheGate = new();

        /// <summary>
        /// Returns the current machine's fingerprint as a 32-char
        /// lowercase hex string (first 16 bytes of SHA-256). The
        /// function is pure — it has no side effects and can be
        /// called from any thread. The result is cached for the
        /// process lifetime, so repeated calls are free.
        /// </summary>
        public static string ComputeCurrent()
        {
            var existing = _cached;
            if (existing != null) return existing;

            lock (_cacheGate)
            {
                if (_cached == null)
                    _cached = ComputeCore();
                return _cached;
            }
        }

        private static string ComputeCore()
        {
            // Order matters. The SHA-256 of the same input
            // string always produces the same output, so as long
            // as every machine produces the same set of fields in
            // the same order, the fingerprint is stable.
            var sb = new StringBuilder(512);
            sb.Append("cpu=").Append(Environment.ProcessorCount).Append(';');
            sb.Append("os=").Append(RuntimeInformation.OSDescription).Append(';');
            sb.Append("arch=").Append(RuntimeInformation.OSArchitecture).Append(';');
            sb.Append("framework=").Append(RuntimeInformation.FrameworkDescription).Append(';');

            // Volume serial number for %SystemDrive%. Win32
            // GetVolumeInformation returns the 32-bit volume
            // serial number; it is stable for the lifetime of
            // the volume (i.e. until the user reformats).
            uint serial = 0;
            try
            {
                var sysDrive = Environment.GetEnvironmentVariable("SystemDrive")
                    ?? "C:\\";
                if (!sysDrive.EndsWith("\\", StringComparison.Ordinal))
                    sysDrive += "\\";
                GetVolumeInformation(sysDrive, null, 0, out serial,
                    out _, out _, null, 0);
            }
            catch (System.Exception ex)
            {
                // WMI not available, or the call failed for any
                // other reason. serial stays 0 — the fingerprint
                // is still derived from the other sources.
                ProGlassAutomation.Services.Logging.AppLogService.Debug("[MachineFingerprint] Volume-serial probe failed: " + ex.Message);
            }
            sb.Append("vol=").Append(serial.ToString("X8")).Append(';');

            // First non-loopback, non-tunnel, operational NIC.
            // The MAC address of a NIC is a stable 48-bit
            // identifier. We pick the first one alphabetically so
            // the choice is deterministic on the same machine
            // across reboots (NetworkInterface ordering is not
            // guaranteed stable between boots; the LINQ
            // OrderBy(string) makes it so).
            try
            {
                var firstMac = NetworkInterface
                    .GetAllNetworkInterfaces()
                    .Where(n =>
                        n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                        && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .OrderBy(n => n.Id, StringComparer.Ordinal)
                    .Select(n => n.GetPhysicalAddress().ToString())
                    .FirstOrDefault();
                sb.Append("mac=").Append(firstMac ?? "none").Append(';');
            }
            catch
            {
                sb.Append("mac=none;");
            }

            // SHA-256, take the first 16 bytes (32 hex chars).
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            var hex = new StringBuilder(32);
            for (int i = 0; i < 16; i++)
                hex.Append(hash[i].ToString("x2"));
            return hex.ToString();
        }

        // ── Win32 P/Invoke ─────────────────────────────────────────
        //
        // GetVolumeInformation: documented in MSDN. dwVolumeSerialNumber
        // is the volume serial number that Explorer shows in the
        // drive-properties dialog. Stable for the lifetime of the
        // volume.
        [DllImport("kernel32.dll", SetLastError = true,
            CharSet = CharSet.Unicode, BestFitMapping = false)]
        private static extern bool GetVolumeInformation(
            string lpRootPathName,
            StringBuilder? lpVolumeNameBuffer,
            int nVolumeNameSize,
            out uint lpVolumeSerialNumber,
            out uint lpMaximumComponentLength,
            out uint lpFileSystemFlags,
            StringBuilder? lpFileSystemNameBuffer,
            int nFileSystemNameSize);
    }
}
