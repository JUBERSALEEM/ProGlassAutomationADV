// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.AttachmentManagement.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.AttachmentManagement members.
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
        //  ATTACHMENT MANAGEMENT
        // ══════════════════════════════════════════════════════════════════

        private async Task AddAttachmentAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Attachment",
                Filter = "PDF Files (*.pdf)|*.pdf|" +
                         "Images (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|" +
                         "All Files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                string path = dialog.FileName;
                string fileName = System.IO.Path.GetFileName(path);
                byte[] bytes = await System.IO.File.ReadAllBytesAsync(path);

                if (bytes.Length > 10 * 1024 * 1024)
                {
                    _dialogs.Show(LocalizationService.Instance.T("Msg.FileIsLargerThan10"),
                        LocalizationService.Instance.T("Msg.CapFileTooLarge"), MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
                string mime = ext switch
                {
                    ".pdf" => "application/pdf",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    _ => "application/octet-stream"
                };

                string lower = fileName.ToLowerInvariant();
                string attachType = lower.Contains("pi") || lower.Contains("proforma")
                    ? "SignedPI"
                    : lower.Contains("dn") || lower.Contains("delivery")
                        ? "SignedDN"
                        : "Other";

                var vm = new InvAttachmentViewModel
                {
                    AttachmentType = attachType,
                    FileName = fileName,
                    MimeType = mime,
                    FileBytes = bytes,
                    FileSize = bytes.Length,
                    UploadedAt = DateTime.Now,
                    Description = ""
                };

                Attachments.Add(vm);

                if (!IsNewInvoice && _taxInvoices != null
                    && !string.IsNullOrWhiteSpace(_editingInvoiceNumber))
                {
                    var attachEntity = new TaxInvoiceAttachment
                    {
                        InvoiceNumber = _editingInvoiceNumber,
                        AttachmentType = vm.AttachmentType,
                        FileName = vm.FileName,
                        MimeType = vm.MimeType,
                        FileBytes = vm.FileBytes,
                        FileSize = vm.FileSize,
                        Description = vm.Description
                    };

                    var saved = await _taxInvoices.AddAttachmentAsync(attachEntity);
                    vm.Id = saved.Id;

                    // Update count directly — no DB round-trip needed
                    if (_selectedFamilyMember != null)
                        _selectedFamilyMember.AttachmentCount += 1;
                    OnPropertyChanged(nameof(SelectedFamilyHeaderText));
                }
            }
            catch (Exception ex)
            {
                _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.FailedX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapAttachmentError"), MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task RemoveAttachmentAsync(InvAttachmentViewModel vm)
        {
            var result = _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.RemoveX"), vm.FileName),
                LocalizationService.Instance.T("Msg.CapConfirmRemove"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (vm.Id > 0 && _taxInvoices != null)
                    await _taxInvoices.DeleteAttachmentAsync(vm.Id);

                Attachments.Remove(vm);

                // Update count directly — no DB round-trip needed
                if (_selectedFamilyMember != null && _selectedFamilyMember.AttachmentCount > 0)
                    _selectedFamilyMember.AttachmentCount -= 1;
                OnPropertyChanged(nameof(SelectedFamilyHeaderText));
            }
            catch (ReadOnlyModeException ex)
            {
                _dialogs.Show(ex.UserMessage,
                    LocalizationService.Instance.T("Msg.CapReadOnlyMode"), MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _dialogs.Show(string.Format(LocalizationService.Instance.T("Msg.FailedX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapRemoveFailed"), MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

    }
}
