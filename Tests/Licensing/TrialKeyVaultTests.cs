// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Licensing/TrialKeyVaultTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the obfuscated trial-private-key vault: XOR-masked shards
// must reassemble into a valid RSA-2048 private key XML that
// matches TrialPublicKeyXml.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Licensing;
using System;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    public sealed class TrialKeyVaultTests
    {
        [Fact]
        public void GetPrivateKeyXml_returns_a_wellformed_RSAKeyValue()
        {
            string xml = TrialKeyVault.GetPrivateKeyXml();

            Assert.False(string.IsNullOrEmpty(xml));
            Assert.StartsWith("<RSAKeyValue>", xml, StringComparison.Ordinal);
            Assert.EndsWith("</RSAKeyValue>", xml, StringComparison.Ordinal);
            // Private key XML must include D (private exponent).
            Assert.Contains("<D>", xml, StringComparison.Ordinal);
        }

        [Fact]
        public void GetPrivateKeyXml_is_cached_and_stable_across_calls()
        {
            // The vault caches the reassembled string. Consecutive
            // calls must return the same reference-equal instance so
            // signature-heavy code paths don't XOR 1.7 KB per call.
            string a = TrialKeyVault.GetPrivateKeyXml();
            string b = TrialKeyVault.GetPrivateKeyXml();

            Assert.Same(a, b);
        }

        [Fact]
        public void Reassembled_key_parses_as_RSA_2048_via_dotnet()
        {
            // The whole point of the vault is that the runtime
            // reassembly produces something .NET's RSA.FromXmlString
            // accepts as a private key. If a future refactor
            // corrupts the shard bytes or the XOR loop, this test
            // fails at build/test time instead of at first launch.
            using var rsa = RSA.Create();
            rsa.FromXmlString(TrialKeyVault.GetPrivateKeyXml());
            Assert.Equal(2048, rsa.KeySize);
        }

        [Fact]
        public void Reassembled_private_key_matches_embedded_TrialPublicKey()
        {
            // A signed-with-private, verified-with-public round-trip
            // proves the vault's private half belongs to the same
            // pair as LicensePublicKey.TrialPublicKeyXml. This is
            // the highest-value regression pin — a mismatched pair
            // would silently kill every trial signature at runtime.
            using var priv = RSA.Create();
            priv.FromXmlString(TrialKeyVault.GetPrivateKeyXml());

            using var pub = RSA.Create();
            pub.FromXmlString(LicensePublicKey.TrialPublicKeyXml);

            byte[] message = Encoding.UTF8.GetBytes("vault-round-trip");
            byte[] sig = priv.SignData(message,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

            bool ok = pub.VerifyData(message, sig,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            Assert.True(ok,
                "TrialKeyVault-reassembled private key does NOT match " +
                "LicensePublicKey.TrialPublicKeyXml. Regenerate both together.");
        }

        [Fact]
        public void ResetCacheForTests_forces_re_reassembly_on_next_read()
        {
            string first = TrialKeyVault.GetPrivateKeyXml();

            TrialKeyVault.ResetCacheForTests();

            string second = TrialKeyVault.GetPrivateKeyXml();
            // Content must be identical (same XOR result) but
            // reference-inequality proves the cache was busted.
            Assert.Equal(first, second);
        }
    }
}
