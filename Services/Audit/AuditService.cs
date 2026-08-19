// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Audit/AuditService.cs
// PROJECT: ProGlassAutomation
// MODULE: Audit / Activity Logging + User Management
//
// Static facade for the whole audit subsystem. Every module writes
// audit entries through Audit.Log(...) — one line, never throws, safe
// to call from anywhere (UI thread or background).
//
// DESIGN RULES
//   • Logging NEVER breaks business flows: every write is wrapped in
//     try/catch and runs on a background thread.
//   • Entries are insert-only. The trail is trustworthy.
//   • The active user is attached to every entry automatically.
//   • ActivityLogged event lets views (Activity tabs, System Center)
//     live-update as actions happen in any section.
//
// USAGE
//   await AuditService.InitializeAsync();              // App startup
//   AuditService.Log(AuditSections.DeliveryNote,
//                    "Saved", "DN-2026-003-01 (12 pcs)",
//                    "DN", "DN-2026-003-01");
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Audit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ProGlassAutomation.Services.Audit
{
    /// <summary>One audit entry broadcast through the ActivityLogged event.</summary>
    public class ActivityEventArgs : EventArgs
    {
        public ActivityLogEntry Entry { get; }
        public ActivityEventArgs(ActivityLogEntry entry) => Entry = entry;
    }

    /// <summary>
    /// Application-wide audit + user service (static facade).
    /// Initialized once in App.xaml.cs during startup.
    /// </summary>
    public static class AuditService
    {
        // ── Active user session ───────────────────────────────────────────

        private static AppUser? _currentUser;

        /// <summary>Active user for this session. Null until sign-in succeeds.</summary>
        public static AppUser? CurrentUser => _currentUser;

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
        /// Creates audit.db schema and seeds the default admin user on
        /// first run. Safe to call multiple times. Never throws —
        /// audit failure must not block app startup.
        /// </summary>
        public static async Task InitializeAsync()
        {
            try
            {
                using var db = new AuditDbContext();
                await db.Database.EnsureCreatedAsync();

                // v2 additive schema upgrades (SessionId, ChangesJson,
                // RecordMetadata, RecordVersions) — safe on existing DBs.
                AuditDbMigrations.Apply();

                lock (_initLock)
                {
                    if (_initialized) return;
                    _initialized = true;
                }

                // Seed default admin so there is always an active user.
                bool hasUsers = await db.Users.AnyAsync();
                if (!hasUsers)
                {
                    // A-3: seeded from the same constants the
                    // default-password detector uses, so the two can never
                    // drift apart.
                    var admin = BuildUser(
                        username: DefaultAdminUsername,
                        displayName: "Administrator",
                        role: AppRoles.Administrator,
                        password: DefaultAdminPassword);

                    admin.CreatedAtUtc = DateTime.UtcNow;
                    db.Users.Add(admin);
                    await db.SaveChangesAsync();
                }

                // AUTH-1: do not treat the first seeded user as signed in.
                // Splash / startup run as "system" until LoginWindow verifies
                // a password. Auto-picking the lowest Id made Presence and
                // the audit trail claim admin was online before anyone signed in.
                _currentUser = null;

                System.Diagnostics.Debug.WriteLine(
                    $"[Audit] Initialized. Session {SessionId}, " +
                    "awaiting sign-in.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
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
                SessionId = SessionId,
                Section = section ?? "",
                Action = action ?? "",
                Details = Truncate(details, 1000),
                EntityType = Truncate(entityType, 20),
                EntityId = Truncate(entityId, 60),
                Reason = Truncate(reason, 500)
            };
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
                await db.SaveChangesAsync();

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
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
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
                    .ToListAsync();
            }
            catch
            {
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
                    .ToListAsync();

                return rows.ToDictionary(r => r.Section, r => r.Count);
            }
            catch
            {
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
                    .ToListAsync();
            }
            catch
            {
                return new List<AppUser>();
            }
        }

        /// <summary>
        /// Creates a new user. Returns (success, error).
        /// Validates username uniqueness and minimum password length.
        /// </summary>
        public static async Task<(bool Success, string Error)>
            CreateUserAsync(
                string username,
                string displayName,
                string role,
                string password,
                string reason = "")
        {
            try
            {
                username = (username ?? "").Trim();
                displayName = (displayName ?? "").Trim();
                role = string.IsNullOrWhiteSpace(role) ? "Operator" : role.Trim();

                if (username.Length < 3)
                    return (false,
                        "Username must be at least 3 characters.");

                if (string.IsNullOrWhiteSpace(displayName))
                    return (false, "Display name is required.");

                if ((password ?? "").Length <
                    ProGlassAutomation.Services.Security.PasswordHasher.MinLength)
                    return (false,
                        $"Password must be at least " +
                        $"{ProGlassAutomation.Services.Security.PasswordHasher.MinLength} characters.");

                using var db = new AuditDbContext();

                bool exists = await db.Users
                    .AnyAsync(u => u.Username == username);

                if (exists)
                    return (false,
                        $"Username '{username}' already exists.");

                var user = BuildUser(username, displayName, role, password);
                user.CreatedAtUtc = DateTime.UtcNow;

                db.Users.Add(user);
                await db.SaveChangesAsync();

                Log(AuditSections.Users, "User created",
                    $"{displayName} ({username}) — role {role}",
                    "USER", username, reason);

                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, $"Could not create user: {ex.Message}");
            }
        }

        /// <summary>Activate / deactivate a user (never deletes them).</summary>
        public static async Task<bool> SetUserActiveAsync(
            int userId, bool isActive)
        {
            try
            {
                using var db = new AuditDbContext();
                var user = await db.Users.FindAsync(userId);
                if (user == null) return false;

                // Guard: never allow deactivating the last active admin.
                if (!isActive && user.Role == "Admin")
                {
                    int activeAdmins = await db.Users
                        .CountAsync(u => u.IsActive && u.Role == "Admin");
                    if (activeAdmins <= 1) return false;
                }

                user.IsActive = isActive;
                await db.SaveChangesAsync();

                Log(AuditSections.Users,
                    isActive ? "User activated" : "User deactivated",
                    user.Username, "USER", user.Username);

                return true;
            }
            catch
            {
                return false;
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
                using var db = new AuditDbContext();
                var user = await db.Users
                    .FirstOrDefaultAsync(u => u.Username == username);

                if (user == null)
                    return (false, "Unknown username.");

                if (!user.IsActive)
                    return (false, "This user is deactivated.");

                // PHASE 13 — persisted lockout: reject sign-ins while the
                // account is locked (survives an app restart).
                int lockoutSeconds = LockoutRemainingSeconds(user);
                if (lockoutSeconds > 0)
                    return (false,
                        $"Account temporarily locked. Try again in {lockoutSeconds}s.");

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
                    await db.SaveChangesAsync();
                    return (false, "Incorrect password.");
                }

                // Success: clear any persisted lockout state.
                if (user.FailedAttemptCount != 0 || user.LockedUntilUtc != null)
                {
                    user.FailedAttemptCount = 0;
                    user.LockedUntilUtc = null;
                }

                user.LastActiveAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();

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
                username = (username ?? "").Trim();

                if ((newPassword ?? "").Length <
                    ProGlassAutomation.Services.Security.PasswordHasher.MinLength)
                    return (false,
                        $"New password must be at least " +
                        $"{ProGlassAutomation.Services.Security.PasswordHasher.MinLength} characters.");

                if (currentPassword == newPassword)
                    return (false,
                        "New password must differ from the current one.");

                using var db = new AuditDbContext();
                var user = await db.Users
                    .FirstOrDefaultAsync(u => u.Username == username);

                if (user == null)
                    return (false, "Unknown username.");

                if (!user.IsActive)
                    return (false, "This user is deactivated.");

                // Proving the current password is what stops a walk-up
                // user silently taking over someone else's account.
                if (!VerifyPassword(user, currentPassword ?? ""))
                    return (false, "Current password is incorrect.");

                user.PasswordHash =
                    Security.PasswordHasher.Hash(newPassword ?? "");
                await db.SaveChangesAsync();

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
                adminUsername = (adminUsername ?? "").Trim();
                targetUsername = (targetUsername ?? "").Trim();

                if ((newPassword ?? "").Length <
                    ProGlassAutomation.Services.Security.PasswordHasher.MinLength)
                    return (false,
                        $"Password must be at least " +
                        $"{ProGlassAutomation.Services.Security.PasswordHasher.MinLength} characters.");

                using var db = new AuditDbContext();

                var admin = await db.Users
                    .FirstOrDefaultAsync(u => u.Username == adminUsername);
                if (admin == null || !admin.IsActive)
                    return (false, "Administrator account not found or inactive.");

                // Accept both role spellings ("Administrator" seeded default
                // and "Admin" used by the user-management UI).
                if (admin.Role != AppRoles.Administrator &&
                    admin.Role != "Admin")
                    return (false, "Only an Administrator can reset a password.");

                if (!VerifyPassword(admin, adminPassword ?? ""))
                    return (false, "Administrator password is incorrect.");

                var target = await db.Users
                    .FirstOrDefaultAsync(u => u.Username == targetUsername);
                if (target == null)
                    return (false, $"User '{targetUsername}' not found.");
                if (!target.IsActive)
                    return (false, "That user is deactivated.");

                target.PasswordHash =
                    Security.PasswordHasher.Hash(newPassword ?? "");
                target.FailedAttemptCount = 0;
                target.LockedUntilUtc = null;
                await db.SaveChangesAsync();

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
                using var db = new AuditDbContext();
                var user = await db.Users
                    .FirstOrDefaultAsync(u => u.Username == username);

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
        public const int MaxFailedAttemptsBeforeLock = 5;

        /// <summary>Length of the lockout window after the threshold is hit.</summary>
        public const int LockoutMinutes = 5;

        /// <summary>
        /// Seconds remaining in the persisted lockout window for
        /// <paramref name="username"/> (0 = not locked). Used by the sign-in
        /// UI to show a countdown. Never throws.
        /// </summary>
        public static async Task<int> LockoutRemainingSecondsAsync(
            string username)
        {
            try
            {
                using var db = new AuditDbContext();
                var user = await db.Users
                    .FirstOrDefaultAsync(u => u.Username == username);
                return user == null ? 0 : LockoutRemainingSeconds(user);
            }
            catch
            {
                // A failed check must never crash the sign-in flow.
                return 0;
            }
        }

        private static int LockoutRemainingSeconds(AppUser user)
        {
            if (user.LockedUntilUtc == null) return 0;
            var remaining = user.LockedUntilUtc.Value - DateTime.UtcNow;
            return remaining.TotalSeconds > 0
                ? (int)Math.Ceiling(remaining.TotalSeconds)
                : 0;
        }

        /// <summary>Seeded first-run administrator account name.</summary>
        public const string DefaultAdminUsername = "admin";

        /// <summary>
        /// Seeded first-run password. Public so the UI can detect that it
        /// is still in force; it is a well-known default, not a secret.
        /// </summary>
        public const string DefaultAdminPassword = "admin";

        private static AppUser BuildUser(
            string username, string displayName,
            string role, string password)
        {
            string salt = GenerateSalt();

            return new AppUser
            {
                Username = username,
                DisplayName = displayName,
                Role = role,
                PasswordSalt = salt,
                PasswordHash = HashPassword(password, salt),
                IsActive = true
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  AUDIT A-2 — PBKDF2 replaces single-round SHA-256
        //
        //  The old scheme was SHA256(saltHex + password). The salt was
        //  fine (16 random bytes, per user) but SHA-256 is fast by
        //  design, so audit.db — an ordinary file in %AppData% that is
        //  also copied into every backup set — was brute-forceable on a
        //  commodity GPU. Hashing now goes through
        //  Services/Security/PasswordHasher (PBKDF2-HMAC-SHA256,
        //  210,000 iterations, OWASP-recommended).
        //
        //  MIGRATION IS SILENT AND REQUIRES NO USER ACTION. Existing
        //  installations already hold SHA-256 hashes; rejecting them
        //  would lock every user out of their own ERP on upgrade.
        //  VerifyPassword accepts both formats and reports when an old
        //  one was used, and the caller re-hashes with PBKDF2 using the
        //  password just proven correct.
        // ══════════════════════════════════════════════════════════════

        private static string GenerateSalt()
        {
            // Retained for the AppUser.PasswordSalt column, which stays
            // populated for legacy rows. PBKDF2 carries its own salt
            // inside the stored value, so new rows do not depend on it.
            var bytes = new byte[16];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes);
        }

        private static string HashPassword(string password, string saltHex)
            => Security.PasswordHasher.Hash(password ?? "");

        /// <summary>
        /// Verifies a password against either hashing scheme.
        /// <paramref name="needsUpgrade"/> is true when the stored hash
        /// was the legacy SHA-256 form and should be re-hashed.
        /// </summary>
        private static bool VerifyPassword(
            AppUser user, string password, out bool needsUpgrade)
        {
            var result = Security.PasswordHasher.Verify(
                password, user.PasswordHash, user.PasswordSalt);

            needsUpgrade = result.NeedsUpgrade;
            return result.Success;
        }

        private static bool VerifyPassword(AppUser user, string password)
            => VerifyPassword(user, password, out _);

        /// <summary>
        /// Re-hashes a verified password with the current scheme and
        /// persists it. Best-effort: a migration failure must never
        /// block a sign-in that has already succeeded.
        /// </summary>
        private static async Task UpgradePasswordHashAsync(
            AuditDbContext db, AppUser user, string password)
        {
            try
            {
                user.PasswordHash = Security.PasswordHasher.Hash(password);
                await db.SaveChangesAsync();

                Logging.AppLogService.Info(
                    $"Password hash upgraded to PBKDF2 for '{user.Username}'.",
                    new Logging.LogContext
                    {
                        Module = "Security",
                        Class = nameof(AuditService),
                        Operation = "UpgradePasswordHash"
                    });
            }
            catch (Exception ex)
            {
                // The user is already authenticated; they simply keep the
                // legacy hash and we retry on their next sign-in.
                Logging.AppLogService.Warning(
                    $"Password hash upgrade failed for '{user.Username}': {ex.Message}",
                    new Logging.LogContext
                    {
                        Module = "Security",
                        Class = nameof(AuditService),
                        Operation = "UpgradePasswordHash"
                    });
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        private static string Truncate(string value, int maxLen)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= maxLen ? value : value[..maxLen];
        }

        private static void RaiseOnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            if (dispatcher.CheckAccess()) action();
            else dispatcher.InvokeAsync(action);
        }
    }
}
