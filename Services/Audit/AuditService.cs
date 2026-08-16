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

        /// <summary>Active user for this session (never null after init).</summary>
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
                    var admin = BuildUser(
                        username: "admin",
                        displayName: "Administrator",
                        role: AppRoles.Administrator,
                        password: "admin");

                    admin.CreatedAtUtc = DateTime.UtcNow;
                    db.Users.Add(admin);
                    await db.SaveChangesAsync();
                }

                _currentUser = await db.Users
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.Id)
                    .FirstOrDefaultAsync();

                if (_currentUser != null)
                {
                    _currentUser.LastActiveAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[Audit] Initialized. Session {SessionId}, " +
                    $"active user: {CurrentUsername}");
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
            _ = WriteEntryAsync(entry);
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
                // Logging must never crash the app.
                System.Diagnostics.Debug.WriteLine(
                    $"[Audit] Write failed: {ex.Message}");
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

                if ((password ?? "").Length < 4)
                    return (false,
                        "Password must be at least 4 characters.");

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

                if (!VerifyPassword(user, password))
                    return (false, "Incorrect password.");

                user.LastActiveAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();

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

        // ══════════════════════════════════════════════════════════════
        //  PASSWORD HASHING
        // ══════════════════════════════════════════════════════════════

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

        private static string GenerateSalt()
        {
            var bytes = new byte[16];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes);
        }

        private static string HashPassword(string password, string saltHex)
        {
            var input = Encoding.UTF8.GetBytes(saltHex + (password ?? ""));
            var hash = SHA256.HashData(input);
            return Convert.ToHexString(hash);
        }

        private static bool VerifyPassword(AppUser user, string password)
        {
            var expected = HashPassword(password, user.PasswordSalt);
            return string.Equals(
                expected, user.PasswordHash,
                StringComparison.OrdinalIgnoreCase);
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
