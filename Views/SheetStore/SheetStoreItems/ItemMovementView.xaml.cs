using ProGlassAutomation.ViewModels.SheetStore.SheetStoreItems;
using System.Windows;

namespace ProGlassAutomation.Views.SheetStore.SheetStoreItems
{
    /// <summary>
    /// Receive / issue a consumable. The explicit constructor is required:
    /// a partial Window with no hand-written ctor compiles cleanly but
    /// never calls InitializeComponent(), so the visual tree never loads
    /// and every x:Name field stays null — silently.
    /// </summary>
    public partial class ItemMovementView : Window
    {
        public ItemMovementView(ItemMovementViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseAction = Close;
        }
    }
}
