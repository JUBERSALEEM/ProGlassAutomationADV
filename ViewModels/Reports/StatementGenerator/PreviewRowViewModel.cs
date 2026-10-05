// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/PreviewRowViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement — generator preview
//
// PreviewRowViewModel split from the misleadingly-named StatementGeneratorViewModel2.cs
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
    public class PreviewRowViewModel : ViewModelBase
    {
        private readonly StatementInvoiceRow _row;

        public PreviewRowViewModel(StatementInvoiceRow row)
        {
            _row = row;
        }

        // ═════════════════════════════════════════════════════════════════
        //  SELECTION
        // ═════════════════════════════════════════════════════════════════

        public event Action? SelectionChanged;

        private bool _isSelected = true;

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
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  DISPLAY PROPERTIES (wrap the row model)
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

        // ═════════════════════════════════════════════════════════════════
        //  RECONCILIATION PASS-THROUGH (Chat 22 v3)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Full reconciliation summary for this invoice's PI family.
        /// Null if backend didn't compute reconciliation for this row
        /// (missing PI data, deleted PI, or empty ProformaId).
        /// </summary>
        public OrderReconciliationSummary? Reconciliation => _row.Reconciliation;

        /// <summary>True when reconciliation data is available for display.</summary>
        public bool HasReconciliation => _row.HasReconciliation;

        /// <summary>True when this row's PI has any billing discrepancy.</summary>
        public bool HasReconciliationIssue => _row.HasReconciliationIssue;

        /// <summary>
        /// True when this row's PI is OVER-INVOICED (billed more than delivered).
        /// ★ THE CRITICAL FLAG — use for red highlighting in the invoice grid.
        /// </summary>
        public bool IsOverInvoiced => _row.IsOverInvoiced;

        /// <summary>
        /// Compact status icon for showing next to the invoice in the grid.
        /// Empty string if no reconciliation data.
        /// </summary>
        public string ReconciliationBadgeIcon
        {
            get
            {
                if (!HasReconciliation) return "";
                return Reconciliation!.OverallIcon;
            }
        }

        /// <summary>
        /// Reconciliation status color hex — for tinting rows/badges.
        /// Returns transparent-neutral if no reconciliation data.
        /// </summary>
        public string ReconciliationColorHex
        {
            get
            {
                if (!HasReconciliation) return "#00000000";  // Transparent
                return Reconciliation!.OverallColorHex;
            }
        }

        /// <summary>
        /// Hover tooltip text summarizing the reconciliation.
        /// e.g. "PI-2026-001 — 1 OVER-INVOICED · Ordered: 2, Delivered: 1, Invoiced: 2"
        /// </summary>
        public string ReconciliationTooltip
        {
            get
            {
                if (!HasReconciliation) return "";

                var s = Reconciliation!;
                return $"{s.ProformaId}\n" +
                       $"{s.SummaryDisplay}\n" +
                       $"{s.TotalsDisplay}";
            }
        }
    }
}
