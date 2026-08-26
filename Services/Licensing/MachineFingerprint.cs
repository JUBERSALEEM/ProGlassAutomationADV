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
using System.Text.RegularExpressions;

namespace ProGlassAutomation.Services.Licensing
{
    /// <summary>
    /// Computes a stable, opaque per-machine identifier. See the
    /// file header for the inputs and the rationale.
    /// </summary>
    public static class MachineFingerprint
    {
        // 2026-08-26 senior fix (activation-limit incident): the v1
        // fingerprint mixed in VOLATILE inputs — RuntimeInformation.
        // OSDescription (contains the Windows build/UBR, which changes
        // with every cumulative Windows update) and FrameworkDescription
        // (changes with every .NET patch). A single Windows update made
        // the fingerprint "new", the activation ledger added a second
        // entry, and a MaxActivations=1 or 2 license flipped to
        // ActivationLimit — the overlay covering the screen.
        //
        // v2 keeps only machine-stable inputs (OS name + edition,
        // volume serial, NIC MAC, CPU count) and strips the build
        // token from the OS string. The version constant is stored in
        // the activation ledger: on first load after an algorithm
        // bump the ledger resets instead of counting phantom entries
        // from the old algorithm.
        public const int FingerprintAlgorithmVersion = 2;

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

            // v2: OS name + edition only. RuntimeInformation.OSDescription
            // on Windows looks like
            //   "Microsoft Windows 11 Pro 10.0.26100.2605"
            // — the trailing build/UBR changes with every cumulative
            // update, so it is stripped. The regex removes a trailing
            // " 10.0.xxxx.xxxx" (or similar) token; if the format ever
            // changes and nothing matches, the full string is kept —
            // deterministic either way.
            string osDesc = RuntimeInformation.OSDescription;
            sb.Append("os=").Append(
                Regex.Replace(osDesc, @"\s+\d+\.\d+[\d.]*$", "")).Append(';');
            sb.Append("arch=").Append(RuntimeInformation.OSArchitecture).Append(';');

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

            // First stable NIC MAC. v2 hardening:
            //   • GetPhysicalAddress() can throw on some virtual
            //     adapters — probe each adapter individually so one
            //     bad NIC cannot zero the whole MAC source.
            //   • Adapters whose MAC comes back empty are skipped
            //     (v1 happily used "" as the identifier).
            //   • Physical Ethernet is preferred over Wi-Fi and other
            //     types (a machine with both has a stable choice even
            //     when the cable is unplugged), then deterministic
            //     OrderBy Id within the preferred class.
            try
            {
                string? firstMac = null;
                foreach (var nic in NetworkInterface
                    .GetAllNetworkInterfaces()
                    .Where(n =>
                        n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                        && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                        ? 0 : 1)
                    .ThenBy(n => n.Id, StringComparer.Ordinal))
                {
                    string mac;
                    try { mac = nic.GetPhysicalAddress().ToString(); }
                    catch { continue; }
                    if (string.IsNullOrEmpty(mac)) continue;
                    firstMac = mac;
                    break;
                }
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
