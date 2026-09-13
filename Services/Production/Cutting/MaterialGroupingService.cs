// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Cutting/MaterialGroupingService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 15 Material Grouping
//   Groups pieces by physically compatible material before optimization.
//   Compatible grouping: Glass family, Color, Thickness, Coating,
//   Processing requirements, Sheet compatibility.
//   Do NOT mix Low-E with Clear, different thicknesses, different colors
//   unless domain explicitly defines compatibility.
//   Extensible grouping system.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Production.Cutting;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Production.Cutting
{
    public static class MaterialGroupingService
    {
        /// <summary>
        /// Group requirements by physically compatible material.
        /// Deterministic ordering, extensible.
        /// </summary>
        public static List<MaterialGroup> GroupByMaterial(IEnumerable<CuttingRequirement> requirements)
        {
            if (requirements == null) return new List<MaterialGroup>();

            var groups = new Dictionary<string, MaterialGroup>(StringComparer.OrdinalIgnoreCase);

            foreach (var req in requirements.OrderBy(r => r.PieceSequence))
            {
                string key = BuildGroupKey(req);

                if (!groups.TryGetValue(key, out var group))
                {
                    group = new MaterialGroup
                    {
                        GroupId = key,
                        GlassFamily = req.GlassFamily,
                        Color = req.Color,
                        ThicknessMm = req.ThicknessMm,
                        Coating = req.Coating,
                        Treatment = req.Treatment
                    };
                    groups[key] = group;
                }

                group.Requirements.Add(req);
            }

            // Deterministic ordering: by GroupId (Family|Color|Thickness|Coating|Treatment)
            var ordered = groups.Values
                .OrderBy(g => g.GlassFamily, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Color, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.ThicknessMm)
                .ThenBy(g => g.Coating, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Treatment, StringComparer.OrdinalIgnoreCase)
                .ToList();

            AppLogService.Info($"[MaterialGrouping] Grouped {requirements.Count()} pieces into {ordered.Count} material groups",
                new LogContext { Module = "Production", Class = nameof(MaterialGroupingService), Operation = nameof(GroupByMaterial) });

            return ordered;
        }

        /// <summary>
        /// Deterministic group key: Family|Color|Thickness|Coating|Treatment
        /// This ensures Low-E with Clear never mixed, different thicknesses never mixed, etc.
        /// </summary>
        public static string BuildGroupKey(CuttingRequirement req)
        {
            string family = (req.GlassFamily ?? "Clear").Trim();
            string color = (req.Color ?? "Clear").Trim();
            string coating = (req.Coating ?? "").Trim();
            string treatment = (req.Treatment ?? "").Trim();

            // Normalize for deterministic key
            return $"{family}|{color}|{req.ThicknessMm}|{coating}|{treatment}".ToUpperInvariant();
        }

        /// <summary>
        /// Check if two requirements are compatible for same sheet.
        /// Extensible — add more rules as domain evolves.
        /// </summary>
        public static bool AreCompatible(CuttingRequirement a, CuttingRequirement b)
        {
            if (a == null || b == null) return false;

            // Thickness must match exactly — different thicknesses cannot share sheet
            if (a.ThicknessMm != b.ThicknessMm) return false;

            // Color must match — Low-E with Clear not allowed
            if (!string.Equals(a.Color?.Trim(), b.Color?.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            // Family must match — HD Grey with Clear not allowed
            if (!string.Equals(a.GlassFamily?.Trim(), b.GlassFamily?.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            // Coating must match — Low-E coating affects processing
            if (!string.Equals(a.Coating?.Trim(), b.Coating?.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            // Treatment may be compatible even if different? For cutting, FT vs Annealed both cut same, so allow different treatment for cutting grouping
            // But for future extensibility, we keep treatment in key for now — can relax later if domain defines compatibility
            // For Phase15 cutting, we group by treatment as well to keep deterministic and safe

            return true;
        }

        /// <summary>
        /// Validate grouping — no incompatible pieces in same group.
        /// </summary>
        public static List<string> ValidateGroups(List<MaterialGroup> groups)
        {
            var errors = new List<string>();
            if (groups == null) return errors;

            foreach (var group in groups)
            {
                var first = group.Requirements.FirstOrDefault();
                if (first == null) continue;

                foreach (var req in group.Requirements.Skip(1))
                {
                    if (!AreCompatible(first, req))
                    {
                        errors.Add($"Group {group.GroupId} contains incompatible pieces: {first.PieceId} ({first.MaterialKey}) vs {req.PieceId} ({req.MaterialKey})");
                    }
                }
            }

            return errors;
        }
    }
}
