// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Statement/StatementFilter.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// PURPOSE:
//   POCO filter criteria used by StatementGeneratorViewModel.
//   NOT persisted to DB directly — serialized into SavedStatement.FilterJson
//   so a saved statement can be re-run with the same criteria.
//
// FILTER MODES (user picks one primary + optional secondary filters):
//   Primary   : Company | Customer | Project | Salesman | DateRange | Status
//   Secondary : Date range always available as secondary filter
//   Statuses  : multi-select (Draft/Issued/Paid/PartiallyPaid/Overdue/
//               Cancelled/Void) — empty list = ALL statuses
//
// PDF OPTIONS:
//   IncludeAttachments — append signed PDFs after statement pages
//   IncludeDnDetails   — expand DN breakdown rows in statement table
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Models.Statement
{
    /// <summary>
    /// Filter criteria for statement generation.
    /// Serialized to JSON and stored in SavedStatement.FilterJson.
    /// All string fields are null = "not applied" (not empty string).
    /// </summary>
    public class StatementFilter
    {
        // ── Primary filter fields ─────────────────────────────────────────

        /// <summary>
        /// Filter by company name (supplier/factory name).
        /// Matches SavedTaxInvoice.CompanyName exactly (case-insensitive).
        /// Null = not applied.
        /// </summary>
        public string? CompanyName { get; set; }

        /// <summary>
        /// Filter by customer name.
        /// Matches SavedTaxInvoice.CustomerName (case-insensitive contains).
        /// Null = not applied.
        /// </summary>
        public string? CustomerName { get; set; }

        /// <summary>
        /// Filter by project name.
        /// Matches SavedTaxInvoice.ProjectName (case-insensitive contains).
        /// Null = not applied.
        /// </summary>
        public string? ProjectName { get; set; }

        /// <summary>
        /// Filter by salesman name.
        /// Resolved from TaxInvoiceSnapshot.PreparedBy.Name.
        /// Requires JSON parse — applied post-query in aggregator.
        /// Null = not applied.
        /// </summary>
        public string? SalesmanName { get; set; }

        // ── Date range (always available as secondary) ────────────────────

        /// <summary>
        /// Start of date range (inclusive). Applied to IssueDate.
        /// Null = no lower bound.
        /// </summary>
        public DateTime? DateFrom { get; set; }

        /// <summary>
        /// End of date range (inclusive). Applied to IssueDate.
        /// Null = no upper bound.
        /// </summary>
        public DateTime? DateTo { get; set; }

        // ── Status multi-select ───────────────────────────────────────────

        /// <summary>
        /// Statuses to INCLUDE in the statement.
        /// Empty list = include ALL statuses.
        /// Valid values: Draft | Issued | Paid | PartiallyPaid |
        ///               Overdue | Cancelled | Void
        /// </summary>
        public List<string> Statuses { get; set; } = new();

        // ── PDF output options ────────────────────────────────────────────

        /// <summary>
        /// When true, Tax Invoice PDFs + signed attachments (SignedPI,
        /// SignedDN, Other) are appended after the statement summary pages.
        /// Produces a single large combined PDF.
        /// </summary>
        public bool IncludeAttachments { get; set; } = false;

        /// <summary>
        /// When true, each invoice row in the statement table is expanded
        /// with DN line-item breakdown (one sub-row per delivery note).
        /// </summary>
        public bool IncludeDnDetails { get; set; } = false;

        // ── Computed helpers ──────────────────────────────────────────────

        /// <summary>
        /// Returns true if at least one filter field is set.
        /// Used by generator VM to enable/disable the Generate button.
        /// </summary>
        public bool HasAnyFilter =>
            !string.IsNullOrWhiteSpace(CompanyName) ||
            !string.IsNullOrWhiteSpace(CustomerName) ||
            !string.IsNullOrWhiteSpace(ProjectName) ||
            !string.IsNullOrWhiteSpace(SalesmanName) ||
            DateFrom.HasValue ||
            DateTo.HasValue ||
            Statuses.Count > 0;

        /// <summary>
        /// Human-readable summary of active filters.
        /// Used as statement subtitle in the PDF header.
        /// </summary>
        public string GetFilterSummary()
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(CompanyName))
                parts.Add($"Company: {CompanyName}");
            if (!string.IsNullOrWhiteSpace(CustomerName))
                parts.Add($"Customer: {CustomerName}");
            if (!string.IsNullOrWhiteSpace(ProjectName))
                parts.Add($"Project: {ProjectName}");
            if (!string.IsNullOrWhiteSpace(SalesmanName))
                parts.Add($"Salesman: {SalesmanName}");
            if (DateFrom.HasValue || DateTo.HasValue)
            {
                string from = DateFrom?.ToString("dd MMM yyyy") ?? "—";
                string to = DateTo?.ToString("dd MMM yyyy") ?? "—";
                parts.Add($"Period: {from} to {to}");
            }
            if (Statuses.Count > 0)
                parts.Add($"Status: {string.Join(", ", Statuses)}");

            return parts.Count > 0
                ? string.Join("  |  ", parts)
                : "All Records";
        }

        /// <summary>
        /// Returns a short title string for the statement document title.
        /// Priority: Customer > Project > Company > Salesman > Date Range > "All"
        /// </summary>
        public string GetStatementTitle()
        {
            if (!string.IsNullOrWhiteSpace(CustomerName))
                return $"Account Statement — {CustomerName}";
            if (!string.IsNullOrWhiteSpace(ProjectName))
                return $"Project Statement — {ProjectName}";
            if (!string.IsNullOrWhiteSpace(CompanyName))
                return $"Company Statement — {CompanyName}";
            if (!string.IsNullOrWhiteSpace(SalesmanName))
                return $"Sales Report — {SalesmanName}";
            if (DateFrom.HasValue || DateTo.HasValue)
            {
                string from = DateFrom?.ToString("MMM yyyy") ?? "—";
                string to = DateTo?.ToString("MMM yyyy") ?? "—";
                return $"Statement — {from} to {to}";
            }
            return "Full Account Statement";
        }
    }
}