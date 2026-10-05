// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ProductionProcessReportService.cs
// PROJECT: ProGlassAutomation
// MODULE: Reporting — Phase 16 Production Process Status, Work Queue,
//         QC/Rework Summary, Pairing/DGU Assembly Traceability, Production Route Report
//   Reuse existing Report Generator, do not create another report engine.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Production.Process;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Services.Production.Process;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Reporting
{
    public sealed class ProductionProcessStatusRow
    {
        public string JobRef { get; set; } = string.Empty;
        public string PiRef { get; set; } = string.Empty;
        public string SpecName { get; set; } = string.Empty;
        public string PieceId { get; set; } = string.Empty;
        public string ProcessType { get; set; } = string.Empty;
        public string PreviousStage { get; set; } = string.Empty;
        public string NextStage { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int QtyRequired { get; set; }
        public int QtyDone { get; set; }
        public int Remaining => Math.Max(0, QtyRequired - QtyDone);
        public string QcStatus { get; set; } = string.Empty;
        public string OperatorName { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Traceability { get; set; } = string.Empty;
    }

    public sealed class ProcessWorkQueueRow
    {
        public string JobRef { get; set; } = string.Empty;
        public string SpecName { get; set; } = string.Empty;
        public string PieceId { get; set; } = string.Empty;
        public string ProcessType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Qty { get; set; }
        public string WaitingOn { get; set; } = string.Empty;
    }

    public sealed class QcReworkSummaryRow
    {
        public string JobRef { get; set; } = string.Empty;
        public string SpecName { get; set; } = string.Empty;
        public string PieceId { get; set; } = string.Empty;
        public string ProcessType { get; set; } = string.Empty;
        public string QcResult { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Inspector { get; set; } = string.Empty;
        public DateTime InspectedAt { get; set; }
        public bool IsRework { get; set; }
    }

    public sealed class PairingAssemblyTraceabilityRow
    {
        public string JobRef { get; set; } = string.Empty;
        public string SpecName { get; set; } = string.Empty;
        public string PairId { get; set; } = string.Empty;
        public string OuterPieceId { get; set; } = string.Empty;
        public string InnerPieceId { get; set; } = string.Empty;
        public string AssemblyLabel { get; set; } = string.Empty;
        public string AssemblyType { get; set; } = string.Empty;
        public string Components { get; set; } = string.Empty;
        public string Traceability { get; set; } = string.Empty;
    }

    public sealed class ProductionRouteReportRow
    {
        public string JobRef { get; set; } = string.Empty;
        public string SpecName { get; set; } = string.Empty;
        public string BuildType { get; set; } = string.Empty;
        public string PieceId { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string CurrentStage { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public static class ProductionProcessReportService
    {
        public static async Task<List<ProductionProcessStatusRow>> GenerateProcessStatusAsync(string jobRef, CancellationToken ct = default)
        {
            var rows = new List<ProductionProcessStatusRow>();
            try
            {
                var execs = await ProductionProcessExecutionService.GetByJobAsync(jobRef, ct).ConfigureAwait(false);
                foreach (var e in execs)
                {
                    ct.ThrowIfCancellationRequested();
                    rows.Add(new ProductionProcessStatusRow
                    {
                        JobRef = e.JobRef,
                        PiRef = e.SourceProformaId,
                        SpecName = e.SpecName,
                        PieceId = e.PieceId,
                        ProcessType = ((ProductionProcessType)e.ProcessType).ToString(),
                        PreviousStage = e.PreviousStage,
                        NextStage = e.NextStage,
                        Status = ((Models.Production.WorkItemStatus)e.Status).ToString(),
                        QtyRequired = e.QtyRequired,
                        QtyDone = e.QtyDone,
                        QcStatus = e.QcStatus.ToString(),
                        OperatorName = $"{e.StartedBy}→{e.CompletedBy}",
                        StartedAt = e.StartedAt,
                        CompletedAt = e.CompletedAt,
                        Traceability = $"PI {e.SourceProformaId} → JO {e.JobRef} → Spec {e.SpecGroupNumber}/{e.SerialNumber} {e.SpecName} → Position {e.Position} → Piece {e.PieceId} → Input {e.InputPieceId} → Process {((ProductionProcessType)e.ProcessType)} {e.ExecutionId} → Output {e.OutputPieceId} → WorkItem {e.OutputWorkItemId} → QC {e.QcId}"
                    });
                }
                return rows.OrderBy(r => r.SpecName).ThenBy(r => r.ProcessType).ToList();
            }
            catch (Exception ex)
            {
                AppLogService.Error("[ProcessReport] GenerateProcessStatus failed", ex,
                    new LogContext { Module = "Reporting", Class = nameof(ProductionProcessReportService), Operation = nameof(GenerateProcessStatusAsync) });
                return rows;
            }
        }

        public static async Task<List<PairingAssemblyTraceabilityRow>> GeneratePairingAssemblyTraceabilityAsync(string jobRef, CancellationToken ct = default)
        {
            var rows = new List<PairingAssemblyTraceabilityRow>();
            try
            {
                var pairings = await PairingService.GetByJobAsync(jobRef, ct).ConfigureAwait(false);
                var assemblies = await DoublingLaminationService.GetByJobAsync(jobRef, ct).ConfigureAwait(false);

                foreach (var p in pairings)
                {
                    ct.ThrowIfCancellationRequested();
                    var assembly = assemblies.FirstOrDefault(a => a.PairingId == p.PairingId);
                    string comps = $"{p.OuterPieceId} + {p.InnerPieceId}";
                    string trace = $"PI {p.SourceProformaId} → JO {p.JobRef} → Spec {p.SpecGroupNumber}/{p.SerialNumber} → Pair {p.PairId} Outer {p.OuterPieceId} {p.OuterGlassRef} Inner {p.InnerPieceId} {p.InnerGlassRef}";

                    if (assembly != null)
                    {
                        trace += $" → Assembly {assembly.AssemblyLabel} {assembly.AssemblyType} {assembly.SpacerInfo} {assembly.InterlayerInfo}";
                        try
                        {
                            var compList = JsonSerializer.Deserialize<List<AssemblyComponent>>(assembly.ComponentsJson ?? "[]") ?? new List<AssemblyComponent>();
                            comps = string.Join(" + ", compList.Select(c => $"{c.Role} {c.PieceId} {c.GlassRef}"));
                        }
                        catch
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("if suppressed");
            }
                    }

                    rows.Add(new PairingAssemblyTraceabilityRow
                    {
                        JobRef = p.JobRef,
                        SpecName = $"{p.SpecGroupNumber}/{p.SerialNumber}",
                        PairId = p.PairId,
                        OuterPieceId = p.OuterPieceId,
                        InnerPieceId = p.InnerPieceId,
                        AssemblyLabel = assembly?.AssemblyLabel ?? "",
                        AssemblyType = assembly?.AssemblyType ?? "",
                        Components = comps,
                        Traceability = trace
                    });
                }

                // Also include assemblies without pairings (SGU lamination etc)
                foreach (var a in assemblies.Where(a => string.IsNullOrWhiteSpace(a.PairingId)))
                {
                    ct.ThrowIfCancellationRequested();
                    rows.Add(new PairingAssemblyTraceabilityRow
                    {
                        JobRef = a.JobRef,
                        SpecName = $"{a.SpecGroupNumber}/{a.SerialNumber}",
                        PairId = "",
                        OuterPieceId = "",
                        InnerPieceId = "",
                        AssemblyLabel = a.AssemblyLabel,
                        AssemblyType = a.AssemblyType,
                        Components = a.ComponentsJson,
                        Traceability = DoublingLaminationService.GetTraceability(a)
                    });
                }

                return rows;
            }
            catch (Exception ex)
            {
                AppLogService.Error("[ProcessReport] GeneratePairingAssemblyTraceability failed", ex,
                    new LogContext { Module = "Reporting", Class = nameof(ProductionProcessReportService), Operation = nameof(GeneratePairingAssemblyTraceabilityAsync) });
                return rows;
            }
        }

        public static async Task<List<ProductionRouteReportRow>> GenerateRouteReportAsync(string jobRef, CancellationToken ct = default)
        {
            var rows = new List<ProductionRouteReportRow>();
            try
            {
                ProductionRepository.EnsureInitialized();
                using var db = new ProductionDbContext();
                var workItems = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .ToListAsync(db.WorkItems.Where(w => w.JobRef == jobRef && !w.IsCancelled), ct).ConfigureAwait(false);

                var groups = workItems.GroupBy(w => new { w.SpecGroupNumber, w.SerialNumber, w.Position }).ToList();

                foreach (var g in groups)
                {
                    ct.ThrowIfCancellationRequested();
                    var first = g.First();
                    var route = ProductionProcessRoutingService.BuildRouteForWorkItem(first, g.ToList());
                    var execs = await ProductionProcessExecutionService.GetByJobAsync(jobRef, ct).ConfigureAwait(false);
                    var relevantExecs = execs.Where(e => e.SpecGroupNumber == first.SpecGroupNumber && e.SerialNumber == first.SerialNumber).ToList();
                    string current = relevantExecs.OrderByDescending(e => e.CreatedAt).FirstOrDefault()?.ProcessType.ToString() ?? "Cutting";
                    string status = relevantExecs.OrderByDescending(e => e.CreatedAt).FirstOrDefault()?.Status.ToString() ?? "Pending";

                    rows.Add(new ProductionRouteReportRow
                    {
                        JobRef = jobRef,
                        SpecName = first.SpecName,
                        BuildType = first.BuildType,
                        PieceId = route.PieceId,
                        Route = route.RouteLabel,
                        CurrentStage = current,
                        Status = status
                    });
                }

                return rows;
            }
            catch (Exception ex)
            {
                AppLogService.Error("[ProcessReport] GenerateRouteReport failed", ex,
                    new LogContext { Module = "Reporting", Class = nameof(ProductionProcessReportService), Operation = nameof(GenerateRouteReportAsync) });
                return rows;
            }
        }
    }
}
