// ═══════════════════════════════════════════════════════════════════════
// FILE: InvoiceFamilyRowViewModel.cs.cs
// PROJECT: ProGlassAutomation
// MODULE: Tax Invoice — list rows
//
// InvoiceFamilyRowViewModel.cs split out (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.TaxInvoice.TaxInvoiceFamily;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList
{
    public class InvoiceFamilyRowViewModel : ViewModelBase
    {
        // ── Identity ──────────────────────────────────────────────────────

        public string ProformaId { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }
        public int InvoiceCount { get; }

        // ── Financial aggregates ──────────────────────────────────────────

        public decimal ProformaTotal { get; }
        public decimal InvoicedTotal { get; }
        public decimal BalanceTotal { get; }
        public decimal TotalVat { get; }
        public int TotalQty { get; }
        public decimal TotalSqm { get; }

        // ── Display strings ───────────────────────────────────────────────

        public string ProformaTotalDisplay { get; }
        public string InvoicedTotalDisplay { get; }
        public string BalanceTotalDisplay { get; }

        public string FamilyStatus { get; }
        public string FamilyStatusBrushKey { get; }

        public double ProgressPercent { get; }
        public string ProgressDisplay { get; }

        public bool IsFullyPaid { get; }
        public bool HasBalance { get; }
        public bool HasIssuedInvoices { get; }

        /// <summary>
        /// NEW: True when family has at least one Draft invoice.
        /// Used by Draft tab filter.
        /// </summary>
        public bool AnyDraft { get; }

        /// <summary>
        /// NEW: True when family has at least one Issued invoice.
        /// Used by Issued tab filter.
        /// </summary>
        public bool AnyIssued { get; }

        /// <summary>
        /// NEW: True when ALL family members are Paid.
        /// Used by Paid tab filter.
        /// </summary>
        public bool AllPaid { get; }

        /// <summary>
        /// NEW: True when family has at least one Overdue invoice.
        /// Used by Overdue filter/display.
        /// </summary>
        public bool AnyOverdue { get; }

        // ── Latest invoice ────────────────────────────────────────────────

        public string LatestInvoiceNumber { get; }
        public int LatestInvoiceId { get; }
        public string LatestIssueDateDisplay { get; }
        public string LatestInvoiceStatus { get; }
        public string LatestStatusBrushKey { get; }

        // ── Family members (NEW) ──────────────────────────────────────────

        /// <summary>
        /// All invoices in this family (master + partials A/B/C).
        /// Ordered by SequenceNumber ASC.
        /// Used by detail sidebar to show family list.
        /// </summary>
        public List<TaxInvoiceRowViewModel> Members { get; }

        // ── Selection state ──────────────────────────────────────────────

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        // ── Constructor ───────────────────────────────────────────────────

        public InvoiceFamilyRowViewModel(TaxInvoiceFamily family)
        {
            ProformaId = family.ProformaId;
            CustomerName = string.IsNullOrWhiteSpace(family.CustomerName)
                ? "—" : family.CustomerName;
            ProjectName = string.IsNullOrWhiteSpace(family.ProjectName)
                ? "—" : family.ProjectName;
            InvoiceCount = family.MemberCount;

            ProformaTotal = family.ProformaTotal;
            InvoicedTotal = family.InvoicedTotal;
            BalanceTotal = family.BalanceTotal;
            TotalVat = family.TotalVat;
            TotalQty = family.TotalQty;
            TotalSqm = family.TotalSqm;

            ProformaTotalDisplay = $"AED {ProformaTotal:N2}";
            InvoicedTotalDisplay = $"AED {InvoicedTotal:N2}";
            BalanceTotalDisplay = $"AED {BalanceTotal:N2}";

            FamilyStatus = family.FamilyStatus;
            FamilyStatusBrushKey = ResolveFamilyStatusBrush(FamilyStatus);

            IsFullyPaid = family.IsFullyPaid;
            HasBalance = family.HasBalance;
            HasIssuedInvoices = family.HasIssuedInvoices;

            ProgressPercent = ProformaTotal > 0m
                ? Math.Min(100.0,
                    Math.Round((double)(InvoicedTotal / ProformaTotal) * 100, 1))
                : 0.0;

            ProgressDisplay =
                $"AED {InvoicedTotal:N2} of AED {ProformaTotal:N2}";

            var latest = family.LatestInvoice;
            LatestInvoiceNumber = family.LatestInvoiceNumber;
            LatestInvoiceId = latest?.Id ?? 0;
            LatestIssueDateDisplay = family.LatestIssueDate == default
                ? "—"
                : family.LatestIssueDate.ToString("dd MMM yyyy");
            LatestInvoiceStatus = latest?.Status ?? "Draft";
            LatestStatusBrushKey = ResolveStatusBrush(LatestInvoiceStatus);

            // ── Build Members collection from family ────────────────────
            Members = family.Members?
                .OrderBy(m => m.SequenceNumber)
                .Select(m => new TaxInvoiceRowViewModel(m))
                .ToList()
                ?? new List<TaxInvoiceRowViewModel>();

            // ── Compute tab-filtering flags ─────────────────────────────
            AnyDraft = Members.Any(m => m.IsDraft);
            // Use the family-level roll-up rather than the narrow row-level
            // Issued flag so PartiallyPaid families still count as already
            // billed/invoiced in tabs, KPIs and progress logic.
            AnyIssued = family.HasIssuedInvoices;
            AllPaid = family.IsFullyPaid;
            AnyOverdue = Members.Any(m => m.IsOverdue);
        }

        // ── Factory ───────────────────────────────────────────────────────

        public static IReadOnlyList<InvoiceFamilyRowViewModel> FromFamilies(
            IEnumerable<TaxInvoiceFamily>? families)
        {
            if (families == null)
                return Array.Empty<InvoiceFamilyRowViewModel>();

            return families
                .Select(f => new InvoiceFamilyRowViewModel(f))
                .ToList();
        }

        // ── Status brush resolvers ────────────────────────────────────────

        private static string ResolveFamilyStatusBrush(string status) =>
            status switch
            {
                "Paid" => "TintGreenBrush",
                "Overdue" => "TintRedBrush",
                "HasCancelled" => "TintRedBrush",
                "PartiallyInvoiced" => "TintAmberBrush",
                "PartiallyPaid" => "TintAmberBrush",
                "Draft" => "TintIndigoBrush",
                _ => "TintIndigoBrush"
            };

        private static string ResolveStatusBrush(string status) =>
            status switch
            {
                "Draft" => "TintIndigoBrush",
                "Issued" => "TintAmberBrush",
                "Paid" => "TintGreenBrush",
                "PartiallyPaid" => "TintAmberBrush",
                "Overdue" => "TintRedBrush",
                "Cancelled" => "TintRedBrush",
                "Void" => "TintRedBrush",
                _ => "TintIndigoBrush"
            };
    }
}
