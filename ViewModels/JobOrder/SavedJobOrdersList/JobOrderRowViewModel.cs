// ═══════════════════════════════════════════════════════════════════════
// FILE: JobOrderRowViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: SavedJobOrdersListViewModel.cs
//
// JobOrderRowViewModel split out of SavedJobOrdersListViewModel.cs (Phase 11/12 God-VM split) — behaviour unchanged.
//
// AUG 2026 PERF: adds a lightweight JobOrderListRow constructor so the list
// can be built from a narrow DB projection (no FullSnapshotJson blob). The
// full-entity constructor remains for callers that already hold an entity.
// Both funnel into one private core so display fields never drift.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.JobOrder.Common.JobPriority;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.Documents;
using System;

namespace ProGlassAutomation.ViewModels.JobOrder.SavedJobOrdersList
{
    public class JobOrderRowViewModel
    {
        public int Id { get; }
        public string JobOrderId { get; }
        public string SourceProformaId { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }
        public decimal GrandTotal { get; }
        public string StatusRaw { get; }
        public string PriorityRaw { get; }
        public DateTime? DeliveryDate { get; }
        public DateTime CreatedAt { get; }
        public DateTime UpdatedAt { get; }

        // These values are resolved by ResolveDisplays after the raw fields
        // have been populated by either constructor. Keep the setters private
        // so the row remains read-only to consumers while the shared resolver
        // can assign the values legally.
        public string GrandTotalDisplay { get; private set; } = string.Empty;
        public string DeliveryDisplay { get; private set; } = string.Empty;
        public string CreatedDisplay { get; private set; } = string.Empty;

        public string StatusLabel { get; private set; } = string.Empty;
        public string StatusBrushKey { get; private set; } = string.Empty;
        public string StatusTintKey { get; private set; } = string.Empty;

        public string PriorityLabel { get; private set; } = string.Empty;
        public string PriorityBrushKey { get; private set; } = string.Empty;
        public string PriorityPillStyleKey { get; private set; } = string.Empty;

        public bool IsOverdue { get; private set; }
        public string OverdueDisplay { get; private set; } = string.Empty;

        // ── delivery-derived list status ────────────────────────────────
        // Populated from the Delivery Notes repository so the list shows the
        // business status (Not delivered / Partially delivered / Fully
        // delivered) instead of the internal production status.

        public int TotalOrderedQty { get; }
        public int TotalDeliveredQty { get; }

        /// <summary>
        /// List-level display status per the business requirement:
        ///   "Fully delivered"      — delivered qty >= ordered qty (>0).
        ///   "Partially delivered"  — some but not all delivered.
        ///   "Not delivered"        — nothing delivered yet.
        /// </summary>
        public string DisplayStatus =>
            DocumentListStatus.ForJobOrder(TotalOrderedQty, TotalDeliveredQty);

        public string DisplayStatusBrushKey =>
            DocumentListStatus.BrushKeyForJobOrder(DisplayStatus);

        /// <summary>
        /// Lightweight constructor used by the saved list. Takes the narrow
        /// JobOrderListRow projection (no FullSnapshotJson blob) so building
        /// the list does not load every row's full snapshot — a real perf win.
        /// </summary>
        public JobOrderRowViewModel(JobOrderListRow row, int totalOrderedQty = 0, int totalDeliveredQty = 0)
        {
            Id = row.Id;
            TotalOrderedQty = totalOrderedQty;
            TotalDeliveredQty = totalDeliveredQty;
            JobOrderId = row.JobOrderId ?? string.Empty;
            SourceProformaId = row.SourceProformaId ?? string.Empty;
            CustomerName = string.IsNullOrWhiteSpace(row.CustomerName) ? "—" : row.CustomerName;
            ProjectName = string.IsNullOrWhiteSpace(row.ProjectName) ? "—" : row.ProjectName;
            GrandTotal = row.GrandTotalVatInclusive;
            StatusRaw = row.Status ?? "Draft";
            PriorityRaw = row.Priority ?? "Medium";
            DeliveryDate = row.DeliveryDate;
            CreatedAt = row.CreatedAt;
            UpdatedAt = row.UpdatedAt;

            ResolveDisplays();
        }

        /// <summary>
        /// Backward-compatible full-entity constructor. Used by callers that
        /// already hold a SavedJobOrder (e.g. detail-adjacent code, tests).
        /// </summary>
        public JobOrderRowViewModel(SavedJobOrder entity, int totalOrderedQty = 0, int totalDeliveredQty = 0)
        {
            Id = entity.Id;
            TotalOrderedQty = totalOrderedQty;
            TotalDeliveredQty = totalDeliveredQty;
            JobOrderId = entity.JobOrderId ?? string.Empty;
            SourceProformaId = entity.SourceProformaId ?? string.Empty;
            CustomerName = string.IsNullOrWhiteSpace(entity.CustomerName) ? "—" : entity.CustomerName;
            ProjectName = string.IsNullOrWhiteSpace(entity.ProjectName) ? "—" : entity.ProjectName;
            GrandTotal = entity.GrandTotalVatInclusive;
            StatusRaw = entity.Status ?? "Draft";
            PriorityRaw = entity.Priority ?? "Medium";
            DeliveryDate = entity.DeliveryDate;
            CreatedAt = entity.CreatedAt;
            UpdatedAt = entity.UpdatedAt;

            ResolveDisplays();
        }

        /// <summary>
        /// Shared display-field resolution so both constructors produce
        /// identical output. The full body was previously duplicated; a
        /// single private core prevents the two from ever drifting.
        /// </summary>
        private void ResolveDisplays()
        {
            GrandTotalDisplay = $"AED {GrandTotal:N0}";
            CreatedDisplay = CreatedAt.ToString("dd MMM yyyy");
            DeliveryDisplay = DeliveryDate.HasValue
                                    ? DeliveryDate.Value.ToString("dd MMM yyyy")
                                    : "No date set";

            if (Enum.TryParse<JobOrderStatus>(StatusRaw, ignoreCase: true, out var statusEnum))
            {
                StatusLabel = JobOrderStatusMeta.GetLabel(statusEnum);
                StatusBrushKey = JobOrderStatusMeta.GetBrushKey(statusEnum);
                StatusTintKey = JobOrderStatusMeta.GetTintKey(statusEnum);
            }
            else
            {
                StatusLabel = StatusRaw;
                StatusBrushKey = "NeutralBrush";
                StatusTintKey = "TintGrayBrush";
            }

            if (Enum.TryParse<JobPriority>(PriorityRaw, ignoreCase: true, out var priorityEnum))
            {
                PriorityLabel = JobPriorityMeta.GetLabel(priorityEnum);
                PriorityBrushKey = JobPriorityMeta.GetBrushKey(priorityEnum);
                PriorityPillStyleKey = JobPriorityMeta.GetPillStyleKey(priorityEnum);
            }
            else
            {
                PriorityLabel = PriorityRaw;
                PriorityBrushKey = "NeutralBrush";
                PriorityPillStyleKey = "PriorityPillLowStyle";
            }

            var today = DateTime.Today;
            IsOverdue = DeliveryDate.HasValue
                        && DeliveryDate.Value.Date < today
                        && !string.Equals(StatusRaw, "Completed", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(StatusRaw, "Delivered", StringComparison.OrdinalIgnoreCase);

            if (IsOverdue)
            {
                int daysLate = (today - DeliveryDate!.Value.Date).Days;
                OverdueDisplay = daysLate == 1
                                     ? "Overdue by 1 day"
                                     : $"Overdue by {daysLate} days";
            }
            else
            {
                OverdueDisplay = string.Empty;
            }
        }
    }
}
