using ProGlassAutomation.ViewModels.SheetStore.SheetStoreHistory;
using System.Windows;

namespace ProGlassAutomation.Views.SheetStore.SheetStoreHistory
{
    public partial class HistoryView : Window
    {
        public HistoryView() : this(new HistoryViewModel()) { }

        public HistoryView(HistoryViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseAction = Close;
        }
    }
}