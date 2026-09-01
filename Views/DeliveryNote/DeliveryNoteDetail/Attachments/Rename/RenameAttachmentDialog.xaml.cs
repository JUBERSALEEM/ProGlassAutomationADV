using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.Views.DeliveryNote.DeliveryNoteDetail.Attachments.Rename
{
    /// <summary>
    /// Modal dialog for renaming an attachment on upload (Phase 10.6a).
    ///
    /// Design (Phase 10.6a polish):
    ///   - Frameless glassy card, 20px rounded corners, drop shadow
    ///   - Custom title bar with drag support + close button
    ///   - Gradient icon pill + title + subtitle
    ///   - Filename TextBox + attached extension pill (indigo focus glow)
    ///   - Inline red-tinted validation banner (only when error present)
    ///   - Gradient Save button + soft Cancel — footer strip with tint
    ///
    /// Interaction:
    ///   - Enter = Save (IsDefault → SaveCommand)
    ///   - Escape = Cancel (IsCancel)
    ///   - Click header = drag window (WindowStyle=None + AllowsTransparency)
    ///
    /// 2026-09-02 (Phase 3.7): editing + validation moved to
    /// ViewModels/DeliveryNote/DeliveryNoteDetail/RenameAttachmentViewModel.cs
    /// (WorkflowProgressDialog pattern — the view wires RequestClose to
    /// DialogResult). Remaining here are the by-design window keeps:
    /// frameless drag + focus/select-all on load.
    ///
    /// Usage:
    ///   var dlg = _dialogs.Create<RenameAttachmentDialog>("IMG_1234.jpg");
    ///   if (dlg.ShowDialog() == true)
    ///       var newName = dlg.ResultFileName;  // e.g. "front-view.jpg"
    /// </summary>
    public partial class RenameAttachmentDialog : Window
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

        // ── Frameless window drag support (window-chrome keep) ────────────
        private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
