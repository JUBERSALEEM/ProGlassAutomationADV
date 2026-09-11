using ProGlassAutomation.Models.Production;

namespace ProGlassAutomation.ViewModels.Production
{
    /// <summary>
    /// Tempering stage queue — work items distributed from released Job Orders
    /// (confirmed PIs — drafts never reach the floor). See
    /// docs/PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md.
    /// </summary>
    public class TemperingViewModel : ProductionStageViewModelBase
    {
        public override ProductionStage Stage => ProductionStage.Tempering;
        public override string ModuleName => "Tempering";
        public override string Description => "Manage tempering furnace operations and batch tracking.";
        public override string Icon => "\uE728"; // Segoe MDL2 (matches AboutView production stages)
    }
}
