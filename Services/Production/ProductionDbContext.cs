// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/ProductionDbContext.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — production.db context (Phase 2, 2026-09-11)
//
// Isolated SQLite database per project architecture (one database per
// domain): %AppData%\ProGlassAutomation\production.db
// New DbContext per operation via `using`, never held as a field —
// same rule as JobOrdersDbContext.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production.SavedProduction;
using System.IO;

namespace ProGlassAutomation.Services.Production
{
    public class ProductionDbContext : DbContext
    {
        public DbSet<SavedProductionRelease> Releases { get; set; }
        public DbSet<SavedProductionWorkItem> WorkItems { get; set; }
        public DbSet<SavedProductionEntry> Entries { get; set; }

        public static string DatabaseFilePath =>
            Path.Combine(
                ProGlassAutomation.Services.Database.AppDataRoot.CurrentFolder,
                "production.db");

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabaseFilePath)!);
            // WAL + busy_timeout + foreign_keys=ON — same resilience as
            // every other isolated database in this app.
            ProGlassAutomation.Services.Database.SqliteResilience
                .ConfigureFor(options, DatabaseFilePath);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var release = modelBuilder.Entity<SavedProductionRelease>();
            release.HasIndex(r => r.JobRef)
                   .IsUnique()
                   .HasDatabaseName("IX_ProductionRelease_JobRef_Unique");
            release.HasIndex(r => r.SourceProformaId)
                   .HasDatabaseName("IX_ProductionRelease_SourceProformaId");

            var item = modelBuilder.Entity<SavedProductionWorkItem>();
            item.HasIndex(i => i.UniqueKey)
                .IsUnique()
                .HasDatabaseName("IX_ProductionWorkItem_UniqueKey");
            item.HasIndex(i => i.JobRef)
                .HasDatabaseName("IX_ProductionWorkItem_JobRef");
            item.HasIndex(i => i.Stage)
                .HasDatabaseName("IX_ProductionWorkItem_Stage");
            item.HasIndex(i => i.Status)
                .HasDatabaseName("IX_ProductionWorkItem_Status");
            item.HasIndex(i => i.SpecGroupNumber)
                .HasDatabaseName("IX_ProductionWorkItem_SpecGroupNumber");

            var entry = modelBuilder.Entity<SavedProductionEntry>();
            entry.HasIndex(e => e.WorkItemId)
                 .HasDatabaseName("IX_ProductionEntry_WorkItemId");
            entry.HasIndex(e => new { e.JobRef, e.Stage, e.SpecGroupNumber, e.SerialNumber })
                 .HasDatabaseName("IX_ProductionEntry_Unit");
        }
    }
}
