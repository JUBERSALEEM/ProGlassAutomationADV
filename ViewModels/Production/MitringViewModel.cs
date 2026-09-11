using ProGlassAutomation.Models.Production;

namespace ProGlassAutomation.ViewModels.Production
{
    /// <summary>
    /// Mitring stage queue — work items distributed from released Job Orders
    /// (confirmed PIs — drafts never reach the floor). See
    /// docs/PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md.
    /// </summary>
    public class MitringViewModel : ProductionStageViewModelBase
    {
        public override ProductionStage Stage => ProductionStage.Mitring;
        public override string ModuleName => "Mitring";
        public override string Description => "Track mitring operations for angled edge finishing.";
        public override string Icon => "\uE7E8"; // Segoe MDL2 (matches AboutView production stages)
    }
}
