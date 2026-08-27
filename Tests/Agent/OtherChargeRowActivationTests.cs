using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class OtherChargeRowActivationTests
    {
        [Fact]
        public void Suggested_activation_turns_a_fresh_row_on()
        {
            var row = new OtherChargeRow();

            row.ApplySuggestedIsActive(true);

            Assert.True(row.IsActive);
            Assert.False(row.HasManualActiveOverride);
        }

        [Fact]
        public void Manual_override_blocks_future_suggested_activation_changes()
        {
            var row = new OtherChargeRow();

            row.ApplySuggestedIsActive(true);
            row.IsActive = false;
            row.ApplySuggestedIsActive(true);

            Assert.False(row.IsActive);
            Assert.True(row.HasManualActiveOverride);
        }

        [Fact]
        public void Restored_active_state_is_treated_as_explicit_operator_choice()
        {
            var row = new OtherChargeRow();

            row.RestoreIsActive(false);
            row.ApplySuggestedIsActive(true);

            Assert.False(row.IsActive);
            Assert.True(row.HasManualActiveOverride);
        }

        [Fact]
        public void Advanced_mode_auto_picks_u_insert_by_default_when_the_spec_contains_it()
        {
            var grid = new DataGridViewModel();
            var group = grid.SpecGroups[0];
            group.SelectedGlassType = "DGU";
            group.SelectedSpecName = "6mm HD Grey FT Glass + 12mm ASP With U-Insert + 6mm Clear FT Glass";

            var vm = new OtherChargesViewModel(grid);
            var row = vm.ChargeRows.First(r => r.TemplateKey == "U_INSERT");

            Assert.True(row.IsActive);
            Assert.NotNull(row.TargetSelection?.SelectedMetric);
            Assert.Equal("DGU_CAVITY", row.TargetSelection!.SelectedMetric!.Key);
            Assert.Contains(row.TargetSelection.AvailableSpecs,
                s => s.SpecGroupNumber == group.GroupNumber && s.IsChecked);
        }

        [Fact]
        public void Advanced_mode_is_enabled_by_default_on_a_fresh_other_charges_viewmodel()
        {
            var vm = new OtherChargesViewModel(new DataGridViewModel());

            Assert.True(vm.UseAdvancedAutomation);
            Assert.False(vm.IsManualMode);
        }
    }
}
