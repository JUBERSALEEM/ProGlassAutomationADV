// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/OrderReport/OrderReportPreviewViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator — Step 2 preview (PI selection)
//
// Shows every Proforma Invoice matching the Step 1 filters, lets the user
// select ONE (single overall report) or MANY (multi-PI report) by ticking
// rows, and reports the selection back through SelectionChanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProGlassAutomation.ViewModels.Reports.OrderReport
{
    public class OrderReportPreviewViewModel : ViewModelBase
    {
        public ObservableCollection<OrderReportPreviewRowViewModel> Rows { get; } = new();

        public event Action? SelectionChanged;

        private int _selectedCount;
        private decimal _selectedValue;

        public int SelectedCount => _selectedCount;
        public bool HasSelection => _selectedCount > 0;
        public bool AllSelected => Rows.Count > 0 && _selectedCount == Rows.Count;

        public string SelectionSummaryDisplay
        {
            get
            {
                int total = Rows.Count;
                if (total == 0) return "No proforma invoices to select";
                return _selectedCount == total
                    ? $"All {total} proforma invoices selected"
                    : $"{_selectedCount} of {total} proforma invoices selected";
            }
        }

        public string SelectedValueDisplay => $"AED {_selectedValue:N2}";

        public void UpdateFromList(IEnumerable<ProformaListRow> rows)
        {
            foreach (var old in Rows)
                old.SelectionChanged -= OnRowSelectionChanged;

            Rows.Clear();

            foreach (var row in rows)
            {
                var vm = new OrderReportPreviewRowViewModel(row) { IsSelected = true };
                vm.SelectionChanged += OnRowSelectionChanged;
                Rows.Add(vm);
            }

            Recompute();
            OnPropertyChanged(string.Empty);
            SelectionChanged?.Invoke();
        }

        public void SelectAll()
        {
            foreach (var r in Rows) r.IsSelectedSilent = true;
            Recompute();
            OnPropertyChanged(string.Empty);
            SelectionChanged?.Invoke();
        }

        public void DeselectAll()
        {
            foreach (var r in Rows) r.IsSelectedSilent = false;
            Recompute();
            OnPropertyChanged(string.Empty);
            SelectionChanged?.Invoke();
        }

        public List<string> GetSelectedProformaIds() =>
            Rows.Where(r => r.IsSelected)
                .Select(r => r.ProformaId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

        private void OnRowSelectionChanged()
        {
            Recompute();
            OnPropertyChanged(string.Empty);
            SelectionChanged?.Invoke();
        }

        private void Recompute()
        {
            var selected = Rows.Where(r => r.IsSelected).ToList();
            _selectedCount = selected.Count;
            _selectedValue = selected.Sum(r => r.GrandTotal);
        }
    }

    public class OrderReportPreviewRowViewModel : ViewModelBase
    {
        private readonly ProformaListRow _row;
        private bool _isSelected;

        public event Action? SelectionChanged;

        public OrderReportPreviewRowViewModel(ProformaListRow row)
        {
            _row = row;
        }

        public string ProformaId => _row.ProformaId;
        public string CompanyName => _row.CompanyName;
        public string CustomerName => _row.CustomerName;
        public string ProjectName => _row.ProjectName;
        public string Status => _row.Status;
        public decimal GrandTotal => _row.GrandTotalVatInclusive;
        public DateTime CreatedAt => _row.CreatedAt;

        public string GrandTotalDisplay => $"AED {GrandTotal:N2}";
        public string CreatedAtDisplay => CreatedAt.ToString("dd MMM yyyy");

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
                SelectionChanged?.Invoke();
            }
        }

        public bool IsSelectedSilent
        {
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }
}
