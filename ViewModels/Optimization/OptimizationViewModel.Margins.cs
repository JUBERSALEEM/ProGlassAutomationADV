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
                ThicknessBucket.Thick => "LM/RM/TM 50, BM 0 — bottom edge cut on the float line",
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

        private bool _allowCrossCompositionStock; // floor default: OFF — never pack on the wrong glass

        /// <summary>
        /// PHASE 5 / finding 5a: opt-in for packing a cutting group on racks
        /// belonging to a DIFFERENT composition. Off by default, because the plan
        /// it produces is not physically cuttable — it exists for an operator who
        /// wants a ballpark sheet count across mixed racks and understands that
        /// the result is marked as a ballpark. When it engages, the group's note
        /// says so explicitly.
        /// </summary>
        public bool AllowCrossCompositionStock
        {
            get => _allowCrossCompositionStock;
            set { _allowCrossCompositionStock = value; OnPropertyChanged(); }
        }

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

        private bool TryBuildInputs(
            out List<CutPiece> pieces, out List<(StockSize Stock, string Composition)> stocks, out MarginProfile margins)
        {
            pieces = new List<CutPiece>();
            stocks = new List<(StockSize Stock, string Composition)>();
            margins = null;

            // PHASE 8 / finding 20 (2026-09-20): expansion below allocates one
            // CutPiece — plus one interpolated "3/24" style label string — per
            // UNIT, with no ceiling. A single paste error in a quantity column
            // ("999999" instead of "999") therefore allocated roughly a million
            // objects before the engine started, and the only backstop in the
            // whole system was the catch (OutOfMemoryException) in RunCoreAsync,
            // which fires AFTER the allocation. The governor scales search
            // breadth by part count but never bounds it, so the hard ceiling
            // belongs here, at the point where the count becomes memory.
            long totalUnits = 0;
            string largestRow = string.Empty;
            int largestQty = 0;

            foreach (var p in Pieces)
            {
                if (!p.TryParse(out _, out _, out int probeQty) || probeQty <= 0) continue;
                totalUnits += probeQty;
                if (probeQty > largestQty)
                {
                    largestQty = probeQty;
                    largestRow = string.IsNullOrWhiteSpace(p.PieceLabel)
                        ? (p.GlassRef ?? "(unlabelled)").Trim()
                        : p.PieceLabel.Trim();
                }
            }

            if (totalUnits > MaxExpandedParts)
            {
                RunMessage = string.Format(CultureInfo.InvariantCulture,
                    "This cutting list expands to {0:N0} individual pieces, above the {1:N0} ceiling — " +
                    "the run was not started. The largest single row is “{2}” at {3:N0}. " +
                    "Check the quantity column for a paste error, or split the job into smaller cutting groups.",
                    totalUnits, MaxExpandedParts, largestRow, largestQty);
                try
                {
                    Services.Logging.AppLogService.Warning(
                        $"[OPT-INPUT] TryBuildInputs REFUSED: {totalUnits} expanded pieces exceeds the " +
                        $"{MaxExpandedParts} ceiling (largest row “{largestRow}” = {largestQty}).",
                        new Services.Logging.LogContext { Module = "Optimization", Operation = "TryBuildInputs" });
                }
                catch { }
                return false;
            }

            if (totalUnits >= ExpandedPartsWarnThreshold)
            {
                AppendInputWarning(string.Format(CultureInfo.InvariantCulture,
                    "Large run: {0:N0} individual pieces. Expect a long search — CANCEL is available throughout.",
                    totalUnits));
            }

            // PHASE 2 / finding 19 (2026-09-20): one unreadable row used to
            // abort the ENTIRE run and name only the first offender, so a
            // 500-row import with a single typo produced nothing at all.
            // Invalid rows are now skipped, listed, and counted — but never
            // quietly: the operator sees every excluded row and the run states
            // that its quantity is missing, because the standing rule is that
            // pieces must never disappear without being accounted for.
            var invalidRows = new List<string>();

            foreach (var p in Pieces)
            {
                if (!p.TryParse(out int w, out int h, out int q))
                {
                    invalidRows.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} (W={1} H={2} Qty={3})",
                        string.IsNullOrWhiteSpace(p.PieceLabel) ? "(unlabelled)" : p.PieceLabel.Trim(),
                        (p.WidthText ?? string.Empty).Trim(),
                        (p.HeightText ?? string.Empty).Trim(),
                        (p.QtyText ?? string.Empty).Trim()));
                    continue;
                }
                p.EnsureMaterialKey();
                bool sourced = !_sizeOnlyPack
                    && (!string.IsNullOrWhiteSpace(p.JobOrderId)
                        || !string.IsNullOrWhiteSpace(p.ProformaId)
                        || p.SpecGroupNumber > 0);
                if (sourced && string.IsNullOrWhiteSpace(p.MaterialKey))
                {
                    invalidRows.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} unresolved colour/thickness (not invented from composition)",
                        string.IsNullOrWhiteSpace(p.PieceLabel) ? "(unlabelled)" : p.PieceLabel.Trim()));
                    continue;
                }
                for (int i = 0; i < q; i++)
                {
                    string baseLabel = string.IsNullOrWhiteSpace(p.PieceLabel)
                        ? (p.GlassRef ?? string.Empty).Trim()
                        : p.PieceLabel.Trim();
                    pieces.Add(new CutPiece
                    {
                        JobRef = string.IsNullOrWhiteSpace(p.JobOrderId) ? (p.ProformaId ?? string.Empty) : p.JobOrderId,
                        GlassRef = p.GlassRef ?? string.Empty,
                        // "G-101 3/24" — piece identity the 2D view / cut
                        // plan carry, matching the design doc's labelling.
                        PieceLabel = q > 1
                            ? $"{baseLabel} {i + 1}/{q}"
                            : baseLabel,
                        Position = p.Position ?? string.Empty,
                        Color = p.Color ?? string.Empty,
                        ThicknessMm = p.ThicknessMm > 0 ? p.ThicknessMm : ParseThicknessMm(p.GlassRef),
                        WidthMm = w,
                        HeightMm = h,
                        // PHASE 8 / finding 8e (2026-09-20): per-row rotation lock.
                        // The packer has always honoured CutPiece.AllowRotate —
                        // Orientations(), the smart-placement candidate filter and
                        // the placement verifier all test it — but this line wrote
                        // the global SETTINGS choice for every row, so the only
                        // option on offer was all-or-nothing. A locked pane now
                        // keeps the orientation the operator typed; every unlocked
                        // pane behaves exactly as before.
                        AllowRotate = RotationEnabled && !p.RotationLocked,
                        ProformaId = p.ProformaId ?? string.Empty,
                        JobOrderId = p.JobOrderId ?? string.Empty,
                        SpecGroupNumber = p.SpecGroupNumber,
                        SourceRowSerial = p.SourceRowSerial,
                        GlassType = p.GlassType ?? string.Empty,
                        Processing = p.Processing ?? string.Empty,
                        Coating = p.Coating ?? string.Empty,
                        W1 = p.W1 > 0 ? p.W1 : w,
                        H1 = p.H1 > 0 ? p.H1 : h,
                        W2 = p.W2,
                        H2 = p.H2,
                        SheetStoreSku = p.SheetStoreSku ?? string.Empty,
                        SheetWidthMm = p.SheetWidthMm,
                        SheetHeightMm = p.SheetHeightMm,
                        SheetPricePerSqm = p.SheetPricePerSqm,
                        MaterialKey = p.MaterialKey ?? string.Empty,
                        Qty = 1
                    });
                }
            }
            if (pieces.Count == 0)
            {
                bool unresolvedOnly = invalidRows.Count > 0
                    && invalidRows.TrueForAll(r => r.IndexOf("unresolved", StringComparison.OrdinalIgnoreCase) >= 0);
                RunMessage = unresolvedOnly
                    ? $"Cannot optimize: {invalidRows.Count} pane(s) have unresolved colour or thickness — " +
                      "identity was not invented from composition."
                    : invalidRows.Count > 0
                    ? $"Nothing to cut: all {invalidRows.Count} piece row(s) are invalid — " +
                      "each needs a positive W×H and quantity."
                    : "Add pieces first (JOBS tab) or import a cutting list from Step 5.";
                try
                {
                    Services.Logging.AppLogService.Warning(
                        $"[OPT-INPUT] TryBuildInputs FAIL no usable pieces: PiecesRows={Pieces.Count} " +
                        $"invalid={invalidRows.Count} IsHeadless={IsHeadlessHost} doc={DocumentKey}" +
                        (invalidRows.Count > 0 ? " | " + string.Join(" ; ", invalidRows.ToArray()) : string.Empty),
                        new Services.Logging.LogContext { Module = "Optimization", Operation = "TryBuildInputs" });
                }
                catch { }
                return false;
            }

            // PHASE 2 / finding 1 (2026-09-20): availability used to be
            //     int max = int.TryParse(s.AvailableText, NumberStyles.Integer, …, out int a)
            //               ? Math.Max(0, a) : 0;
            // and the packer reads MaxSheets == 0 as UNLIMITED
            // (GuillotinePacker.cs:1000, Advanced.cs:38, Smart.cs:112). Three
            // everyday inputs therefore produced a plan built on infinite
            // glass: a blank cell, anything NumberStyles.Integer rejects — and
            // "1,000" is rejected, because that style allows no thousands
            // separator, which makes an Excel paste of a stock balance the
            // single most likely trigger — and a negative number, which
            // Math.Max clamped to the same 0. The plan then reported an
            // excellent sheet count and utilisation for racks that do not
            // exist, and the cost followed it.
            //
            // The rule adopted here: a parse failure is an ERROR, never a
            // default. Only an explicitly blank field means "no limit", and
            // that is surfaced rather than assumed.
            var zeroStockNotes = new List<string>();

            foreach (var s in Stocks)
            {
                // Only racks ticked "use in run" are fed to the optimizer —
                // the STOCKS tab selects which Step 4 sheets each spec uses.
                if (!s.IsUsedInRun) continue;

                if (!TryParseWholeMm(s.WidthText, out int w) ||
                    !TryParseWholeMm(s.HeightText, out int h) ||
                    w <= 0 || h <= 0)
                {
                    RunMessage = $"Rack \"{RackLabel(s)}\" has an invalid size (STOCKS tab) — " +
                        "width and height must be whole millimetres greater than zero.";
                    return false;
                }

                string availText = (s.AvailableText ?? string.Empty).Trim();
                int max;

                if (availText.Length == 0 || IsUnlimitedToken(availText))
                {
                    // Explicitly no limit. The row says so, so "∞" on screen and
                    // MaxSheets == 0 in the engine are the same statement.
                    max = StockSize.UnlimitedSheets;
                }
                else if (!TryParseSheetCount(availText, out int parsed))
                {
                    RunMessage = $"Rack {w}×{h} has an availability the engine cannot read " +
                        $"(\"{availText}\") — enter a whole number of sheets, or leave the " +
                        "field blank for no limit.";
                    return false;
                }
                else if (parsed < 0)
                {
                    RunMessage = $"Rack {w}×{h} has a negative availability ({parsed}) — " +
                        "a stock balance cannot be below zero.";
                    return false;
                }
                else if (parsed == 0)
                {
                    // Zero means ZERO, not infinite. The rack is left out of the
                    // pool and the operator is told, which is the only reading
                    // that cannot quietly hand the engine unlimited glass.
                    zeroStockNotes.Add($"{w}×{h}");
                    continue;
                }
                else
                {
                    max = parsed;
                }

                // PHASE 9 / finding B3: the rack now carries its thickness.
                //
                // This was the ONLY place in the module that built a StockSize,
                // and it set width, height and balance but never ThicknessMm — so
                // every rack in every run carried 0. Two consequences:
                //
                //   1. GuillotinePacker.VerifyComprehensive has a piece-vs-stock
                //      thickness check, guarded by `sheet.Stock.ThicknessMm > 0`.
                //      With the field always 0 it could never fire from this
                //      module: 8mm pieces laid onto a 6mm rack passed the oracle
                //      that Phase 8 just made the export gate.
                //   2. The run-wide balance ledger (finding B2) keys racks on
                //      size + thickness + colour. Without a thickness, two
                //      different glasses at the same dimensions shared one
                //      ledger entry.
                //
                // Pieces already derive their thickness from their own glass ref
                // through this same parser, and a dedicated rack's composition IS
                // that glass ref — so the two sides agree by construction. A
                // manual rack has no composition, ParseThicknessMm returns 0, and
                // the check stays dormant exactly as it is today.
                stocks.Add((new StockSize
                {
                    WidthMm = w,
                    HeightMm = h,
                    MaxSheets = max,
                    ThicknessMm = s.ThicknessMm > 0 ? s.ThicknessMm : ParseThicknessMm(s.Composition),
                    Color = s.Color ?? string.Empty,
                    MaterialKey = s.MaterialKey ?? string.Empty
                }, s.Composition ?? string.Empty));
            }

            if (zeroStockNotes.Count > 0)
            {
                string listed = string.Join(", ", zeroStockNotes
                    .Take(6)
                    .ToArray());
                if (zeroStockNotes.Count > 6)
                    listed += $" +{zeroStockNotes.Count - 6} more";

                if (stocks.Count == 0)
                {
                    RunMessage = $"Every ticked rack has 0 sheets available ({listed}) — nothing to cut from. " +
                        "Update the balances on STOCKS, or leave a balance blank for no limit.";
                    return false;
                }

                // A warning on the SUCCESS path cannot go into RunMessage: Run()
                // clears RunMessage after TryBuildInputs returns, so it would be
                // wiped before the operator saw it. Input warnings are carried
                // in a field and re-applied by Run() straight after the clear.
                AppendInputWarning($"Rack(s) with 0 sheets available were left out of the run: {listed}. " +
                    "Zero means zero — leave a balance blank for no limit.");
            }

            if (stocks.Count == 0)
            {
                RunMessage = "Add at least one stock size and tick it for use (STOCKS tab).";
                try
                {
                    Services.Logging.AppLogService.Warning(
                        $"[OPT-INPUT] TryBuildInputs FAIL no stocks: StocksRows={Stocks.Count} StocksUsed={Stocks.Count(s=>s.IsUsedInRun)} IsHeadless={IsHeadlessHost} doc={DocumentKey} PiecesRows={Pieces.Count}",
                        new Services.Logging.LogContext { Module = "Optimization", Operation = "TryBuildInputs" });
                }
                catch { }
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

            int thickness = TryParseWholeMm(ThicknessText, out int th) && th > 0 ? th : 6;

            // PHASE 1 / finding 3 (2026-09-20): "did the operator customize?"
            // is decided HERE, where the operator's text is read, by comparing
            // against the house table for this thickness. It used to be inferred
            // downstream in ResolveGroupMargins from `LeftMm != 0 || TopMm != 0`,
            // which discarded a deliberate zero on those two edges and silently
            // substituted house defaults instead.
            //
            // The breakout comparison goes through DefaultFor so the sanctioned
            // 50 / 40 choice on thick glass counts as house, while any other
            // breakout value counts as customized.
            var house = MarginProfile.DefaultFor(thickness, breakout);
            bool customized =
                left != house.LeftMm
                || right != house.RightMm
                || top != house.TopMm
                || bottom != house.BottomMm
                || kerf != house.KerfMm
                || breakout != house.MinBreakoutMm;

            margins = new MarginProfile
            {
                ThicknessMm = thickness,
                LeftMm = left,
                RightMm = right,
                TopMm = top,
                BottomMm = bottom,
                KerfMm = kerf,
                MinBreakoutMm = breakout,
                IsCustomized = customized
            };

            // ── DIAGNOSTIC LOGGING (Rule 18) ────────────────────────────────
            try
            {
                Services.Logging.AppLogService.Info(
                    $"[OPT-INPUT] TryBuildInputs ok: {pieces.Count} expanded pieces (from {Pieces.Count} rows), " +
                    $"{stocks.Count} stock racks ({stocks.Count(t => t.Stock.MaxSheets == StockSize.UnlimitedSheets)} with no limit)" +
                    $"{(zeroStockNotes.Count > 0 ? ", " + zeroStockNotes.Count + " left out at zero balance" : "")}, " +
                    $"margins L={left} R={right} T={top} B={bottom} kerf={kerf} breakout={breakout} " +
                    $"thickness={thickness} customized={customized} rot={RotationEnabled}",
                    new Services.Logging.LogContext { Module = "Optimization", Operation = "TryBuildInputs" });
            }
            catch { }

            // PHASE 2 / finding 19: excluded rows are named, counted, and stated
            // to be OUTSIDE the plan. Skipping a bad row is acceptable; letting
            // its quantity vanish without an accounting is not.
            if (invalidRows.Count > 0)
            {
                string listed = string.Join(" ; ", invalidRows.Take(8).ToArray());
                if (invalidRows.Count > 8)
                    listed += $" ; +{invalidRows.Count - 8} more";

                AppendInputWarning(
                    $"{invalidRows.Count} piece row(s) were EXCLUDED — their quantity is NOT in this plan: {listed}.");

                try
                {
                    Services.Logging.AppLogService.Warning(
                        $"[OPT-INPUT] TryBuildInputs excluded {invalidRows.Count} invalid row(s): " +
                        string.Join(" ; ", invalidRows.ToArray()),
                        new Services.Logging.LogContext { Module = "Optimization", Operation = "TryBuildInputs" });
                }
                catch { }
            }

            return true;
        }

        // ── input warnings carried across the RunMessage clear ─────────
        //
        // Run() calls TryBuildInputs BEFORE it resets RunMessage for the new
        // run, so anything written to RunMessage on a SUCCESS path is wiped a
        // few lines later. Warnings that must survive are collected here and
        // re-applied by Run() immediately after the clear.

        private readonly List<string> _inputWarnings = new();

        /// <summary>Warnings from the last input build, or empty.</summary>
        internal IReadOnlyList<string> InputWarnings => _inputWarnings;

        private void AppendInputWarning(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (!_inputWarnings.Contains(message)) _inputWarnings.Add(message);
        }

        /// <summary>Starts a fresh warning list. Called by Run() before building inputs.</summary>
        internal void ClearInputWarnings() => _inputWarnings.Clear();

        /// <summary>
        /// Re-applies the input warnings to the run header. APPENDS, never
        /// replaces: the summary ApplyResults writes is what the operator reads
        /// first, and the same append-never-replace rule protects these notes
        /// on exactly the large jobs where they matter most.
        /// </summary>
        internal void ApplyInputWarningsToRunMessage()
        {
            if (_inputWarnings.Count == 0) return;

            string notes = string.Join(" ", _inputWarnings.ToArray());
            RunMessage = string.IsNullOrWhiteSpace(RunMessage)
                ? notes
                : RunMessage + " " + notes;
        }

        // ── PHASE 8 scale ceilings ───────────────────────────────────────
        //
        // OptimizationSearchGovernor documents its industrial tier as
        // "15,001 - 1,000,000+ parts", so the hard ceiling sits at twice that
        // envelope: a run above it is a data-entry error, not a job. The warning
        // threshold is far below the ceiling so a legitimately large job is told
        // what to expect rather than being refused.

        /// <summary>Hard ceiling on expanded pieces per run.</summary>
        internal const int MaxExpandedParts = 2_000_000;

        /// <summary>Above this the operator is told the run will be slow.</summary>
        internal const int ExpandedPartsWarnThreshold = 250_000;

        // ── PHASE 2 input parsing helpers ────────────────────────────────
        //
        // Every one of these exists because a parse failure used to resolve to
        // a PERMISSIVE DEFAULT: an unreadable stock balance became 0, which the
        // packer reads as unlimited glass; an unreadable thickness became 0,
        // which became the 6mm house margin profile. The rule now is that a
        // parse failure is an error the operator is told about, and only an
        // explicitly blank field takes a documented default.

        /// <summary>
        /// Reads a whole millimetre value, tolerating the thousands separator an
        /// Excel paste carries ("3,300") and surrounding whitespace. Returns
        /// false — never a silent zero — when the text is not a whole number.
        /// </summary>
        private static bool TryParseWholeMm(string? text, out int value) =>
            int.TryParse(
                (text ?? string.Empty).Trim(),
                NumberStyles.Integer | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out value);

        /// <summary>
        /// Reads a sheet count. "1,000" is the case that mattered: the old
        /// NumberStyles.Integer rejected it, and the rejected value became 0,
        /// which the packer reads as unlimited.
        /// </summary>
        private static bool TryParseSheetCount(string? text, out int value) =>
            int.TryParse(
                (text ?? string.Empty).Trim(),
                NumberStyles.Integer | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out value);

        /// <summary>
        /// The tokens that mean "this rack has no balance limit". Deliberately
        /// short: anything not listed here and not a whole number is refused,
        /// because a generous synonym list is how an unreadable balance quietly
        /// turns into infinite glass again.
        /// </summary>
        private static bool IsUnlimitedToken(string text)
        {
            string t = text.Trim();
            return t == "∞"
                || string.Equals(t, "inf", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "unlimited", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "no limit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "nolimit", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Identifies a rack in a refusal message, using the text the
        /// operator actually typed rather than a value that failed to parse.</summary>
        private static string RackLabel(StockEntryVm s)
        {
            string size = $"{(s.WidthText ?? "?").Trim()}×{(s.HeightText ?? "?").Trim()}";
            string composition = (s.Composition ?? string.Empty).Trim();
            string source = (s.SourceLabel ?? string.Empty).Trim();
            return composition.Length > 0 ? $"{size} · {composition}"
                 : source.Length > 0 ? $"{size} · {source}"
                 : size;
        }

        // ── money parsing ──────────────────────────────────────────────
        //
        // PHASE 2 / finding 6. `TextParsing.CleanNum` strips every comma
        // before parsing, which is right for a count and wrong for money: a
        // European decimal comma became a magnitude change ("45,5" → 455).
        // Trying CurrentCulture and then InvariantCulture does not fix it
        // either — each reads the OTHER culture's decimal separator as a
        // thousands separator, so the two guesses differ by exactly the
        // factor that matters and neither can be trusted.
        //
        // So money is parsed by SHAPE. A rate per m² is unambiguous in one of
        // four forms; anything else is refused with a message rather than
        // guessed, because the wrong guess here reaches a customer invoice.

        private static readonly System.Text.RegularExpressions.Regex MoneyPlain =
            new System.Text.RegularExpressions.Regex(@"^\d+$",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>One separator, one or two decimals — a decimal point in
        /// either culture's spelling.</summary>
        private static readonly System.Text.RegularExpressions.Regex MoneyDecimal =
            new System.Text.RegularExpressions.Regex(@"^(\d+)[.,](\d{1,2})$",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>Invariant thousands grouping, optional decimals: 1,234 or 1,234.50.</summary>
        private static readonly System.Text.RegularExpressions.Regex MoneyGroupedInvariant =
            new System.Text.RegularExpressions.Regex(@"^(\d{1,3}(?:,\d{3})+)(?:\.(\d{1,2}))?$",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>European thousands grouping, optional decimals: 1.234 or 1.234,50.</summary>
        private static readonly System.Text.RegularExpressions.Regex MoneyGroupedEuropean =
            new System.Text.RegularExpressions.Regex(@"^(\d{1,3}(?:\.\d{3})+)(?:,(\d{1,2}))?$",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// Reads a money amount from operator-typed text without guessing at a
        /// separator. Returns false — never a silent zero — when the shape is
        /// not one of the four unambiguous forms.
        /// </summary>
        private static bool TryParseMoney(string? text, out decimal value)
        {
            value = 0m;
            string t = (text ?? string.Empty).Trim();
            if (t.Length == 0) return false;

            // A leading currency word or symbol is tolerated; the digits are not.
            t = t.Replace("AED", string.Empty, StringComparison.OrdinalIgnoreCase)
                 .Replace("₨", string.Empty)
                 .Replace("£", string.Empty)
                 .Replace("$", string.Empty)
                 .Replace(" ", string.Empty)
                 .Trim();
            if (t.Length == 0) return false;

            bool negative = t.StartsWith("-", StringComparison.Ordinal);
            if (negative || t.StartsWith("+", StringComparison.Ordinal)) t = t.Substring(1);

            var plain = MoneyPlain.Match(t);
            if (plain.Success)
            {
                if (!decimal.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out value))
                    return false;
            }
            else
            {
                var dec = MoneyDecimal.Match(t);
                if (dec.Success)
                {
                    // "45,5" and "45.5" are the same amount: one separator with
                    // one or two digits after it is a decimal point, not a group.
                    if (!decimal.TryParse(dec.Groups[1].Value + "." + dec.Groups[2].Value,
                            NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                        return false;
                }
                else
                {
                    var grouped = MoneyGroupedInvariant.Match(t);
                    var european = MoneyGroupedEuropean.Match(t);

                    if (grouped.Success && !european.Success)
                    {
                        string normalized = grouped.Groups[1].Value.Replace(",", string.Empty)
                            + (grouped.Groups[2].Success ? "." + grouped.Groups[2].Value : string.Empty);
                        if (!decimal.TryParse(normalized, NumberStyles.Number,
                                CultureInfo.InvariantCulture, out value))
                            return false;
                    }
                    else if (european.Success && !grouped.Success)
                    {
                        string normalized = european.Groups[1].Value.Replace(".", string.Empty)
                            + (european.Groups[2].Success ? "." + european.Groups[2].Value : string.Empty);
                        if (!decimal.TryParse(normalized, NumberStyles.Number,
                                CultureInfo.InvariantCulture, out value))
                            return false;
                    }
                    else
                    {
                        // Either no recognisable shape, or both groupings match
                        // ("1,234.567" style) — genuinely ambiguous, so refuse.
                        return false;
                    }
                }
            }

            if (negative) value = -value;
            return true;
        }
    }
}
