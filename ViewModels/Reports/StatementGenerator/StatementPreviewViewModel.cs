// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementPreviewViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement — generator preview
//
// StatementPreviewViewModel split from the misleadingly-named StatementGeneratorViewModel2.cs
// (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Reconciliation;
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

        // ── CHAT 22 v3 — Reconciliation summaries (unique per PI) ─────────
        public ObservableCollection<OrderReconciliationSummary> ReconciliationSummaries { get; } = new();

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
        //  SELECTION STATE — CHAT 17
        // ═════════════════════════════════════════════════════════════════

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

        public bool HasSelection => _selectedCount > 0;
        public bool AllSelected => Rows.Count > 0 && _selectedCount == Rows.Count;
        public bool NoneSelected => _selectedCount == 0;

        // ═════════════════════════════════════════════════════════════════
        //  RECONCILIATION DISPLAY HELPERS (Chat 22 v3)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>True when at least one PI in the preview has reconciliation data.</summary>
        public bool HasAnyReconciliation => ReconciliationSummaries.Count > 0;

        /// <summary>True when ANY PI in the preview has over-invoicing. Dashboard alert flag.</summary>
        public bool HasAnyOverInvoicing =>
            ReconciliationSummaries.Any(s => s.HasOverInvoicing);

        /// <summary>True when ANY PI has any kind of discrepancy.</summary>
        public bool HasAnyDiscrepancy =>
            ReconciliationSummaries.Any(s => s.HasDiscrepancies);

        /// <summary>Count of PIs with over-invoicing (the critical bug).</summary>
        public int OverInvoicingCount =>
            ReconciliationSummaries.Count(s => s.HasOverInvoicing);

        /// <summary>Count of PIs with under-invoicing (revenue leakage).</summary>
        public int UnderInvoicingCount =>
            ReconciliationSummaries.Count(s => s.HasUnderInvoicing);

        /// <summary>Count of PIs with pending delivery balance.</summary>
        public int PendingDeliveryCount =>
            ReconciliationSummaries.Count(s => s.HasPendingDelivery);

        /// <summary>Count of PIs fully matched (green).</summary>
        public int FullyMatchedCount =>
            ReconciliationSummaries.Count(s => s.AllMatched);

        /// <summary>
        /// Compact header line for the reconciliation section.
        /// e.g. "5 orders  ·  3 matched  ·  1 OVER-INVOICED  ·  1 pending delivery"
        /// </summary>
        public string ReconciliationHeaderDisplay
        {
            get
            {
                int total = ReconciliationSummaries.Count;
                if (total == 0) return "No reconciliation data available";

                var parts = new List<string>
                {
                    $"{total} order{(total == 1 ? "" : "s")}"
                };

                if (FullyMatchedCount > 0)
                    parts.Add($"{FullyMatchedCount} matched");
                if (OverInvoicingCount > 0)
                    parts.Add($"{OverInvoicingCount} OVER-INVOICED");
                if (UnderInvoicingCount > 0)
                    parts.Add($"{UnderInvoicingCount} under-invoiced");
                if (PendingDeliveryCount > 0)
                    parts.Add($"{PendingDeliveryCount} pending delivery");

                return string.Join("  ·  ", parts);
            }
        }

        /// <summary>Header background color hex — red if any over-invoicing.</summary>
        public string ReconciliationHeaderColorHex
        {
            get
            {
                if (HasAnyOverInvoicing) return "#B91C1C";      // Red
                if (HasAnyDiscrepancy) return "#B45309";        // Amber
                if (HasAnyReconciliation) return "#047857";     // Green
                return "#6B7280";                                // Gray
            }
        }

        /// <summary>Header icon — matches worst status.</summary>
        public string ReconciliationHeaderIcon
        {
            get
            {
                if (HasAnyOverInvoicing) return "🔴";
                if (HasAnyDiscrepancy) return "🟡";
                if (HasAnyReconciliation) return "✅";
                return "⚪";
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  UPDATE FROM SNAPSHOT
        // ═════════════════════════════════════════════════════════════════

        public void UpdateFromSnapshot(StatementSnapshot? snapshot)
        {
            // Detach event handlers from OLD rows before clearing
            foreach (var oldRow in Rows)
                oldRow.SelectionChanged -= OnRowSelectionChanged;

            Rows.Clear();
            AgingBars.Clear();
            ReconciliationSummaries.Clear();   // Chat 22 v3

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

            // ── CHAT 22 v3 — Rebuild reconciliation summaries ─────────────
            RebuildReconciliationSummaries();

            // ── Recompute selection counters (all selected by default) ─────
            RecomputeSelection();

            // ── Batch notify ──────────────────────────────────────────────
            OnPropertyChanged(string.Empty);
            SelectionChanged?.Invoke();
        }

        // ═════════════════════════════════════════════════════════════════
        //  RECONCILIATION AGGREGATION (Chat 22 v3)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Extracts unique reconciliation summaries from the preview rows.
        /// Multiple invoices sharing the same ProformaId contribute ONE
        /// summary. Sort order:
        ///   1. Over-invoiced (RED) first  ← user's attention needed
        ///   2. Under-invoiced (BLUE)
        ///   3. Pending delivery (AMBER)
        ///   4. Fully matched (GREEN)
        ///   5. Not started (GRAY)
        ///   Alphabetical within same status.
        /// </summary>
        private void RebuildReconciliationSummaries()
        {
            ReconciliationSummaries.Clear();

            if (Rows.Count == 0) return;

            // Extract unique reconciliation summaries by ProformaId
            var uniqueSummaries = Rows
                .Where(r => r.Reconciliation != null)
                .Select(r => r.Reconciliation!)
                .GroupBy(s => s.ProformaId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            // Sort: worst status first (over-invoiced grabs attention)
            var sorted = uniqueSummaries
                .OrderBy(s => StatusSortOrder(s.OverallStatus))
                .ThenBy(s => s.ProformaId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var summary in sorted)
                ReconciliationSummaries.Add(summary);
        }

        /// <summary>Sort priority — lower = more urgent = shown first.</summary>
        private static int StatusSortOrder(ReconciliationStatus status) => status switch
        {
            ReconciliationStatus.OverInvoiced => 1,  // Most critical
            ReconciliationStatus.UnderInvoiced => 2,
            ReconciliationStatus.OverDelivered => 3,
            ReconciliationStatus.PartialDelivery => 4,
            ReconciliationStatus.FullyMatched => 5,
            ReconciliationStatus.NotYetStarted => 6,
            _ => 7
        };

        // ═════════════════════════════════════════════════════════════════
        //  SELECTION COMMANDS
        // ═════════════════════════════════════════════════════════════════

        public void SelectAll()
        {
            foreach (var row in Rows)
                row.IsSelectedSilent = true;

            RecomputeSelection();
            OnPropertyChanged(string.Empty);
            SelectionChanged?.Invoke();
        }

        public void DeselectAll()
        {
            foreach (var row in Rows)
                row.IsSelectedSilent = false;

            RecomputeSelection();
            OnPropertyChanged(string.Empty);
            SelectionChanged?.Invoke();
        }

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
}
