using System.Windows.Controls;

namespace ProGlassAutomation.Views.Shared
{
    /// <summary>
    /// Sidebar storage card (clickable — opens the storage breakdown popup).
    /// Pure bindings — the command lives on MainViewModel.
    /// </summary>
    public partial class StorageSidebarCard : UserControl
    {
        public StorageSidebarCard()
        {
            InitializeComponent();
        }
    }
}
