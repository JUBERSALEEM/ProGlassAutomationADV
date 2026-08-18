// ═══════════════════════════════════════════════════════════════════════
// FILE: JobOrderGroupRowViewModel.cs.cs
// PROJECT: ProGlassAutomation
// MODULE: Delivery Note — list rows
//
// JobOrderGroupRowViewModel.cs split out (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus;
using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryType;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList
{
    public class JobOrderGroupRowViewModel
    {
        // Shared JO context
        public string JobOrderId { get; }
        public string ProformaId { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }

        // Family stats — qty
        public int DnCount { get; }
        public int TotalOrdered { get; }
        public int TotalDelivered { get; }
        public int TotalBalance { get; }

        // Family stats — SQM
        public decimal TotalOrderedSqm { get; }
        public decimal TotalDeliveredSqm { get; }
        public decimal TotalBalanceSqm { get; }

        // Display strings
        public string QtySummaryDisplay { get; }
        public string SqmSummaryDisplay { get; }

        public bool AllDelivered { get; }
        public bool AnyDelivered { get; }
        public string StatusLabel { get; }
        public double ProgressPercent { get; }
        public string ProgressDisplay { get; }

        // Latest split
        public string LatestDnId { get; }
        public int LatestDnDbId { get; }
        public DateTime LatestDeliveryDate { get; }
        public string LatestDeliveryDateDisplay { get; }
        public string LatestDriverName { get; }
        public int LatestSequenceNumber { get; }

        public bool HasFoc { get; }
        public bool HasRework { get; }
        public bool HasReturn { get; }

        // ── CHAT 19 INVOICED ─────────────────────────────────────────────

        /// <summary>
        /// True when AT LEAST ONE split DN in this group is invoiced.
        /// Shows "🧾 PARTIAL INVOICE" badge on group row.
        /// </summary>
        public bool HasAnyInvoiced { get; }

        /// <summary>
        /// True when ALL dispatched/delivered split DNs are invoiced.
        /// Shows "✅ FULLY INVOICED" badge on group row.
        /// Only counts DNs that are eligible (dispatched or later).
        /// </summary>
        public bool IsFullyInvoiced { get; }

        /// <summary>
        /// Count of DNs in this group that are invoiced.
        /// </summary>
        public int InvoicedCount { get; }

        /// <summary>
        /// Count of DNs that are eligible for invoicing
        /// (dispatched/delivered/signed/received).
        /// </summary>
        public int EligibleForInvoiceCount { get; }

        /// <summary>
        /// e.g. "2 of 3 invoiced" — shown in group row tooltip/badge.
        /// </summary>
        public string InvoicedCountDisplay { get; }

        /// <summary>
        /// Badge text for group row:
        ///   "✅ FULLY INVOICED"   — when IsFullyInvoiced = true
        ///   "🧾 PARTIAL INVOICE"  — when HasAnyInvoiced but not all
        ///   ""                    — when no invoices exist
        /// </summary>
        public string InvoicedBadgeText { get; }

        /// <summary>
        /// Badge background key for XAML:
        ///   "TintGreenBrush"  — fully invoiced
        ///   "TintIndigoBrush" — partially invoiced
        ///   ""                — not invoiced (badge hidden)
        /// </summary>
        public string InvoicedBadgeBrushKey { get; }

        // ── Factory ───────────────────────────────────────────────────────

        public static IReadOnlyList<JobOrderGroupRowViewModel> GroupFrom(
            IEnumerable<DeliveryNoteRowViewModel>? source,
            Dictionary<string, JobOrderSqmSummary>? sqmCache = null)
        {
            if (source == null)
                return Array.Empty<JobOrderGroupRowViewModel>();

            var cache = sqmCache
                ?? new Dictionary<string, JobOrderSqmSummary>();

            return source
                .Where(x => !string.IsNullOrWhiteSpace(x.JobOrderId)
                         && !x.IsMasterDn)
                .GroupBy(x => x.JobOrderId,
                         StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    cache.TryGetValue(g.Key, out var sqm);
                    return new JobOrderGroupRowViewModel(
                        g.Key, g.ToList(), sqm);
                })
                .OrderByDescending(g => g.LatestDeliveryDate)
                .ThenByDescending(g => g.LatestSequenceNumber)
                .ToList();
        }

        // ── Constructor ───────────────────────────────────────────────────

        private JobOrderGroupRowViewModel(
            string jobOrderId,
            List<DeliveryNoteRowViewModel> family,
            JobOrderSqmSummary? sqm)
        {
            JobOrderId = jobOrderId;
            DnCount = family.Count;

            var latest = family
                .OrderByDescending(x => x.SequenceNumber)
                .ThenByDescending(x => x.DeliveryDate)
                .First();

            LatestDnId = latest.DeliveryNoteId;
            LatestDnDbId = latest.Id;
            LatestDeliveryDate = latest.DeliveryDate;
            LatestDeliveryDateDisplay = latest.DeliveryDateDisplay;
            LatestDriverName = string.IsNullOrWhiteSpace(latest.DriverName)
                ? "—" : latest.DriverName;
            LatestSequenceNumber = latest.SequenceNumber;

            ProformaId = latest.ProformaId;
            CustomerName = latest.CustomerName;
            ProjectName = latest.ProjectName;

            TotalOrdered = family.Max(x => x.TotalOrderedQty);
            TotalDelivered = family.Sum(x => x.TotalDeliveredQty);
            TotalBalance = Math.Max(0, TotalOrdered - TotalDelivered);

            TotalOrderedSqm = sqm?.OrderedSqm ?? 0m;
            TotalDeliveredSqm = sqm?.DeliveredSqm ?? 0m;
            TotalBalanceSqm = sqm?.BalanceSqm ?? 0m;

            QtySummaryDisplay =
                $"{TotalOrdered} ordered  ·  " +
                $"{TotalDelivered} delivered  ·  " +
                $"{TotalBalance} balance";

            SqmSummaryDisplay =
                $"{TotalOrderedSqm:0.000} SQM  ·  " +
                $"{TotalDeliveredSqm:0.000} del  ·  " +
                $"{TotalBalanceSqm:0.000} bal";

            AllDelivered = family.All(x =>
                string.Equals(x.Status, "Delivered",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, "Signed",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, "Received",
                    StringComparison.OrdinalIgnoreCase));

            AnyDelivered = family.Any(x =>
                string.Equals(x.Status, "Dispatched",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, "Delivered",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, "Signed",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, "Received",
                    StringComparison.OrdinalIgnoreCase));

            StatusLabel = AllDelivered ? "Completed" : "In Progress";

            ProgressPercent = TotalOrdered > 0
                ? Math.Round((double)TotalDelivered / TotalOrdered * 100, 1)
                : 0;
            ProgressDisplay = $"{TotalDelivered} / {TotalOrdered} pcs";

            HasFoc = family.Any(x => x.IsFoc);
            HasRework = family.Any(x =>
                string.Equals(x.DeliveryType, "Rework",
                    StringComparison.OrdinalIgnoreCase));
            HasReturn = family.Any(x =>
                string.Equals(x.DeliveryType, "Return",
                    StringComparison.OrdinalIgnoreCase));

            // ── CHAT 19 INVOICED: invoice state aggregation ───────────────

            // Eligible = dispatched or later (invoice-worthy statuses)
            var eligibleDns = family.Where(x =>
                string.Equals(x.Status, "Dispatched",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, "Delivered",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, "Signed",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, "Received",
                    StringComparison.OrdinalIgnoreCase)).ToList();

            EligibleForInvoiceCount = eligibleDns.Count;
            InvoicedCount = eligibleDns.Count(x => x.HasTaxInvoice);

            HasAnyInvoiced = InvoicedCount > 0;

            // Fully invoiced = all eligible DNs are invoiced
            // AND at least one eligible DN exists
            IsFullyInvoiced = EligibleForInvoiceCount > 0
                && InvoicedCount == EligibleForInvoiceCount;

            InvoicedCountDisplay = EligibleForInvoiceCount > 0
                ? $"{InvoicedCount} of {EligibleForInvoiceCount} invoiced"
                : "";

            // Badge text + brush
            if (IsFullyInvoiced)
            {
                InvoicedBadgeText = "✅ FULLY INVOICED";
                InvoicedBadgeBrushKey = "TintGreenBrush";
            }
            else if (HasAnyInvoiced)
            {
                InvoicedBadgeText = "🧾 PARTIAL INVOICE";
                InvoicedBadgeBrushKey = "TintIndigoBrush";
            }
            else
            {
                InvoicedBadgeText = "";
                InvoicedBadgeBrushKey = "";
            }
        }
    }
}
