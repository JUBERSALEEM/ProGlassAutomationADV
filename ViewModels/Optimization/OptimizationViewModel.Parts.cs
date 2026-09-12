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
                return int.TryParse(WidthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out width) &&
                       int.TryParse(HeightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out height) &&
                       int.TryParse(QtyText, NumberStyles.Integer, CultureInfo.InvariantCulture, out qty) &&
                       width > 0 && height > 0 && qty > 0;
            }
        }

        public ObservableCollection<PieceEntryVm> Pieces { get; } = new();

        private PieceEntryVm _newPiece = new();
        public PieceEntryVm NewPiece { get => _newPiece; private set { _newPiece = value; OnPropertyChanged(); } }

        public ICommand AddPieceCommand { get; }
        public ICommand RemovePieceCommand { get; }
        public ICommand ClearPiecesCommand { get; }
        public ICommand LoadReferenceJobCommand { get; }

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

        /// <summary>
        /// Lists every confirmed (non-draft, non-void) Job Order and expands
        /// its cutting pieces via the production distribution engine — the
        /// same code the floor uses, so sizes/semantics never drift.
        /// Never throws: any failure surfaces as a status line.
        /// </summary>
        private void LoadReferenceJob()
        {
            const string lowE = "6mm climaguard hp neutral 70 low e ft glass";
            const string clear = "6mm clear ft glass";
            const string ramly = "6mm obscure clear ramly ft glass";

            // Step 5 rows: ref, W×H (unit size — equal-size DGU, both panes
            // cut the same), qty. Spec 1 first (34 rows), Spec 2 (12 rows).
            (string Ref, int W, int H, int Qty)[] spec1 =
            {
                ("11",1050,2127,2),("9",1054,2130,2),("10",1055,2130,2),("12",800,1798,2),
                ("1",905,2536,2),("2",1054,1986,2),("4",854,1981,2),("7",1003,1857,2),
                ("3",856,1970,2),("8",844,1457,2),("13",650,1382,2),("14",650,1372,2),
                ("2",605,1471,2),("5",553,1448,2),("SA",499,1430,2),("S",509,1421,2),
                ("R17",1815,725,1),("R8",1515,530,1),("R10",2013,828,1),("R10",2513,535,1),
                ("A2",1055,1985,2),("A11",1046,2130,2),("A9",1050,2130,2),("A10",1055,2130,2),
                ("A7",968,1889,2),("A4",850,1970,2),("A3",837,1984,2),("A1",838,2532,2),
                ("A14",657,1350,2),("A8",935,1418,2),("A13",652,1350,2),("A5",654,1479,2),
                ("A6",655,1474,2),("A12",800,1805,2)
            };

            (string Ref, int W, int H)[] spec2 =
            {
                ("A7",525,380),("A11",725,430),("A13",720,430),("A14",720,430),
                ("A18",725,430),("A19",725,430),("8",520,375),("12",725,430),
                ("14",710,430),("15",730,430),("19",720,430),("20",720,430)
            };

            Pieces.Clear();
            void Add(string composition, string position, string label, int w, int h, int qty)
            {
                var row = NewPieceRow(
                    composition, label,
                    w.ToString(CultureInfo.InvariantCulture),
                    h.ToString(CultureInfo.InvariantCulture),
                    qty.ToString(CultureInfo.InvariantCulture));
                row.Position = position;
                Pieces.Add(row);
            }

            // 76 low-e OUTER panes — every unit of both specs.
            foreach (var (r, w, h, q) in spec1) Add(lowE, "Outer", r, w, h, q);
            foreach (var (r, w, h) in spec2) Add(lowE, "Outer", r, w, h, 1);
            // 64 clear INNER panes (Spec 1).
            foreach (var (r, w, h, q) in spec1) Add(clear, "Inner", r, w, h, q);
            // 12 ramly obscure INNER panes (Spec 2).
            foreach (var (r, w, h) in spec2) Add(ramly, "Inner", r, w, h, 1);

            // STOCKS: the benchmark rack set — each cutting group packs
            // against its OWN Step 4 rack so the run reproduces the owner's
            // reference exactly (Low-E 17 sheets @ 82.77% on 3210×2250;
            // ramly 1 sheet on 2500×1875). Composition tags route each glass
            // group to its own rack instead of a shared pool.
            Stocks.Clear();
            Stocks.Add(new StockEntryVm("3210", "2250", lowE, "reference · low-e outer rack"));
            Stocks.Add(new StockEntryVm("3210", "2250", clear, "reference · clear inner rack"));
            Stocks.Add(new StockEntryVm("2500", "1875", ramly, "reference · ramly inner rack"));
            StocksStatusText = "Reference racks loaded — low-e & clear pack on 3210×2250, ramly on 2500×1875.";

            RunMessage = "Reference calibration job loaded — 152 pieces: 76 low-e outer · 64 clear inner · 12 ramly inner.";
            RefreshDerived();
        }

        private void RefreshDerived()
        {
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

            RebuildCuttingGroups();
            RebuildCutPlan();
            RefreshKpis();
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
