using ProGlassAutomation.ViewModels.SheetStore.SheetStoreBuy;
using System.Windows;

namespace ProGlassAutomation.Views.SheetStore.SheetStoreBuy
{
    public partial class BuyDialogView : Window
    {
        public BuyDialogView(BuyDialogViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseAction = Close;
        }
    }
}