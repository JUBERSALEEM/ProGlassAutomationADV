using ProGlassAutomation.Models.Production;

namespace ProGlassAutomation.ViewModels.Production
{
    /// <summary>
    /// Polish stage queue — work items distributed from released Job Orders
    /// and Approved Proforma Invoices. See
    /// docs/PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md.
    /// </summary>
    public class PolishViewModel : ProductionStageViewModelBase
    {
        public override ProductionStage Stage => ProductionStage.Polish;
        public override string ModuleName => "Polish";
        public override string Description => "Track polishing operations for edges and surfaces.";
        public override string Icon => "\uE895"; // Segoe MDL2 (matches AboutView production stages)
    }
}
