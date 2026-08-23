using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using Xunit;

namespace ProGlassAutomation.Tests.Inquiry
{
    public class InquiryWorkflowTests
    {
        [Theory]
        [InlineData("received", "Received")]
        [InlineData("ON HOLD", "Hold")]
        [InlineData("confirmed", "Confirmed")]
        public void Status_normalization_is_backward_safe(string input, string expected) =>
            Assert.Equal(expected, InquiryStatuses.Normalize(input));

        [Theory]
        [InlineData("Received", "Prepared")]
        [InlineData("Prepared", "Sent")]
        [InlineData("Sent", "Confirmed")]
        [InlineData("Hold", "Prepared")]
        public void Valid_transitions_are_allowed(string from, string to) =>
            Assert.True(InquiryWorkflowService.CanTransition(from, to));

        [Theory]
        [InlineData("Received", "Confirmed")]
        [InlineData("Confirmed", "Sent")]
        [InlineData("Cancelled", "Prepared")]
        public void Unsafe_transitions_are_blocked(string from, string to) =>
            Assert.False(InquiryWorkflowService.CanTransition(from, to));
    }
}
