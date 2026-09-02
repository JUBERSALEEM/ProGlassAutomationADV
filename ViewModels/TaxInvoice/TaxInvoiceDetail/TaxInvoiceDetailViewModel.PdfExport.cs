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

    }
}
