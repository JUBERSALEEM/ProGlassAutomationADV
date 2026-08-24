// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Audit/AuditDbContext.cs
// PROJECT: ProGlassAutomation
// MODULE: Audit / Activity Logging
//
// Dedicated SQLite DbContext for users + activity log.
// Database file: %AppData%\ProGlassAutomation\audit.db
//
// Kept isolated from all business databases so the audit trail can be
// backed up / inspected independently (same pattern as
// ProformaInvoicesDbContext).
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Audit;
using System;
using System.IO;

namespace ProGlassAutomation.Services.Audit
{
    /// <summary>
    /// EF Core context for audit.db (users + activity log).
    /// Create inside a using-block — contexts are short-lived by design.
    /// </summary>
    public class AuditDbContext : DbContext
    {
        public DbSet<AppUser> Users { get; set; }
        public DbSet<ActivityLogEntry> ActivityLog { get; set; }

        /// <summary>Pure path computation — no side effects.</summary>
        public static string DatabaseFilePath
        {
            get
            {
                var appDataFolder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ProGlassAutomation");

                return Path.Combine(appDataFolder, "audit.db");
            }
        }

        protected override void OnConfiguring(
            DbContextOptionsBuilder optionsBuilder)
        {
            // Ensure folder exists once per context creation.
            var folder = Path.GetDirectoryName(DatabaseFilePath)!;
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            // Senior audit (Aug 2026): audit.db is security-critical — WAL
            // + foreign_keys=ON + busy_timeout=5s guarantee no audit write is
            // lost to a transient lock, and cascade behaviour matches the model.
            ProGlassAutomation.Services.Database.SqliteResilience
                .ConfigureFor(optionsBuilder, DatabaseFilePath);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.HasIndex(e => e.Username).IsUnique();
            });

            modelBuilder.Entity<ActivityLogEntry>(entity =>
            {
                // Activity views always query by recency + section.
                entity.HasIndex(e => e.TimestampUtc)
                    .HasDatabaseName("IX_ActivityLog_TimestampUtc");
                entity.HasIndex(e => e.Section)
                    .HasDatabaseName("IX_ActivityLog_Section");
                entity.HasIndex(e => e.Username)
                    .HasDatabaseName("IX_ActivityLog_Username");
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
