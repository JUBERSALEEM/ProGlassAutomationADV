// ═══════════════════════════════════════════════════════════════
// FILE: OptimizationViewModel.Parts.cs
// MODULE: Optimization — PARTS — cutting list (piece entries) and the derived summaries/KPIs (partial of OptimizationViewModel)
// ═══════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;

namespace ProGlassAutomation.ViewModels.Optimization
{
    public partial class OptimizationViewModel : ViewModels.ViewModelBase
    {

        public sealed class PieceEntryVm : ViewModels.ViewModelBase
        {
            private string _glassRef = "6mm clear ft glass";
            private string _pieceLabel = string.Empty;
            private string _position = string.Empty;
            private string _widthText = string.Empty;
            private string _heightText = string.Empty;
            private string _qtyText = "1";

            /// <summary>Raised when an editable field changes (VM refreshes
            /// the summary, merge groups and cut plan).</summary>
            public Action? OnEdited { get; set; }

            public string GlassRef { get => _glassRef; set { _glassRef = value; OnPropertyChanged(); } }
            public string PieceLabel { get => _pieceLabel; set { _pieceLabel = value; OnPropertyChanged(); } }
            /// <summary>Pane position provenance — "Outer", "Inner", … ("" for ad-hoc pieces).</summary>
            public string Position { get => _position; set { _position = value; OnPropertyChanged(); } }
            public string WidthText { get => _widthText; set { _widthText = value; OnPropertyChanged(); OnEdited?.Invoke(); } }
            public string HeightText { get => _heightText; set { _heightText = value; OnPropertyChanged(); OnEdited?.Invoke(); } }
            public string QtyText { get => _qtyText; set { _qtyText = value; OnPropertyChanged(); OnEdited?.Invoke(); } }

            public bool TryParse(out int width, out int height, out int qty)
            {
                width = height = qty = 0;
                return ParsePositiveInt(WidthText, out width)
                    && ParsePositiveInt(HeightText, out height)
                    && ParsePositiveInt(QtyText, out qty);
            }

            /// <summary>
            /// Positive-integer parsing that also accepts integer-valued
            /// decimals: "12" and "12.0" parse, "12.5" and "0" do not.
            /// The app formats sizes with invariant decimals in places, and
            /// an operator who types a half-millimetre must get a clean
            /// validation message — not a row that dies later inside an
            /// unguarded Convert.ToInt32.
            /// </summary>
            private static bool ParsePositiveInt(string? text, out int value)
            {
                value = 0;
                if (string.IsNullOrWhiteSpace(text)) return false;
                text = text.Trim();

                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int direct))
                    return direct > 0;

                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                {
                    if (d > 0 && d == Math.Floor(d) && d <= int.MaxValue)
                    {
                        value = (int)d;
                        return true;
                    }
                }
                return false;
            }
        }

        public ObservableCollection<PieceEntryVm> Pieces { get; } = new();

        private PieceEntryVm _newPiece = new();
        public PieceEntryVm NewPiece { get => _newPiece; private set { _newPiece = value; OnPropertyChanged(); } }

        public ICommand AddPieceCommand { get; }
        public ICommand RemovePieceCommand { get; }
        public ICommand ClearPiecesCommand { get; }
        // REMOVED 2026-09-20: LoadReferenceJobCommand — per user request
        // "load reference job in optimization in module in jobs page is not
        // working remove it and left side is job selection so job list should
        // auto import". Auto-import now handles job → spec → cutlist.

        private string _pieceSummary = "No pieces yet";
        public string PieceSummary { get => _pieceSummary; private set { _pieceSummary = value; OnPropertyChanged(); } }

        // ── piece list ops ─────────────────────────────────────────────

        /// <summary>Creates a list piece with the edit callback wired so the
        /// summary / merge groups / cut plan refresh on inline qty edits.</summary>
        private PieceEntryVm NewPieceRow(string glass, string label, string w, string h, string qty)
        {
            var row = new PieceEntryVm
            {
                GlassRef = glass,
                PieceLabel = label,
                WidthText = w,
                HeightText = h,
                QtyText = qty
            };
            row.OnEdited = () => RefreshDerived();
            return row;
        }

        private void AddPiece()
        {
            if (!NewPiece.TryParse(out _, out _, out _))
            {
                RunMessage = "Piece needs a label, positive W×H (mm) and qty ≥ 1.";
                return;
            }

            Pieces.Add(NewPieceRow(
                NewPiece.GlassRef?.Trim() ?? string.Empty,
                NewPiece.PieceLabel?.Trim() ?? string.Empty,
                NewPiece.WidthText, NewPiece.HeightText, NewPiece.QtyText));
            NewPiece = new PieceEntryVm { GlassRef = NewPiece.GlassRef, PieceLabel = string.Empty };
            RunMessage = string.Empty;
            RefreshDerived();
        }

        private void RemovePiece(PieceEntryVm entry)
        {
            if (entry != null) Pieces.Remove(entry);
            RefreshDerived();
        }

        // REMOVED 2026-09-20: LoadReferenceJob() — hardcoded 152-piece benchmark
        // removed per owner request. Job selection now auto-imports real jobs
        // with merging. Method kept as no-op for binary compat if referenced
        // via reflection, but does nothing.
        private void LoadReferenceJob()
        {
            RunMessage = "Reference job loader removed — tick real jobs on the left to auto-import (merged by spec+glass+size).";
        }

        private void RefreshDerived()
        {
            // 2026-09-19 regression fix: validate/reset the cutting-group
            // selection FIRST (against the CURRENT piece set). The old order
            // built SizeGroups + CutPlan with a possibly-stale selection and
            // only then re-validated it — after a Pieces.Clear() by any
            /// caller (reference load, session restore, clear), a stale
            // selection filtered every group out and the SIZES tab + cut
            // plan rendered empty while KPIs still showed the full count.
            RebuildCuttingGroups();

            int count = 0;
            double area = 0;
            foreach (var p in Pieces)
            {
                if (!p.TryParse(out int w, out int h, out int q)) continue;
                count += q;
                area += w / 1000.0 * (h / 1000.0) * q;
            }

            PieceSummary = count == 0
                ? "No pieces yet"
                : string.Format(CultureInfo.InvariantCulture,
                    "{0} pieces · {1:0.00} m² glass", count, area);

            HasPieces = count > 0;

            // Merge groups carry ONLY their own pcs + area (low-e 76, clear
            // 64, ramly 12) — and reflect the cutting-group selector, so
            // picking "low-e outer" shows that group alone.
            SizeGroups.Clear();
            foreach (var grp in Pieces
                .Where(p => p.TryParse(out _, out _, out _))
                .GroupBy(p => (p.GlassRef ?? string.Empty).Trim())
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!SelectedGroupMatches(grp.Key)) continue;

                int gCount = 0;
                int gRows = 0;
                double gArea = 0;
                var gSizes = new HashSet<(int W, int H)>();
                foreach (var p in grp)
                {
                    p.TryParse(out int w, out int h, out int q);
                    gCount += q;
                    gArea += w / 1000.0 * (h / 1000.0) * q;
                    gSizes.Add((w, h));
                }
                gRows = gSizes.Count;
                SizeGroups.Add(new SizeGroupVm
                {
                    Composition = grp.Key,
                    Thickness = ParseThicknessLabel(grp.Key),
                    PieceCount = gCount.ToString(CultureInfo.InvariantCulture),
                    SizeRows = gRows.ToString(CultureInfo.InvariantCulture),
                    AreaM2 = gArea.ToString("0.00", CultureInfo.InvariantCulture)
                });
            }

            RebuildCutPlan();
            RefreshKpis();
            RebuildIndustrialReport();
        }

        /// <summary>
        /// Recomputes the four dusk-navy KPI cards: confirmed jobs, pieces,
        /// total glass area and cutting groups (the SIZES selector minus the
        /// "ALL" sentinel).
        /// </summary>
        private void RefreshKpis()
        {
            KpiJobs = AvailableJobs.Count.ToString(CultureInfo.InvariantCulture);

            int pcs = 0;
            double area = 0;
            foreach (var p in Pieces)
            {
                if (!p.TryParse(out int w, out int h, out int q)) continue;
                pcs += q;
                area += w / 1000.0 * (h / 1000.0) * q;
            }
            KpiPieces = pcs.ToString(CultureInfo.InvariantCulture);
            KpiGlass = area.ToString("0.0", CultureInfo.InvariantCulture) + " m²";

            int groups = System.Math.Max(0, CuttingGroups.Count - 1); // exclude "ALL — every glass group"
            KpiGroups = groups.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>True when the SIZES selector is ALL or equals the composition.</summary>
    }
}
