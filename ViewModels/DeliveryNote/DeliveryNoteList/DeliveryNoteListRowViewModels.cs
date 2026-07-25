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
    //  BUG-37: StatusMeta and TypeMeta now resolved via using directives
    //          at the top of this file — no more fragile full namespace
    //          path inline in constructor.
    //  BUG-38: Extracted from DeliveryNoteListViewModel.cs (was 487 lines).
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
        public int SequenceNumber { get; }
        public DateTime DeliveryDate { get; }
        public string DeliveryTime { get; }
        public DateTime CreatedAt { get; }

        // Computed display properties
        public string StatusLabel { get; }
        public string StatusBrushKey { get; }
        public string TypeLabel { get; }
        public string TypeBrushKey { get; }
        public string DeliveryDateDisplay { get; }
        public string QtySummary { get; }
        public bool HasBalance { get; }
        public bool IsDummy { get; }
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
            SequenceNumber = e.SequenceNumber;
            DeliveryDate = e.DeliveryDate;
            DeliveryTime = e.DeliveryTime;
            CreatedAt = e.CreatedAt;

            // BUG-37: resolved via using directive — no full namespace path
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

            CanGenerateInvoice =
                string.Equals(Status, "Delivered",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Signed",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Received",
                    StringComparison.OrdinalIgnoreCase);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  PENDING QTY ROW VIEWMODEL — one per Job Order in Pending Qty tab
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
    //  JOB ORDER GROUP ROW VIEWMODEL — Ship 8a + 8c-1 + 8c-2
    //
    //  Aggregates a family of split DNs into one per-JO summary row.
    //
    //  Ship 8c-1 qty math:
    //    OrderedQty replicated across splits → MAX
    //    DeliveredQty is per-split partial  → SUM
    //    Balance = Ordered - Delivered (clamped ≥ 0)
    //
    //  Ship 8c-2 SQM:
    //    Sourced from repository batch cache (JobOrderSqmSummary).
    //    Graceful fallback to 0 if JO not in cache.
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

        // Family stats — SQM (Ship 8c-2)
        public decimal TotalOrderedSqm { get; }
        public decimal TotalDeliveredSqm { get; }
        public decimal TotalBalanceSqm { get; }

        // Display strings
        public string QtySummaryDisplay { get; }
        public string SqmSummaryDisplay { get; }

        public bool AllDelivered { get; }
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
                .Where(x => !string.IsNullOrWhiteSpace(x.JobOrderId))
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

            // Latest split by sequence number
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

            // Ship 8c-1: correct qty math
            TotalOrdered = family.Max(x => x.TotalOrderedQty);
            TotalDelivered = family.Sum(x => x.TotalDeliveredQty);
            TotalBalance = Math.Max(0, TotalOrdered - TotalDelivered);

            // Ship 8c-2: SQM from repository cache
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

            StatusLabel = AllDelivered ? "Completed" : "In Progress";

            ProgressPercent = TotalOrdered > 0
                ? Math.Round((double)TotalDelivered / TotalOrdered * 100, 1)
                : 0;
            ProgressDisplay =
                $"{TotalDelivered} / {TotalOrdered} pcs";

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