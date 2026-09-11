using ProGlassAutomation.Models.Production;

namespace ProGlassAutomation.ViewModels.Production
{
    /// <summary>
    /// Holes & Cutouts stage queue — work items distributed from released Job Orders
    /// (confirmed PIs — drafts never reach the floor). See
    /// docs/PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md.
    /// </summary>
    public class HolesCutoutsViewModel : ProductionStageViewModelBase
    {
        public override ProductionStage Stage => ProductionStage.HolesCutouts;
        public override string ModuleName => "Holes & Cutouts";
        public override string Description => "Manage drilling and cutout operations for hardware fittings and openings.";
        public override string Icon => "\uE71D"; // Segoe MDL2 (matches AboutView production stages)
    }
}
