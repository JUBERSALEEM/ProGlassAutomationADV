using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
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
    }
}
