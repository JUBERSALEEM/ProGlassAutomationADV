using ProGlassAutomation.ViewModels.SheetStore.SheetStoreUse;
using System.Windows;

namespace ProGlassAutomation.Views.SheetStore.SheetStoreUse
{
    public partial class UseDialogView : ProGlassAutomation.Views.Shared.Dialogs.ThemedDialogWindow
    {
        public UseDialogView(UseDialogViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseAction = Close;
        }
    }
}