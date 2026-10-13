using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Logging;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Database
{
    /// <summary>
    /// Single folder every ERP SQLite file lives under.
    /// Supports a professional LAN/Wi-Fi shared data root so all company
    /// workstations can point at the same dataset from Settings.
    ///
    /// SHARED LAN/WI-FI DATA ROOT:
    /// The saved UNC path is probed before any DbContext opens. If the share
    /// is reachable, all databases use it. If it is unavailable at startup,
    /// the application remains usable against this workstation's local
    /// AppData database and raises a visible warning; it never pretends that
    /// local writes are shared. A persisted review marker prevents a later
    /// startup from silently switching datasets after offline local work.
    /// A share that disappears after it has been selected remains a hard
    /// database error; the app never hot-switches roots mid-session.
    /// </summary>
    public static class AppDataRoot
    {
        public const string ProductionName = "ProGlassAutomation";
        public const string DemoName = "ProGlassAutomation.Demo";

        private static string _sharedDataRoot = "";
        private static string _configuredSharedDataRoot = "";
        private static bool _sharedFolderVerified = false;

        public static bool IsDemo { get; private set; }

        /// <summary>Live shared root actually in use right now ("" = local).</summary>
        public static bool IsSharedDataRootEnabled =>
            !string.IsNullOrWhiteSpace(_sharedDataRoot) && !IsDemo;

        public static string SharedDataRoot => _sharedDataRoot;

        /// <summary>Saved shared root, normalized when possible, even when unavailable.</summary>
        public static string ConfiguredSharedDataRoot => _configuredSharedDataRoot;

        /// <summary>
        /// The persisted settings asked for a shared root, whether or not
        /// it is active. Lets the UI distinguish configured-but-offline or
        /// reconnect-pending local mode from a deliberate local profile.
        /// </summary>
        public static bool SharedRootConfigured { get; private set; }

        /// <summary>
        /// Human-readable local-mode warning when the saved shared root is
        /// unavailable or requires an explicit reconnect after prior offline
        /// work. Empty when the configured root is active or local mode was
        /// deliberately selected.
        /// </summary>
        public static string SharedRootFallbackReason { get; private set; } = "";

        /// <summary>True when a configured shared root is not active and this workstation is using local data.</summary>
        public static bool SharedRootFallbackActive =>
            SharedRootConfigured && !IsSharedDataRootEnabled && !IsDemo;

        /// <summary>True when the saved network path responds but requires explicit reconnect review.</summary>
        public static bool SharedRootReconnectAvailable { get; private set; }

        public static string? TestFolderOverride { get; set; }

        public static string CurrentFolder
        {
            get
            {
                var overrideFolder = TestFolderOverride;
                if (!string.IsNullOrWhiteSpace(overrideFolder))
                    return overrideFolder;

                if (IsDemo)
                    return DemoFolder;

                // Offline fallback is a single, deliberate local root for
                // every module. SharedRootFallbackActive remains exposed so
                // the UI can warn that these local changes do not sync.
                if (!string.IsNullOrWhiteSpace(_sharedDataRoot))
                    return _sharedDataRoot;

                return ProductionFolder;
            }
        }

        public static string ProductionFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ProductionName);

        public static string DemoFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            DemoName);

        public static void ConfigureFrom(AppConfig? config)
        {
            // Reset every shared-root state, then re-derive it from config.
            _sharedDataRoot = "";
            _configuredSharedDataRoot = "";
            _sharedFolderVerified = false;
            SharedRootConfigured = false;
            SharedRootFallbackReason = "";
            SharedRootReconnectAvailable = false;

            var data = config?.DataConnectivity;
            if (data?.UseSharedDataRoot != true) return;

            SharedRootConfigured = true;
            _configuredSharedDataRoot = data.SharedDataRootPath?.Trim() ?? "";

            if (!TryNormalizeSharedRoot(data.SharedDataRootPath, out var normalized))
            {
                const string detail = "The saved shared-folder path is empty or invalid.";
                if (!data.SharedRootOfflineFallbackNeedsReview)
                    data.SharedRootLocalModeNoticePending = true;
                data.SharedRootOfflineFallbackNeedsReview = true;
                SharedRootFallbackReason =
                    "The saved shared-folder path is empty or invalid. ProGlass is continuing with " +
                    "this workstation's local database. Changes saved locally will not be shared with " +
                    "other workstations. In Settings → Data, enter the existing UNC or mapped network " +
                    "folder path, test access, review the intended dataset, then select Apply and Restart " +
                    "to reconnect. Local and shared data are never copied or merged automatically.";
                AppLogService.Warning(
                    "Shared data root is enabled with an empty or invalid path; continuing with the " +
                    $"local database. {detail}",
                    new LogContext { Module = "Database", Operation = "ConfigureSharedRoot" });
                return;
            }

            _configuredSharedDataRoot = normalized;

            if (!SharedDataRootService.LooksNetworkBased(normalized))
            {
                const string failure = "The saved path is local, not a UNC or mapped network folder.";
                if (!data.SharedRootOfflineFallbackNeedsReview)
                    data.SharedRootLocalModeNoticePending = true;
                data.SharedRootOfflineFallbackNeedsReview = true;
                SharedRootFallbackReason =
                    "The configured shared data path is not a network location. ProGlass is continuing with " +
                    "this workstation's local database; local changes will not be shared with other PCs. " +
                    "Enter and test the same UNC or mapped-drive path on each workstation, review the " +
                    "intended dataset, and select Apply and Restart to reconnect. " + failure;
                AppLogService.Warning(
                    "Configured shared data root is local; continuing with the local database. " +
                    $"Path: {normalized}.",
                    new LogContext { Module = "Database", Operation = "ConfigureSharedRoot" });
                return;
            }

            if (!TryVerifySharedFolder(
                normalized, out var failure, out var failureCause))
            {
                failureCause ??= new IOException(failure);
                if (!data.SharedRootOfflineFallbackNeedsReview)
                    data.SharedRootLocalModeNoticePending = true;
                data.SharedRootOfflineFallbackNeedsReview = true;
                string placeholderHint = UsesExampleServerName(normalized)
                    ? " 'SERVER' is only an example, not a real host name. Enter the actual shared-folder UNC path or mapped network drive that is available on every workstation."
                    : "";
                SharedRootFallbackReason =
                    "The configured shared company data folder " + normalized + " is unavailable. " +
                    "ProGlass is continuing with this workstation's local database; changes made now " +
                    "will not appear on other workstations. The saved path is checked again at startup. " +
                    "When the network returns, enter the same UNC or mapped-drive path in Settings → Data, " +
                    "test access, review the intended dataset, and select Apply and Restart to reconnect. " +
                    "Local and shared data are never copied or merged automatically. " + failure + placeholderHint;
                AppLogService.Warning(
                    "Configured shared data root is unreachable; continuing with the local database. " +
                    $"Path: {normalized}. {failure}",
                    failureCause,
                    new LogContext { Module = "Database", Operation = "ConfigureSharedRoot" });
                return;
            }

            if (data.SharedRootOfflineFallbackNeedsReview)
            {
                // This workstation wrote to local AppData while the saved
                // share was offline. Even though the share has returned, do
                // not silently switch datasets and hide those local changes.
                // Settings can explicitly reconnect after review.
                SharedRootReconnectAvailable = true;
                if (!data.SharedRootReconnectNoticeShown)
                {
                    data.SharedRootLocalModeNoticePending = true;
                    data.SharedRootReconnectNoticeShown = true;
                }
                SharedRootFallbackReason =
                    "The saved shared folder is reachable again: " + normalized + ". ProGlass is " +
                    "staying on this workstation's local database because it may contain changes made " +
                    "while the share was offline. Local changes are not merged automatically. Review " +
                    "the local data separately, then use Settings → Data to explicitly reconnect " +
                    "to the shared folder. Reconnecting does not copy or merge records.";
                AppLogService.Warning(
                    "The saved shared folder is reachable, but this workstation remains in local mode " +
                    "until offline local data is reviewed. Path: " + normalized,
                    new LogContext { Module = "Database", Operation = "SharedRootReconnectReview" });
                return;
            }

            _sharedDataRoot = normalized;
            _sharedFolderVerified = true;
            AppLogService.Info(
                $"Shared company database root enabled: {_sharedDataRoot}",
                new LogContext { Module = "Database", Operation = "ConfigureSharedRoot" });
        }

        private static bool UsesExampleServerName(string path)
        {
            if (!path.StartsWith(@"\\", StringComparison.Ordinal)) return false;
            string remainder = path.Substring(2);
            int separator = remainder.IndexOf('\\');
            string host = separator < 0 ? remainder : remainder[..separator];
            return host.Equals("SERVER", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures the current data folder exists, with shared-root
        /// awareness. An unavailable share is resolved to the local root
        /// once during startup, before any DbContext opens. A selected shared
        /// root is not hot-switched if it fails during a live session, and a
        /// verified share is not re-probed on every context creation.
        /// </summary>
        public static void EnsureCurrentFolder()
        {
            string folder = CurrentFolder;

            if (IsSharedDataRootEnabled)
            {
                if (_sharedFolderVerified) return;
                if (TryVerifySharedFolder(folder, out var failure, out var cause))
                {
                    _sharedFolderVerified = true;
                    return;
                }

                _sharedFolderVerified = false;
                throw new SharedDataRootUnavailableException(
                    folder,
                    cause ?? new IOException(failure),
                    failure);
            }

            Directory.CreateDirectory(folder);
        }

        /// <summary>
        /// Confirms that the configured network folder already exists and
        /// supports read/write/delete access. Startup never creates a missing
        /// folder or changes Windows sharing. The bounded probe cannot freeze
        /// startup on an unresponsive network location.
        /// </summary>
        internal static bool TryVerifySharedFolder(
            string folder, out string failure, out Exception? failureCause)
        {
            // Network filesystem calls can block inside the SMB redirector.
            // Run the read/write/delete probe off-thread with a hard bound.
            // A timeout selects local mode before DbContexts open; an active
            // shared session is never switched to local mid-operation.
            failure = "";
            failureCause = null;
            try
            {
                var probe = Task.Run(() => VerifySharedFolderAccess(folder));
                probe.ContinueWith(
                    t => { _ = t.Exception; },
                    TaskContinuationOptions.OnlyOnFaulted);
                if (!probe.Wait(TimeSpan.FromSeconds(4)))
                {
                    failure = "The shared folder did not respond within 4 seconds — the host " +
                        "computer may be off or the network is down.";
                    failureCause = new TimeoutException(
                        "The shared-folder startup probe exceeded four seconds.");
                    return false;
                }

                return true;
            }
            catch (AggregateException ex)
            {
                var cause = ex.GetBaseException();
                failureCause = cause;
                failure = DescribeShareFailure(cause);
                return false;
            }
            catch (Exception ex)
            {
                failureCause = ex;
                failure = DescribeShareFailure(ex);
                return false;
            }
        }

        private static void VerifySharedFolderAccess(string folder)
        {
            var attributes = File.GetAttributes(folder);
            if ((attributes & FileAttributes.Directory) == 0)
                throw new IOException("The configured shared-root path is not a folder.");

            string probePath = Path.Combine(
                folder, ".proglass_startup_probe_" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(probePath, "ProGlass shared-root startup probe.");
                _ = File.ReadAllText(probePath);
                File.Delete(probePath);
            }
            catch
            {
                try { if (File.Exists(probePath)) File.Delete(probePath); }
                catch { /* Preserve the original access failure. */ }
                throw;
            }
        }

        /// <summary>
        /// Maps a raw .NET folder-access exception onto a sentence an operator can
        /// act on. Shared by the startup probe, the Settings probe button
        /// and EnsureCurrentFolder so every surface says the same thing.
        /// </summary>
        internal static string DescribeShareFailure(Exception ex)
        {
            if (ex is UnauthorizedAccessException)
                return "Access was denied to the folder — check that the share's security " +
                    "permissions allow this Windows user to read AND write.";

            if (ex is IOException)
            {
                string msg = ex.Message ?? "";
                if (msg.Contains("network path", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("cannot find", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("could not find", StringComparison.OrdinalIgnoreCase))
                    return "The configured folder path could not be found — check the host computer " +
                        "name or IP, that the shared folder exists, and that this workstation can reach " +
                        "the same LAN, Wi-Fi, VPN, or remote network.";
                return "The shared folder did not respond — the host computer may be off or " +
                    "the network is down.";
            }

            return ex.Message ?? ex.GetType().Name;
        }

        // ── Mapped-drive detection (WNetGetConnection) ────────────────────
        // A mapped network drive is a shared location just like a UNC path.
        // Query the Windows redirector at use time so drives mapped or
        // remapped during the session are classified correctly.

        /// <summary>
        /// Returns the UNC path a local drive letter points at ("Z:" →
        /// "\\SERVER\share"), or "" for a real local disk.
        /// </summary>
        public static string GetMappedNetworkPath(string path)
        {
            string root = GetDriveRoot(path);
            if (root.Length < 3) return "";

            string unc = "";
            try
            {
                int capacity = 512;
                while (capacity <= 32768)
                {
                    var sb = new StringBuilder(capacity);
                    int length = sb.Capacity;
                    int result = WNetGetConnectionW(root, sb, ref length);
                    if (result == 0)
                    {
                        unc = sb.ToString();
                        break;
                    }
                    if (result != 234) break; // ERROR_MORE_DATA
                    capacity = Math.Max(length + 1, capacity * 2);
                }
            }
            catch
            {
                // DllImport unavailable (non-Windows host, tests) — treat as local.
            }

            return unc;
        }

        private static string GetDriveRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            var trimmed = path.Trim();
            if (trimmed.Length >= 2 && trimmed[1] == ':')
            {
                char c = trimmed[0];
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                    return trimmed.Substring(0, 2) + @"\";
            }
            return "";
        }

        [DllImport("mpr.dll", CharSet = CharSet.Unicode, BestFitMapping = false, ExactSpelling = true)]
        private static extern int WNetGetConnectionW(
            string localName, StringBuilder remoteName, ref int bufferLength);

        public static bool TryNormalizeSharedRoot(string? path, out string normalized)
        {
            normalized = "";
            if (string.IsNullOrWhiteSpace(path)) return false;

            try
            {
                var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('\"')));
                var local = Path.GetFullPath(ProductionFolder);
                var demo = Path.GetFullPath(DemoFolder);

                if (full.Equals(local, StringComparison.OrdinalIgnoreCase)
                    || full.Equals(demo, StringComparison.OrdinalIgnoreCase))
                    return false;

                // Preserve a volume root (for example a mapped share entered
                // as Z:\). Trimming its final separator would turn it into
                // drive-relative "Z:", which resolves against the process's
                // current directory on that drive.
                string volumeRoot = Path.GetPathRoot(full) ?? "";
                normalized = string.Equals(full, volumeRoot, StringComparison.OrdinalIgnoreCase)
                    ? full
                    : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void EnterDemo()
        {
            IsDemo = true;
            _sharedFolderVerified = false;
            Directory.CreateDirectory(CurrentFolder);
        }

        public static void ExitDemo()
        {
            IsDemo = false;
            _sharedFolderVerified = false;
        }
    }

    /// <summary>
    /// Thrown when a database operation needs the shared company data folder
    /// and the share is not reachable right now. The outer message is the
    /// single operator-facing explanation; the inner exception retains the
    /// underlying technical cause for logs and crash reports.
    /// </summary>
    public sealed class SharedDataRootUnavailableException : Exception
    {
        public string SharedFolder { get; }
        public string FailureReason { get; }

        public SharedDataRootUnavailableException(
            string folder,
            Exception inner,
            string? failureReason = null)
            : base(BuildOperatorMessage(folder, failureReason), inner)
        {
            SharedFolder = folder;
            FailureReason = ToSingleLine(failureReason);
        }

        private static string BuildOperatorMessage(
            string folder, string? failureReason)
        {
            var message =
                "The configured shared company data folder is unavailable: " + ToSingleLine(folder) +
                ".\r\n\r\n" +
                "Database access is blocked. This workstation will not open local AppData. " +
                "Check that (1) the computer hosting the share is on, (2) the folder is " +
                "still shared on the WiFi/LAN, and (3) this workstation is connected to " +
                "the same network. Restore access and restart ProGlass.";

            string reason = ToSingleLine(failureReason);
            if (reason.Length > 0)
                message += "\r\n\r\nReason: " + reason;

            return message;
        }

        private static string ToSingleLine(string? value) =>
            (value ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}
