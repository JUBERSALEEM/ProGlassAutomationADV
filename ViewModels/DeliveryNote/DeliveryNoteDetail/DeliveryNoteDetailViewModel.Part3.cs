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

            // ERP hardening (Phase 3, G-4): a posted DN (not hard-deletable)
            // uses STRICT mode so a failed verification fails closed rather
            // than orphan a linked tax invoice.
            bool strict = !ProGlassAutomation.Services.Workflow.DocumentLifecyclePolicy.CanHardDelete(
                ProGlassAutomation.Services.Workflow.StatusTransitions.Kind.DeliveryNote,
                _entity.Status, hasChildren: false);
            var guard = await _database.CheckDependencyAsync(
                DependencyGuard.Kind.DeliveryNote,
                _entity.DeliveryNoteId, strict);
            bool hasChildren = guard.HasDependents;

            // ERP lifecycle hardening (Phase 1): three-way decision.
            var decision = ProGlassAutomation.Services.Workflow.DocumentLifecyclePolicy.DecideDelete(
                ProGlassAutomation.Services.Workflow.StatusTransitions.Kind.DeliveryNote,
                _entity.Status, hasChildren);

            if (decision == ProGlassAutomation.Services.Workflow.DocumentLifecyclePolicy.DeleteDecision.Blocked)
            {
                _dialogs.Show(guard.Message, LocalizationService.Instance.T("Msg.CapCannotDelete"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (decision == ProGlassAutomation.Services.Workflow.DocumentLifecyclePolicy.DeleteDecision.Void)
            {
                await VoidDnAsync();
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
                    catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("foreach suppressed");
            }
                }

                await _deliveryNotes.DeleteAsync(_entity.Id);

                AuditService.Log(
                    AuditSections.DeliveryNote, "Delivery note deleted",
                    DeliveryNoteId, "DN", DeliveryNoteId);

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
                    string.Format(LocalizationService.Instance.T("Msg.FailedToDeleteX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapDeleteFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        /// <summary>
        /// ERP lifecycle hardening (Phase 1): soft-close (void) this delivery
        /// note when it may not be hard-deleted. Attachments are kept — the
        /// note still exists.
        /// </summary>
        private async Task VoidDnAsync()
        {
            if (_entity == null || _deliveryNotes == null) return;

            var proceed = _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Void.MsgConfirmVoidX"), DeliveryNoteId),
                LocalizationService.Instance.T("Void.Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (proceed != MessageBoxResult.Yes) return;

            var dlg = _dialogs.Create<ProGlassAutomation.Views.Shared.VoidReasonDialog>(DeliveryNoteId);
            if (dlg.ShowDialog() != true) return;
            string reason = dlg.ResultReason;

            try
            {
                IsSaving = true;
                await _deliveryNotes.VoidAsync(_entity.Id, reason);

                AuditService.Log(
                    AuditSections.DeliveryNote, "Delivery note voided",
                    DeliveryNoteId, "DN", DeliveryNoteId, reason);

                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Void.MsgVoidedX"), DeliveryNoteId),
                    LocalizationService.Instance.T("Void.CapVoided"),
                    MessageBoxButton.OK, MessageBoxImage.Information);

                OnRequestBackToList?.Invoke();
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Void.MsgVoidFailedX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Void.CapVoidFailed"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                    string.Format(LocalizationService.Instance.T("Msg.FailedToOpenTaxInvoice"), ExceptionMessages.Deep(ex)),
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
                    string.Format(LocalizationService.Instance.T("Msg.ExportFailedX"), ExceptionMessages.Deep(ex)),
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

        // Sharing Center — same layouts as the exports above, staged once
        // into the share temp folder (rule's default format), then composer.
        private async Task ShareAsync()
        {
            if (!EnsureLoaded(out var entity, out var snapshot)) return;

            var context = ProGlassAutomation.Services.Sharing
                .ShareContextFactory.FromDeliveryNote(entity, snapshot);

            await ProGlassAutomation.Services.Sharing.ShareLauncher
                .GenerateAndShareAsync(context, async folder =>
                {
                    string ruleFormat = ProGlassAutomation.App.Config.Sharing
                        .RuleFor("DN").DefaultFormat ?? "Pdf";
                    ProGlassAutomation.Services.Export.Core.IDocumentLayout layout;
                    ProGlassAutomation.Services.Export.Core.ExportFormat format;
                    if (string.Equals(ruleFormat, "Excel",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        layout = new DeliveryNoteExcelLayout(entity, snapshot);
                        format = ProGlassAutomation.Services.Export.Core.ExportFormat.Excel;
                    }
                    else if (string.Equals(ruleFormat, "Csv",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        layout = new DeliveryNoteCsvLayout(entity, snapshot);
                        format = ProGlassAutomation.Services.Export.Core.ExportFormat.Csv;
                    }
                    else
                    {
                        layout = new DeliveryNotePdfLayout(entity, snapshot);
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
                    string.Format(LocalizationService.Instance.T("Msg.FailedToUploadImageX"), ExceptionMessages.Deep(ex)),
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
                    string.Format(LocalizationService.Instance.T("Msg.FailedToUploadPDFX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapUploadFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally { IsSaving = false; }
        }

        /// <summary>
        /// Opens an attachment.
        ///
        /// Uses the ASYNC service overload: the synchronous one validates
        /// with .GetAwaiter().GetResult(), which does file I/O and MIME
        /// sniffing on the UI thread and visibly stalls the window for a
        /// large attachment or a network path.
        /// Phase 1 C-3 fix: async void → SafeFireAndForget (no unhandled crash)
        private void OpenAttachment(DnAttachmentRowViewModel row)
        {
            AsyncUtility.SafeFireAndForget(() => OpenAttachmentAsync(row), "DN OpenAttachment");
        }

        private async Task OpenAttachmentAsync(DnAttachmentRowViewModel row)
        {
            var result = await _attachmentService
                .OpenInDefaultAppAsync(row.FilePath)
                .ConfigureAwait(true);

            if (result.IsFailure)
            {
                // SafeFireAndForget will catch this and log it; we show the
                // dialog ourselves so the user gets a clear explanation.
                ViewModels.Shared.UiDispatch.Run(() =>
                {
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.CouldNotOpenFileX"), result.Error),
                        LocalizationService.Instance.T("Msg.CapOpenFailed"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
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
                    string.Format(LocalizationService.Instance.T("Msg.FailedToDeleteAttachmentX"), ExceptionMessages.Deep(ex)),
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
                    $"[DN] MarkReceived skipped: {ExceptionMessages.Deep(ex)}");
            }
        }

        private void OnItemDeliveredQtyChanged()
        {
            RecalculateTotals();
            MarkDirty();   // v2 §28: user changed a delivered quantity
            // Dispatch / Save / Fill-remaining / Reset all gate their
            // CanExecute on the live delivered/balance qty, so re-query them
            // as soon as a row changes — otherwise the Dispatch button stayed
            // disabled until some unrelated property nudged the command manager.
            ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
        }

        /// <summary>
        /// Pushes the live edited row VMs back onto the persisted entity items
        /// and recomputes the entity's stored totals. Single source of the
        /// row→entity mapping, shared by Save and Dispatch (both used to
        /// duplicate this block, which is how they could drift).
        /// </summary>
        private void SyncRowsToEntity()
        {
            if (_entity == null) return;

            foreach (var row in ItemRows)
            {
                var item = _entity.Items.FirstOrDefault(i =>
                    i.SpecGroupNumber == row.SpecGroupNumber &&
                    i.SerialNumber == row.SerialNumber);
                if (item == null) continue;

                item.DeliveredQty = row.DeliveredQty;
                item.BalanceQty = Math.Max(0,
                    item.OrderedQty - item.PreviouslyDeliveredQty - item.DeliveredQty);
                item.Notes = row.Notes;
            }

            _entity.TotalOrderedQty = _entity.Items.Sum(i => i.OrderedQty);
            _entity.TotalDeliveredQty = _entity.Items.Sum(i => i.DeliveredQty);
            _entity.TotalBalanceQty = _entity.Items.Sum(i => i.BalanceQty);
            _entity.TotalPreviouslyDeliveredQty =
                _entity.Items.Sum(i => i.PreviouslyDeliveredQty);
        }

        private void RecalculateTotals()
        {
            // SOURCE OF TRUTH = _itemRows (the live, editable row VMs the user
            // types into), NOT _entity.Items. The entity's Items are only
            // synced back on Save/Dispatch, and they are deduped/filtered at
            // load — so reading them here made the "This DN", Balance and
            // Progress KPIs frozen while the user edited a delivered qty
            // (the reported bug). Totals are computed over ALL rows (not the
            // filtered VisibleItemRows) so the KPI strip still shows the
            // complete picture even when fully-delivered lines are hidden.
            var totals = Services.DeliveryNote.DeliveryNoteLineService.CalculateTotals(_itemRows);
            TotalOrderedQty = totals.Ordered;
            TotalPreviouslyDeliveredQty = totals.PrevDelivered;
            TotalDeliveredQty = totals.Delivered;
            TotalBalanceQty = totals.Balance;
        }
    }
}
