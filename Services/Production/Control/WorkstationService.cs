// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Control/WorkstationService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 17 Workstation / Resource Master
//   Factory-level workstation/resource assignment where architecture safely supports it.
//   Concept: Process → Workstation → Assigned WorkItems
//   Examples: CUT-01, TEMPER-01, POLISH-01, CNC-01, LAM-01
//   Do NOT integrate actual machines, do NOT assume PLC/CNC APIs, ERP-level only.
//   Smallest reusable master-data structure necessary, no second database, uses production.db
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production.Control
{
    public static class WorkstationService
    {
        private static bool _seeded;

        public static async Task EnsureSeededAsync(CancellationToken ct = default)
        {
            if (_seeded) return;
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            var existing = await db.Workstations.AsNoTracking().CountAsync(ct).ConfigureAwait(false);
            if (existing > 0) { _seeded = true; return; }

            var defaults = new List<SavedWorkstation>
            {
                new SavedWorkstation { WorkstationId = "CUT-01", Code = "CUT-01", Name = "Cutting Station 01", ProcessType = 1, ProcessName = "Cutting", IsActive = true, Description = "Primary cutting table", CreatedBy = "System", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new SavedWorkstation { WorkstationId = "CUT-02", Code = "CUT-02", Name = "Cutting Station 02", ProcessType = 1, ProcessName = "Cutting", IsActive = true, Description = "Secondary cutting table", CreatedBy = "System", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new SavedWorkstation { WorkstationId = "CNC-01", Code = "CNC-01", Name = "CNC Holes/Cutouts 01", ProcessType = 2, ProcessName = "HolesCutouts", IsActive = true, Description = "CNC for holes and cutouts", CreatedBy = "System", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new SavedWorkstation { WorkstationId = "POLISH-01", Code = "POLISH-01", Name = "Polish Station 01", ProcessType = 3, ProcessName = "Polish", IsActive = true, Description = "Edge polish", CreatedBy = "System", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new SavedWorkstation { WorkstationId = "EDGEDEL-01", Code = "EDGEDEL-01", Name = "Edge Deletion 01", ProcessType = 4, ProcessName = "EdgeDeletion", IsActive = true, Description = "Edge deletion", CreatedBy = "System", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new SavedWorkstation { WorkstationId = "MITRING-01", Code = "MITRING-01", Name = "Mitring Station 01", ProcessType = 5, ProcessName = "Mitring", IsActive = true, Description = "Mitring", CreatedBy = "System", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new SavedWorkstation { WorkstationId = "TEMPER-01", Code = "TEMPER-01", Name = "Tempering Furnace 01", ProcessType = 6, ProcessName = "Tempering", IsActive = true, Description = "Tempering furnace", CreatedBy = "System", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new SavedWorkstation { WorkstationId = "PAIR-01", Code = "PAIR-01", Name = "Pairing Station 01", ProcessType = 7, ProcessName = "Pairing", IsActive = true, Description = "DGU pairing", CreatedBy = "System", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new SavedWorkstation { WorkstationId = "LAM-01", Code = "LAM-01", Name = "Lamination Station 01", ProcessType = 8, ProcessName = "DoublingLamination", IsActive = true, Description = "Doubling/Lamination", CreatedBy = "System", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new SavedWorkstation { WorkstationId = "DGU-01", Code = "DGU-01", Name = "DGU Assembly 01", ProcessType = 8, ProcessName = "DoublingLamination", IsActive = true, Description = "DGU doubling", CreatedBy = "System", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
            };

            db.Workstations.AddRange(defaults);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            _seeded = true;
        }

        public static async Task<List<SavedWorkstation>> GetAllAsync(CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.Workstations.AsNoTracking().OrderBy(w => w.ProcessType).ThenBy(w => w.Code).ToListAsync(ct).ConfigureAwait(false);
        }

        public static async Task<List<SavedWorkstation>> GetByProcessAsync(int processType, CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.Workstations.AsNoTracking().Where(w => w.ProcessType == processType && w.IsActive).OrderBy(w => w.Code).ToListAsync(ct).ConfigureAwait(false);
        }

        public static async Task<SavedWorkstation?> GetByIdAsync(string workstationId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(workstationId)) return null;
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.Workstations.AsNoTracking().FirstOrDefaultAsync(w => w.WorkstationId == workstationId, ct).ConfigureAwait(false);
        }

        public static async Task<SavedWorkstation> CreateAsync(string code, string name, int processType, string description, string createdBy, CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();

            code = (code ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Workstation code required", nameof(code));

            var existing = await db.Workstations.FirstOrDefaultAsync(w => w.WorkstationId == code, ct).ConfigureAwait(false);
            if (existing != null) return existing;

            var ws = new SavedWorkstation
            {
                WorkstationId = code,
                Code = code,
                Name = string.IsNullOrWhiteSpace(name) ? code : name.Trim(),
                ProcessType = processType,
                ProcessName = ((Models.Production.ProductionStage)processType).ToString(),
                IsActive = true,
                Description = description ?? "",
                CreatedBy = createdBy ?? "System",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            db.Workstations.Add(ws);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return ws;
        }
    }
}
