// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/GlobalSearch/GlobalSearchBar.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared control — top-bar global search (PHASE E)
//
// Pure MVVM: the DataContext (GlobalSearchViewModel) owns the query, the
// results and the navigation callback. This file only runs
// InitializeComponent() — there are no event handlers.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Controls.GlobalSearch
{
    /// <summary>
    /// Compact search pill with a module filter and a results popup.
    /// Host it anywhere and set DataContext to a GlobalSearchViewModel.
    /// </summary>
    public partial class GlobalSearchBar : UserControl
    {
        public GlobalSearchBar()
        {
            InitializeComponent();
        }
    }
}
