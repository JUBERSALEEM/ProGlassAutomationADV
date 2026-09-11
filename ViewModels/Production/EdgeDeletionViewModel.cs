using ProGlassAutomation.Models.Production;

namespace ProGlassAutomation.ViewModels.Production
{
    /// <summary>
    /// Edge Deletion stage queue — work items distributed from released Job Orders
    /// (confirmed PIs — drafts never reach the floor). See
    /// docs/PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md.
    /// </summary>
    public class EdgeDeletionViewModel : ProductionStageViewModelBase
    {
        public override ProductionStage Stage => ProductionStage.EdgeDeletion;
        public override string ModuleName => "Edge Deletion";
        public override string Description => "Manage edge deletion process for coated glass sheets.";
        public override string Icon => "\uE774"; // Segoe MDL2 (matches AboutView production stages)
    }
}
