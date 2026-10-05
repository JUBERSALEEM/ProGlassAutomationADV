// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.PdfExport.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.PdfExport members.
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
        //  PDF EXPORT (unchanged from Chat 14)
        // ══════════════════════════════════════════════════════════════════

        private async Task ExportPdfAsync()
        {
            if (_taxInvoices == null || _editingId == 0) return;

            try
            {
                IsSaving = true;
                StatusMessage = "Generating PDF…";

                var entity = await _taxInvoices.GetByIdAsync(_editingId);
                if (entity == null)
                {
                    StatusMessage = "Invoice not found.";
                    return;
                }

                var attachments = await _taxInvoices
                    .GetAttachmentsAsync(entity.InvoiceNumber);

                bool includeAttachments = false;
                if (attachments != null && attachments.Count > 0)
                {
                    var choice = _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.InvoiceHasXAttachmentS"), attachments.Count),
                        LocalizationService.Instance.T("Msg.CapIncludeAttachments"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    includeAttachments = choice == MessageBoxResult.Yes;
                }

                string? savedPath = await Services.TaxInvoice.Pdf
                    .TaxInvoicePdfExporter.SaveToFileAsync(
                        entity,
                        includeAttachments ? attachments : null);

                if (string.IsNullOrWhiteSpace(savedPath))
                {
                    StatusMessage = "PDF export cancelled.";
                    return;
                }

                StatusMessage = $"✅ PDF saved: {System.IO.Path.GetFileName(savedPath)}";

                var open = _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.PDFSavedXOpenNow"), savedPath),
                    LocalizationService.Instance.T("Msg.CapPDFReady"), MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (open == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(savedPath)
                        {
                            UseShellExecute = true
                        });
                }
            }
            catch (ReadOnlyModeException ex)
            {
                StatusMessage = ex.UserMessage;
                _dialogs.Show(ex.UserMessage,
                    LocalizationService.Instance.T("Msg.CapReadOnlyMode"), MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"PDF export failed: {ExceptionMessages.Deep(ex)}";
                _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.FailedX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapPDFExportFailed"), MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        // ── Sharing Center ──────────────────────────────────────────
        // Same generation core as ExportPdfAsync (GenerateAsync /
        // GenerateWithAttachmentsAsync + the same attachments question),
        // staged once into the share temp folder, then the composer.
        private async Task ShareAsync()
        {
            if (_taxInvoices == null || _editingId == 0) return;

            try
            {
                IsSaving = true;
                StatusMessage = "Preparing share…";

                var entity = await _taxInvoices.GetByIdAsync(_editingId);
                if (entity == null)
                {
                    StatusMessage = "Invoice not found.";
                    return;
                }

                var attachments = await _taxInvoices
                    .GetAttachmentsAsync(entity.InvoiceNumber);

                bool includeAttachments = false;
                if (attachments != null && attachments.Count > 0)
                {
                    var choice = _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.InvoiceHasXAttachmentS"), attachments.Count),
                        LocalizationService.Instance.T("Msg.CapIncludeAttachments"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    includeAttachments = choice == MessageBoxResult.Yes;
                }

                var snapshot = Services.TaxInvoice.Mapper
                    .TaxInvoiceMapper.DeserializeSnapshot(entity);
                var context = ProGlassAutomation.Services.Sharing
                    .ShareContextFactory.FromTaxInvoice(entity, snapshot);

                await ProGlassAutomation.Services.Sharing.ShareLauncher
                    .GenerateAndShareAsync(context, async folder =>
                    {
                        byte[] pdf = includeAttachments
                            ? await Services.TaxInvoice.Pdf.TaxInvoicePdfExporter
                                .GenerateWithAttachmentsAsync(entity, attachments)
                            : await Services.TaxInvoice.Pdf.TaxInvoicePdfExporter
                                .GenerateAsync(entity);

                        string safeName = string.Concat(
                            (entity.InvoiceNumber ?? "invoice").Trim()
                                .Select(ch => System.IO.Path.GetInvalidFileNameChars()
                                    .Contains(ch) ? '_' : ch));
                        if (safeName.Length == 0) safeName = "invoice";
                        if (includeAttachments) safeName += "_WITH-ATTACHMENTS";
                        string path = System.IO.Path.Combine(folder, safeName + ".pdf");
                        await System.IO.File.WriteAllBytesAsync(path, pdf);
                        return path;
                    }, _dialogs);

                StatusMessage = "";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Share failed: {ExceptionMessages.Deep(ex)}";
            }
            finally
            {
                IsSaving = false;
            }
        }

    }
}
