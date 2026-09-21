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
            private bool _rotationLocked;

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

            /// <summary>
            /// PHASE 8 / finding 8e (2026-09-20): per-piece rotation lock.
            /// <c>CutPiece.AllowRotate</c> has been honoured by the packer all
            /// along — <c>Orientations</c>, the smart-placement candidate filter and
            /// the placement verifier all test it — but <c>TryBuildInputs</c> wrote
            /// <c>AllowRotate = RotationEnabled</c> for every row, so the only
            /// choice on offer was all-or-nothing. This surfaces the per-piece flag
            /// that already existed underneath it.
            /// Default false keeps today's behaviour exactly: rotation follows the
            /// SETTINGS choice. Locking a row removes its 90° candidate only, which
            /// is what an operator needs for a pane whose edge work, pattern,
            /// coating direction or tempering must not be turned.
            /// </summary>
            public bool RotationLocked { get => _rotationLocked; set { _rotationLocked = value; OnPropertyChanged(); OnEdited?.Invoke(); } }

            // Canonical identity — copied from PI/JO/Step 5, never invented.
            public string ProformaId { get; set; } = "";
            public string JobOrderId { get; set; } = "";
            public int SpecGroupNumber { get; set; }
            public int SourceRowSerial { get; set; }
            public string GlassType { get; set; } = "";
            public string Color { get; set; } = "";
            public int ThicknessMm { get; set; }
            public string Processing { get; set; } = "";
            public string Coating { get; set; } = "";
            public int W1 { get; set; }
            public int H1 { get; set; }
            public int W2 { get; set; }
            public int H2 { get; set; }
            public string SheetStoreSku { get; set; } = "";
            public int SheetWidthMm { get; set; }
            public int SheetHeightMm { get; set; }
            public decimal SheetPricePerSqm { get; set; }
            public string MaterialKey { get; set; } = "";

            public void EnsureMaterialKey()
            {
                if (!string.IsNullOrWhiteSpace(MaterialKey)) return;
                MaterialKey = OptimizationMaterialIdentity.BuildKey(
                    GlassType, Color, ThicknessMm, Processing, Coating);
            }

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
        /// <summary>2026-09-12 (31138eb) calibration DGU — 152 pieces.</summary>
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
            row.OnEdited = () =>
            {
                OptimizationResultFeed.Invalidate("optimization input changed");
                RefreshDerived();
            };
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
        /// Owner calibration DGU from 2026-09-11/12 (f2780ff / 31138eb):
        /// 76 low-e outer + 64 clear inner + 12 ramly inner = 152 pieces.
        /// These are the measured benchmark sizes, not placeholders.
        /// </summary>
        private void LoadReferenceJob()
        {
            const string lowE = "6mm climaguard hp neutral 70 low e ft glass";
            const string clear = "6mm clear ft glass";
            const string ramly = "6mm obscure clear ramly ft glass";

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
                if (string.Equals(position, "Inner", StringComparison.OrdinalIgnoreCase))
                {
                    row.W2 = w;
                    row.H2 = h;
                }
                else
                {
                    row.W1 = w;
                    row.H1 = h;
                }
                row.EnsureMaterialKey();
                Pieces.Add(row);
            }

            foreach (var (r, w, h, q) in spec1) Add(lowE, "Outer", r, w, h, q);
            foreach (var (r, w, h) in spec2) Add(lowE, "Outer", r, w, h, 1);
            foreach (var (r, w, h, q) in spec1) Add(clear, "Inner", r, w, h, q);
            foreach (var (r, w, h) in spec2) Add(ramly, "Inner", r, w, h, 1);

            // 31138eb seeded these four house racks so STOCK / Run were not empty.
            if (Stocks.Count == 0)
            {
                foreach (var (w, h) in new[] { (3210, 2250), (2500, 1875), (2440, 1830), (3660, 2440) })
                {
                    Stocks.Add(new StockEntryVm(
                        w.ToString(CultureInfo.InvariantCulture),
                        h.ToString(CultureInfo.InvariantCulture),
                        string.Empty,
                        "default rack")
                    { IsUsedInRun = true });
                }
                OnPropertyChanged(nameof(HasStocks));
                StocksStatusText = "Reference job house racks (3210×2250 / 2500×1875 / 2440×1830 / 3660×2440).";
            }

            RunMessage = "Reference calibration job loaded — 152 pieces: 76 low-e outer · 64 clear inner · 12 ramly inner.";
            RefreshDerived();
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
            //
            // RC-010: keyed through CompositionLabel so the group key, the
            // dropdown entry and the cut-plan key are the SAME string. The raw
            // `(p.GlassRef ?? "").Trim()` key produced "" for unlabelled glass,
            // which the dropdown deliberately excluded — so SelectedGroupMatches
            // filtered that whole group out of the cards whenever a specific
            // composition was selected, and glass quietly left the SIZES tab.
            SizeGroups.Clear();
            foreach (var grp in Pieces
                .Where(p => p.TryParse(out _, out _, out _))
                .GroupBy(p => CompositionLabel(p.GlassRef), StringComparer.OrdinalIgnoreCase)
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
