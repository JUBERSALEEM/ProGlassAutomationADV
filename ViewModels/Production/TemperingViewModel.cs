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
        public override string Description => "Furnace station for FT / Heat-Strengthened / Heat-Soaked panes. A piece enters only when cutting and all its edge work are done; SGU pieces leave the factory dispatch-ready straight from here.";
        public override string IndustryFocus => "Furnace cycles — FT / HS / HST panes only · IN = cut + all edge work · SGU dispatch-ready off the furnace.";
        public override string Icon => "\uE728"; // Segoe MDL2 (matches AboutView production stages)
    }
}
