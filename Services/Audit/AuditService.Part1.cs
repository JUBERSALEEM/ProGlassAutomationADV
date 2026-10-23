// ═══════════════════════════════════════════════════════════════════════
// FILE: AuditService.Part1.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.Audit;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Licensing;

namespace ProGlassAutomation.Services.Audit
{
        public static partial class AuditService
    {
            // ── Active user session ───────────────────────────────────────────

        private static AppUser? _currentUser;

        /// <summary>Active user for this session. Null until sign-in succeeds.</summary>
        public static AppUser? CurrentUser => _currentUser;

        /// <summary>False when audit/user schema migrations failed; writes and sign-in fail closed.</summary>
        public static bool IsDatabaseReady => AuditDbMigrations.IsReady;

        /// <summary>Username attached to new audit entries.</summary>
        public static string CurrentUsername =>
            _currentUser?.Username ?? "system";

        /// <summary>Raised on the UI thread after each entry is written.</summary>
        public static event EventHandler<ActivityEventArgs>? ActivityLogged;

        /// <summary>Raised on the UI thread when the active user changes.</summary>
        public static event EventHandler? CurrentUserChanged;

        private static bool _initialized;
        private static readonly object _initLock = new();

        // ── Session (v2) ─────────────────────────────────────────────────

        /// <summary>Unique id of the running session (one app launch).</summary>
        public static string SessionId { get; } =
            "S-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

        /// <summary>When this session started.</summary>
        public static DateTime SessionStartedUtc { get; } = DateTime.UtcNow;

        // ══════════════════════════════════════════════════════════════
        //  INITIALIZATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates audit.db schema on first run. Safe to
        /// call multiple times. Never throws — audit
        /// failure must not block app startup.
        ///
        /// Commit 5 of the licensing refactor: no longer
        /// seeds a default admin/admin user. The
        /// splash detects an empty Users table and
        /// shows an interactive first-run setup
        /// wizard (FirstRunSetupPanel) that creates
        /// the first user with the operator's choice
        /// of credentials. The wizard requires
        /// username, display name, and a password
        /// (twice) — same validation rules as
        /// CreateUserAsync.
        /// </summary>
        public static async Task InitializeAsync()
        {
            try
            {
                using var db = new AuditDbContext();
                await db.Database.EnsureCreatedAsync();

                // Additive schema upgrades are a prerequisite for account
                // identity and audit writes. A collision or failed migration
                // leaves sign-in and bootstrap disabled until repaired.
                if (!AuditDbMigrations.Apply())
                {
                    lock (_initLock) _initialized = false;
                    _currentUser = null;
                    return;
                }

                lock (_initLock)
                {
                    if (_initialized) return;
                    _initialized = true;
                }

                // AUTH-1: do not treat any user as signed in.
                // Splash / startup run as "system" until LoginWindow
                // verifies a password. Auto-picking the lowest Id made
                // Presence and the audit trail claim admin was online
                // before anyone signed in.
                _currentUser = null;

                AppLogService.Debug(
                    $"[Audit] Initialized. Session {SessionId}, " +
                    "awaiting sign-in.");
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[Audit] Initialize failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Records the session login/logout boundary in the audit trail.
        /// </summary>
        public static void LogSessionBoundary(bool login)
        {
            Log(
                AuditSections.Users,
                login ? "Session started" : "Session ended",
                $"Session {SessionId} for {CurrentUsername}");
        }

        /// <summary>
        /// Records a REFUSED authentication attempt in the audit trail —
        /// a failed sign-in, or a password change whose current-password
        /// proof did not hold.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The successful halves of both flows have always been audited
        /// (<see cref="LogSessionBoundary"/> writes "Session started", and
        /// <c>ChangePasswordAsync</c> writes "Password changed"). The refused
        /// halves were written nowhere durable: the only record was an
        /// <c>AppLogService.Warning</c> in <c>LoginPanelViewModel</c> — a text
        /// file in <c>%AppData%</c> that <c>AppLogService</c> deletes after
        /// <c>RetentionDays</c> (14), and which is not carried in the backup
        /// set the way <c>audit.db</c> is.
        /// </para>
        /// <para>
        /// That asymmetry matters more here than in most products because
        /// sign-in lockout is deliberately DISABLED — see
        /// <c>LockoutRemainingSeconds</c> for the reasoning and for the
        /// condition under which that decision must be revisited. Unlimited
        /// attempts with no durable record means "did anyone try to get in
        /// while nobody was watching?" cannot be answered from anything that
        /// survives a fortnight. Refusals are recorded now, so a guessing
        /// attack leaves a trail even though it is still not blocked; blocking
        /// it is a separate decision with its own trade-off, and this change
        /// does not make it.
        /// </para>
        /// <para>
        /// A refused password change is the stronger signal of the two: the
        /// current-password proof is what stops a walk-up user taking over
        /// someone else's account, so a failure there means somebody had the
        /// dialog open and did not know the password.
        /// </para>
        /// <para>
        /// The name that was typed goes in the DETAILS, not the user field:
        /// <see cref="Log"/> attaches <c>CurrentUsername</c> automatically, and
        /// on a refused attempt that is either empty or the wrong person — the
        /// attempted name is the field an investigator actually needs. The
        /// password is never passed here and never written.
        /// </para>
        /// <para>
        /// Never throws. An audit failure must not turn a refused attempt into
        /// a crashed one — the same rule <c>OperationGuard</c> applies when it
        /// records a permission denial.
        /// </para>
        /// </remarks>
        private static void LogAuthRefusal(
            string action, string attemptedUsername, string reason)
        {
            try
            {
                Log(
                    AuditSections.Users,
                    action,
                    $"Attempted as '" +
                    $"{(string.IsNullOrWhiteSpace(attemptedUsername) ? "(blank)" : attemptedUsername)}' " +
                    $"— {reason}");
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "[AuditService] Audit write for a refused auth attempt failed: "
                    + ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  LOGGING (the one-line API used by every module)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Fire-and-forget audit write. NEVER throws. Safe from any
        /// thread. The active username is attached automatically.
        /// </summary>
        public static void Log(
            string section,
            string action,
            string details = "",
            string entityType = "",
            string entityId = "",
            string reason = "")
        {
            WriteEntry(BuildEntry(
                section, action, details, entityType, entityId, reason));
        }

        /// <summary>
        /// Audit write carrying a before/after change set — the Activity
        /// UI shows these as GitHub-style diffs.
        /// </summary>
        public static void LogWithChanges(
            string section,
            string action,
            string details,
            string entityType,
            string entityId,
            List<AuditChange> changes,
            string reason = "")
        {
            var entry = BuildEntry(
                section, action, details, entityType, entityId, reason);
            entry.ChangesJson = AuditChangeSerializer.ToJson(changes);
            WriteEntry(entry);
        }

        /// <summary>Awaitable variant for flows that need ordering guarantees.</summary>
        public static async Task LogAsync(
            string section,
            string action,
            string details = "",
            string entityType = "",
            string entityId = "",
            string reason = "")
        {
            await WriteEntryAsync(BuildEntry(
                section, action, details, entityType, entityId, reason));
        }

        private static ActivityLogEntry BuildEntry(
            string section,
            string action,
            string details,
            string entityType,
            string entityId,
            string reason = "")
        {
            return new ActivityLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Username = CurrentUsername,
                UserDisplayName = Truncate(_currentUser?.DisplayName ?? CurrentUsername, 120),
                UserRole = Truncate(_currentUser?.Role ?? "System", 30),
                MachineName = Truncate(GetWorkstationName(), 80),
                AppVersion = Truncate(GetAppVersion(), 40),
                LocalTimeZone = Truncate(GetLocalTimeZone(), 80),
                OperationId = "OP-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                SessionId = SessionId,
                Section = section ?? "",
                Action = action ?? "",
                Details = Truncate(details, 1000),
                EntityType = Truncate(entityType, 20),
                EntityId = Truncate(entityId, 60),
                Reason = Truncate(reason, 500)
            };
        }


        private static string GetWorkstationName()
        {
            try
            {
                var configured = ProGlassAutomation.App.Config?.DataConnectivity?.WorkstationName;
                return string.IsNullOrWhiteSpace(configured)
                    ? Environment.MachineName
                    : configured.Trim();
            }
            catch
            {
                return Environment.MachineName;
            }
        }

        private static string GetAppVersion()
        {
            try
            {
                var asm = Assembly.GetEntryAssembly() ?? typeof(AuditService).Assembly;
                return asm.GetName().Version?.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static string GetLocalTimeZone()
        {
            try { return TimeZoneInfo.Local.Id; }
            catch { return ""; }
        }

        private static void WriteEntry(ActivityLogEntry entry)
        {
            // Fire-and-forget by design (Log() is called from synchronous
            // business code), but the task is explicitly observed:
            // WriteEntryAsync already swallows and logs its own exceptions,
            // and this continuation is the backstop for anything thrown
            // outside that try — otherwise the failure would surface much
            // later as an UnobservedTaskException with no context.
            _ = WriteEntryAsync(entry).ContinueWith(t =>
                {
                    Logging.AppLogService.Error(
                        "Audit write task faulted.", t.Exception,
                        new Logging.LogContext
                        {
                            Module = "Audit",
                            Class = nameof(AuditService),
                            Operation = "WriteEntry"
                        });
                },
                System.Threading.CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private static async Task WriteEntryAsync(ActivityLogEntry entry)
        {
            try
            {
                using var db = new AuditDbContext();
                db.ActivityLog.Add(entry);
                await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());

                RaiseOnUi(() =>
                    ActivityLogged?.Invoke(
                        null, new ActivityEventArgs(entry)));
            }
            catch (Exception ex)
            {
                // AUDIT R-1 (RECURRENCE) — this was Debug.WriteLine only.
                // System.Diagnostics.Debug is [Conditional("DEBUG")], so in
                // the SHIPPED build the compiler removes the call and this
                // becomes an empty catch. That meant a failed audit write —
                // a locked, full or read-only audit.db — was completely
                // invisible: the app carried on as though the action had
                // been recorded when nothing was persisted.
                //
                // For an audit trail that is the one failure that must never
                // be silent, because its whole purpose is to be trusted
                // later. Routed to AppLogService, which writes to the
                // rolling log file in every configuration.
                //
                // Still non-throwing: logging must not crash the business
                // operation that triggered it.
                Logging.AppLogService.Error(
                    "Audit write failed — this activity was NOT recorded " +
                    $"in the audit trail (section '{entry.Section}', " +
                    $"action '{entry.Action}').",
                    ex,
                    new Logging.LogContext
                    {
                        Module = "Audit",
                        Class = nameof(AuditService),
                        Operation = "WriteEntry"
                    });
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  QUERIES (Activity views)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Most recent entries, newest first.
        /// section = null → all sections.
        /// </summary>
        public static async Task<List<ActivityLogEntry>> GetRecentAsync(
            int count = 100, string? section = null)
        {
            try
            {
                using var db = new AuditDbContext();

                IQueryable<ActivityLogEntry> query = db.ActivityLog;

                if (!string.IsNullOrWhiteSpace(section))
                    query = query.Where(e => e.Section == section);

                return await query
                    .OrderByDescending(e => e.TimestampUtc)
                    .Take(Math.Max(1, count))
                    .ToListAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[Audit] GetRecent failed: {ex.Message}");
                return new List<ActivityLogEntry>();
            }
        }

        /// <summary>Distinct section names that have activity (for filters).</summary>
        public static async Task<List<string>> GetSectionsWithActivityAsync()
        {
            try
            {
                using var db = new AuditDbContext();
                return await db.ActivityLog
                    .Select(e => e.Section)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToListAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Phase 26d: was a bare catch. An empty section list is
                // indistinguishable from "nobody has done anything yet", so a
                // corrupt or locked audit database presented as a brand-new
                // install with no history at all. GetRecentAsync already logs
                // on this path; this one did not.
                AppLogService.Error(
                    "[Audit] Could not read the activity sections list — the "
                    + "Diagnostics filters will appear empty.", ex);
                return new List<string>();
            }
        }

        /// <summary>Entry count per section (System Center overview).</summary>
        public static async Task<Dictionary<string, int>>
            GetSectionCountsAsync()
        {
            try
            {
                using var db = new AuditDbContext();
                var rows = await db.ActivityLog
                    .GroupBy(e => e.Section)
                    .Select(g => new { Section = g.Key, Count = g.Count() })
                    .ToListAsync().ConfigureAwait(false);

                return rows.ToDictionary(r => r.Section, r => r.Count);
            }
            catch (Exception ex)
            {
                // Phase 26d: System Center renders these counts directly, so a
                // read failure showed a wall of zeros that reads as "no
                // activity recorded" rather than "activity could not be read".
                AppLogService.Error(
                    "[Audit] Could not count activity per section — the System "
                    + "Center overview will show zeros.", ex);
                return new Dictionary<string, int>();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  USER MANAGEMENT
        // ══════════════════════════════════════════════════════════════

        /// <summary>All users ordered by creation date.</summary>
        public static async Task<List<AppUser>> GetUsersAsync()
        {
            try
            {
                using var db = new AuditDbContext();
                return await db.Users
                    .OrderBy(u => u.Id)
                    .ToListAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Phase 26d: the worst of the silent ones. User management
                // renders this list, so a transient read failure showed an
                // administrator an EMPTY user table — which looks exactly like
                // "the user accounts are gone", and invites them to recreate
                // accounts that in fact still exist.
                AppLogService.Error(
                    "[Audit] Could not read the user list — user management "
                    + "will appear empty even though the accounts still exist.",
                    ex);
                return new List<AppUser>();
            }
        }

        /// <summary>
        /// True when at least one user exists. On any schema or query error,
        /// returns true so startup shows login instead of first-run setup.
        /// The bootstrap operation independently uses a conditional INSERT
        /// and never treats an error as an empty table.
        /// </summary>
        public static async Task<bool> HasAnyUsersAsync()
        {
            try
            {
                if (!AuditDbMigrations.IsReady)
                    await InitializeAsync().ConfigureAwait(false);

                // Fail closed: on a schema/read failure, choose the login
                // surface, never the first-user bootstrap surface.
                if (!AuditDbMigrations.IsReady) return true;

                using var db = new AuditDbContext();
                return await db.Users.AnyAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "[Audit] Could not determine whether a user exists. " +
                    "Failing closed: first-user setup will not be offered.", ex);
                return true;
            }
        }

        /// <summary>
        /// Creates a normal account. This path is available only to a signed-in
        /// Administrator and refuses to create the first account; first-user
        /// bootstrap has a separate atomic, empty-table-only operation.
        /// </summary>
        public static async Task<(bool Success, string Error)>
            CreateUserAsync(
                string username,
                string displayName,
                string role,
                string password,
                string reason = "",
                string mobile = "",
                string email = "")
        {
            try
            {
                DemandAdministratorUserManagement("create a user");
                if (!AuditDbMigrations.IsReady)
                    return (false, "User database is not ready; account creation is disabled.");

                username = (username ?? string.Empty).Trim();
                displayName = (displayName ?? string.Empty).Trim();
                string normalizedUsername = UsernameNormalizer.Normalize(username);
                role = string.IsNullOrWhiteSpace(role)
                    ? AppRoles.Operator
                    : AppRoles.Normalize(role);

                if (username.Length < 3 || normalizedUsername.Length == 0)
                    return (false, "Username must be at least 3 characters.");
                if (username.Length > 60 || normalizedUsername.Length > 120)
                    return (false, "Username is too long.");
                if (string.IsNullOrWhiteSpace(displayName))
                    return (false, "Display name is required.");
                if (!AppRoles.All.Contains(role, StringComparer.Ordinal)
                    && !string.Equals(role, AppRoles.Operator, StringComparison.Ordinal))
                    return (false, "Select a recognized user role.");

                var policy = ProGlassAutomation.Services.Security.PasswordPolicy
                    .ValidateNewPassword(password, username, displayName);
                if (!policy.Accepted)
                    return (false, policy.Error);

                using var db = new AuditDbContext();
                if (!await db.Users.AnyAsync().ConfigureAwait(false))
                    return (false,
                        "No account exists yet. Use the protected first-run administrator setup.");

                if (await db.Users.AnyAsync(u =>
                        u.UsernameNormalized == normalizedUsername).ConfigureAwait(false))
                    return (false, $"Username '{username}' already exists (usernames ignore case).");

                var user = BuildUser(username, displayName, role, password);
                user.UsernameNormalized = normalizedUsername;
                user.CreatedAtUtc = DateTime.UtcNow;
                user.Mobile = TruncateContact(mobile, 40);
                user.Email = TruncateContact(email, 200);
                db.Users.Add(user);
                await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());

                Log(AuditSections.Users, "User created",
                    $"{displayName} ({username}) — role {role}",
                    "USER", username, reason);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Could not create user: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates the first account as canonical Administrator. The INSERT is
        /// conditional on NOT EXISTS in the same SQLite statement, so two
        /// concurrent first-run windows cannot both bootstrap different users.
        /// A failed read/write/schema check never falls through as "empty".
        /// </summary>
        public static async Task<(bool Success, string Error)>
            BootstrapFirstAdministratorAsync(
                string username,
                string displayName,
                string password,
                string reason = "First-run setup.")
        {
            try
            {
                ReadOnlyGate.Demand("bootstrap the first Administrator");
                if (!AuditDbMigrations.IsReady)
                    return (false, "User database is not ready; first-run setup is disabled.");

                username = (username ?? string.Empty).Trim();
                displayName = (displayName ?? string.Empty).Trim();
                string normalizedUsername = UsernameNormalizer.Normalize(username);
                if (username.Length < 3 || normalizedUsername.Length == 0)
                    return (false, "Username must be at least 3 characters.");
                if (username.Length > 60 || normalizedUsername.Length > 120)
                    return (false, "Username is too long.");
                if (string.IsNullOrWhiteSpace(displayName))
                    return (false, "Display name is required.");

                var policy = ProGlassAutomation.Services.Security.PasswordPolicy
                    .ValidateNewPassword(password, username, displayName);
                if (!policy.Accepted)
                    return (false, policy.Error);

                var user = BuildUser(
                    username, displayName, AppRoles.Administrator, password);
                user.UsernameNormalized = normalizedUsername;
                user.CreatedAtUtc = DateTime.UtcNow;

                using var db = new AuditDbContext();
                int inserted = await db.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO AppUsers
                        (Username, UsernameNormalized, DisplayName, Role, Mobile,
                         Email, PasswordHash, PasswordSalt, IsActive, CreatedAtUtc,
                         LastActiveAtUtc, FailedAttemptCount, LockedUntilUtc)
                    SELECT {user.Username}, {user.UsernameNormalized},
                           {user.DisplayName}, {user.Role}, {user.Mobile},
                           {user.Email}, {user.PasswordHash}, {user.PasswordSalt},
                           {user.IsActive}, {user.CreatedAtUtc},
                           {user.LastActiveAtUtc}, {user.FailedAttemptCount},
                           {user.LockedUntilUtc}
                    WHERE NOT EXISTS (SELECT 1 FROM AppUsers);")
                    .ConfigureAwait(false);

                if (inserted != 1)
                    return (false,
                        "First-run setup is closed because an account already exists. Sign in as an Administrator.");

                Log(AuditSections.Users, "First Administrator created",
                    $"{displayName} ({username}) — role {AppRoles.Administrator}",
                    "USER", username, reason);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "Protected first-Administrator bootstrap failed; no success was reported.", ex);
                return (false, $"Could not create the first Administrator: {ex.Message}");
            }
        }

        private static void DemandAdministratorUserManagement(string operation)
        {
            OperationGuard.Demand(AppPermissions.SettingsManage, operation);
            ReadOnlyGate.Demand(operation);
        }

        /// <summary>
        /// Updates a user's Sharing Center contact fields (mobile/email).
        /// Case-insensitive lookup like every other username read. Returns
        /// (success, error). Never touches credentials or role.
        /// </summary>
        public static async Task<(bool Success, string Error)>
            UpdateUserContactAsync(string username, string mobile, string email)
        {
            try
            {
                DemandAdministratorUserManagement("update user contact details");
                if (!AuditDbMigrations.IsReady)
                    return (false, "User database is not ready; contact changes are disabled.");

                username = (username ?? "").Trim();
                if (username.Length == 0)
                    return (false, "Select a user first.");

                using var db = new AuditDbContext();
                string lookup = UsernameNormalizer.Normalize(username);
                var user = await db.Users
                    .FirstOrDefaultAsync(u => u.UsernameNormalized == lookup)
                    .ConfigureAwait(false);
                if (user == null)
                    return (false, $"User '{username}' not found.");

                user.Mobile = TruncateContact(mobile, 40);
                user.Email = TruncateContact(email, 200);
                await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());

                // Refresh the live session copy so the composer shows the
                // new contact immediately when editing yourself.
                if (_currentUser != null &&
                    string.Equals(_currentUser.Username, user.Username,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _currentUser.Mobile = user.Mobile;
                    _currentUser.Email = user.Email;
                }

                Log(AuditSections.Users, "User contact updated",
                    user.Username, "USER", user.Username);
                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, $"Could not save contact: {ex.Message}");
            }
        }

        private static string TruncateContact(string? value, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            value = value.Trim();
            return value.Length <= maxLen ? value : value.Substring(0, maxLen);
        }

        /// <summary>
        /// Activate / deactivate a user. The target change is serialized with
        /// a SQLite write transaction so two administrators cannot both remove
        /// the last active Administrator based on the same stale count.
        /// </summary>
        public static async Task<(bool Success, string Error)> SetUserActiveAsync(
            int userId, bool isActive)
        {
            try
            {
                DemandAdministratorUserManagement("change a user's active state");
                if (!AuditDbMigrations.IsReady)
                    return (false, "User database is not ready; account changes are disabled.");

                using var db = new AuditDbContext();
                await db.Database.OpenConnectionAsync().ConfigureAwait(false);
                await using var tx = await db.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable)
                    .ConfigureAwait(false);

                var users = await db.Users.ToListAsync().ConfigureAwait(false);
                var user = users.FirstOrDefault(candidate => candidate.Id == userId);
                if (user == null) return (false, "User not found.");

                if (!UserAdministrationPolicy.HasActiveAdministratorAfterChange(
                        users, userId, isActive: isActive))
                    return (false,
                        "This change would leave the system without an active Administrator.");

                user.IsActive = isActive;
                await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
                await tx.CommitAsync().ConfigureAwait(false);

                if (_currentUser?.Id == user.Id)
                {
                    _currentUser.IsActive = user.IsActive;
                    RaiseOnUi(() => CurrentUserChanged?.Invoke(null, EventArgs.Empty));
                }

                Log(AuditSections.Users,
                    isActive ? "User activated" : "User deactivated",
                    user.Username, "USER", user.Username);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "Could not change a user's active state; the account was not reported as changed.", ex);
                return (false, $"Could not change account state: {ex.Message}");
            }
        }

        /// <summary>Changes a user's role; the canonical legacy alias is normalized.</summary>
        public static async Task<(bool Success, string Error)> UpdateUserRoleAsync(
            int userId, string role, string reason = "")
        {
            try
            {
                DemandAdministratorUserManagement("change a user's role");
                if (!AuditDbMigrations.IsReady)
                    return (false, "User database is not ready; role changes are disabled.");

                string normalizedRole = AppRoles.Normalize(role);
                if (!AppRoles.All.Contains(normalizedRole, StringComparer.Ordinal)
                    && !string.Equals(normalizedRole, AppRoles.Operator, StringComparison.Ordinal))
                    return (false, "Select a recognized user role.");

                using var db = new AuditDbContext();
                await db.Database.OpenConnectionAsync().ConfigureAwait(false);
                await using var tx = await db.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable)
                    .ConfigureAwait(false);

                var users = await db.Users.ToListAsync().ConfigureAwait(false);
                var user = users.FirstOrDefault(candidate => candidate.Id == userId);
                if (user == null) return (false, "User not found.");

                if (!UserAdministrationPolicy.HasActiveAdministratorAfterChange(
                        users, userId, role: normalizedRole))
                    return (false,
                        "This role change would leave the system without an active Administrator.");

                string previousRole = AppRoles.Normalize(user.Role);
                user.Role = normalizedRole;
                await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
                await tx.CommitAsync().ConfigureAwait(false);

                if (_currentUser?.Id == user.Id)
                {
                    _currentUser.Role = normalizedRole;
                    RaiseOnUi(() => CurrentUserChanged?.Invoke(null, EventArgs.Empty));
                }

                Log(AuditSections.Users, "User role changed",
                    $"{user.Username}: {previousRole} → {normalizedRole}",
                    "USER", user.Username, reason);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                AppLogService.Error("Could not change a user's role.", ex);
                return (false, $"Could not change user role: {ex.Message}");
            }
        }

        /// <summary>
        /// Account rows are retained because audit events are keyed by user
        /// identity. The operation exists as an explicit service boundary for
        /// callers: it refuses deletion (and specifically protects the last
        /// active Administrator); use deactivation instead.
        /// </summary>
        public static async Task<(bool Success, string Error)> DeleteUserAsync(
            int userId, string reason = "")
        {
            try
            {
                DemandAdministratorUserManagement("delete a user");
                if (!AuditDbMigrations.IsReady)
                    return (false, "User database is not ready; account changes are disabled.");

                using var db = new AuditDbContext();
                var users = await db.Users.ToListAsync().ConfigureAwait(false);
                var user = users.FirstOrDefault(candidate => candidate.Id == userId);
                if (user == null) return (false, "User not found.");

                if (AppRoles.IsAdministrator(user.Role) && user.IsActive
                    && UserAdministrationPolicy.CountActiveAdministrators(users) <= 1)
                    return (false,
                        "The last active Administrator cannot be deleted or removed. Activate another Administrator first.");

                Log(AuditSections.Users, "User deletion refused",
                    $"Deletion of {user.Username} was refused; accounts are retained for audit history.",
                    "USER", user.Username, reason);
                return (false,
                    "User accounts are retained for audit history. Deactivate the account instead.");
            }
            catch (Exception ex)
            {
                AppLogService.Error("Could not evaluate user deletion request.", ex);
                return (false, $"Could not delete user: {ex.Message}");
            }
        }

        /// <summary>
        /// Switches the active session user (verified by password).
        /// All subsequent audit entries carry the new username.
        /// </summary>
        public static async Task<(bool Success, string Error)>
            SwitchUserAsync(string username, string password)
        {
            try
            {
                if (!AuditDbMigrations.IsReady)
                    await InitializeAsync().ConfigureAwait(false);
                if (!AuditDbMigrations.IsReady)
                    return (false, "User database is not ready; sign-in is disabled.");

                using var db = new AuditDbContext();
                // USERNAME MATCHING IS CASE-INSENSITIVE.
                // AppUser.Username carries no COLLATE NOCASE -
                // AuditDbContext.cs:54 declares a plain unique index -
                // and SQLite's '=' on TEXT uses BINARY collation. So a
                // straight equality compared bytes: an operator who created
                // "Admin" at first-run setup and then typed "admin", or the
                // other way round, was told "Unknown username." and could
                // not get into their own ERP. Both sign-in and
                // change-password now fold case, so the account an operator
                // created is reachable whatever capitalisation they type -
                // and the two paths cannot disagree about who they are.
                string lookupName = UsernameNormalizer.Normalize(username);
                var user = await db.Users
                    .FirstOrDefaultAsync(u => u.UsernameNormalized == lookupName)
                    .ConfigureAwait(false);

                if (user == null)
                {
                    // The more interesting half of a guessing attack: this is
                    // how an account name gets enumerated, and it never
                    // touches a user row, so the FailedAttemptCount counter
                    // below cannot see it at all.
                    LogAuthRefusal("Sign-in failed", username, "unknown username");
                    return (false, "Unknown username.");
                }

                if (!user.IsActive)
                {
                    // A deactivated account being tried is worth knowing
                    // about: it is either a departed employee's login still in
                    // circulation, or somebody working down a list.
                    LogAuthRefusal("Sign-in failed", username, "account is deactivated");
                    return (false, "This user is deactivated.");
                }

                // PHASE 13 — persisted lockout: reject sign-ins while the
                // account is locked (survives an app restart).
                int lockoutSeconds = LockoutRemainingSeconds(user);
                if (lockoutSeconds > 0)
                {
                    LogAuthRefusal("Sign-in failed", username, "account is locked out");
                    return (false,
                        $"Account temporarily locked. Try again in {lockoutSeconds}s.");
                }

                if (!VerifyPassword(user, password, out bool needsUpgrade))
                {
                    // PHASE 13 — persist the failed attempt and trigger a
                    // lockout window once the threshold is reached. The count
                    // is reset when a window fires, so a user who waits out
                    // the window gets a fresh attempt budget.
                    user.FailedAttemptCount += 1;
                    if (user.FailedAttemptCount >= MaxFailedAttemptsBeforeLock)
                    {
                        user.LockedUntilUtc =
                            DateTime.UtcNow.AddMinutes(LockoutMinutes);
                        user.FailedAttemptCount = 0;
                    }
                    await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
                    LogAuthRefusal("Sign-in failed", username, "incorrect password");
                    return (false, "Incorrect password.");
                }

                // Success: clear any persisted lockout state.
                if (user.FailedAttemptCount != 0 || user.LockedUntilUtc != null)
                {
                    user.FailedAttemptCount = 0;
                    user.LockedUntilUtc = null;
                }

                user.LastActiveAtUtc = DateTime.UtcNow;
                await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());

                // A-2: silently migrate a legacy SHA-256 hash now that
                // the plaintext has been proven correct. Runs after the
                // sign-in is already committed, so a failure here cannot
                // prevent the user getting in.
                if (needsUpgrade)
                    await UpgradePasswordHashAsync(db, user, password);

                _currentUser = user;

                Log(AuditSections.Users, "User switched",
                    $"Active user is now {user.DisplayName}",
                    "USER", user.Username);

                RaiseOnUi(() => CurrentUserChanged?.Invoke(null, EventArgs.Empty));
                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, $"Switch failed: {ex.Message}");
            }
        }

        /// <summary>
        /// A-3: changes a user's password after proving the current one.
        ///
        /// Before this existed there was NO way to rotate any password in
        /// the entire application, so the seeded admin/admin credential was
        /// permanent. Verification uses the same dual-scheme path as
        /// sign-in, so a legacy SHA-256 hash can still be used to
        /// authenticate the change; the new hash is always PBKDF2.
        /// </summary>
        public static async Task<(bool Success, string Error)>
            ChangePasswordAsync(
                string username,
                string currentPassword,
                string newPassword,
                string reason = "")
        {
            try
            {
                if (!AuditDbMigrations.IsReady)
                    await InitializeAsync().ConfigureAwait(false);
                if (!AuditDbMigrations.IsReady)
                    return (false, "User database is not ready; password changes are disabled.");

                username = (username ?? "").Trim();

                // Same single home as creation: the only additional rule is
                // "must differ", which is about rotation, not strength.
                var policy = ProGlassAutomation.Services.Security.PasswordPolicy
                    .ValidateNewPassword(newPassword, username);
                if (!policy.Accepted)
                    return (false, policy.Error);

                if (currentPassword == newPassword)
                    return (false,
                        "New password must differ from the current one.");

                using var db = new AuditDbContext();
                // USERNAME MATCHING IS CASE-INSENSITIVE.
                // AppUser.Username carries no COLLATE NOCASE -
                // AuditDbContext.cs:54 declares a plain unique index -
                // and SQLite's '=' on TEXT uses BINARY collation. So a
                // straight equality compared bytes: an operator who created
                // "Admin" at first-run setup and then typed "admin", or the
                // other way round, was told "Unknown username." and could
                // not get into their own ERP. Both sign-in and
                // change-password now fold case, so the account an operator
                // created is reachable whatever capitalisation they type -
                // and the two paths cannot disagree about who they are.
                string lookupName = UsernameNormalizer.Normalize(username);
                var user = await db.Users
                    .FirstOrDefaultAsync(u => u.UsernameNormalized == lookupName)
                    .ConfigureAwait(false);

                if (user == null)
                {
                    LogAuthRefusal("Password change refused", username, "unknown username");
                    return (false, "Unknown username.");
                }

                if (!user.IsActive)
                {
                    LogAuthRefusal("Password change refused", username, "account is deactivated");
                    return (false, "This user is deactivated.");
                }

                // Proving the current password is what stops a walk-up
                // user silently taking over someone else's account.
                if (!VerifyPassword(user, currentPassword ?? ""))
                {
                    // The success path writes "Password changed". A failure
                    // used to write nothing at all, so the one attempt that
                    // most deserves a record — somebody holding the dialog
                    // open who does not know the password — was the only one
                    // that left no trace. The new password is not passed here.
                    LogAuthRefusal(
                        "Password change refused", username,
                        "current password incorrect");
                    return (false, "Current password is incorrect.");
                }

                user.PasswordHash =
                    Security.PasswordHasher.Hash(newPassword ?? "");
                await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());

                // The password itself is never logged — only the fact of
                // the change, so the audit trail stays useful without
                // becoming a credential store.
                Log(AuditSections.Users, "Password changed",
                    $"Password rotated for {user.DisplayName} ({user.Username})",
                    "USER", user.Username, reason);

                // Keep the in-memory copy consistent when the active user
                // rotates their own password.
                if (_currentUser != null && _currentUser.Id == user.Id)
                    _currentUser.PasswordHash = user.PasswordHash;

                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, $"Could not change password: {ex.Message}");
            }
        }

        /// <summary>
        /// PHASE 13 / forgot-password: an active Administrator resets another
        /// user's (or their own) password after proving the administrator's
        /// password. Used by the login screen's "Forgot password?" flow.
        ///
        /// This is the honest recovery path for a local, offline app: there is
        /// no email to mail a reset link to, so the gate is the local
        /// administrator's live credential. The reset also clears any
        /// persisted lockout state on the target account.
        /// </summary>
        public static async Task<(bool Success, string Error)>
            AdminResetPasswordAsync(
                string adminUsername,
                string adminPassword,
                string targetUsername,
                string newPassword,
                string reason = "")
        {
            try
            {
                if (!AuditDbMigrations.IsReady)
                    await InitializeAsync().ConfigureAwait(false);
                if (!AuditDbMigrations.IsReady)
                    return (false, "User database is not ready; password resets are disabled.");

                adminUsername = (adminUsername ?? "").Trim();
                targetUsername = (targetUsername ?? "").Trim();

                var policy = ProGlassAutomation.Services.Security.PasswordPolicy
                    .ValidateNewPassword(newPassword, targetUsername);
                if (!policy.Accepted)
                    return (false, policy.Error);

                using var db = new AuditDbContext();

                // USERNAME MATCHING IS CASE-INSENSITIVE — same rule as
                // sign-in and change-password (see SwitchUserAsync). These
                // two lookups used straight equality, so resetting the
                // password of "Admin" while typing "admin" failed with
                // "not found" on an account that provably exists.
                string adminLookup = UsernameNormalizer.Normalize(adminUsername);
                var admin = await db.Users
                    .FirstOrDefaultAsync(u => u.UsernameNormalized == adminLookup).ConfigureAwait(false);
                if (admin == null || !admin.IsActive)
                    return (false, "Administrator account not found or inactive.");

                // Accept both role spellings ("Administrator" seeded default
                // and "Admin" used by the user-management UI).
                if (!AppRoles.IsAdministrator(admin.Role))
                    return (false, "Only an Administrator can reset a password.");

                if (!VerifyPassword(admin, adminPassword ?? ""))
                    return (false, "Administrator password is incorrect.");

                string targetLookup = UsernameNormalizer.Normalize(targetUsername);
                var target = await db.Users
                    .FirstOrDefaultAsync(u => u.UsernameNormalized == targetLookup).ConfigureAwait(false);
                if (target == null)
                    return (false, $"User '{targetUsername}' not found.");
                if (!target.IsActive)
                    return (false, "That user is deactivated.");

                target.PasswordHash =
                    Security.PasswordHasher.Hash(newPassword ?? "");
                target.FailedAttemptCount = 0;
                target.LockedUntilUtc = null;
                await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());

                Log(AuditSections.Users, "Password reset",
                    $"Password reset for {target.DisplayName} " +
                    $"({target.Username}) by {admin.DisplayName}",
                    "USER", target.Username, reason);

                // Keep the in-memory copy consistent when the active user's
                // own password is reset by another admin.
                if (_currentUser != null && _currentUser.Id == target.Id)
                    _currentUser.PasswordHash = target.PasswordHash;

                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, $"Could not reset password: {ex.Message}");
            }
        }

        /// <summary>
        /// A-3: true when <paramref name="username"/> still authenticates
        /// with the seeded default password. Used to warn the operator.
        ///
        /// This is deliberately a check, not an enforcement: this build has
        /// no sign-in screen, so refusing to start would lock the user out
        /// of their own offline data with no recovery path.
        /// </summary>
        public static async Task<bool> IsUsingDefaultPasswordAsync(
            string username = DefaultAdminUsername)
        {
            try
            {
                if (!AuditDbMigrations.IsReady)
                    await InitializeAsync().ConfigureAwait(false);
                if (!AuditDbMigrations.IsReady) return false;

                using var db = new AuditDbContext();
                // Case-insensitive like every other username lookup: the
                // default-credential warning must fire for "Admin" as well
                // as "admin", or the banner silently misses the account it
                // exists to warn about.
                string lookup = UsernameNormalizer.Normalize(username);
                var user = await db.Users
                    .FirstOrDefaultAsync(u => u.UsernameNormalized == lookup).ConfigureAwait(false);

                if (user == null || !user.IsActive) return false;

                return VerifyPassword(user, DefaultAdminPassword);
            }
            catch
            {
                // A failed check must never be reported as "safe", but it
                // also must not break the caller: treat as "unknown / no
                // warning" and let the log record the failure elsewhere.
                return false;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  PASSWORD HASHING
        // ══════════════════════════════════════════════════════════════

        // PHASE 13 — persisted lockout policy.
        /// <summary>Consecutive failed attempts allowed before a lockout window.</summary>
    }
}
