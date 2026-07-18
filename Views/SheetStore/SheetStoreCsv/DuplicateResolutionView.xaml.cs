using ProGlassAutomation.ViewModels.SheetStore.SheetStoreCsv;
using System.Windows;

namespace ProGlassAutomation.Views.SheetStore.SheetStoreCsv
{
    public partial class DuplicateResolutionView : Window
    {
        public DuplicateResolutionView(DuplicateResolutionViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseAction = Close;
        }
    }
}