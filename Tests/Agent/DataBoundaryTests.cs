// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/DataBoundaryTests.cs
// PROJECT: ProGlassAutomation.Tests — PHASE T
//
// The data boundary is the component that decides whether ERP data may
// leave the machine. The master prompt requires it to be FAIL-CLOSED, so
// these tests are written to catch the failure mode that matters:
// something being ALLOWED that should not be.
//
// Note the asymmetry on purpose — a false "blocked" is an inconvenience,
// a false "allowed" is a data leak.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Security.DataBoundary;
using System.Threading.Tasks;
using ProGlassAutomation.Agent.Security.Integrity;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class DataBoundaryTests
    {
        // ══════════════════════════════════════════════════════════════
        //  NOTHING SENSITIVE LEAVES, EVEN WITH CONSENT
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Secrets_never_leave_even_with_consent()
        {
            var verdict = AgentDataBoundary.MayLeaveDevice(
                DataClass.Secret, consentGiven: true);

            Assert.False(verdict.Allowed);
        }

        [Fact]
        public void Business_data_never_leaves_even_with_consent()
        {
            // Consenting to a cloud MODEL is not consenting to upload the
            // customer list. Conflating those two is how ERP data leaks.
            var verdict = AgentDataBoundary.MayLeaveDevice(
                DataClass.BusinessData, consentGiven: true);

            Assert.False(verdict.Allowed);
        }

        [Fact]
        public void Unclassified_is_treated_as_sensitive()
        {
            var verdict = AgentDataBoundary.MayLeaveDevice(
                DataClass.Unclassified, consentGiven: true);

            Assert.False(verdict.Allowed);
        }

        [Fact]
        public void Public_knowledge_needs_consent()
        {
            Assert.False(AgentDataBoundary
                .MayLeaveDevice(DataClass.PublicKnowledge, consentGiven: false).Allowed);

            Assert.True(AgentDataBoundary
                .MayLeaveDevice(DataClass.PublicKnowledge, consentGiven: true).Allowed);
        }

        [Fact]
        public void Every_denial_carries_a_reason()
        {
            // A silent block is indistinguishable from a bug. The user must
            // always be told why.
            foreach (var cls in new[]
                     {
                         DataClass.Secret, DataClass.BusinessData,
                         DataClass.Unclassified
                     })
            {
                var v = AgentDataBoundary.MayLeaveDevice(cls, consentGiven: true);
                Assert.False(string.IsNullOrWhiteSpace(v.Reason));
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  CLASSIFIER ERRS UPWARD
        // ══════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("AIzaSyA1234567890abcdefghijklmnopqrstu")]
        [InlineData("password=hunter2")]
        public void Credentials_classify_as_secret(string text) =>
            Assert.Equal(DataClass.Secret, AgentDataBoundary.Classify(text));

        [Theory]
        [InlineData("profit on PI-1023")]
        [InlineData("customer Al Noor Glass")]
        [InlineData("AED 4,500 selling price")]
        public void Business_markers_classify_as_business_data(string text) =>
            Assert.Equal(DataClass.BusinessData, AgentDataBoundary.Classify(text));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Empty_input_is_unclassified_not_public(string? text) =>
            Assert.Equal(DataClass.Unclassified, AgentDataBoundary.Classify(text));

        [Fact]
        public void Generic_product_questions_are_public_knowledge()
        {
            // Over-classifying everything would make the cloud provider
            // useless, so prove a genuinely general question passes.
            Assert.Equal(
                DataClass.PublicKnowledge,
                AgentDataBoundary.Classify("what thickness options exist for tempered glass"));
        }

        // ══════════════════════════════════════════════════════════════
        //  INTEGRITY
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public async Task Verifying_without_an_expected_hash_fails_closed()
        {
            var result = await AgentIntegrity.VerifyFileAsync("anything.bin", "");

            Assert.False(result.Verified);
        }

        [Fact]
        public async Task Missing_file_is_reported_not_thrown()
        {
            var result = await AgentIntegrity.HashFileAsync(
                "Z:\\definitely\\not\\here.bin");

            Assert.False(result.Verified);
            Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        }

        [Theory]
        [InlineData("{\"a\":1}", true)]
        [InlineData("[{\"a\":1}]", true)]
        [InlineData("{\"a\":1", false)]          // truncated mid-write
        [InlineData("{\"a\":\"}\"}", true)]       // brace inside a string
        [InlineData("", false)]
        [InlineData("not json", false)]
        public void Truncated_json_is_detected(string json, bool expected) =>
            Assert.Equal(
                expected,
                AgentIntegrity.LooksStructurallyCompleteJson(json, maxLength: 10000));

        [Fact]
        public void Oversized_json_is_rejected()
        {
            string big = "{" + new string('x', 500) + "}";

            Assert.False(
                AgentIntegrity.LooksStructurallyCompleteJson(big, maxLength: 100));
        }
    }
}
