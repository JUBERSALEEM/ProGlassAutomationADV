// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step8Attachments/
//       AttachmentsStepViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Proforma Invoice — Step 8 Attachments
//
// Wizard step 8: attach site photos / drawings / approvals to the
// invoice. UX mirrors the Delivery Note attachments section:
//   • Add Image / Add PDF buttons (file picker → optional rename →
//     copy into the invoice folder)
//   • Row list with preview, size, date
//   • Open / Rename / Delete per row
//
// Persistence is manifest-file based (ProformaAttachmentService) so
// attachments work for ANY invoice without an EF schema migration.
// Files are keyed by the Proforma ID from Step 1, so they survive
// closing and re-opening the invoice.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Win32;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.DeliveryNote.Attachments;
using ProGlassAutomation.Services.ProformaInvoice.Attachments;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step1CompanySetup;
using ProGlassAutomation.Views.DeliveryNote.DeliveryNoteDetail.Attachments.Rename;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step8Attachments
{
    /// <summary>
    /// ViewModel for wizard Step 8 — Attachments.
    /// Reads the Proforma ID live from Step 1 (CompanySetupViewModel).
    /// </summary>
    public class AttachmentsStepViewModel : ViewModelBase
    {
        private readonly CompanySetupViewModel _company;
        private readonly ProformaAttachmentService _attachments = new();

                private readonly IDialogService _dialogs = null!;

public AttachmentsStepViewModel(IDialogService? dialogs,
            CompanySetupViewModel company)
        {
            _dialogs = dialogs!;
            _company = company;

            AddImageCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(AddImageAsync, "PI Step8"));
            AddPdfCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(AddPdfAsync, "PI Step8"));
            OpenAttachmentCommand = new RelayCommand(param =>
            {
                if (param is ProformaAttachmentRowViewModel row)
                    OpenAttachment(row);
            });
            RenameAttachmentCommand = new RelayCommand(
                param => AsyncUtility.SafeFireAndForget(() => RenameAttachmentAsync(
                    param as ProformaAttachmentRowViewModel), "PI Step8"));
            DeleteAttachmentCommand = new RelayCommand(
                param => AsyncUtility.SafeFireAndForget(() => DeleteAttachmentAsync(
                    param as ProformaAttachmentRowViewModel), "PI Step8"));
            RefreshCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(LoadAsync, "PI Step8"));

            // Follow Proforma ID changes (new invoice / id edit in Step 1)
            _company.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CompanySetupViewModel.ProformaId))
                {
                    OnPropertyChanged(nameof(ProformaId));
                    OnPropertyChanged(nameof(HasProformaId));
                    AsyncUtility.SafeFireAndForget(LoadAsync, "PI Step8");
                }
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════════

        private ObservableCollection<ProformaAttachmentRowViewModel>
            _attachmentsList = new();

        public ObservableCollection<ProformaAttachmentRowViewModel> Attachments
        {
            get => _attachmentsList;
            private set
            {
                _attachmentsList = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AttachmentsCount));
                OnPropertyChanged(nameof(HasNoAttachments));
                OnPropertyChanged(nameof(HasAttachments));
            }
        }

        public int AttachmentsCount => Attachments?.Count ?? 0;
        public bool HasNoAttachments => AttachmentsCount == 0;
        public bool HasAttachments => AttachmentsCount > 0;

        public string ProformaId => _company.ProformaId ?? "";
        public bool HasProformaId => !string.IsNullOrWhiteSpace(ProformaId);

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set { _isBusy = value; OnPropertyChanged(); }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════════
        //  COMMANDS
        // ══════════════════════════════════════════════════════════════

        public ICommand AddImageCommand { get; }
        public ICommand AddPdfCommand { get; }
        public ICommand OpenAttachmentCommand { get; }
        public ICommand RenameAttachmentCommand { get; }
        public ICommand DeleteAttachmentCommand { get; }
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// Raised after one or more files are successfully uploaded. The
        /// wizard shell uses this to continue the approval flow (show the
        /// Signed-PI / LPO confirmation) when the invoice was opened at Step 8
        /// for approval.
        /// </summary>
        public event Action? AttachmentUploaded;

        // ══════════════════════════════════════════════════════════════
        //  LOAD
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads the manifest for the current Proforma ID. Called when
        /// the step is entered and whenever the ID changes.
        /// </summary>
        public async Task LoadAsync()
        {
            if (!HasProformaId)
            {
                Attachments =
                    new ObservableCollection<ProformaAttachmentRowViewModel>();
                return;
            }

            try
            {
                var records = await _attachments.LoadManifestAsync(ProformaId);

                Attachments = new ObservableCollection
                    <ProformaAttachmentRowViewModel>(
                        records.Select(r =>
                            new ProformaAttachmentRowViewModel(r)));

                StatusMessage = "";
            }
            catch (Exception ex)
            {
                StatusMessage = $"⚠ Could not load attachments: {ExceptionMessages.Deep(ex)}";
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  ADD IMAGE / PDF
        // ══════════════════════════════════════════════════════════════

        private async Task AddImageAsync()
        {
            await AddFileAsync(
                AttachmentLimits.ImageDialogFilter,
                isImage: true);
        }

        private async Task AddPdfAsync()
        {
            await AddFileAsync(
                AttachmentLimits.PdfDialogFilter,
                isImage: false);
        }

        private async Task AddFileAsync(string dialogFilter, bool isImage)
        {
            if (!HasProformaId)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.SetAProformaIDIn"),
                    LocalizationService.Instance.T("DeliveryNote.Attachments"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = dialogFilter,
                Multiselect = true,
                Title = isImage
                    ? "Select images to attach"
                    : "Select PDFs to attach"
            };

            if (dialog.ShowDialog() != true) return;

            IsBusy = true;
            int added = 0;

            try
            {
                foreach (var sourcePath in dialog.FileNames)
                {
                    // ── Validate (same rules as Delivery Note) ────────
                    bool validType = isImage
                        ? _attachments.IsValidImageExtension(sourcePath)
                        : _attachments.IsValidPdfExtension(sourcePath);

                    if (!validType)
                    {
                        StatusMessage =
                            $"⚠ Skipped {System.IO.Path.GetFileName(sourcePath)}" +
                            " — unsupported file type.";
                        continue;
                    }

                    if (!_attachments.IsUnderSizeLimit(sourcePath))
                    {
                        StatusMessage =
                            $"⚠ Skipped {System.IO.Path.GetFileName(sourcePath)}" +
                            $" — larger than {AttachmentLimits.MaxFileSizeDisplay}.";
                        continue;
                    }

                    // ── Optional rename before upload ─────────────────
                    string originalName =
                        System.IO.Path.GetFileName(sourcePath);

                    var renameDialog = _dialogs.Create<RenameAttachmentDialog>(originalName);

                    if (renameDialog.ShowDialog() != true)
                        continue;   // user cancelled this file

                    // ── Copy file + persist manifest ──────────────────
                    var record = isImage
                        ? await _attachments.CopyImageAsync(
                            sourcePath, ProformaId)
                        : await _attachments.CopyPdfAsync(
                            sourcePath, ProformaId);

                    // Apply the user's chosen name (service handles
                    // the physical rename + manifest update).
                    if (!string.Equals(
                            renameDialog.ResultFileName, record.FileName,
                            StringComparison.Ordinal))
                    {
                        await _attachments.RenameAsync(
                            ProformaId, record.Id,
                            renameDialog.ResultFileName);
                    }

                    added++;
                }

                await LoadAsync();

                if (added > 0)
                {
                    StatusMessage =
                        $"✓ {added} attachment{(added == 1 ? "" : "s")} added.";

                    AuditService.Log(
                        AuditSections.Proforma,
                        "Attachment added",
                        $"{added} file(s) attached to {ProformaId}",
                        "PI", ProformaId);

                    // Let the shell continue a pending approval flow.
                    AttachmentUploaded?.Invoke();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"⚠ Upload failed: {ExceptionMessages.Deep(ex)}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  OPEN / RENAME / DELETE
        // ══════════════════════════════════════════════════════════════

        private void OpenAttachment(ProformaAttachmentRowViewModel? row)
        {
            if (row == null) return;

            try
            {
                _attachments.OpenInDefaultApp(row.FilePath);

                AuditService.Log(
                    AuditSections.Proforma, "Attachment opened",
                    row.FileName, "PI", ProformaId);
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.CannotOpenFileX"), ex.Message),
                    LocalizationService.Instance.T("DeliveryNote.Attachments"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async Task RenameAttachmentAsync(
            ProformaAttachmentRowViewModel? row)
        {
            if (row == null) return;

            var dialog = _dialogs.Create<RenameAttachmentDialog>(row.FileName);

            if (dialog.ShowDialog() != true) return;

            bool ok = await _attachments.RenameAsync(
                ProformaId, row.Id, dialog.ResultFileName);

            if (ok)
            {
                StatusMessage = "✓ Renamed.";
                await LoadAsync();

                AuditService.Log(
                    AuditSections.Proforma, "Attachment renamed",
                    $"{row.FileName} → {dialog.ResultFileName}",
                    "PI", ProformaId);
            }
            else
            {
                StatusMessage = "⚠ Rename failed — file may be in use.";
            }
        }

        private async Task DeleteAttachmentAsync(
            ProformaAttachmentRowViewModel? row)
        {
            if (row == null) return;

            var confirm = _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.RemoveAttachmentXTheFile"), row.FileName),
                LocalizationService.Instance.T("Msg.CapDeleteAttachment"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            bool ok = await _attachments.DeleteAsync(ProformaId, row.Id);

            if (ok)
            {
                StatusMessage = "✓ Attachment removed.";
                await LoadAsync();

                AuditService.Log(
                    AuditSections.Proforma, "Attachment deleted",
                    row.FileName, "PI", ProformaId);
            }
            else
            {
                StatusMessage = "⚠ Delete failed.";
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  SAFE FIRE — async command helper (never crashes the wizard)
        // ══════════════════════════════════════════════════════════════

        
    }
}
