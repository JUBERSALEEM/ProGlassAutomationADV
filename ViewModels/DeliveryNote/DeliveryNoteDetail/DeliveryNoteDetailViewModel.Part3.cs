// ═══════════════════════════════════════════════════════════════════════
// FILE: DeliveryNoteDetailViewModel.Part3.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Win32;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.DeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Attachments;
using ProGlassAutomation.Services.DeliveryNote.Export;
using ProGlassAutomation.Services.DeliveryNote.Mapper;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.Views.DeliveryNote.DeliveryNoteDetail.Attachments.Rename;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Workflow;
using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus;
using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail
{
        public partial class DeliveryNoteDetailViewModel : ViewModelBase
    {
            // ═════════════════════════════════════════════════════════════════
        //  DELETE DN (guarded for Master DN)
        // ═════════════════════════════════════════════════════════════════

        private async Task DeleteDnAsync()
        {
            if (_entity == null || _deliveryNotes == null) return;
            if (_entity.IsMasterDn) return;

            var guard = await _database.CheckDependencyAsync(
                DependencyGuard.Kind.DeliveryNote,
                _entity.DeliveryNoteId);
            if (guard.HasDependents)
            {
                _dialogs.Show(guard.Message, LocalizationService.Instance.T("Msg.CapCannotDelete"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.DeleteDeliveryNoteXCustomer"), DeliveryNoteId, CustomerName, ProjectName, Status),
                LocalizationService.Instance.T("Msg.CapConfirmDelete"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                IsSaving = true;

                var freshAttachments = await _deliveryNotes
                    .GetAttachmentsAsync(_entity.DeliveryNoteId);

                foreach (var att in freshAttachments)
                {
                    try { _attachmentService.DeleteFile(att.FilePath); }
                    catch { /* file may already be gone */ }
                }

                await _deliveryNotes.DeleteAsync(_entity.Id);

                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.DeliveryNoteXDeletedSuccessfully"), DeliveryNoteId),
                    LocalizationService.Instance.T("SheetStore.Deleted"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                OnRequestBackToList?.Invoke();
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToDeleteX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapDeleteFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  TAX INVOICE
        // ═════════════════════════════════════════════════════════════════

        private bool CanConvertToTaxInvoice(DnHistoryEntryViewModel? entry)
        {
            if (_entity == null || IsSaving || IsMasterDn) return false;
            if (string.IsNullOrWhiteSpace(ProformaId)) return false;

            // CHAT 19 INVOICED: already invoiced DNs cannot be re-invoiced
            // Check the specific history entry's invoiced state
            if (entry != null && entry.HasTaxInvoice) return false;

            // If no specific entry, check the currently loaded DN
            if (entry == null && _entity.HasTaxInvoice) return false;

            var targetStatus = entry?.Status ?? Status;
            return targetStatus == "Dispatched"
                || targetStatus == "Delivered"
                || targetStatus == "Signed"
                || targetStatus == "Received";
        }

        private async Task ConvertToTaxInvoiceAsync(
            DnHistoryEntryViewModel? entry)
        {
            if (string.IsNullOrWhiteSpace(ProformaId))
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.DeliveryNoteHasNoLinked"),
                    LocalizationService.Instance.T("Msg.CapPIReference"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (OnRequestOpenTaxInvoiceForDeliveryNote == null)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.TaxInvoiceNavigationIsNot"),
                    LocalizationService.Instance.T("Msg.CapWired"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var targetDnId = entry?.DeliveryNoteId ?? DeliveryNoteId;
            var targetStatus = entry?.Status ?? Status;
            var targetQty = entry?.DeliveredQty ?? TotalDeliveredQty;

            if (string.IsNullOrWhiteSpace(targetDnId))
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.CannotDetermineWhichDNTo"),
                    LocalizationService.Instance.T("Msg.CapDNSelected"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (targetQty <= 0)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.DNXHas0Delivered"), targetDnId),
                    LocalizationService.Instance.T("Msg.CapNothingToInvoice"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                await OnRequestOpenTaxInvoiceForDeliveryNote(targetDnId);
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToOpenTaxInvoice"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapNavigationFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  EXPORT
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Deep-sweep fix: exports must NEVER crash the app. Any failure
        /// surfaces the standard error dialog instead of an unhandled
        /// exception on the UI thread.
        /// </summary>
        private async Task GuardedExportAsync(Func<Task> export, string kind)
        {
            try { await export(); }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.ExportFailedX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapXExportFailed"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task ExportPdfAsync()
        {
            if (!EnsureLoaded(out var entity, out var snapshot)) return;
            var layout = new DeliveryNotePdfLayout(entity, snapshot);
            var engine = new PdfExportEngine();
            ShowExportResult(
                await engine.ExportAsync(layout,
                    new ExportRequest
                    {
                        PromptForLocation = true,
                        OpenAfterSave = true
                    }), "PDF");
        }

        private async Task ExportExcelAsync()
        {
            if (!EnsureLoaded(out var entity, out var snapshot)) return;
            var layout = new DeliveryNoteExcelLayout(entity, snapshot);
            var engine = new ExcelExportEngine();
            ShowExportResult(
                await engine.ExportAsync(layout,
                    new ExportRequest
                    {
                        PromptForLocation = true,
                        OpenAfterSave = true
                    }), "Excel");
        }

        private async Task ExportCsvAsync()
        {
            if (!EnsureLoaded(out var entity, out var snapshot)) return;
            var layout = new DeliveryNoteCsvLayout(entity, snapshot);
            var engine = new CsvExportEngine();
            ShowExportResult(
                await engine.ExportAsync(layout,
                    new ExportRequest
                    {
                        PromptForLocation = true,
                        OpenAfterSave = true
                    }), "CSV");
        }

        private bool EnsureLoaded(
            out SavedDeliveryNote entity,
            out DeliveryNoteSnapshot snapshot)
        {
            if (_entity == null)
            {
                entity = null!;
                snapshot = null!;
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.LoadADeliveryNoteBefore"),
                    LocalizationService.Instance.T("Msg.CapNothingToExport"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            entity = _entity;
            snapshot = _snapshot ?? new DeliveryNoteSnapshot
            {
                Header = new DnHeaderSnapshot
                {
                    DeliveryNoteId = _entity.DeliveryNoteId,
                    JobOrderId = _entity.JobOrderId,
                    Status = _entity.Status,
                    DeliveryType = _entity.DeliveryType,
                    DeliveryDate = _entity.DeliveryDate,
                    DeliveryTime = _entity.DeliveryTime
                },
                JobOrderRef = new DnJobOrderRefSnapshot
                {
                    CustomerName = _entity.CustomerName,
                    ProjectName = _entity.ProjectName
                }
            };
            return true;
        }

        private void ShowExportResult(
            ExportResult result, string formatLabel)
        {
            if (result.Success || result.Cancelled) return;
            _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.XExportFailedY"), formatLabel, result.ErrorMessage),
                LocalizationService.Instance.T("Msg.CapExportFailed"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        // ═════════════════════════════════════════════════════════════════
        //  ATTACHMENTS
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadAttachmentsDirectlyAsync(string deliveryNoteId)
        {
            if (_deliveryNotes == null ||
                string.IsNullOrWhiteSpace(deliveryNoteId)) return;

            var list = await _deliveryNotes
                .GetAttachmentsAsync(deliveryNoteId);
            var rows = list
                .Select(a => new DnAttachmentRowViewModel(a)).ToList();
            Attachments =
                new ObservableCollection<DnAttachmentRowViewModel>(rows);

            var historyRow = HistoryEntries.FirstOrDefault(h =>
                string.Equals(h.DeliveryNoteId, deliveryNoteId,
                    StringComparison.OrdinalIgnoreCase));
            if (historyRow != null)
                historyRow.AttachmentCount = rows.Count;

            OnPropertyChanged(nameof(SelectedDnHeaderText));
        }

        private async Task UploadImageAsync()
        {
            if (_entity == null || _deliveryNotes == null) return;

            var dlg = new OpenFileDialog
            {
                Title = "Select image to attach",
                Filter = AttachmentLimits.ImageDialogFilter,
                Multiselect = false
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                if (!_attachmentService.IsValidImageExtension(dlg.FileName))
                {
                    _dialogs.Show(
                        LocalizationService.Instance.T("Msg.UnsupportedImageFormatAllowedJPG"),
                        LocalizationService.Instance.T("Msg.CapInvalidFile"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!_attachmentService.IsUnderSizeLimit(dlg.FileName))
                {
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.FileIsTooLargeMaximum"), AttachmentLimits.MaxFileSizeDisplay),
                        LocalizationService.Instance.T("Msg.CapFileTooLarge"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var origName = Path.GetFileName(dlg.FileName);
                var renameDlg = _dialogs.Create<RenameAttachmentDialog>(origName);
                if (renameDlg.ShowDialog() != true) return;

                IsSaving = true;
                var entity = await _attachmentService
                    .CopyImageAsync(
                        dlg.FileName, _deliveryNoteId,
                        renameDlg.ResultFileName);
                await _deliveryNotes.AddAttachmentAsync(entity);
                await LoadAttachmentsDirectlyAsync(_deliveryNoteId);

                AuditService.Log(
                    AuditSections.DeliveryNote, "Attachment added",
                    $"Image '{entity.FileName}' → {_deliveryNoteId}",
                    "DN", _deliveryNoteId);
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToUploadImageX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapUploadFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally { IsSaving = false; }
        }

        private async Task UploadPdfAsync()
        {
            if (_entity == null || _deliveryNotes == null) return;

            var dlg = new OpenFileDialog
            {
                Title = "Select PDF to attach",
                Filter = AttachmentLimits.PdfDialogFilter,
                Multiselect = false
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                if (!_attachmentService.IsValidPdfExtension(dlg.FileName))
                {
                    _dialogs.Show(
                        LocalizationService.Instance.T("Msg.OnlyPDFFilesAreAllowed"),
                        LocalizationService.Instance.T("Msg.CapInvalidFile"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!_attachmentService.IsUnderSizeLimit(dlg.FileName))
                {
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.FileIsTooLargeMaximum"), AttachmentLimits.MaxFileSizeDisplay),
                        LocalizationService.Instance.T("Msg.CapFileTooLarge"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var origName = Path.GetFileName(dlg.FileName);
                var renameDlg = _dialogs.Create<RenameAttachmentDialog>(origName);
                if (renameDlg.ShowDialog() != true) return;

                IsSaving = true;
                var entity = await _attachmentService
                    .CopyPdfAsync(
                        dlg.FileName, _deliveryNoteId,
                        renameDlg.ResultFileName);
                await _deliveryNotes.AddAttachmentAsync(entity);
                await MarkReceivedAfterSignedCopyAsync();
                await LoadAttachmentsDirectlyAsync(_deliveryNoteId);

                AuditService.Log(
                    AuditSections.DeliveryNote, "Attachment added",
                    $"PDF '{entity.FileName}' → {_deliveryNoteId}",
                    "DN", _deliveryNoteId);
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToUploadPDFX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapUploadFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally { IsSaving = false; }
        }

        private void OpenAttachment(DnAttachmentRowViewModel row)
        {
            try { _attachmentService.OpenInDefaultApp(row.FilePath); }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.CouldNotOpenFileX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapOpenFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task DeleteAttachmentAsync(DnAttachmentRowViewModel row)
        {
            if (_deliveryNotes == null) return;

            var result = _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.DeleteAttachmentXThisCannot"), row.FileName),
                LocalizationService.Instance.T("Msg.CapConfirmDelete"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _deliveryNotes.DeleteAttachmentAsync(row.Id);
                _attachmentService.DeleteFile(row.FilePath);
                await LoadAttachmentsDirectlyAsync(_deliveryNoteId);

                AuditService.Log(
                    AuditSections.DeliveryNote, "Attachment deleted",
                    $"'{row.FileName}' removed from {_deliveryNoteId}",
                    "DN", _deliveryNoteId);
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToDeleteAttachmentX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapDeleteFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Signed copy attached → customer received. Only after the
        /// truck has left (Dispatched / Delivered / Signed).
        /// </summary>
        private async Task MarkReceivedAfterSignedCopyAsync()
        {
            if (_entity == null || _deliveryNotes == null || IsMasterDn)
                return;
            if (string.Equals(Status, "Received", StringComparison.OrdinalIgnoreCase))
                return;
            if (IsOpenForDispatch(Status))
                return;

            try
            {
                // Enforce the workflow table: only valid sources may move
                // to Received (Signed → Received, Dispatched → Received …).
                if (!_statusService.Validate(
                        StatusTransitions.Kind.DeliveryNote,
                        Status, DeliveryNoteStatusValues.Received).IsValid)
                    return;

                Status = DeliveryNoteStatusValues.Received;
                _entity.Status = DeliveryNoteStatusValues.Received;
                await _deliveryNotes.UpdateAsync(_entity);
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[DN] MarkReceived skipped: {ex.Message}");
            }
        }

        private void OnItemDeliveredQtyChanged()
        {
            RecalculateTotals();
            MarkDirty();   // v2 §28: user changed a delivered quantity
        }

        private void RecalculateTotals()
        {
            // Calculate totals from ALL items (not filtered), so the KPI strip
            // shows the complete picture even when fully delivered items are hidden
            var totals = Services.DeliveryNote.DeliveryNoteLineService.CalculateTotals(
                _entity?.Items ?? Enumerable.Empty<Models.DeliveryNote.SavedDeliveryNote.DeliveryNoteItem>());
            TotalOrderedQty = totals.Ordered;
            TotalPreviouslyDeliveredQty = totals.PrevDelivered;
            TotalDeliveredQty = totals.Delivered;
            TotalBalanceQty = totals.Balance;
        }
    }
}
