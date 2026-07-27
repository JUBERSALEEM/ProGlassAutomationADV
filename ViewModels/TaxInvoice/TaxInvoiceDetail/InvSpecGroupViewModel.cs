using ProGlassAutomation.Commands;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceDetail
{
    /// <summary>
    /// ViewModel for one spec group on a Tax Invoice.
    /// Contains an ObservableCollection of InvRowViewModel rows.
    ///
    /// Split from TaxInvoiceDetailViewModel.cs (400-line limit).
    ///
    /// RATE PROPAGATION:
    ///   Setting BaseRate pushes the new rate to every existing row.
    ///   New rows added via AddRowCommand inherit BaseRate at creation time.
    ///
    /// ROW RECALC CHAIN:
    ///   InvRowViewModel.RecalcRow()
    ///     → InvSpecGroupViewModel.RecalculateGroup()
    ///       → TaxInvoiceDetailViewModel.RecalculateFinancials()  (via OnRowChanged)
    ///
    /// This ensures every keystroke in W1/H1/Qty/Rate immediately
    /// updates the group totals and invoice grand total live.
    /// </summary>
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

    // ══════════════════════════════════════════════════════════════════════
    //  ROW VIEW MODEL — one glass panel row within a spec group
    //
    //  RECALC CHAIN (every field change triggers this):
    //    W1/H1 change → SQM recalc via SqmCalculator.CalculatePanel()
    //    Qty/Rate change → Value recalc
    //    Any change → RecalculateGroup() → OnRowChanged (parent VM)
    //
    //  SQM uses SqmCalculator — 0.5 m² minimum floor (UAE standard).
    //  Value = Rate × Qty × Sqm (per panel, not per total SQM).
    // ══════════════════════════════════════════════════════════════════════

    public class InvRowViewModel : ViewModelBase
    {
        private readonly InvSpecGroupViewModel _parent;

        public InvRowViewModel(InvSpecGroupViewModel parent)
        {
            _parent = parent;
        }

        // ── Serial number ─────────────────────────────────────────────────

        private int _serialNumber;
        public int SerialNumber
        {
            get => _serialNumber;
            set { _serialNumber = value; OnPropertyChanged(); }
        }

        // ── Glass ref ─────────────────────────────────────────────────────

        private string _glassRef = "";
        public string GlassRef
        {
            get => _glassRef;
            set { _glassRef = value; OnPropertyChanged(); }
        }

        // ── Dimensions (trigger SQM recalc) ──────────────────────────────

        private int _w1;
        public int W1
        {
            get => _w1;
            set
            {
                if (_w1 == value) return;
                _w1 = value;
                OnPropertyChanged();
                RecalcRow();
            }
        }

        private int _h1;
        public int H1
        {
            get => _h1;
            set
            {
                if (_h1 == value) return;
                _h1 = value;
                OnPropertyChanged();
                RecalcRow();
            }
        }

        private int _w2;
        public int W2
        {
            get => _w2;
            set { _w2 = value; OnPropertyChanged(); }
            // W2/H2 are display-only shape dimensions — no SQM effect
        }

        private int _h2;
        public int H2
        {
            get => _h2;
            set { _h2 = value; OnPropertyChanged(); }
        }

        // ── Qty (triggers value recalc) ───────────────────────────────────

        private int _qty;
        public int Qty
        {
            get => _qty;
            set
            {
                if (_qty == value) return;
                _qty = value;
                OnPropertyChanged();
                RecalcRow();
            }
        }

        // ── Rate (triggers value recalc) ──────────────────────────────────

        private decimal _rate;
        public decimal Rate
        {
            get => _rate;
            set
            {
                if (_rate == value) return;
                _rate = value;
                OnPropertyChanged();
                RecalcRow();
            }
        }

        // ── Notes ─────────────────────────────────────────────────────────

        private string _notes = "";
        public string Notes
        {
            get => _notes;
            set { _notes = value; OnPropertyChanged(); }
        }

        // ── Computed (read-only, live) ────────────────────────────────────

        private decimal _sqm;
        public decimal Sqm
        {
            get => _sqm;
            private set { _sqm = value; OnPropertyChanged(); }
        }

        private decimal _totalSqm;
        public decimal TotalSqm
        {
            get => _totalSqm;
            private set { _totalSqm = value; OnPropertyChanged(); }
        }

        private decimal _value;
        public decimal Value
        {
            get => _value;
            private set { _value = value; OnPropertyChanged(); }
        }

        // ── Recalculation ─────────────────────────────────────────────────

        /// <summary>
        /// Recalculates SQM → TotalSqm → Value, then propagates up
        /// to the parent group and ultimately the invoice grand total.
        ///
        /// SqmCalculator.CalculatePanel() applies 0.5 m² minimum floor.
        /// Value formula: Rate × Qty × Sqm (per panel area, not total area).
        /// </summary>
        private void RecalcRow()
        {
            Sqm = SqmCalculator.CalculatePanel(_w1, _h1);
            TotalSqm = Math.Round(Sqm * _qty, 3);
            Value = Math.Round(_rate * _qty * Sqm, 2);

            // Propagate up the chain
            _parent.RecalculateGroup();
            _parent.OnRowChanged?.Invoke();
        }
    }
}