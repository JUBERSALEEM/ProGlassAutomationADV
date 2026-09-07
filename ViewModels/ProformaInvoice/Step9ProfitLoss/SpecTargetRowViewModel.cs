// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step9ProfitLoss/SpecTargetRowViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Step 9 — Target Profitability (per-specification rows + sidebar)
//
// Wraps the immutable SpecTargetPriceResult from the shared calculation
// layer into a bindable row for the Step 9 target tabs and the detail
// sidebar. All formatting lives here so the XAML stays declarative.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Linq;
using System.Text;
using ProGlassAutomation.Services.ProformaInvoice.TargetPricing;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss
{
    public sealed class SpecTargetRowViewModel : ViewModelBase
    {
        private SpecTargetPriceResult? _result;
        public SpecTargetPriceResult? Result
        {
            get => _result;
            private set
            {
                _result = value;
                OnPropertyChanged(string.Empty);
            }
        }

        /// <summary>Readable preview of the Step 5 dimension rows.</summary>
        public string DimensionsSummary { get; private set; } = "";

        /// <summary>True when this row is selected in the comparison list (drives the sidebar).</summary>
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public string RequiredTargetValueDisplay => Result != null
            ? $"AED {Result.RequiredTargetValue:N2}"
            : "—";

        public string DifferenceValueDisplay => Result?.DifferenceValue switch
        {
            null => "—",
            var d when d < 0m => $"-AED {Math.Abs(d.Value):N2}",
            var d => $"AED {d.Value:N2}"
        };

        public static SpecTargetRowViewModel FromResult(
            SpecTargetPriceResult result, SpecGroup? group = null)
        {
            return new SpecTargetRowViewModel
            {
                Result = result,
                DimensionsSummary = BuildDimensionsSummary(group)
            };
        }

        public bool HasResult => Result != null;

        // ── SECTION 1 — IDENTITY ─────────────────────────────────────────

        public int SpecNumber => Result?.SpecNumber ?? 0;
        public string SpecName => Result?.SpecName ?? "";
        public string SpecNumberDisplay => SpecNumber > 0 ? $"SPEC {SpecNumber}" : "INVOICE-WIDE";
        public string SpecDisplayName =>
            !string.IsNullOrWhiteSpace(SpecName)
                ? SpecName
                : SpecNumberDisplay;

        public string BuildTypeLabel => Result?.BuildType switch
        {
            GlassBuildType.Sgu => "SGU — single glazed",
            GlassBuildType.Dgu => "DGU — double glazed",
            GlassBuildType.Lam => "LAM — laminated",
            GlassBuildType.DguLam => "DGU + LAM hybrid",
            _ => "Unknown build"
        };

        // ── SECTION 2 — STEP 5 INPUT ────────────────────────────────────

        public string GivenPricePerSqmDisplay => FormatRate(Result?.GivenPricePerSqm);
        public string AreaSqmDisplay => Result != null
            ? $"{Result.AreaSqm:0.###} m²"
            : "—";
        public string GivenValueDisplay => Result != null
            ? $"AED {Result.GivenValue:N2}"
            : "—";
        public bool HasGivenPrice => (Result?.GivenPricePerSqm ?? 0m) > 0m;

        // ── SECTION 3 — TARGET FORMULA ───────────────────────────────────

        public string OuterGlassRateDisplay => FormatRate(Result?.OuterGlassRatePerSqm);
        public string InnerGlassRateDisplay => FormatRate(Result?.InnerGlassRatePerSqm);
        public string ThirdGlassRateDisplay => FormatRate(Result?.ThirdGlassRatePerSqm);
        public string AspRateDisplay => FormatRate(Result?.AspRatePerSqm);
        public string ProcessingRateDisplay => FormatRate(Result?.ProcessingRatePerSqm);

        /// <summary>True for builds with a second pane (DGU / LAM / DGU+LAM).</summary>
        public bool HasSecondGlass => Result?.BuildType is GlassBuildType.Dgu
            or GlassBuildType.Lam
            or GlassBuildType.DguLam;

        /// <summary>True for the three-pane DGU+LAM hybrid.</summary>
        public bool HasThirdGlass => Result?.BuildType == GlassBuildType.DguLam;

        /// <summary>True when the build carries an ASP/spacer rate (DGU / DGU+LAM).</summary>
        public bool HasAspRate =>
            (Result?.BuildType is GlassBuildType.Dgu or GlassBuildType.DguLam)
            && (Result?.AspRatePerSqm ?? 0m) > 0m;

        /// <summary>True when the build has formula processing add-ons (SGU / LAM / DGU+LAM).</summary>
        public bool HasProcessingRate =>
            (Result?.ProcessingRatePerSqm ?? 0m) > 0m;
        public string YieldFactorDisplay => Result != null
            ? $"{Result.YieldFactor:0.###}"
            : "—";
        public string TargetUpliftDisplay => Result != null
            ? $"{Result.TargetUpliftPercent:0.#}%"
            : "—";
        public string FormulaText => Result?.FormulaText ?? "—";
        public string SpecificationTargetDisplay => FormatRate(Result?.SpecificationTargetPerSqm);
        public bool HasSpecificationTarget => (Result?.SpecificationTargetPerSqm ?? 0m) > 0m;

        // ── SECTION 4 — U-INSERT ────────────────────────────────────────

        public string UInsertTreatmentLabel => Result?.UInsertTreatment switch
        {
            UInsertTreatment.Additional => "Additional — Step 6 extra",
            UInsertTreatment.IncludedInStep5Price => "Included in Step 5 price",
            UInsertTreatment.Unresolved => "Unresolved",
            _ => "Not applicable"
        };

        public string UInsertTreatmentDetail => Result?.UInsertTreatment switch
        {
            // Revised rule (2026-09-04): Step 6 row ACTIVE = billed
            // separately, so the required target stays the NORMAL
            // specification target (rate not added).
            UInsertTreatment.Additional =>
                "Step 6 bills this U-insert as its own charge on top of the given price — " +
                "the required target stays the normal specification target (rate not added).",
            // Step 6 row OFF = the given price already covers the U-insert,
            // so the target stays the base-price specification target; the
            // rate is shown for reference only (never added — adding it
            // would double-count the U-insert inside the given price).
            UInsertTreatment.IncludedInStep5Price =>
                "The U-insert is covered inside the Step 5 given price — the required target " +
                "stays the base-price specification target; the rate is shown, not added.",
            UInsertTreatment.Unresolved => "Cannot resolve — check the Step 6 U-insert row and its rate.",
            _ => "This specification carries no U-insert extra."
        };

        public string UInsertRateDisplay => FormatRate(Result?.UInsertRatePerSqm);
        public string UInsertSourceDisplay => Result?.UInsertSourceLabel ?? "—";
        public string AppliedUInsertDisplay => FormatRate(Result?.AppliedUInsertPerSqm);

        /// <summary>Sidebar caption for the U-insert value row.</summary>
        public string UInsertAppliedCaption => Result?.UInsertTreatment switch
        {
            UInsertTreatment.IncludedInStep5Price => "Rate shown, not added to target",
            UInsertTreatment.Additional => "Step 6 extra (excluded from target)",
            _ => "U-insert rate"
        };

        /// <summary>
        /// Sidebar value for the U-insert row. Under the revised rule
        /// (2026-09-04) the rate is NEVER added to the required target for
        /// either treatment — it is informational only.
        /// </summary>
        public string UInsertAppliedValueDisplay => Result?.UInsertTreatment switch
        {
            UInsertTreatment.IncludedInStep5Price => "In given price — not added to target",
            UInsertTreatment.Additional => "Not added — billed separately in Step 6",
            UInsertTreatment.Unresolved => "Unresolved — check Step 6",
            _ => "—"
        };

        // ── SECTION 5 — RESULT ──────────────────────────────────────────

        public string BasePriceDisplay => FormatRate(Result?.BasePricePerSqm);

        public string BaseDifferencePerSqmDisplay => Result?.ExtraProfitPerSqm switch
        {
            null => "—",
            var d when d < 0m => $"-AED {Math.Abs(d.Value):N2}/m²",
            var d => $"AED {d.Value:N2}/m²"
        };

        public string BaseDifferenceValueDisplay
        {
            get
            {
                if (Result?.ExtraProfitPerSqm == null || Result.AreaSqm <= 0m)
                    return "—";
                decimal value = Math.Round(Result.ExtraProfitPerSqm.Value * Result.AreaSqm, 2);
                return value < 0m
                    ? $"-AED {Math.Abs(value):N2}"
                    : $"AED {value:N2}";
            }
        }

        public string BaseMarginDisplay =>
            Result?.ExtraProfitMarginPercent switch
            {
                null => "—",
                var m => $"{m.Value:+0.##;-0.##;0.##}%"
            };

        public bool IsBelowBase => (Result?.ExtraProfitMarginPercent ?? 0m) < 0m;

        public string FinalRequiredTargetDisplay => FormatRate(Result?.FinalRequiredTargetPerSqm);
        public string DifferencePerSqmDisplay => Result?.DifferencePerSqm switch
        {
            null => "—",
            var d when d < 0m => $"-AED {Math.Abs(d.Value):N2}/m²",
            var d => $"AED {d.Value:N2}/m²"
        };

        public string MarginAgainstTargetDisplay =>
            Result?.MarginAgainstTargetPercent switch
            {
                null => "—",
                var m => $"{m.Value:0.##}%"
            };

        public string StatusLabel => Result?.Status switch
        {
            TargetStatus.UpToDate => "UP TO DATE",
            TargetStatus.BelowTarget => "BELOW TARGET",
            TargetStatus.AtTarget => "AT TARGET",
            TargetStatus.AboveTarget => "ABOVE TARGET",
            TargetStatus.MissingBasePrice => "MISSING BASE PRICE",
            TargetStatus.MissingGlassRate => "MISSING GLASS RATE",
            TargetStatus.MissingAspRate => "MISSING ASP RATE",
            TargetStatus.MissingYield => "MISSING YIELD",
            TargetStatus.MissingUInsertTreatment => "MISSING U-INSERT TREATMENT",
            TargetStatus.CalculationIncomplete => "CALCULATION INCOMPLETE",
            TargetStatus.SnapshotStale => "SNAPSHOT STALE",
            _ => "—"
        };

        public string WarningSummary =>
            Result?.Warnings?.FirstOrDefault() ?? "";

        public bool HasWarnings => (Result?.Warnings?.Count ?? 0) > 0;
        public bool HasValidMargin => Result?.HasValidMargin ?? false;
        public bool IsBelowTarget => Result?.Status == TargetStatus.BelowTarget;
        public bool IsAboveTarget => Result?.Status == TargetStatus.AboveTarget;
        public bool IsAtTarget => Result?.Status == TargetStatus.AtTarget;
        public bool IsIncomplete =>
            Result?.Status is TargetStatus.MissingBasePrice
                or TargetStatus.MissingGlassRate
                or TargetStatus.MissingAspRate
                or TargetStatus.MissingYield
                or TargetStatus.MissingUInsertTreatment
                or TargetStatus.CalculationIncomplete
                or TargetStatus.SnapshotStale;

        private static string BuildDimensionsSummary(SpecGroup? group)
        {
            if (group?.ItemRows == null || group.ItemRows.Count == 0)
                return "No dimension rows yet.";

            const int maxLines = 8;
            var sb = new StringBuilder();
            int lines = 0;

            foreach (var row in group.ItemRows.Where(r => r != null))
            {
                if (row.W1 <= 0 || row.H1 <= 0) continue;
                if (lines >= maxLines) break;

                string label = !string.IsNullOrWhiteSpace(row.GlassRef)
                    ? row.GlassRef
                    : "Row";

                if (row.W2 > 0 && row.H2 > 0)
                {
                    sb.AppendLine($"{label} {row.W1}×{row.H1} + {row.W2}×{row.H2}  × {row.Qty}");
                }
                else
                {
                    sb.AppendLine($"{label} {row.W1}×{row.H1}  × {row.Qty}");
                }

                lines++;
            }

            if (sb.Length == 0)
                return "No dimension rows with sizes yet.";

            int total = group.ItemRows.Count(r => r != null);
            if (total > lines)
                sb.Append($"+{total - lines} more row(s)");

            return sb.ToString().TrimEnd('\r', '\n');
        }

        private static string FormatRate(decimal? value)
        {
            return value switch
            {
                null => "—",
                var v when v.Value <= 0m => "—",
                var v => $"AED {v.Value:N2}/m²"
            };
        }
    }
}
