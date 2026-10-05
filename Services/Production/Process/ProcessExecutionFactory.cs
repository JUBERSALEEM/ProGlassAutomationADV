// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Process/ProcessExecutionFactory.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 16 Process-Specific Execution Helpers
//   Implements Holes/Cutouts, Polish, EdgeDeletion, Mitring, Tempering
//   as real production stages using shared execution framework.
//   Preserve available process information such as hole count, polish type,
//   edges, angle, operator, machine/workstation, start/completion, notes.
//   Do not invent machine-specific fields if application does not have them,
//   use extensible metadata only where necessary.
//   Do not build full CAD system — objective is production execution and traceability.
//   If existing spec models already contain hole/cutout data, reuse it, do not duplicate geometry.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Production.Process;
using System.Collections.Generic;
using System.Text.Json;

namespace ProGlassAutomation.Services.Production.Process
{
    public static class ProcessExecutionFactory
    {
        // ── Holes / Cutouts ──────────────────────────────────────────────
        public static string BuildHolesCutoutsDetailJson(
            int holeCount = 0,
            int cutoutCount = 0,
            List<HoleInfo>? holes = null,
            List<CutoutInfo>? cutouts = null,
            string toolInfo = "",
            string notes = "")
        {
            var detail = new HolesCutoutsDetail
            {
                HoleCount = holeCount,
                CutoutCount = cutoutCount,
                Holes = holes ?? new List<HoleInfo>(),
                Cutouts = cutouts ?? new List<CutoutInfo>(),
                ToolInfo = toolInfo,
                Notes = notes
            };
            return JsonSerializer.Serialize(detail);
        }

        // ── Polish ───────────────────────────────────────────────────────
        public static string BuildPolishDetailJson(
            string polishType = "",
            List<string>? edges = null,
            decimal quantityLm = 0,
            string operatorName = "",
            string machine = "",
            string workstation = "",
            string notes = "")
        {
            var detail = new PolishDetail
            {
                PolishType = polishType,
                Edges = edges ?? new List<string>(),
                EdgeCount = edges?.Count ?? 0,
                QuantityLm = quantityLm,
                OperatorName = operatorName,
                Machine = machine,
                Workstation = workstation,
                Notes = notes
            };
            return JsonSerializer.Serialize(detail);
        }

        // ── Edge Deletion ────────────────────────────────────────────────
        public static string BuildEdgeDeletionDetailJson(
            List<string>? requiredEdges = null,
            string operatorName = "",
            string machine = "",
            string workstation = "",
            string notes = "")
        {
            var detail = new EdgeDeletionDetail
            {
                RequiredEdges = requiredEdges ?? new List<string>(),
                EdgeCount = requiredEdges?.Count ?? 0,
                OperatorName = operatorName,
                Machine = machine,
                Workstation = workstation,
                Notes = notes
            };
            return JsonSerializer.Serialize(detail);
        }

        // ── Mitring ──────────────────────────────────────────────────────
        public static string BuildMitringDetailJson(
            List<MitringEdge>? edges = null,
            string operatorName = "",
            string machine = "",
            string workstation = "",
            string notes = "")
        {
            var detail = new MitringDetail
            {
                Edges = edges ?? new List<MitringEdge>(),
                OperatorName = operatorName,
                Machine = machine,
                Workstation = workstation,
                Notes = notes
            };
            return JsonSerializer.Serialize(detail);
        }

        // ── Tempering ────────────────────────────────────────────────────
        public static string BuildTemperingDetailJson(
            string temperingRequirement = "",
            string heatRequirement = "",
            string batchId = "",
            string operatorName = "",
            string machine = "",
            string furnace = "",
            string notes = "")
        {
            var detail = new TemperingDetail
            {
                TemperingRequirement = temperingRequirement,
                HeatRequirement = heatRequirement,
                BatchId = batchId,
                OperatorName = operatorName,
                Machine = machine,
                Furnace = furnace,
                Notes = notes
            };
            return JsonSerializer.Serialize(detail);
        }

        // ── Helpers to parse back ────────────────────────────────────────
        public static T? ParseDetail<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<T>(json); }
            catch { return null; }
        }

        public static string SummarizeDetail(string json, ProductionProcessType processType)
        {
            if (string.IsNullOrWhiteSpace(json)) return "—";
            try
            {
                return processType switch
                {
                    ProductionProcessType.HolesCutouts => SummarizeHoles(JsonSerializer.Deserialize<HolesCutoutsDetail>(json)),
                    ProductionProcessType.Polish => SummarizePolish(JsonSerializer.Deserialize<PolishDetail>(json)),
                    ProductionProcessType.EdgeDeletion => SummarizeEdgeDeletion(JsonSerializer.Deserialize<EdgeDeletionDetail>(json)),
                    ProductionProcessType.Mitring => SummarizeMitring(JsonSerializer.Deserialize<MitringDetail>(json)),
                    ProductionProcessType.Tempering => SummarizeTempering(JsonSerializer.Deserialize<TemperingDetail>(json)),
                    _ => json.Length > 200 ? json.Substring(0, 200) + "…" : json
                };
            }
            catch { return json.Length > 200 ? json.Substring(0, 200) + "…" : json; }
        }

        private static string SummarizeHoles(HolesCutoutsDetail? d)
        {
            if (d == null) return "—";
            return $"Holes {d.HoleCount} Cutouts {d.CutoutCount} Tool {d.ToolInfo} {d.Notes}".Trim();
        }

        private static string SummarizePolish(PolishDetail? d)
        {
            if (d == null) return "—";
            return $"{d.PolishType} Edges {string.Join(",", d.Edges)} {d.QuantityLm} LM {d.Notes}".Trim();
        }

        private static string SummarizeEdgeDeletion(EdgeDeletionDetail? d)
        {
            if (d == null) return "—";
            return $"Edges {string.Join(",", d.RequiredEdges)} {d.Notes}".Trim();
        }

        private static string SummarizeMitring(MitringDetail? d)
        {
            if (d == null) return "—";
            var edges = string.Join("; ", d.Edges.ConvertAll(e => $"{e.Edge} {(e.AngleDeg.HasValue ? e.AngleDeg.Value.ToString("0.##") + "°" : "angle n/a")}"));
            return $"Mitring {edges} {d.Notes}".Trim();
        }

        private static string SummarizeTempering(TemperingDetail? d)
        {
            if (d == null) return "—";
            return $"{d.TemperingRequirement} Batch {d.BatchId} Machine {d.Machine} {d.Notes}".Trim();
        }
    }
}
