// ═══════════════════════════════════════════════════════════════
// FILE: OptimizationViewModel.Margins.cs
// MODULE: Optimization — SETTINGS — cutting margins, kerf, breakout, rotation and input build (partial of OptimizationViewModel)
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

        // ── MARGINS tab ────────────────────────────────────────────────

        private string _thicknessText = "6";
        public string ThicknessText { get => _thicknessText; set { _thicknessText = value; OnPropertyChanged(); } }

        private string _leftMarginText = "15";
        private string _rightMarginText = "15";
        private string _topMarginText = "15";
        private string _bottomMarginText = "15";
        private string _kerfText = "3";
        private string _minBreakoutText = "15";
        private bool _rotationEnabled = true; // floor default: rotation ON for all pieces

        public string LeftMarginText { get => _leftMarginText; set { _leftMarginText = value; OnPropertyChanged(); } }
        public string RightMarginText { get => _rightMarginText; set { _rightMarginText = value; OnPropertyChanged(); } }
        public string TopMarginText { get => _topMarginText; set { _topMarginText = value; OnPropertyChanged(); } }
        public string BottomMarginText { get => _bottomMarginText; set { _bottomMarginText = value; OnPropertyChanged(); } }
        public string KerfText { get => _kerfText; set { _kerfText = value; OnPropertyChanged(); } }
        public string MinBreakoutText { get => _minBreakoutText; set { _minBreakoutText = value; OnPropertyChanged(); } }
        public bool RotationEnabled { get => _rotationEnabled; set { _rotationEnabled = value; OnPropertyChanged(); } }

        public ICommand LoadHouseDefaultsCommand { get; }

        public ObservableCollection<string> EffortLevels { get; } = new() { "1", "2", "3", "4", "5" };
        private string _selectedEffortLevel = "3";
        public string SelectedEffortLevel
        {
            get => _selectedEffortLevel;
            set { _selectedEffortLevel = value; OnPropertyChanged(); }
        }

        // ── margins ────────────────────────────────────────────────────

        private void LoadHouseDefaults()
        {
            int thickness = int.TryParse(ThicknessText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int t) && t > 0 ? t : 6;
            var m = MarginProfile.DefaultFor(thickness);
            ThicknessText = thickness.ToString(CultureInfo.InvariantCulture);
            LeftMarginText = m.LeftMm.ToString(CultureInfo.InvariantCulture);
            RightMarginText = m.RightMm.ToString(CultureInfo.InvariantCulture);
            TopMarginText = m.TopMm.ToString(CultureInfo.InvariantCulture);
            BottomMarginText = m.BottomMm.ToString(CultureInfo.InvariantCulture);
            KerfText = m.KerfMm.ToString(CultureInfo.InvariantCulture);
            MinBreakoutText = m.MinBreakoutMm.ToString(CultureInfo.InvariantCulture);
        }

        private bool TryBuildInputs(
            out List<CutPiece> pieces, out List<(StockSize Stock, string Composition)> stocks, out MarginProfile margins)
        {
            pieces = new List<CutPiece>();
            stocks = new List<(StockSize Stock, string Composition)>();
            margins = null;

            foreach (var p in Pieces)
            {
                if (!p.TryParse(out int w, out int h, out int q))
                {
                    RunMessage = $"Piece \"{p.PieceLabel}\" is invalid — needs positive W×H and qty.";
                    return false;
                }
                for (int i = 0; i < q; i++)
                {
                    string baseLabel = string.IsNullOrWhiteSpace(p.PieceLabel)
                        ? (p.GlassRef ?? string.Empty).Trim()
                        : p.PieceLabel.Trim();
                    pieces.Add(new CutPiece
                    {
                        GlassRef = p.GlassRef ?? string.Empty,
                        // "G-101 3/24" — piece identity the 2D view / cut
                        // plan carry, matching the design doc's labelling.
                        PieceLabel = q > 1
                            ? $"{baseLabel} {i + 1}/{q}"
                            : baseLabel,
                        Position = p.Position ?? string.Empty,
                        ThicknessMm = ParseThicknessMm(p.GlassRef),
                        WidthMm = w,
                        HeightMm = h,
                        AllowRotate = RotationEnabled
                    });
                }
            }
            if (pieces.Count == 0)
            {
                RunMessage = "Add pieces first (JOBS tab) or load the reference job.";
                return false;
            }

            foreach (var s in Stocks)
            {
                // Only racks ticked "use in run" are fed to the optimizer —
                // the STOCKS tab selects which Step 4 sheets each spec uses.
                if (!s.IsUsedInRun) continue;

                if (!int.TryParse(s.WidthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) ||
                    !int.TryParse(s.HeightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) ||
                    w <= 0 || h <= 0)
                {
                    RunMessage = "Stock pool has an invalid size (STOCKS tab).";
                    return false;
                }
                int max = int.TryParse(s.AvailableText, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int a) ? System.Math.Max(0, a) : 0;
                stocks.Add((new StockSize { WidthMm = w, HeightMm = h, MaxSheets = max },
                    s.Composition ?? string.Empty));
            }
            if (stocks.Count == 0)
            {
                RunMessage = "Add at least one stock size and tick it for use (STOCKS tab).";
                return false;
            }

            int Parse(string text, string name, out int value)
            {
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0)
                    return value;
                value = -1;
                RunMessage = $"{name} must be a whole number ≥ 0 (MARGINS tab).";
                return value;
            }

            int left = Parse(LeftMarginText, "Left margin", out int lm);
            int right = Parse(RightMarginText, "Right margin", out int rm);
            int top = Parse(TopMarginText, "Top margin", out int tm);
            int bottom = Parse(BottomMarginText, "Bottom margin", out int bm);
            int kerf = Parse(KerfText, "Kerf", out int k);
            int breakout = Parse(MinBreakoutText, "Minimum breakout", out int mb);
            if (left < 0 || right < 0 || top < 0 || bottom < 0 || kerf < 0 || breakout < 0)
                return false;

            int thickness = int.TryParse(ThicknessText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int th) && th > 0 ? th : 6;

            margins = new MarginProfile
            {
                ThicknessMm = thickness,
                LeftMm = left,
                RightMm = right,
                TopMm = top,
                BottomMm = bottom,
                KerfMm = kerf,
                MinBreakoutMm = breakout
            };
            return true;
        }
    }
}
