using ProGlassAutomation.Models.Audit;
using ProGlassAutomation.Services.Audit;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Audit
{
    public class AuthNormalizationTests
    {
        [Theory]
        [InlineData("  juber  ", "JUBER")]
        [InlineData("Ａlice", "ALICE")]
        [InlineData("Admin", "ADMIN")]
        public void Username_normalizer_trims_folds_compatibility_and_case(
            string input, string expected)
        {
            Assert.Equal(expected, UsernameNormalizer.Normalize(input));
        }

        [Fact]
        public void Compatibility_and_case_variants_share_one_identity_key()
        {
            Assert.Equal(
                UsernameNormalizer.Normalize("Alice"),
                UsernameNormalizer.Normalize("  ＡＬＩＣＥ  "));
            Assert.Equal(
                UsernameNormalizer.Normalize("KATE"),
                UsernameNormalizer.Normalize("Kate"));
        }

        [Fact]
        public void Empty_username_normalizes_to_empty()
        {
            Assert.Equal(string.Empty, UsernameNormalizer.Normalize("  \t "));
        }

        [Fact]
        public void Legacy_admin_spelling_is_canonical_administrator()
        {
            Assert.Equal(AppRoles.Administrator, AppRoles.Normalize(" Admin "));
            Assert.True(AppRoles.IsAdministrator("ADMIN"));
            Assert.True(RolePermissions.Has("admin", AppPermissions.SettingsManage));
        }

        [Fact]
        public void Last_active_administrator_cannot_be_deactivated_or_demoted()
        {
            var users = new[]
            {
                User(1, AppRoles.Administrator, active: true),
                User(2, AppRoles.Operator, active: true)
            };

            Assert.False(UserAdministrationPolicy.HasActiveAdministratorAfterChange(
                users, 1, isActive: false));
            Assert.False(UserAdministrationPolicy.HasActiveAdministratorAfterChange(
                users, 1, role: AppRoles.Manager));
        }

        [Fact]
        public void Change_is_allowed_when_another_active_administrator_remains()
        {
            var users = new[]
            {
                User(1, AppRoles.Administrator, active: true),
                User(2, "Admin", active: true),
                User(3, AppRoles.Operator, active: true)
            };

            Assert.True(UserAdministrationPolicy.HasActiveAdministratorAfterChange(
                users, 1, isActive: false));
            Assert.True(UserAdministrationPolicy.HasActiveAdministratorAfterChange(
                users, 1, role: AppRoles.Manager));
            Assert.Equal(2, UserAdministrationPolicy.CountActiveAdministrators(users));
        }

        [Fact]
        public void Unknown_user_cannot_be_used_to_bypass_admin_count()
        {
            Assert.False(UserAdministrationPolicy.HasActiveAdministratorAfterChange(
                new[] { User(1, AppRoles.Administrator, active: true) },
                targetUserId: 99, isActive: false));
        }

        [Fact]
        public void Database_and_migration_enforce_unique_normalized_username_key()
        {
            string context = ReadRepoFile("Services/Audit/AuditDbContext.cs");
            string migration = ReadRepoFile("Services/Audit/AuditDbMigrations.cs");

            Assert.Contains("HasIndex(e => e.UsernameNormalized)", context);
            Assert.Contains(".IsUnique()", context);
            Assert.Contains("CREATE UNIQUE INDEX IF NOT EXISTS IX_AppUsers_UsernameNormalized", migration);
            Assert.Contains("BackfillNormalizedUsernames(connection)", migration);
            Assert.Contains("Username normalization found a collision", migration);
            Assert.Contains("RequireNoDuplicateKeyValues", migration);
            Assert.Contains("VerifyRequiredSchema(connection)", migration);
            Assert.Contains("RequireIndex(connection, \"IX_AppUsers_UsernameNormalized\"", migration);
            Assert.Contains("RequireNoEmptyValues", migration);
        }

        [Fact]
        public void First_user_bootstrap_fails_closed_and_uses_a_conditional_insert()
        {
            string service = ReadRepoFile("Services/Audit/AuditService.Part1.cs");

            Assert.Contains("if (!AuditDbMigrations.IsReady) return true", service);
            Assert.Contains("Failing closed: first-user setup will not be offered", service);
            Assert.Contains("WHERE NOT EXISTS (SELECT 1 FROM AppUsers)", service);
            Assert.Contains("if (inserted != 1)", service);
        }

        [Fact]
        public void Delete_user_is_retained_and_protects_the_last_active_administrator()
        {
            string service = ReadRepoFile("Services/Audit/AuditService.Part1.cs");
            int start = service.IndexOf("public static async Task<(bool Success, string Error)> DeleteUserAsync(",
                StringComparison.Ordinal);
            Assert.True(start >= 0);
            int end = service.IndexOf("SwitchUserAsync(", start, StringComparison.Ordinal);
            Assert.True(end > start);

            string method = service.Substring(start, end - start);
            Assert.Contains("CountActiveAdministrators(users) <= 1", method);
            Assert.Contains("User deletion refused", method);
            Assert.Contains("accounts are retained for audit history", method);
            Assert.Contains("BeginTransactionAsync(IsolationLevel.Serializable)", service);
        }

        [Fact]
        public void Login_looks_up_the_same_normalized_key_that_creation_stores()
        {
            string service = ReadRepoFile("Services/Audit/AuditService.Part1.cs");
            string other = ReadRepoFile("Services/Audit/AuditService.Part2.cs");

            Assert.Contains("UsernameNormalized = UsernameNormalizer.Normalize(username)", other);
            Assert.Contains("u.UsernameNormalized == lookupName", service);
            Assert.Contains("u.UsernameNormalized == adminLookup", service);
            Assert.Contains("u.UsernameNormalized == targetLookup", service);
        }

        private static string ReadRepoFile(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ProGlassAutomation.csproj")))
                    return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                "Could not locate repository root from " + AppContext.BaseDirectory);
        }

        private static AppUser User(int id, string role, bool active) => new()
        {
            Id = id,
            Username = $"user-{id}",
            UsernameNormalized = $"USER-{id}",
            DisplayName = $"User {id}",
            Role = role,
            IsActive = active
        };
    }
}
