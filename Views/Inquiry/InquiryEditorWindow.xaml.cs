using Microsoft.Win32;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.ViewModels.Inquiry;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Inquiry
{
    /// <summary>
    /// Inquiry workspace window. UI plumbing only:
    /// - Ctrl+S saves, Esc closes (with the VM's discard confirmation);
    /// - drag-and-drop attachment intake (A-drop zone);
    /// - file dialog + confirm callbacks;
    /// - attachment preview.
    ///
    /// A6 fix: the preview uses the ctor-injected InquiryAttachmentService —
    /// never the service locator (App.ServiceProvider may be null when the
    /// DI composition fell back to manual construction at startup).
    /// </summary>
    public partial class InquiryEditorWindow : Window
    {
        private readonly InquiryAttachmentService _attachmentService;
        private readonly InquiryEditorViewModel _viewModel;

        public InquiryEditorWindow(IInquiryWorkflowService workflow,
            InquiryAttachmentService attachments, InquiryRecord? record = null)
        {
            InitializeComponent();
            _attachmentService = attachments ?? throw new ArgumentNullException(nameof(attachments));
            var vm = new InquiryEditorViewModel(workflow, attachments, record);
            _viewModel = vm;
            vm.CloseRequested += saved => { DialogResult = saved; };
            vm.SelectFilesRequested = SelectFiles;
            vm.ConfirmDiscardRequested = () => MessageBox.Show(
                "Discard unsaved changes to this inquiry?", "Unsaved changes",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
            vm.ConfirmDeleteRequested = file => MessageBox.Show(
                $"Delete attachment '{file.FileName}'? The file is removed from disk as well.",
                "Delete attachment",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
            vm.PreviewRequested = Preview;
            DataContext = vm;
            // Put the caret where the story starts.
            Loaded += (_, __) =>
            {
                CustomerInput.Focus();
                Keyboard.Focus(CustomerInput);
            };
        }

        private static string[]? SelectFiles()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Add inquiry attachments", Multiselect = true,
                Filter = "Supported files|*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.dwg;*.dxf;*.xlsx;*.xls;*.csv|PDF|*.pdf|Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|All files|*.*"
            };
            return dialog.ShowDialog() == true ? dialog.FileNames.ToArray() : null;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                if (_viewModel.SaveCommand.CanExecute(null)) _viewModel.SaveCommand.Execute(null);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                _viewModel.CancelCommand.Execute(null);
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            bool files = e.Data.GetDataPresent(DataFormats.FileDrop);
            e.Effects = files ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void OnFileDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;
            e.Handled = true;
            try
            {
                await _viewModel.AddDroppedFilesAsync(paths);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not attach the dropped files: " + ex.Message,
                    "Attachment", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Preview(InquiryAttachment attachment) =>
            new InquiryAttachmentPreviewWindow(attachment, _attachmentService) { Owner = this }.ShowDialog();
    }
}
