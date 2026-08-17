using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator
{
    /// <summary>
    /// Represents a glass structure type option (SGU / DGU / LAM / DGU+LAM).
    /// </summary>
    public class GlassTypeCard : ViewModelBase
    {
        public string Code { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Icon { get; set; } = "";
        public string AccentColor { get; set; } = "";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
    }

    /// <summary>
    /// Represents a specification target group (visual preview only —
    /// diverges from Step 5's real SpecGroups by design).
    ///
    /// BUG-43: This is a Step-4-only visual collection for the "Apply Rate
    ///         to Specs" picker. It does NOT sync with Step5VM.SpecGroups.
    ///         Users pick which specs the calculated rate applies to;
    ///         those specs are then updated in the Step 5 grid separately.
    /// </summary>
    public class SpecTargetGroup : ViewModelBase
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
    }

    /// <summary>
    /// ViewModel for Step 4 of the Proforma Invoice wizard.
    /// Smart Pricing Estimator: glass type selection, per-glass-type
    /// parameter panels, processing treatments, spec target groups,
    /// and live rate calculation.
    /// APPLY button saves the current spec to specifications.db.
    ///
    /// BUG-40/41 [HIGH]: Invalid yield factor no longer silently reset —
    ///                    warning message shown via YieldFactorWarning.
    /// BUG-42 [HIGH]: DGU+LAM hybrid returns "" for spec name so save
    ///                is blocked instead of writing "not yet implemented"
    ///                garbage to the DB. Save command disabled via
    ///                CanExecute check.
    /// BUG-43 [MEDIUM]: SpecGroups documented — divergent from Step5 by design.
    /// BUG-44 [MEDIUM]: Save success shows one-line status instead of blocking
    ///                  MessageBox (matches DN pattern — non-blocking UX).
    /// BUG-45 [LOW]: Property-changed switches consolidated — recalculates
    ///               whenever the active panel changes any tracked property.
    /// </summary>
    public class PricingEstimatorViewModel : ViewModelBase
    {
        // === Panel ViewModels (one per glass type) ===
        public SguParameters.SguParametersViewModel SguVM { get; }
        public DguParameters.DguParametersViewModel DguVM { get; }
        public LamParameters.LamParametersViewModel LamVM { get; }
        public DguLamParameters.DguLamParametersViewModel DguLamVM { get; }

        // === Glass Type Cards ===
        public ObservableCollection<GlassTypeCard> GlassTypes { get; }

        private GlassTypeCard? _selectedGlassType;
        public GlassTypeCard? SelectedGlassType
        {
            get => _selectedGlassType;
            set
            {
                if (_selectedGlassType == value) return;

                if (_selectedGlassType != null)
                    _selectedGlassType.IsSelected = false;

                _selectedGlassType = value;

                if (_selectedGlassType != null)
                    _selectedGlassType.IsSelected = true;

                OnPropertyChanged();

                // Recalculate rate when glass type changes
                RecalculateEstimatedRate();
            }
        }

        // === Processing Treatments (shared) ===

        private bool _polishedEdges;
        public bool PolishedEdges
        {
            get => _polishedEdges;
            set { _polishedEdges = value; OnPropertyChanged(); }
        }

        private bool _drillCutouts;
        public bool DrillCutouts
        {
            get => _drillCutouts;
            set { _drillCutouts = value; OnPropertyChanged(); }
        }

        private bool _countersunkHoles;
        public bool CountersunkHoles
        {
            get => _countersunkHoles;
            set { _countersunkHoles = value; OnPropertyChanged(); }
        }

        private bool _edgeDeletionLayer;
        public bool EdgeDeletionLayer
        {
            get => _edgeDeletionLayer;
            set { _edgeDeletionLayer = value; OnPropertyChanged(); }
        }

        // === Specification Target Groups (Step 4 preview only — see BUG-43) ===
        public ObservableCollection<SpecTargetGroup> SpecGroups { get; }

        public int TotalSpecsActive => SpecGroups?.Count(g => g.IsSelected) ?? 0;

        // === Estimated Rate (calculated live) ===

        private decimal _estimatedRatePerSqm;
        public decimal EstimatedRatePerSqm
        {
            get => _estimatedRatePerSqm;
            set
            {
                _estimatedRatePerSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCalculatedRate));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool HasCalculatedRate => EstimatedRatePerSqm > 0m;

        // BUG-40/41: warning message when yield factor is invalid
        private string _yieldFactorWarning = "";
        public string YieldFactorWarning
        {
            get => _yieldFactorWarning;
            private set
            {
                if (_yieldFactorWarning == value) return;
                _yieldFactorWarning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasYieldFactorWarning));
            }
        }
        public bool HasYieldFactorWarning => !string.IsNullOrEmpty(_yieldFactorWarning);

        // BUG-44: non-blocking status line for save success/failure
        // R-2: re-entry guard for ApplyRateAsync. ApplyRateCommand's
        // canExecute checks spec validity only, and the async lambda is
        // async void, so Execute() returns at the first await and a
        // second click would run SaveOrUpdateAsync concurrently
        // (double-counting UsageCount). Plain bool, not Interlocked:
        // commands are raised on the UI thread only.
        private bool _isApplyingRate;

        private string _applyStatusMessage = "";
        public string ApplyStatusMessage
        {
            get => _applyStatusMessage;
            private set
            {
                if (_applyStatusMessage == value) return;
                _applyStatusMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasApplyStatus));
            }
        }
        public bool HasApplyStatus => !string.IsNullOrEmpty(_applyStatusMessage);

        // === Commands ===
        public ICommand SelectGlassTypeCommand { get; }
        public ICommand ToggleSpecGroupCommand { get; }
        public ICommand ApplyRateCommand { get; }

        public PricingEstimatorViewModel()
        {
            // Initialize panel VMs
            SguVM = new SguParameters.SguParametersViewModel();
            DguVM = new DguParameters.DguParametersViewModel();
            LamVM = new LamParameters.LamParametersViewModel();
            DguLamVM = new DguLamParameters.DguLamParametersViewModel();

            // BUG-45: blanket recalc when the active glass type panel
            // changes ANY property — no more brittle property-name switches
            SguVM.PropertyChanged += OnAnyPanelPropertyChanged;
            DguVM.PropertyChanged += OnAnyPanelPropertyChanged;
            LamVM.PropertyChanged += OnAnyPanelPropertyChanged;
            DguLamVM.PropertyChanged += OnAnyPanelPropertyChanged;

            // Seed 4 glass type options
            GlassTypes = new ObservableCollection<GlassTypeCard>
            {
                new GlassTypeCard
                {
                    Code        = "SGU",
                    Title       = "Single Glass Unit (SGU)",
                    Description = "Monolithic pane. Direct pricing with custom color & treatments.",
                    Icon        = "\U0001F4E6",
                    AccentColor = "#6366F1"
                },
                new GlassTypeCard
                {
                    Code        = "DGU",
                    Title       = "Double Glass Unit (DGU)",
                    Description = "Insulated glass assembly. Configures spacers, gas insertion & outer/inner treatments.",
                    Icon        = "\U0001F4DA",
                    AccentColor = "#A855F7"
                },
                new GlassTypeCard
                {
                    Code        = "LAM",
                    Title       = "Laminated Assembly (LAM)",
                    Description = "Multi-layer safety sandwich. Customizes PVB thickness, outer/inner prices & margins.",
                    Icon        = "\U0001F517",
                    AccentColor = "#22C55E"
                },
                new GlassTypeCard
                {
                    Code        = "DGU+LAM",
                    Title       = "DGU + Laminated (Hybrid)",
                    Description = "Double glazed unit with one laminated layer. Combines thermal and safety performance.",
                    Icon        = "\U0001F3D7",
                    AccentColor = "#F97316"
                }
            };

            // BUG-43: SpecGroups is preview-only, not linked to Step5
            SpecGroups = new ObservableCollection<SpecTargetGroup>
            {
                new SpecTargetGroup
                {
                    Name        = "Spec Group 1",
                    Description = "Default spec group",
                    IsSelected  = true
                }
            };

            SelectGlassTypeCommand = new RelayCommand(param =>
            {
                if (param is GlassTypeCard card)
                    SelectedGlassType = card;
            });

            ToggleSpecGroupCommand = new RelayCommand(param =>
            {
                if (param is SpecTargetGroup group)
                {
                    group.IsSelected = !group.IsSelected;
                    OnPropertyChanged(nameof(TotalSpecsActive));
                }
            });

            // CanExecute still requires a real spec name, which is what
            // keeps an unimplemented glass type from saving garbage. All
            // four types now produce one.
            ApplyRateCommand = new RelayCommand(
                async _ => await ApplyRateAsync(),
                _ => TotalSpecsActive > 0
                     && EstimatedRatePerSqm > 0m
                     && !string.IsNullOrWhiteSpace(
                            GetCurrentSpecName(SelectedGlassType?.Code ?? "")));

            // Default selection = SGU
            SelectedGlassType = GlassTypes[0];

            // Default treatments
            PolishedEdges = false;
            DrillCutouts = false;
            CountersunkHoles = false;
            EdgeDeletionLayer = false;

            // Initial calc
            RecalculateEstimatedRate();

            // PHASE A/J: populate the Sheet Store dropdowns and prices.
            // Fire-and-forget with its own guard — a constructor cannot
            // await, and an empty/locked Sheet Store must never stop the
            // pricing panels from opening.
            LoadSheetStoreDataAsync();
        }

        /// <summary>
        /// PHASE A/J — pulls Sheet Store colours, sizes and prices into the
        /// SGU, DGU and LAM panels. Without this the sheet-size dropdowns
        /// stay empty and every price box reads 0.00.
        /// Never throws; each panel guards itself internally.
        /// </summary>
        private async void LoadSheetStoreDataAsync()
        {
            try
            {
                await SguVM.LoadSheetStoreAsync().ConfigureAwait(true);
                await DguVM.LoadSheetStoreAsync().ConfigureAwait(true);
                await LamVM.LoadSheetStoreAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PricingEstimator] Sheet Store load failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Re-reads Sheet Store data. Call after the user edits stock so
        /// Step 4 reflects the new prices without reopening the wizard.
        /// </summary>
        public void RefreshSheetStoreData() => LoadSheetStoreDataAsync();

        // ==================== APPLY RATE (SAVE TO DATABASE) ====================

        /// <summary>
        /// Saves the currently configured spec (name + rate) to
        /// specifications.db for later reuse in Step 5 (Data Grid) spec
        /// dropdown.
        ///
        /// BUG-42: Returns early with a friendly warning if the current
        ///         glass type has no real spec name (DGU+LAM hybrid isn't
        ///         implemented yet — was previously writing garbage).
        /// BUG-44: Uses one-line non-blocking ApplyStatusMessage instead
        ///         of blocking MessageBox for success. Errors still use
        ///         MessageBox because they need to interrupt user attention.
        /// </summary>
        private async System.Threading.Tasks.Task ApplyRateAsync()
        {
            if (SelectedGlassType == null || EstimatedRatePerSqm <= 0m)
            {
                ApplyStatusMessage =
                    "⚠  Configure glass parameters and ensure estimated rate > 0.";
                return;
            }

            string glassType = SelectedGlassType.Code;
            string specName = GetCurrentSpecName(glassType);
            decimal rate = EstimatedRatePerSqm;

            // BUG-42: Guard — empty spec name means the type isn't
            // implemented (e.g. DGU+LAM). Do NOT save garbage.
            if (string.IsNullOrWhiteSpace(specName))
            {
                ApplyStatusMessage =
                    $"⚠  {glassType} pricing is not yet implemented — cannot save spec.";
                return;
            }

            // R-2: guard placed after the validation returns above so the
            // early-exit paths still surface their status message.
            if (_isApplyingRate) return;
            _isApplyingRate = true;

            try
            {
                var savedSpec = await App.Specs.SaveOrUpdateAsync(
                    glassType, specName, rate);

                // PHASE G: remember WHICH stock sheet this spec was priced
                // against, so Step 5 can carry it and Step 9 can cost the
                // sheet the user actually chose instead of re-guessing.
                // specifications.db deliberately stays name+price only —
                // sheet choice is a per-invoice decision, not a spec property.
                RecordSheetSelection(glassType, specName);

                bool isNew = savedSpec.UsageCount == 1;

                // BUG-44: non-blocking one-line status
                ApplyStatusMessage = isNew
                    ? $"✓  Saved '{specName}' — AED {rate:N2}/sqm. Now available in Step 5."
                    : $"✓  Updated '{specName}' — AED {rate:N2}/sqm  (used {savedSpec.UsageCount}×).";
            }
            catch (Exception ex)
            {
                // Errors still use MessageBox — must interrupt the user
                MessageBox.Show(
                    $"Failed to save spec:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                ApplyStatusMessage = $"✗  Save failed: {ex.Message}";
            }
            finally
            {
                // Must release on EVERY path, including the failure path,
                // or the Apply button stays dead for the rest of the session.
                _isApplyingRate = false;
            }
        }

        /// <summary>
        /// PHASE G — publishes the chosen stock sheet(s) for this spec into
        /// SpecSheetRegistry. Only DGU and SGU expose a sheet picker today;
        /// other panels record nothing and Step 9 falls back as before.
        /// Never throws — a failure here must not block saving the spec.
        /// </summary>
        private void RecordSheetSelection(string glassType, string specName)
        {
            try
            {
                switch (glassType)
                {
                    // Record the numeric width/height the user actually has
                    // in the boxes. Those are the authoritative values now
                    // that sheet size is two editable fields per side - a
                    // typed custom size has no entry in the size dropdown.
                    case "DGU":
                        ProGlassAutomation.Services.ProformaInvoice.SpecSheetRegistry
                            .Record(specName,
                                new ProGlassAutomation.Services.ProformaInvoice.SpecSheetSelection
                                {
                                    OuterWidthMm = DguVM.OuterSheetWidthMm,
                                    OuterHeightMm = DguVM.OuterSheetHeightMm,
                                    InnerWidthMm = DguVM.InnerSheetWidthMm,
                                    InnerHeightMm = DguVM.InnerSheetHeightMm
                                });
                        break;

                    case "SGU":
                        ProGlassAutomation.Services.ProformaInvoice.SpecSheetRegistry
                            .Record(specName,
                                new ProGlassAutomation.Services.ProformaInvoice.SpecSheetSelection
                                {
                                    OuterWidthMm = SguVM.SheetWidthMm,
                                    OuterHeightMm = SguVM.SheetHeightMm
                                });
                        break;

                    case "LAM":
                        ProGlassAutomation.Services.ProformaInvoice.SpecSheetRegistry
                            .Record(specName,
                                new ProGlassAutomation.Services.ProformaInvoice.SpecSheetSelection
                                {
                                    OuterWidthMm = LamVM.OuterSheetWidthMm,
                                    OuterHeightMm = LamVM.OuterSheetHeightMm,
                                    InnerWidthMm = LamVM.InnerSheetWidthMm,
                                    InnerHeightMm = LamVM.InnerSheetHeightMm
                                });
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PricingEstimator] sheet selection not recorded: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the current auto-generated spec name from the active panel.
        ///
        /// BUG-42 originally forced DGU+LAM to "" because the hybrid had no
        /// implementation and a placeholder string was being saved as if it
        /// were a real specification. The hybrid is implemented now, so it
        /// returns its true spec name — while "" was correct it also left
        /// APPLY RATE permanently disabled, so a hybrid could be priced on
        /// screen and never applied to a spec group.
        ///
        /// The empty-string guard in ApplyRateAsync stays: it is what stops
        /// an unimplemented type from writing garbage, and it must keep
        /// working for any type added later.
        /// </summary>
        private string GetCurrentSpecName(string glassType)
        {
            return glassType switch
            {
                "SGU" => SguVM.GeneratedSpecName ?? "",
                "DGU" => DguVM.GeneratedSpecName ?? "",
                "LAM" => LamVM.GeneratedSpecName ?? "",
                "DGU+LAM" => DguLamVM.GeneratedSpecName ?? "",
                _ => ""
            };
        }

        // ==================== LIVE CALCULATION ====================

        // BUG-45: single handler for all panel property changes.
        // Only recalculates if the currently selected glass type matches
        // the panel that fired the event.
        private void OnAnyPanelPropertyChanged(
            object? sender, PropertyChangedEventArgs e)
        {
            string? currentType = SelectedGlassType?.Code;
            if (currentType == null) return;

            bool relevant = currentType switch
            {
                "SGU" => sender is SguParameters.SguParametersViewModel,
                "DGU" => sender is DguParameters.DguParametersViewModel,
                "LAM" => sender is LamParameters.LamParametersViewModel,
                "DGU+LAM" => sender is DguLamParameters.DguLamParametersViewModel,
                _ => false
            };

            if (relevant)
                RecalculateEstimatedRate();
        }

        private void RecalculateEstimatedRate()
        {
            YieldFactorWarning = "";   // BUG-40/41: clear stale warning

            if (SelectedGlassType == null)
            {
                EstimatedRatePerSqm = 0m;
                return;
            }

            switch (SelectedGlassType.Code)
            {
                case "SGU":
                    EstimatedRatePerSqm = CalculateSguRate();
                    break;

                case "DGU":
                    EstimatedRatePerSqm = CalculateDguRate();
                    break;

                case "LAM":
                    EstimatedRatePerSqm = CalculateLamRate();
                    break;

                case "DGU+LAM":
                    // Was 0m "pending business definition", which meant a real
                    // hybrid quote priced at NOTHING and nothing on screen said
                    // so. Now costed as a THREE-pane unit: outer + air spacer +
                    // laminated outer + interlayer + laminated inner.
                    EstimatedRatePerSqm = CalculateDguLamRate();
                    break;

                default:
                    EstimatedRatePerSqm = 0m;
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  BUG-40 [HIGH]: SGU rate — invalid yield factor no longer silent
        //
        //  Step 1: raw = Base Price per SQM
        //  Step 2: cost = raw / Yield Factor
        //  Step 3: + Cutting Cost
        //  Step 4: + Tempering Cost
        //  Step 5: * (1 + Margin/100)
        // ══════════════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════════════
        //  PRE-MARGIN COST BASIS
        //
        //  Every rate above is  basis x (1 + margin%).  The basis is the
        //  number a quoted rate has to be measured against, and until now it
        //  was a local variable inside each Calculate*Rate method — invisible
        //  to anything else.
        //
        //  Step 9's rate check needs it. It was using the Profit & Loss
        //  materials cost instead, which is a different quantity entirely:
        //  that figure is built from SHEETS PURCHASED at PURCHASE prices,
        //  while the quoted rate is built from CUT AREA at SELLING prices
        //  plus spacer, cutting, tempering and yield. Comparing a rate
        //  against it reported a margin that was never the margin.
        //
        //  Worked example, 6mm Clear FT + 12mm ASP + 6mm Clear FT:
        //      basis (25 + 25)/0.85 + 45 = 103.82 per SQM
        //      x 1.15 = 119.39   x 1.10 = 114.20
        //  So 114.20 reads as 10%, which is what the business expects.
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Cost per SQM before the profit margin is applied, for the glass
        /// type currently selected. Returns 0 when no type is selected or the
        /// selection is not recognised.
        /// </summary>
        public decimal CostBasisPerSqm
        {
            get
            {
                switch (SelectedGlassType?.Code)
                {
                    case "SGU": return SguBasis();
                    case "DGU": return DguBasis();
                    case "LAM": return LamBasis();
                    case "DGU+LAM": return DguLamBasis();
                    default: return 0m;
                }
            }
        }

        /// <summary>Yield never divides by zero; mirrors the rate methods.</summary>
        private static decimal SafeYield(decimal yieldFactor) =>
            yieldFactor <= 0m ? 1m : yieldFactor;

        private decimal SguBasis()
        {
            var vm = SguVM;
            decimal basis = vm.BasePricePerSqm / SafeYield(vm.YieldFactor)
                          + vm.CuttingCostPerSqm
                          + vm.TemperingCostPerSqm;
            return Math.Round(basis, 2);
        }

        private decimal DguBasis()
        {
            var vm = DguVM;
            decimal basis = (vm.OuterBasePrice + vm.InnerBasePrice)
                                / SafeYield(vm.YieldFactor)
                          + vm.SpacerPrice;
            return Math.Round(basis, 2);
        }

        private decimal LamBasis()
        {
            var vm = LamVM;
            decimal basis = (vm.OuterBasePrice + vm.InnerBasePrice)
                                / SafeYield(vm.YieldFactor)
                          + vm.PvbMaterialRate
                          + vm.OuterCuttingCost + vm.InnerCuttingCost
                          + vm.OuterTemperingCost + vm.InnerTemperingCost;
            return Math.Round(basis, 2);
        }

        private decimal DguLamBasis()
        {
            var vm = DguLamVM;
            decimal basis = (vm.OuterBasePrice + vm.LamOuterBasePrice + vm.LamInnerBasePrice)
                                / SafeYield(vm.YieldFactor)
                          + vm.TotalCuttingCost
                          + vm.TotalTemperingCost
                          + vm.InterlayerSellPrice
                          + vm.SpacerPrice;
            return Math.Round(basis, 2);
        }

        private decimal CalculateSguRate()
        {
            var vm = SguVM;

            decimal basePrice = vm.BasePricePerSqm;
            decimal yieldFactor = vm.YieldFactor;
            decimal cuttingCost = vm.CuttingCostPerSqm;
            decimal temperingCost = vm.TemperingCostPerSqm;
            decimal marginPercent = vm.TargetMarginPercent;

            // BUG-40: warn instead of silent reset
            if (yieldFactor <= 0m)
            {
                YieldFactorWarning =
                    $"⚠  SGU yield factor must be > 0 (currently {yieldFactor:0.###}). " +
                    "Using 1.0 as fallback.";
                yieldFactor = 1m;
            }

            decimal step1 = basePrice;
            decimal step2 = step1 / yieldFactor;
            decimal step3 = step2 + cuttingCost;
            decimal step4 = step3 + temperingCost;
            decimal step5 = step4 * (1m + (marginPercent / 100m));

            return Math.Round(step5, 2);
        }

        // ══════════════════════════════════════════════════════════════════
        //  BUG-41 [HIGH]: DGU rate — invalid yield factor no longer silent
        // ══════════════════════════════════════════════════════════════════

        private decimal CalculateDguRate()
        {
            var vm = DguVM;

            decimal outerPrice = vm.OuterBasePrice;
            decimal innerPrice = vm.InnerBasePrice;
            decimal spacerPrice = vm.SpacerPrice;
            decimal yieldFactor = vm.YieldFactor;
            decimal marginPercent = vm.MarginProfitPercent;

            // BUG-41: warn instead of silent reset
            if (yieldFactor <= 0m)
            {
                YieldFactorWarning =
                    $"⚠  DGU yield factor must be > 0 (currently {yieldFactor:0.###}). " +
                    "Using 1.0 as fallback.";
                yieldFactor = 1m;
            }

            decimal step1 = outerPrice + innerPrice;                    // 73
            decimal step2 = step1 / yieldFactor;                        // 85.88
            decimal step3 = step2 + spacerPrice;                        // 130.88
            decimal step4 = step3 * (1m + (marginPercent / 100m));      // 150.51

            return Math.Round(step4, 2);
        }

        // ══════════════════════════════════════════════════════════════════
        //  BUG-41 [HIGH]: LAM rate — invalid yield factor no longer silent
        // ══════════════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════════════
        //  DGU+LAM — three panes of glass, one air spacer, one interlayer
        //
        //  The rule set lives on DguLamParametersViewModel.CalculateRate so
        //  there is ONE definition of the hybrid price. This wrapper only
        //  surfaces its warning through the existing YieldFactorWarning
        //  channel, the same way the SGU/DGU/LAM branches do.
        // ══════════════════════════════════════════════════════════════════

        private decimal CalculateDguLamRate()
        {
            decimal rate = DguLamVM.CalculateRate(out string warning);

            if (!string.IsNullOrWhiteSpace(warning))
                YieldFactorWarning = warning;

            return rate;
        }

        private decimal CalculateLamRate()
        {
            var vm = LamVM;

            decimal outerPrice = vm.OuterBasePrice;
            decimal innerPrice = vm.InnerBasePrice;
            decimal pvbRate = vm.PvbMaterialRate;
            decimal outerCutting = vm.OuterCuttingCost;
            decimal innerCutting = vm.InnerCuttingCost;
            decimal outerTempering = vm.OuterTemperingCost;
            decimal innerTempering = vm.InnerTemperingCost;
            decimal yieldFactor = vm.YieldFactor;
            decimal marginPercent = vm.MarginProfitPercent;

            // BUG-41: warn instead of silent reset
            if (yieldFactor <= 0m)
            {
                YieldFactorWarning =
                    $"⚠  LAM yield factor must be > 0 (currently {yieldFactor:0.###}). " +
                    "Using 1.0 as fallback.";
                yieldFactor = 1m;
            }

            decimal step1 = outerPrice + innerPrice;                              // 50
            decimal step2 = step1 / yieldFactor;                                  // 58.82
            decimal step3 = step2 + pvbRate;                                      // 168.82
            decimal step4 = step3 + outerCutting + innerCutting;                  // 178.82
            decimal step5 = step4 + outerTempering + innerTempering;              // 198.82
            decimal step6 = step5 * (1m + (marginPercent / 100m));                // 228.65

            return Math.Round(step6, 2);
        }
    }
}