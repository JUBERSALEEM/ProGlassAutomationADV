using ProGlassAutomation.Models.ProformaInvoice.Step7PrintPreview.InvoiceMainSummary;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.InvoiceMainSummary
{
    /// <summary>
    /// Financial summary card at the top of Step 7 (Print Preview).
    /// Aggregates glass materials subtotal from Step 5, other charges from Step 6,
    /// applies user-entered Discount (% or AED), applies UAE VAT (default 5%, toggleable),
    /// and produces the final Grand Total (VAT Inclusive) shown on the printed PI.
    /// </summary>
    public class InvoiceMainSummaryViewModel : ViewModelBase
    {
        private readonly DataGridViewModel _dataGridVM;
        private readonly OtherChargesViewModel _otherChargesVM;

        public string Title { get; } = "INVOICE MAIN SUMMARY";
        public string Subtitle { get; } = "Final financial breakdown — glass materials + other charges + discount + VAT.";

        // ── Inputs (from other steps — read-only here) ────────────────────

        private decimal _glassMaterialsSubtotal;
        public decimal GlassMaterialsSubtotal
        {
            get => _glassMaterialsSubtotal;
            private set { if (_glassMaterialsSubtotal == value) return; _glassMaterialsSubtotal = value; OnPropertyChanged(); Recalculate(); }
        }

        private decimal _otherChargesSubtotal;
        public decimal OtherChargesSubtotal
        {
            get => _otherChargesSubtotal;
            private set { if (_otherChargesSubtotal == value) return; _otherChargesSubtotal = value; OnPropertyChanged(); Recalculate(); }
        }

        // ── Discount ──────────────────────────────────────────────────────

        public List<DiscountType> DiscountTypeOptions { get; } = new()
        {
            DiscountType.Percentage,
            DiscountType.FixedAmount
        };

        private DiscountType _selectedDiscountType = DiscountType.Percentage;
        public DiscountType SelectedDiscountType
        {
            get => _selectedDiscountType;
            set
            {
                if (_selectedDiscountType == value) return;
                _selectedDiscountType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DiscountUnitLabel));
                Recalculate();
            }
        }

        /// <summary>Discount value — interpreted as % if Percentage, or AED if FixedAmount.</summary>
        private decimal _discountValue;
        public decimal DiscountValue
        {
            get => _discountValue;
            set
            {
                if (_discountValue == value) return;
                _discountValue = value < 0 ? 0 : value;
                OnPropertyChanged();
                Recalculate();
            }
        }

        /// <summary>UI hint next to discount input: "%" or "AED".</summary>
        public string DiscountUnitLabel => _selectedDiscountType == DiscountType.Percentage ? "%" : "AED";

        // ── VAT ────────────────────────────────────────────────────────────

        private bool _isVatApplicable = true;
        public bool IsVatApplicable
        {
            get => _isVatApplicable;
            set { if (_isVatApplicable == value) return; _isVatApplicable = value; OnPropertyChanged(); Recalculate(); }
        }

        private decimal _vatPercent = 5m;
        public decimal VatPercent
        {
            get => _vatPercent;
            set
            {
                if (_vatPercent == value) return;
                _vatPercent = value < 0 ? 0 : value;
                OnPropertyChanged();
                Recalculate();
            }
        }

        // ── Computed values (read-only) ───────────────────────────────────

        private decimal _netSubtotal;
        public decimal NetSubtotal
        {
            get => _netSubtotal;
            private set { if (_netSubtotal == value) return; _netSubtotal = value; OnPropertyChanged(); }
        }

        private decimal _discountAmount;
        public decimal DiscountAmount
        {
            get => _discountAmount;
            private set { if (_discountAmount == value) return; _discountAmount = value; OnPropertyChanged(); }
        }

        private decimal _subtotalBeforeVat;
        public decimal SubtotalBeforeVat
        {
            get => _subtotalBeforeVat;
            private set { if (_subtotalBeforeVat == value) return; _subtotalBeforeVat = value; OnPropertyChanged(); }
        }

        private decimal _vatAmount;
        public decimal VatAmount
        {
            get => _vatAmount;
            private set { if (_vatAmount == value) return; _vatAmount = value; OnPropertyChanged(); }
        }

        private decimal _grandTotalVatInclusive;
        public decimal GrandTotalVatInclusive
        {
            get => _grandTotalVatInclusive;
            private set { if (_grandTotalVatInclusive == value) return; _grandTotalVatInclusive = value; OnPropertyChanged(); }
        }

        // ── Constructor ───────────────────────────────────────────────────

        public InvoiceMainSummaryViewModel(DataGridViewModel dataGridVM, OtherChargesViewModel otherChargesVM)
        {
            _dataGridVM = dataGridVM ?? throw new ArgumentNullException(nameof(dataGridVM));
            _otherChargesVM = otherChargesVM ?? throw new ArgumentNullException(nameof(otherChargesVM));

            // Subscribe to upstream changes
            _dataGridVM.SummaryViewModel.PropertyChanged += OnDataGridSummaryChanged;
            _otherChargesVM.PropertyChanged += OnOtherChargesChanged;

            // Prime initial values
            SyncFromDataGrid();
            SyncFromOtherCharges();
        }

        // ── Change tracking ───────────────────────────────────────────────

        private void OnDataGridSummaryChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_dataGridVM.SummaryViewModel.GrandTotalValue))
                SyncFromDataGrid();
        }

        private void OnOtherChargesChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OtherChargesViewModel.TotalOtherCharges))
                SyncFromOtherCharges();
        }

        private void SyncFromDataGrid()
        {
            GlassMaterialsSubtotal = _dataGridVM.SummaryViewModel.GrandTotalValue;
        }

        private void SyncFromOtherCharges()
        {
            OtherChargesSubtotal = _otherChargesVM.TotalOtherCharges;
        }

        // ── Core calculation ──────────────────────────────────────────────

        /// <summary>
        /// Recomputes the full financial waterfall:
        ///
        ///   NetSubtotal        = GlassMaterialsSubtotal + OtherChargesSubtotal
        ///   DiscountAmount     = NetSubtotal * (DiscountValue/100)  OR  DiscountValue
        ///                        (capped so subtotal never goes negative)
        ///   SubtotalBeforeVat  = NetSubtotal - DiscountAmount
        ///   VatAmount          = SubtotalBeforeVat * (VatPercent/100)  OR  0 if VAT disabled
        ///   GrandTotal         = SubtotalBeforeVat + VatAmount
        /// </summary>
        private void Recalculate()
        {
            decimal net = GlassMaterialsSubtotal + OtherChargesSubtotal;

            decimal discount = SelectedDiscountType switch
            {
                DiscountType.Percentage => Math.Round(net * (DiscountValue / 100m), 2),
                DiscountType.FixedAmount => DiscountValue,
                _ => 0m
            };

            // Cap discount so subtotal never goes negative
            if (discount > net) discount = net;

            decimal beforeVat = net - discount;
            decimal vat = IsVatApplicable ? Math.Round(beforeVat * (VatPercent / 100m), 2) : 0m;
            decimal grand = beforeVat + vat;

            NetSubtotal = Math.Round(net, 2);
            DiscountAmount = Math.Round(discount, 2);
            SubtotalBeforeVat = Math.Round(beforeVat, 2);
            VatAmount = vat;
            GrandTotalVatInclusive = Math.Round(grand, 2);
        }
    }
}