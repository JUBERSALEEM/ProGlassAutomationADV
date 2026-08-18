// ═══════════════════════════════════════════════════════════════════════
//  Services/Statement/Workflow/StatementSnapshotFilter.cs
//
//  Pure statement-snapshot filter predicates. Extracted from
//  StatementWorkflowEngine (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using System;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;

namespace ProGlassAutomation.Services.Statement.Workflow
{
    /// <summary>
    /// Pure predicates used to decide which invoices belong in a statement.
    /// </summary>
    public static class StatementSnapshotFilter
    {
        /// <summary>
        /// True when the invoice satisfies every field of the filter.
        /// A blank filter field means "no restriction".
        /// </summary>
        public static bool MatchesFilter(
            TaxInvoiceListItem inv, StatementFilter filter)
        {
            if (!SafeMatch(inv.CompanyName, filter.CompanyName)) return false;
            if (!SafeMatch(inv.CustomerName, filter.CustomerName)) return false;
            if (!SafeMatch(inv.ProjectName, filter.ProjectName)) return false;
            if (filter.DateFrom.HasValue &&
                inv.IssueDate.Date < filter.DateFrom.Value.Date) return false;
            if (filter.DateTo.HasValue &&
                inv.IssueDate.Date > filter.DateTo.Value.Date) return false;
            if (filter.Statuses != null && filter.Statuses.Count > 0)
            {
                var statuses = filter.Statuses
                    .Select(s => s.ToLowerInvariant()).ToHashSet();
                if (!statuses.Contains(
                        (inv.Status ?? "").ToLowerInvariant()))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Case-insensitive substring match. A blank needle matches anything.
        /// </summary>
        public static bool SafeMatch(string? haystack, string? needle)
        {
            if (string.IsNullOrWhiteSpace(needle)) return true;
            if (string.IsNullOrEmpty(haystack)) return false;
            return haystack.Trim().Contains(needle.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
