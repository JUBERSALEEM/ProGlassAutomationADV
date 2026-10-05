// ═══════════════════════════════════════════════════════════════════════
// FILE: InvRowViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: InvSpecGroupViewModel.cs
//
// InvRowViewModel split out of InvSpecGroupViewModel.cs (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceDetail
{
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
            Value = MoneyRounding.Round(_rate * _qty * Sqm);

            // Propagate up the chain
            _parent.RecalculateGroup();
            _parent.OnRowChanged?.Invoke();
        }
    }
}
