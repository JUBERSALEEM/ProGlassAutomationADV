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
using ProGlassAutomation.Services.Sharing;
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

            // 2026-09-10 unification — single merged approve flow: forward
            // straight to Step 8, whose ONE "APPROVE INVOICE" action takes
            // the confirmation document (image OR PDF, type auto-detected)
            // and then runs the Signed-PI / LPO confirmation. Cancelling at
            // any point leaves the status unchanged. (The old JPG-vs-PDF
            // choice dialog is deleted — see docs/STEP8_APPROVE_UNIFICATION.md.)
            OnInvoiceApproveRequested?.Invoke(entity.Id);
            await Task.CompletedTask;
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
                if (existing != null && existing.Count > 0)
                {
                    // RC-015 (2026-09-20): a Job Order created BEFORE its quote
                    // was approved is born Draft — JobOrderMapper writes
                    // Released only when the PI is already "Approved" at the
                    // instant of conversion (JobOrderMapper.cs:92), and that is
                    // the ONLY place in the codebase that ever assigns
                    // Released. So the early return here left such a job Draft
                    // forever: approving the PI created nothing new and
                    // upgraded nothing existing.
                    //
                    // The consequence is invisible from the Proforma side. The
                    // Optimization picker's source lock filters Draft out
                    // (OptimizationViewModel.Jobs.cs — !Is(j.Status, Draft)),
                    // so the confirmed job simply never appeared in the JOBS
                    // tab while the status line still read "Loading confirmed
                    // job orders…". That is the reported "not reading confirmed
                    // PI as job order".
                    //
                    // Approval is the confirmation, so approval releases the
                    // job — the same intent the mapper documents. Follows the
                    // list-side status-change pattern exactly: validate the
                    // transition, update the indexed column, keep the embedded
                    // snapshot's Production.Status in sync (the detail view,
                    // its save path and the production-sheet exports read the
                    // status from the snapshot), then audit.
                    foreach (var job in existing)
                    {
                        if (job == null || job.IsVoid) continue;
                        if (!ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus
                                .JobOrderStatusValues.Is(
                                    job.Status,
                                    ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus
                                        .JobOrderStatusValues.Draft))
                            continue;

                        var transition = StatusTransitions.Validate(
                            StatusTransitions.Kind.JobOrder,
                            job.Status,
                            ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus
                                .JobOrderStatusValues.Released);
                        if (!transition.IsValid)
                        {
                            ProGlassAutomation.Services.Logging.AppLogService.Warning(
                                $"Approved PI {piEntity.ProformaId} has job {job.JobOrderId} in " +
                                $"status '{job.Status}' that cannot be released: {transition.Reason}");
                            continue;
                        }

                        string released = ProGlassAutomation.Models.JobOrder.Common
                            .JobOrderStatus.JobOrderStatusValues.Released;

                        job.Status = released;
                        job.UpdatedAt = DateTime.Now;

                        var existingJobSnapshot = JobOrderMapper.DeserializeSnapshot(job);
                        if (existingJobSnapshot?.Production != null)
                        {
                            existingJobSnapshot.Production.Status = released;
                            existingJobSnapshot.Production.LastEditedAt = DateTime.Now;
                            existingJobSnapshot.Production.LastEditedBy = Environment.UserName;
                            job.FullSnapshotJson = JobOrderMapper.SerializeSnapshot(existingJobSnapshot);
                        }

                        await _jobs.UpdateAsync(job);

                        AuditService.Log(
                            AuditSections.JobOrder,
                            "Draft job released on invoice approval",
                            $"{job.JobOrderId} ← {piEntity.ProformaId} (was Draft)",
                            "JO", job.JobOrderId);
                    }

                    return;   // already converted — nothing to insert
                }

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
                LastExportStatus = $"✗ PDF export failed: {ExceptionMessages.Deep(ex)}";
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.PDFExportFailedX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapExportFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// FULL DOSSIER export — every section of the invoice in one
        /// workbook: overview, specifications, cut list, other charges,
        /// terms, and optionally profit &amp; loss.
        ///
        /// <paramref name="includeProfitLoss"/> is the whole safety story
        /// here. The customer-facing variant is the DEFAULT action; the
        /// internal one is a separate, explicitly-labelled command. An
        /// "export everything" button that silently carries cost and
        /// margin will eventually be emailed to a customer.
        ///
        /// The internal variant additionally requires ProformaDelete,
        /// so a user who may export a quote cannot obtain the margin
        /// simply by choosing the other button.
        ///
        /// ProformaDelete is used as the elevation gate because there is no
        /// dedicated cost permission and it is granted only to Manager and
        /// Administrator — Estimator and Sales both hold ProformaExport but
        /// not this, which is precisely the separation required.
        /// </summary>
        private async Task ExportDossierAsync(
            SavedInvoiceListItem item, bool includeProfitLoss)
            => await ExportDossierAsync(item, includeProfitLoss, asPdf: false);

        /// <summary>
        /// Dossier export. <paramref name="asPdf"/> selects the print/email
        /// form; Excel is the working form. Both honour the same
        /// P&amp;L gate, so the format cannot become a way around it.
        /// </summary>
        private async Task ExportDossierAsync(
            SavedInvoiceListItem item, bool includeProfitLoss, bool asPdf)
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.ProformaExport,
                "export an invoice dossier"))
                return;

            if (includeProfitLoss
                && !ProGlassAutomation.Services.Audit.PermissionService.Require(
                    ProGlassAutomation.Services.Audit.AppPermissions.ProformaDelete,
                    "export a dossier including profit and loss"))
                return;

            if (item == null || _invoices == null) return;

            try
            {
                var entity = await _invoices.GetByIdAsync(item.Id);
                if (entity == null) return;

                var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);

                var request = new ExportRequest
                {
                    PromptForLocation = true,
                    OpenAfterSave = true,
                    SuggestedFileName =
                        $"{entity.ProformaId}_Dossier"
                        + (includeProfitLoss ? "_INTERNAL" : "")
                };

                ExportResult result;
                if (asPdf)
                {
                    var pdfLayout = new ProformaDossierPdfLayout(
                        entity, snapshot, includeProfitLoss);
                    result = await new PdfExportEngine()
                        .ExportAsync(pdfLayout, request);
                }
                else
                {
                    var xlLayout = new ProformaDossierExcelLayout(
                        entity, snapshot, includeProfitLoss);
                    result = await new ExcelExportEngine()
                        .ExportAsync(xlLayout, request);
                }

                LastExportStatus = result.Success
                    ? (includeProfitLoss
                        ? $"✓ Internal dossier exported: {entity.ProformaId}"
                        : $"✓ Dossier exported: {entity.ProformaId}")
                    : result.Cancelled ? "" : "✗ Dossier export failed";

                if (!result.Success && !result.Cancelled)
                    _dialogs.Show(
                        result.ErrorMessage ?? "Dossier export failed.",
                        LocalizationService.Instance.T("Msg.CapExportFailed"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                ProGlassAutomation.Services.Logging.AppLogService.Warning(
                    $"Dossier export failed: {ExceptionMessages.Deep(ex)}",
                    new ProGlassAutomation.Services.Logging.LogContext
                    {
                        Module = "ProformaInvoice",
                        Operation = "ExportDossier"
                    });

                _dialogs.Show(
                    "Dossier export failed.\n\n" + ExceptionMessages.Deep(ex),
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
                LastExportStatus = $"✗ Excel export failed: {ExceptionMessages.Deep(ex)}";
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.ExcelExportFailedX"), ExceptionMessages.Deep(ex)),
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
                LastExportStatus = $"✗ CSV export failed: {ExceptionMessages.Deep(ex)}";
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.CSVExportFailedX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapExportFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ── Sharing Center ──────────────────────────────────────────
        // Same exporter as ExportPdf/Excel/CsvFromListAsync, run ONCE into
        // the share temp folder (rule's default format), then the composer.
        private async Task ShareFromListAsync(SavedInvoiceListItem? item)
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.ProformaExport, "share an invoice"))
                return;

            if (item == null || _invoices == null) return;

            try
            {
                var entity = await _invoices.GetByIdAsync(item.Id);
                if (entity == null) return;

                var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);
                var context = ShareContextFactory.FromProformaInvoice(entity, snapshot);

                await ShareLauncher.GenerateAndShareAsync(context, async folder =>
                    {
                        string ruleFormat = App.Config.Sharing
                            .RuleFor("PI").DefaultFormat ?? "Pdf";
                        IDocumentLayout layout;
                        ExportFormat format;
                        if (string.Equals(ruleFormat, "Excel",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            layout = new ProformaInvoiceExcelLayout(entity, snapshot);
                            format = ExportFormat.Excel;
                        }
                        else if (string.Equals(ruleFormat, "Csv",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            layout = new ProformaInvoiceCsvLayout(entity, snapshot);
                            format = ExportFormat.Csv;
                        }
                        else
                        {
                            layout = new ProformaInvoicePdfLayout(entity, snapshot);
                            format = ExportFormat.Pdf;
                        }

                        var staged = await ShareLauncher.ExportLayoutToFolderAsync(
                            layout, format, folder);
                        if (!staged.Success)
                            LastExportStatus = $"✗ Share failed: {staged.ErrorMessage}";
                        return staged.SavedPath;
                    }, _dialogs);
            }
            catch (Exception ex)
            {
                LastExportStatus = $"✗ Share failed: {ExceptionMessages.Deep(ex)}";
            }
        }

        // Phase 4: Event lifetime — detach static events
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { ProGlassAutomation.Services.SampleData.SampleDataService.DataSeeded -= _dataSeededHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SavedInvoicesListViewModel] Dispose: DataSeeded detach failed: " + ExceptionMessages.Deep(ex)); }
            try { _database.UnsubscribeDocumentsChanged(LoadInvoicesAsync); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SavedInvoicesListViewModel] Dispose: DocumentLifecycle detach failed: " + ExceptionMessages.Deep(ex)); }
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
                    "ScrubIdsForDuplicate could not sanitise snapshot: " + ExceptionMessages.Deep(ex),
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
