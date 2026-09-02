// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.Helpers.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.3 — StatementGeneratorViewModel partial split. This part owns the
// Helpers members; fields + constructor live in StatementGeneratorViewModel.cs.
// ═══════════════════════════════════════════════════════════════════════

using PdfSharp.Pdf;
using ProGlassAutomation.Services.Logging;
using PdfSharp.Pdf.IO;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Reconciliation;
using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Statement.Aggregation;
using ProGlassAutomation.Services.Statement.Pdf;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.Statement.Workflow;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Views.Reports.Workflow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.Database;

namespace ProGlassAutomation.ViewModels.Reports.StatementGenerator
{
    public partial class StatementGeneratorViewModel : ViewModelBase
    {
        // ═════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════

        private StatementFilter BuildFilter() => new()
        {
            CompanyName = TextParsing.NullIfEmpty(_filterCompany),
            CustomerName = TextParsing.NullIfEmpty(_filterCustomer),
            ProjectName = TextParsing.NullIfEmpty(_filterProject),
            SalesmanName = TextParsing.NullIfEmpty(_filterSalesman),
            DateFrom = _dateFrom,
            DateTo = _dateTo,
            Statuses = StatusItems.Where(s => s.IsChecked).Select(s => s.Status).ToList(),
            IncludeAttachments = _includeAttachments,
            IncludeDnDetails = _includeDnDetails
        };

        /// <summary>
        /// Builds the statement number as "SOA-CustomerName-dd-MM-yy-hh-mm-tt"
        /// (12-hour clock). Falls back to company, then "All" when no customer
        /// is selected. The name is filename-safe and time-stamped so it is
        /// unique per minute.
        /// </summary>
        private static string BuildStatementNumber(StatementFilter filter)
        {
            string customer = (filter.CustomerName ?? "").Trim();
            if (customer.Length == 0)
                customer = (filter.CompanyName ?? "").Trim();
            if (customer.Length == 0)
                customer = "All";

            // Filename-safe: keep letters/digits, replace everything else
            // (spaces, punctuation) with a dash.
            var safe = new string(customer
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray())
                .Trim('-');
            if (string.IsNullOrWhiteSpace(safe)) safe = "All";
            if (safe.Length > 40) safe = safe.Substring(0, 40);

            var now = DateTime.Now;
            string date = now.ToString("dd-MM-yy");
            string time = now.ToString("hh-mm-tt");
            return $"SOA-{safe}-{date}-{time}";
        }

        /// <summary>
        /// Finds an existing saved statement that was generated with the same
        /// primary filters (company / customer / project / salesman / date
        /// range). Returns null when no duplicate exists, so the generator can
        /// warn the user instead of silently piling up identical statements.
        /// </summary>
        private async Task<StatementListItem?> FindDuplicateStatementAsync(StatementFilter filter)
        {
            try
            {
                var existing = await _repo.GetAllAsync();

                foreach (var item in existing)
                {
                    if (!SameText(item.FilterCompany, filter.CompanyName)) continue;
                    if (!SameText(item.FilterCustomer, filter.CustomerName)) continue;
                    if (!SameText(item.FilterProject, filter.ProjectName)) continue;
                    if (!SameText(item.FilterSalesman, filter.SalesmanName)) continue;
                    if (!SameDate(item.FilterDateFrom, filter.DateFrom)) continue;
                    if (!SameDate(item.FilterDateTo, filter.DateTo)) continue;

                    return item;
                }
            }
            catch (Exception ex)
            {
                // Duplicate detection is best-effort — never block generation
                // on a DB read failure.
                AppLogService.Debug($"[StatementGenerator] Duplicate check failed: {ex.Message}");
            }

            return null;
        }

        private static bool SameText(string? a, string? b) =>
            string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

        private static bool SameDate(DateTime? a, DateTime? b)
        {
            if (a.HasValue != b.HasValue) return false;
            if (!a.HasValue) return true;
            return a.Value.Date == b.Value.Date;
        }

        private void ClearFilters()
        {
            _debounceTimer.Stop();

            _filterCompany = "";
            _filterCustomer = "";
            _filterProject = "";
            _filterSalesman = "";
            _dateFrom = null;
            _dateTo = null;
            _includeAttachments = true;
            _includeDnDetails = false;
            _includeBalanceAnalysis = true;
            _includeAgingChart = true;
            _includeInsertPlaceholders = true;
            _includeWarningsSummary = true;

            foreach (var s in StatusItems)
                s.IsChecked = s.Status is "Issued" or "Paid" or "PartiallyPaid" or "Overdue";

            OnPropertyChanged(string.Empty);

            PreviewSnapshot = null;
            ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() => PreviewVM.UpdateFromSnapshot(null));

            StatusMessage = "Filters cleared.";
        }


        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:N1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):N1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):N2} GB";
        }
    }
}
