// ═══════════════════════════════════════════════════════════════════════
// FILE: SavedInvoicesListViewModel.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Workflow;
using ProGlassAutomation.Models.ProformaInvoice.Common;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.ProformaInvoice.Attachments;
using ProGlassAutomation.Services.ProformaInvoice.Export;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Documents;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.ViewModels;
using System;
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
using Microsoft.Extensions.DependencyInjection;
using ProGlassAutomation.Views.ProformaInvoice.SavedInvoicesList;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.SavedInvoicesList
{
        public partial class SavedInvoicesListViewModel : ViewModelBase, IDisposable
    {
            private async Task ApproveAsync(SavedProformaInvoice entity)
        {
            if (entity == null) return;

            // Always ask JPG or PDF and forward to Step 8 — even if the
            // invoice already has attachments, so the user re-checks the
            // confirmation document before it is approved.
            var choice = _dialogs.Create<ApprovalUploadChoiceDialog>();
            if (choice.ShowDialog() != true || !choice.Chosen)
                return;   // cancelled — status unchanged

            // Open Step 8 (Attachments) with the matching uploader.
            OnInvoiceUploadRequested?.Invoke(entity.Id, choice.UploadPdf);
        }

        /// <summary>
        /// Runs after the user uploads the signed PI / LPO in Step 8. Shows the
        /// Signed-PI / LPO pattern, persists the LPO number if entered, marks
        /// the invoice Approved and auto-converts it to a Job Order.
        /// </summary>
        public async Task CompleteApprovalAsync(int invoiceId)
        {
            if (_invoices == null) return;

            var entity = await _invoices.GetByIdAsync(invoiceId);
            if (entity == null) return;

            var attachments = await new ProformaAttachmentService()
                .LoadManifestAsync(entity.ProformaId ?? "");

            // Customer-confirmation pattern (Signed PI / LPO + LPO number).
            var dlg = _dialogs.Create<InvoiceApprovalDialog>(attachments?.Count ?? 0);
            if (dlg.ShowDialog() != true || !dlg.Approved)
                return;   // cancelled — status unchanged

            // Persist the LPO number into the snapshot (Step 3 Project.LpoNumber)
            // so invoices / statement generator carry it (optional for Signed PI).
            string lpoToSave = dlg.LpoNumber?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(lpoToSave))
            {
                var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);
                snapshot.Project.LpoNumber = lpoToSave;
                entity.FullSnapshotJson =
                    ProformaInvoiceMapper.SerializeSnapshot(snapshot);
            }

            // Approve + persist — validated against the workflow table so
            // e.g. a Cancelled PI cannot silently become Approved.
            if (!_status.Validate(
                    StatusTransitions.Kind.ProformaInvoice,
                    entity.Status, ProformaInvoiceStatus.Approved).IsValid)
                return;

            entity.Status = ProformaInvoiceStatus.Approved;
            entity.UpdatedAt = DateTime.Now;
            await _invoices.UpdateAsync(entity);

            AuditService.Log(
                AuditSections.Proforma, "Invoice approved",
                $"{entity.ProformaId} approved " +
                $"({(dlg.IsSignedPi ? "signed PI" : "LPO " + dlg.LpoNumber)})",
                "PI", entity.ProformaId);

            // Auto-convert to a Job Order.
            await ConvertToJobOrderAsync(entity);

            await LoadInvoicesAsync();

            _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.InvoiceXApprovedAndConverted"), entity.ProformaId),
                LocalizationService.Instance.T("Msg.CapApproved"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// Auto-converts an approved Proforma Invoice to a Job Order using the
        /// same mapper/repository the old manual "Convert to Job Order" action
        /// used. Skips when a Job Order already exists for the proforma, so a
        /// re-approval can never duplicate it.
        /// </summary>
        private async Task ConvertToJobOrderAsync(SavedProformaInvoice piEntity)
        {
            if (piEntity == null || _jobs == null) return;

            try
            {
                var existing = await _jobs.GetBySourceProformaIdAsync(
                    piEntity.ProformaId ?? "");
                if (existing != null && existing.Count > 0) return;   // already converted

                var jobSnapshot = JobOrderMapper.BuildFromProformaInvoice(piEntity);
                if (await _jobs.ExistsByJobOrderIdAsync(jobSnapshot.JobOrderId))
                {
                    jobSnapshot.JobOrderId = await _jobs.SuggestNextIdAsync(
                        jobSnapshot.JobOrderId);
                }

                var jobEntity = JobOrderMapper.BuildEntityFromSnapshot(
                    jobSnapshot, piEntity.GrandTotalVatInclusive);
                await _jobs.InsertAsync(jobEntity);

                try
                {
                    await ProGlassAutomation.Services.DeliveryNote
                        .MasterDeliveryNoteService
                        .EnsureForJobOrderAsync(jobEntity);
                }
                catch (Exception masterEx)
                {
                    ProGlassAutomation.Services.Logging.AppLogService.Error(
                        "Job Order saved but Master DN was not created.",
                        masterEx,
                        new ProGlassAutomation.Services.Logging.LogContext
                        {
                            Module = "DeliveryNote",
                            Class = nameof(SavedInvoicesListViewModel),
                            Operation = "EnsureMasterDn"
                        });
                }

                AuditService.Log(
                    AuditSections.JobOrder, "Job Order auto-created on approval",
                    $"{jobEntity.JobOrderId} ← {piEntity.ProformaId}",
                    "JO", jobEntity.JobOrderId);
            }
            catch (Exception ex)
            {
                // A Job Order failure must not roll back the approval itself.
                ProGlassAutomation.Services.Logging.AppLogService.Error(
                    "Auto-convert to Job Order on approval failed.", ex,
                    new ProGlassAutomation.Services.Logging.LogContext
                    {
                        Module = "Proforma",
                        Class = nameof(SavedInvoicesListViewModel),
                        Operation = "ApproveAutoConvert"
                    });
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 9.5b — Export from saved list via SHARED ENGINE
        //  BUG-75/76: success feedback + CSV parity
        // ══════════════════════════════════════════════════════════════════

        private async Task ExportPdfFromListAsync(SavedInvoiceListItem item)
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.ProformaExport, "export an invoice PDF"))
                return;

            if (item == null || _invoices == null) return;

            try
            {
                var entity = await _invoices.GetByIdAsync(item.Id);
                if (entity == null) return;

                var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);
                var layout = new ProformaInvoicePdfLayout(entity, snapshot);
                var engine = new PdfExportEngine();
                var request = new ExportRequest
                {
                    PromptForLocation = true,
                    OpenAfterSave = true
                };

                var result = await engine.ExportAsync(layout, request);

                // BUG-75: success feedback
                LastExportStatus = result.Success
                    ? $"✓ PDF exported: {entity.ProformaId}"
                    : result.Cancelled ? "" : $"✗ PDF export failed";

                if (!result.Success && !result.Cancelled)
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.PDFExportFailedX"), result.ErrorMessage),
                        LocalizationService.Instance.T("Msg.CapExportFailed"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                LastExportStatus = $"✗ PDF export failed: {ex.Message}";
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.PDFExportFailedX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapExportFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task ExportExcelFromListAsync(SavedInvoiceListItem item)
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.ProformaExport, "export an invoice to Excel"))
                return;

            if (item == null || _invoices == null) return;

            try
            {
                var entity = await _invoices.GetByIdAsync(item.Id);
                if (entity == null) return;

                var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);
                var layout = new ProformaInvoiceExcelLayout(entity, snapshot);
                var engine = new ExcelExportEngine();
                var request = new ExportRequest
                {
                    PromptForLocation = true,
                    OpenAfterSave = true
                };

                var result = await engine.ExportAsync(layout, request);

                LastExportStatus = result.Success
                    ? $"✓ Excel exported: {entity.ProformaId}"
                    : result.Cancelled ? "" : $"✗ Excel export failed";

                if (!result.Success && !result.Cancelled)
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.ExcelExportFailedX"), result.ErrorMessage),
                        LocalizationService.Instance.T("Msg.CapExportFailed"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                LastExportStatus = $"✗ Excel export failed: {ex.Message}";
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.ExcelExportFailedX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapExportFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// BUG-76: CSV export from list — was missing, PDF/Excel both had it.
        /// Now list and detail views have full parity for all 3 formats.
        /// </summary>
        private async Task ExportCsvFromListAsync(SavedInvoiceListItem item)
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.ProformaExport, "export an invoice CSV"))
                return;

            if (item == null || _invoices == null) return;

            try
            {
                var entity = await _invoices.GetByIdAsync(item.Id);
                if (entity == null) return;

                var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);
                var layout = new ProformaInvoiceCsvLayout(entity, snapshot);
                var engine = new CsvExportEngine();
                var request = new ExportRequest
                {
                    PromptForLocation = true,
                    OpenAfterSave = true
                };

                var result = await engine.ExportAsync(layout, request);

                LastExportStatus = result.Success
                    ? $"✓ CSV exported: {entity.ProformaId}"
                    : result.Cancelled ? "" : $"✗ CSV export failed";

                if (!result.Success && !result.Cancelled)
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.CSVExportFailedX"), result.ErrorMessage),
                        LocalizationService.Instance.T("Msg.CapExportFailed"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                LastExportStatus = $"✗ CSV export failed: {ex.Message}";
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.CSVExportFailedX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapExportFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Phase 4: Event lifetime — detach static events
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { ProGlassAutomation.Services.SampleData.SampleDataService.DataSeeded -= _dataSeededHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SavedInvoicesListViewModel] Dispose: DataSeeded detach failed: " + ex.Message); }
            try { _database.UnsubscribeDocumentsChanged(LoadInvoicesAsync); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SavedInvoicesListViewModel] Dispose: DocumentLifecycle detach failed: " + ex.Message); }
            GC.SuppressFinalize(this);
        }

        // ══════════════════════════════════════════════════════════════
        //  DUPLICATE HELPER — scrub Inquiry/PI id fields from snapshot
        //
        //  Called by ExecuteDuplicateAsync to prevent the duplicated
        //  invoice from silently inheriting the source's Inquiry
        //  back-link via the snapshot JSON. Without this, a duplicate
        //  saved back through the wizard re-populated Company.InquiryId
        //  from the snapshot on RestoreWizardFromSnapshot, and a second
        //  PI ended up linked to the same inquiry — bypassing the
        //  Convert-to-PI duplicate-prevention guard.
        //
        //  Deserialise → clear the two fields → re-serialise with the
        //  same JsonOptions the mapper uses so the round-trip is
        //  byte-identical for every OTHER field.
        // ══════════════════════════════════════════════════════════════
        private static string ScrubIdsForDuplicate(
            string? sourceSnapshotJson, string newProformaId)
        {
            if (string.IsNullOrWhiteSpace(sourceSnapshotJson))
                return sourceSnapshotJson ?? string.Empty;

            try
            {
                var snap = ProformaInvoiceMapper.DeserializeSnapshot(
                    new Models.ProformaInvoice.SavedInvoice.SavedProformaInvoice
                    {
                        FullSnapshotJson = sourceSnapshotJson
                    });
                if (snap.Company != null)
                {
                    snap.Company.InquiryId = null;
                    snap.Company.ProformaId = newProformaId ?? string.Empty;
                }
                return ProformaInvoiceMapper.SerializeSnapshot(snap);
            }
            catch (Exception ex)
            {
                // Deserialise failed — keep the original snapshot so
                // the duplicate at least carries the same visual state.
                // The entity's own InquiryId is still null, so the
                // back-link stays disconnected until the user saves
                // through the wizard; the audit log records why.
                ProGlassAutomation.Services.Logging.AppLogService.Warning(
                    "ScrubIdsForDuplicate could not sanitise snapshot: " + ex.Message,
                    new ProGlassAutomation.Services.Logging.LogContext
                    {
                        Module = "Proforma",
                        Class = nameof(SavedInvoicesListViewModel),
                        Operation = "ScrubIdsForDuplicate"
                    });
                return sourceSnapshotJson;
            }
        }
    }
}
