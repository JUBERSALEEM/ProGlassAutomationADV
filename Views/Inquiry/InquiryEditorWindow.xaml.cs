using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.ViewModels.Inquiry;
using System.Windows;

namespace ProGlassAutomation.Views.Inquiry
{
    public partial class InquiryEditorWindow : Window
    {
        public InquiryEditorWindow(IInquiryWorkflowService workflow, InquiryRecord? record = null)
        {
            InitializeComponent();
            var vm = new InquiryEditorViewModel(workflow, record);
            vm.CloseRequested += saved => { DialogResult = saved; };
            DataContext = vm;
        }
    }
}
