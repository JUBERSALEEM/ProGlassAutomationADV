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

            try
            {
                IsSaving = true;
                StatusMessage = "Saving…";

                if (_taxInvoices == null)
                {
                    StatusMessage = "Database not initialized.";
                    return;
                }

                var state = BuildWizardState();
                string savedInvoiceNumber;
                string savedSnapshotJson = "";   // v2 §24 version capture

                if (IsNewInvoice)
                {
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

                    var updated = TaxInvoiceMapper.BuildEntity(
                        state, entity.InvoiceNumber,
                        entity.SequenceNumber, entity.SequenceLetter);
                    updated.Id = entity.Id;
                    updated.CreatedAt = entity.CreatedAt;
                    savedSnapshotJson = updated.FullSnapshotJson;

                    await _taxInvoices.UpdateAsync(updated);
                    savedInvoiceNumber = entity.InvoiceNumber;
                    StatusMessage = $"✅ Updated {entity.InvoiceNumber}";
                }

                // ── CHAT 19 INVOICED: mark DNs as invoiced after TI saved ────────
                //
                // DeliveryNoteReference stores DN IDs in two formats:
                //   Single DN : "DN-2026-001-01"
                //   Multiple  : "DN-2026-001-01,DN-2026-001-02,DN-2026-001-03"
                //
                // We parse all IDs and mark each one as invoiced.
                // MarkMultipleAsInvoicedAsync handles both single and multiple.
                // It is idempotent — safe to call even if already marked.
                // Only marks DNs that exist in the DB — missing IDs silently skipped.
                //
                // This fires regardless of Insert or Update so that:
                //   a) Newly saved TI immediately marks DNs
                //   b) Re-saving existing TI keeps DNs marked (idempotent)

                // Single audit point — both insert and update reach here.
                AuditService.Log(
                    AuditSections.TaxInvoice, "Tax invoice saved",
                    $"{savedInvoiceNumber} — status {Status}",
                    "TI", savedInvoiceNumber);

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

                await MarkDnsAsInvoicedAsync(savedInvoiceNumber);

                // Chat 16: reload family sidebar after save
                await LoadFamilyAsync(ProformaId);
            }
            catch (Exception ex)
            {
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
        /// CHAT 19 INVOICED — marks all referenced DNs as invoiced.
        /// Called after every successful TI save (insert or update).
        ///
        /// Parses DeliveryNoteReference field which may contain:
        ///   "DN-2026-001-01"                          ← single DN
        ///   "DN-2026-001-01,DN-2026-001-02"           ← multiple DNs (comma)
        ///
        /// Uses MarkMultipleAsInvoicedAsync which is:
        ///   • Idempotent — safe to call multiple times
        ///   • Batch — one DB call for all IDs
        ///   • Null-safe — empty/blank IDs filtered before DB call
        ///
        /// Non-fatal: if DN marking fails, TI is already saved.
        /// Error is logged to debug output only — no user-facing error.
        /// </summary>
        private async Task MarkDnsAsInvoicedAsync(string taxInvoiceId)
        {
            if (_deliveryNotes == null) return;
            if (string.IsNullOrWhiteSpace(DeliveryNoteReference)) return;
            if (string.IsNullOrWhiteSpace(taxInvoiceId)) return;

            try
            {
                // Phase 11/12: reuse the shared DN-reference parser (single
                // implementation, also used by the reconciliation sweep).
                var dnIds = ProGlassAutomation.Services.TaxInvoice.Repository
                    .TaxInvoiceDnReconciler.ParseDnRefs(DeliveryNoteReference)
                    .ToList();

                if (dnIds.Count == 0) return;

                // Mark all referenced DNs as invoiced in one DB call
                await _deliveryNotes.MarkMultipleAsInvoicedAsync(
                    dnIds, taxInvoiceId);

                AppLogService.Debug(
                    $"[TaxInvoiceDetailVM] Marked {dnIds.Count} DN(s) as " +
                    $"invoiced with TI '{taxInvoiceId}': " +
                    string.Join(", ", dnIds));
            }
            catch (Exception ex)
            {
                // ══════════════════════════════════════════════════════
                //  AUDIT D-4 / R-1 — this is the cross-database seam.
                //
                //  The tax invoice is already committed to taxinvoices.db.
                //  This second write targets deliverynotes.db, a DIFFERENT
                //  database file, so no transaction can span the two. If
                //  it fails, the books disagree: an invoice exists while
                //  its delivery notes still look un-invoiced, so the same
                //  DNs can be invoiced again.
                //
                //  It was reported ONLY through Debug.WriteLine, which is
                //  [Conditional("DEBUG")] and therefore absent from the
                //  Release build — the inconsistency was completely
                //  invisible in production. It now reaches the log file
                //  and the audit trail so it can be found and corrected.
                //
                //  Still deliberately non-fatal: failing the whole save
                //  here would be worse, because the invoice IS saved and
                //  telling the user otherwise would be a lie. Re-saving
                //  the invoice re-runs this and repairs the link, since
                //  MarkMultipleAsInvoicedAsync is idempotent.
                // ══════════════════════════════════════════════════════
                ProGlassAutomation.Services.Logging.AppLogService.Error(
                    "Tax invoice saved but its delivery notes could not be " +
                    "marked as invoiced. Re-save the invoice to repair the " +
                    $"link. TI='{taxInvoiceId}', DNs='{DeliveryNoteReference}'.",
                    ex,
                    new ProGlassAutomation.Services.Logging.LogContext
                    {
                        Module = "TaxInvoice",
                        Class = nameof(TaxInvoiceDetailViewModel),
                        Operation = "MarkDnsAsInvoiced"
                    });

                try
                {
                    AuditService.Log(
                        AuditSections.TaxInvoice,
                        "DN invoiced-link FAILED",
                        $"TI {taxInvoiceId} saved, but delivery notes " +
                        $"'{DeliveryNoteReference}' were not marked invoiced. " +
                        "Re-save to repair.");
                }
                catch (System.Exception auditEx)
                {
                    // Audit failure must not mask the original problem.
                    ProGlassAutomation.Services.Logging.AppLogService.Warning("[TaxInvoiceDetailViewModel] Audit write failed after TI save: " + auditEx.Message);
                }
            }
        }

    }
}
