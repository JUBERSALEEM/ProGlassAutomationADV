using Microsoft.Win32;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.ViewModels.Inquiry;
using System.Windows;

namespace ProGlassAutomation.Views.Inquiry
{
    public partial class InquiryEditorWindow : Window
    {
        public InquiryEditorWindow(IInquiryWorkflowService workflow,
            InquiryAttachmentService attachments, InquiryRecord? record = null)
        {
            InitializeComponent();
            var vm = new InquiryEditorViewModel(workflow, attachments, record);
            vm.CloseRequested += saved => { DialogResult = saved; };
            vm.SelectFilesRequested = SelectFiles;
            vm.ConfirmDeleteRequested = file => MessageBox.Show(
                $"Delete attachment '{file.FileName}'?", "Delete attachment",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
            vm.PreviewRequested = Preview;
            DataContext = vm;
        }

        private static string[]? SelectFiles()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Add Inquiry attachments", Multiselect = true,
                Filter = "Supported files|*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.dwg;*.dxf;*.xlsx;*.xls;*.csv|PDF|*.pdf|Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|All files|*.*"
            };
            return dialog.ShowDialog() == true ? dialog.FileNames : null;
        }

        private void Preview(InquiryAttachment attachment)
        {
            var service = App.ServiceProvider.GetService(typeof(InquiryAttachmentService)) as InquiryAttachmentService;
            if (service == null) return;
            new InquiryAttachmentPreviewWindow(attachment, service) { Owner = this }.ShowDialog();
        }
    }
}
