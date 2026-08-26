using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges.Wastages
{
    public class WastagesSectionViewModel : ViewModelBase
    {
        private readonly DataGridViewModel _dataGrid;

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
            var row = new WastageRow();
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
            var previousRates = WastageRows
                .GroupBy(r => r.ColorName ?? "", System.StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Rate, System.StringComparer.OrdinalIgnoreCase);

            WastageRows.Clear();

            foreach (var group in suggestions
                .GroupBy(x => x.DisplayName, System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key, System.StringComparer.OrdinalIgnoreCase))
            {
                var row = new WastageRow
                {
                    IsActive = true,
                    ColorName = group.Key,
                    Rate = previousRates.TryGetValue(group.Key, out var rate) ? rate : 40m
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

            row.AvailableSources.Clear();

            foreach (var group in _dataGrid.SpecGroups)
            {
                // Outer
                var outer = new SpecGroupColorSide
                {
                    SpecNumber = group.GroupNumber,
                    SpecName = group.GroupName,
                    SideLabel = "Outer (W1/H1)",
                    SqmValue = group.FooterTotalSqm
                };
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
                        SqmValue = group.FooterTotalSqm2
                    };
                    if (checkedIds.Contains($"{inner.SpecNumber}-{inner.SideLabel}"))
                        inner.IsChecked = true;
                    row.AvailableSources.Add(inner);
                }
            }
            row.ForceRefreshSqm();
        }

        public void RefreshTotal() => RecalculateTotal();

        private void RecalculateTotal()
        {
            TotalWastageValue = WastageRows
                .Where(r => r.IsActive)
                .Sum(r => r.ResultAmount);
        }
    }
}