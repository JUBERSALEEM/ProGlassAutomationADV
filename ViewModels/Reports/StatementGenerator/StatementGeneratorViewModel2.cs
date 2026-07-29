// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel2.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16 + Chat 17 workflow redesign)
//
// PURPOSE:
//   Part 2 of 2 — Preview results + selection + aging + KPIs.
//
// CHAT 17 ADDITIONS:
//   1. PreviewRowViewModel.IsSelected — bool, default TRUE
//      Notifies parent VM when changed → live selection counts update
//   2. StatementPreviewViewModel.SelectionChanged event
//      Parent VM subscribes to update counters
//   3. Selection counters:
//        SelectedCount            — how many invoices checked
//        SelectedInvoicedDisplay  — sum of GrandTotal of selected
//        SelectedOutstandingDisplay — sum of Balance of selected
//        SelectedAttachmentCount  — total attachments to be merged
//        SelectionSummaryDisplay  — "3 of 5 invoices · 6 attachments"
//   4. Helper methods:
//        SelectAll()   — check all rows
//        DeselectAll() — uncheck all rows
//        GetSelectedInvoiceNumbers() — for PDF export filter
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement;
using System;
using System.Collections.Generic;
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

        // ── Financial KPIs (all invoices in preview) ──────────────────────
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

        // ── Outstanding color key ─────────────────────────────────────────
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
        //  SELECTION STATE — CHAT 17 NEW
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fired whenever any row's IsSelected changes.
        /// StatementGeneratorViewModel subscribes to update its
        /// CanExecute state on the Generate command.
        /// </summary>
        public event Action? SelectionChanged;

        private int _selectedCount;
        private decimal _selectedInvoicedAmount;
        private decimal _selectedOutstandingAmount;
        private int _selectedAttachmentCount;

        public int SelectedCount => _selectedCount;

        public string SelectedInvoicedDisplay
            => $"AED {_selectedInvoicedAmount:N2}";

        public string SelectedOutstandingDisplay
            => $"AED {_selectedOutstandingAmount:N2}";

        public int SelectedAttachmentCount => _selectedAttachmentCount;

        /// <summary>
        /// Human-readable summary shown in the selection toolbar.
        /// Example: "3 of 5 invoices selected · 6 attachments to include"
        /// </summary>
        public string SelectionSummaryDisplay
        {
            get
            {
                int total = Rows.Count;
                if (total == 0) return "No invoices to select";

                string invoicePart = _selectedCount == total
                    ? $"All {total} invoices selected"
                    : $"{_selectedCount} of {total} invoices selected";

                if (_selectedAttachmentCount > 0)
                    return $"{invoicePart}  ·  " +
                           $"{_selectedAttachmentCount} attachment(s) to include";

                return invoicePart;
            }
        }

        /// <summary>True when at least one invoice is selected.</summary>
        public bool HasSelection => _selectedCount > 0;

        /// <summary>True when all rows are selected (for Select All toggle).</summary>
        public bool AllSelected
            => Rows.Count > 0 && _selectedCount == Rows.Count;

        /// <summary>True when NO rows are selected.</summary>
        public bool NoneSelected => _selectedCount == 0;

        // ═════════════════════════════════════════════════════════════════
        //  UPDATE FROM SNAPSHOT
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by StatementGeneratorViewModel after preview completes.
        /// Rebuilds all observable collections from the new snapshot.
        /// All rows start SELECTED by default (senior ERP UX standard).
        /// Must be called on UI thread.
        /// </summary>
        public void UpdateFromSnapshot(StatementSnapshot? snapshot)
        {
            // Detach event handlers from OLD rows before clearing
            foreach (var oldRow in Rows)
                oldRow.SelectionChanged -= OnRowSelectionChanged;

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
                _selectedCount = 0;
                _selectedInvoicedAmount = 0;
                _selectedOutstandingAmount = 0;
                _selectedAttachmentCount = 0;
                OnPropertyChanged(string.Empty);
                SelectionChanged?.Invoke();
                return;
            }

            // ── Build preview rows (all pre-selected) ─────────────────────
            foreach (var row in snapshot.InvoiceRows)
            {
                var vm = new PreviewRowViewModel(row) { IsSelected = true };
                vm.SelectionChanged += OnRowSelectionChanged;
                Rows.Add(vm);
            }

            // ── Financial KPIs (all rows) ─────────────────────────────────
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

            // ── Recompute selection counters (all selected by default) ─────
            RecomputeSelection();

            // ── Batch notify ──────────────────────────────────────────────
            OnPropertyChanged(string.Empty);
            SelectionChanged?.Invoke();
        }

        // ═════════════════════════════════════════════════════════════════
        //  SELECTION COMMANDS (invoked by view/parent VM)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>Marks every row as selected.</summary>
        public void SelectAll()
        {
            foreach (var row in Rows)
                row.IsSelectedSilent = true;

            RecomputeSelection();
            OnPropertyChanged(string.Empty);
            SelectionChanged?.Invoke();
        }

        /// <summary>Marks every row as deselected.</summary>
        public void DeselectAll()
        {
            foreach (var row in Rows)
                row.IsSelectedSilent = false;

            RecomputeSelection();
            OnPropertyChanged(string.Empty);
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Returns invoice numbers of currently selected rows.
        /// Used by StatementPdfExporter to filter which invoices to include.
        /// </summary>
        public List<string> GetSelectedInvoiceNumbers()
        {
            return Rows
                .Where(r => r.IsSelected)
                .Select(r => r.InvoiceNumber)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
        }

        // ═════════════════════════════════════════════════════════════════
        //  INTERNAL — event handler + counter recomputation
        // ═════════════════════════════════════════════════════════════════

        private void OnRowSelectionChanged()
        {
            RecomputeSelection();
            OnPropertyChanged(string.Empty);
            SelectionChanged?.Invoke();
        }

        private void RecomputeSelection()
        {
            var selected = Rows.Where(r => r.IsSelected).ToList();

            _selectedCount = selected.Count;
            _selectedInvoicedAmount = selected.Sum(r => r.RawGrandTotal);
            _selectedOutstandingAmount = selected.Sum(r => r.RawBalance);
            _selectedAttachmentCount = selected.Sum(r => r.AttachmentCount);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  PREVIEW ROW VIEWMODEL
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// One row in the preview invoice list.
    /// Wraps StatementInvoiceRow for XAML binding.
    ///
    /// CHAT 17 ADDITIONS:
    ///   • IsSelected — two-way bindable, default TRUE
    ///   • IsSelectedSilent — updates without firing event (for bulk ops)
    ///   • SelectionChanged event — notifies parent VM on change
    ///   • RawGrandTotal / RawBalance — decimal accessors for summation
    /// </summary>
    public class PreviewRowViewModel : ViewModelBase
    {
        private readonly StatementInvoiceRow _row;

        public PreviewRowViewModel(StatementInvoiceRow row)
        {
            _row = row;
        }

        // ═════════════════════════════════════════════════════════════════
        //  SELECTION (Chat 17)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fires when IsSelected changes.
        /// Parent StatementPreviewViewModel subscribes to update counts.
        /// </summary>
        public event Action? SelectionChanged;

        private bool _isSelected = true;

        /// <summary>
        /// Two-way bindable selection state.
        /// Setting this fires SelectionChanged event → parent recalculates.
        /// Use IsSelectedSilent for bulk operations (Select All / None).
        /// </summary>
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

        /// <summary>
        /// Sets IsSelected WITHOUT firing SelectionChanged.
        /// Used by SelectAll() / DeselectAll() to avoid N event fires.
        /// The bulk operation manually calls RecomputeSelection() once.
        /// </summary>
        public bool IsSelectedSilent
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  DISPLAY PROPERTIES (all read-only, wrap the row model)
        // ═════════════════════════════════════════════════════════════════

        public string InvoiceNumber => _row.InvoiceNumber ?? "";
        public string ProformaId => _row.ProformaId ?? "";

        public string DeliveryNoteRef
            => string.IsNullOrWhiteSpace(_row.DeliveryNoteRef)
               ? "—" : _row.DeliveryNoteRef;

        public string InvoiceType => _row.InvoiceType ?? "";
        public string Status => _row.Status ?? "";
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

        public string DaysOverdueDisplay =>
            _row.DaysOverdue > 0 ? $"{_row.DaysOverdue}d overdue" : "";

        // ── Raw numeric accessors (used by parent for summing) ────────────
        public decimal RawGrandTotal => _row.GrandTotal;
        public decimal RawBalance => _row.Balance;

        // ── Attachment badge for UI display ───────────────────────────────
        public string AttachmentBadge
            => _row.AttachmentCount > 0
               ? $"📎 {_row.AttachmentCount}"
               : "";

        // ── Colors ────────────────────────────────────────────────────────

        public string StatusColor => (_row.Status ?? "") switch
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