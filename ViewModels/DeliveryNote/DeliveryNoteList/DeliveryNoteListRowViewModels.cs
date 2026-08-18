using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus;
using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryType;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList
{
    // ═════════════════════════════════════════════════════════════════════
    //  DN ROW VIEWMODEL — one per Delivery Note in the list
    //
    //  BUG-37: StatusMeta and TypeMeta resolved via using directives.
    //  BUG-38: Extracted from DeliveryNoteListViewModel.cs.
    //  CHAT 14: CanGenerateInvoice includes "Dispatched".
    //  CHAT 19: Added IsMasterDn, DeliveryProgressPercent, IsFullyDelivered.
    //
    //  CHAT 19 INVOICED:
    //    HasTaxInvoice    — true when this DN has been invoiced
    //    TaxInvoiceId     — the Tax Invoice reference (e.g. TI-2026-001)
    //    InvoicedBadgeText — display text: "✅ TI-2026-001" or ""
    //    IsInvoiced       — convenience alias for HasTaxInvoice
    // ═════════════════════════════════════════════════════════════════════

    public class DeliveryNoteRowViewModel : ViewModelBase
    {
        public int Id { get; }
        public string DeliveryNoteId { get; }
        public string JobOrderId { get; }
        public string ProformaId { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }
        public string Status { get; }
        public string DeliveryType { get; }
        public bool IsFoc { get; }
        public string DriverName { get; }
        public string VehicleNumber { get; }
        public int TotalOrderedQty { get; }
        public int TotalDeliveredQty { get; }
        public int TotalBalanceQty { get; }
        public int TotalPreviouslyDeliveredQty { get; }
        public int SequenceNumber { get; }
        public DateTime DeliveryDate { get; }
        public string DeliveryTime { get; }
        public DateTime CreatedAt { get; }

        // ── CHAT 19 — Master DN ───────────────────────────────────────────

        public bool IsMasterDn { get; }
        public double DeliveryProgressPercent { get; }
        public bool IsFullyDelivered { get; }

        // ── CHAT 19 INVOICED ─────────────────────────────────────────────

        /// <summary>
        /// True when this DN has been converted to a Tax Invoice.
        /// Set by TaxInvoiceDetailViewModel.SaveAsync() via
        /// DeliveryNoteRepository.MarkAsInvoicedAsync().
        /// Drives invoiced badge in DN list and history sidebar.
        /// </summary>
        public bool HasTaxInvoice { get; }

        /// <summary>
        /// The Tax Invoice reference number linked to this DN.
        /// e.g. "TI-2026-001" or "TI-2026-001-A".
        /// Empty string = not yet invoiced.
        /// </summary>
        public string TaxInvoiceId { get; }

        /// <summary>
        /// Convenience alias for HasTaxInvoice.
        /// Use in XAML bindings where "IsInvoiced" reads more naturally.
        /// </summary>
        public bool IsInvoiced => HasTaxInvoice;

        /// <summary>
        /// Display text for the invoiced badge in the list row.
        /// Returns "✅ {TaxInvoiceId}" when invoiced.
        /// Returns "" when not yet invoiced.
        /// Used in XAML badge Visibility + Text binding.
        /// </summary>
        public string InvoicedBadgeText =>
            HasTaxInvoice && !string.IsNullOrWhiteSpace(TaxInvoiceId)
                ? $"✅ {TaxInvoiceId}"
                : "";

        // ── Computed display properties ───────────────────────────────────

        /// <summary>
        /// List-level display status per the business requirement:
        ///   "Invoiced"   — the DN has been converted to a Tax Invoice.
        ///   "Dn created" — otherwise (created, dispatched, delivered, etc).
        /// The stored Status string is kept intact for the detail/production
        /// flows; this is the user-facing list status.
        /// </summary>
        public string DisplayStatus => IsInvoiced ? "Invoiced" : "Dn created";

        public string StatusLabel { get; }
        public string StatusBrushKey { get; }
        public string TypeLabel { get; }
        public string TypeBrushKey { get; }
        public string DeliveryDateDisplay { get; }
        public string QtySummary { get; }
        public bool HasBalance { get; }

        /// <summary>Backward compat — use IsMasterDn for new code.</summary>
        public bool IsDummy { get; }

        /// <summary>
        /// CHAT 14: Dispatched/Delivered/Signed/Received = invoice-eligible.
        /// CHAT 19: Always false when IsMasterDn = true.
        /// CHAT 19 INVOICED: Also false when already invoiced
        ///   (cannot invoice same DN twice).
        /// </summary>
        public bool CanGenerateInvoice { get; }

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

        public DeliveryNoteRowViewModel(DeliveryNoteListItem e)
        {
            Id = e.Id;
            DeliveryNoteId = e.DeliveryNoteId;
            JobOrderId = e.JobOrderId;
            ProformaId = e.ProformaId;
            CustomerName = string.IsNullOrWhiteSpace(e.CustomerName)
                ? "—" : e.CustomerName;
            ProjectName = string.IsNullOrWhiteSpace(e.ProjectName)
                ? "—" : e.ProjectName;
            Status = e.Status;
            DeliveryType = e.DeliveryType;
            IsFoc = e.IsFoc;
            DriverName = e.DriverName;
            VehicleNumber = e.VehicleNumber;
            TotalOrderedQty = e.TotalOrderedQty;
            TotalDeliveredQty = e.TotalDeliveredQty;
            TotalBalanceQty = e.TotalBalanceQty;
            TotalPreviouslyDeliveredQty = e.TotalPreviouslyDeliveredQty;
            SequenceNumber = e.SequenceNumber;
            DeliveryDate = e.DeliveryDate;
            DeliveryTime = e.DeliveryTime;
            CreatedAt = e.CreatedAt;

            // CHAT 19: Master DN
            IsMasterDn = e.IsMasterDn;

            DeliveryProgressPercent = e.TotalOrderedQty > 0
                ? Math.Min(100.0,
                    (double)e.TotalPreviouslyDeliveredQty
                    / e.TotalOrderedQty * 100.0)
                : 0.0;

            IsFullyDelivered = e.TotalOrderedQty > 0
                && e.TotalBalanceQty == 0;

            // CHAT 19 INVOICED: map from list item
            HasTaxInvoice = e.HasTaxInvoice;
            TaxInvoiceId = e.TaxInvoiceId ?? "";

            var statusInfo = DeliveryNoteStatusMeta.Get(Status);
            StatusLabel = $"{statusInfo.Icon} {statusInfo.DisplayName}";
            StatusBrushKey = statusInfo.BrushKey;

            var typeInfo = DeliveryTypeMeta.Get(DeliveryType);
            TypeLabel = $"{typeInfo.Icon} {typeInfo.DisplayName}";
            TypeBrushKey = typeInfo.BrushKey;

            DeliveryDateDisplay = DeliveryDate.ToString("dd MMM yyyy");
            QtySummary =
                $"{TotalDeliveredQty} delivered  •  {TotalBalanceQty} balance";
            HasBalance = TotalBalanceQty > 0;

            IsDummy = string.Equals(
                DeliveryType, "Dummy",
                StringComparison.OrdinalIgnoreCase);

            // CHAT 19 INVOICED: already invoiced = cannot invoice again
            bool isInvoiceEligibleStatus =
                string.Equals(Status, "Dispatched",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Delivered",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Signed",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Received",
                    StringComparison.OrdinalIgnoreCase);

            CanGenerateInvoice =
                !IsMasterDn &&
                !HasTaxInvoice &&        // CHAT 19 INVOICED: block re-invoicing
                isInvoiceEligibleStatus;
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  PENDING QTY ROW VIEWMODEL
    // ═════════════════════════════════════════════════════════════════════

    public class PendingQtyRowViewModel
    {
        public string JobOrderId { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }
        public int TotalOrdered { get; }
        public int TotalDelivered { get; }
        public int TotalBalance { get; }
        public string LastDnId { get; }
        public string ProgressDisplay { get; }
        public double ProgressPercent { get; }

        public PendingQtyRowViewModel(PendingQtySummary s)
        {
            JobOrderId = s.JobOrderId;
            CustomerName = string.IsNullOrWhiteSpace(s.CustomerName)
                ? "—" : s.CustomerName;
            ProjectName = string.IsNullOrWhiteSpace(s.ProjectName)
                ? "—" : s.ProjectName;
            TotalOrdered = s.TotalOrdered;
            TotalDelivered = s.TotalDelivered;
            TotalBalance = s.TotalBalance;
            LastDnId = s.LastDnId;
            ProgressDisplay = $"{TotalDelivered} / {TotalOrdered} pcs";
            ProgressPercent = TotalOrdered > 0
                ? Math.Round((double)TotalDelivered / TotalOrdered * 100, 1)
                : 0;
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  JOB ORDER GROUP ROW VIEWMODEL
    //
    //  CHAT 19 INVOICED:
    //    HasAnyInvoiced     — at least one split DN in this JO is invoiced
    //    IsFullyInvoiced    — ALL eligible split DNs are invoiced
    //    InvoicedCount      — how many DNs in group are invoiced
    //    InvoicedCountDisplay — e.g. "2 of 3 invoiced"
    //    InvoicedBadgeText  — "✅ Fully Invoiced" / "🧾 Partial Invoice" / ""
    // ═════════════════════════════════════════════════════════════════════

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