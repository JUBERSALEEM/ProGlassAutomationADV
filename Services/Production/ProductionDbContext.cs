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

        // Phase 15 cutting + 2D optimization workflow
        public DbSet<SavedCuttingSession> CuttingSessions { get; set; }
        public DbSet<SavedCuttingSheet> CuttingSheets { get; set; }
        public DbSet<SavedCuttingPlacedPiece> CuttingPlacedPieces { get; set; }

        // Phase 16 core production process execution
        public DbSet<SavedProcessExecution> ProcessExecutions { get; set; }
        public DbSet<SavedProcessPairing> ProcessPairings { get; set; }
        public DbSet<SavedProcessAssembly> ProcessAssemblies { get; set; }

        // Phase 17 production control & factory readiness
        public DbSet<SavedWorkstation> Workstations { get; set; }
        public DbSet<SavedProductionControlHold> ControlHolds { get; set; }
        public DbSet<SavedProductionControlAssignment> ControlAssignments { get; set; }
        public DbSet<SavedProductionControlOverride> ControlOverrides { get; set; }

        public static string DatabaseFilePath =>
            Path.Combine(
                ProGlassAutomation.Services.Database.AppDataRoot.CurrentFolder,
                "production.db");

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // Shared-aware folder ensure (2026-09-24) — see AppDataRoot.
            ProGlassAutomation.Services.Database.AppDataRoot.EnsureCurrentFolder();
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

            // Phase 15: Cutting sessions
            var cutSession = modelBuilder.Entity<SavedCuttingSession>();
            cutSession.HasIndex(c => c.CuttingSessionId)
                .IsUnique()
                .HasDatabaseName("IX_CuttingSession_SessionId_Unique");
            cutSession.HasIndex(c => c.JobRef)
                .HasDatabaseName("IX_CuttingSession_JobRef");
            cutSession.HasIndex(c => c.OptimizationSessionId)
                .HasDatabaseName("IX_CuttingSession_OptSession");
            cutSession.HasIndex(c => c.Status)
                .HasDatabaseName("IX_CuttingSession_Status");
            cutSession.HasIndex(c => c.MaterialGroupId)
                .HasDatabaseName("IX_CuttingSession_MaterialGroup");

            // Phase 15: Cutting sheets
            var cutSheet = modelBuilder.Entity<SavedCuttingSheet>();
            cutSheet.HasIndex(s => s.CuttingSheetId)
                .IsUnique()
                .HasDatabaseName("IX_CuttingSheet_SheetId_Unique");
            cutSheet.HasIndex(s => s.CuttingSessionId)
                .HasDatabaseName("IX_CuttingSheet_SessionId");
            cutSheet.HasIndex(s => s.JobRef)
                .HasDatabaseName("IX_CuttingSheet_JobRef");
            cutSheet.HasIndex(s => s.StockSheetId)
                .HasDatabaseName("IX_CuttingSheet_StockSheetId");
            cutSheet.HasIndex(s => s.SheetNo)
                .HasDatabaseName("IX_CuttingSheet_SheetNo");

            // Phase 15: Cutting placed pieces
            var cutPiece = modelBuilder.Entity<SavedCuttingPlacedPiece>();
            cutPiece.HasIndex(p => p.PlacedPieceId)
                .IsUnique()
                .HasDatabaseName("IX_CuttingPlacedPiece_PieceId_Unique");
            cutPiece.HasIndex(p => p.CuttingSheetId)
                .HasDatabaseName("IX_CuttingPlacedPiece_SheetId");
            cutPiece.HasIndex(p => p.CuttingSessionId)
                .HasDatabaseName("IX_CuttingPlacedPiece_SessionId");
            cutPiece.HasIndex(p => p.JobRef)
                .HasDatabaseName("IX_CuttingPlacedPiece_JobRef");
            cutPiece.HasIndex(p => p.PieceId)
                .HasDatabaseName("IX_CuttingPlacedPiece_PieceId");
            cutPiece.HasIndex(p => p.SourceWorkItemId)
                .HasDatabaseName("IX_CuttingPlacedPiece_SourceWorkItemId");
            cutPiece.HasIndex(p => p.Status)
                .HasDatabaseName("IX_CuttingPlacedPiece_Status");
            cutPiece.HasIndex(p => p.ReservationId)
                .HasDatabaseName("IX_CuttingPlacedPiece_ReservationId");
            cutPiece.HasIndex(p => p.AllocationId)
                .HasDatabaseName("IX_CuttingPlacedPiece_AllocationId");

            // Phase 16: Process executions
            var procExec = modelBuilder.Entity<SavedProcessExecution>();
            procExec.HasIndex(e => e.ExecutionId)
                .IsUnique()
                .HasDatabaseName("IX_ProcessExecution_ExecutionId_Unique");
            procExec.HasIndex(e => e.JobRef)
                .HasDatabaseName("IX_ProcessExecution_JobRef");
            procExec.HasIndex(e => e.ProcessType)
                .HasDatabaseName("IX_ProcessExecution_ProcessType");
            procExec.HasIndex(e => e.Status)
                .HasDatabaseName("IX_ProcessExecution_Status");
            procExec.HasIndex(e => e.PieceId)
                .HasDatabaseName("IX_ProcessExecution_PieceId");
            procExec.HasIndex(e => e.OutputWorkItemId)
                .HasDatabaseName("IX_ProcessExecution_OutputWorkItemId");
            procExec.HasIndex(e => e.InputWorkItemId)
                .HasDatabaseName("IX_ProcessExecution_InputWorkItemId");
            procExec.HasIndex(e => e.ParentExecutionId)
                .HasDatabaseName("IX_ProcessExecution_ParentExecutionId");
            procExec.HasIndex(e => new { e.JobRef, e.SpecGroupNumber, e.SerialNumber })
                .HasDatabaseName("IX_ProcessExecution_Unit");

            // Phase 16: Pairings
            var pairing = modelBuilder.Entity<SavedProcessPairing>();
            pairing.HasIndex(p => p.PairingId)
                .IsUnique()
                .HasDatabaseName("IX_ProcessPairing_PairingId_Unique");
            pairing.HasIndex(p => p.JobRef)
                .HasDatabaseName("IX_ProcessPairing_JobRef");
            pairing.HasIndex(p => p.PairId)
                .HasDatabaseName("IX_ProcessPairing_PairId");
            pairing.HasIndex(p => new { p.JobRef, p.SpecGroupNumber, p.SerialNumber })
                .HasDatabaseName("IX_ProcessPairing_Unit");

            // Phase 16: Assemblies
            var assembly = modelBuilder.Entity<SavedProcessAssembly>();
            assembly.HasIndex(a => a.AssemblyId)
                .IsUnique()
                .HasDatabaseName("IX_ProcessAssembly_AssemblyId_Unique");
            assembly.HasIndex(a => a.JobRef)
                .HasDatabaseName("IX_ProcessAssembly_JobRef");
            assembly.HasIndex(a => a.AssemblyLabel)
                .HasDatabaseName("IX_ProcessAssembly_AssemblyLabel");
            assembly.HasIndex(a => a.PairingId)
                .HasDatabaseName("IX_ProcessAssembly_PairingId");
            assembly.HasIndex(a => new { a.JobRef, a.SpecGroupNumber, a.SerialNumber })
                .HasDatabaseName("IX_ProcessAssembly_Unit");

            // Phase 17: Workstations
            var ws = modelBuilder.Entity<SavedWorkstation>();
            ws.HasIndex(w => w.WorkstationId)
                .IsUnique()
                .HasDatabaseName("IX_Workstation_WorkstationId_Unique");
            ws.HasIndex(w => w.Code)
                .HasDatabaseName("IX_Workstation_Code");
            ws.HasIndex(w => w.ProcessType)
                .HasDatabaseName("IX_Workstation_ProcessType");
            ws.HasIndex(w => w.IsActive)
                .HasDatabaseName("IX_Workstation_IsActive");

            // Phase 17: Control Holds
            var hold = modelBuilder.Entity<SavedProductionControlHold>();
            hold.HasIndex(h => h.HoldId)
                .IsUnique()
                .HasDatabaseName("IX_ControlHold_HoldId_Unique");
            hold.HasIndex(h => h.WorkItemId)
                .HasDatabaseName("IX_ControlHold_WorkItemId");
            hold.HasIndex(h => h.JobRef)
                .HasDatabaseName("IX_ControlHold_JobRef");
            hold.HasIndex(h => h.IsReleased)
                .HasDatabaseName("IX_ControlHold_IsReleased");
            hold.HasIndex(h => new { h.JobRef, h.SpecGroupNumber, h.SerialNumber })
                .HasDatabaseName("IX_ControlHold_Unit");

            // Phase 17: Control Assignments
            var assign = modelBuilder.Entity<SavedProductionControlAssignment>();
            assign.HasIndex(a => a.AssignmentId)
                .IsUnique()
                .HasDatabaseName("IX_ControlAssignment_AssignmentId_Unique");
            assign.HasIndex(a => a.WorkItemId)
                .HasDatabaseName("IX_ControlAssignment_WorkItemId");
            assign.HasIndex(a => a.JobRef)
                .HasDatabaseName("IX_ControlAssignment_JobRef");
            assign.HasIndex(a => a.WorkstationId)
                .HasDatabaseName("IX_ControlAssignment_WorkstationId");
            assign.HasIndex(a => a.IsActive)
                .HasDatabaseName("IX_ControlAssignment_IsActive");

            // Phase 17: Control Overrides (Priority/DueDate)
            var ctrl = modelBuilder.Entity<SavedProductionControlOverride>();
            ctrl.HasIndex(c => c.ControlId)
                .IsUnique()
                .HasDatabaseName("IX_ControlOverride_ControlId_Unique");
            ctrl.HasIndex(c => c.WorkItemId)
                .HasDatabaseName("IX_ControlOverride_WorkItemId");
            ctrl.HasIndex(c => c.JobRef)
                .HasDatabaseName("IX_ControlOverride_JobRef");
            ctrl.HasIndex(c => c.Priority)
                .HasDatabaseName("IX_ControlOverride_Priority");
            ctrl.HasIndex(c => c.DueDate)
                .HasDatabaseName("IX_ControlOverride_DueDate");
        }
    }
}
