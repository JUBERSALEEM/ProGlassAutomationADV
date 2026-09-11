using ProGlassAutomation.Models.Production;

namespace ProGlassAutomation.ViewModels.Production
{
    /// <summary>
    /// Cutting stage queue — work items distributed from released Job Orders
    /// (confirmed PIs — drafts never reach the floor). See
    /// docs/PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md.
    /// </summary>
    public class CuttingViewModel : ProductionStageViewModelBase
    {
        public override ProductionStage Stage => ProductionStage.Cutting;
        public override string ModuleName => "Cutting";
        public override string Description => "Track glass cutting operations, machine assignments, and cut piece output.";
        public override string Icon => "\uE8C6"; // Segoe MDL2 (matches AboutView production stages)
    }
}
