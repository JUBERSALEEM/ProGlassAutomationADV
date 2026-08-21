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
        /// <summary>Real delivery notes in this group (Dummy balance DNs excluded).</summary>
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

            // Count real delivery notes only — Dummy DNs are auto-generated
            // balance placeholders, not actual deliveries.
            DnCount = family.Count(x =>
                !string.Equals(x.DeliveryType, "Dummy",
                    StringComparison.OrdinalIgnoreCase));

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
        }
    }
}
