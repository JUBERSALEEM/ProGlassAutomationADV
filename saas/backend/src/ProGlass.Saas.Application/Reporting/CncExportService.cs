// ═══════════════════════════════════════════════════════════════════════
//  Phase 21 — CNC machine-file export over a PERSISTED optimization
//  session. The WPF exports the plan it holds in memory after a run;
//  the SaaS persists every session (Phase 11) and rebuilds the identical
//  plan from the normalized sheet/placement/cut/leftover rows, so an
//  export is reproducible long after the run.
//
//  Documented differences (mapping §1.9):
//   • Source stamp: the WPF stamps the in-memory run's provenance; the
//     SaaS stamps the session's document type + composition key.
//   • Kerf: the WPF reads the run's margin profile; the SaaS run params
//     do not persist a custom kerf, so it is stamped from the house
//     MarginProfile for the session's thickness (the profile the run
//     used unless customized — the params record minBreakoutMm only).
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Application.Optimization;
using ProGlass.Saas.Domain.Optimization;

namespace ProGlass.Saas.Application.Reporting;

public sealed class CncExportService(IOptimizationStore store)
{
    /// <summary>
    /// Lists the export targets with their readiness contract (the honest
    /// part: proprietary formats are declared, not guessed).
    /// </summary>
    public IReadOnlyList<CncExportFormatDto> ListFormats()
    {
        return CncExportFormats.All
            .Select(f => new CncExportFormatDto(
                f.Id, f.DisplayName, f.Extension, f.IsReady, f.Note))
            .ToList();
    }

    /// <summary>
    /// Builds the machine file for a persisted session. Unknown format
    /// ids and not-ready formats are refused — a typo can never export
    /// to the wrong machine, and a proprietary format is never guessed.
    /// </summary>
    public async Task<CncExportResult> ExportSessionAsync(
        Guid tenantId, Guid sessionId, string formatId, CancellationToken ct = default)
    {
        var format = CncExportFormats.ById(formatId);
        if (format is null)
        {
            return CncExportResult.Fail(
                $"Unknown CNC format '{formatId}'. Available: " +
                string.Join(", ", CncExportFormats.All.Select(f => f.Id)) + ".");
        }
        if (!format.IsReady || format.Writer is null)
        {
            return CncExportResult.Fail(
                $"{format.DisplayName} is not implemented. {format.Note}");
        }

        var session = await store.FindSessionAsync(tenantId, sessionId, ct).ConfigureAwait(false);
        if (session is null)
        {
            return CncExportResult.Fail($"Optimization session {sessionId} was not found.");
        }

        var plan = await BuildPlanAsync(tenantId, session, ct).ConfigureAwait(false);
        var text = format.Writer(plan);

        return CncExportResult.Ok(new CncExportFile(
            format.Id, format.DisplayName, format.Extension,
            session.Id, text));
    }

    private async Task<CncExportPlan> BuildPlanAsync(
        Guid tenantId, Domain.Entities.OptimizationSession session, CancellationToken ct)
    {
        var sheets = await store.ListSheetsForSessionAsync(tenantId, session.Id, ct)
            .ConfigureAwait(false);
        var sheetIds = sheets.Select(s => s.Id).ToList();
        var placements = await store.ListPlacementsAsync(tenantId, sheetIds, ct)
            .ConfigureAwait(false);
        var cuts = await store.ListCutsAsync(tenantId, sheetIds, ct)
            .ConfigureAwait(false);
        var leftovers = await store.ListLeftoversAsync(tenantId, sheetIds, ct)
            .ConfigureAwait(false);

        var result = new OptimizationResult();
        foreach (var sheet in sheets.OrderBy(s => s.SheetNo))
        {
            var layout = new SheetLayout
            {
                SheetNo = sheet.SheetNo,
                Stock = new StockSize(
                    sheet.StockWMm, sheet.StockHMm, sheet.ThicknessMm,
                    sheet.Color ?? "", "", sheet.MaterialKey ?? ""),
                UsableX = sheet.UsableX,
                UsableY = sheet.UsableY,
                UsableWidth = sheet.UsableW,
                UsableHeight = sheet.UsableH
            };

            foreach (var p in placements.Where(p => p.SheetId == sheet.Id))
            {
                layout.Pieces.Add(new PlacedPiece(
                    new CutPiece(
                        JobRef: p.JobRef ?? "",
                        GlassRef: p.GlassRef ?? "",
                        SpecLabel: p.SpecLabel ?? "",
                        Position: p.Position ?? "",
                        Color: p.Color ?? "",
                        ThicknessMm: p.ThicknessMm,
                        WidthMm: p.WMm,
                        HeightMm: p.HMm,
                        AllowRotate: true,
                        PieceLabel: p.PieceRef ?? ""),
                    p.XMm, p.YMm, p.WMm, p.HMm, p.Rotated));
            }

            foreach (var c in cuts.Where(c => c.SheetId == sheet.Id).OrderBy(c => c.Seq))
            {
                layout.Cuts.Add(new CutStep(
                    c.Seq,
                    string.Equals(c.Axis, "Vertical", StringComparison.OrdinalIgnoreCase)
                        ? CutAxis.Vertical
                        : CutAxis.Horizontal,
                    c.AtMm, c.FromX, c.FromY, c.FromW, c.FromH, c.Stage, c.IsTrim));
            }

            foreach (var l in leftovers.Where(l => l.SheetId == sheet.Id))
            {
                layout.Leftovers.Add(new RemnantRegion(l.SheetNo, l.XMm, l.YMm, l.WMm, l.HMm)
                {
                    IsUsable = l.IsReusable
                });
            }

            result.Sheets.Add(layout);
        }

        return new CncExportPlan
        {
            Result = result,
            Source = $"{session.DocumentType} {session.CompositionKey} (session {session.Id})",
            CutPattern = session.Strategy,
            KerfMm = sheets.Count == 0
                ? 0
                : MarginProfile.DefaultFor(sheets.Max(s => s.ThicknessMm)).KerfMm,
            GeneratedAt = DateTimeOffset.Now
        };
    }
}

public sealed record CncExportFormatDto(
    string Id, string DisplayName, string Extension, bool IsReady, string Note);

public sealed record CncExportFile(
    string FormatId, string FormatName, string Extension, Guid SessionId, string Text);

public sealed class CncExportResult
{
    public bool Success { get; private init; }
    public string Message { get; private init; } = "";
    public CncExportFile? Value { get; private init; }

    public static CncExportResult Ok(CncExportFile file) =>
        new() { Success = true, Value = file };

    public static CncExportResult Fail(string message) =>
        new() { Success = false, Message = message };
}
