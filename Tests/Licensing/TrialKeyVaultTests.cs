using ProGlassAutomation.Services.Licensing;
using System;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    public sealed class TrialKeyVaultTests
    {
        [Fact]
        public void GetPrivateKeyXml_returns_placeholder_after_secure_cutover()
        {
            string xml = TrialKeyVault.GetPrivateKeyXml();

            Assert.False(string.IsNullOrEmpty(xml));
            Assert.Contains("PLACEHOLDER_TRIAL_PRIVATE_KEY_REMOVED", xml, StringComparison.Ordinal);
            Assert.DoesNotContain("<RSAKeyValue>", xml, StringComparison.Ordinal);
        }

        [Fact]
        public void GetPrivateKeyXml_is_cached_and_stable_across_calls()
        {
            string a = TrialKeyVault.GetPrivateKeyXml();
            string b = TrialKeyVault.GetPrivateKeyXml();

            Assert.Same(a, b);
            Assert.Equal("PLACEHOLDER_TRIAL_PRIVATE_KEY_REMOVED", a);
        }

        [Fact]
        public void ResetCacheForTests_keeps_same_placeholder_content()
        {
            string first = TrialKeyVault.GetPrivateKeyXml();
            TrialKeyVault.ResetCacheForTests();
            string second = TrialKeyVault.GetPrivateKeyXml();

            Assert.Equal(first, second);
        }
    }
}
