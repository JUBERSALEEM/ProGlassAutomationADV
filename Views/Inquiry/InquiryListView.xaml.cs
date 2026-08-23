using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.ViewModels.Inquiry;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Inquiry
{
    /// <summary>
    /// Code-behind for the Inquiry register ("Lumen" layout). UI plumbing only:
    /// - wires the editor/export/import/preview dialogs onto the ViewModel;
    /// - double-click opens the selected inquiry in the workspace.
    /// All behaviour lives in InquiryListViewModel.
    /// </summary>
    public partial class InquiryListView : UserControl
    {
        public InquiryListView()
        {
            InitializeComponent();
            DataContextChanged += (_, __) => WireEditor();
            Loaded += (_, __) => WireEditor();
        }

        /// <summary>Double-click a row card → open it in the workspace.</summary>
        private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is InquiryListViewModel vm
                && vm.SelectedInquiry != null
                && vm.EditInquiryCommand.CanExecute(vm.SelectedInquiry))
            {
                vm.EditInquiryCommand.Execute(vm.SelectedInquiry);
            }
        }

        private void WireEditor()
        {
            if (DataContext is not InquiryListViewModel vm || vm.EditRequested != null) return;
            vm.EditRequested = async record =>
            {
                var workflow = App.ServiceProvider.GetRequiredService<IInquiryWorkflowService>();
                var attachments = App.ServiceProvider.GetRequiredService<InquiryAttachmentService>();
                var window = new InquiryEditorWindow(workflow, attachments, record)
                { Owner = Application.Current.MainWindow };
                return window.ShowDialog() == true;
            };
            vm.ExportPathRequested = () =>
            {
                var d = new SaveFileDialog
                {
                    Filter = "CSV file|*.csv",
                    FileName = $"InquiryRegister_{DateTime.Now:yyyyMMdd}.csv"
                };
                return d.ShowDialog() == true ? d.FileName : null;
            };
            vm.ImportPathRequested = () =>
            {
                var d = new OpenFileDialog { Filter = "CSV file|*.csv" };
                return d.ShowDialog() == true ? d.FileName : null;
            };
            vm.ConfirmImportRequested = count => MessageBox.Show(
                $"Create {count} validated inquiry records?", "Import inquiries",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
            vm.ConfirmDeleteAttachmentRequested = file => MessageBox.Show(
                $"Delete attachment '{file.FileName}'? The file is removed from disk as well.",
                "Delete attachment",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
            vm.PreviewAttachmentRequested = file =>
            {
                var service = App.ServiceProvider.GetService(typeof(InquiryAttachmentService))
                    as InquiryAttachmentService
                    ?? new InquiryAttachmentService(App.Inquiries ?? new InquiryRepository());
                var preview = new InquiryAttachmentPreviewWindow(file, service);
                if (Application.Current?.MainWindow is Window owner) preview.Owner = owner;
                preview.Show();
            };
        }
    }
}
