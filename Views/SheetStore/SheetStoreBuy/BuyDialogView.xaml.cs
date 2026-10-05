using ProGlassAutomation.ViewModels.SheetStore.SheetStoreBuy;
using System.Windows;

namespace ProGlassAutomation.Views.SheetStore.SheetStoreBuy
{
    public partial class BuyDialogView : ProGlassAutomation.Views.Shared.Dialogs.ThemedDialogWindow
    {
        public BuyDialogView(BuyDialogViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseAction = Close;
        }
    }
}