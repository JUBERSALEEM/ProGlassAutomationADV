using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
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
            row.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WastageRow.ResultAmount)
                    || e.PropertyName == nameof(WastageRow.IsActive))
                    RecalculateTotal();
            };
            WastageRows.Add(row);
        }

        public void SyncAllWastageSources()
        {
            foreach (var row in WastageRows) SyncSourcesForRow(row);
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
            var previousRows = WastageRows
                .GroupBy(r => r.ColorName ?? "", System.StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First(),
                    System.StringComparer.OrdinalIgnoreCase);

            WastageRows.Clear();

            foreach (var group in suggestions
                .GroupBy(x => ExtractColorKey(x.DisplayName), System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key, System.StringComparer.OrdinalIgnoreCase))
            {
                previousRows.TryGetValue(group.Key, out var previousRow);

                var row = new WastageRow
                {
                    IsActive = previousRow?.IsActive ?? false,
                    ColorName = group.Key,
                    Rate = previousRow?.Rate ?? 0m,
                    UseOptimizationFormula = previousRow?.UseOptimizationFormula ?? true,
                    FormulaPricePerSqm = previousRow?.FormulaPricePerSqm ?? 0m,
                    OptimizationPercent = previousRow?.OptimizationPercent ?? 0m
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

            // Hook total recalculation on state changes
            row.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WastageRow.ResultAmount)
                    || e.PropertyName == nameof(WastageRow.IsActive))
                    RecalculateTotal();
            };
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

                // Outer
                var outer = new SpecGroupColorSide
                {
                    SpecNumber = group.GroupNumber,
                    SpecName = group.GroupName,
                    SideLabel = "Outer (W1/H1)",
                    ColorName = group.OuterGlassColor,
                    SqmValue = group.FooterTotalSqm,
                    SheetWidthMm = group.OuterSheetWidthMm,
                    SheetHeightMm = group.OuterSheetHeightMm,
                    PricePerSqm = group.OuterSheetPricePerSqm
                };
                ApplyStep4SourceMetadata(outer, step4Snapshot, group.SelectedGlassType, preferInner: false);
                if (checkedIds.Contains($"{outer.SpecNumber}-{outer.SideLabel}"))
                    outer.IsChecked = true;
                row.AvailableSources.Add(outer);

                // Inner (if DGU/LAM)
                bool hasInner = group.SelectedGlassType != "SGU";
                if (hasInner)
                {
                    var inner = new SpecGroupColorSide
                    {
                        SpecNumber = group.GroupNumber,
                        SpecName = group.GroupName,
                        SideLabel = "Inner (W2/H2)",
                        ColorName = group.InnerGlassColor,
                        SqmValue = group.FooterTotalSqm2,
                        SheetWidthMm = group.InnerSheetWidthMm,
                        SheetHeightMm = group.InnerSheetHeightMm,
                        PricePerSqm = group.InnerSheetPricePerSqm
                    };
                    ApplyStep4SourceMetadata(inner, step4Snapshot, group.SelectedGlassType, preferInner: true);
                    if (checkedIds.Contains($"{inner.SpecNumber}-{inner.SideLabel}"))
                        inner.IsChecked = true;
                    row.AvailableSources.Add(inner);
                }
            }
            row.ForceRefreshSqm();
        }

        public void RefreshTotal() => RecalculateTotal();

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

        private void RecalculateTotal()
        {
            TotalWastageValue = WastageRows
                .Where(r => r.IsActive)
                .Sum(r => r.ResultAmount);
        }
    }
}