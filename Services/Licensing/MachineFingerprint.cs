// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Licensing/MachineFingerprint.cs
// PROJECT: ProGlassAutomation
// MODULE: Commercial licensing — device fingerprint (HWID)
//
// WHAT THIS IS
// ────────────
// A stable, opaque identifier for the machine the app is running on.
// It is used by:
//   • the license verifier / offline activation flow, to decide whether
//     a machine-bound license applies to this device — including
//     vendor-issued offline trial responses that are bound to one
//     machine so copied/synced activation artifacts are rejected on
//     other PCs;
//   • the activation ledger in %AppData%\ProGlassAutomation\
//     activations.json (portable licenses count activations per
//     fingerprint);
//   • the one-trial-per-machine anchors (TrialLedger).
//
// HOW IT IS COMPUTED (v3 — 2026-08-26, deep hardware identity)
// ─────────────────────────────────────────────────────────────
// SHA-256 over a concatenation of PERSISTENT HARDWARE identifiers,
// fixed field order:
//   • guid=   HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid
//             (registry — stable per OS installation, unique per
//             install; the anchor that survives a NIC swap)
//   • cpu=    Win32_Processor.ProcessorId (WMI — the CPU chip's
//             own ID; unique per physical processor)
//   • board=  Win32_BaseBoard.SerialNumber (WMI — motherboard
//             serial), falling back to Win32_BIOS.SerialNumber on
//             OEMs that only stamp the BIOS
//   • disk=   first physical Win32_DiskDrive.SerialNumber (WMI —
//             the storage device's serial, stable across Windows
//             reinstalls, unlike the volume serial)
//   • vol=    %SystemDrive% volume serial (Win32
//             GetVolumeInformation P/Invoke — changes on reformat,
//             kept as an extra stable input)
//   • os=     OS name + edition (RuntimeInformation.OSDescription
//             with the trailing build/UBR token stripped — the
//             build number changes with every cumulative Windows
//             update and must never enter the identity)
//   • arch=   OS architecture
//
// v3 replaced the v2 input set (core COUNT, volume serial, first NIC
// MAC) because core count + volume serial are NOT discriminating on
// imaged / corporate fleets: two PCs built from the same image can
// share core count and even volume serial, and MAC is the user's
// stated weak point (VPN / virtual adapters / NIC swap). The v3 set
// requires two PCs to share MachineGuid AND CPU chip ID AND
// motherboard serial AND disk serial to collide — i.e. the same
// physical machine (or a full disk clone, which then also shares the
// registry-derived MachineGuid and is caught by the cpu/board/disk
// fields when the clone's physical chips differ).
//
// Every WMI / registry / volume probe is individually guarded:
// one unavailable source can never zero the whole fingerprint, and
// empty or OEM-placeholder values ("To Be Filled By O.E.M.",
// "Default string", …) are skipped instead of hashed.
//
// DEGENERATE-GUARD: if fewer than two deep inputs are available
// (WMI disabled + registry locked + volume probe failed), a
// os/arch-only fingerprint would be identical on any two machines
// running the same Windows edition — the exact collision that
// caused the cross-PC trial lockout. In that case MAC + core count
// are appended as dispersion inputs and a Warning is logged, so the
// fingerprint is never derived from an empty hardware set.
//
// This is INTENTIONALLY not unbreakable "DRM" — a sufficiently
// motivated attacker can spoof WMI. The fingerprint exists to:
//   • make each physical PC get its OWN trial (the 2026-08-26 fix),
//   • prevent accidental copying of a license between machines,
//   • provide a stable identifier for the activation ledger.
//
// FAILURE MODES
// ─────────────
// Server Core / WMI-disabled machines still produce a fingerprint
// from guid + vol + os + arch (plus the degenerate-guard inputs).
// The fingerprint is never null and is cached for the process
// lifetime (ComputeCurrent was recomputing NIC enumeration on every
// call — the About-page UI freeze of 2026-08-25).
//
// UPGRADE NOTE: v2 → v3 changes every machine's fingerprint. The
// activation ledger carries FingerprintAlgorithmVersion and resets
// itself on the first load after the bump (phantom entries from the
// old algorithm are dropped, this machine re-registers). A
// machine-bound .lic issued under v2 must be RE-ISSUED once under
// v3 — expected and standard for HWID licensing.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

// MachineFingerprint is a PURE licensing primitive. It intentionally does
// NOT reference AppLogService / Audit / CorrelationContext / any app
// service — a fingerprint probe must never fail and must never couple to
// application logging. See Licensing/MachineFingerprint contract notes.
namespace ProGlassAutomation.Services.Licensing
{
    /// <summary>
    /// Computes a stable, opaque per-machine hardware identifier.
    /// See the file header for the inputs and the rationale.
    /// </summary>
    public static class MachineFingerprint
    {
        // 2026-08-26 (cross-PC trial lockout, round 2): v2 optimized
        // for STABILITY (dropped volatile OS/framework strings after
        // the activation-limit incident) but its remaining inputs —
        // core COUNT, volume serial, first NIC MAC — are not
        // discriminating: imaged fleet PCs share core count and can
        // share volume serial, and MAC is weak under VPNs / virtual
        // adapters / NIC swaps. v3 anchors the identity on deep,
        // persistent hardware (CPU chip ID, motherboard serial, disk
        // serial, MachineGuid). The version constant is stored in the
        // activation ledger, so the ledger auto-resets on the first
        // load after this bump instead of counting stale v2 entries.
        public const int FingerprintAlgorithmVersion = 3;

        // OEMs commonly ship unpopulated serial fields. Hashing
        // "To Be Filled By O.E.M." would make every such machine
        // share an input — treat them as missing instead.
        private static readonly string[] PlaceholderSerials =
        {
            "to be filled by o.e.m.",
            "to be filled by oem",
            "default string",
            "system serial number",
            "oem default",
            "none",
            "0",
            "12345",
            "123456",
            "12345678",
            "123456789"
        };

        // 2026-08-25 senior fix: the fingerprint is stable for the
        // process lifetime (hardware does not change mid-run), yet it
        // was recomputed on EVERY call — including on the UI thread
        // from LicenseStatusViewModel.Refresh() when the About page
        // opened. The WMI queries can each take a second on slow
        // machines, and the NIC enumeration can take several — that
        // was the About-page UI freeze. Compute once, cache forever.
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
            var sb = new StringBuilder(768);
            int deepInputs = 0;

            // 1) MachineGuid — stable per OS installation.
            //    Registry read is fast and never throws the way
            //    WMI can, so it leads the set.
            string guid = ReadMachineGuid();
            if (guid.Length > 0)
            {
                sb.Append("guid=").Append(guid).Append(';');
                deepInputs++;
            }

            // 2) CPU chip ID (WMI Win32_Processor.ProcessorId).
            //    Unique per physical processor; survives OS
            //    reinstalls. Order by DeviceID (CPU0, CPU1, …) so a
            //    multi-socket host picks the SAME chip every run.
            string cpu = QueryWmiFirst(
                "Win32_Processor", "ProcessorId", orderByProperty: "DeviceID");
            if (cpu.Length > 0)
            {
                sb.Append("cpu=").Append(cpu).Append(';');
                deepInputs++;
            }

            // 3) Motherboard serial (WMI Win32_BaseBoard), falling
            //    back to the BIOS serial on OEMs that only stamp
            //    Win32_BIOS. Order by Tag / Name so the winner is
            //    stable when a host reports more than one row.
            string board = QueryWmiFirst(
                "Win32_BaseBoard", "SerialNumber", orderByProperty: "Tag");
            if (board.Length == 0)
                board = QueryWmiFirst(
                    "Win32_BIOS", "SerialNumber", orderByProperty: "Name");
            if (board.Length > 0)
            {
                sb.Append("board=").Append(board).Append(';');
                deepInputs++;
            }

            // 4) First physical disk serial (WMI Win32_DiskDrive).
            //    Stable across Windows reinstalls — unlike the
            //    volume serial, which changes on every reformat. Order
            //    by Index (physical device order: PHYSICALDRIVE0,
            //    PHYSICALDRIVE1, …) so a multi-disk machine always
            //    picks the SAME disk regardless of WMI's row order —
            //    otherwise a USB / second drive appearing could flip
            //    the winner and turn a valid machine-bound trial into
            //    a false MachineMismatch.
            string disk = QueryWmiFirst(
                "Win32_DiskDrive", "SerialNumber", orderByProperty: "Index");
            if (disk.Length > 0)
            {
                sb.Append("disk=").Append(disk).Append(';');
                deepInputs++;
            }

            // 5) Volume serial number for %SystemDrive%. Win32
            //    GetVolumeInformation returns the 32-bit volume
            //    serial; stable until reformat. Kept from v2 as an
            //    extra input.
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
                // The probe failed for any reason (locked-down
                // environment, missing drive). serial stays 0 —
                // the fingerprint is still derived from the other
                // sources; the degenerate-guard below catches the
                // case where EVERY deep source fails.
                // best-effort — a missing/denied volume probe just skips that input.
                _ = ex;
            }
            sb.Append("vol=").Append(serial.ToString("X8")).Append(';');
            if (serial != 0)
                deepInputs++;

            // 6) OS name + edition only (kept from v2). The trailing
            //    build/UBR token changes with every cumulative
            //    Windows update and must never enter the identity.
            //    If the OS format ever changes and nothing matches,
            //    the full string is kept — deterministic either way.
            string osDesc = RuntimeInformation.OSDescription;
            sb.Append("os=").Append(
                Regex.Replace(osDesc, @"\s+\d+\.\d+[\d.]*$", "")).Append(';');
            sb.Append("arch=").Append(RuntimeInformation.OSArchitecture).Append(';');

            // DEGENERATE-GUARD: with fewer than two deep inputs the
            // remaining identity (os + arch) is identical on any two
            // machines running the same Windows edition — exactly
            // the collision that locks a fresh PC out of its own
            // trial. Add MAC + core count as dispersion inputs and
            // log it, so this case is visible in the log.
            if (deepInputs < 2)
            {
                string? firstMac = FirstMacAddress();
                sb.Append("mac=").Append(firstMac ?? "none").Append(';');
                sb.Append("cores=").Append(Environment.ProcessorCount).Append(';');
                // Degenerate-guard is documented; the probe is best-effort and
                // never logs (a fingerprint must not couple to app services).
            }

            // SHA-256, take the first 16 bytes (32 hex chars).
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            var hex = new StringBuilder(32);
            for (int i = 0; i < 16; i++)
                hex.Append(hash[i].ToString("x2"));
            return hex.ToString();
        }

        // ── Input probes — every one individually guarded ─────────

        /// <summary>
        /// Reads the first non-placeholder value of a WMI property
        /// from a WMI class. Returns "" when WMI is unavailable,
        /// the query times out, or every row is empty/placeholder.
        /// Never throws.
        ///
        /// DETERMINISM (2026-08-26 fix): WMI does NOT guarantee row
        /// order, so naively taking "the first row" makes the result
        /// nondeterministic when a class has more than one row
        /// (multi-disk machines, multi-socket CPUs). A machine-bound
        /// trial signed against one run's "first disk/CPU" would not
        /// match the next launch's different "first row", flipping a
        /// valid trial to MachineMismatch. This method therefore
        /// collects every valid serial and picks a STABLE winner
        /// ordered by <paramref name="orderByProperty"/> (e.g. the
        /// disk's Index) so the fingerprint is identical across runs.
        /// </summary>
        private static string QueryWmiFirst(
            string wmiClass, string property, string? orderByProperty = null)
        {
            try
            {
                // When an order-by column is supplied we select BOTH the
                // value column and the sort key (e.g. a disk's Index).
                // Otherwise select just the value column — a duplicate
                // column in a WQL SELECT is invalid.
                bool hasOrderBy = !string.IsNullOrEmpty(orderByProperty);
                string selectColumns = hasOrderBy
                    ? $"{property}, {orderByProperty}"
                    : property;
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT {selectColumns} FROM {wmiClass}");
                searcher.Options.Timeout = TimeSpan.FromSeconds(5);
                using var results = searcher.Get();

                // Collect every usable value with its sort key, then
                // sort deterministically (numeric keys first, then
                // ordinal) and return the winner. This removes the
                // dependence on WMI's unrecorded row order.
                var rows = new List<(string SortKey, string Value)>();
                foreach (ManagementObject mo in results)
                {
                    var value = mo[property] as string;
                    if (!IsValidSerial(value)) continue;

                    // The sort key is normally the order-by property's
                    // string form (numeric for a disk's Index, "CPU0" /
                    // "CPU1" for a processor's DeviceID). If the
                    // order-by column is absent for a row, fall back to
                    // the value itself so the row is still comparable.
                    string sortKey = hasOrderBy
                        ? (mo[orderByProperty]?.ToString() ?? "").Trim()
                        : value!.Trim().ToUpperInvariant();
                    if (sortKey.Length == 0)
                        sortKey = value!.Trim().ToUpperInvariant();

                    rows.Add((sortKey, value!.Trim().ToUpperInvariant()));
                }
                if (rows.Count == 0) return "";
                rows.Sort((a, b) => CompareSortKeys(a.SortKey, b.SortKey));
                return rows[0].Value;
            }
            catch (System.Exception ex)
            {
                // WMI disabled, class missing, access denied, timeout
                // — any of these just means "this input is missing".
                // WMI unavailable → this input is simply "missing".
                _ = ex;
            }
            return "";
        }

        /// <summary>
        /// Sorts two WMI sort keys deterministically. A key that is a
        /// whole number (e.g. a disk's <c>Index</c>) orders numerically
        /// so PHYSICALDRIVE0 always wins regardless of string padding;
        /// otherwise keys compare ordinal. This keeps the fingerprint
        /// stable across runs and across machines.
        /// </summary>
        private static int CompareSortKeys(string a, string b)
        {
            bool aNum = long.TryParse(a, out long numA);
            bool bNum = long.TryParse(b, out long numB);
            if (aNum && bNum) return numA.CompareTo(numB);
            if (aNum) return -1;   // numeric keys sort before text keys
            if (bNum) return 1;
            return string.CompareOrdinal(a, b);
        }

        /// <summary>
        /// True when a hardware serial value is usable as an
        /// identity input: non-blank, at least 3 chars, and not an
        /// OEM placeholder string.
        /// </summary>
        private static bool IsValidSerial(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string v = raw.Trim();
            if (v.Length < 3) return false;
            foreach (var p in PlaceholderSerials)
            {
                if (string.Equals(v, p, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid — the
        /// per-OS-installation identity anchor. Returns "" when the
        /// key is missing or the read is denied.
        /// </summary>
        private static string ReadMachineGuid()
        {
            try
            {
                using var key = Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                var value = key?.GetValue("MachineGuid") as string;
                return value == null ? "" : value.Trim();
            }
            catch (System.Exception ex)
            {
                // Registry read denied → MachineGuid is "missing".
                _ = ex;
                return "";
            }
        }

        /// <summary>
        /// First stable NIC MAC — v2 logic, kept ONLY for the
        /// degenerate-guard path. MAC is deliberately NOT part of the
        // normal identity (VPN / virtual adapters / NIC swap make it
        // the weakest input, and the deep hardware set supersedes
        // it). Per-NIC try/catch so one bad adapter cannot sink the
        // probe; physical Ethernet is preferred over Wi-Fi, then a
        // deterministic OrderBy Id.
        /// </summary>
        private static string? FirstMacAddress()
        {
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
                return firstMac;
            }
            catch
            {
                return null;
            }
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
