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

        // Phase 14 foundations
        public DbSet<SavedMaterialReservation> Reservations { get; set; }
        public DbSet<SavedMaterialAllocation> Allocations { get; set; }
        public DbSet<SavedProductionQcEntry> QcEntries { get; set; }
        public DbSet<SavedActualCost> ActualCosts { get; set; }
        public DbSet<SavedProductionReplacement> Replacements { get; set; }

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
            item.HasIndex(i => i.IsFoc)
                .HasDatabaseName("IX_ProductionWorkItem_IsFoc");
            item.HasIndex(i => i.IsCancelled)
                .HasDatabaseName("IX_ProductionWorkItem_IsCancelled");
            item.HasIndex(i => i.ParentWorkItemId)
                .HasDatabaseName("IX_ProductionWorkItem_ParentWorkItemId");

            var entry = modelBuilder.Entity<SavedProductionEntry>();
            entry.HasIndex(e => e.WorkItemId)
                 .HasDatabaseName("IX_ProductionEntry_WorkItemId");
            entry.HasIndex(e => new { e.JobRef, e.Stage, e.SpecGroupNumber, e.SerialNumber })
                 .HasDatabaseName("IX_ProductionEntry_Unit");
            entry.HasIndex(e => e.EntryType)
                 .HasDatabaseName("IX_ProductionEntry_EntryType");
            entry.HasIndex(e => e.ParentEntryId)
                 .HasDatabaseName("IX_ProductionEntry_ParentEntryId");
            entry.HasIndex(e => e.IsFoc)
                 .HasDatabaseName("IX_ProductionEntry_IsFoc");
            entry.HasIndex(e => e.RelatedReservationId)
                 .HasDatabaseName("IX_ProductionEntry_RelatedReservationId");
            entry.HasIndex(e => e.RelatedQcId)
                 .HasDatabaseName("IX_ProductionEntry_RelatedQcId");

            // Phase 14: Reservations
            var res = modelBuilder.Entity<SavedMaterialReservation>();
            res.HasIndex(r => r.ReservationId)
               .IsUnique()
               .HasDatabaseName("IX_MaterialReservation_ReservationId_Unique");
            res.HasIndex(r => r.JobRef)
               .HasDatabaseName("IX_MaterialReservation_JobRef");
            res.HasIndex(r => r.GlassSheetId)
               .HasDatabaseName("IX_MaterialReservation_GlassSheetId");
            res.HasIndex(r => r.Status)
               .HasDatabaseName("IX_MaterialReservation_Status");
            res.HasIndex(r => r.OptimizationSessionId)
               .HasDatabaseName("IX_MaterialReservation_OptSession");

            // Phase 14: Allocations
            var alloc = modelBuilder.Entity<SavedMaterialAllocation>();
            alloc.HasIndex(a => a.AllocationId)
                 .IsUnique()
                 .HasDatabaseName("IX_MaterialAllocation_AllocationId_Unique");
            alloc.HasIndex(a => a.ReservationId)
                 .HasDatabaseName("IX_MaterialAllocation_ReservationId");
            alloc.HasIndex(a => a.JobRef)
                 .HasDatabaseName("IX_MaterialAllocation_JobRef");
            alloc.HasIndex(a => a.OptimizationSessionId)
                 .HasDatabaseName("IX_MaterialAllocation_OptSession");
            alloc.HasIndex(a => a.Status)
                 .HasDatabaseName("IX_MaterialAllocation_Status");

            // Phase 14: QC
            var qc = modelBuilder.Entity<SavedProductionQcEntry>();
            qc.HasIndex(q => q.QcId)
              .IsUnique()
              .HasDatabaseName("IX_ProductionQc_QcId_Unique");
            qc.HasIndex(q => q.WorkItemId)
              .HasDatabaseName("IX_ProductionQc_WorkItemId");
            qc.HasIndex(q => q.JobRef)
              .HasDatabaseName("IX_ProductionQc_JobRef");
            qc.HasIndex(q => q.Result)
              .HasDatabaseName("IX_ProductionQc_Result");
            qc.HasIndex(q => q.Stage)
              .HasDatabaseName("IX_ProductionQc_Stage");

            // Phase 14: Actual Costs
            var cost = modelBuilder.Entity<SavedActualCost>();
            cost.HasIndex(c => c.CostId)
                .IsUnique()
                .HasDatabaseName("IX_ActualCost_CostId_Unique");
            cost.HasIndex(c => c.JobRef)
                .HasDatabaseName("IX_ActualCost_JobRef");
            cost.HasIndex(c => c.CostType)
                .HasDatabaseName("IX_ActualCost_CostType");
            cost.HasIndex(c => c.IsFoc)
                .HasDatabaseName("IX_ActualCost_IsFoc");

            // Phase 14: Replacements
            var repl = modelBuilder.Entity<SavedProductionReplacement>();
            repl.HasIndex(r => r.ReplacementId)
                .IsUnique()
                .HasDatabaseName("IX_Replacement_ReplacementId_Unique");
            repl.HasIndex(r => r.JobRef)
                .HasDatabaseName("IX_Replacement_JobRef");
            repl.HasIndex(r => r.OriginalWorkItemId)
                .HasDatabaseName("IX_Replacement_OriginalWorkItemId");
            repl.HasIndex(r => r.ReplacementWorkItemId)
                .HasDatabaseName("IX_Replacement_ReplacementWorkItemId");
            repl.HasIndex(r => r.EntryType)
                .HasDatabaseName("IX_Replacement_EntryType");
            repl.HasIndex(r => r.IsFoc)
                .HasDatabaseName("IX_Replacement_IsFoc");
        }
    }
}
