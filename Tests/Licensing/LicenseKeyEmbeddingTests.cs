// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Licensing/LicenseKeyEmbeddingTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// SCOPE
// ─────
// Regression pin: the embedded RSA-2048 key pairs in
// LicensePublicKey.cs must NOT be the placeholder text. When they
// were, the app booted into the license activation screen on every
// launch because:
//
//   • LicenseVerifier.Verify short-circuited to `NotActivated`
//     on the IsPlaceholder check for the MAIN key.
//   • LicenseService.IssueAutoTrialIfEligible short-circuited on
//     the IsTrialKeyPlaceholder check and never wrote a .lic.
//   • LicenseStatusViewModel.IsLicenseRequired then returned
//     true → the shell overlaid the license activation view.
//
// Symptom: "Activate ProGlass Automation" screen after first-run
// setup even though the 30-day trial was supposed to be automatic.
//
// This test fails loudly if a future refactor accidentally re-
// introduces the placeholder — the message includes the fix
// instructions so the next developer to hit this doesn't have to
// re-discover the root cause.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Licensing;
using System;
using System.Security.Cryptography;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    /// <summary>
    /// Guarantees the shipped binary carries real RSA-2048 keys, not
    /// the placeholder strings. Serialised via a xUnit collection
    /// because the tests below observe the static
    /// <see cref="LicensePublicKey"/> and would race with any other
    /// suite that swaps the key via <c>SetForTest</c>.
    /// </summary>
    [Collection("LicensePublicKey")]
    public sealed class LicenseKeyEmbeddingTests
    {
        [Fact]
        public void Main_public_key_is_not_the_placeholder()
        {
            Assert.False(LicensePublicKey.IsPlaceholder,
                "LicensePublicKey.PublicKeyXml is still the placeholder — " +
                "every launch will fall into the license activation screen " +
                "because LicenseVerifier short-circuits on this check. Fix: " +
                "generate an RSA-2048 key pair, paste the public XML into " +
                "Services/Licensing/LicensePublicKey.cs (PublicKeyXml), " +
                "and hold the private half on the vendor build machine.");
        }

        [Fact]
        public void Trial_key_pair_is_not_the_placeholder()
        {
            Assert.False(LicensePublicKey.IsTrialKeyPlaceholder,
                "LicensePublicKey.TrialPublicKeyXml / TrialPrivateKeyXml " +
                "is still the placeholder — the app cannot self-sign the " +
                "30-day auto-trial on first launch, so the license activation " +
                "screen shows after user setup. Fix: embed a real RSA-2048 " +
                "trial key pair in Services/Licensing/LicensePublicKey.cs " +
                "(both TrialPublicKeyXml and TrialPrivateKeyXml).");
        }

        [Fact]
        public void Main_public_key_is_valid_RSA_XML_that_dotnet_can_parse()
        {
            // If the placeholder was replaced with a hand-typed string
            // that isn't valid RSA XML, RSA.FromXmlString throws at
            // runtime the first time a license is verified. This test
            // catches that at build/test time instead.
            using var rsa = RSA.Create();
            var ex = Record.Exception(
                () => rsa.FromXmlString(LicensePublicKey.PublicKeyXml));
            Assert.Null(ex);
            Assert.Equal(2048, rsa.KeySize);
        }

        [Fact]
        public void Trial_public_key_is_valid_RSA_XML_that_dotnet_can_parse()
        {
            using var rsa = RSA.Create();
            var ex = Record.Exception(
                () => rsa.FromXmlString(LicensePublicKey.TrialPublicKeyXml));
            Assert.Null(ex);
            Assert.Equal(2048, rsa.KeySize);
        }

        [Fact]
        public void Trial_private_key_is_valid_RSA_XML_that_dotnet_can_parse()
        {
            using var rsa = RSA.Create();
            var ex = Record.Exception(
                () => rsa.FromXmlString(LicensePublicKey.TrialPrivateKeyXml));
            Assert.Null(ex);
            Assert.Equal(2048, rsa.KeySize);

            // Sanity check: the private key XML MUST include the D
            // element — a public-only XML pasted into the private
            // property by mistake would silently fall through here
            // and only surface as a signature failure at runtime.
            Assert.Contains("<D>", LicensePublicKey.TrialPrivateKeyXml, StringComparison.Ordinal);
        }

        [Fact]
        public void Trial_key_pair_public_and_private_halves_match()
        {
            // The two embedded XML strings must belong to the SAME
            // key pair — if a copy-paste mistake made them different,
            // every self-signed trial would fail signature verification
            // and drop into NotActivated on every launch.
            //
            // We test this by signing a well-known payload with the
            // private key and verifying with the public key.
            using var priv = RSA.Create();
            priv.FromXmlString(LicensePublicKey.TrialPrivateKeyXml);

            using var pub = RSA.Create();
            pub.FromXmlString(LicensePublicKey.TrialPublicKeyXml);

            byte[] message = System.Text.Encoding.UTF8.GetBytes(
                "trial-key-pair-round-trip");

            byte[] signature = priv.SignData(message,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

            bool verified = pub.VerifyData(message, signature,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

            Assert.True(verified,
                "TrialPublicKeyXml and TrialPrivateKeyXml do NOT belong " +
                "to the same key pair. Regenerate both halves from a " +
                "single RSA.Create() call and paste them together.");
        }
    }
}
