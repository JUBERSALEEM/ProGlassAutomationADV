using ProGlassAutomation.Models.Production;

namespace ProGlassAutomation.ViewModels.Production
{
    /// <summary>
    /// Doubling / Lamination stage queue — work items distributed from released Job Orders
    /// (confirmed PIs — drafts never reach the floor). See
    /// docs/PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md.
    /// </summary>
    public class DoublingLaminationViewModel : ProductionStageViewModelBase
    {
        public override ProductionStage Stage => ProductionStage.DoublingLamination;
        public override string ModuleName => "Doubling / Lamination";
        public override string Description => "Final assembly: DGU units (spacer, seal, gas fill) and laminated lites (interlayer + autoclave). Every unit finished here becomes dispatch-ready pieces feeding the delivery notes.";
        public override string IndustryFocus => "Double-seal & laminate — DGU spacer/seal/gas · PVB/SGP interlayer · finished units → dispatch-ready.";
        public override string Icon => "\uE81C"; // Segoe MDL2 (matches AboutView production stages)
    }
}
