using ProGlassAutomation.Services.ProformaInvoice;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public sealed class ProformaDuplicateSnapshotScrubTests
    {
        [Fact]
        public void Detach_clears_inquiry_link_and_rewrites_proforma_id()
        {
            const string json = "{\"Company\":{\"InquiryId\":7,\"ProformaId\":\"PI-1\",\"CompanyName\":\"Al Noor\"}}";

            string? result = ProformaDuplicateSnapshotScrub.TryDetach(json, "PI-2");

            Assert.NotNull(result);
            Assert.DoesNotContain("7", result);
            Assert.Contains("PI-2", result);
            Assert.Contains("Al Noor", result);
        }

        [Fact]
        public void Detach_returns_null_when_the_company_block_is_missing()
        {
            Assert.Null(ProformaDuplicateSnapshotScrub.TryDetach("{\"Project\":{\"Name\":\"Tower\"}}", "PI-2"));
        }

        [Fact]
        public void Detach_returns_null_for_invalid_json_instead_of_the_original()
        {
            const string broken = "{\"Company\":";

            Assert.Null(ProformaDuplicateSnapshotScrub.TryDetach(broken, "PI-2"));
        }
    }
}
