using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges.Wastages
{
    public class WastagesSectionViewModel : ViewModelBase
    {
        private readonly DataGridViewModel _dataGrid;
        private Func<IReadOnlyList<Step4PricingSnapshot>>? _step4SectionsProvider;

        public ObservableCollection<WastageRow> WastageRows { get; } = new();

        private decimal _totalWastageValue;
        public decimal TotalWastageValue
        {
            get => _totalWastageValue;
            private set
            {
                if (_totalWastageValue == value) return;
                _totalWastageValue = value;
                OnPropertyChanged();
            }
        }

        public ICommand AddWastageCommand { get; }
        public ICommand RemoveWastageCommand { get; }

        public void BindStep4SectionsProvider(Func<IReadOnlyList<Step4PricingSnapshot>> provider)
        {
            _step4SectionsProvider = provider;
            SyncAllWastageSources();
        }

        public WastagesSectionViewModel(DataGridViewModel dataGrid)
        {
            _dataGrid = dataGrid;
            AddWastageCommand = new RelayCommand(_ => AddWastage());
            RemoveWastageCommand = new RelayCommand(p =>
            {
                if (p is WastageRow r)
                {
                    DetachRow(r);
                    WastageRows.Remove(r);
                    RecalculateTotal();
                }
            });
        }

        private void AddWastage()
        {
            var row = new WastageRow
            {
                Rate = 0m
            };
            SyncSourcesForRow(row);
            AttachRow(row);
            WastageRows.Add(row);
        }

        public void SyncAllWastageSources()
        {
            foreach (var row in WastageRows) SyncSourcesForRow(row);
            RecalculateTotal();
        }

        /// <summary>
        /// Removes every current wastage row after unhooking its live total
        /// subscription. Used by reset/restore paths so Step 6 never keeps
        /// listening to rows from the previous invoice state.
        /// </summary>
        public void ClearWastageRows()
        {
            foreach (var row in WastageRows.ToList())
                DetachRow(row);

            WastageRows.Clear();
            RecalculateTotal();
        }

        /// <summary>
        /// Advanced Step 6 mode: rebuild wastage rows from the glass sides the
        /// current specification set requires. Existing rates are preserved by
        /// display name so an operator's invoice-specific price survives a
        /// refresh even while the source selection is auto-managed.
        /// </summary>
        public void ApplyAdvancedSuggestions(IEnumerable<AutoWastageSuggestion> suggestions)
        {
            // A DGU build has TWO panes — outer and inner — and DGU+LAM has
            // three. Each side is its own wastage row with its own setup
            // SHEET QTY, so same-colour sides must NOT be folded into one
            // row: they were, and Step 9 could only read one quantity for
            // both panes. Rows therefore key on colour + side.
            var previousRows = WastageRows
                .GroupBy(r => WastageRowKey(r.ColorName, RowSideKey(r)),
                    System.StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First(),
                    System.StringComparer.OrdinalIgnoreCase);

            ClearWastageRows();

            foreach (var group in suggestions
                .GroupBy(x => WastageRowKey(
                        ExtractColorKey(x.DisplayName), x.SourceSideLabel),
                    System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key, System.StringComparer.OrdinalIgnoreCase))
            {
                previousRows.TryGetValue(group.Key, out var previousRow);

                var row = new WastageRow
                {
                    IsActive = previousRow?.IsActive ?? true,
                    ColorName = ExtractColorKey(group.First().DisplayName),
                    Rate = previousRow?.Rate ?? 0m,
                    UseOptimizationFormula = previousRow?.UseOptimizationFormula ?? true,
                    FormulaPricePerSqm = previousRow?.FormulaPricePerSqm ?? 0m,
                    OptimizationPercent = previousRow?.OptimizationPercent ?? 0m,
                    SheetQuantity = previousRow?.SheetQuantity ?? 1
                };

                PopulateSourcesForRestoredRow(row);

                var selectedKeys = group
                    .Select(x => $"{x.SpecGroupNumber}-{x.SourceSideLabel}")
                    .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

                foreach (var src in row.AvailableSources)
                {
                    string key = $"{src.SpecNumber}-{src.SideLabel}";
                    src.IsChecked = selectedKeys.Contains(key);
                }

                WastageRows.Add(row);
            }

            RecalculateTotal();
        }

        /// <summary>
        /// BUG-16 support: Public entry point for the mapper.
        /// Populates AvailableSources on a freshly created wastage row
        /// during snapshot restore, WITHOUT firing UI commands.
        ///
        /// Also hooks the row's PropertyChanged event so total updates
        /// when the row becomes active or its rate changes.
        ///
        /// Called by ProformaInvoiceMapper.RestoreOtherCharges().
        /// Do NOT add the row to WastageRows here — caller does that
        /// after applying restored state (IsActive, ColorName, Rate,
        /// CheckedSourceKeys).
        /// </summary>
        public void PopulateSourcesForRestoredRow(WastageRow row)
        {
            if (row == null) return;

            // Populate available sources from current spec groups
            SyncSourcesForRow(row);
            AttachRow(row);
        }

        private void SyncSourcesForRow(WastageRow row)
        {
            var checkedIds = row.AvailableSources
                .Where(s => s.IsChecked)
                .Select(s => $"{s.SpecNumber}-{s.SideLabel}")
                .ToList();

            var step4Sections = _step4SectionsProvider?.Invoke();

            row.AvailableSources.Clear();

            foreach (var group in _dataGrid.SpecGroups)
            {
                var step4Snapshot = ResolveStep4Snapshot(step4Sections, group.GroupNumber);

                // ══════════════════════════════════════════════════════════
                //  BUILD IS RESOLVED FROM THE SPEC FIRST, TYPE FIELD SECOND —
                //  the SAME rule OtherChargeAutomationRules uses. Previously
                //  this method read the raw SelectGlassType text, so a DGU
                //  whose spec-name variant lacks the spacer token was treated
                //  as single glazing: the inner wastage source was never
                //  created and "only outer got auto-selected".
                // ══════════════════════════════════════════════════════════
                string type = OtherChargeAutomationRules.ResolveEffectiveGlassType(
                    group.SelectedGlassType, group.SelectedSpecName);

                // A double-glass build's second pane is the same unit size as
                // the first (W1/H1) unless the grid carries an explicit
                // W2/H2 — Step 9 already costs both panes on that area, so
                // the wastage sources must match instead of silently pricing
                // only the outer pane.
                decimal innerSqm = group.FooterTotalSqm2 > 0m
                    ? group.FooterTotalSqm2
                    : group.FooterTotalSqm;

                if (type == "DGU+LAM")
                {
                    var paneColors = ResolveDguLamPaneColors(group, step4Snapshot);

                    AddSource(row, checkedIds, new SpecGroupColorSide
                    {
                        SpecNumber = group.GroupNumber,
                        SpecName = group.GroupName,
                        SideLabel = "Outer (W1/H1)",
                        ColorName = paneColors.Outer,
                        SqmValue = group.FooterTotalSqm,
                        SheetWidthMm = group.OuterSheetWidthMm,
                        SheetHeightMm = group.OuterSheetHeightMm,
                        PricePerSqm = group.OuterSheetPricePerSqm
                    }, step4Snapshot, group.SelectedGlassType, preferInner: false);

                    AddSource(row, checkedIds, new SpecGroupColorSide
                    {
                        SpecNumber = group.GroupNumber,
                        SpecName = group.GroupName,
                        SideLabel = "Lam Outer (W2/H2)",
                        ColorName = paneColors.LamOuter,
                        SqmValue = innerSqm,
                        SheetWidthMm = group.InnerSheetWidthMm,
                        SheetHeightMm = group.InnerSheetHeightMm,
                        PricePerSqm = group.InnerSheetPricePerSqm
                    }, step4Snapshot, group.SelectedGlassType, preferInner: true);

                    AddSource(row, checkedIds, new SpecGroupColorSide
                    {
                        SpecNumber = group.GroupNumber,
                        SpecName = group.GroupName,
                        SideLabel = "Lam Inner (W2/H2)",
                        ColorName = paneColors.LamInner,
                        SqmValue = innerSqm,
                        SheetWidthMm = group.InnerSheetWidthMm,
                        SheetHeightMm = group.InnerSheetHeightMm,
                        PricePerSqm = group.InnerSheetPricePerSqm
                    }, step4Snapshot, group.SelectedGlassType, preferInner: true);

                    continue;
                }

                AddSource(row, checkedIds, new SpecGroupColorSide
                {
                    SpecNumber = group.GroupNumber,
                    SpecName = group.GroupName,
                    SideLabel = "Outer (W1/H1)",
                    ColorName = group.OuterGlassColor,
                    SqmValue = group.FooterTotalSqm,
                    SheetWidthMm = group.OuterSheetWidthMm,
                    SheetHeightMm = group.OuterSheetHeightMm,
                    PricePerSqm = group.OuterSheetPricePerSqm
                }, step4Snapshot, group.SelectedGlassType, preferInner: false);

                // A resolved double-build offers the inner source; so does a
                // grid that physically carries a W2/H2 column even when the
                // type field is blank (manual mode must stay usable).
                if (type is "DGU" or "LAM" || group.FooterTotalSqm2 > 0m)
                {
                    AddSource(row, checkedIds, new SpecGroupColorSide
                    {
                        SpecNumber = group.GroupNumber,
                        SpecName = group.GroupName,
                        SideLabel = "Inner (W2/H2)",
                        ColorName = group.InnerGlassColor,
                        SqmValue = innerSqm,
                        SheetWidthMm = group.InnerSheetWidthMm,
                        SheetHeightMm = group.InnerSheetHeightMm,
                        PricePerSqm = group.InnerSheetPricePerSqm
                    }, step4Snapshot, group.SelectedGlassType, preferInner: true);
                }
            }
            row.ForceRefreshSqm();
        }

        public void RefreshTotal() => RecalculateTotal();

        private static void AddSource(
            WastageRow row,
            IReadOnlyCollection<string> checkedIds,
            SpecGroupColorSide source,
            Step4PricingSnapshot? step4Snapshot,
            string? selectedGlassType,
            bool preferInner)
        {
            ApplyStep4SourceMetadata(source, step4Snapshot, selectedGlassType, preferInner);
            if (checkedIds.Contains($"{source.SpecNumber}-{source.SideLabel}"))
                source.IsChecked = true;
            row.AvailableSources.Add(source);
        }

        private static (string Outer, string LamOuter, string LamInner) ResolveDguLamPaneColors(
            SpecGroup group,
            Step4PricingSnapshot? snapshot)
        {
            var glassMatches = PnLSpecParser.GlassMatches(group.SelectedSpecName);
            if (glassMatches.Count >= 3)
            {
                return (
                    (glassMatches[0].Groups["color"].Value ?? string.Empty).Trim(),
                    (glassMatches[1].Groups["color"].Value ?? string.Empty).Trim(),
                    (glassMatches[2].Groups["color"].Value ?? string.Empty).Trim());
            }

            return (
                snapshot?.DguLam.OuterGlassColor?.Trim() ?? string.Empty,
                snapshot?.DguLam.LamOuterColor?.Trim() ?? group.OuterGlassColor ?? string.Empty,
                snapshot?.DguLam.LamInnerColor?.Trim() ?? group.InnerGlassColor ?? string.Empty);
        }

        private void AttachRow(WastageRow row)
        {
            if (row == null) return;
            row.PropertyChanged -= OnWastageRowChanged;
            row.PropertyChanged += OnWastageRowChanged;
        }

        private void DetachRow(WastageRow row)
        {
            if (row == null) return;
            row.PropertyChanged -= OnWastageRowChanged;
        }

        private void OnWastageRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WastageRow.ResultAmount)
                || e.PropertyName == nameof(WastageRow.IsActive))
            {
                RecalculateTotal();
                return;
            }

            // Step 9 reads the setup SHEET QTY even when the wastage charge
            // row is DISABLED, so editing the box must notify downstream
            // consumers even though the disabled row's result (0) did not
            // change. An active row re-raises through ResultAmount instead.
            if (e.PropertyName == nameof(WastageRow.SheetQuantity))
            {
                bool wasDisabled = sender is WastageRow row && !row.IsActive;
                RecalculateTotal();
                if (wasDisabled)
                    OnPropertyChanged(nameof(TotalWastageValue));
            }
        }

        private static Step4PricingSnapshot? ResolveStep4Snapshot(
            IReadOnlyList<Step4PricingSnapshot>? sections,
            int groupNumber)
        {
            if (sections == null || groupNumber <= 0 || groupNumber > sections.Count)
                return null;

            return sections[groupNumber - 1];
        }

        private static void ApplyStep4SourceMetadata(
            SpecGroupColorSide source,
            Step4PricingSnapshot? snapshot,
            string? selectedGlassType,
            bool preferInner)
        {
            if (source == null || snapshot == null)
                return;

            string type = (selectedGlassType ?? string.Empty).Trim().ToUpperInvariant();
            string color = (source.ColorName ?? string.Empty).Trim();

            if (type == "SGU")
            {
                ApplyIfMatch(source, color, snapshot.Sgu.GlassColor,
                    snapshot.Sgu.SheetWidthMm, snapshot.Sgu.SheetHeightMm,
                    snapshot.Sgu.BasePricePerSqm);
                return;
            }

            if (type == "DGU")
            {
                ApplyBestMatch(source, color, preferInner,
                    (snapshot.Dgu.OuterGlassColor, snapshot.Dgu.OuterSheetWidthMm, snapshot.Dgu.OuterSheetHeightMm, snapshot.Dgu.OuterBasePrice),
                    (snapshot.Dgu.InnerGlassColor, snapshot.Dgu.InnerSheetWidthMm, snapshot.Dgu.InnerSheetHeightMm, snapshot.Dgu.InnerBasePrice));
                return;
            }

            if (type == "LAM")
            {
                ApplyBestMatch(source, color, preferInner,
                    (snapshot.Lam.OuterGlassColor, snapshot.Lam.OuterSheetWidthMm, snapshot.Lam.OuterSheetHeightMm, snapshot.Lam.OuterBasePrice),
                    (snapshot.Lam.InnerGlassColor, snapshot.Lam.InnerSheetWidthMm, snapshot.Lam.InnerSheetHeightMm, snapshot.Lam.InnerBasePrice));
                return;
            }

            if (type == "DGU+LAM")
            {
                ApplyBestMatch(source, color, preferInner,
                    (snapshot.DguLam.OuterGlassColor, 0, 0, snapshot.DguLam.OuterBasePrice),
                    (snapshot.DguLam.LamInnerColor, 0, 0, snapshot.DguLam.LamInnerBasePrice),
                    (snapshot.DguLam.LamOuterColor, 0, 0, snapshot.DguLam.LamOuterBasePrice));
            }
        }

        private static void ApplyBestMatch(
            SpecGroupColorSide source,
            string color,
            bool preferInner,
            params (string Color, int Width, int Height, decimal Price)[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
                return;

            var ordered = preferInner && candidates.Length > 1
                ? candidates.Skip(1).Concat(candidates.Take(1))
                : candidates.AsEnumerable();

            foreach (var candidate in ordered)
            {
                if (!ColorsMatch(color, candidate.Color))
                    continue;

                ApplyMetadata(source, candidate.Width, candidate.Height, candidate.Price);
                return;
            }
        }

        private static void ApplyIfMatch(
            SpecGroupColorSide source,
            string sourceColor,
            string? candidateColor,
            int width,
            int height,
            decimal price)
        {
            if (!ColorsMatch(sourceColor, candidateColor))
                return;

            ApplyMetadata(source, width, height, price);
        }

        private static void ApplyMetadata(
            SpecGroupColorSide source,
            int width,
            int height,
            decimal price)
        {
            if (width > 0)
                source.SheetWidthMm = width;
            if (height > 0)
                source.SheetHeightMm = height;
            if (price > 0m)
                source.PricePerSqm = price;
        }

        private static bool ColorsMatch(string? left, string? right)
        {
            return string.Equals(
                (left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractColorKey(string? displayName)
        {
            string text = (displayName ?? string.Empty).Trim();
            if (text.Length == 0) return string.Empty;

            int emDash = text.IndexOf('—');
            if (emDash > 0)
                return text[..emDash].Trim();

            int hyphen = text.IndexOf('-');
            if (hyphen > 0)
                return text[..hyphen].Trim();

            return text;
        }

        /// <summary>
        /// Row identity in advanced mode = glass colour + pane side.
        /// "Clear — outer glass wastage" and "Clear — inner glass wastage"
        /// are two separate wastage charges (and two setup SHEET QTY boxes).
        /// </summary>
        private static string WastageRowKey(string? colorName, string? sideLabel) =>
            $"{ExtractColorKey(colorName)}:::{NormalizeSideKey(sideLabel)}";

        private static string NormalizeSideKey(string? sideLabel)
        {
            string side = (sideLabel ?? string.Empty).Trim();
            if (side.IndexOf("Lam Inner", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Lam Inner";
            if (side.IndexOf("Lam Outer", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Lam Outer";
            if (side.IndexOf("Inner", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Inner";
            return "Outer";
        }

        /// <summary>
        /// The side a live wastage row belongs to (WastageRow.SideDisplay).
        /// Used to preserve a row's setup across advanced refreshes; a row
        /// created before the side-aware change falls back to outer.
        /// </summary>
        private static string RowSideKey(WastageRow row) =>
            string.IsNullOrWhiteSpace(row.SideDisplay)
                ? "Outer"
                : row.SideDisplay;

        private void RecalculateTotal()
        {
            TotalWastageValue = WastageRows
                .Where(r => r.IsActive)
                .Sum(r => r.ResultAmount);
        }
    }
}