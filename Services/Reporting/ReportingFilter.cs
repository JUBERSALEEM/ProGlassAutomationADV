// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingFilter.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Global Filter Model
//
// PURPOSE:
//   Single filter contract consumed by ALL report modules and the
//   centralized ReportingDataService. Guarantees consistent filtering
//   semantics across PI, JO, DN, TI, Company, Salesman, Project,
//   Glass/Spec, Statement, Profitability reports.
//
// DESIGN:
//   • Every field nullable/optional — projects are optional, records
//     without projects must continue working correctly.
//   • DateRange is inclusive.
//   • Text filters are case-insensitive contains.
//   • Status is exact match (case-insensitive).
//   • GlassType: SGU / DGU / LAM / DGU+LAM
//   • Pagination is handled by the service layer, not the filter.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Services.Reporting
{
    public class ReportingFilter
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        public string? Company { get; set; }
        public string? Customer { get; set; }
        public string? Salesman { get; set; }
        public string? Project { get; set; }

        public string? ProformaId { get; set; }
        public string? JobOrderId { get; set; }
        public string? DeliveryNoteId { get; set; }
        public string? TaxInvoiceNumber { get; set; }

        public string? Status { get; set; }

        // Glass/Spec filters
        public string? GlassType { get; set; }
        public string? Specification { get; set; }
        public string? Colour { get; set; }
        public string? Thickness { get; set; }
        public string? GlassRef { get; set; }

        // Global text search
        public string? SearchText { get; set; }

        // ── Sort ────────────────────────────────────────────────────────
        // Report-defined sort key (e.g. "Date", "Amount", "Customer",
        // "Outstanding"). null/empty = the report's natural default order.
        // Each report maps its own known keys; unknown keys fall back to the
        // default so a stale key can never break a query.
        public string? SortField { get; set; }
        public bool SortDescending { get; set; } = true;
        public bool HasSort => !string.IsNullOrWhiteSpace(SortField);

        // Pagination
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 100;

        public int Skip => (Math.Max(1, Page) - 1) * Math.Max(1, PageSize);
        public int Take => Math.Max(1, PageSize);

        public bool HasDateRange => DateFrom.HasValue || DateTo.HasValue;
        public bool HasCompany => !string.IsNullOrWhiteSpace(Company);
        public bool HasCustomer => !string.IsNullOrWhiteSpace(Customer);
        public bool HasSalesman => !string.IsNullOrWhiteSpace(Salesman);
        public bool HasProject => !string.IsNullOrWhiteSpace(Project);
        public bool HasSearch => !string.IsNullOrWhiteSpace(SearchText);

        public bool IsEmpty =>
            !HasDateRange && !HasCompany && !HasCustomer && !HasSalesman &&
            !HasProject && string.IsNullOrWhiteSpace(ProformaId) &&
            string.IsNullOrWhiteSpace(JobOrderId) &&
            string.IsNullOrWhiteSpace(DeliveryNoteId) &&
            string.IsNullOrWhiteSpace(TaxInvoiceNumber) &&
            string.IsNullOrWhiteSpace(Status) &&
            string.IsNullOrWhiteSpace(GlassType) &&
            string.IsNullOrWhiteSpace(Specification) &&
            string.IsNullOrWhiteSpace(Colour) &&
            string.IsNullOrWhiteSpace(Thickness) &&
            string.IsNullOrWhiteSpace(GlassRef) &&
            !HasSearch;

        public ReportingFilter Clone() => (ReportingFilter)MemberwiseClone();

        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (DateFrom.HasValue) parts.Add($"From:{DateFrom:yyyy-MM-dd}");
            if (DateTo.HasValue) parts.Add($"To:{DateTo:yyyy-MM-dd}");
            if (HasCompany) parts.Add($"Company:{Company}");
            if (HasCustomer) parts.Add($"Customer:{Customer}");
            if (HasSalesman) parts.Add($"Salesman:{Salesman}");
            if (HasProject) parts.Add($"Project:{Project}");
            if (!string.IsNullOrWhiteSpace(ProformaId)) parts.Add($"PI:{ProformaId}");
            if (!string.IsNullOrWhiteSpace(Status)) parts.Add($"Status:{Status}");
            if (HasSearch) parts.Add($"Search:{SearchText}");
            return parts.Count == 0 ? "No filter" : string.Join(" · ", parts);
        }
    }

    public class PagedResult<T>
    {
        public System.Collections.Generic.List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasNext => Page < TotalPages;
        public bool HasPrevious => Page > 1;
    }
}
