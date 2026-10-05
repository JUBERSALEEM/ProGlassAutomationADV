using ProGlassAutomation.ViewModels.Shared;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Shared
{
    public partial class ScreenRecordPanel : UserControl
    {
        public ScreenRecordPanel()
        {
            InitializeComponent();
            DataContext = ScreenRecordPanelViewModel.Shared;
        }
    }
}
