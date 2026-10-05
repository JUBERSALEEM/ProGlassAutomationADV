using ProGlassAutomation.ViewModels.SheetStore;
using System.Windows;

namespace ProGlassAutomation.Views.SheetStore
{
    public partial class AddEditSheetView : Window
    {
        public AddEditSheetView() : this(new AddEditSheetViewModel()) { }

        public AddEditSheetView(AddEditSheetViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseAction = Close;
        }
    }
}