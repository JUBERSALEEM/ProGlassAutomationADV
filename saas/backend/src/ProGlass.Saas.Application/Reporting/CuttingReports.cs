// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 22:
//   Services/Reporting/CuttingReportService.cs (Phase 15 cutting sheet
//   report + material consumption report)
// The WPF reads its CuttingSessions/CuttingSheets/CuttingPlacedPieces
// tables; the SaaS stores the same plan in the optimization session
// tables (Phase 11), so the reports render from those.
//
// Documented differences (mapping §1.9):
//  • The SaaS optimization placement does not persist the WPF's
//    SpecGroupNumber/SourceRowSerial on placements (the piece identity
//    is the PieceRef label + dims) → SpecGroupNumber renders "".
//  • Sheet verification: the WPF has per-sheet IsVerified/
//    VerificationResult; the SaaS verifies the SESSION (conservation +
//    geometry) — the row carries the session verdicts.
//  • Allocations: the WPF counts an allocation ledger (Allocated/
//    Consumed); the allocation ledger is a DEFERRED production extra in
//    the SaaS → those counts render 0 (never invented).
//  • Operator: the WPF stores the operator NAME on the sheet; the SaaS
//    stores the user id → resolved to the user's DisplayName.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Application.Optimization;

namespace ProGlass.Saas.Application.Reporting;

/// <summary>One cut sheet of one job (WPF CuttingSheetReportRow).</summary>
public sealed record CuttingSheetReportRowDto(
    string JobRef,
    string PiRef,
    string SpecName,
    string SpecGroupNumber,
    string Material,
    string Color,
    string Thickness,
    string SheetLabel,
    string SheetDims,
    int SheetNo,
    int PiecesCount,
    string PiecesList,
    decimal UtilizationPct,
    decimal WastagePct,
    string Rotation,
    bool IsVerified,
    string VerificationResult,
    string OperatorName,
    DateTime CreatedAt);

/// <summary>Per-session material summary (WPF MaterialConsumptionReportRow).</summary>
public sealed record MaterialConsumptionReportRowDto(
    string JobRef,
    string PiRef,
    string SpecName,
    string MaterialGroup,
    string Material,
    string Color,
    string Thickness,
    string SheetSize,
    int SheetsUsed,
    int TotalPieces,
    decimal TotalAreaSqm,
    decimal StockAreaSqm,
    decimal UtilizationPct,
    decimal WastagePct,
    int ReservedQty,
    int AllocatedQty,
    int ConsumedQty,
    string Traceability);

public sealed class CuttingReportService(
    IReportingStore reporting,
    IOptimizationStore optimization)
{
    /// <summary>
    /// Cutting sheet report for a job: one row per cut sheet — job, PI,
    /// spec, material, sheet dims, pieces, utilization/wastage, rotation,
    /// verification, operator.
    /// </summary>
    public async Task<IReadOnlyList<CuttingSheetReportRowDto>> GenerateCuttingSheetReportAsync(
        Guid tenantId, string jobOrderNo, CancellationToken ct = default)
    {
        var rows = new List<CuttingSheetReportRowDto>();
        var job = await reporting.FindTraceJobAsync(tenantId, jobOrderNo, ct)
            .ConfigureAwait(false);
        if (job is null) return rows;

        var piNo = await reporting.GetProformaNoAsync(tenantId, job.ProformaId, ct)
            .ConfigureAwait(false) ?? "";

        var sessions = await optimization.ListSessionsForJobAsync(tenantId, job.Id, ct)
            .ConfigureAwait(false);
        foreach (var session in sessions)
        {
            var sheets = await optimization.ListSheetsForSessionAsync(tenantId, session.Id, ct)
                .ConfigureAwait(false);
            var sheetIds = sheets.Select(s => s.Id).ToList();
            var placements = await optimization.ListPlacementsAsync(tenantId, sheetIds, ct)
                .ConfigureAwait(false);

            string operatorName = await reporting.GetUserDisplayNameAsync(
                tenantId, session.CreatedBy, ct).ConfigureAwait(false) ?? "";

            foreach (var sheet in sheets.OrderBy(s => s.SheetNo))
            {
                var pieces = placements
                    .Where(p => p.SheetId == sheet.Id)
                    .OrderBy(p => p.PieceRef, StringComparer.Ordinal)
                    .ToList();

                string piecesList = string.Join(", ", pieces.Select(p =>
                    $"{p.PieceRef} {p.WMm}×{p.HMm} {(p.Rotated ? "90°" : "0°")}"));

                bool verified = session.ConservationVerified && session.GeometryVerified;

                rows.Add(new CuttingSheetReportRowDto(
                    JobRef: job.JobNo,
                    PiRef: piNo,
                    SpecName: pieces.FirstOrDefault()?.SpecLabel ?? session.CompositionKey,
                    SpecGroupNumber: "",   // not persisted on placements (documented)
                    Material: sheet.Color ?? "",
                    Color: sheet.Color ?? "",
                    Thickness: sheet.ThicknessMm.ToString("0.##"),
                    SheetLabel: $"{sheet.StockWMm} × {sheet.StockHMm}",
                    SheetDims: $"{sheet.StockWMm} × {sheet.StockHMm}",
                    SheetNo: sheet.SheetNo,
                    PiecesCount: pieces.Count,
                    PiecesList: piecesList,
                    UtilizationPct: sheet.UtilizationPct,
                    WastagePct: 100m - sheet.UtilizationPct,
                    Rotation: pieces.Any(p => p.Rotated) ? "Mixed 0°/90°" : "0°",
                    IsVerified: verified,
                    VerificationResult: verified ? "Verified" : "Not verified",
                    OperatorName: operatorName,
                    CreatedAt: sheet.CreatedAt));
            }
        }

        return rows.OrderBy(r => r.SheetNo).ToList();
    }

    /// <summary>
    /// Material consumption report for a job: one row per optimization
    /// session — sheets used, pieces, areas, utilization/wastage,
    /// reservations, and the full traceability chain.
    /// </summary>
    public async Task<IReadOnlyList<MaterialConsumptionReportRowDto>> GenerateMaterialConsumptionReportAsync(
        Guid tenantId, string jobOrderNo, CancellationToken ct = default)
    {
        var rows = new List<MaterialConsumptionReportRowDto>();
        var job = await reporting.FindTraceJobAsync(tenantId, jobOrderNo, ct)
            .ConfigureAwait(false);
        if (job is null) return rows;

        var piNo = await reporting.GetProformaNoAsync(tenantId, job.ProformaId, ct)
            .ConfigureAwait(false) ?? "";

        var sessions = await optimization.ListSessionsForJobAsync(tenantId, job.Id, ct)
            .ConfigureAwait(false);
        var reservations = await reporting.ListReservationsForJobAsync(tenantId, job.Id, ct)
            .ConfigureAwait(false);

        foreach (var session in sessions)
        {
            var sheets = await optimization.ListSheetsForSessionAsync(tenantId, session.Id, ct)
                .ConfigureAwait(false);
            var sheetIds = sheets.Select(s => s.Id).ToList();
            var placements = await optimization.ListPlacementsAsync(tenantId, sheetIds, ct)
                .ConfigureAwait(false);

            decimal totalArea = sheets.Sum(s => s.UsedSqm);
            decimal stockArea = sheets.Sum(s => s.StockSqm);
            decimal util = stockArea > 0 ? totalArea / stockArea * 100 : 0;

            rows.Add(new MaterialConsumptionReportRowDto(
                JobRef: job.JobNo,
                PiRef: piNo,
                SpecName: session.CompositionKey,
                MaterialGroup: session.CompositionKey,
                Material: session.CompositionKey,
                Color: sheets.FirstOrDefault()?.Color ?? "",
                Thickness: sheets.FirstOrDefault()?.ThicknessMm.ToString("0.##") ?? "",
                SheetSize: sheets.FirstOrDefault() is { } s0 ? $"{s0.StockWMm} × {s0.StockHMm}" : "",
                SheetsUsed: sheets.Count,
                TotalPieces: placements.Count,
                TotalAreaSqm: totalArea,
                StockAreaSqm: stockArea,
                UtilizationPct: util,
                WastagePct: stockArea > 0 ? 100m - util : 0m,
                ReservedQty: reservations.Sum(r => r.ReservedQty),
                AllocatedQty: 0,   // allocation ledger: deferred production extra
                ConsumedQty: 0,    // (documented — never invented)
                Traceability:
                    $"PI {piNo} → JO {job.JobNo} → MaterialGroup {session.CompositionKey} " +
                    $"→ Session {session.Id} → Sheets {string.Join(",", sheets.OrderBy(x => x.SheetNo).Select(x => x.SheetNo))}"));
        }

        return rows;
    }
}
