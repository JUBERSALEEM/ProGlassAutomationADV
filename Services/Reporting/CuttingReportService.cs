// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/CuttingReportService.cs
// PROJECT: ProGlassAutomation
// MODULE: Reporting — Phase 15 Cutting Sheet Report, Optimization Layout Report,
//         Material Consumption Report (reuses existing report generator architecture)
//   Reports:
//   - Cutting Sheet Report: Job PI Spec Material Sheet dims number Pieces Utilization Wastage Rotation Verification Operator
//   - Optimization Layout Report: same + layout details
//   - Material Consumption Report: Summary Job PI Spec Material Sheet dims number Pieces Utilization Wastage
//   No new report engine, reuses existing ReportingDataService + PrintTokens + GDS.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Reporting
{
    public sealed class CuttingSheetReportRow
    {
        public string JobRef { get; set; } = string.Empty;
        public string PiRef { get; set; } = string.Empty;
        public string SpecName { get; set; } = string.Empty;
        public string SpecGroupNumber { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Thickness { get; set; } = string.Empty;
        public string SheetLabel { get; set; } = string.Empty;
        public string SheetDims { get; set; } = string.Empty;
        public int SheetNo { get; set; }
        public int PiecesCount { get; set; }
        public string PiecesList { get; set; } = string.Empty;
        public decimal UtilizationPct { get; set; }
        public decimal WastagePct { get; set; }
        public string Rotation { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public string VerificationResult { get; set; } = string.Empty;
        public string OperatorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public sealed class MaterialConsumptionReportRow
    {
        public string JobRef { get; set; } = string.Empty;
        public string PiRef { get; set; } = string.Empty;
        public string SpecName { get; set; } = string.Empty;
        public string MaterialGroup { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Thickness { get; set; } = string.Empty;
        public string SheetSize { get; set; } = string.Empty;
        public int SheetsUsed { get; set; }
        public int TotalPieces { get; set; }
        public decimal TotalAreaSqm { get; set; }
        public decimal StockAreaSqm { get; set; }
        public decimal UtilizationPct { get; set; }
        public decimal WastagePct { get; set; }
        public int ReservedQty { get; set; }
        public int AllocatedQty { get; set; }
        public int ConsumedQty { get; set; }
        public string Traceability { get; set; } = string.Empty;
    }

    public static class CuttingReportService
    {
        /// <summary>
        /// Generate cutting sheet report for a job — reuses existing report data flow.
        /// </summary>
        public static async Task<List<CuttingSheetReportRow>> GenerateCuttingSheetReportAsync(
            string jobRef,
            CancellationToken ct = default)
        {
            var rows = new List<CuttingSheetReportRow>();
            try
            {
                Production.ProductionRepository.EnsureInitialized();
                using var db = new Production.ProductionDbContext();

                var sessions = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .ToListAsync(
                        db.CuttingSessions.Where(c => c.JobRef == jobRef),
                        ct).ConfigureAwait(false);

                foreach (var session in sessions)
                {
                    ct.ThrowIfCancellationRequested();

                    var sheets = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                        .ToListAsync(db.CuttingSheets.Where(s => s.CuttingSessionId == session.CuttingSessionId), ct)
                        .ConfigureAwait(false);

                    foreach (var sheet in sheets)
                    {
                        var pieces = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                            .ToListAsync(db.CuttingPlacedPieces.Where(p => p.CuttingSheetId == sheet.CuttingSheetId), ct)
                            .ConfigureAwait(false);

                        string piecesList = string.Join(", ", pieces.Select(p => $"{p.PieceLabel} {p.WidthMm}×{p.HeightMm} {(p.IsRotated ? "90°" : "0°")}"));

                        rows.Add(new CuttingSheetReportRow
                        {
                            JobRef = session.JobRef,
                            PiRef = session.SourceProformaId,
                            SpecName = pieces.FirstOrDefault()?.SpecName ?? session.MaterialGroupId,
                            SpecGroupNumber = pieces.FirstOrDefault()?.SpecGroupNumber.ToString() ?? "",
                            Material = sheet.Color,
                            Color = sheet.Color,
                            Thickness = sheet.ThicknessMm.ToString("0.##"),
                            SheetLabel = sheet.StockSizeLabel,
                            SheetDims = $"{sheet.StockWidthMm} × {sheet.StockHeightMm}",
                            SheetNo = sheet.SheetNo,
                            PiecesCount = pieces.Count,
                            PiecesList = piecesList,
                            UtilizationPct = sheet.UtilizationPct,
                            WastagePct = sheet.WastagePct,
                            Rotation = pieces.Any(p => p.IsRotated) ? "Mixed 0°/90°" : "0°",
                            IsVerified = sheet.IsVerified,
                            VerificationResult = sheet.VerificationResult,
                            OperatorName = sheet.CreatedBy,
                            CreatedAt = sheet.CreatedAt
                        });
                    }
                }

                return rows.OrderBy(r => r.SheetNo).ToList();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                AppLogService.Error("[CuttingReport] GenerateCuttingSheetReport failed",
                    ex, new LogContext { Module = "Reporting", Class = nameof(CuttingReportService), Operation = nameof(GenerateCuttingSheetReportAsync) });
                return rows;
            }
        }

        /// <summary>
        /// Generate material consumption report — summary Job PI Spec Material Sheet dims number Pieces Utilization Wastage.
        /// </summary>
        public static async Task<List<MaterialConsumptionReportRow>> GenerateMaterialConsumptionReportAsync(
            string jobRef,
            CancellationToken ct = default)
        {
            var rows = new List<MaterialConsumptionReportRow>();
            try
            {
                Production.ProductionRepository.EnsureInitialized();
                using var db = new Production.ProductionDbContext();

                var sessions = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .ToListAsync(db.CuttingSessions.Where(c => c.JobRef == jobRef), ct).ConfigureAwait(false);

                foreach (var session in sessions)
                {
                    ct.ThrowIfCancellationRequested();

                    var sheets = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                        .ToListAsync(db.CuttingSheets.Where(s => s.CuttingSessionId == session.CuttingSessionId), ct)
                        .ConfigureAwait(false);

                    var allPieces = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                        .ToListAsync(db.CuttingPlacedPieces.Where(p => p.CuttingSessionId == session.CuttingSessionId), ct)
                        .ConfigureAwait(false);

                    decimal totalArea = sheets.Sum(s => s.UsedSqm);
                    decimal stockArea = sheets.Sum(s => s.StockSqm);

                    // Reservations / allocations
                    var reservations = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                        .ToListAsync(db.Reservations.Where(r => r.JobRef == jobRef), ct).ConfigureAwait(false);
                    var allocations = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                        .ToListAsync(db.Allocations.Where(a => a.JobRef == jobRef), ct).ConfigureAwait(false);

                    rows.Add(new MaterialConsumptionReportRow
                    {
                        JobRef = session.JobRef,
                        PiRef = session.SourceProformaId,
                        SpecName = session.MaterialGroupId,
                        MaterialGroup = session.MaterialGroupId,
                        Material = session.MaterialGroupId,
                        Color = sheets.FirstOrDefault()?.Color ?? "",
                        Thickness = sheets.FirstOrDefault()?.ThicknessMm.ToString("0.##") ?? "",
                        SheetSize = sheets.FirstOrDefault()?.StockSizeLabel ?? "",
                        SheetsUsed = sheets.Count,
                        TotalPieces = allPieces.Count,
                        TotalAreaSqm = totalArea,
                        StockAreaSqm = stockArea,
                        UtilizationPct = stockArea > 0 ? totalArea / stockArea * 100 : 0,
                        WastagePct = stockArea > 0 ? 100 - totalArea / stockArea * 100 : 0,
                        ReservedQty = reservations.Sum(r => r.Quantity),
                        AllocatedQty = allocations.Count(a => a.Status == (int)AllocationStatus.Allocated),
                        ConsumedQty = allocations.Count(a => a.Status == (int)AllocationStatus.Consumed),
                        Traceability = $"PI {session.SourceProformaId} → JO {session.JobRef} → MaterialGroup {session.MaterialGroupId} → Session {session.CuttingSessionId} → Sheets {string.Join(",", sheets.Select(s => s.SheetNo))}"
                    });
                }

                return rows;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                AppLogService.Error("[CuttingReport] GenerateMaterialConsumptionReport failed",
                    ex, new LogContext { Module = "Reporting", Class = nameof(CuttingReportService), Operation = nameof(GenerateMaterialConsumptionReportAsync) });
                return rows;
            }
        }
    }
}
