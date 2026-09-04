// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.TargetPricing.cs
// PROJECT: ProGlassAutomation
// MODULE: Step 9 — Target Profitability (approved KPI model)
//
// This partial owns the target-price model built on the shared
// SpecificationTargetCalculator. It is kept STRICTLY separate from the
// actual-cost rows (ProfitLossStepViewModel.Calculation.cs): the target
// model answers "is the Step 5 given price above the required target?",
// the actual rows answer "what does the job really cost?".
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.Services.ProformaInvoice.TargetPricing;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss
{
    public partial class ProfitLossStepViewModel : ViewModelBase
    {
        /// <summary>Version of the target-price calculation model.</summary>
        public const string TargetCalculationVersion = "2.0";

        // ══════════════════════════════════════════════════════════════
        //  OUTPUTS — TARGET PROFITABILITY
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// One row per specification group with its target-price result.
        /// Drives Tab 4 (Glass / Specification Comparison) and the detail
        /// sidebar on Tab 1.
        /// </summary>
        public ObservableCollection<SpecTargetRowViewModel> TargetSpecsView { get; } = new();

        /// <summary>True once at least one specification has a target result.</summary>
        public bool HasTargetSpecs => TargetSpecsView.Count > 0;

        /// <summary>
        /// Selects a specification row (Tab 4) and opens its six-section
        /// detail sidebar.
        /// </summary>
        public ICommand SelectTargetSpecCommand { get; }

        private void SetSelectedTargetSpec(object? parameter)
        {
            var row = parameter as SpecTargetRowViewModel;
            foreach (var r in TargetSpecsView)
                r.IsSelected = ReferenceEquals(r, row);

            SelectedTargetSpec = row;
        }

        /// <summary>Raw job-level aggregate of the last recalculation.</summary>
        public TargetPriceResult? JobTargetResult { get; private set; }

        public bool HasJobTarget => JobTargetResult != null;

        // ── EXPECTED PROFIT MARGIN (middle KPI card, approved uplift) ──
        // The target formula multiplies the pre-uplift specification result
        // by (1 + margin/100). The margin is the company default captured in
        // Settings → Features → Proforma Invoice → Step 9 ("Profit margin %",
        // default 15 → ×1.15). Drafts and invoices saved before the setting
        // existed auto-adapt to the CURRENT global default (always live);
        // SetExpectedProfitMarginOverride can pin a per-invoice value when a
        // saved snapshot is re-hydrated.

        private decimal? _expectedProfitMarginPercent;

        /// <summary>True when this invoice carries its own captured margin
        /// instead of following the global default live.</summary>
        public bool HasExpectedProfitMarginOverride => _expectedProfitMarginPercent.HasValue;

        /// <summary>Effective expected profit margin (%) for this invoice.</summary>
        public decimal ExpectedProfitMarginPercent =>
            _expectedProfitMarginPercent
            ?? Services.Features.Step9PricingDefaults.ExpectedProfitMarginPercent;

        /// <summary>Middle KPI card value, e.g. "15%".</summary>
        public string ExpectedProfitMarginDisplay => $"{ExpectedProfitMarginPercent:0.#}%";

        /// <summary>Factor label, e.g. "×1.15".</summary>
        public string ExpectedProfitMarginFactorLabel =>
            $"×{1m + ExpectedProfitMarginPercent / 100m:0.##}";

        /// <summary>Where the middle-card value comes from.</summary>
        public string ExpectedProfitMarginSourceLabel => HasExpectedProfitMarginOverride
            ? "Captured for this invoice"
            : "Company default · Settings → Features → Proforma Invoice → Step 9";

        /// <summary>Pins an invoice-specific expected margin (e.g. the value
        /// captured when the invoice was created/saved).</summary>
        public void SetExpectedProfitMarginOverride(decimal percent)
        {
            percent = Services.Features.Step9PricingDefaults.ClampPercent(percent);
            if (_expectedProfitMarginPercent == percent) return;
            _expectedProfitMarginPercent = percent;
            OnPropertyChanged(nameof(ExpectedProfitMarginPercent));
            OnPropertyChanged(nameof(ExpectedProfitMarginDisplay));
            OnPropertyChanged(nameof(ExpectedProfitMarginFactorLabel));
            NotifyTargetOutputsChanged();
        }

        /// <summary>Called when the Settings default changes so an unpinned
        /// invoice follows the new global value live.</summary>
        private void OnExpectedProfitMarginDefaultChanged()
        {
            if (HasExpectedProfitMarginOverride) return;

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                RaiseExpectedProfitMarginChangedAndRecalc();
            else
                dispatcher.BeginInvoke(new Action(RaiseExpectedProfitMarginChangedAndRecalc));
        }

        private void RaiseExpectedProfitMarginChangedAndRecalc()
        {
            OnPropertyChanged(nameof(ExpectedProfitMarginPercent));
            OnPropertyChanged(nameof(ExpectedProfitMarginDisplay));
            OnPropertyChanged(nameof(ExpectedProfitMarginFactorLabel));
            QueueRecalculate();
        }

        /// <summary>
        /// When the user selects a specification on the target tab the
        /// sidebar binds to this row. Null until a selection is made.
        /// </summary>
        private SpecTargetRowViewModel? _selectedTargetSpec;
        public SpecTargetRowViewModel? SelectedTargetSpec
        {
            get => _selectedTargetSpec;
            set
            {
                if (ReferenceEquals(_selectedTargetSpec, value)) return;
                _selectedTargetSpec = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedTargetSpec));
            }
        }

        public bool HasSelectedTargetSpec => SelectedTargetSpec != null;

        // ── KPI card 1 — REQUIRED TARGET PRICE ──────────────────────────

        public string RequiredTargetPriceDisplay => FormatRateCard(JobTargetResult?.AverageFinalRequiredTargetPerSqm);
        public string SpecificationTargetAverageDisplay => FormatRateCard(JobTargetResult?.AverageSpecificationTargetPerSqm);

        /// <summary>
        /// "Included in price · rate folded in (AED x/m²)" / "Additional · Step 6 extra" /
        /// "Not applicable" / "Mixed" / "Unresolved".
        ///
        /// Approved rule: Additional (Step 6 row active) = the extra is billed
        /// separately and stays OUT of the target; IncludedInStep5Price
        /// (toggle off) = the rate is folded INTO the final required target.
        /// </summary>
        public string UInsertSummaryDisplay
        {
            get
            {
                var specs = JobTargetResult?.Specifications?.ToList() ?? new List<SpecTargetPriceResult>();
                if (specs.Count == 0) return "—";

                bool anyAdditional = specs.Any(s => s.UInsertTreatment == UInsertTreatment.Additional);
                bool anyIncluded = specs.Any(s => s.UInsertTreatment == UInsertTreatment.IncludedInStep5Price);
                bool anyUnresolved = specs.Any(s => s.UInsertTreatment == UInsertTreatment.Unresolved);
                bool anyApplicable = specs.Any(s => s.UInsertTreatment == UInsertTreatment.NotApplicable);

                if (anyUnresolved && specs.Count(s =>
                        s.UInsertTreatment != UInsertTreatment.Unresolved) == 0)
                    return "Unresolved — check Step 6";

                if (anyAdditional && anyIncluded)
                    return "Mixed — some specs bill U-insert separately, some fold it into price";

                if (anyAdditional)
                    return "Additional — Step 6 extra billed on top (not in target)";

                decimal applied = JobTargetResult?.AverageAppliedUInsertPerSqm ?? 0m;
                if (anyIncluded)
                {
                    return applied > 0m
                        ? $"Included in price — rate folded into target (AED {applied:N2}/m²)"
                        : "Included in price — U-insert covered by the given price";
                }

                if (anyApplicable || specs.Count == 0)
                    return "Not applicable";

                return "Unresolved — check Step 6";
            }
        }

        public bool HasUInsertSummaryWarning =>
            (JobTargetResult?.Specifications?.Any(s => s.UInsertTreatment == UInsertTreatment.Unresolved)
             ?? false);

        // ── KPI card 2 — MARGIN AGAINST TARGET ──────────────────────────

        public string MarginAgainstTargetDisplay =>
            JobTargetResult?.MarginAgainstTargetPercent switch
            {
                null => "—",
                var m => $"{m.Value:0.##}%"
            };

        public string GivenPricePerSqmDisplay => FormatRateCard(JobTargetResult?.AverageGivenPricePerSqm);

        public string DifferencePerSqmDisplay
        {
            get
            {
                if (JobTargetResult == null) return "—";
                decimal diff = (JobTargetResult.AverageGivenPricePerSqm
                                - JobTargetResult.AverageFinalRequiredTargetPerSqm);
                return diff < 0m
                    ? $"-AED {Math.Abs(diff):N2}/m²"
                    : $"AED {diff:N2}/m²";
            }
        }

        public string TargetJobStatusLabel => TargetStatusLabel(JobTargetResult?.Status);

        public bool IsTargetBelow => JobTargetResult?.Status == TargetStatus.BelowTarget;
        public bool IsTargetAbove => JobTargetResult?.Status == TargetStatus.AboveTarget;
        public bool IsTargetIncomplete =>
            JobTargetResult?.Status is TargetStatus.MissingBasePrice
                or TargetStatus.MissingGlassRate
                or TargetStatus.MissingAspRate
                or TargetStatus.MissingYield
                or TargetStatus.MissingUInsertTreatment
                or TargetStatus.CalculationIncomplete
                or TargetStatus.SnapshotStale;

        // ── Calculation summary (price source + formula detail) ─────────

        public string GivenPriceSourceLabel => Step5PriceSource.SourceLabel;

        /// <summary>Formula text of the FIRST complete spec (single-spec jobs show the exact formula).</summary>
        public string TargetFormulaPreview =>
            (JobTargetResult?.Specifications?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.FormulaText))
             ?? JobTargetResult?.Specifications?.FirstOrDefault())
            ?.FormulaText ?? "—";

        public string TargetFormulaSpecLabel =>
            (JobTargetResult?.Specifications?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.FormulaText)))
            ?.SpecNumber.ToString() is string n
                ? $"SPEC {n}"
                : "";

        /// <summary>"N specification group(s)" when more than one spec is present.</summary>
        public string TargetScopeNote
        {
            get
            {
                int count = JobTargetResult?.Specifications?.Count ?? 0;
                return count switch
                {
                    0 => "No specification selected — add glass in Step 5.",
                    1 => "1 specification",
                    _ => $"{count} specifications · area-weighted averages"
                };
            }
        }

        // ── Value totals (compact fields under the cards) ───────────────

        public string TotalGivenValueDisplay => JobTargetResult != null
            ? $"AED {JobTargetResult.TotalGivenValue:N2}"
            : "—";

        public string TotalRequiredTargetValueDisplay => JobTargetResult != null
            ? $"AED {JobTargetResult.TotalRequiredTargetValue:N2}"
            : "—";

        public string TotalDifferenceValueDisplay
        {
            get
            {
                if (JobTargetResult == null) return "—";
                return JobTargetResult.TotalDifferenceValue < 0m
                    ? $"-AED {Math.Abs(JobTargetResult.TotalDifferenceValue):N2}"
                    : $"AED {JobTargetResult.TotalDifferenceValue:N2}";
            }
        }

        // ── Calculation state (auto-sync) ────────────────────────────────

        private DateTime? _lastCalculatedAt;
        public DateTime? LastCalculatedAt
        {
            get => _lastCalculatedAt;
            private set
            {
                _lastCalculatedAt = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastCalculatedAtDisplay));
            }
        }

        public string LastCalculatedAtDisplay =>
            LastCalculatedAt switch
            {
                null => "Never calculated",
                var d => $"Calculated {d.Value:dd MMM yyyy, HH:mm:ss}"
            };

        private List<string> _calculationWarnings = new();
        public IReadOnlyList<string> CalculationWarnings => _calculationWarnings;
        public bool HasCalculationWarnings => _calculationWarnings.Count > 0;

        /// <summary>Warning line shown under the KPI cards when relevant.</summary>
        public string CalculationWarningSummary =>
            _calculationWarnings.FirstOrDefault() ?? "";

        // ══════════════════════════════════════════════════════════════
        //  BUILD THE TARGET RESULTS (called from RecalculateAsync)
        // ══════════════════════════════════════════════════════════════

        private void RebuildTargetResults()
        {
            var warnings = new List<string>();
            var specs = new List<(SpecTargetPriceResult Result, SpecGroup Group)>();

            foreach (var group in _dataGrid?.SpecGroups ?? Enumerable.Empty<SpecGroup>())
            {
                if (group == null) continue;

                var (cutSqm, _) = PnLMeasurement.MeasureGroup(group);
                if (cutSqm <= 0m) continue;

                string specName = group.SelectedSpecName ?? "";
                var (buildType, glassTypeCode) = DetectBuild(group);

                var card = BuildRateCard(group, buildType);
                decimal given = group.BaseRate;

                var spec = SpecificationTargetCalculator.Calculate(
                    group.GroupNumber,
                    specName,
                    glassTypeCode,
                    buildType,
                    card,
                    given,
                    cutSqm);

                var resolution = ResolveUInsert(group, specName, buildType);
                SpecificationTargetCalculator.ApplyUInsert(spec, resolution);

                if (spec.Warnings.Count > 0)
                    warnings.AddRange(spec.Warnings);

                specs.Add((spec, group));
            }

            var aggregate = TargetProfitabilityAggregator.AggregateJob(
                specs.Select(t => t.Result));
            JobTargetResult = aggregate;

            foreach (var w in aggregate.Warnings)
            {
                if (!warnings.Contains(w)) warnings.Add(w);
            }

            _calculationWarnings = warnings.Distinct(StringComparer.Ordinal).ToList();
            OnPropertyChanged(nameof(CalculationWarnings));
            OnPropertyChanged(nameof(HasCalculationWarnings));
            OnPropertyChanged(nameof(CalculationWarningSummary));

            // Publish rows in spec order.
            TargetSpecsView.Clear();
            foreach (var (spec, group) in specs)
                TargetSpecsView.Add(SpecTargetRowViewModel.FromResult(spec, group));

            // Preserve the sidebar selection when it still exists.
            SpecTargetRowViewModel? keep = null;
            if (SelectedTargetSpec != null)
            {
                keep = TargetSpecsView.FirstOrDefault(r =>
                    r.SpecNumber == SelectedTargetSpec.SpecNumber
                    && string.Equals(r.SpecName, SelectedTargetSpec.SpecName, StringComparison.Ordinal));
            }
            else if (TargetSpecsView.Count > 0)
            {
                keep = TargetSpecsView[0];
            }

            foreach (var r in TargetSpecsView)
                r.IsSelected = ReferenceEquals(r, keep);

            SelectedTargetSpec = keep;

            OnPropertyChanged(nameof(HasTargetSpecs));
            OnPropertyChanged(nameof(HasJobTarget));
            NotifyTargetOutputsChanged();

            // Track the most specific aggregate status: when any spec is
            // incomplete the job cannot show a real margin.
            LastCalculatedAt = DateTime.Now;
            OnPropertyChanged(nameof(CalculationStateLabel));
            OnPropertyChanged(nameof(CalculationStatus));
        }

        private void NotifyTargetOutputsChanged()
        {
            OnPropertyChanged(nameof(ExpectedProfitMarginPercent));
            OnPropertyChanged(nameof(ExpectedProfitMarginDisplay));
            OnPropertyChanged(nameof(ExpectedProfitMarginFactorLabel));
            OnPropertyChanged(nameof(ExpectedProfitMarginSourceLabel));
            OnPropertyChanged(nameof(RequiredTargetPriceDisplay));
            OnPropertyChanged(nameof(SpecificationTargetAverageDisplay));
            OnPropertyChanged(nameof(UInsertSummaryDisplay));
            OnPropertyChanged(nameof(HasUInsertSummaryWarning));
            OnPropertyChanged(nameof(MarginAgainstTargetDisplay));
            OnPropertyChanged(nameof(GivenPricePerSqmDisplay));
            OnPropertyChanged(nameof(DifferencePerSqmDisplay));
            OnPropertyChanged(nameof(TargetJobStatusLabel));
            OnPropertyChanged(nameof(IsTargetBelow));
            OnPropertyChanged(nameof(IsTargetAbove));
            OnPropertyChanged(nameof(IsTargetIncomplete));
            OnPropertyChanged(nameof(TargetFormulaPreview));
            OnPropertyChanged(nameof(TargetFormulaSpecLabel));
            OnPropertyChanged(nameof(TargetScopeNote));
            OnPropertyChanged(nameof(TotalGivenValueDisplay));
            OnPropertyChanged(nameof(TotalRequiredTargetValueDisplay));
            OnPropertyChanged(nameof(TotalDifferenceValueDisplay));
            OnPropertyChanged(nameof(CalculationVersionLabel));
        }

        public string CalculationVersionLabel => $"Target model v{TargetCalculationVersion}";

        // ── Adapters from the Step 5 / Step 4 / Step 6 objects ──────────

        private static (GlassBuildType BuildType, string GlassTypeCode) DetectBuild(SpecGroup group)
        {
            if (group == null) return (GlassBuildType.Unknown, "");

            string specName = group.SelectedSpecName ?? "";
            string rawType = group.SelectedGlassType ?? "";
            string type = rawType.Trim().ToUpperInvariant();

            var spacer = PnLSpecParser.Spacer(specName);
            var interlayer = PnLSpecParser.Interlayer(specName);
            bool spacerDetected = spacer.Success;
            bool pvbDetected = interlayer.Success;

            if (type == "DGU+LAM" || (spacerDetected && pvbDetected))
                return (GlassBuildType.DguLam, "DGU+LAM");

            if (type.Contains("DGU") || spacerDetected)
                return (GlassBuildType.Dgu, "DGU");

            if (type.Contains("LAM") || pvbDetected)
                return (GlassBuildType.Lam, "LAM");

            if (type.Length == 0 || type.Contains("SGU") || type.Contains("SINGLE"))
                return (GlassBuildType.Sgu, "SGU");

            return (GlassBuildType.Unknown, rawType);
        }

        /// <summary>
        /// The yield factor used by the specification formula
        /// ((glass ÷ yield) + add-ons) × (1 + margin/100).
        ///
        /// APPROVED LINKAGE (2026-09): the yield IS the cutting-wastage
        /// allowance — 15% wastage = ÷0.85, 10% = ÷0.90, 25% = ÷0.75.
        /// It is therefore derived from the Step 9 WASTAGE % box
        /// (yield = 1 − wastage/100) rather than read from the Step 4
        /// Yield rows, so the formula the operator watches on Step 9
        /// moves the moment they edit the box. Step 4's own Yield rows
        /// keep driving the Step 4 sell-rate preview and are untouched
        /// by this rule.
        /// </summary>
        private decimal EffectiveYieldFactor()
        {
            // WastagePercent is clamped to 0–95 by its setter, so the
            // yield stays within (0.05 … 1.00]; the >0 guard in the
            // shared calculator therefore never trips for a valid box.
            decimal yield = 1m - (WastagePercent / 100m);
            return yield <= 0m ? 0.05m : yield;
        }

        /// <summary>
        /// Builds the rate card from the bound Step 4 panel of the spec's own
        /// build type (each group is scored against its own glass type, the
        /// same discipline QuoteBasisFor uses for the actual-cost model).
        /// Yield comes from the Step 9 WASTAGE % box (see
        /// <see cref="EffectiveYieldFactor"/>); the uplift comes from the
        /// expected profit margin (Settings default, live for unpinned
        /// invoices — see <see cref="ExpectedProfitMarginPercent"/>).
        /// </summary>
        private SpecificationRateCard? BuildRateCard(SpecGroup group, GlassBuildType buildType)
        {
            if (group == null) return null;

            decimal yield = EffectiveYieldFactor();
            decimal upliftPercent = ExpectedProfitMarginPercent;

            switch (buildType)
            {
                case GlassBuildType.Dgu:
                    if (_dguVm == null) return null;
                    return new SpecificationRateCard
                    {
                        BuildType = GlassBuildType.Dgu,
                        OuterGlassRatePerSqm = _dguVm.OuterBasePrice,
                        InnerGlassRatePerSqm = _dguVm.InnerBasePrice,
                        AspRatePerSqm = _dguVm.SpacerPrice,
                        ProcessingRatePerSqm = 0m,
                        YieldFactor = yield,
                        TargetUpliftPercent = upliftPercent
                    };

                case GlassBuildType.Sgu:
                    if (_sguVm == null) return null;
                    return new SpecificationRateCard
                    {
                        BuildType = GlassBuildType.Sgu,
                        OuterGlassRatePerSqm = _sguVm.BasePricePerSqm,
                        InnerGlassRatePerSqm = 0m,
                        AspRatePerSqm = 0m,
                        ProcessingRatePerSqm = _sguVm.CuttingCostPerSqm + _sguVm.TemperingCostPerSqm,
                        YieldFactor = yield,
                        TargetUpliftPercent = upliftPercent
                    };

                case GlassBuildType.Lam:
                    if (_lamVm == null) return null;
                    return new SpecificationRateCard
                    {
                        BuildType = GlassBuildType.Lam,
                        OuterGlassRatePerSqm = _lamVm.OuterBasePrice,
                        InnerGlassRatePerSqm = _lamVm.InnerBasePrice,
                        AspRatePerSqm = 0m,
                        ProcessingRatePerSqm = _lamVm.PvbMaterialRate
                                               + _lamVm.OuterCuttingCost + _lamVm.InnerCuttingCost
                                               + _lamVm.OuterTemperingCost + _lamVm.InnerTemperingCost,
                        YieldFactor = yield,
                        TargetUpliftPercent = upliftPercent
                    };

                case GlassBuildType.DguLam:
                    if (_dguLamVm == null) return null;
                    return new SpecificationRateCard
                    {
                        BuildType = GlassBuildType.DguLam,
                        OuterGlassRatePerSqm = _dguLamVm.OuterBasePrice,
                        InnerGlassRatePerSqm = _dguLamVm.LamInnerBasePrice,
                        ThirdGlassRatePerSqm = _dguLamVm.LamOuterBasePrice,
                        AspRatePerSqm = _dguLamVm.SpacerPrice,
                        ProcessingRatePerSqm = _dguLamVm.TotalCuttingCost
                                               + _dguLamVm.TotalTemperingCost
                                               + _dguLamVm.InterlayerSellPrice,
                        YieldFactor = yield,
                        TargetUpliftPercent = upliftPercent
                    };

                default:
                    return null;
            }
        }

        /// <summary>
        /// Resolves the U-insert treatment + dynamic rate for one spec group
        /// via the dedicated Step6ChargeResolver. The automation toggle is
        /// never used as the accounting treatment.
        /// </summary>
        private UInsertResolution ResolveUInsert(
            SpecGroup group, string specName, GlassBuildType buildType)
        {
            if (group == null)
                return new UInsertResolution { Treatment = UInsertTreatment.Unresolved };

            var query = new Step6UInsertQuery
            {
                SpecRequiresUInsert = SpecRequiresUInsert(group, specName, buildType),
                Step4SeedRate = group.UInsertPricePerSqm,
                RowExists = FindUInsertRow() != null,
                RowActiveForSpec = UInsertRowActiveForSpec(group),
                RowRate = FindUInsertRow()?.Rate ?? 0m,
                TemplateDefaultRate = PredefinedChargeTemplates.GetAll()
                    .FirstOrDefault(t => string.Equals(t.Key, "U_INSERT", StringComparison.OrdinalIgnoreCase))
                    ?.DefaultRate ?? 0m
            };

            return Step6ChargeResolver.ResolveUInsert(query);
        }

        private bool SpecRequiresUInsert(
            SpecGroup group, string specName, GlassBuildType buildType)
        {
            if (group.UInsertPricePerSqm > 0m) return true;

            string evidence = $"{specName ?? ""} {group.SelectedGlassType ?? ""}";
            if (evidence.IndexOf("U-Insert", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (_otherChargesVm == null) return false;

            try
            {
                bool matchedSameGlass = LaminateGlassMatcher.IsMatchedSameGlass(
                    group.SelectedSpecName,
                    group.SelectedGlassType,
                    group.OuterGlassColor,
                    group.InnerGlassColor);

                return OtherChargeAutomationRules.GetChargeSuggestions(
                        group.SelectedGlassType,
                        group.SelectedSpecName,
                        matchedSameGlass)
                    .Any(s => string.Equals(s.TemplateKey, "U_INSERT", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private Models.ProformaInvoice.Step6OtherCharges.OtherChargeRow? FindUInsertRow()
        {
            return _otherChargesVm?.ChargeRows?.FirstOrDefault(r =>
                string.Equals(r.TemplateKey, "U_INSERT", StringComparison.OrdinalIgnoreCase));
        }

        private bool UInsertRowActiveForSpec(SpecGroup group)
        {
            var row = FindUInsertRow();
            if (row == null || !row.IsActive || row.TargetSelection == null) return false;

            return row.TargetSelection.AvailableSpecs.Any(s =>
                s != null && s.IsChecked && s.SpecGroupNumber == group.GroupNumber);
        }

        private static string TargetStatusLabel(TargetStatus? status)
        {
            return status switch
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
        }

        private static string FormatRateCard(decimal? value)
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
