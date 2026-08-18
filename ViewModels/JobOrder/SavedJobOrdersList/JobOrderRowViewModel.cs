// ═══════════════════════════════════════════════════════════════════════
// FILE: JobOrderRowViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: SavedJobOrdersListViewModel.cs
//
// JobOrderRowViewModel split out of SavedJobOrdersListViewModel.cs (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.JobOrder.Common.JobPriority;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.JobOrder.Export;
using ProGlassAutomation.Services.JobOrder.Mapper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

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

        public string GrandTotalDisplay { get; }
        public string DeliveryDisplay { get; }
        public string CreatedDisplay { get; }

        public string StatusLabel { get; }
        public string StatusBrushKey { get; }
        public string StatusTintKey { get; }

        public string PriorityLabel { get; }
        public string PriorityBrushKey { get; }
        public string PriorityPillStyleKey { get; }

        public bool IsOverdue { get; }
        public string OverdueDisplay { get; }

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
        public string DisplayStatus
        {
            get
            {
                if (TotalOrderedQty > 0 && TotalDeliveredQty >= TotalOrderedQty)
                    return "Fully delivered";
                if (TotalDeliveredQty > 0)
                    return "Partially delivered";
                return "Not delivered";
            }
        }

        public string DisplayStatusBrushKey => DisplayStatus switch
        {
            "Fully delivered" => "SuccessBrush",
            "Partially delivered" => "WarningBrush",
            _ => "NeutralBrush"
        };

        public JobOrderRowViewModel(SavedJobOrder entity, int totalOrderedQty = 0, int totalDeliveredQty = 0)
        {
            Id = entity.Id;
            TotalOrderedQty = totalOrderedQty;
            TotalDeliveredQty = totalDeliveredQty;
            JobOrderId = entity.JobOrderId ?? string.Empty;
            SourceProformaId = entity.SourceProformaId ?? string.Empty;
            CustomerName = string.IsNullOrWhiteSpace(entity.CustomerName)
                                   ? "—" : entity.CustomerName;
            ProjectName = string.IsNullOrWhiteSpace(entity.ProjectName)
                                   ? "—" : entity.ProjectName;
            GrandTotal = entity.GrandTotalVatInclusive;
            StatusRaw = entity.Status ?? "Draft";
            PriorityRaw = entity.Priority ?? "Medium";
            DeliveryDate = entity.DeliveryDate;
            CreatedAt = entity.CreatedAt;
            UpdatedAt = entity.UpdatedAt;

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
