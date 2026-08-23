using Microsoft.Extensions.DependencyInjection;
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
                var window = new InquiryEditorWindow(workflow, record)
                {
                    Owner = Application.Current.MainWindow
                };
                return Task.FromResult(window.ShowDialog() == true);
            };
        }
    }
}
