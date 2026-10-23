// ═══════════════════════════════════════════════════════════════════════
// FILE: SavedJobOrdersListViewModel.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.JobOrder.Common.JobPriority;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Documents;
using ProGlassAutomation.Services.JobOrder.Export;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.ProformaInvoice.Attachments;
using System;
using ProGlassAutomation.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using Microsoft.Extensions.DependencyInjection;
using ProGlassAutomation.Views.JobOrder.SavedJobOrdersList.ProformaPicker;
using ProGlassAutomation.Views.JobOrder.SavedJobOrdersList;

namespace ProGlassAutomation.ViewModels.JobOrder.SavedJobOrdersList
{
        public partial class SavedJobOrdersListViewModel : ViewModelBase, IDisposable
    {
        /// <summary>
        /// Register quick-edit entry points. Both reuse the normal saved-list
        /// workflow so status transitions, snapshot synchronization, repository
        /// authorization, and audit behaviour remain in one place.
        /// </summary>
        public async Task ChangeStatusFromRegisterAsync(int jobOrderId, string newStatus)
        {
            if (jobOrderId <= 0 || string.IsNullOrWhiteSpace(newStatus)) return;
            var row = _allJobs.FirstOrDefault(item => item.Id == jobOrderId)
                ?? new JobOrderRowViewModel(new JobOrderListRow { Id = jobOrderId });
            await ChangeStatusAsync(row, newStatus);
        }

        public async Task ChangePriorityFromRegisterAsync(int jobOrderId, string newPriority)
        {
            if (jobOrderId <= 0 || string.IsNullOrWhiteSpace(newPriority)) return;
            var row = _allJobs.FirstOrDefault(item => item.Id == jobOrderId)
                ?? new JobOrderRowViewModel(new JobOrderListRow { Id = jobOrderId });
            await ChangePriorityAsync(row, newPriority);
        }

        private async Task ChangePriorityAsync(JobOrderRowViewModel row, string newPriority)
        {
            try
            {
                var entity = await _jobs.GetByIdAsync(row.Id);
                if (entity == null) return;

                entity.Priority = newPriority;
                entity.UpdatedAt = DateTime.Now;

                // Same consistency requirement as status: the production
                // sheet and detail view read priority from the snapshot.
                var snapshot = JobOrderMapper.DeserializeSnapshot(entity);
                if (snapshot?.Production != null)
                {
                    snapshot.Production.Priority = newPriority;
                    snapshot.Production.LastEditedAt = DateTime.Now;
                    snapshot.Production.LastEditedBy = Environment.UserName;
                    entity.FullSnapshotJson = JobOrderMapper.SerializeSnapshot(snapshot);
                }

                await _jobs.UpdateAsync(entity, row.RowVersion);

                AuditService.Log(
                    AuditSections.JobOrder, "Job order priority changed",
                    $"{entity.JobOrderId} → {newPriority}", "JO", entity.JobOrderId);

                await LoadJobOrdersAsync();
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToChangePriorityX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapUpdateFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  EXPORT (Phase 9.5a-r3 — called from right-click menu)
        // ═════════════════════════════════════════════════════════════════
        /// <summary>
        /// Loads the full entity + snapshot for the given row and exports
        /// a PDF via the shared export engine. Wired to the "Export PDF"
        /// menu item in SavedJobOrdersListView.xaml.cs OnRowRightClick.
        /// </summary>
        public async Task ExportPdfAsync(JobOrderRowViewModel row)
        {
            var (entity, snapshot) = await LoadEntityAndSnapshotAsync(row);
            if (entity == null || snapshot == null) return;

            var layout = new JobOrderPdfLayout(entity, snapshot);
            var engine = new PdfExportEngine();
            var request = new ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true
            };

            var result = await engine.ExportAsync(layout, request);
            ShowExportResult(result, "PDF");
        }

        /// <summary>
        /// Loads the full entity + snapshot for the given row and exports
        /// an Excel workbook via the shared export engine. Wired to the
        /// "Export Excel" menu item in SavedJobOrdersListView.xaml.cs.
        /// </summary>
        public async Task ExportExcelAsync(JobOrderRowViewModel row)
        {
            var (entity, snapshot) = await LoadEntityAndSnapshotAsync(row);
            if (entity == null || snapshot == null) return;

            var layout = new JobOrderExcelLayout(entity, snapshot);
            var engine = new ExcelExportEngine();
            var request = new ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true
            };

            var result = await engine.ExportAsync(layout, request);
            ShowExportResult(result, "Excel");
        }

        /// <summary>
        /// Sharing Center — same layouts as the exports above, staged once
        /// into the share temp folder (rule's default format), then the
        /// composer. Wired to the "Share" menu item next to Export.
        /// </summary>
        public async Task ShareAsync(JobOrderRowViewModel row)
        {
            var (entity, snapshot) = await LoadEntityAndSnapshotAsync(row);
            if (entity == null || snapshot == null) return;

            var context = ProGlassAutomation.Services.Sharing
                .ShareContextFactory.FromJobOrder(entity);

            await ProGlassAutomation.Services.Sharing.ShareLauncher
                .GenerateAndShareAsync(context, async folder =>
                {
                    string ruleFormat = ProGlassAutomation.App.Config.Sharing
                        .RuleFor("JO").DefaultFormat ?? "Pdf";
                    ProGlassAutomation.Services.Export.Core.IDocumentLayout layout;
                    ProGlassAutomation.Services.Export.Core.ExportFormat format;
                    if (string.Equals(ruleFormat, "Excel",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        layout = new JobOrderExcelLayout(entity, snapshot);
                        format = ProGlassAutomation.Services.Export.Core.ExportFormat.Excel;
                    }
                    else
                    {
                        layout = new JobOrderPdfLayout(entity, snapshot);
                        format = ProGlassAutomation.Services.Export.Core.ExportFormat.Pdf;
                    }

                    var staged = await ProGlassAutomation.Services.Sharing.ShareLauncher
                        .ExportLayoutToFolderAsync(layout, format, folder);
                    if (!staged.Success)
                        _dialogs.Error(
                            staged.ErrorMessage ?? "Export failed.", "Share");
                    return staged.SavedPath;
                }, _dialogs);
        }

        private async Task<(SavedJobOrder? entity, Models.JobOrder.SavedJobOrder.JobOrderSnapshot? snapshot)>
            LoadEntityAndSnapshotAsync(JobOrderRowViewModel row)
        {
            if (row == null) return (null, null);

            try
            {
                var entity = await _jobs.GetByIdAsync(row.Id);
                if (entity == null)
                {
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.JobOrderXNoLonger"), row.JobOrderId),
                        LocalizationService.Instance.T("Msg.CapFound"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return (null, null);
                }

                var snapshot = JobOrderMapper.DeserializeSnapshot(entity);
                if (snapshot == null)
                {
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.JobOrderXHasCorrupt"), row.JobOrderId),
                        LocalizationService.Instance.T("Msg.CapCorruptData"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return (entity, null);
                }

                return (entity, snapshot);
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToLoadJobOrder"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapLoadFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return (null, null);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { ProGlassAutomation.Services.SampleData.SampleDataService.DataSeeded -= _dataSeededHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SavedJobOrdersListViewModel] Dispose: DataSeeded detach failed: " + ExceptionMessages.Deep(ex)); }
            // Detach the static lifecycle subscriber too, or this VM stays
            // alive forever and re-runs LoadJobOrdersAsync on every document
            // change across the app even when the list is no longer in use.
            try { _database?.UnsubscribeDocumentsChanged(LoadJobOrdersAsync); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SavedJobOrdersListViewModel] Dispose: DocumentLifecycle detach failed: " + ExceptionMessages.Deep(ex)); }
            GC.SuppressFinalize(this);
        }

        private void ShowExportResult(ExportResult result, string formatLabel)
        {
            if (result.Success || result.Cancelled)
                return;

            _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.XExportFailedY"), formatLabel, result.ErrorMessage),
                LocalizationService.Instance.T("Msg.CapExportFailed"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds a { JobOrderId → (ordered, delivered) } map from Master DN
        /// ordered qty and confirmed real DNs. Includes fully delivered jobs
        /// that a pending-qty query would drop. Never throws.
        /// </summary>
        private async Task<Dictionary<string, (int Ordered, int Delivered)>>
            LoadDeliveryQtyMapAsync(IReadOnlyList<JobOrderListRow> rows)
        {
            var map = new Dictionary<string, (int, int)>(
                StringComparer.OrdinalIgnoreCase);
            try
            {
                var jobs = rows.Select(r => (r.JobOrderId ?? "", r.SourceProformaId ?? ""));
                var snap = await DocumentProgressSnapshot.LoadAsync(jobs);
                foreach (var kv in snap.ByJobOrder)
                    map[kv.Key] = (kv.Value.Ordered, kv.Value.ConfirmedDelivered);
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    "[SavedJobOrdersListViewModel] Delivery-qty map unavailable; rows show 'Not delivered': "
                    + ExceptionMessages.Deep(ex));
            }
            return map;
        }

        private static bool Eq(string? a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static int PriorityRank(string? priority) =>
            priority?.ToLowerInvariant() switch
            {
                "high" => 3,
                "medium" => 2,
                "low" => 1,
                _ => 0
            };
    }
}
