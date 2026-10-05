// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Production/ProductionStagePageBase.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — code-behind base for the eight stage pages
//
// The eight production modules are now SEPARATE, purpose-built pages
// (CuttingView, HolesCutoutsView, …) — each owns its own columns,
// detail sidebar and hero. What they share is only this page plumbing:
// the initial async load when the page appears, and the master-grid
// scroll feed for the 100-row incremental loader. No shared screen —
// shared *plumbing* only. See
// docs/PRODUCTION_OPTIMIZATION_REDESIGN_2026-09-12.md.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.Production;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Production
{
    /// <summary>
    /// Shared code-behind plumbing for the eight stage pages. The views
    /// themselves are fully separate; this base only triggers the initial
    /// async load and forwards master-grid scroll geometry to the VM so
    /// very long jobs keep loading in pages of 100. The stage pages use
    /// this class as their XAML ROOT element (not &lt;UserControl&gt;) so the
    /// generated partial and the code-behind partial share one base.
    /// </summary>
    public class ProductionStagePageBase : UserControl
    {
        public ProductionStagePageBase()
        {
            Loaded += OnViewLoaded;
        }

        protected void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ProductionStageViewModelBase vm)
            {
                Utilities.AsyncUtility.SafeFireAndForget(
                    vm.LoadAsync, "Production stage page load");
            }
        }

        /// <summary>
        /// Bubbled from the stage page's own master grid — forwards scroll
        /// geometry so the VM renders the next 100 rows when the floor
        /// scrolls near the bottom.
        /// </summary>
        protected void MasterGrid_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (DataContext is ProductionStageViewModelBase vm)
                vm.OnMasterListScrolled(e.VerticalOffset, e.ViewportHeight, e.ExtentHeight);
        }
    }
}
