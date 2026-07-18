using ProGlassAutomation.ViewModels.SheetStore.SheetStoreUse;
using System.Windows;

namespace ProGlassAutomation.Views.SheetStore.SheetStoreUse
{
    public partial class UseDialogView : Window
    {
        public UseDialogView(UseDialogViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseAction = Close;
        }
    }
}