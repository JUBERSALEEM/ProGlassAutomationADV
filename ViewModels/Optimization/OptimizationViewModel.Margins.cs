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

        /// <summary>
        /// Glass thickness driving the house margin profile.
        ///
        /// PHASE 1 / finding 1d (2026-09-20, owner requirement: "auto select as
        /// per thickness trigger"): changing thickness now AUTO-SELECTS that
        /// bucket's house margins — but only while the operator has not touched
        /// them. A customized SETTINGS tab is never overwritten underneath the
        /// operator; that is the same destructive-refresh class of bug as
        /// RC-016, where navigating into a station silently blanked work the
        /// operator had already done. When the fields are customized the hint
        /// names the house numbers and offers the one-click instead.
        /// </summary>
        public string ThicknessText
        {
            get => _thicknessText;
            set
            {
                if (string.Equals(_thicknessText, value, StringComparison.Ordinal)) return;

                int previousThickness = CurrentThicknessMm;
                _thicknessText = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentThicknessMm));
                OnPropertyChanged(nameof(HasBreakoutChoice));
                OnPropertyChanged(nameof(BreakoutHint));

                AutoSelectHouseDefaultsOnThicknessChange(previousThickness);

                OnPropertyChanged(nameof(MarginHint));
            }
        }

        /// <summary>Thickness as the engine will read it: unparseable or
        /// non-positive falls back to 6mm, the long-standing default.</summary>
        public int CurrentThicknessMm =>
            TryParseWholeMm(ThicknessText, out int t) && t > 0 ? t : 6;

        /// <summary>
        /// True when the current thickness offers the 50 / 40 breakout choice —
        /// i.e. the thick (9mm and above) house bucket.
        /// </summary>
        public bool HasBreakoutChoice => MarginProfile.HasBreakoutChoice(CurrentThicknessMm);

        /// <summary>The two sanctioned breakout values on thick glass, as text.</summary>
        public ObservableCollection<string> BreakoutOptions { get; } = new()
        {
            MarginProfile.BreakoutPrimaryMm.ToString(CultureInfo.InvariantCulture),
            MarginProfile.BreakoutAlternateMm.ToString(CultureInfo.InvariantCulture)
        };

        private string? _selectedBreakoutOption;

        /// <summary>
        /// The 50 / 40 breakout switch. Writes through to
        /// <see cref="MinBreakoutText"/>, which stays the single source of
        /// truth so session persistence and the engine input path are unchanged.
        /// </summary>
        public string? SelectedBreakoutOption
        {
            get => _selectedBreakoutOption;
            set
            {
                if (string.Equals(_selectedBreakoutOption, value, StringComparison.Ordinal)) return;
                _selectedBreakoutOption = value;
                OnPropertyChanged();

                if (TryParseWholeMm(value, out int mm) && mm > 0)
                    MinBreakoutText = mm.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Explains the breakout choice where it applies.</summary>
        public string BreakoutHint => HasBreakoutChoice
            ? $"{MarginProfile.BreakoutPrimaryMm}mm is the house default on thick glass. " +
              $"Switch to {MarginProfile.BreakoutAlternateMm}mm when the narrower offcut is still worth keeping."
            : "The 50 / 40 choice applies to 9mm and above only — on this thickness the breakout equals the margin.";

        /// <summary>
        /// Names the house profile for the current thickness, and — when the
        /// operator has customized the fields — says so and offers the
        /// one-click. Never overwrites customized values on its own.
        /// </summary>
        public string MarginHint
        {
            get
            {
                int thickness = CurrentThicknessMm;
                var house = MarginProfile.DefaultFor(thickness);
                string houseText = string.Format(CultureInfo.InvariantCulture,
                    "House defaults for {0}mm ({1}): LM {2} · RM {3} · TM {4} · BM {5} · kerf {6} · breakout {7}{8}.",
                    thickness, DescribeBucket(thickness),
                    house.LeftMm, house.RightMm, house.TopMm, house.BottomMm, house.KerfMm,
                    house.MinBreakoutMm,
                    HasBreakoutChoice ? $" (or {MarginProfile.BreakoutAlternateMm})" : string.Empty);

                return FieldsAreHouseDefaults(thickness)
                    ? houseText
                    : houseText + " Your values differ — they will be used exactly as entered. " +
                      "Press LOAD HOUSE DEFAULTS to take the table above.";
            }
        }

        private static string DescribeBucket(int thickness) =>
            MarginProfile.BucketFor(thickness) switch
            {
                ThicknessBucket.Thick => "LM/RM/BM 50, TM 0 — top edge left untrimmed",
                ThicknessBucket.Medium => "30mm on all four edges",
                _ => "15mm on all four edges"
            };

        private string _leftMarginText = "15";
        private string _rightMarginText = "15";
        private string _topMarginText = "15";
        private string _bottomMarginText = "15";
        private string _kerfText = "3";
        private string _minBreakoutText = "15";
        private bool _rotationEnabled = true; // floor default: rotation ON for all pieces

        public string LeftMarginText { get => _leftMarginText; set { _leftMarginText = value; OnPropertyChanged(); OnPropertyChanged(nameof(MarginHint)); } }
        public string RightMarginText { get => _rightMarginText; set { _rightMarginText = value; OnPropertyChanged(); OnPropertyChanged(nameof(MarginHint)); } }
        public string TopMarginText { get => _topMarginText; set { _topMarginText = value; OnPropertyChanged(); OnPropertyChanged(nameof(MarginHint)); } }
        public string BottomMarginText { get => _bottomMarginText; set { _bottomMarginText = value; OnPropertyChanged(); OnPropertyChanged(nameof(MarginHint)); } }
        public string KerfText { get => _kerfText; set { _kerfText = value; OnPropertyChanged(); OnPropertyChanged(nameof(MarginHint)); } }

        /// <summary>
        /// Minimum breakout. Kept as free text (a session restores into it, and
        /// the engine reads it), with <see cref="SelectedBreakoutOption"/> as the
        /// sanctioned 50 / 40 switch on thick glass.
        /// </summary>
        public string MinBreakoutText
        {
            get => _minBreakoutText;
            set
            {
                if (string.Equals(_minBreakoutText, value, StringComparison.Ordinal)) return;
                _minBreakoutText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MarginHint));

                // Keep the switch in step with the text, so the ComboBox never
                // shows a value that is not what the engine will use.
                string? matched = BreakoutOptions.FirstOrDefault(o =>
                    string.Equals(o, (value ?? string.Empty).Trim(), StringComparison.Ordinal));
                if (!string.Equals(_selectedBreakoutOption, matched, StringComparison.Ordinal))
                {
                    _selectedBreakoutOption = matched;
                    OnPropertyChanged(nameof(SelectedBreakoutOption));
                }
            }
        }

        public bool RotationEnabled { get => _rotationEnabled; set { _rotationEnabled = value; OnPropertyChanged(); } }

        public ICommand LoadHouseDefaultsCommand { get; }

        public ObservableCollection<string> EffortLevels { get; } = new() { "1", "2", "3", "4", "5" };
        private string _selectedEffortLevel = "3";
        public string SelectedEffortLevel
        {
            get => _selectedEffortLevel;
            set
            {
                if (_selectedEffortLevel != value)
                {
                    _selectedEffortLevel = value;
                    OnPropertyChanged();
                    SyncStageStrategyFromEffort(value);
                }
            }
        }

        public ObservableCollection<string> StageStrategies { get; } = new()
        {
            "Stage 1: QUICK (Deterministic Shelf)",
            "Stage 2: Effort 2 (Multi-Order Grid)",
            "Stage 3: Effort 3 (Stratified Smart Search)",
            "Stage 4: Effort 4 (Windowed Best-Split & Sheet-Fill)",
            "Stage 5: Effort 5 (Deep Smart Placement & Consolidation)"
        };

        private string _selectedStageStrategy = "Stage 3: Effort 3 (Stratified Smart Search)";
        public string SelectedStageStrategy
        {
            get => _selectedStageStrategy;
            set
            {
                if (_selectedStageStrategy != value)
                {
                    _selectedStageStrategy = value;
                    OnPropertyChanged();
                    SyncEffortFromStageStrategy(value);
                }
            }
        }

        private void SyncStageStrategyFromEffort(string effort)
        {
            _selectedStageStrategy = effort switch
            {
                "1" => StageStrategies[0],
                "2" => StageStrategies[1],
                "3" => StageStrategies[2],
                "4" => StageStrategies[3],
                "5" => StageStrategies[4],
                _ => StageStrategies[2]
            };
            OnPropertyChanged(nameof(SelectedStageStrategy));
        }

        private void SyncEffortFromStageStrategy(string strategy)
        {
            if (string.IsNullOrWhiteSpace(strategy)) return;
            string effort = strategy.StartsWith("Stage 1", StringComparison.OrdinalIgnoreCase) ? "1"
                          : strategy.StartsWith("Stage 2", StringComparison.OrdinalIgnoreCase) ? "2"
                          : strategy.StartsWith("Stage 3", StringComparison.OrdinalIgnoreCase) ? "3"
                          : strategy.StartsWith("Stage 4", StringComparison.OrdinalIgnoreCase) ? "4"
                          : strategy.StartsWith("Stage 5", StringComparison.OrdinalIgnoreCase) ? "5"
                          : "3";
            if (_selectedEffortLevel != effort)
            {
                _selectedEffortLevel = effort;
                OnPropertyChanged(nameof(SelectedEffortLevel));
            }
        }

        // ── margins ────────────────────────────────────────────────────

        /// <summary>
        /// Writes one house profile into the SETTINGS fields.
        /// </summary>
        /// <param name="thickness">Thickness whose bucket supplies the numbers.</param>
        /// <param name="breakoutMm">
        /// Breakout to apply. Null keeps the house primary; a sanctioned
        /// alternative (40 on thick glass) is honoured, anything else is
        /// clamped back to the bucket default so a stray value can never
        /// disable breakout protection.
        /// </param>
        private void ApplyHouseDefaults(int thickness, int? breakoutMm = null)
        {
            var m = MarginProfile.DefaultFor(thickness, breakoutMm);

            ThicknessText = thickness.ToString(CultureInfo.InvariantCulture);
            LeftMarginText = m.LeftMm.ToString(CultureInfo.InvariantCulture);
            RightMarginText = m.RightMm.ToString(CultureInfo.InvariantCulture);
            TopMarginText = m.TopMm.ToString(CultureInfo.InvariantCulture);
            BottomMarginText = m.BottomMm.ToString(CultureInfo.InvariantCulture);
            KerfText = m.KerfMm.ToString(CultureInfo.InvariantCulture);
            MinBreakoutText = m.MinBreakoutMm.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// LOAD HOUSE DEFAULTS. Preserves a breakout the operator already chose
        /// from the sanctioned 50 / 40 pair, so switching thickness does not
        /// silently undo a deliberate breakout decision.
        /// </summary>
        private void LoadHouseDefaults()
        {
            int thickness = CurrentThicknessMm;

            int? keepBreakout = TryParseWholeMm(MinBreakoutText, out int bo)
                                && MarginProfile.HasBreakoutChoice(thickness)
                                && (bo == MarginProfile.BreakoutPrimaryMm || bo == MarginProfile.BreakoutAlternateMm)
                ? bo
                : (int?)null;

            ApplyHouseDefaults(thickness, keepBreakout);
        }

        /// <summary>
        /// True when every SETTINGS field still holds the house numbers for
        /// <paramref name="thickness"/> — i.e. the operator has not customized
        /// them. On thick glass either sanctioned breakout (50 or 40) counts as
        /// house, because both are floor-approved; on thin and medium glass the
        /// breakout must equal the margin.
        /// </summary>
        private bool FieldsAreHouseDefaults(int thickness)
        {
            var house = MarginProfile.DefaultFor(thickness);

            if (!TryParseWholeMm(LeftMarginText, out int l)) return false;
            if (!TryParseWholeMm(RightMarginText, out int r)) return false;
            if (!TryParseWholeMm(TopMarginText, out int t)) return false;
            if (!TryParseWholeMm(BottomMarginText, out int b)) return false;
            if (!TryParseWholeMm(KerfText, out int kerf)) return false;
            if (!TryParseWholeMm(MinBreakoutText, out int bo)) return false;

            bool breakoutIsHouse = MarginProfile.HasBreakoutChoice(thickness)
                ? bo == MarginProfile.BreakoutPrimaryMm || bo == MarginProfile.BreakoutAlternateMm
                : bo == house.MinBreakoutMm;

            return l == house.LeftMm
                && r == house.RightMm
                && t == house.TopMm
                && b == house.BottomMm
                && kerf == house.KerfMm
                && breakoutIsHouse;
        }

        /// <summary>
        /// The thickness trigger. Fires from the <see cref="ThicknessText"/>
        /// setter and follows the owner's "auto select as per thickness" rule
        /// without ever destroying customized work:
        ///
        ///   • same bucket as before → the house numbers are identical, so
        ///     there is nothing to select;
        ///   • different bucket, fields untouched → apply the new house profile
        ///     automatically, keeping a sanctioned breakout choice;
        ///   • different bucket, fields customized → change NOTHING, and let
        ///     <see cref="MarginHint"/> name the new house numbers and offer
        ///     the one-click.
        /// </summary>
        private void AutoSelectHouseDefaultsOnThicknessChange(int previousThickness)
        {
            int thickness = CurrentThicknessMm;

            if (MarginProfile.BucketFor(thickness) == MarginProfile.BucketFor(previousThickness))
                return;

            if (!FieldsAreHouseDefaults(previousThickness))
            {
                // Customized. Overwriting here is exactly the RC-016 mistake:
                // the operator did work, a routine navigation-style event
                // erased it, and nothing said so.
                Services.Logging.AppLogService.Info(
                    $"[OPT-MARGINS] thickness {previousThickness}mm → {thickness}mm changed bucket " +
                    $"({MarginProfile.BucketFor(previousThickness)} → {MarginProfile.BucketFor(thickness)}) " +
                    "but the SETTINGS fields are customized, so they were left alone.",
                    new Services.Logging.LogContext { Module = "Optimization", Operation = "MarginsAutoSelect" });
                return;
            }

            int? keepBreakout = TryParseWholeMm(MinBreakoutText, out int bo)
                                && (bo == MarginProfile.BreakoutPrimaryMm || bo == MarginProfile.BreakoutAlternateMm)
                ? bo
                : (int?)null;

            ApplyHouseDefaults(thickness, keepBreakout);

            Services.Logging.AppLogService.Info(
                $"[OPT-MARGINS] thickness {previousThickness}mm → {thickness}mm auto-selected the " +
                $"{MarginProfile.BucketFor(thickness)} house profile " +
                $"(LM {LeftMarginText} RM {RightMarginText} TM {TopMarginText} BM {BottomMarginText} " +
                $"kerf {KerfText} breakout {MinBreakoutText}).",
                new Services.Logging.LogContext { Module = "Optimization", Operation = "MarginsAutoSelect" });
        }

        private static bool TryParseWholeMm(string? text, out int value) =>
            int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out value);

        public string ThicknessSourceHint
        {
            get
            {
                var thicknesses = InputThicknesses();
                return thicknesses.Count > 1
                    ? "Mixed thicknesses: house defaults apply separately to each glass group. Select a group to inspect its thickness. Custom margins apply as entered."
                    : "Thickness follows the imported spec/pane. Manual margin edits are preserved; LOAD HOUSE DEFAULTS resets them.";
            }
        }

        private List<int> InputThicknesses() => Pieces
            .Where(p => SelectedGroupMatches(p.GlassRef) && p.TryParse(out _, out _, out _))
            .Select(p => p.ThicknessMm > 0 ? p.ThicknessMm : ParseThicknessMm(p.GlassRef))
            .Where(t => t > 0).Distinct().ToList();

        private void SyncThicknessFromInputs()
        {
            if (IsRunning || IsDisposed) return;
            var thicknesses = InputThicknesses();
            if (thicknesses.Count == 1)
                ThicknessText = thicknesses[0].ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(ThicknessSourceHint));
        }

        internal const int MaxExpandedParts = 2_000_000;

        private bool TryBuildInputs(
            out List<CutPiece> pieces, out List<(StockSize Stock, string Composition)> stocks, out MarginProfile margins)
        {
            pieces = new List<CutPiece>();
            stocks = new List<(StockSize Stock, string Composition)>();
            margins = null;

            long totalUnits = 0;
            foreach (var row in Pieces)
            {
                if (!row.TryParse(out _, out _, out int quantity))
                {
                    RunMessage = $"Piece \"{row.PieceLabel}\" needs positive whole-mm dimensions and quantity.";
                    return false;
                }
                totalUnits += quantity;
            }
            if (totalUnits > MaxExpandedParts)
            {
                RunMessage = $"This list expands to {totalUnits:N0} pieces; the supported admission limit is {MaxExpandedParts:N0}. Split the job before running.";
                return false;
            }
            pieces.Capacity = (int)totalUnits;
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
                        JobRef = string.IsNullOrWhiteSpace(p.JobOrderId) ? p.ProformaId : p.JobOrderId,
                        ProformaId = p.ProformaId,
                        JobOrderId = p.JobOrderId,
                        SpecGroupNumber = p.SpecGroupNumber,
                        SourceRowSerial = p.SourceRowSerial,
                        Color = p.Color,
                        MaterialKey = p.MaterialKey,
                        GlassType = p.GlassType,
                        Processing = p.Processing,
                        Coating = p.Coating,
                        GlassRef = p.GlassRef ?? string.Empty,
                        // "G-101 3/24" — piece identity the 2D view / cut
                        // plan carry, matching the design doc's labelling.
                        PieceLabel = q > 1
                            ? $"{baseLabel} {i + 1}/{q}"
                            : baseLabel,
                        Position = p.Position ?? string.Empty,
                        ThicknessMm = p.ThicknessMm > 0 ? p.ThicknessMm : ParseThicknessMm(p.GlassRef),
                        WidthMm = w,
                        HeightMm = h,
                        AllowRotate = RotationEnabled && !p.RotationLocked
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
                string available = (s.AvailableText ?? "").Trim();
                int max;
                if (available.Length == 0 || available == "∞") max = StockSize.UnlimitedSheets;
                else if (!int.TryParse(available, NumberStyles.Integer, CultureInfo.InvariantCulture, out max) || max < 0)
                {
                    RunMessage = "Stock availability must be a non-negative whole number, blank or ∞ (STOCKS tab).";
                    return false;
                }
                else if (max == 0) continue; // zero balance is not unlimited stock
                stocks.Add((new StockSize { WidthMm = w, HeightMm = h, MaxSheets = max,
                    ThicknessMm = s.ThicknessMm > 0 ? s.ThicknessMm : ParseThicknessMm(s.Composition),
                    Color = string.IsNullOrWhiteSpace(s.Color) ? (s.Composition ?? string.Empty).Trim().ToUpperInvariant() : s.Color,
                    Label = s.Composition, MaterialKey = s.MaterialKey },
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
                MinBreakoutMm = breakout,
                IsCustomized = !FieldsAreHouseDefaults(thickness)
            };
            return true;
        }
    }
}
