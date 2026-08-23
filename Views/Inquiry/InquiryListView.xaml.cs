using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.ViewModels.Inquiry;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Inquiry
{
    public partial class InquiryListView : UserControl
    {
        public InquiryListView()
        {
            InitializeComponent();
            DataContextChanged += (_, __) => WireEditor();
            Loaded += (_, __) => WireEditor();
        }

        private void WireEditor()
        {
            if (DataContext is not InquiryListViewModel vm || vm.EditRequested != null) return;
            vm.EditRequested = record =>
            {
                var workflow = App.ServiceProvider.GetRequiredService<IInquiryWorkflowService>();
                var window = new InquiryEditorWindow(workflow, record) { Owner = Application.Current.MainWindow };
                return Task.FromResult(window.ShowDialog() == true);
            };
            vm.ExportPathRequested = () =>
            {
                var d = new SaveFileDialog { Filter = "CSV file|*.csv", FileName = $"InquiryRegister_{System.DateTime.Now:yyyyMMdd}.csv" };
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
        }
    }
}
