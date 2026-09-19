// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Optimization/OptimizationModels.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — glass cutting optimization (design 2026-09-12)
//
// Pure domain models for the sheet-optimization engine. No WPF, no EF —
// the engine is fully unit-testable and pinned by oracle tests
// (no-overlap, margins, kerf, rotation). See
// docs/OPTIMIZATION_MODULE_DESIGN_2026-09-12.md.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProGlassAutomation.Services.Optimization
{
    /// <summary>
    /// How pieces may be turned on the sheet. Free90 is ADAPTIVE: the
    /// piece keeps its original orientation whenever that fits — rotation
    /// is used only when the piece needs it (the floor's preferred 0/90
    /// mix). This is the default for every run.
    /// </summary>
    public enum RotationPolicy
    {
        /// <summary>Pieces stay exactly as entered (W×H).</summary>
        Locked,
        /// <summary>Original orientation first; rotate 90° only if it doesn't fit.</summary>
        Free90
    }

    /// <summary>Guillotine split direction after a placement.</summary>
    public enum SplitMode
    {
        /// <summary>Bottom remainder keeps the full width (calibrated default).</summary>
        HorizontalFirst,
        /// <summary>Right remainder keeps the full height.</summary>
        VerticalFirst,
        /// <summary>Split along the side with the longer leftover.</summary>
        LongerLeftover
    }

    /// <summary>
    /// Cutting allowances for one glass thickness. Margins are the edge
    /// trims cut off the raw stock before piece placement; kerf is the
    /// blade gap between cuts; MinBreakout is the narrowest offcut worth
    /// keeping (thinner strips are reported as trim waste).
    /// </summary>
    /// <summary>
    /// Which house-margin profile a glass thickness falls into.
    ///
    /// PHASE 1 (2026-09-20, owner-specified): the old table interpolated
    /// 15 / 20 / 25mm by thickness with a breakout fixed at 15mm for every
    /// glass. The floor runs three real profiles, and the third is
    /// deliberately ASYMMETRIC — thick glass takes no trim on the bottom
    /// edge because that edge is cut on the float line.
    /// </summary>
    public enum ThicknessBucket
    {
        /// <summary>Up to 6mm — 15mm on all four edges, breakout 15mm, kerf 3mm.</summary>
        Thin,
        /// <summary>7–8mm — 30mm on all four edges, breakout 30mm, kerf 3mm.</summary>
        Medium,
        /// <summary>9mm and above — LM/RM/TM 50mm, BM 0mm, breakout 50mm (switchable to 40mm), kerf 4mm.</summary>
        Thick
    }

    public sealed class MarginProfile
    {
        public int ThicknessMm { get; init; }
        public int LeftMm { get; init; }
        public int RightMm { get; init; }
        public int TopMm { get; init; }
        public int BottomMm { get; init; }
        public int KerfMm { get; init; }
        public int MinBreakoutMm { get; init; }

        /// <summary>
        /// True when these values came from the operator rather than from the
        /// house table for this thickness.
        ///
        /// PHASE 1 / finding 3: <c>ResolveGroupMargins</c> used to INFER
        /// "the operator customized this" from <c>LeftMm != 0 || TopMm != 0</c>
        /// — two of the four margin fields. So an operator who deliberately
        /// set Left = 0 and Top = 0 (a completely normal trim pattern, and the
        /// house pattern for the thick bucket's bottom edge) was read as
        /// NOT having customized anything, and house defaults for the parsed
        /// thickness were substituted with an empty note: wrong usable area,
        /// wrong sheet count, nothing on screen to explain it. Setting
        /// Right = 15 / Bottom = 15 with Left = Top = 0 was invisible too.
        ///
        /// Customization is now an explicit fact about the profile, decided
        /// where the operator's input is read (TryBuildInputs), not guessed
        /// from which numbers happen to be zero.
        /// </summary>
        public bool IsCustomized { get; init; }

        /// <summary>The breakout the floor prefers on thick glass.</summary>
        public const int BreakoutPrimaryMm = 50;

        /// <summary>
        /// The breakout the floor accepts instead on thick glass, when the
        /// offcut is worth keeping. Owner-specified as switchable; it is the
        /// only margin value with a sanctioned alternative.
        /// </summary>
        public const int BreakoutAlternateMm = 40;

        /// <summary>
        /// Maps a thickness onto its house bucket. Unlisted thicknesses ROUND
        /// UP to the next known bucket — thicker glass needs more trim, so
        /// rounding up is the conservative direction: 7mm takes the 8mm
        /// profile, 9mm and 11mm take the thick profile, and anything above
        /// 12mm stays thick. An unknown or non-positive thickness (a label the
        /// parser could not read) takes the thin profile, matching the
        /// long-standing 6mm fallback.
        /// </summary>
        public static ThicknessBucket BucketFor(int thicknessMm) =>
            thicknessMm <= 6 ? ThicknessBucket.Thin
            : thicknessMm <= 8 ? ThicknessBucket.Medium
            : ThicknessBucket.Thick;

        /// <summary>
        /// True when the thickness offers the 50 / 40 breakout choice. Only the
        /// thick bucket does — on thin and medium glass the breakout equals the
        /// margin, and there is nothing to choose between.
        /// </summary>
        public static bool HasBreakoutChoice(int thicknessMm) =>
            BucketFor(thicknessMm) == ThicknessBucket.Thick;

        /// <summary>
        /// House defaults for a thickness — editable in the SETTINGS tab.
        /// </summary>
        /// <param name="thicknessMm">Glass thickness in mm.</param>
        /// <param name="minBreakoutMm">
        /// Optional breakout override. Honoured ONLY where the floor has a
        /// choice (the thick bucket) and clamped to the two sanctioned values,
        /// so a stray 0 can never disable breakout protection on thin glass.
        /// </param>
        public static MarginProfile DefaultFor(int thicknessMm, int? minBreakoutMm = null)
        {
            ThicknessBucket bucket = BucketFor(thicknessMm);

            int left, right, top, bottom, breakout;
            switch (bucket)
            {
                case ThicknessBucket.Thick:
                    // LM 50 · RM 50 · TM 50 · BM 0 — the bottom edge is cut on
                    // the float line, so no trim is taken there.
                    left = right = top = 50;
                    bottom = 0;
                    breakout = minBreakoutMm == BreakoutAlternateMm
                        ? BreakoutAlternateMm
                        : BreakoutPrimaryMm;
                    break;

                case ThicknessBucket.Medium:
                    left = right = top = bottom = 30;
                    breakout = 30;
                    break;

                default:
                    left = right = top = bottom = 15;
                    breakout = 15;
                    break;
            }

            return new MarginProfile
            {
                ThicknessMm = thicknessMm,
                LeftMm = left,
                RightMm = right,
                TopMm = top,
                BottomMm = bottom,
                KerfMm = thicknessMm >= 10 ? 4 : 3,
                MinBreakoutMm = breakout
            };
        }

        /// <summary>
        /// True when every cutting-relevant value matches. Used to decide
        /// whether the operator has touched the house defaults, so an
        /// untouched SETTINGS tab can follow a thickness change automatically
        /// and a customized one is never overwritten underneath them.
        /// </summary>
        public bool SameGeometryAs(MarginProfile? other) =>
            other != null
            && LeftMm == other.LeftMm
            && RightMm == other.RightMm
            && TopMm == other.TopMm
            && BottomMm == other.BottomMm
            && KerfMm == other.KerfMm
            && MinBreakoutMm == other.MinBreakoutMm;
    }

    /// <summary>One stock sheet size in the run's pool.</summary>
    public sealed class StockSize
    {
        public int WidthMm { get; init; }
        public int HeightMm { get; init; }
        public int ThicknessMm { get; init; }
        public string Color { get; init; } = "";
        public string Label { get; init; } = "";
        public string MaterialKey { get; init; } = "";

        /// <summary>
        /// Optional cap (Sheet Store balance). <see cref="UnlimitedSheets"/> = no limit.
        /// </summary>
        public int MaxSheets { get; init; }

        /// <summary>
        /// The value <see cref="MaxSheets"/> takes when a rack has no balance
        /// limit. Named so no call site has to read a bare 0 as "infinite
        /// glass", and so the three packer strategies that special-case it
        /// (GuillotinePacker.OpenNewSheet, Advanced, Smart) can be traced from
        /// here.
        ///
        /// PHASE 2 / finding 1: this value must only ever be reached from an
        /// EXPLICITLY blank or "∞" availability field. An unreadable balance
        /// used to fall through to it, which handed the engine unlimited stock
        /// for a rack the operator had typed something into.
        /// </summary>
        public const int UnlimitedSheets = 0;

        /// <summary>
        /// The value the STOCKS <em>Available</em> field defaults to — a very large
        /// finite number meaning "no cap; usage is tracked, not limited".
        ///
        /// PHASE 9 (safe): this is NOT the same thing as
        /// <see cref="UnlimitedSheets"/>. 0 is the sentinel the three packer
        /// strategies special-case; 99999999 is an ordinary int they honour as a
        /// genuinely enormous cap. Any code asking "is this rack limited?" has to
        /// test BOTH, or a rack nobody set a balance on gets counted, deducted and
        /// reported as though an operator had typed a number into it. Named here so
        /// the places that used to hard-code the literal — StockEntryVm's default,
        /// the session DTO and its restore — share one definition.
        /// </summary>
        public const int EffectivelyUnlimitedSheets = 99999999;

        /// <summary>
        /// Returns a copy of this rack carrying a different Sheet Store balance.
        /// Every property here is <c>init</c>, so re-pricing a rack part-way
        /// through a run means making a new one rather than mutating the instance
        /// the packer is still holding.
        ///
        /// PHASE 9 / finding B2. Callers must never pass
        /// <see cref="UnlimitedSheets"/> to mean "exhausted" — 0 reads as
        /// INFINITE GLASS in all three packer strategies. A rack with nothing
        /// left has to be dropped from the pool, not zeroed.
        /// </summary>
        public StockSize WithMaxSheets(int maxSheets) => new StockSize
        {
            WidthMm = WidthMm,
            HeightMm = HeightMm,
            ThicknessMm = ThicknessMm,
            Color = Color,
            Label = Label,
            MaterialKey = MaterialKey,
            MaxSheets = maxSheets
        };

        public string SizeLabel => string.IsNullOrWhiteSpace(Label)
            ? $"{WidthMm.ToString(CultureInfo.InvariantCulture)} × {HeightMm.ToString(CultureInfo.InvariantCulture)}"
            : Label;
    }

    /// <summary>One piece to cut (already expanded from row quantities).</summary>
    public sealed class CutPiece
    {
        public string JobRef { get; init; } = "";
        public string GlassRef { get; init; } = "";
        public string SpecLabel { get; init; } = "";
        public string Position { get; init; } = "";
        public string Color { get; init; } = "";
        public int ThicknessMm { get; init; }
        public int WidthMm { get; init; }
        public int HeightMm { get; init; }
        public bool AllowRotate { get; init; } = true;

        /// <summary>Identity for the 2D view, e.g. "G-101 · 3/24".</summary>
        public string PieceLabel { get; init; } = "";

        // Canonical provenance — copied from the source row, never invented.
        public string ProformaId { get; init; } = "";
        public string JobOrderId { get; init; } = "";
        public int SpecGroupNumber { get; init; }
        public int SourceRowSerial { get; init; }
        public string GlassType { get; init; } = "";
        public string Processing { get; init; } = "";
        public string Coating { get; init; } = "";
        public int W1 { get; init; }
        public int H1 { get; init; }
        public int W2 { get; init; }
        public int H2 { get; init; }
        public string SheetStoreSku { get; init; } = "";
        public int SheetWidthMm { get; init; }
        public int SheetHeightMm { get; init; }
        public decimal SheetPricePerSqm { get; init; }
        public string MaterialKey { get; init; } = "";
        public int Qty { get; init; } = 1;
    }

    /// <summary>One placed piece on one sheet (integer mm coordinates).</summary>
    public sealed class PlacedPiece
    {
        public CutPiece Piece { get; init; } = null!;
        public int X { get; init; }
        public int Y { get; init; }
        public int WidthMm { get; init; }
        public int HeightMm { get; init; }
        public bool Rotated { get; init; }
    }

    /// <summary>One optimized sheet with its placements and metrics.</summary>
    public sealed class SheetLayout
    {
        public int SheetNo { get; set; }
        public StockSize Stock { get; init; } = null!;

        /// <summary>Usable rectangle inside the edge trims.</summary>
        public int UsableX { get; init; }
        public int UsableY { get; init; }
        public int UsableWidth { get; init; }
        public int UsableHeight { get; init; }

        public List<PlacedPiece> Pieces { get; } = new();

        public decimal UsedSqm =>
            System.Math.Round(Pieces.Sum(p => (decimal)p.WidthMm * p.HeightMm) / 1_000_000m,
                4, System.MidpointRounding.AwayFromZero);

        public decimal StockSqm =>
            System.Math.Round((decimal)Stock.WidthMm * Stock.HeightMm / 1_000_000m,
                4, System.MidpointRounding.AwayFromZero);

        /// <summary>Utilization % = placed glass area / full sheet area.</summary>
        public decimal UtilizationPct => StockSqm == 0 ? 0m :
            System.Math.Round(UsedSqm / StockSqm * 100m, 2, System.MidpointRounding.AwayFromZero);

        public string UtilizationLabel =>
            UtilizationPct.ToString("0.##", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>Whole-run result.</summary>
    /// <summary>Which scale band an input falls into. Decided purely by part
    /// count so that a given job always gets the same search budget and the
    /// engine stays deterministic.</summary>
    public enum ScaleTier
    {
        /// <summary>Full search. Everything the calibration benchmark covers
        /// lands here, so results stay bit-identical to earlier releases.</summary>
        Standard = 0,
        /// <summary>Search breadth trimmed; ordering and split diversity kept.</summary>
        Large = 1,
        /// <summary>Minimum viable search, chosen so the run terminates.</summary>
        Huge = 2
    }

    /// <summary>
    /// How much search the optimizer is allowed to perform for a given input
    /// size. The engine's cost grows roughly with parts x sheets, so a group of
    /// 100,000 parts is not 1,000 times a group of 100 — it is about a million
    /// times. Rather than run for hours or exhaust memory, large inputs trade
    /// search BREADTH (deterministic shuffles, beam widening) for termination,
    /// while keeping the ordering and split-rule diversity that produces most
    /// of the quality. Every reduction is recorded on the result so it is
    /// visible to the operator instead of silently degrading the plan.
    /// </summary>
    public sealed class ScaleProfile
    {
        public ScaleProfile(int pieceCount, int requestedEffort, ScaleTier tier,
            int shuffleCap, bool allowBeam, int maxRectsOrderings, int consolidationSweeps)
        {
            PieceCount = pieceCount;
            RequestedEffort = requestedEffort;
            Tier = tier;
            ShuffleCap = shuffleCap;
            AllowBeam = allowBeam;
            MaxRectsOrderings = maxRectsOrderings;
            ConsolidationSweeps = consolidationSweeps;
        }

        public int PieceCount { get; }
        public int RequestedEffort { get; }
        public ScaleTier Tier { get; }
        /// <summary>Maximum number of deterministic shuffles per ordering/split pair.</summary>
        public int ShuffleCap { get; }
        /// <summary>Beam search copies the whole open-sheet list per candidate.</summary>
        public bool AllowBeam { get; }
        /// <summary>How many orderings the MaxRects candidate may run.</summary>
        public int MaxRectsOrderings { get; }
        /// <summary>Consolidation sweeps cap.</summary>
        public int ConsolidationSweeps { get; }

        /// <summary>True when any search breadth was reduced because of size.</summary>
        public bool IsReduced => Tier != ScaleTier.Standard;

        /// <summary>Whether sheet-at-a-time quadratic strategies are allowed.</summary>
        public bool AllowQuadraticStrategies => Tier != ScaleTier.Huge;

        public static ScaleProfile For(int pieceCount, int requestedEffort)
        {
            var plan = OptimizationSearchGovernor.PlanForOptimize(pieceCount, requestedEffort);
            var tier = plan.Tier switch
            {
                SearchTier.Full => ScaleTier.Standard,
                SearchTier.Narrowed => ScaleTier.Large,
                _ => ScaleTier.Huge
            };

            return new ScaleProfile(
                pieceCount,
                requestedEffort,
                tier,
                plan.ShuffleCap,
                plan.Tier == SearchTier.Full,
                plan.MaxRectOrders,
                plan.MaxConsolidationSweeps);
        }

        /// <summary>Plain-language description of what was reduced and why.</summary>
        public string Summary
        {
            get
            {
                var plan = OptimizationSearchGovernor.PlanForOptimize(PieceCount, RequestedEffort);
                return !string.IsNullOrWhiteSpace(plan.Note) ? plan.Note : $"{PieceCount} parts: adaptive search at effort {RequestedEffort}.";
            }
        }
    }

    /// <summary>Progress checkpoint reported by the optimizer. Emitted between
    /// candidate passes, which is the natural granularity: each pass is one
    /// complete packing of the group.</summary>
    public sealed class OptimizeProgress
    {
        public string Stage { get; init; } = "";
        public int Completed { get; init; }
        public int Total { get; init; }
        public string Message { get; init; } = "";

        public double Fraction => Total <= 0
            ? 0d
            : System.Math.Min(1d, (double)Completed / Total);

        public static OptimizeProgress Of(string stage, int completed, int total, string message = "") =>
            new OptimizeProgress { Stage = stage, Completed = completed, Total = total, Message = message };
    }

    public sealed class OptimizationResult
    {
        public List<SheetLayout> Sheets { get; } = new();
        public List<CutPiece> Unplaced { get; } = new();
        public RotationPolicy Rotation { get; init; }

        /// <summary>Search reductions applied because of input size, in plain
        /// language. Always empty for standard-size groups, so existing reports
        /// and tests are unaffected.</summary>
        public List<string> ScaleNotes { get; } = new();
        /// <summary>Which strategy produced this plan. Empty for every legacy
        /// candidate, so nothing that reads a result today changes. It exists
        /// because a multi-start run evaluates up to 147 complete plans and
        /// keeps the best — without this the operator cannot tell whether a
        /// better number came from the placement policy or from luck.</summary>
        public string Strategy { get; set; } = "";

        // ── PHASE 4 / findings 7, 8, 9 (2026-09-20): the oracle verdict ──
        //
        // Two independent oracles exist in GuillotinePacker and they check
        // DIFFERENT things:
        //   Verify             — geometry: margins, rotation policy, dimension
        //                        integrity, overlap, kerf.
        //   ValidateResult     — CONSERVATION: every input piece placed exactly
        //                        once, nothing missing, nothing duplicated,
        //                        input count == placed + unplaced.
        // The run header used to label a group "Verified" from Verify alone, and
        // PackQuick never called ValidateResult at all — so a QUICK plan that
        // had LOST pieces displayed a green "Verified". The verdict now travels
        // with the result so every consumer (header, export, Step 6 feed) can
        // tell what was actually checked.

        /// <summary>Every problem any oracle reported against this plan.</summary>
        public List<string> ValidationProblems { get; } = new();

        /// <summary>
        /// True when conservation was CHECKED and passed. Distinct from
        /// <see cref="HasValidationFailure"/> so a caller can tell "verified"
        /// from "not checked".
        /// </summary>
        public bool ConservationVerified { get; set; }

        /// <summary>True when geometry was CHECKED and passed.</summary>
        public bool GeometryVerified { get; set; }

        /// <summary>True when the conservation oracle ran against this plan at
        /// all, regardless of outcome. Lets the run header distinguish
        /// "verified" from "failed" from "never checked" — the QUICK path used
        /// to be in the third state while displaying the first.</summary>
        public bool ConservationChecked { get; set; }

        /// <summary>True when the geometry oracle ran against this plan at all,
        /// regardless of outcome.</summary>
        public bool GeometryChecked { get; set; }

        /// <summary>
        /// True when any oracle reported a problem. Such a plan is still shown
        /// on screen so the operator can inspect what went wrong, but it is
        /// never exported to a cut file and never feeds Step 6 — a plan that
        /// fails geometry or conservation must not become production work.
        /// </summary>
        public bool HasValidationFailure => ValidationProblems.Count > 0;

        /// <summary>
        /// Records oracle output. Kept as one method so no call site can set the
        /// flag without also recording WHY.
        /// </summary>
        public void RecordValidation(IEnumerable<string> problems, bool conservationChecked, bool geometryChecked)
        {
            if (problems != null)
                foreach (var p in problems)
                    if (!string.IsNullOrWhiteSpace(p) && !ValidationProblems.Contains(p))
                        ValidationProblems.Add(p);

            if (conservationChecked) ConservationChecked = true;
            if (geometryChecked) GeometryChecked = true;

            ConservationVerified = ConservationChecked && !HasValidationFailure;
            GeometryVerified = GeometryChecked && !HasValidationFailure;
        }

        public int SheetsUsed => Sheets.Count;

        public decimal TotalUsedSqm =>
            System.Math.Round(Sheets.Sum(s => s.UsedSqm), 4, System.MidpointRounding.AwayFromZero);

        public decimal TotalStockSqm =>
            System.Math.Round(Sheets.Sum(s => s.StockSqm), 4, System.MidpointRounding.AwayFromZero);

        /// <summary>Weighted utilization across all sheets.</summary>
        public decimal UtilizationPct => TotalStockSqm == 0 ? 0m :
            System.Math.Round(TotalUsedSqm / TotalStockSqm * 100m, 2, System.MidpointRounding.AwayFromZero);

        public bool Success => Unplaced.Count == 0 && Sheets.Count > 0;
    }
}
