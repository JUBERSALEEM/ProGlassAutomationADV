// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.Save.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.Save members.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.TaxInvoice.Attachment;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.TaxInvoice;
using ProGlassAutomation.Services.TaxInvoice.Mapper;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList;
using ProGlassAutomation.Views.TaxInvoice.TaxInvoiceDetail;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceDetail
{
    public partial class TaxInvoiceDetailViewModel : ViewModelBase
    {
        // ══════════════════════════════════════════════════════════════════
        //  SAVE (enhanced — reload family after save)
        // ══════════════════════════════════════════════════════════════════

        public async Task SaveAsync()
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.TaxInvoiceEdit, "save a tax invoice"))
                return;

            if (!ValidateBeforeSave()) return;

            // X-6 / D-4: this save spans TWO databases — it writes
            // taxinvoices.db and then updates deliverynotes.db. No
            // transaction can cover both, so being killed between them
            // leaves the books inconsistent. Registering the operation
            // makes the window's Closing handler warn the user instead of
            // terminating mid-sequence.
            using var _critical = ProGlassAutomation.Services.Safety
                .CriticalOperationTracker.Begin("Saving tax invoice");

            string savedInvoiceNumber = _editingInvoiceNumber;
            string savedSnapshotJson = "";
            bool statusOnlyTransition = false;
            bool bodySaveCommitted = false;
            bool statusTransitionAttempted = false;
            bool statusTransitionCommitted = false;

            try
            {
                IsSaving = true;
                StatusMessage = "Saving…";

                if (_taxInvoices == null)
                {
                    StatusMessage = "Database not initialized.";
                    return;
                }

                if (!await EnsureReferencedDnsInvoiceableAsync(
                    IsNewInvoice ? null : _editingInvoiceNumber))
                    return;

                var state = BuildWizardState();

                if (IsNewInvoice)
                {
                    // AUDIT P1 (2026-09-05) — LPO reuse warning. Saving a
                    // NEW invoice whose PO/LPO reference is already used on
                    // an UNRELATED family (different ProformaId) for the
                    // same customer is the classic double-billing path.
                    // Same-family reuse is legitimate (continuation notes on
                    // one PO), so only cross-family hits warn. Non-blocking:
                    // a customer may genuinely re-issue a PO; the choice is
                    // the operator's, the warning is the audit's ask.
                    string po = (PurchaseOrderNumber ?? "").Trim();
                    if (po.Length > 0)
                    {
                        try
                        {
                            var conflicts = await _taxInvoices
                                .GetByPurchaseOrderAsync(po, CustomerName ?? "");
                            var crossFamily = conflicts
                                .Where(c => !string.Equals(
                                    c.ProformaId ?? "", ProformaId ?? "",
                                    StringComparison.OrdinalIgnoreCase))
                                .Take(3)
                                .ToList();
                            if (crossFamily.Count > 0)
                            {
                                string seen = string.Join(", ",
                                    crossFamily.Select(c =>
                                        $"{c.InvoiceNumber} ({c.IssueDate:yyyy-MM-dd})"));
                                var reuse = _dialogs.Show(
                                    "The same LPO / PO number '" + po +
                                    "' already appears on " + seen +
                                    " for customer '" + (CustomerName ?? "") +
                                    "'. Continuing may double-bill the same purchase order.\n\n" +
                                    "Save anyway?",
                                    LocalizationService.Instance.T("Msg.CapValidation"),
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Warning);
                                if (reuse != MessageBoxResult.Yes)
                                {
                                    StatusMessage = "Save cancelled — LPO already in use.";
                                    return;
                                }
                            }
                        }
                        catch (Exception poEx)
                        {
                            // The warning is advisory; it must never block
                            // a legitimate save path because of a failed
                            // READ. Log loudly, continue the save.
                            Services.Logging.AppLogService.Warning(
                                $"[TI Save] PO-reuse check skipped: {poEx.Message}");
                        }
                    }

                    int seq = await _taxInvoices
                        .GetNextSequenceNumberAsync(ProformaId);
                    bool hasExisting = await _taxInvoices
                        .HasAnyInvoiceAsync(ProformaId);
                    string letter = hasExisting
                        ? await _taxInvoices
                            .GetNextSequenceLetterAsync(ProformaId)
                        : "";
                    string invoiceNum = await _taxInvoices
                        .BuildInvoiceNumberAsync(ProformaId, letter);
                    bool exists = await _taxInvoices
                        .ExistsByInvoiceNumberAsync(invoiceNum);

                    if (exists)
                    {
                        StatusMessage = $"Invoice '{invoiceNum}' already exists.";
                        return;
                    }

                    var entity = TaxInvoiceMapper.BuildEntity(
                        state, invoiceNum, seq, letter);
                    savedSnapshotJson = entity.FullSnapshotJson;
                    var saved = await _taxInvoices.InsertAsync(entity);
                    _editingId = saved.Id;
                    _editingInvoiceNumber = saved.InvoiceNumber;
                    savedInvoiceNumber = saved.InvoiceNumber;
                    IsNewInvoice = false;
                    StatusMessage = $"✅ Saved as {saved.InvoiceNumber}";
                }
                else
                {
                    var entity = await _taxInvoices.GetByIdAsync(_editingId);
                    if (entity == null)
                    {
                        StatusMessage = "Invoice not found — cannot update.";
                        return;
                    }
                    if (!_loadedRowVersion.HasValue)
                        throw new ProGlassAutomation.Services.Database.ConcurrencyConflictException(
                            "Tax Invoice", entity.InvoiceNumber,
                            expectedRowVersion: -1, actualRowVersion: entity.RowVersion);

                    string targetStatus = Status;
                    bool statusChanged = !string.Equals(
                        targetStatus, entity.Status, StringComparison.OrdinalIgnoreCase);
                    bool bodyEditableAtStoredStatus =
                        ProGlassAutomation.Services.Workflow.DocumentLifecyclePolicy.CanEditBody(
                            ProGlassAutomation.Services.Workflow.StatusTransitions.Kind.TaxInvoice,
                            entity.Status);
                    if (!bodyEditableAtStoredStatus && !statusChanged)
                    {
                        StatusMessage = "This Tax Invoice is body-locked; choose a valid status transition or cancel the save.";
                        return;
                    }
                    statusOnlyTransition = statusChanged && !bodyEditableAtStoredStatus;

                    if (statusChanged)
                    {
                        var transition = ProGlassAutomation.Services.Workflow.StatusTransitions
                            .Validate(
                                ProGlassAutomation.Services.Workflow.StatusTransitions.Kind.TaxInvoice,
                                entity.Status, targetStatus);
                        if (!transition.IsValid)
                        {
                            StatusMessage = transition.Reason ?? "Invalid Tax Invoice status transition.";
                            return;
                        }
                    }

                    var updated = TaxInvoiceMapper.BuildEntity(
                        state, entity.InvoiceNumber,
                        entity.SequenceNumber, entity.SequenceLetter);
                    updated.Id = entity.Id;
                    updated.CreatedAt = entity.CreatedAt;

                    if (bodyEditableAtStoredStatus)
                    {
                        // Body updates remain status-neutral. First commit the
                        // validated draft body using its stored source status;
                        // then use the repository's status-only transition.
                        TaxInvoiceMapper.SetHeaderStatus(updated, entity.Status);
                        await _taxInvoices.UpdateAsync(updated, _loadedRowVersion.Value);
                        _loadedRowVersion = updated.RowVersion;
                        bodySaveCommitted = true;
                        savedSnapshotJson = updated.FullSnapshotJson;
                        savedInvoiceNumber = entity.InvoiceNumber;
                    }

                    if (statusChanged)
                    {
                        statusTransitionAttempted = true;
                        var statusUpdated = await _taxInvoices.UpdateStatusAsync(
                            entity.InvoiceNumber, targetStatus, _loadedRowVersion.Value);
                        if (statusUpdated != null)
                            _loadedRowVersion = statusUpdated.RowVersion;
                        statusTransitionCommitted = true;
                    }

                    var refreshed = await _taxInvoices.GetByInvoiceNumberAsync(entity.InvoiceNumber);
                    if (refreshed == null)
                        throw new InvalidOperationException("The Tax Invoice no longer exists.");
                    updated = refreshed;
                    _loadedRowVersion = updated.RowVersion;
                    savedSnapshotJson = updated.FullSnapshotJson;
                    savedInvoiceNumber = entity.InvoiceNumber;
                    StatusMessage = $"✅ Updated {entity.InvoiceNumber}";
                }

                // TaxInvoiceRepository synchronizes the complete TI↔DN link set
                // after its own save and throws an explicit partial-commit error
                // if the second database cannot be updated or verified.

                // Single audit point — both insert and update reach here.
                if (!statusOnlyTransition)
                {
                    AuditService.Log(
                        AuditSections.TaxInvoice, "Tax invoice saved",
                        $"{savedInvoiceNumber} — status {Status}",
                        "TI", savedInvoiceNumber);
                }

                CaptureCleanState();   // v2 §28: saved = clean

                // v2 §24: append a restorable version of every save.
                if (!string.IsNullOrEmpty(savedSnapshotJson))
                {
                    _ = ProGlassAutomation.Services.Audit
                        .VersionHistoryService.AddVersionAsync(
                            "TI", savedInvoiceNumber,
                            $"Status {Status}",
                            savedSnapshotJson);
                }

                // TaxInvoiceRepository owns cross-database TI↔DN synchronization;
                // it returns success only after the link set is verified.

                // Chat 16: reload family sidebar after save
                await LoadFamilyAsync(ProformaId);
            }
            catch (TaxInvoiceLinkSynchronizationException linkEx)
            {
                // The invoice row committed, but the second SQLite file did
                // not. Preserve the assigned identity/version so a retry edits
                // this invoice rather than creating a duplicate.
                var persisted = linkEx.PersistedInvoice;
                _editingId = persisted.Id;
                _editingInvoiceNumber = persisted.InvoiceNumber;
                _loadedRowVersion = persisted.RowVersion;
                Status = persisted.Status;
                IsNewInvoice = false;
                CaptureCleanState();
                StatusMessage = linkEx.Message;
                AuditService.Log(
                    AuditSections.TaxInvoice, "Tax invoice saved with link failure",
                    $"Tax Invoice {persisted.InvoiceNumber} committed; DN link synchronization failed.",
                    "TI", persisted.InvoiceNumber, reason: linkEx.InnerException?.Message ?? linkEx.Message);
                _dialogs.Show(
                    linkEx.Message,
                    LocalizationService.Instance.T("Msg.CapSaveFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (ProGlassAutomation.Services.Database.ConcurrencyConflictException cc)
            {
                // ERP hardening (Phase 5): another user saved this invoice
                // after we opened it. Don't overwrite their work — tell the
                // operator to reload.
                StatusMessage = "⚠ Saved elsewhere — reload before saving.";
                _dialogs.Show(cc.Message,
                    LocalizationService.Instance.T("Msg.CapSaveFailed"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                if (bodySaveCommitted && statusTransitionAttempted
                    && !statusTransitionCommitted)
                {
                    StatusMessage =
                        "Tax Invoice body was saved, but the status transition failed: " +
                        ExceptionMessages.Deep(ex);
                    AuditService.Log(
                        AuditSections.TaxInvoice, "Tax invoice body saved; status change failed",
                        $"Tax Invoice {savedInvoiceNumber} body committed; requested status " +
                            $"'{Status}' was not confirmed.",
                        "TI", savedInvoiceNumber,
                        reason: ExceptionMessages.Deep(ex));
                    if (!string.IsNullOrEmpty(savedSnapshotJson))
                    {
                        _ = ProGlassAutomation.Services.Audit.VersionHistoryService
                            .AddVersionAsync(
                                "TI", savedInvoiceNumber,
                                "Body saved; status transition failed",
                                savedSnapshotJson);
                    }
                    _dialogs.Show(StatusMessage,
                        LocalizationService.Instance.T("Msg.CapSaveFailed"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StatusMessage = $"Save failed: {ExceptionMessages.Deep(ex)}";
                _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.FailedToSaveX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapSaveFailed"), MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        /// <summary>
        /// Confirmed, not-already-billed DNs only. Draft/Dispatching/Master
        /// and a DN already marked to a different TI cannot be saved.
        /// </summary>
        private async Task<bool> EnsureReferencedDnsInvoiceableAsync(
            string? thisInvoiceNumber)
        {
            if (string.IsNullOrWhiteSpace(DeliveryNoteReference)) return true;

            var dnIds = TaxInvoiceDnReconciler.ParseDnRefs(DeliveryNoteReference);
            if (dnIds.Count == 0) return true;
            if (_deliveryNotes == null)
            {
                const string unavailable = "Delivery Note repository is unavailable; the Tax Invoice cannot be saved with unverified links.";
                StatusMessage = unavailable;
                _dialogs.Show(unavailable,
                    LocalizationService.Instance.T("Msg.CapSaveFailed"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            foreach (var id in dnIds)
            {
                var dn = await _deliveryNotes.GetByDeliveryNoteIdAsync(id);
                string? problem = null;
                if (dn == null)
                    problem = $"Delivery Note '{id}' does not exist.";
                else if (dn.IsVoid)
                    problem = $"Delivery Note '{id}' is voided and cannot be invoiced.";
                else if (!TaxInvoiceLineService.CanInvoiceDeliveryNote(
                             dn.IsMasterDn, dn.Status, dn.HasTaxInvoice,
                             dn.TaxInvoiceId, thisInvoiceNumber))
                    problem = dn.HasTaxInvoice
                        ? $"Delivery Note '{id}' is already invoiced as {dn.TaxInvoiceId}."
                        : $"Delivery Note '{id}' is not a confirmed delivery (status {dn.Status}).";

                if (problem == null) continue;
                StatusMessage = problem;
                _dialogs.Show(problem,
                    LocalizationService.Instance.T("Msg.CapSaveFailed"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }


    }
}
