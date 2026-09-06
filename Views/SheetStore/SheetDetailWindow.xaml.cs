using ProGlassAutomation.ViewModels.SheetStore;
using System.Windows;

namespace ProGlassAutomation.Views.SheetStore
{
    /// <summary>
    /// Full-screen inventory detail workspace, opened by double-clicking a
    /// row. The explicit constructor is required: a partial Window with no
    /// hand-written ctor compiles cleanly but never calls
    /// InitializeComponent(), so the visual tree never loads.
    /// </summary>
    public partial class SheetDetailWindow : Window
    {
        public SheetDetailWindow(SheetDetailWindowViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseAction = Close;
        }
    }
}
