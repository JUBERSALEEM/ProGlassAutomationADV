// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.Prepare.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.3 — StatementGeneratorViewModel partial split. This part owns the
// Prepare members; fields + constructor live in StatementGeneratorViewModel.cs.
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
        //  PREPARE
        // ═════════════════════════════════════════════════════════════════

        public void PrepareNew()
        {
            _editId = 0;
            _editNumber = "";
            _pendingListRefresh = false;
            CurrentStep = 1;
            ClearFilters();
            PreviewSnapshot = null;

            ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() => PreviewVM.UpdateFromSnapshot(null));

            ResetWorkflowDisplayState();
            NotifyAllNavigationStates();   // [CHAT 22 v3]

            StatusMessage = "";
            OnPropertyChanged(string.Empty);
        }

        public void PrepareEdit(StatementListItem item)
        {
            _editId = item.Id;
            _editNumber = item.StatementNumber;
            _pendingListRefresh = false;
            CurrentStep = 1;

            _debounceTimer.Stop();

            _filterCompany = item.FilterCompany ?? "";
            _filterCustomer = item.FilterCustomer ?? "";
            _filterProject = item.FilterProject ?? "";
            _filterSalesman = item.FilterSalesman ?? "";
            _dateFrom = item.FilterDateFrom;
            _dateTo = item.FilterDateTo;

            PreviewSnapshot = null;

            ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() => PreviewVM.UpdateFromSnapshot(null));

            ResetWorkflowDisplayState();
            NotifyAllNavigationStates();   // [CHAT 22 v3]

            StatusMessage = $"Editing {item.StatementNumber} — loading matching invoices...";
            OnPropertyChanged(string.Empty);

            SchedulePreview();
        }

        private void ResetWorkflowDisplayState()
        {
            ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() =>
            {
                LastWorkflowSteps.Clear();
                ProcessingLog.Clear();
                WorkflowWarnings.Clear();
                MergedPdfSections.Clear();
                BalancePdfSections.Clear();
                AlertsPdfSections.Clear();

                LastWorkflowProgressPercent = 0;
                LastWorkflowStatusText = "Ready to Generate";
                HasWorkflowRun = false;
                IsLastWorkflowComplete = false;

                OrderedQtyMetric = 0;
                DeliveredQtyMetric = 0;
                BalanceQtyMetric = 0;
                DeliveryRatePercent = 0;

                WarningCount = 0;
                ErrorCount = 0;
                InfoCount = 0;

                LastPdfPath = "";
                LastPdfSize = "";
                LastPdfPageCount = 0;
                LastPdfGenerationSec = 0;

                ActivePdfTab = "Merged";

                // Reset deep-detail result data too.
                LastSnapshot = null;
                LastReconciliationSummaries.Clear();
                LastIncludedLines.Clear();
                LastDueLines.Clear();
                IncludedGroups.Clear();
                DueGroups.Clear();
                ActiveReconciliationTab = "Included";
                OnPropertyChanged(nameof(LastIncludedLines));
                OnPropertyChanged(nameof(LastDueLines));
                OnPropertyChanged(nameof(IncludedGroups));
                OnPropertyChanged(nameof(DueGroups));
                OnPropertyChanged(nameof(IncludedTotalQty));
                OnPropertyChanged(nameof(IncludedTotalSqm));
                OnPropertyChanged(nameof(DueTotalQty));
                OnPropertyChanged(nameof(DueTotalSqm));
                OnPropertyChanged(nameof(LastHasReconciliationIssue));
                OnPropertyChanged(nameof(HasReconciliationJobOrder));

                OnPropertyChanged(nameof(HasLastPdf));
                OnPropertyChanged(nameof(MergedSectionCount));
                OnPropertyChanged(nameof(BalanceSectionCount));
                OnPropertyChanged(nameof(AlertsSectionCount));
            });
        }

        private void PrepareForNewWorkflowRun()
        {
            ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() =>
            {
                LastWorkflowSteps.Clear();
                ProcessingLog.Clear();
                WorkflowWarnings.Clear();
                MergedPdfSections.Clear();
                BalancePdfSections.Clear();
                AlertsPdfSections.Clear();

                LastWorkflowProgressPercent = 0;
                LastWorkflowStatusText = "Preparing pipeline…";
                HasWorkflowRun = true;
                IsLastWorkflowComplete = false;

                OrderedQtyMetric = 0;
                DeliveredQtyMetric = 0;
                BalanceQtyMetric = 0;
                DeliveryRatePercent = 0;

                WarningCount = 0;
                ErrorCount = 0;
                InfoCount = 0;

                LastPdfPath = "";
                LastPdfSize = "";
                LastPdfPageCount = 0;
                LastPdfGenerationSec = 0;

                OnPropertyChanged(nameof(HasLastPdf));
                OnPropertyChanged(nameof(MergedSectionCount));
                OnPropertyChanged(nameof(BalanceSectionCount));
                OnPropertyChanged(nameof(AlertsSectionCount));
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  STATUS ITEM COUNT UPDATE
        // ═════════════════════════════════════════════════════════════════

        private void UpdateStatusItemCounts(StatementSnapshot? snapshot)
        {
            ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() =>
            {
                if (snapshot == null || snapshot.InvoiceRows.Count == 0)
                {
                    foreach (var item in StatusItems) item.Count = 0;
                    return;
                }

                var grouped = snapshot.InvoiceRows
                    .GroupBy(r => (r.Status ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

                foreach (var item in StatusItems)
                    item.Count = grouped.TryGetValue(item.Status, out int c) ? c : 0;
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  MASTER DATA
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadMasterDataAsync()
        {
            try
            {
                var companies = await Task.Run(() => _masterData.GetAllCompaniesAsync());
                var clients = await Task.Run(() => _masterData.GetAllClientsAsync());
                var projects = await Task.Run(() => _masterData.GetAllProjectsAsync());
                var salesmen = await Task.Run(() => _masterData.GetAllSalesmenAsync());

                ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() =>
                {
                    Companies.Clear();
                    foreach (var c in companies.Select(c => c.CompanyName)
                        .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                        Companies.Add(c);

                    // Keep the full client list so the Customer dropdown can
                    // be scoped to the selected salesman.
                    _allClients = clients ?? new List<SavedClient>();

                    // Keep full company records for v3 letterhead branding.
                    _allCompanies = companies ?? new List<SavedCompany>();

                    // Keep the full project list so the Project dropdown can
                    // be scoped to the selected customer.
                    _allProjects = projects ?? new List<SavedProject>();

                    Salesmen.Clear();
                    foreach (var s in salesmen.Select(s => s.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                        Salesmen.Add(s);

                    // Repopulate Customers scoped to the currently selected
                    // salesman (all when none is selected), then Projects
                    // scoped to the currently selected customer.
                    RefreshCustomersForSalesman();
                    RefreshProjectsForCustomer();
                });
            }
            catch (Exception ex)
            {
                // Fully qualified to avoid ambiguity: this file also uses
                // LogEntry from Models.Statement.Workflow, which would clash
                // with an imported Logging.LogEntry if we used a using directive.
                ProGlassAutomation.Services.Logging.AppLogService.Error(
                    "[StatementGenerator] Master data load failed", ex);
            }
        }

        /// <summary>
        /// Repopulates the Customer dropdown scoped to the selected salesman.
        /// When no salesman is selected, all customers are shown. When one is
        /// selected, only clients whose SalesRepresentative matches it appear,
        /// so the operator can't pick a customer that doesn't belong to that
        /// salesman.
        /// </summary>
        private void RefreshCustomersForSalesman()
        {
            string salesman = _filterSalesman?.Trim() ?? "";

            IEnumerable<string> names = _allClients
                .Select(c => c.CustomerName ?? "")
                .Where(n => !string.IsNullOrWhiteSpace(n));

            if (salesman.Length > 0)
            {
                names = _allClients
                    .Where(c => string.Equals(
                        c.SalesRepresentative?.Trim() ?? "",
                        salesman,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.CustomerName ?? "")
                    .Where(n => !string.IsNullOrWhiteSpace(n));
            }

            var ordered = names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Customers.Clear();
            foreach (var n in ordered)
                Customers.Add(n);

            // If the currently selected customer no longer belongs to the
            // chosen salesman, clear it so a stale (invalid) value can't be
            // carried into the preview/generation.
            if (salesman.Length > 0 && !string.IsNullOrWhiteSpace(_filterCustomer))
            {
                bool stillValid = _allClients.Any(c =>
                    string.Equals(c.CustomerName?.Trim() ?? "", _filterCustomer.Trim(),
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.SalesRepresentative?.Trim() ?? "", salesman,
                        StringComparison.OrdinalIgnoreCase));

                if (!stillValid)
                {
                    _filterCustomer = "";
                    OnPropertyChanged(nameof(FilterCustomer));
                }
            }
        }

        /// <summary>
        /// Repopulates the Project dropdown scoped to the selected customer.
        /// When no customer is selected, all projects are shown. When one is
        /// selected, only projects whose CustomerName matches it appear, so
        /// the operator can't pick a project that belongs to another client.
        /// </summary>
        private void RefreshProjectsForCustomer()
        {
            string customer = _filterCustomer?.Trim() ?? "";

            IEnumerable<string> names = _allProjects
                .Select(ProjectDisplayName)
                .Where(n => !string.IsNullOrWhiteSpace(n));

            if (customer.Length > 0)
            {
                names = _allProjects
                    .Where(p => string.Equals(
                        p.CustomerName?.Trim() ?? "",
                        customer,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(ProjectDisplayName)
                    .Where(n => !string.IsNullOrWhiteSpace(n));
            }

            var ordered = names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Projects.Clear();
            foreach (var n in ordered)
                Projects.Add(n);

            // Clear a stale project selection that no longer belongs to the
            // chosen customer.
            if (customer.Length > 0 && !string.IsNullOrWhiteSpace(_filterProject))
            {
                string sel = _filterProject.Trim();
                bool stillValid = _allProjects.Any(p =>
                    string.Equals(ProjectDisplayName(p), sel,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.CustomerName?.Trim() ?? "", customer,
                        StringComparison.OrdinalIgnoreCase));

                if (!stillValid)
                {
                    _filterProject = "";
                    OnPropertyChanged(nameof(FilterProject));
                }
            }
        }

        private static string ProjectDisplayName(SavedProject p) =>
            !string.IsNullOrWhiteSpace(p.ProjectContractNo)
                ? p.ProjectContractNo.Trim()
                : (p.ProjectIdentifierName?.Trim() ?? "");

    }
}
