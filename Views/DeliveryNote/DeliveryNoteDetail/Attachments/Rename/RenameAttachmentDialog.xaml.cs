using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail;
using ProGlassAutomation.Views.Shared.Dialogs;
using System.Windows;

namespace ProGlassAutomation.Views.DeliveryNote.DeliveryNoteDetail.Attachments.Rename
{
    /// <summary>
    /// Modal dialog for renaming an attachment on upload (Phase 10.6a).
    ///
    /// Design (2026-09 dialog audit rebuild):
    ///   - Shared ThemedDialogWindow chrome (header badge/title/subtitle,
    ///     close button, drag, footer strip) — one source of truth
    ///   - Filename TextBox + attached extension pill (indigo focus glow)
    ///   - Inline red-tinted validation banner (only when error present)
    ///
    /// Interaction:
    ///   - Enter = Save (IsDefault → SaveCommand)
    ///   - Escape = Cancel (IsCancel)
    ///   - Header drag handled by the shared chrome
    ///
    /// Usage:
    ///   var dlg = _dialogs.Create&lt;RenameAttachmentDialog&gt;("IMG_1234.jpg");
    ///   if (dlg.ShowDialog() == true)
    ///       var newName = dlg.ResultFileName;  // e.g. "front-view.jpg"
    /// </summary>
    public partial class RenameAttachmentDialog : ThemedDialogWindow
    {
        private readonly RenameAttachmentViewModel _vm;

        /// <summary>
        /// Final filename including extension after user clicks Save.
        /// Only valid if ShowDialog() returned true.
        /// </summary>
        public string ResultFileName => _vm.ResultFileName;

        public RenameAttachmentDialog(string originalFileName)
        {
            InitializeComponent();

            _vm = new RenameAttachmentViewModel(originalFileName);
            _vm.RequestClose = result =>
            {
                DialogResult = result;
                Close();
            };
            DataContext = _vm;

            // Select the whole name on load so user can start typing to
            // replace it (window-lifecycle keep).
            FileNameTextBox.Loaded += (_, __) =>
            {
                FileNameTextBox.Focus();
                FileNameTextBox.SelectAll();
            };
        }
    }
}
