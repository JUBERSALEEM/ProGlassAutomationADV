using ProGlassAutomation.ViewModels.SheetStore.SheetStoreCsv;
using System.Windows;

namespace ProGlassAutomation.Views.SheetStore.SheetStoreCsv
{
    public partial class DuplicateResolutionView : ProGlassAutomation.Views.Shared.Dialogs.ThemedDialogWindow
    {
        public DuplicateResolutionView(DuplicateResolutionViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseAction = Close;
        }
    }
}