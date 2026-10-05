using ProGlassAutomation.Models.Production;

namespace ProGlassAutomation.ViewModels.Production
{
    /// <summary>
    /// Polish stage queue — work items distributed from released Job Orders
    /// (confirmed PIs — drafts never reach the floor). See
    /// docs/PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md.
    /// </summary>
    public class PolishViewModel : ProductionStageViewModelBase
    {
        public override ProductionStage Stage => ProductionStage.Polish;
        public override string ModuleName => "Polish";
        public override string Description => "Edge polishing and grinding per pane, measured in LM. Polished edges protect handling and seal adhesion — every piece must finish here before Tempering.";
        public override string IndustryFocus => "Edge polish & grind — measured in LM per pane · sub-type tracked (all-sided, matched-lam, edge grind).";
        public override string Icon => "\uE895"; // Segoe MDL2 (matches AboutView production stages)
    }
}
