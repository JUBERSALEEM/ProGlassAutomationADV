// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/DeliveryNote/DeliveryNoteDetail/RenameAttachmentViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Delivery Note — attachment rename dialog (Phase 10.6a).
//
// 2026-09-02 (Phase 3.7): the rename dialog's controller logic moved out
// of Views/DeliveryNote/DeliveryNoteDetail/Attachments/Rename/RenameAttachmentDialog.xaml.cs
// into this VM, following the repo's VM-dialog pattern (see
// WorkflowProgressViewModel): the view wires RequestClose to DialogResult.
//
// Contract preserved for callers (`_dialogs.Create<RenameAttachmentDialog>
// (origName)` then `dlg.ResultFileName`): the dialog still exposes
// ResultFileName, forwarding to this VM.
//
// Layout note: the validation strip is deliberately Hidden (never
// Collapsed) when clean so the fixed-height card never re-measures —
// hence ValidationVisibility is a Visibility-typed property bound
// directly (the built-in BooleanToVisibilityConverter would map
// False→Collapsed and break that design).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail
{
    /// <summary>Modal-dialog VM: edit + validate a new attachment file
    /// name, then report the result via RequestClose.</summary>
    public sealed class RenameAttachmentViewModel : ViewModelBase
    {
        private readonly string _extension;

        /// <summary>Extension pill shown right of the text box
        /// (e.g. ".jpg" — stays fixed; only the name is editable).</summary>
        public string Extension { get; }

        /// <summary>Editable file name without extension (TwoWay bound).</summary>
        public string FileName { get; set; } = "";

        /// <summary>Final filename including extension. Valid only after a
        /// successful Save (RequestClose(true)).</summary>
        public string ResultFileName { get; private set; } = "";

        /// <summary>Validation strip text ("⚠  …"). Empty when clean.</summary>
        public string ValidationMessage { get; private set; } = "";

        /// <summary>Validation strip visibility — Hidden (not Collapsed)
        /// when clean so the card keeps its fixed height.</summary>
        public Visibility ValidationVisibility { get; private set; } = Visibility.Hidden;

        /// <summary>View wiring point: result=true → DialogResult=true.
        /// null until the window assigns it.</summary>
        public Action<bool>? RequestClose { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public RenameAttachmentViewModel(string originalFileName)
        {
            _extension = Path.GetExtension(originalFileName);
            Extension = _extension;
            FileName = Path.GetFileNameWithoutExtension(originalFileName);
            SaveCommand = new RelayCommand(_ => Save());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        private void Save()
        {
            var name = FileName?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError("Filename cannot be empty.");
                return;
            }

            if (name.Any(c => Path.GetInvalidFileNameChars().Contains(c)))
            {
                ShowError("Filename contains invalid characters: \\ / : * ? \" < > |");
                return;
            }

            if (name.Length > 200)
            {
                ShowError("Filename is too long (max 200 characters).");
                return;
            }

            ResultFileName = name + _extension;
            RequestClose?.Invoke(true);
        }

        private void ShowError(string msg)
        {
            ValidationMessage = "⚠  " + msg;
            ValidationVisibility = Visibility.Visible;
            OnPropertyChanged(nameof(ValidationMessage));
            OnPropertyChanged(nameof(ValidationVisibility));
        }
    }
}
