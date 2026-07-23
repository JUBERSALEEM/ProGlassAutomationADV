using System.IO;
using System.Linq;
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
    ///   - Enter = Save (IsDefault)
    ///   - Escape = Cancel (IsCancel + KeyDown fallback)
    ///   - Click header = drag window (WindowStyle=None + AllowsTransparency)
    ///
    /// Usage:
    ///   var dlg = new RenameAttachmentDialog("IMG_1234.jpg")
    ///   {
    ///       Owner = Application.Current.MainWindow
    ///   };
    ///   if (dlg.ShowDialog() == true)
    ///   {
    ///       var newName = dlg.ResultFileName;  // e.g. "front-view.jpg"
    ///   }
    /// </summary>
    public partial class RenameAttachmentDialog : Window
    {
        /// <summary>
        /// Final filename including extension after user clicks Save.
        /// Only valid if ShowDialog() returned true.
        /// </summary>
        public string ResultFileName { get; private set; } = "";

        private readonly string _extension;

        public RenameAttachmentDialog(string originalFileName)
        {
            InitializeComponent();

            _extension = Path.GetExtension(originalFileName);
            var nameOnly = Path.GetFileNameWithoutExtension(originalFileName);

            ExtensionLabel.Text = _extension;
            FileNameTextBox.Text = nameOnly;

            // Select the whole name on load so user can start typing to replace it
            FileNameTextBox.Loaded += (_, __) =>
            {
                FileNameTextBox.Focus();
                FileNameTextBox.SelectAll();
            };
        }

        // ── Frameless window drag support ────────────────────────────────
        private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        // ── Escape fallback (in case IsCancel doesn't intercept early) ───
        private void OnFileNameKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            var name = FileNameTextBox.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError("Filename cannot be empty.");
                return;
            }

            var invalid = Path.GetInvalidFileNameChars();
            if (name.Any(c => invalid.Contains(c)))
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
            DialogResult = true;
            Close();
        }

        private void ShowError(string msg)
        {
            ValidationText.Text = "⚠  " + msg;
            ValidationBorder.Visibility = Visibility.Visible;
        }
    }
}