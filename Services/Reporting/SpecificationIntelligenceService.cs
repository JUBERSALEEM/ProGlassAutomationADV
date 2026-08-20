// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/SpecificationIntelligenceService.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Canonical Specification Intelligence Engine
//
// PURPOSE:
//   ONE centralized specification intelligence service providing:
//     • Deterministic canonical specification fingerprinting (Type-Aware)
//     • Physical engineering decomposition (Layer thicknesses, treatments,
//       coatings, PVB interlayers, air spacers, colors, and dimensions)
//     • Real specification row extraction from document snapshots (PI, JO)
//     • High-speed indexed specification aggregation across large datasets
//     • Strict physical equality checking (6+12A+6 ≠ 6+16A+6, 1200×800 ≠ 1200×900)
//
// CONSUMED BY:
//   • Glass / Specification Report
//   • Row-Level Traceability Engine
//   • Proforma Invoice Pricing Estimator & DataGrid (Step 4 & 5)
//   • Job Order Production & Scheduling
//   • Future Manufacturing Modules (Cutting, Tempering, Pairing, Lamination)
//   • AI Intelligence Tools
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProGlassAutomation.Services.Reporting
{
    /// <summary>
    /// Decomposed physical engineering profile of a glass specification.
    /// </summary>
    public class GlassSpecificationProfile
    {
        public string GlassType { get; set; } = "SGU"; // SGU, DGU, LAM, DGU+LAM
        public string RawSpecName { get; set; } = "";

        // ── Outer Glass Layer ─────────────────────────────────────────────
        public int OuterThicknessMm { get; set; } = 6;
        public string OuterColor { get; set; } = "Clear";
        public string OuterTreatment { get; set; } = "Annealed"; // Annealed, Tempered, Heat Strengthened
        public string OuterCoating { get; set; } = "None"; // None, Low-E, Reflective, Solar Control, Sunergy

        // ── Middle / Interlayer Layer (Spacer or PVB) ─────────────────────
        public bool HasSpacer { get; set; }
        public int SpacerWidthMm { get; set; } // 6, 8, 9, 10, 12, 14, 15, 16, 18, 20mm
        public string SpacerType { get; set; } = "Aluminum"; // Aluminum, Warm Edge, Stainless Steel
        public string GasType { get; set; } = "Air"; // Air, Argon 90%

        public bool HasPvb { get; set; }
        public decimal PvbThicknessMm { get; set; } // 0.38, 0.76, 1.14, 1.52, 2.28mm
        public string PvbType { get; set; } = "Clear PVB"; // Clear PVB, Acoustic PVB, SGP (SentryGlas)

        // ── Inner Glass Layer ─────────────────────────────────────────────
        public bool HasInnerLayer { get; set; }
        public int InnerThicknessMm { get; set; }
        public string InnerColor { get; set; } = "";
        public string InnerTreatment { get; set; } = "";
        public string InnerCoating { get; set; } = "";

        // ── Canonical Fingerprint ─────────────────────────────────────────
        public string CanonicalFingerprint { get; set; } = "";

        public override string ToString() => CanonicalFingerprint;
    }

    /// <summary>
    /// Contract for global specification intelligence.
    /// </summary>
    public interface ISpecificationIntelligenceService
    {
        GlassSpecificationProfile DecomposeSpecification(string? specName, string? glassType, string? outerColor = "", string? innerColor = "");
        string BuildCanonicalFingerprint(string? glassRef, string? glassType, string? specName, string? outerColor, string? innerColor, int w1, int h1, int w2 = 0, int h2 = 0);
        bool AreSpecificationsEqual(SpecIdentity a, SpecIdentity b);
        List<GlassSpecReportRow> ExtractGlassRowsFromPi(ProformaInvoiceSnapshot snap, string proformaId, string company, string customer, string salesman, DateTime issueDate, string piStatus);
        List<GlassSpecReportRow> ExtractGlassRowsFromJo(JobOrderSnapshot snap, string jobOrderId, string sourcePiId, string customer, string project, string salesman, string status);
        List<SpecAggregation> AggregateSpecificationRows(IEnumerable<GlassSpecReportRow> rows);
    }

    /// <summary>
    /// Implementation of the Canonical Specification Intelligence Engine.
    /// </summary>
    public class SpecificationIntelligenceService : ISpecificationIntelligenceService
    {
        public static readonly SpecificationIntelligenceService Instance = new();

        private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();
        private static string NormRef(string? s) => (s ?? "").Trim().ToUpperInvariant();

        /// <summary>
        /// Decomposes any human-readable or system spec string into an exact physical engineering profile.
        /// </summary>
        public GlassSpecificationProfile DecomposeSpecification(string? specName, string? glassType, string? outerColor = "", string? innerColor = "")
        {
            var profile = new GlassSpecificationProfile
            {
                RawSpecName = specName ?? "",
                OuterColor = string.IsNullOrWhiteSpace(outerColor) ? "Clear" : outerColor.Trim(),
                InnerColor = string.IsNullOrWhiteSpace(innerColor) ? "" : innerColor.Trim()
            };

            var s = Norm(specName);
            var type = Norm(glassType);
            if (string.IsNullOrWhiteSpace(type)) type = SpecificationMatcher.DetectType(specName);
            profile.GlassType = type.ToUpperInvariant();

            // 1. Detect Spacer / Air Gap
            var spacerMatch = Regex.Match(s, @"(\d+)\s*(?:mm\s*)?(?:asp|a|spacer|air)");
            if (spacerMatch.Success && int.TryParse(spacerMatch.Groups[1].Value, out var sw))
            {
                profile.HasSpacer = true;
                profile.SpacerWidthMm = sw;
                profile.SpacerType = s.Contains("warm") ? "Warm Edge" : "Aluminum";
                profile.GasType = s.Contains("argon") ? "Argon" : "Air";
            }

            // 2. Detect PVB / SGP Interlayer
            var pvbMatch = Regex.Match(s, @"(\d+(?:\.\d+)?)\s*(?:mm\s*)?(?:pvb|sgp|sentry|interlayer)");
            if (pvbMatch.Success && decimal.TryParse(pvbMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pt))
            {
                profile.HasPvb = true;
                profile.PvbThicknessMm = pt;
                profile.PvbType = s.Contains("sgp") || s.Contains("sentry") ? "SGP" : s.Contains("acoustic") ? "Acoustic PVB" : "Clear PVB";
            }

            // 3. Detect Thicknesses
            var thicknesses = Regex.Matches(s, @"(\d+)\s*mm")
                .Cast<Match>()
                .Select(m => int.TryParse(m.Groups[1].Value, out var t) ? t : 0)
                .Where(t => t >= 3 && t <= 25)
                .ToList();

            if (thicknesses.Count > 0)
            {
                profile.OuterThicknessMm = thicknesses[0];
                if (thicknesses.Count > 1)
                {
                    profile.HasInnerLayer = true;
                    profile.InnerThicknessMm = thicknesses[thicknesses.Count - 1];
                }
            }

            // 4. Detect Treatments
            if (s.Contains("temp") || s.Contains("ft") || s.Contains("toughened")) profile.OuterTreatment = "Tempered";
            else if (s.Contains("hs") || s.Contains("heat strengthened")) profile.OuterTreatment = "Heat Strengthened";
            else profile.OuterTreatment = "Annealed";

            // 5. Detect Coatings
            if (s.Contains("low-e") || s.Contains("lowe")) profile.OuterCoating = "Low-E";
            else if (s.Contains("sunergy")) profile.OuterCoating = "Sunergy";
            else if (s.Contains("solarban")) profile.OuterCoating = "Solarban";
            else if (s.Contains("reflect") || s.Contains("stopsol")) profile.OuterCoating = "Reflective";

            profile.CanonicalFingerprint = BuildCanonicalFingerprint("", profile.GlassType, profile.RawSpecName, profile.OuterColor, profile.InnerColor, 0, 0);

            return profile;
        }

        /// <summary>
        /// Builds an exact, type-aware, deterministic canonical fingerprint key.
        /// </summary>
        public string BuildCanonicalFingerprint(
            string? glassRef,
            string? glassType,
            string? specName,
            string? outerColor,
            string? innerColor,
            int w1,
            int h1,
            int w2 = 0,
            int h2 = 0)
        {
            return SpecificationMatcher.BuildCanonicalKey(glassRef, glassType, specName, outerColor, innerColor, w1, h1, w2, h2);
        }

        /// <summary>
        /// True if two specifications describe the exact same physical glass configuration.
        /// </summary>
        public bool AreSpecificationsEqual(SpecIdentity a, SpecIdentity b)
        {
            return SpecificationMatcher.IsExactMatch(a, b);
        }

        /// <summary>
        /// Extracts itemized physical glass rows from a Proforma Invoice snapshot.
        /// </summary>
        public List<GlassSpecReportRow> ExtractGlassRowsFromPi(
            ProformaInvoiceSnapshot snap,
            string proformaId,
            string company,
            string customer,
            string salesman,
            DateTime issueDate,
            string piStatus)
        {
            var rows = new List<GlassSpecReportRow>();
            if (snap?.SpecGroups == null) return rows;

            foreach (var grp in snap.SpecGroups)
            {
                if (grp?.ItemRows == null) continue;
                foreach (var r in grp.ItemRows)
                {
                    if (r == null) continue;
                    if (string.IsNullOrWhiteSpace(r.GlassRef) && r.W1 == 0 && r.H1 == 0) continue;

                    var sqm1 = r.Sqm1;
                    var totalSqm = r.TotalSqm;
                    var rate = r.Rate;
                    var amount = Math.Round(sqm1 * r.Qty * rate, 2);
                    var canonical = BuildCanonicalFingerprint(
                        r.GlassRef, grp.SelectedGlassType, grp.SelectedSpecName,
                        grp.OuterGlassColor, grp.InnerGlassColor, r.W1, r.H1, r.W2, r.H2);

                    rows.Add(new GlassSpecReportRow
                    {
                        RowId = $"{proformaId}-G{grp.GroupNumber}-S{r.SerialNumber}",
                        ProformaId = proformaId,
                        GlassRef = r.GlassRef ?? "",
                        SpecName = grp.SelectedSpecName ?? grp.GroupName ?? "",
                        GlassType = grp.SelectedGlassType ?? "",
                        OuterColor = grp.OuterGlassColor ?? "",
                        InnerColor = grp.InnerGlassColor ?? "",
                        W1 = r.W1,
                        H1 = r.H1,
                        W2 = r.W2,
                        H2 = r.H2,
                        Qty = r.Qty,
                        Sqm1 = sqm1,
                        TotalSqm = totalSqm,
                        Rate = rate,
                        Amount = amount,
                        BaseRate = grp.BaseRate,
                        GroupNumber = grp.GroupNumber,
                        GroupName = grp.GroupName ?? "",
                        SerialNumber = r.SerialNumber,
                        OuterSheetWidthMm = grp.OuterSheetWidthMm,
                        OuterSheetHeightMm = grp.OuterSheetHeightMm,
                        InnerSheetWidthMm = grp.InnerSheetWidthMm,
                        InnerSheetHeightMm = grp.InnerSheetHeightMm,
                        CompanyName = company,
                        CustomerName = customer,
                        Salesman = salesman,
                        ProjectName = snap.Project?.ProjectIdentifierName ?? snap.Project?.ProjectReference ?? "",
                        IssueDate = issueDate,
                        PiStatus = piStatus,
                        CanonicalKey = canonical,
                        DimensionsKey = $"{r.W1}×{r.H1}" + (r.W2 > 0 || r.H2 > 0 ? $" / {r.W2}×{r.H2}" : "")
                    });
                }
            }
            return rows;
        }

        /// <summary>
        /// Extracts itemized physical glass rows from a Job Order snapshot.
        /// </summary>
        public List<GlassSpecReportRow> ExtractGlassRowsFromJo(
            JobOrderSnapshot snap,
            string jobOrderId,
            string sourcePiId,
            string customer,
            string project,
            string salesman,
            string status)
        {
            if (snap?.EmbeddedInvoice == null) return new List<GlassSpecReportRow>();
            var rows = ExtractGlassRowsFromPi(snap.EmbeddedInvoice, sourcePiId, "", customer, salesman, DateTime.Now, status);
            foreach (var r in rows)
            {
                r.JobOrderId = jobOrderId;
                r.ProjectName = project;
            }
            return rows;
        }

        /// <summary>
        /// Groups raw glass rows into canonical build aggregations with quantity, SQM, and value totals.
        /// </summary>
        public List<SpecAggregation> AggregateSpecificationRows(IEnumerable<GlassSpecReportRow> rows)
        {
            if (rows == null) return new List<SpecAggregation>();

            return rows.GroupBy(r => r.CanonicalKey)
                .Select(g =>
                {
                    var first = g.First();
                    return new SpecAggregation
                    {
                        CanonicalKey = g.Key,
                        SpecName = first.SpecName,
                        GlassType = first.GlassType,
                        GlassRef = first.GlassRef,
                        DimensionsKey = first.DimensionsKey,
                        TotalRows = g.Count(),
                        TotalQty = g.Sum(x => x.Qty),
                        TotalSqm = Math.Round(g.Sum(x => x.TotalSqm), 3),
                        TotalValue = Math.Round(g.Sum(x => x.Amount), 2),
                        AvgRate = g.Average(x => x.Rate),
                        DeliveredQty = g.Sum(x => x.DeliveredQty),
                        BalanceQty = g.Sum(x => x.BalanceQty),
                        DistinctCustomers = g.Select(x => x.CustomerName).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                        DistinctProjects = g.Select(x => x.ProjectName).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                        TopCustomers = g.GroupBy(x => x.CustomerName).OrderByDescending(x => x.Sum(v => v.Qty)).Take(3).Select(x => x.Key).ToList()
                    };
                })
                .OrderByDescending(g => g.TotalQty)
                .ToList();
        }
    }
}
