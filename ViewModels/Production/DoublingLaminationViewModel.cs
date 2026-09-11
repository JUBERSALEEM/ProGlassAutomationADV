using ProGlassAutomation.Models.Production;

namespace ProGlassAutomation.ViewModels.Production
{
    /// <summary>
    /// Doubling / Lamination stage queue — work items distributed from released Job Orders
    /// and Approved Proforma Invoices. See
    /// docs/PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md.
    /// </summary>
    public class DoublingLaminationViewModel : ProductionStageViewModelBase
    {
        public override ProductionStage Stage => ProductionStage.DoublingLamination;
        public override string ModuleName => "Doubling / Lamination";
        public override string Description => "Manage double glazing and lamination processes for finished glass units.";
        public override string Icon => "\uE81C"; // Segoe MDL2 (matches AboutView production stages)
    }
}
