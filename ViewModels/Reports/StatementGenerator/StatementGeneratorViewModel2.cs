// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel2.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// PURPOSE:
//   Part 2 of 2 — Preview results display + financial summary KPIs.
//   Wraps StatementSnapshot into ObservableCollection<PreviewRowViewModel>
//   for the preview ListView in StatementGeneratorView.
//
//   Observes StatementGeneratorViewModel.PreviewSnapshot via property
//   reference — updated by GeneratorVM after successful preview run.
//
//   AGING DISPLAY:
//     5 AgingBarViewModel items — bound to ItemsControl for custom bars.
//     Width computed as % of total outstanding (PercentToWidthConverter).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProGlassAutomation.ViewModels.Reports.StatementGenerator
{
    public class StatementPreviewViewModel : ViewModelBase
    {
        // ── Preview rows ──────────────────────────────────────────────────
        public ObservableCollection<PreviewRowViewModel> Rows { get; } = new();

        // ── Aging bars ────────────────────────────────────────────────────
        public ObservableCollection<AgingBarViewModel> AgingBars { get; } = new();

        // ── Financial KPIs ────────────────────────────────────────────────
        private decimal _totalInvoiced;
        private decimal _totalVat;
        private decimal _totalPaid;
        private decimal _totalOutstanding;
        private decimal _overdueAmount;
        private int _invoiceCount;
        private int _totalQty;
        private decimal _totalSqm;

        public string TotalInvoicedDisplay
            => $"AED {_totalInvoiced:N2}";
        public string TotalVatDisplay
            => $"AED {_totalVat:N2}";
        public string TotalPaidDisplay
            => $"AED {_totalPaid:N2}";
        public string TotalOutstandingDisplay
            => $"AED {_totalOutstanding:N2}";
        public string OverdueAmountDisplay
            => _overdueAmount > 0 ? $"AED {_overdueAmount:N2}" : "None";
        public int InvoiceCount => _invoiceCount;
        public int TotalQty => _totalQty;
        public string TotalSqmDisplay => $"{_totalSqm:N3} m²";
        public bool HasOverdue => _overdueAmount > 0;
        public bool HasRows => Rows.Count > 0;

        // ── Outstanding color ─────────────────────────────────────────────
        public string OutstandingColorKey
        {
            get
            {
                if (_overdueAmount > 0) return "Red";
                if (_totalOutstanding > 0) return "Amber";
                return "Green";
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  UPDATE FROM SNAPSHOT
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by StatementGeneratorViewModel after preview completes.
        /// Rebuilds all observable collections from the new snapshot.
        /// Must be called on UI thread.
        /// </summary>
        public void UpdateFromSnapshot(StatementSnapshot? snapshot)
        {
            Rows.Clear();
            AgingBars.Clear();

            if (snapshot == null)
            {
                _totalInvoiced = 0;
                _totalVat = 0;
                _totalPaid = 0;
                _totalOutstanding = 0;
                _overdueAmount = 0;
                _invoiceCount = 0;
                _totalQty = 0;
                _totalSqm = 0;
                OnPropertyChanged(string.Empty);
                return;
            }

            // ── Preview rows ──────────────────────────────────────────────
            foreach (var row in snapshot.InvoiceRows)
                Rows.Add(new PreviewRowViewModel(row));

            // ── Financial KPIs ────────────────────────────────────────────
            var fin = snapshot.Financial;
            _totalInvoiced = fin.TotalInvoiced;
            _totalVat = fin.TotalVat;
            _totalPaid = fin.TotalPaid;
            _totalOutstanding = fin.TotalOutstanding;
            _overdueAmount = fin.OverdueAmount;
            _invoiceCount = fin.TotalInvoiceCount;
            _totalQty = fin.TotalQty;
            _totalSqm = fin.TotalSqm;

            // ── Aging bars ────────────────────────────────────────────────
            var aging = snapshot.Aging;
            decimal total = aging.Total > 0 ? aging.Total : 1m;

            AgingBars.Add(new AgingBarViewModel(
                "Current", aging.Current, total, "#047857"));
            AgingBars.Add(new AgingBarViewModel(
                "0-30 Days", aging.Days0To30, total, "#B45309"));
            AgingBars.Add(new AgingBarViewModel(
                "31-60 Days", aging.Days31To60, total, "#B45309"));
            AgingBars.Add(new AgingBarViewModel(
                "61-90 Days", aging.Days61To90, total, "#B91C1C"));
            AgingBars.Add(new AgingBarViewModel(
                "90+ Days", aging.Days90Plus, total, "#B91C1C"));

            // ── Batch notify ──────────────────────────────────────────────
            OnPropertyChanged(string.Empty);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  PREVIEW ROW VIEWMODEL
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// One row in the preview invoice list.
    /// Wraps StatementInvoiceRow for XAML binding.
    /// </summary>
    public class PreviewRowViewModel : ViewModelBase
    {
        private readonly StatementInvoiceRow _row;

        public PreviewRowViewModel(StatementInvoiceRow row)
        {
            _row = row;
        }

        public string InvoiceNumber => _row.InvoiceNumber;
        public string ProformaId => _row.ProformaId;
        public string DeliveryNoteRef
            => string.IsNullOrWhiteSpace(_row.DeliveryNoteRef)
               ? "—" : _row.DeliveryNoteRef;
        public string InvoiceType => _row.InvoiceType;
        public string Status => _row.Status;
        public string IssueDateDisplay => _row.IssueDateDisplay;
        public string DueDateDisplay => _row.DueDateDisplay;
        public int TotalQty => _row.TotalQty;
        public string TotalSqmDisplay
            => $"{_row.TotalSqm:N3} m²";
        public string GrandTotalDisplay
            => $"AED {_row.GrandTotal:N2}";
        public string BalanceDisplay
            => _row.Balance > 0 ? $"AED {_row.Balance:N2}" : "PAID";
        public bool IsOverdue => _row.IsOverdue;
        public bool IsPaid => _row.Balance <= 0;
        public bool HasSignedPi => _row.HasSignedPi;
        public bool HasSignedDn => _row.HasSignedDn;
        public int AttachmentCount => _row.AttachmentCount;
        public bool HasAttachments => _row.AttachmentCount > 0;
        public int DaysOverdue => _row.DaysOverdue;

        // Display helpers
        public string DaysOverdueDisplay =>
            _row.DaysOverdue > 0 ? $"{_row.DaysOverdue}d overdue" : "";

        public string StatusColor => _row.Status switch
        {
            "Paid" => "#047857",
            "PartiallyPaid" => "#047857",
            "Issued" => "#B45309",
            "Overdue" => "#B91C1C",
            "Cancelled" => "#B91C1C",
            "Void" => "#B91C1C",
            _ => "#475569"
        };

        public string BalanceColor =>
            _row.Balance <= 0 ? "#047857" :
            _row.IsOverdue ? "#B91C1C" : "#B45309";
    }

    // ═════════════════════════════════════════════════════════════════════
    //  AGING BAR VIEWMODEL
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// One bar in the aging analysis visual.
    /// WidthPercent drives the bar width via PercentToWidthConverter.
    /// </summary>
    public class AgingBarViewModel : ViewModelBase
    {
        public string Label { get; }
        public decimal Amount { get; }
        public decimal WidthPercent { get; }
        public string Color { get; }
        public bool HasAmount => Amount > 0;
        public string AmountDisplay
            => Amount > 0 ? $"AED {Amount:N0}" : "—";
        public string PercentDisplay
            => WidthPercent > 0
               ? $"{WidthPercent:0.#}%"
               : "";

        public AgingBarViewModel(
            string label, decimal amount, decimal total, string color)
        {
            Label = label;
            Amount = amount;
            Color = color;
            WidthPercent = total > 0
                ? Math.Round(amount / total * 100m, 1)
                : 0m;
        }
    }
}