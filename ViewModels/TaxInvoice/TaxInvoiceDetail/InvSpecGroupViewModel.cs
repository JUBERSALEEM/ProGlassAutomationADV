using ProGlassAutomation.Commands;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceDetail
{
    public class InvSpecGroupViewModel : ViewModelBase
    {
        // ── Callback to parent detail VM ──────────────────────────────────
        public Action? OnRowChanged { get; set; }

        // ── Identity ──────────────────────────────────────────────────────
        public int GroupNumber { get; }
        public string ProformaId { get; }

        // ── Editable fields ───────────────────────────────────────────────

        private string _groupName = "";
        public string GroupName
        {
            get => _groupName;
            set { _groupName = value; OnPropertyChanged(); }
        }

        private string _glassType = "";
        public string GlassType
        {
            get => _glassType;
            set { _glassType = value; OnPropertyChanged(); }
        }

        private string _specName = "";
        public string SpecName
        {
            get => _specName;
            set { _specName = value; OnPropertyChanged(); }
        }

        private decimal _baseRate;
        public decimal BaseRate
        {
            get => _baseRate;
            set
            {
                if (_baseRate == value) return;
                _baseRate = value;
                OnPropertyChanged();

                // Propagate to all existing rows
                foreach (var row in Rows)
                    row.Rate = value;

                OnRowChanged?.Invoke();
            }
        }

        // ── Group totals (computed, displayed in group footer) ────────────

        private int _groupTotalQty;
        public int GroupTotalQty
        {
            get => _groupTotalQty;
            private set { _groupTotalQty = value; OnPropertyChanged(); }
        }

        private decimal _groupTotalSqm;
        public decimal GroupTotalSqm
        {
            get => _groupTotalSqm;
            private set { _groupTotalSqm = value; OnPropertyChanged(); }
        }

        private decimal _groupTotalValue;
        public decimal GroupTotalValue
        {
            get => _groupTotalValue;
            private set { _groupTotalValue = value; OnPropertyChanged(); }
        }

        // ── Rows ──────────────────────────────────────────────────────────
        public ObservableCollection<InvRowViewModel> Rows { get; } = new();

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand AddRowCommand { get; }
        public ICommand RemoveRowCommand { get; }

        // ── Constructor ───────────────────────────────────────────────────

        public InvSpecGroupViewModel(int groupNumber, string proformaId)
        {
            GroupNumber = groupNumber;
            ProformaId = proformaId;

            AddRowCommand = new RelayCommand(_ =>
            {
                int next = Rows.Count > 0
                    ? Rows.Max(r => r.SerialNumber) + 1
                    : 1;

                var row = new InvRowViewModel(this)
                {
                    SerialNumber = next,
                    Rate = BaseRate   // inherit current group rate
                };

                Rows.Add(row);
                RecalculateGroup();
                OnRowChanged?.Invoke();
            });

            RemoveRowCommand = new RelayCommand(param =>
            {
                if (param is not InvRowViewModel row) return;
                Rows.Remove(row);
                RenumberRows();
                RecalculateGroup();
                OnRowChanged?.Invoke();
            });
        }

        // ── Recalculation ─────────────────────────────────────────────────

        /// <summary>
        /// Recomputes GroupTotalQty, GroupTotalSqm, GroupTotalValue
        /// from current row values.
        /// Called by:
        ///   • InvRowViewModel.RecalcRow() (after any field change)
        ///   • AddRowCommand / RemoveRowCommand
        ///   • TaxInvoiceDetailViewModel.RestoreFromSnapshot()
        /// </summary>
        public void RecalculateGroup()
        {
            GroupTotalQty = Rows.Sum(r => r.Qty);
            GroupTotalSqm = Math.Round(Rows.Sum(r => r.TotalSqm), 3);
            GroupTotalValue = Math.Round(Rows.Sum(r => r.Value), 2);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void RenumberRows()
        {
            for (int i = 0; i < Rows.Count; i++)
                Rows[i].SerialNumber = i + 1;
        }
    }
}
