using Microsoft.Data.Sqlite;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Database
{
    public sealed class SharedDataRootProbeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string NormalizedPath { get; init; } = "";
        public bool LooksLikeNetworkPath { get; init; }
    }

    /// <summary>
    /// Professional LAN/Wi-Fi data-root helper. This does not turn SQLite
    /// into a database server; it safely points all company workstations at
    /// the same shared folder and verifies read/write/delete access first.
    ///
    /// SENIOR AUDIT (2026-09-24) — the shared folder preparation used to
    /// corrupt the company database in three quiet ways:
    ///   1. It copied the live -wal and -shm sidecars of the running app.
    ///      -shm is per-process shared-memory state (SQLite documentation:
    ///      never copy it), and WAL frames captured mid-transaction leave
    ///      the copied .db inconsistent.
    ///   2. It left the copied databases in WAL journal mode. WAL across
    ///      an SMB/NAS redirector is unsafe — the -shm coordination file
    ///      is not reliable over a network share, and two workstations can
    ///      diverge silently.
    ///   3. ApplyToConfig persisted UseSharedDataRoot=true with an empty
    ///      path when validation failed, leaving the next startup without
    ///      a usable shared target. That invalid configuration is still
    ///      rejected; a valid share that later goes offline now selects a
    ///      visible local fallback instead of terminating the application.
    /// All three are fixed here: checkpoint the local databases first,
    /// copy only the .db files, convert every shared database to
    /// rollback-journal (DELETE) mode — persisted in the SQLite header —
    /// and refuse to save a half-configured shared mode.
    /// </summary>
    public static class SharedDataRootService
    {
        private const string ManifestFileName = "proglass_shared_root.json";
        private const string ShareName = "ProGlassData";
        private const string FirewallRuleName = "ProGlassAutomation-SMB-LocalSubnet";
        private static readonly string[] ExtraDataSubfolders =
            { "Inquiries", "ProformaInvoices", "DeliveryNotes" };

        public static SharedDataRootProbeResult Probe(string? folder)
        {
            if (IsServerPlaceholderPath(folder)
                || !AppDataRoot.TryNormalizeSharedRoot(folder, out var normalized)
                || !LooksNetworkBased(normalized))
                return ProbeCore(folder);

            var probe = Task.Run(() => ProbeCore(normalized));
            probe.ContinueWith(
                task => { _ = task.Exception; },
                TaskContinuationOptions.OnlyOnFaulted);
            if (!probe.Wait(TimeSpan.FromSeconds(4)))
            {
                const string timeoutMessage =
                    "✗ The shared folder did not respond within 4 seconds. Check that the host is on, this PC is on the same LAN/Wi-Fi, SMB/TCP 445 is allowed, and the Windows credentials and permissions are valid. No data root was changed.";
                AppLogService.Warning(
                    "Shared data-root Settings probe exceeded four seconds. Path: " + normalized,
                    new TimeoutException(timeoutMessage),
                    new LogContext { Module = "Database", Operation = "ProbeSharedDataRoot" });
                return new SharedDataRootProbeResult
                {
                    Ok = false,
                    NormalizedPath = normalized,
                    LooksLikeNetworkPath = true,
                    Message = timeoutMessage
                };
            }

            try { return probe.GetAwaiter().GetResult(); }
            catch (Exception ex)
            {
                return new SharedDataRootProbeResult
                {
                    Ok = false,
                    NormalizedPath = normalized,
                    LooksLikeNetworkPath = true,
                    Message = "✗ Shared folder probe failed: " + AppDataRoot.DescribeShareFailure(ex)
                };
            }
        }

        private static SharedDataRootProbeResult ProbeCore(string? folder)
        {
            if (IsServerPlaceholderPath(folder))
            {
                return new SharedDataRootProbeResult
                {
                    Ok = false,
                    Message = "✗ \\\\SERVER is only an example, not a real computer name. On the primary PC click Create Share on This PC; on a client click Find on LAN or enter the actual host name/IP, such as \\\\OFFICE-PC\\ProGlassData or \\\\192.168.1.25\\ProGlassData.",
                    NormalizedPath = folder ?? "",
                    LooksLikeNetworkPath = true
                };
            }

            if (!AppDataRoot.TryNormalizeSharedRoot(folder, out var normalized))
            {
                return new SharedDataRootProbeResult
                {
                    Ok = false,
                    Message = "Enter a shared folder different from the local ProGlass AppData folder. Example: \\\\HOSTNAME\\ProGlassData or \\\\192.168.1.25\\ProGlassData. Replace the examples with the primary PC name/IP; the literal \\\\SERVER is not a real host name.",
                    NormalizedPath = folder ?? ""
                };
            }

            bool network = LooksNetworkBased(normalized);

            try
            {
                Directory.CreateDirectory(normalized);
                var testFile = Path.Combine(normalized,
                    ".proglass_network_write_test_" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.WriteAllText(testFile,
                        "ProGlass shared database write test " + DateTime.UtcNow.ToString("o"));
                    _ = File.ReadAllText(testFile);
                    File.Delete(testFile);
                }
                catch
                {
                    try { if (File.Exists(testFile)) File.Delete(testFile); }
                    catch { /* Preserve the read/write/delete failure. */ }
                    throw;
                }

                string[] databaseFiles = Directory.GetFiles(
                    normalized, "*.db", SearchOption.TopDirectoryOnly);
                string dataHint;
                if (databaseFiles.Length > 0)
                {
                    string shownNames = string.Join(", ", databaseFiles
                        .Select(file => Path.GetFileName(file))
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Take(5));
                    string moreFiles = databaseFiles.Length > 5
                        ? $", and {databaseFiles.Length - 5} more"
                        : "";
                    dataHint = $" {databaseFiles.Length} database file(s) found: {shownNames}{moreFiles}.";
                }
                else
                {
                    dataHint = " No database files found yet; Prepare Folder seeds local data only when the host operator explicitly checks Copy Local (off by default).";
                }
                string manifestHint = DescribeManifest(normalized);

                return new SharedDataRootProbeResult
                {
                    Ok = true,
                    NormalizedPath = normalized,
                    LooksLikeNetworkPath = network,
                    Message = (network
                        ? "✓ Shared data folder is reachable and writable on the network."
                        : "✓ This local folder is writable, but it is not a network share and clients cannot use this local path. On the primary PC choose Create Share on This PC, then test the returned UNC path.")
                        + dataHint + manifestHint + " Restart ProGlass on each workstation after enabling."
                };
            }
            catch (Exception ex)
            {
                return new SharedDataRootProbeResult
                {
                    Ok = false,
                    NormalizedPath = normalized,
                    LooksLikeNetworkPath = network,
                    Message = "✗ Shared folder is not ready: " + AppDataRoot.DescribeShareFailure(ex)
                };
            }
        }

        private static string DescribeManifest(string folder)
        {
            string manifestPath = Path.Combine(folder, ManifestFileName);
            if (!File.Exists(manifestPath)) return " Shared-root manifest not found yet.";

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                var root = document.RootElement;
                if (!root.TryGetProperty("Product", out var product)
                    || !string.Equals(product.GetString(),
                        "ProGlass Automation", StringComparison.OrdinalIgnoreCase))
                    return " A manifest file exists, but it is not identified as a ProGlass Automation shared root.";

                string workstation = root.TryGetProperty("Workstation", out var host)
                    ? host.GetString() ?? ""
                    : "";
                return string.IsNullOrWhiteSpace(workstation)
                    ? " ProGlass shared-root manifest found."
                    : " ProGlass shared-root manifest found (last prepared by " + workstation + ").";
            }
            catch
            {
                return " A shared-root manifest file exists but could not be read.";
            }
        }

        /// <summary>
        /// A folder is "network based" when it is a UNC path or a local
        /// drive letter the Windows redirector reports as a network drive
        /// (a mapped LAN/Wi-Fi share).
        /// </summary>
        public static bool LooksNetworkBased(string path) =>
            !string.IsNullOrWhiteSpace(path)
            && (path.Trim().StartsWith(@"\\", StringComparison.Ordinal)
                || !string.IsNullOrEmpty(AppDataRoot.GetMappedNetworkPath(path)));

        /// <summary>
        /// A folder this PC can create and share. Never the local AppData
        /// root — that path is rejected by <see cref="AppDataRoot.TryNormalizeSharedRoot"/>.
        /// </summary>
        public static string SuggestShareableFolder()
        {
            // Public Documents is writable by standard users on a normal
            // Windows installation. Publishing the SMB share itself is an
            // elevated operation, but creating the underlying folder should
            // not require the whole application to run as administrator.
            string publicDocuments = Environment.GetFolderPath(
                Environment.SpecialFolder.CommonDocuments);
            if (string.IsNullOrWhiteSpace(publicDocuments))
            {
                string publicRoot = Environment.GetEnvironmentVariable("PUBLIC") ?? "";
                if (!string.IsNullOrWhiteSpace(publicRoot))
                    publicDocuments = Path.Combine(publicRoot, "Documents");
            }
            if (string.IsNullOrWhiteSpace(publicDocuments))
            {
                string? systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
                publicDocuments = Path.Combine(
                    string.IsNullOrWhiteSpace(systemRoot) ? @"C:\" : systemRoot,
                    "Users", "Public", "Documents");
            }

            return Path.Combine(publicDocuments, "ProGlassData");
        }

        /// <summary>UNC path clients use to reach this PC's ProGlass SMB share.</summary>
        public static string BuildHostSharePath(string? hostName = null)
        {
            string host = string.IsNullOrWhiteSpace(hostName)
                ? Environment.MachineName
                : hostName.Trim();
            if (host.IndexOfAny(new[] { '\\', '/' }) >= 0)
                throw new ArgumentException("Enter a computer name or IPv4 address, not a path.", nameof(hostName));
            return "\\\\" + host + "\\" + ShareName;
        }

        /// <summary>
        /// Stable host-name path first, followed by private IPv4 alternatives
        /// for clients on networks where Windows name resolution is disabled.
        /// </summary>
        public static IReadOnlyList<string> GetHostShareConnectionPaths()
        {
            var paths = new List<string> { BuildHostSharePath() };
            foreach (var address in GetLocalPrivateIPv4Addresses().Take(4))
            {
                string path = BuildHostSharePath(address.ToString());
                if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase))
                    paths.Add(path);
            }
            return paths;
        }

        /// <summary>
        /// True only when the selected path is this PC's own ProGlassData
        /// share root. Folder preparation must not copy one client's local
        /// dataset into another host's company share.
        /// </summary>
        public static bool IsCurrentComputerShareRoot(string? path)
        {
            if (!AppDataRoot.TryNormalizeSharedRoot(path, out string normalized))
                return false;

            string unc = normalized.TrimEnd('\\', '/');
            if (!unc.StartsWith(@"\\", StringComparison.Ordinal)) return false;
            string[] parts = unc.Substring(2).Split('\\',
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2
                || !parts[1].Equals(ShareName, StringComparison.OrdinalIgnoreCase))
                return false;

            string host = parts[0];
            string shortHost = host.Split('.')[0];
            if (shortHost.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
                || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host.Equals(".", StringComparison.Ordinal)
                || IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address))
                return true;

            return GetLocalPrivateIPv4Addresses().Any(local =>
                local.ToString().Equals(host, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The primary-PC action always creates a local folder and publishes
        /// it on THIS computer. If the path box currently contains a UNC or
        /// mapped network path (including the literal \\SERVER example), it
        /// deliberately uses the safe local suggestion instead of trying to
        /// create a folder on a remote machine.
        /// </summary>
        public static (bool Ok, string Message, string Path) CreateHostSharedFolder(
            string? preferredLocalPath = null)
        {
            string localFolder = string.IsNullOrWhiteSpace(preferredLocalPath)
                || LooksNetworkBased(preferredLocalPath)
                ? SuggestShareableFolder()
                : preferredLocalPath.Trim();
            return CreateSharedFolder(localFolder, publishWindowsShare: true);
        }

        public static bool IsServerPlaceholderPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string value = path.Trim();
            if (!value.StartsWith("\\\\", StringComparison.Ordinal)) return false;
            string remainder = value.Substring(2);
            int separator = remainder.IndexOf('\\');
            string host = separator < 0 ? remainder : remainder[..separator];
            return host.Equals("SERVER", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates the folder and manifest. On a local path, the default
        /// operation now publishes a real SMB share, applies both share and
        /// NTFS write permissions, opens SMB only to the local subnet on a
        /// Private/Domain firewall profile, and verifies the resulting UNC
        /// path. A created local folder is never reported as a working share.
        /// </summary>
        public static (bool Ok, string Message, string Path) CreateSharedFolder(
            string? path, bool publishWindowsShare = true)
        {
            bool usedSuggestion = string.IsNullOrWhiteSpace(path);
            string requested = usedSuggestion ? SuggestShareableFolder() : path!.Trim();
            if (!AppDataRoot.TryNormalizeSharedRoot(requested, out string normalized))
            {
                return (false,
                    "✗ That path cannot be the shared folder. Use a dedicated folder such as Public Documents\\ProGlassData, not the local ProGlass AppData folder.",
                    requested);
            }

            bool networkPath = LooksNetworkBased(normalized);
            string expectedHostPath = BuildHostSharePath();
            if (publishWindowsShare && !networkPath
                && !TryValidateLocalHostShareFolder(normalized, out string safetyFailure))
            {
                return (false, "✗ " + safetyFailure, expectedHostPath);
            }

            try
            {
                Directory.CreateDirectory(normalized);
                string probeFile = Path.Combine(normalized, ".proglass-share-probe");
                File.WriteAllText(probeFile, "ok");
                File.Delete(probeFile);
                if (!WriteManifest(normalized))
                    throw new IOException("The ProGlass shared-root manifest could not be written.");
            }
            catch (Exception ex)
            {
                string extra = networkPath
                    ? " A network path can only be created when the remote share already exists. On the primary PC, click Create Share on This PC instead."
                    : "";
                return (false,
                    "✗ Could not create the shared folder: " + AppDataRoot.DescribeShareFailure(ex) + extra,
                    networkPath ? normalized : expectedHostPath);
            }

            string created = usedSuggestion
                ? "Created the suggested folder outside AppData: " + normalized + ". "
                : "Created/verified the folder: " + normalized + ". ";

            if (!publishWindowsShare)
                return (true,
                    "✓ " + created + "This test did not publish a Windows share.",
                    normalized);

            if (networkPath)
            {
                return (true,
                    "✓ " + created + "This path is already on a network share; use the same UNC path on each workstation and test it before applying.",
                    normalized);
            }

            var publish = TryPublishWindowsShare(normalized);
            if (!publish.Ok)
            {
                return (false,
                    "✗ The local folder " + normalized + " exists, but ProGlass could not finish publishing a usable Windows network share. "
                    + publish.Message
                    + " The app has not switched databases. Approve the Windows administrator prompt and retry, or have an administrator configure the share and permissions.",
                    expectedHostPath);
            }

            if (!publish.TrustedNetworkProfile)
            {
                return (false,
                    "⚠ The Windows share and firewall rule were created, but this PC has no active Private or Domain network profile. Set the office Ethernet/Wi-Fi network to Private in Windows Settings (do not enable SMB on a Public network), then enter the share path below in Settings and click Test Connection. The share path is "
                    + expectedHostPath + ".",
                    expectedHostPath);
            }

            var clients = GetHostShareConnectionPaths();
            bool hostNameVerified = AppDataRoot.TryVerifySharedFolder(
                expectedHostPath, out string verifyFailure, out _);
            string verifiedPath = hostNameVerified ? expectedHostPath : "";
            if (!hostNameVerified)
            {
                // Some workgroups can reach the host by IPv4 even when the
                // PC name does not resolve locally. Prefer the hostname, but
                // verify an IP path before giving up on primary-PC setup.
                foreach (string ipPath in clients.Skip(1).Take(3))
                {
                    if (!AppDataRoot.TryVerifySharedFolder(ipPath, out _, out _))
                        continue;
                    verifiedPath = ipPath;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(verifiedPath))
            {
                string connectionPaths = string.Join("\n  ", clients);
                return (false,
                    "⚠ Windows reports the share as published, but this PC could not verify read/write/delete access through its host-name or IP UNC path. "
                    + verifyFailure + " Check SMB service, Windows credentials, NTFS Modify permission, the Private/Domain network profile, and firewall policy. Try these client paths:\n  "
                    + connectionPaths,
                    expectedHostPath);
            }

            string alternatives = string.Join(
                "\n  ", clients.Where(path =>
                    !path.Equals(verifiedPath, StringComparison.OrdinalIgnoreCase)
                    && (hostNameVerified || !path.Equals(
                        expectedHostPath, StringComparison.OrdinalIgnoreCase))));
            string alternativeNote = !string.IsNullOrWhiteSpace(alternatives)
                ? "\n  Other paths: " + alternatives
                : string.Equals(verifiedPath, expectedHostPath, StringComparison.OrdinalIgnoreCase)
                    ? "\n  Connect this PC to the office LAN/Wi-Fi to display an IP fallback."
                    : "";
            string hostNameNote = hostNameVerified
                ? ""
                : " The computer name did not verify locally, so this host is using the IP path shown below; reserve that address in DHCP or use the host name if it resolves on clients.";
            return (true,
                "✓ Created and verified the ProGlass Windows share. Client PCs use:\n  "
                + verifiedPath + alternativeNote + hostNameNote
                + "\nWindows permissions: Authenticated Users have share Change and NTFS Modify access; inbound SMB/TCP 445 is restricted to the local subnet on Private/Domain networks. A client may need the host PC's or office domain's Windows credentials. On each client, Find on LAN or enter one path above, Test Connection, then explicitly Save + Restart. No data is copied or merged by Create Share.",
                verifiedPath);
        }

        private sealed record SharePublishResult(
            bool Ok,
            bool TrustedNetworkProfile,
            string Message);

        private static bool TryValidateLocalHostShareFolder(
            string folder, out string failure)
        {
            failure = "";
            string full;
            try
            {
                full = Path.GetFullPath(folder);
            }
            catch (Exception ex)
            {
                failure = "That is not a valid local folder path: " + ex.Message;
                return false;
            }

            string volumeRoot = Path.GetPathRoot(full) ?? "";
            if (string.Equals(full, volumeRoot, StringComparison.OrdinalIgnoreCase))
            {
                failure = "For safety, ProGlass will not share an entire drive. Choose a dedicated folder such as Public Documents\\ProGlassData.";
                return false;
            }

            var restrictedRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppDataRoot.ProductionFolder,
                AppDataRoot.DemoFolder
            };
            foreach (string restricted in restrictedRoots.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (IsSameOrBelow(full, restricted) || IsSameOrBelow(restricted, full))
                {
                    failure = "For safety, ProGlass will not share a Windows/system, application-data, or ProGlass AppData folder. Choose a dedicated data folder such as Public Documents\\ProGlassData.";
                    return false;
                }
            }

            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(profile) && IsSameOrBelow(profile, full))
            {
                failure = "For safety, ProGlass will not share a whole Windows user profile. Choose a dedicated folder inside Public Documents or another data-only location.";
                return false;
            }

            return true;
        }

        private static bool IsSameOrBelow(string candidate, string parent)
        {
            string candidateFull = Path.GetFullPath(candidate);
            string parentFull = Path.GetFullPath(parent);
            if (candidateFull.Equals(parentFull, StringComparison.OrdinalIgnoreCase))
                return true;
            string prefix = parentFull.EndsWith(Path.DirectorySeparatorChar)
                || parentFull.EndsWith(Path.AltDirectorySeparatorChar)
                ? parentFull
                : parentFull + Path.DirectorySeparatorChar;
            return candidateFull.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static SharePublishResult TryPublishWindowsShare(string folder)
        {
            if (!OperatingSystem.IsWindows())
                return new SharePublishResult(false, false,
                    "SMB share creation is supported only on Windows. The folder remains local: " + folder + ".");

            string tempId = Guid.NewGuid().ToString("N");
            string scriptPath = Path.Combine(Path.GetTempPath(), "ProGlass-Smb-" + tempId + ".ps1");
            string resultPath = Path.Combine(Path.GetTempPath(), "ProGlass-Smb-" + tempId + ".json");
            Process? process = null;
            try
            {
                File.WriteAllText(scriptPath, WindowsShareSetupScript,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                bool elevated = IsCurrentProcessElevated();
                var start = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    UseShellExecute = !elevated,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                if (!elevated) start.Verb = "runas";
                start.ArgumentList.Add("-NoLogo");
                start.ArgumentList.Add("-NoProfile");
                start.ArgumentList.Add("-NonInteractive");
                start.ArgumentList.Add("-ExecutionPolicy");
                start.ArgumentList.Add("Bypass");
                start.ArgumentList.Add("-File");
                start.ArgumentList.Add(scriptPath);
                start.ArgumentList.Add("-Folder");
                start.ArgumentList.Add(folder);
                start.ArgumentList.Add("-ShareName");
                start.ArgumentList.Add(ShareName);
                start.ArgumentList.Add("-FirewallRuleName");
                start.ArgumentList.Add(FirewallRuleName);
                start.ArgumentList.Add("-ResultFile");
                start.ArgumentList.Add(resultPath);

                process = Process.Start(start);
                if (process == null)
                    return new SharePublishResult(false, false,
                        "Windows could not start its SMB configuration helper.");
                if (!process.WaitForExit(120000))
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch { /* The timeout remains the useful operator error. */ }
                    return new SharePublishResult(false, false,
                        "Windows did not finish configuring the share within 2 minutes. Check the UAC prompt, folder size, SMB service, and Windows Firewall, then retry.");
                }

                string json = File.Exists(resultPath) ? File.ReadAllText(resultPath) : "";
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new SharePublishResult(false, false,
                        "The Windows SMB helper returned no result (exit code " + process.ExitCode + "). Check the administrator prompt and PowerShell availability.");
                }

                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                bool ok = root.TryGetProperty("Ok", out var okValue)
                    && okValue.ValueKind == JsonValueKind.True;
                bool trustedProfile = root.TryGetProperty("TrustedNetworkProfile", out var profileValue)
                    && profileValue.ValueKind == JsonValueKind.True;
                string message = root.TryGetProperty("Message", out var messageValue)
                    ? messageValue.GetString() ?? ""
                    : "Windows did not provide setup details.";
                return new SharePublishResult(ok && process.ExitCode == 0,
                    trustedProfile, message);
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return new SharePublishResult(false, false,
                    "Windows administrator approval was cancelled. The local folder remains on this PC, but the network share was not confirmed. Approve the UAC prompt when retrying.");
            }
            catch (Exception ex)
            {
                try
                {
                    AppLogService.Warning(
                        "Windows SMB share setup did not complete: " + ex.Message,
                        ex,
                        new LogContext { Module = "Database", Operation = "PublishWindowsShare" });
                }
                catch { /* Logging must not mask the setup result. */ }
                return new SharePublishResult(false, false,
                    "Windows could not finish the share/permission/firewall setup: "
                    + AppDataRoot.DescribeShareFailure(ex));
            }
            finally
            {
                process?.Dispose();
                try { if (File.Exists(scriptPath)) File.Delete(scriptPath); }
                catch { /* Best-effort temporary-script cleanup. */ }
                try { if (File.Exists(resultPath)) File.Delete(resultPath); }
                catch { /* Best-effort temporary-result cleanup. */ }
            }
        }

        private static bool IsCurrentProcessElevated()
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        private const string WindowsShareSetupScript = """
            param(
                [Parameter(Mandatory=$true)][string]$Folder,
                [Parameter(Mandatory=$true)][string]$ShareName,
                [Parameter(Mandatory=$true)][string]$FirewallRuleName,
                [Parameter(Mandatory=$true)][string]$ResultFile
            )
            $ErrorActionPreference = 'Stop'
            $stage = 'Preparing folder permissions'
            try {
                $Folder = [System.IO.Path]::GetFullPath($Folder)
                $authenticatedUsers = ([System.Security.Principal.SecurityIdentifier]'S-1-5-11').Translate([System.Security.Principal.NTAccount]).Value
                $administrators = ([System.Security.Principal.SecurityIdentifier]'S-1-5-32-544').Translate([System.Security.Principal.NTAccount]).Value
                $everyone = ([System.Security.Principal.SecurityIdentifier]'S-1-1-0').Translate([System.Security.Principal.NTAccount]).Value
                $anonymous = ([System.Security.Principal.SecurityIdentifier]'S-1-5-7').Translate([System.Security.Principal.NTAccount]).Value
                $guests = ([System.Security.Principal.SecurityIdentifier]'S-1-5-32-546').Translate([System.Security.Principal.NTAccount]).Value
                $existingShare = Get-SmbShare -Name $ShareName -ErrorAction SilentlyContinue
                if ($null -ne $existingShare -and
                    -not ([System.IO.Path]::GetFullPath($existingShare.Path)).Equals($Folder, [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "The share name $ShareName is already used for a different folder ($($existingShare.Path)). Remove or rename that share before retrying."
                }

                & "$env:SystemRoot\System32\icacls.exe" $Folder '/grant' '*S-1-5-11:(OI)(CI)M' '/T' | Out-Null
                if ($LASTEXITCODE -ne 0) { throw "NTFS Modify permission assignment failed (icacls exit $LASTEXITCODE)." }

                $stage = 'Publishing SMB share'
                if ($null -ne $existingShare) {
                    Grant-SmbShareAccess -Name $ShareName -AccountName $authenticatedUsers -AccessRight Change -Force -ErrorAction Stop | Out-Null
                    Grant-SmbShareAccess -Name $ShareName -AccountName $administrators -AccessRight Full -Force -ErrorAction Stop | Out-Null
                    Get-SmbShareAccess -Name $ShareName -ErrorAction Stop |
                        Where-Object { $_.AccountName -eq $everyone -or $_.AccountName -eq $anonymous -or $_.AccountName -eq $guests } |
                        ForEach-Object { Revoke-SmbShareAccess -Name $ShareName -AccountName $_.AccountName -Force -ErrorAction Stop }
                    Set-SmbShare -Name $ShareName -CachingMode None -ErrorAction Stop | Out-Null
                }
                else {
                    New-SmbShare -Name $ShareName -Path $Folder -ChangeAccess $authenticatedUsers -FullAccess $administrators -CachingMode None -FolderEnumerationMode AccessBased -Description 'ProGlass shared company data' -ErrorAction Stop | Out-Null
                }

                $stage = 'Configuring local-subnet SMB firewall rule'
                Get-NetFirewallRule -Name $FirewallRuleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue
                New-NetFirewallRule -Name $FirewallRuleName -DisplayName 'ProGlass Automation SMB (Private LAN)' -Description 'Allow ProGlass SMB share access only from the local subnet on Private or Domain networks.' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 445 -RemoteAddress LocalSubnet -Profile @('Private', 'Domain') -Enabled True -ErrorAction Stop | Out-Null

                $trustedProfile = @(
                    Get-NetConnectionProfile -ErrorAction SilentlyContinue |
                    Where-Object { $_.NetworkCategory -eq 'Private' -or $_.NetworkCategory -eq 'DomainAuthenticated' }
                ).Count -gt 0

                [ordered]@{
                    Ok = $true
                    TrustedNetworkProfile = $trustedProfile
                    Message = 'SMB share, Authenticated Users share access, NTFS Modify permission, and local-subnet firewall rule are configured.'
                } | ConvertTo-Json -Compress | Set-Content -LiteralPath $ResultFile -Encoding UTF8 -Force
                exit 0
            }
            catch {
                [ordered]@{
                    Ok = $false
                    TrustedNetworkProfile = $false
                    Message = ($stage + ': ' + $_.Exception.Message)
                } | ConvertTo-Json -Compress | Set-Content -LiteralPath $ResultFile -Encoding UTF8 -Force
                exit 1
            }
            """;

        /// <summary>
        /// Finds only ProGlass-marked shares on this PC's RFC1918 IPv4
        /// subnets. It checks SMB TCP/445, then verifies the product manifest;
        /// it never auto-selects or applies a dataset.
        /// </summary>
        public static async Task<IReadOnlyList<string>> DiscoverSharedDataRootsAsync(
            CancellationToken cancellationToken = default)
        {
            if (!OperatingSystem.IsWindows()) return Array.Empty<string>();

            var addresses = GetLocalSubnetCandidates();
            if (addresses.Count == 0) return Array.Empty<string>();

            using var scanTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            scanTimeout.CancelAfter(TimeSpan.FromSeconds(30));
            CancellationToken scanToken = scanTimeout.Token;
            var found = new ConcurrentBag<string>();
            using var concurrency = new SemaphoreSlim(32, 32);
            var checks = addresses.Select(async address =>
            {
                bool entered = false;
                try
                {
                    await concurrency.WaitAsync(scanToken).ConfigureAwait(false);
                    entered = true;
                    if (!await IsSmbPortOpenAsync(address, scanToken).ConfigureAwait(false))
                        return;

                    string unc = BuildHostSharePath(address.ToString());
                    var manifestCheck = Task.Run(
                        () => HasProGlassSharedRootManifest(unc), scanToken);
                    var completed = await Task.WhenAny(
                        manifestCheck,
                        Task.Delay(TimeSpan.FromSeconds(2), scanToken)).ConfigureAwait(false);
                    if (completed == manifestCheck && await manifestCheck.ConfigureAwait(false))
                        found.Add(unc);
                }
                catch (OperationCanceledException) when (scanToken.IsCancellationRequested)
                {
                    // Caller cancellation or the 30-second scan safety limit.
                }
                catch (Exception ex)
                {
                    AppLogService.Warning(
                        "A local SMB discovery probe failed: " + ex.Message,
                        new LogContext { Module = "Database", Operation = "DiscoverSharedDataRoots" });
                }
                finally
                {
                    if (entered) concurrency.Release();
                }
            }).ToArray();

            await Task.WhenAll(checks).ConfigureAwait(false);
            return found.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static bool HasProGlassSharedRootManifest(string unc)
        {
            try
            {
                string manifestPath = Path.Combine(unc, ManifestFileName);
                if (!File.Exists(manifestPath)) return false;
                using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                var root = document.RootElement;
                return root.TryGetProperty("Product", out var product)
                    && product.GetString()?.Equals("ProGlass Automation", StringComparison.OrdinalIgnoreCase) == true
                    && root.TryGetProperty("Purpose", out var purpose)
                    && purpose.GetString()?.Contains("Shared", StringComparison.OrdinalIgnoreCase) == true;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> IsSmbPortOpenAsync(
            IPAddress address, CancellationToken cancellationToken)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromMilliseconds(400));
                using var client = new TcpClient(AddressFamily.InterNetwork);
                await client.ConnectAsync(address, 445, timeout.Token).ConfigureAwait(false);
                return client.Connected;
            }
            catch
            {
                return false;
            }
        }

        private static List<IPAddress> GetLocalSubnetCandidates()
        {
            var candidates = new HashSet<IPAddress>();
            NetworkInterface[] interfaces;
            try { interfaces = NetworkInterface.GetAllNetworkInterfaces(); }
            catch { return candidates.ToList(); }

            foreach (var network in interfaces)
            {
                if (!IsLikelyOfficeNetworkInterface(network))
                    continue;

                try
                {
                    foreach (var unicast in network.GetIPProperties().UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily != AddressFamily.InterNetwork
                            || unicast.IPv4Mask == null
                            || !IsPrivateIPv4(unicast.Address))
                            continue;

                        uint local = IPv4ToUInt32(unicast.Address);
                        uint mask = IPv4ToUInt32(unicast.IPv4Mask);
                        uint subnet = local & mask;
                        uint broadcast = subnet | ~mask;
                        if (broadcast <= subnet + 1) continue;

                        uint first = subnet + 1;
                        uint last = broadcast - 1;
                        ulong hostCount = (ulong)last - first + 1;
                        if (hostCount > 1024)
                        {
                            ulong start = local > subnet + 512
                                ? (ulong)local - 512
                                : (ulong)subnet + 1;
                            ulong maximumStart = (ulong)last - 1023;
                            start = Math.Max((ulong)first, Math.Min(start, maximumStart));
                            first = (uint)start;
                            last = Math.Min(last, first + 1023);
                        }

                        for (uint host = first; host <= last; host++)
                        {
                            if (host != local) candidates.Add(UInt32ToIPv4(host));
                            if (host == uint.MaxValue) break;
                        }
                    }
                }
                catch
                {
                    // Ignore an adapter whose network properties are unavailable.
                }
            }

            return candidates.Take(2048).ToList();
        }

        private static bool IsLikelyOfficeNetworkInterface(NetworkInterface network)
        {
            if (network.OperationalStatus != OperationalStatus.Up
                || network.NetworkInterfaceType == NetworkInterfaceType.Loopback
                || network.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                return false;

            string description = (network.Name + " " + network.Description)
                .ToLowerInvariant();
            return !description.Contains("virtual", StringComparison.Ordinal)
                && !description.Contains("vmware", StringComparison.Ordinal)
                && !description.Contains("virtualbox", StringComparison.Ordinal)
                && !description.Contains("hyper-v", StringComparison.Ordinal)
                && !description.Contains("wsl", StringComparison.Ordinal)
                && !description.Contains("docker", StringComparison.Ordinal)
                && !description.Contains("vpn", StringComparison.Ordinal)
                && !description.Contains("wireguard", StringComparison.Ordinal)
                && !description.Contains("tailscale", StringComparison.Ordinal)
                && !description.Contains("tap-windows", StringComparison.Ordinal)
                && !description.Contains("teredo", StringComparison.Ordinal)
                && !description.Contains("isatap", StringComparison.Ordinal);
        }

        private static bool IsPrivateIPv4(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }

        private static IEnumerable<IPAddress> GetLocalPrivateIPv4Addresses()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(IsLikelyOfficeNetworkInterface)
                    .SelectMany(network => network.GetIPProperties().UnicastAddresses)
                    .Select(unicast => unicast.Address)
                    .Where(address => address.AddressFamily == AddressFamily.InterNetwork
                        && IsPrivateIPv4(address))
                    .Distinct()
                    .ToArray();
            }
            catch
            {
                return Array.Empty<IPAddress>();
            }
        }

        private static uint IPv4ToUInt32(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();
            return ((uint)bytes[0] << 24)
                | ((uint)bytes[1] << 16)
                | ((uint)bytes[2] << 8)
                | bytes[3];
        }

        private static IPAddress UInt32ToIPv4(uint value) => new(new[]
        {
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value
        });

        public static (bool Ok, string Message) PrepareSharedFolder(
            string sharedFolder,
            bool copyLocalData)
        {
            if (!IsCurrentComputerShareRoot(sharedFolder))
                return (false,
                    "✗ Prepare Folder may only run on the primary PC against its own \\\\HOSTNAME\\ProGlassData share. Client PCs should Test Connection and use Save + Restart; client-local data is never copied or merged into another host's share.");

            var probe = Probe(sharedFolder);
            if (!probe.Ok) return (false, probe.Message);

            try
            {
                Directory.CreateDirectory(probe.NormalizedPath);
                int copied = 0;
                int checkpointed = 0;
                bool targetWasEmpty = IsFolderEmptyOfProGlassData(probe.NormalizedPath);

                if (copyLocalData && targetWasEmpty)
                {
                    // Do not seed from a partially checkpointed source. The
                    // safety backup was already made by the Settings flow.
                    checkpointed = CheckpointLocalDatabases(out int localDatabaseCount);
                    if (checkpointed != localDatabaseCount)
                        throw new InvalidOperationException(
                            $"Only {checkpointed} of {localDatabaseCount} local databases passed the WAL checkpoint; no local records were copied. Close ProGlass on this PC and retry preparation after verifying the backup.");
                    copied = CopyLocalDatasetTo(probe.NormalizedPath);
                }

                // Convert every database now living on the share to
                // rollback-journal (DELETE) mode. The mode is persisted in
                // the SQLite file header, so from this moment every
                // workstation opens the shared databases SMB-safe.
                int converted = ConvertDatabasesToRollbackJournal(probe.NormalizedPath);

                if (!WriteManifest(probe.NormalizedPath))
                    throw new IOException("The ProGlass shared-root manifest could not be written.");

                string copyHint = targetWasEmpty && !copyLocalData
                    ? " No local data was copied (copy-local is off); this workstation's local records remain separate."
                    : !targetWasEmpty && copyLocalData
                        ? " No local data was copied because the share already contained ProGlass data; existing records were not overwritten or merged."
                        : "";
                return (true, "✓ Shared folder prepared"
                    + (copied > 0 ? $" and {copied} local data item(s) copied" : "")
                    + copyHint
                    + $", {checkpointed} local database(s) checkpointed, {converted} shared database(s) set to rollback-journal mode."
                    + " Save settings and restart all workstations.");
            }
            catch (Exception ex)
            {
                AppLogService.Error("Shared data folder preparation failed", ex,
                    new LogContext { Module = "Database", Operation = "PrepareSharedFolder" });
                return (false, "✗ Could not prepare shared folder: " + ex.Message);
            }
        }

        /// <summary>
        /// Writes the shared-mode decision into config. Returns (Ok,
        /// Message) instead of void: the caller must not report success —
        /// or persist anything — when the enabled state is invalid.
        /// </summary>
        public static (bool Ok, string Message) ApplyToConfig(
            AppConfig config, bool enabled, string path, bool copyLocalData)
        {
            config.DataConnectivity ??= new DataConnectivitySettings();

            // Declared before the guard: when enabled == false the
            // TryNormalizeSharedRoot call short-circuits and its out
            // parameter would be unassigned (CS0165) at the use below.
            string normalized = "";
            if (enabled && !AppDataRoot.TryNormalizeSharedRoot(path, out normalized))
            {
                return (false, "✗ Enter a valid shared folder path first " +
                    "(for example \\\\OFFICE-PC\\ProGlassData) — shared mode is not saved with a missing path.");
            }

            config.DataConnectivity.UseSharedDataRoot = enabled;
            config.DataConnectivity.SharedDataRootPath = enabled ? normalized : "";
            config.DataConnectivity.CopyLocalDataOnEnable = copyLocalData;
            config.DataConnectivity.SharedRootOfflineFallbackNeedsReview = false;
            config.DataConnectivity.SharedRootLocalModeNoticePending = false;
            config.DataConnectivity.SharedRootReconnectNoticeShown = false;
            if (string.IsNullOrWhiteSpace(config.DataConnectivity.WorkstationName))
                config.DataConnectivity.WorkstationName = Environment.MachineName;

            return (true, enabled
                ? "✓ Shared company database connectivity saved."
                : "✓ Workstation will use its local AppData database root after restart.");
        }

        // ── Preparation internals ────────────────────────────────────────

        /// <summary>
        /// Forces every local SQLite database to checkpoint its WAL into
        /// the main .db file. Returns the number that passed and the total
        /// discovered count; callers must refuse to seed when any checkpoint
        /// fails, so local-only WAL records are never silently omitted.
        /// </summary>
        private static int CheckpointLocalDatabases(out int databaseCount)
        {
            var sourceRoot = AppDataRoot.ProductionFolder;
            string[] databases = Directory.Exists(sourceRoot)
                ? Directory.GetFiles(sourceRoot, "*.db", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            databaseCount = databases.Length;

            int done = 0;
            foreach (var db in databases)
            {
                try
                {
                    using var conn = new SqliteConnection(
                        SqliteResilience.BuildConnectionString(db));
                    conn.Open();
                    SqliteResilience.ApplyConnectionPolicy(conn);
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                        using var reader = cmd.ExecuteReader();
                        if (reader.Read() && reader.GetInt32(0) == 0)
                        {
                            done++;
                            break;
                        }
                        Thread.Sleep(250);
                    }
                }
                catch (Exception ex)
                {
                    AppLogService.Warning(
                        $"WAL checkpoint skipped for {Path.GetFileName(db)}: {ex.Message}",
                        new LogContext { Module = "Database", Operation = "CheckpointLocalDatabases" });
                }
            }
            return done;
        }

        /// <summary>
        /// Sets rollback-journal mode on every .db now on the shared root.
        /// The mode is stored in the database header, so one conversion
        /// here protects every workstation from WAL-over-SMB for the life
        /// of the file.
        /// </summary>
        private static int ConvertDatabasesToRollbackJournal(string root)
        {
            if (!Directory.Exists(root)) return 0;

            int converted = 0;
            foreach (var db in Directory.GetFiles(root, "*.db", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    using var conn = new SqliteConnection(
                        SqliteResilience.BuildSharedRootConnectionString(db));
                    conn.Open();
                    string mode = SqliteResilience.ApplyConnectionPolicy(
                        conn, sharedRootOverride: true);
                    if (string.Equals(mode, "delete", StringComparison.OrdinalIgnoreCase))
                        converted++;
                }
                catch (Exception ex)
                {
                    // Do not report the root prepared when even one database
                    // remains in WAL mode. Applying shared settings is gated
                    // on every file passing the central mode verification.
                    AppLogService.Error(
                        $"Could not set rollback journal on shared {Path.GetFileName(db)}.",
                        ex,
                        new LogContext { Module = "Database", Operation = "ConvertSharedDatabases" });
                    throw new InvalidOperationException(
                        $"Could not convert {Path.GetFileName(db)} to the required DELETE journal mode. " +
                        "Close all ProGlass workstations and retry shared-folder preparation.", ex);
                }
            }
            return converted;
        }

        private static bool WriteManifest(string root)
        {
            try
            {
                var manifest = new
                {
                    Product = "ProGlass Automation",
                    Purpose = "Shared LAN/Wi-Fi company database root",
                    CreatedOrUpdatedAtUtc = DateTime.UtcNow,
                    Workstation = Environment.MachineName,
                    AppVersion = typeof(SharedDataRootService).Assembly.GetName().Version?.ToString() ?? "",
                    JournalMode = "DELETE (rollback journal — WAL is unsafe over SMB/NAS)",
                    Warning = "Do not edit database files manually. Do not use cloud-sync folders for live database files."
                };
                File.WriteAllText(Path.Combine(root, ManifestFileName),
                    JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Warning("Shared-root manifest could not be written: " + ex.Message,
                    new LogContext { Module = "Database", Operation = "WriteSharedRootManifest" });
                return false;
            }
        }

        private static bool IsFolderEmptyOfProGlassData(string folder)
        {
            if (!Directory.Exists(folder)) return true;
            return !Directory.EnumerateFiles(folder, "*.db", SearchOption.TopDirectoryOnly).Any()
                && ExtraDataSubfolders.All(s => !Directory.Exists(Path.Combine(folder, s)));
        }

        private static int CopyLocalDatasetTo(string targetRoot)
        {
            var sourceRoot = AppDataRoot.ProductionFolder;
            if (!Directory.Exists(sourceRoot)) return 0;

            // Stage on the same share, then promote only after every source
            // file copied successfully. A low-disk or network error must not
            // leave a partial set of company databases at the live root.
            string stagingRoot = Path.Combine(
                targetRoot, ".proglass_seed_staging_" + Guid.NewGuid().ToString("N"));
            var promotedFiles = new List<string>();
            var promotedDirectories = new List<string>();
            try
            {
                Directory.CreateDirectory(stagingRoot);
                foreach (var file in Directory.GetFiles(
                    sourceRoot, "*.db", SearchOption.TopDirectoryOnly))
                {
                    // Only the .db file is copied. The WAL was absorbed into
                    // it by CheckpointLocalDatabases; -shm is never copied.
                    File.Copy(file,
                        Path.Combine(stagingRoot, Path.GetFileName(file)),
                        overwrite: false);
                }

                foreach (var sub in ExtraDataSubfolders)
                {
                    var source = Path.Combine(sourceRoot, sub);
                    if (Directory.Exists(source))
                        CopyDirectory(source, Path.Combine(stagingRoot, sub));
                }

                if (!IsFolderEmptyOfProGlassData(targetRoot))
                    throw new IOException(
                        "The shared folder gained ProGlass data during preparation. The staged local copy was not promoted; close all clients and retry after reviewing the target.");

                foreach (var stagedFile in Directory.GetFiles(
                    stagingRoot, "*.db", SearchOption.TopDirectoryOnly))
                {
                    string target = Path.Combine(
                        targetRoot, Path.GetFileName(stagedFile));
                    File.Move(stagedFile, target);
                    promotedFiles.Add(target);
                }

                foreach (var sub in ExtraDataSubfolders)
                {
                    string stagedDirectory = Path.Combine(stagingRoot, sub);
                    if (!Directory.Exists(stagedDirectory)) continue;

                    string targetDirectory = Path.Combine(targetRoot, sub);
                    if (Directory.Exists(targetDirectory))
                        throw new IOException(
                            "The target gained the " + sub + " folder during preparation. The staged local copy was not promoted completely.");
                    Directory.Move(stagedDirectory, targetDirectory);
                    promotedDirectories.Add(targetDirectory);
                }

                return promotedFiles.Count + promotedDirectories.Count;
            }
            catch
            {
                foreach (string directory in promotedDirectories.AsEnumerable().Reverse())
                {
                    try { Directory.Delete(directory, recursive: true); }
                    catch { /* Preserve the copy/promotion failure. */ }
                }
                foreach (string file in promotedFiles.AsEnumerable().Reverse())
                {
                    try { if (File.Exists(file)) File.Delete(file); }
                    catch { /* Preserve the copy/promotion failure. */ }
                }
                throw;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(stagingRoot))
                        Directory.Delete(stagingRoot, recursive: true);
                }
                catch { /* Staging cleanup is best-effort after the original result. */ }
            }
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }
    }
}
