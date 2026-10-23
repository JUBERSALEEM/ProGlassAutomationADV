using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;

namespace ProGlassAutomation.Services.RecordsSync
{
    /// <summary>
    /// Background orchestrator: ProGlass database → normalized records →
    /// Supabase public.records → (unchanged) website.
    ///
    /// Threading contract (WPF safety):
    ///   • StartIfConfigured() runs on the UI thread at startup and does
    ///     ONLY: read two small local files, create a timer. No network,
    ///     no database, no dialogs.
    ///   • Every sync pass runs on the thread pool (timer callback →
    ///     Task.Run-style async), guarded by a semaphore so a slow pass
    ///     never overlaps the next tick.
    ///   • Failures are logged to AppLogService and retried with
    ///     exponential backoff (capped at 60 minutes). NO exception may
    ///     escape the pass, and nothing here ever opens UI — a dead
    ///     network must be invisible to the operator except in the log.
    ///
    /// Credentials: Supabase key lives in SecretVault (DPAPI CurrentUser +
    /// app entropy) under the RecordsSync-prefixed vault entries. URL, Auth
    /// email, and credential-scope metadata live in recordssync.json. Missing,
    /// unbound, or mismatched configuration → the service stays dormant.
    /// </summary>
    public sealed class RecordsSyncService : IDisposable
    {
        /// <summary>The production Supabase project used by this Records Sync integration.</summary>
        public const string ExpectedProjectUrl = "https://afpbdyamedpanwijxghk.supabase.co";

        /// <summary>SecretVault key for the publishable/anon key.</summary>
        public const string SecretKey = "RecordsSync.SupabaseKey";

        /// <summary>SecretVault key for the dedicated Supabase Auth user's
        /// password (the Windowfilehost security pattern — authenticated
        /// role, never service-role).</summary>
        public const string SecretPasswordKey = "RecordsSync.SupabasePassword";

        private static readonly string SettingsPath = Path.Combine(
            AppDataRoot.CurrentFolder, "recordssync.json");
        private static readonly string StatePath = Path.Combine(
            AppDataRoot.CurrentFolder, "recordssync-state.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        private readonly ProGlassRecordsSource _source = new();
        private readonly SemaphoreSlim _gate = new(1, 1);

        private Timer? _timer;
        private RecordsSyncSettings _settings = new();
        private int _backoffMinutes;
        private bool _disposed;

        /// <summary>Read-only status for the Settings surface. This PI sync
        /// service has no atomic publication backend, so availability remains false.</summary>
        public RecordsSyncStatusSnapshot Status => BuildStatusSnapshot();

        /// <summary>Raised after local configuration or a sync pass changes
        /// the observable status. UI listeners should marshal to their dispatcher.</summary>
        public event Action<RecordsSyncStatusSnapshot>? StatusChanged;

        /// <summary>Returns a detached copy of current PI sync settings.
        /// Credential material is intentionally excluded.</summary>
        public RecordsSyncSettings GetSettingsSnapshot() => new()
        {
            Enabled = _settings.Enabled,
            SupabaseUrl = _settings.SupabaseUrl,
            AuthEmail = _settings.AuthEmail,
            CredentialBindingProjectUrl = _settings.CredentialBindingProjectUrl,
            CredentialBindingAuthEmail = _settings.CredentialBindingAuthEmail,
            SourceFile = _settings.SourceFile,
            SourceWorksheet = _settings.SourceWorksheet,
            IntervalMinutes = _settings.IntervalMinutes,
        };

        /// <summary>Validates a Supabase project root URL. Production endpoints
        /// must use HTTPS; plain HTTP is accepted only for loopback development.</summary>
        public static string NormalizeProjectUrl(string? value)
        {
            var candidate = (value ?? string.Empty).Trim();
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
                (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                 !(string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback)) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                (uri.AbsolutePath != "/" && !string.IsNullOrEmpty(uri.AbsolutePath)) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new ArgumentException(
                    "Enter the root HTTPS Supabase project URL without a path (HTTP is allowed only for a loopback development server).",
                    nameof(value));
            }

            return uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath.TrimEnd('/');
        }

        /// <summary>Verifies that the configured URL is the expected production
        /// project endpoint after normalizing a trailing slash/whitespace.</summary>
        public static bool IsExpectedProjectUrl(string? projectUrl)
        {
            try
            {
                return string.Equals(
                    NormalizeProjectUrl(projectUrl), ExpectedProjectUrl,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>Returns true only when the saved SecretVault credentials
        /// have explicit non-secret project/email association metadata matching
        /// the active settings. Legacy or mismatched credentials are unbound.</summary>
        public static bool CredentialBindingMatches(
            RecordsSyncSettings settings,
            string? projectUrl,
            string? authEmail)
        {
            ArgumentNullException.ThrowIfNull(settings);
            var activeEmail = (authEmail ?? string.Empty).Trim();
            var boundEmail = (settings.CredentialBindingAuthEmail ?? string.Empty).Trim();
            if (activeEmail.Length == 0 || boundEmail.Length == 0)
                return false;

            try
            {
                var activeUrl = NormalizeProjectUrl(projectUrl);
                var boundUrl = NormalizeProjectUrl(settings.CredentialBindingProjectUrl);
                return string.Equals(activeUrl, boundUrl, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(activeEmail, boundEmail, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>Stores connection metadata locally and credentials in the
        /// existing SecretVault. Existing Enabled/source/interval settings are
        /// preserved. This is local-only: it never tests the network, starts a
        /// timer, or sends PI rows.</summary>
        public async Task SaveConfigurationAsync(
            RecordsSyncSettings settings,
            string apiKey,
            string password,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (_disposed)
                throw new ObjectDisposedException(nameof(RecordsSyncService));

            var apiKeyValue = (apiKey ?? string.Empty).Trim();
            var passwordValue = password ?? string.Empty;
            if (apiKeyValue.Length == 0)
                throw new ArgumentException("A Supabase publishable/anon key is required.", nameof(apiKey));
            if (!SupabaseRecordsClient.IsPublishableOrAnonApiKey(apiKeyValue))
                throw new ArgumentException(
                    "Only a Supabase publishable or legacy anon key can be saved; secret and service-role keys are rejected.",
                    nameof(apiKey));
            if (passwordValue.Length == 0)
                throw new ArgumentException("A dedicated Records Sync account password is required.", nameof(password));

            var projectUrl = NormalizeProjectUrl(settings.SupabaseUrl);
            if (!IsExpectedProjectUrl(projectUrl))
                throw new ArgumentException(
                    "ProGlass Records Sync must use the configured production project URL: " + ExpectedProjectUrl,
                    nameof(settings));

            var normalized = new RecordsSyncSettings
            {
                // This page never enables/disables the existing worker or changes
                // its PI-only source scope and cadence.
                Enabled = _settings.Enabled,
                SupabaseUrl = projectUrl,
                AuthEmail = (settings.AuthEmail ?? string.Empty).Trim(),
                CredentialBindingProjectUrl = projectUrl,
                CredentialBindingAuthEmail = (settings.AuthEmail ?? string.Empty).Trim(),
                SourceFile = _settings.SourceFile,
                SourceWorksheet = _settings.SourceWorksheet,
                IntervalMinutes = _settings.IntervalMinutes,
            };
            if (normalized.AuthEmail.Length == 0)
                throw new ArgumentException("A dedicated Records Sync account email is required.", nameof(settings));
            if (!SupabaseRecordsClient.KeyHasNoProjectMismatch(apiKeyValue, normalized.SupabaseUrl))
                throw new ArgumentException(
                    "The legacy anon key project reference does not match the configured Supabase URL.",
                    nameof(apiKey));

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(RecordsSyncService));

                var hadOldKey = SecretVault.TryRetrieve(SecretKey, out var oldKey);
                var hadOldPassword = SecretVault.TryRetrieve(SecretPasswordKey, out var oldPassword);
                var keyMayHaveChanged = false;
                var passwordMayHaveChanged = false;
                try
                {
                    keyMayHaveChanged = true;
                    StoreAndVerifySecret(SecretKey, apiKeyValue);
                    passwordMayHaveChanged = true;
                    StoreAndVerifySecret(SecretPasswordKey, passwordValue);
                    PersistSettings(normalized);
                }
                catch
                {
                    if (passwordMayHaveChanged)
                        RestoreSecret(SecretPasswordKey, hadOldPassword, oldPassword);
                    if (keyMayHaveChanged)
                        RestoreSecret(SecretKey, hadOldKey, oldKey);
                    throw;
                }

                // No call to StartIfConfigured or timer re-arm here.
                _settings = normalized;
            }
            finally
            {
                _gate.Release();
            }

            RaiseStatusChanged();
        }

        private RecordsSyncStatusSnapshot BuildStatusSnapshot()
        {
            var state = LoadState();
            DateTimeOffset? lastSuccess = state.LastSuccessUtc == default
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(state.LastSuccessUtc, DateTimeKind.Utc));
            return new RecordsSyncStatusSnapshot
            {
                Enabled = _settings.Enabled,
                AtomicBackendAvailable = false,
                PendingCount = 0,
                LastSuccessfulSyncUtc = lastSuccess,
            };
        }

        private void RaiseStatusChanged()
        {
            var handlers = StatusChanged;
            if (handlers == null) return;

            var snapshot = BuildStatusSnapshot();
            foreach (Action<RecordsSyncStatusSnapshot> handler in handlers.GetInvocationList())
            {
                try { handler(snapshot); }
                catch (Exception ex)
                {
                    AppLogService.Debug("[RecordsSync] status listener failed: " + ex.Message);
                }
            }
        }

        private static void PersistSettings(RecordsSyncSettings settings)
        {
            var folder = Path.GetDirectoryName(SettingsPath);
            if (string.IsNullOrWhiteSpace(folder))
                throw new InvalidOperationException("Records Sync settings folder is unavailable.");

            Directory.CreateDirectory(folder);
            var temporaryPath = SettingsPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
                File.Move(temporaryPath, SettingsPath, overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
                catch (Exception ex)
                {
                    AppLogService.Warning("[RecordsSync] temporary settings file cleanup failed: " + ex.GetType().Name);
                }
            }
        }

        private static void StoreAndVerifySecret(string key, string value)
        {
            SecretVault.Store(key, value);
            if (!SecretVault.TryRetrieve(key, out var roundTrip) ||
                !string.Equals(value, roundTrip, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Credential storage could not be verified in the Windows credential vault.");
            }
        }

        private static void RestoreSecret(string key, bool hadPreviousValue, string previousValue)
        {
            try
            {
                if (hadPreviousValue)
                {
                    StoreAndVerifySecret(key, previousValue);
                }
                else
                {
                    SecretVault.Remove(key);
                    if (SecretVault.TryRetrieve(key, out _))
                        throw new InvalidOperationException("The previous credential could not be removed during rollback.");
                }
            }
            catch (Exception ex)
            {
                AppLogService.Error("[RecordsSync] credential rollback failed for " + key + ".", ex);
            }
        }

        /// <summary>Reads configuration and arms the timer IF the operator
        /// enabled and configured the integration. Never throws; a broken
        /// config file simply leaves the sync dormant (logged once).</summary>
        public void StartIfConfigured()
        {
            try
            {
                _settings = LoadSettings();
                RaiseStatusChanged();
                if (!_settings.Enabled)
                    return;

                if (!IsExpectedProjectUrl(_settings.SupabaseUrl))
                {
                    AppLogService.Warning(
                        "[RecordsSync] configured project URL does not match this app’s production Records Sync project — staying dormant.");
                    return;
                }

                if (!CredentialBindingMatches(_settings, _settings.SupabaseUrl, _settings.AuthEmail))
                {
                    AppLogService.Warning(
                        "[RecordsSync] saved credentials are not bound to the active project and Auth email — staying dormant until credentials are re-entered and saved in Settings.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_settings.SupabaseUrl) ||
                    !SecretVault.TryRetrieve(SecretKey, out var savedApiKey) ||
                    string.IsNullOrWhiteSpace(savedApiKey) ||
                    string.IsNullOrWhiteSpace(_settings.AuthEmail) ||
                    !SecretVault.TryRetrieve(SecretPasswordKey, out var savedPassword) ||
                    string.IsNullOrEmpty(savedPassword))
                {
                    AppLogService.Info(
                        "[RecordsSync] enabled but missing the project URL, publishable/anon key, or dedicated Auth credentials — staying dormant.");
                    return;
                }

                if (!SupabaseRecordsClient.IsPublishableOrAnonApiKey(savedApiKey) ||
                    !SupabaseRecordsClient.KeyHasNoProjectMismatch(savedApiKey, _settings.SupabaseUrl))
                {
                    AppLogService.Warning(
                        "[RecordsSync] saved key is not an allowed publishable/anon key for the configured project — staying dormant.");
                    return;
                }

                AppLogService.Info(
                    "[RecordsSync] auth mode: dedicated authenticated user (RLS-respecting).");

                var interval = TimeSpan.FromMinutes(
                    Math.Clamp(_settings.IntervalMinutes, 5, 1440));

                // First pass 2 minutes after launch: startup belongs to the
                // operator, not to the network.
                _timer = new Timer(
                    _ => _ = RunGuardedAsync(),
                    null,
                    TimeSpan.FromMinutes(2),
                    interval);

                AppLogService.Info(
                    $"[RecordsSync] armed — every {interval.TotalMinutes:0} min, " +
                    $"source '{_settings.SourceFile}/{_settings.SourceWorksheet}'.");
            }
            catch (Exception ex)
            {
                AppLogService.Error("[RecordsSync] failed to start; sync disabled.", ex);
            }
        }

        /// <summary>One full pass: extract → filter changed → send →
        /// reconcile → persist state. Returns rows written; safe to call
        /// from tests or a future manual "Sync now" affordance.</summary>
        public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
        {
            var settings = _settings;
            if (!IsExpectedProjectUrl(settings.SupabaseUrl))
            {
                throw new InvalidOperationException(
                    "Configured project URL does not match this app’s production Records Sync project.");
            }

            if (!CredentialBindingMatches(settings, settings.SupabaseUrl, settings.AuthEmail))
            {
                throw new InvalidOperationException(
                    "Saved Records Sync credentials are not bound to the active Supabase project and Auth email. Re-enter and save both credentials in Settings before syncing.");
            }

            if (string.IsNullOrWhiteSpace(settings.SupabaseUrl) ||
                string.IsNullOrWhiteSpace(settings.AuthEmail))
            {
                throw new InvalidOperationException(
                    "Records sync is not configured (missing project URL or dedicated Auth email).");
            }
            if (!SecretVault.TryRetrieve(SecretKey, out var apiKey) ||
                string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Records sync is not configured (missing publishable/anon key).");
            }
            if (!SecretVault.TryRetrieve(SecretPasswordKey, out var authPassword) ||
                string.IsNullOrEmpty(authPassword))
            {
                throw new InvalidOperationException(
                    "Records sync is not configured (missing dedicated Auth password).");
            }

            var rows = _source.Extract(settings.SourceFile, settings.SourceWorksheet);
            var state = LoadState();

            // Change detection: identical content hash → already on the
            // server → do not re-send. This is what makes a double run a
            // no-op instead of duplicated uploads.
            var changed = new List<RecordPayload>(rows.Count);
            foreach (var row in rows)
            {
                var hash = Hash(row);
                if (!state.Hashes.TryGetValue(row.PiNumber, out var previous) ||
                    previous != hash)
                {
                    changed.Add(row);
                }
            }

            var written = 0;
            var skipped = 0;
            using (var client = new SupabaseRecordsClient(
                       settings.SupabaseUrl, apiKey, settings.AuthEmail, authPassword))
            {
                // Upsert only content changes, but reconciliation must always
                // receive the COMPLETE active local PI set. Passing `changed`
                // here would deactivate every unchanged PI after any edit.
                (written, skipped) = await client.SyncAsync(
                    changed,
                    rows.Select(r => r.PiNumber).ToArray(),
                    settings.SourceFile,
                    settings.SourceWorksheet,
                    cancellationToken).ConfigureAwait(false);
            }

            if (skipped > 0)
                AppLogService.Info(
                    $"[RecordsSync] {skipped} row(s) skipped by version/ownership fences — will retry next pass.");

            // Drop PIs deleted locally. If any changed rows were skipped,
            // retain their old hashes (or leave new rows unhashed) so the
            // next pass retries them; we only have an aggregate skip count,
            // so conservatively retry all changed rows in that case.
            var changedPiNumbers = new HashSet<string>(
                changed.Select(r => r.PiNumber), StringComparer.Ordinal);
            var nextHashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (skipped > 0 && changedPiNumbers.Contains(row.PiNumber))
                {
                    if (state.Hashes.TryGetValue(row.PiNumber, out var previousHash))
                        nextHashes[row.PiNumber] = previousHash;
                    continue;
                }

                nextHashes[row.PiNumber] = Hash(row);
            }
            state.Hashes = nextHashes;

            state.LastSuccessUtc = DateTime.UtcNow;
            state.LastSentCount = written;
            SaveState(state);
            RaiseStatusChanged();
            return written;
        }

        private async Task RunGuardedAsync()
        {
            if (_disposed) return;
            if (!await _gate.WaitAsync(0).ConfigureAwait(false))
                return; // previous pass still running — skip this tick

            try
            {
                var written = await RunOnceAsync().ConfigureAwait(false);
                _backoffMinutes = 0;
                if (written > 0)
                    AppLogService.Info($"[RecordsSync] pass complete — {written} row(s) written.");
            }
            catch (Exception ex)
            {
                // Offline, DNS, timeout, locked DB, malformed row that
                // slipped through: all land here, all are survivable.
                _backoffMinutes = _backoffMinutes == 0 ? 1 : Math.Min(_backoffMinutes * 2, 60);
                AppLogService.Error(
                    $"[RecordsSync] pass failed ({ex.GetType().Name}: {ex.Message}); " +
                    $"retrying in ~{_backoffMinutes} min.", ex);

                RescheduleBackoff();
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Re-arms a one-shot timer for the backoff window; the
        /// regular cadence resumes on the next success (backoff reset).</summary>
        private void RescheduleBackoff()
        {
            try
            {
                _timer?.Dispose();
                _timer = new Timer(
                    _ => _ = RunGuardedAsync(),
                    null,
                    TimeSpan.FromMinutes(_backoffMinutes),
                    TimeSpan.FromMinutes(
                        Math.Clamp(_settings.IntervalMinutes, 5, 1440)));
            }
            catch (Exception ex)
            {
                AppLogService.Error("[RecordsSync] could not reschedule after failure.", ex);
            }
        }

        private static RecordsSyncSettings LoadSettings()
        {
            if (!File.Exists(SettingsPath))
                return new RecordsSyncSettings();
            try
            {
                return JsonSerializer.Deserialize<RecordsSyncSettings>(
                           File.ReadAllText(SettingsPath))
                       ?? new RecordsSyncSettings();
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "[RecordsSync] recordssync.json unreadable — using defaults (dormant).", ex);
                return new RecordsSyncSettings();
            }
        }

        private static RecordsSyncState LoadState()
        {
            if (!File.Exists(StatePath))
                return new RecordsSyncState();
            try
            {
                return JsonSerializer.Deserialize<RecordsSyncState>(
                           File.ReadAllText(StatePath))
                       ?? new RecordsSyncState();
            }
            catch (Exception)
            {
                return new RecordsSyncState(); // worst case: one full re-send
            }
        }

        private static void SaveState(RecordsSyncState state)
        {
            try
            {
                Directory.CreateDirectory(AppDataRoot.CurrentFolder);
                File.WriteAllText(StatePath, JsonSerializer.Serialize(state, JsonOptions));
            }
            catch (Exception ex)
            {
                // State is an optimization; losing it costs bandwidth once.
                AppLogService.Error("[RecordsSync] could not persist state.", ex);
            }
        }

        private static string Hash(RecordPayload row)
        {
            var json = JsonSerializer.Serialize(row);
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(bytes);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
            StatusChanged = null;
            _gate.Dispose();
        }
    }
}
