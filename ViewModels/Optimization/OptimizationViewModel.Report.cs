// ═══════════════════════════════════════════════════════════════
// FILE: OptimizationViewModel.Report.cs
// MODULE: Optimization — REPORTS tab — industrial cut report + session summaries (partial of OptimizationViewModel)
// ═══════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CsvHelper;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;

namespace ProGlassAutomation.ViewModels.Optimization
{
    public partial class OptimizationViewModel : ViewModels.ViewModelBase
    {

        // ── REPORTS tab ───────────────────────────────────────────────
        // The industrial cut report (the owner's production format) groups
        // the session by glass composition: one utilization line per spec
        // × sheet size, then a GLASS REF / W1 / H1 / W2 / H2 / QTY /
        // TOTAL SQM piece table per spec. The legacy layout / parts /
        // stocks summaries remain below as session detail.

        // ── Industrial report models ─────────────────────────────────

        /// <summary>One glass composition (spec) in the industrial report:
        /// its utilization line(s) and its per-ref piece table.</summary>
        public sealed class IndustrialReportSpecVm
        {
            public string Composition { get; set; } = string.Empty;
            public string ThicknessLabel { get; set; } = string.Empty;
            public string SpecName { get; set; } = string.Empty;
            public List<IndustrialUtilizationLineVm> Utilization { get; } = new();
            public List<IndustrialReportPieceVm> Pieces { get; } = new();
            public bool HasUtilization => Utilization.Count > 0;
        }

        /// <summary>One owner-format utilization line, e.g.
        /// "6MM THICKNESS CLIMAGUARD HP NEUTRAL 70 LOW E(SPEC) 3210 * 2250
        /// SHEET UTILIZED QTY 17 (SHEET SIZE) - 82.77% UTILIZATION OUTER".
        /// Tone drives the green/amber/red colour coding (≥85 / 70–84 / &lt;70).</summary>
        public sealed class IndustrialUtilizationLineVm
        {
            public string Line { get; set; } = string.Empty;
            public double UtilizationPct { get; set; }
            public string Tone { get; set; } = "Good";
        }

        /// <summary>One piece row in a spec's table: GLASS REF · W1 · H1 ·
        /// W2 · H2 · QTY · TOTAL SQM.</summary>
        public sealed class IndustrialReportPieceVm
        {
            public string GlassRef { get; set; } = string.Empty;
            public string W1 { get; set; } = string.Empty;
            public string H1 { get; set; } = string.Empty;
            public string W2 { get; set; } = string.Empty;
            public string H2 { get; set; } = string.Empty;
            public string Qty { get; set; } = string.Empty;
            public string TotalSqm { get; set; } = string.Empty;
        }

        public ObservableCollection<IndustrialReportSpecVm> IndustrialReport { get; } = new();

        private bool _hasIndustrialReport;
        public bool HasIndustrialReport { get => _hasIndustrialReport; private set { _hasIndustrialReport = value; OnPropertyChanged(); } }

        // ── export commands ──────────────────────────────────────────

        public ICommand ExportLayoutReportCommand { get; }
        public ICommand ExportPartsReportCommand { get; }
        public ICommand ExportStocksReportCommand { get; }
        public ICommand ExportIndustrialReportCommand { get; }

        private void ExportLayoutReport()
        {
            if (!HasResult || SheetRows.Count == 0)
            {
                RunMessage = "Run a cut first — the layout summary has no sheets yet.";
                return;
            }
            AsyncUtility.SafeFireAndForget(
                async () => await ReportingExportService.ExportCsvAsync(
                    SheetRows.ToList(),
                    "Optimization — Layout Summary",
                    new[] { "Sheet", "Glass", "Stock (mm)", "Pieces", "Utilization", "Offcut (m²)" },
                    r => new[]
                    {
                        r.SheetNo, r.Composition, r.StockLabel, r.PieceCount, r.Utilization, r.WasteLabel
                    },
                    new ExportRequest { SuggestedFileName = "Optimization_Layout_Summary" }),
                "Optimization layout report export");
        }

        private void ExportPartsReport()
        {
            if (!HasPieces || SizeGroups.Count == 0)
            {
                RunMessage = "The cutting list is empty — add pieces before exporting the parts summary.";
                return;
            }
            AsyncUtility.SafeFireAndForget(
                async () => await ReportingExportService.ExportCsvAsync(
                    SizeGroups.ToList(),
                    "Optimization — Parts Summary",
                    new[] { "Glass Composition", "Thickness", "Pieces", "Area (m²)" },
                    g => new[] { g.Composition, g.Thickness, g.PieceCount, g.AreaM2 },
                    new ExportRequest { SuggestedFileName = "Optimization_Parts_Summary" }),
                "Optimization parts report export");
        }

        private void ExportStocksReport()
        {
            if (Stocks.Count == 0)
            {
                RunMessage = "The stock pool is empty — select a job order to import its Step 4 sheets.";
                return;
            }
            AsyncUtility.SafeFireAndForget(
                async () => await ReportingExportService.ExportCsvAsync(
                    Stocks.ToList(),
                    "Optimization — Stocks Summary",
                    new[] { "Sheet (mm)", "Glass / Group", "Available", "Used this run", "Source", "In run" },
                    s => new[]
                    {
                        s.WidthText + "×" + s.HeightText,
                        s.CompositionLabel,
                        s.AvailableText,
                        s.UsedLabel,
                        s.SourceLabel,
                        s.IsUsedInRun ? "Yes" : "No"
                    },
                    new ExportRequest { SuggestedFileName = "Optimization_Stocks_Summary" }),
                "Optimization stocks report export");
        }

        private void ExportIndustrialReport()
        {
            if (!HasIndustrialReport)
            {
                RunMessage = "Nothing to export — add pieces (JOBS tab) and run a cut first.";
                return;
            }
            var layout = new IndustrialReportCsvLayout(IndustrialReport.ToList());
            AsyncUtility.SafeFireAndForget(
                async () => await new CsvExportEngine().ExportAsync(
                    layout,
                    new ExportRequest { SuggestedFileName = "Optimization_Industrial_Cut_Report" }),
                "Optimization industrial report export");
        }

        // ── industrial report build ──────────────────────────────────

        /// <summary>
        /// Rebuilds <see cref="IndustrialReport"/> from the current cutting
        /// list (piece tables) and the last run (utilization lines). One
        /// group per glass composition, ordered like the SIZES merge.
        /// Composition text is never shown empty — unlabelled glass reads
        /// "(unlabelled glass)".
        /// </summary>
        private void RebuildIndustrialReport()
        {
            IndustrialReport.Clear();

            foreach (var grp in Pieces
                .Where(p => p.TryParse(out _, out _, out _))
                .GroupBy(p => (p.GlassRef ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                string composition = string.IsNullOrWhiteSpace(grp.Key)
                    ? "(unlabelled glass)"
                    : grp.Key;

                var spec = new IndustrialReportSpecVm
                {
                    Composition = composition,
                    ThicknessLabel = FormatThicknessLabel(composition),
                    SpecName = FormatSpecName(composition)
                };

                // ── piece table (Step 5 style): one row per glass ref,
                //    outer/inner pane dimensions re-joined from the list ──
                foreach (var refGroup in grp
                    .GroupBy(p => (p.PieceLabel ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g.Key, StringComparer.Ordinal))
                {
                    var outer = refGroup.FirstOrDefault(p => IsFirstPane(p.Position));
                    var inner = refGroup.FirstOrDefault(p => IsSecondPane(p.Position));

                    int w1 = 0, h1 = 0, w2 = 0, h2 = 0, qty = 0;
                    // Outer/Glass pane → W1/H1; Inner/Lam Inner pane → W2/H2
                    // (the Step 5 column semantics: W1/H1 = first pane, W2/H2
                    // = second pane). A ref carrying both panes fills both.
                    if (outer != null && outer.TryParse(out int ow, out int oh, out int oq)) { w1 = ow; h1 = oh; qty = oq; }
                    if (inner != null && inner.TryParse(out int iw, out int ih, out int iq)) { w2 = iw; h2 = ih; if (qty == 0) qty = iq; }

                    double unitMm2 = w1 > 0 && h1 > 0 ? (double)w1 * h1 : (double)w2 * h2;
                    double totalSqm = unitMm2 / 1_000_000.0 * qty;

                    spec.Pieces.Add(new IndustrialReportPieceVm
                    {
                        GlassRef = string.IsNullOrWhiteSpace(refGroup.Key) ? "—" : refGroup.Key,
                        W1 = w1 > 0 ? w1.ToString(CultureInfo.InvariantCulture) : string.Empty,
                        H1 = h1 > 0 ? h1.ToString(CultureInfo.InvariantCulture) : string.Empty,
                        W2 = w2 > 0 ? w2.ToString(CultureInfo.InvariantCulture) : string.Empty,
                        H2 = h2 > 0 ? h2.ToString(CultureInfo.InvariantCulture) : string.Empty,
                        Qty = qty.ToString(CultureInfo.InvariantCulture),
                        TotalSqm = totalSqm.ToString("0.000", CultureInfo.InvariantCulture)
                    });
                }

                // ── utilization lines: one per sheet size the group cut on ──
                var run = _groupRuns.FirstOrDefault(r =>
                    string.Equals((r.Composition ?? string.Empty).Trim(), composition, StringComparison.OrdinalIgnoreCase));
                if (run != null)
                {
                    string position = PositionsLabel(grp);
                    foreach (var sheetGroup in run.Result.Sheets
                        .GroupBy(s => (s.Stock.WidthMm, s.Stock.HeightMm)))
                    {
                        double glass = sheetGroup.Sum(s => s.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm));
                        double stockArea = sheetGroup.Sum(s => (long)s.Stock.WidthMm * s.Stock.HeightMm);
                        double util = stockArea == 0 ? 0.0 : glass / stockArea * 100.0;

                        spec.Utilization.Add(new IndustrialUtilizationLineVm
                        {
                            Line = string.Format(CultureInfo.InvariantCulture,
                                "{0} THICKNESS {1}(SPEC) {2} * {3} SHEET UTILIZED QTY {4} (SHEET SIZE) - {5}% UTILIZATION {6}",
                                spec.ThicknessLabel,
                                spec.SpecName,
                                sheetGroup.Key.WidthMm,
                                sheetGroup.Key.HeightMm,
                                sheetGroup.Count(),
                                util.ToString("0.00", CultureInfo.InvariantCulture),
                                position).Trim(),
                            UtilizationPct = util,
                            Tone = UtilizationTone(util)
                        });
                    }
                }

                IndustrialReport.Add(spec);
            }

            HasIndustrialReport = IndustrialReport.Count > 0;
        }

        // ── formatting helpers ───────────────────────────────────────

        /// <summary>Leading thickness of a composition, e.g. "6mm clear …" → "6MM".</summary>
        private static string FormatThicknessLabel(string? composition)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                composition ?? string.Empty, @"^\s*(\d+(?:\.\d+)?)\s*mm");
            return m.Success ? m.Groups[1].Value.ToUpperInvariant() + "MM" : string.Empty;
        }

        /// <summary>Owner-style spec name from a composition: strips the
        /// leading thickness and trailing "ft glass"/"glass", uppercases —
        /// "6mm climaguard hp neutral 70 low e ft glass" → "CLIMAGUARD HP
        /// NEUTRAL 70 LOW E".</summary>
        private static string FormatSpecName(string? composition)
        {
            if (string.IsNullOrWhiteSpace(composition)) return string.Empty;
            string s = System.Text.RegularExpressions.Regex.Replace(
                composition, @"^\s*\d+(?:\.\d+)?\s*mm\s*", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(
                s, @"\s+(ft\s+)?glass\s*$", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = s.Trim().ToUpperInvariant();
            return s.Length == 0 ? "(UNLABELLED GLASS)" : s;
        }

        private static bool IsFirstPane(string? position) =>
            string.Equals(position, "Outer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(position, "Glass", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(position, "Lam Outer", StringComparison.OrdinalIgnoreCase);

        private static bool IsSecondPane(string? position) =>
            string.Equals(position, "Inner", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(position, "Lam Inner", StringComparison.OrdinalIgnoreCase);

        /// <summary>Pane positions present in a composition's pieces, e.g.
        /// "OUTER" / "INNER" / "OUTER/INNER" (empty when none are tagged).</summary>
        private static string PositionsLabel(IEnumerable<PieceEntryVm> pieces)
        {
            var positions = pieces
                .Select(p => p.Position ?? string.Empty)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return positions.Count == 0 ? string.Empty : string.Join("/", positions);
        }

        /// <summary>Colour-code band: green ≥85%, amber 70–84%, red &lt;70%.</summary>
        private static string UtilizationTone(double util) =>
            util >= 85.0 ? "Good" : util >= 70.0 ? "Mid" : "Low";

        // ── CSV layout for the owner-format industrial report ───────

        /// <summary>
        /// Writes the industrial report exactly like the owner prints it:
        /// per glass composition, the utilization line(s), then the
        /// GLASS REF / W1 / H1 / W2 / H2 / QTY / TOTAL SQM table, then a
        /// blank row before the next composition. Uses the shared CSV
        /// engine (UTF-8 BOM, formula-injection safe).
        /// </summary>
        private sealed class IndustrialReportCsvLayout : ICsvLayout
        {
            private readonly List<IndustrialReportSpecVm> _specs;

            public IndustrialReportCsvLayout(List<IndustrialReportSpecVm> specs) => _specs = specs;

            public string DocumentPrefix => "OPT";
            public string DocumentId => "OPTIMIZATION-INDUSTRIAL-CUT-REPORT";
            public string DocumentTitle => "Optimization — Industrial Cut Report";
            public string CompanyName => "ProGlass";
            public string Reference => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            public DateTime DocumentDate => DateTime.Now;

            public void WriteRows(CsvWriter csv)
            {
                foreach (var spec in _specs)
                {
                    // Utilization line(s) — one per sheet size.
                    foreach (var line in spec.Utilization)
                    {
                        csv.WriteField(line.Line);
                        csv.NextRecord();
                    }
                    if (spec.Utilization.Count == 0)
                    {
                        csv.WriteField(spec.Composition.ToUpperInvariant());
                        csv.NextRecord();
                    }

                    // Piece table header.
                    csv.WriteField("GLASS REF");
                    csv.WriteField("W1");
                    csv.WriteField("H1");
                    csv.WriteField("W2");
                    csv.WriteField("H2");
                    csv.WriteField("QTY");
                    csv.WriteField("TOTAL SQM");
                    csv.NextRecord();

                    foreach (var p in spec.Pieces)
                    {
                        csv.WriteField(p.GlassRef);
                        csv.WriteField(p.W1);
                        csv.WriteField(p.H1);
                        csv.WriteField(p.W2);
                        csv.WriteField(p.H2);
                        csv.WriteField(p.Qty);
                        csv.WriteField(p.TotalSqm);
                        csv.NextRecord();
                    }

                    csv.NextRecord(); // blank row between compositions
                }
            }
        }
    }
}
