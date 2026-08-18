using System.Windows.Controls;

namespace ProGlassAutomation.Controls.LoadingState
{
    /// <summary>
    /// Shared, theme-adaptive loading spinner (rotating dashed ring + label).
    /// Views can host this wherever they show an in-progress state, instead
    /// of each view declaring its own loading markup (Phase 9 - design
    /// consistency). Adoption is incremental.
    /// </summary>
    public partial class LoadingStateControl : UserControl
    {
        public LoadingStateControl()
        {
            InitializeComponent();
        }
    }
}
