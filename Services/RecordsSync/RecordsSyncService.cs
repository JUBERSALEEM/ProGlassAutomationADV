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
    /// app entropy) under "RecordsSync.SupabaseKey". URL and switches live
    /// in recordssync.json. Both absent → the service stays dormant.
    /// </summary>
    public sealed class RecordsSyncService : IDisposable
    {
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

        /// <summary>Reads configuration and arms the timer IF the operator
        /// enabled and configured the integration. Never throws; a broken
        /// config file simply leaves the sync dormant (logged once).</summary>
        public void StartIfConfigured()
        {
            try
            {
                _settings = LoadSettings();
                if (!_settings.Enabled)
                    return;

                if (string.IsNullOrWhiteSpace(_settings.SupabaseUrl) ||
                    !SecretVault.TryRetrieve(SecretKey, out _))
                {
                    AppLogService.Info(
                        "[RecordsSync] enabled but not configured (URL or key missing) — staying dormant.");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_settings.AuthEmail) &&
                    SecretVault.TryRetrieve(SecretPasswordKey, out _))
                {
                    AppLogService.Info(
                        "[RecordsSync] auth mode: dedicated authenticated user (RLS-respecting).");
                }
                else
                {
                    AppLogService.Info(
                        "[RecordsSync] auth mode: stored key used directly as Bearer — configure " +
                        "AuthEmail + '" + SecretPasswordKey + "' for the recommended authenticated-user mode.");
                }

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
            if (!SecretVault.TryRetrieve(SecretKey, out var apiKey) ||
                string.IsNullOrWhiteSpace(settings.SupabaseUrl))
            {
                throw new InvalidOperationException(
                    "Records sync is not configured (missing URL or key).");
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
            SecretVault.TryRetrieve(SecretPasswordKey, out var authPassword);
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
            _gate.Dispose();
        }
    }
}
