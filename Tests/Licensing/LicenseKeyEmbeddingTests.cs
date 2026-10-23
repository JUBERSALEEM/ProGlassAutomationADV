using ProGlassAutomation.Services.Licensing;
using System;
using System.IO;
using System.Security.Cryptography;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    [Collection("LicensePublicKey")]
    public sealed class LicenseKeyEmbeddingTests
    {
        [Fact]
        public void Main_public_key_is_not_the_placeholder()
        {
            Assert.False(LicensePublicKey.IsPlaceholder,
                "LicensePublicKey.PublicKeyXml is still the placeholder. " +
                "Embed the real production verification key in the app trust ring.");
        }

        [Fact]
        public void Main_public_key_is_valid_RSA_XML_that_dotnet_can_parse()
        {
            using var rsa = RSA.Create();
            var ex = Record.Exception(() => rsa.FromXmlString(LicensePublicKey.PublicKeyXml));
            Assert.Null(ex);
            Assert.Equal(2048, rsa.KeySize);
        }

        [Fact]
        public void Generator_compatible_public_key_is_valid_RSA_XML_that_dotnet_can_parse()
        {
            using var rsa = RSA.Create();
            var ex = Record.Exception(() => rsa.FromXmlString(LicensePublicKey.GeneratorCompatiblePublicKeyXml));
            Assert.Null(ex);
            Assert.Equal(2048, rsa.KeySize);
        }

        [Fact]
        public void Generator_compatible_public_key_matches_the_generator_embedded_key()
        {
            // The public compatibility value must match the app trust
            // ring while a replacement key and customer migration are
            // planned. This is not authorization to keep signing with the
            // exposed private key; the generator is fail-closed.
            string root = LocateProjectRoot();
            string src = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "ProductionSigningKey.cs"));

            var match = System.Text.RegularExpressions.Regex.Match(
                src, "<RSAKeyValue>.*?</RSAKeyValue>",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            Assert.True(match.Success, "ProductionSigningKey must embed a public RSAKeyValue literal.");
            Assert.Equal(LicensePublicKey.GeneratorCompatiblePublicKeyXml, match.Value);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Tools", "LicenseGenerator", "MainWindow.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }

        [Fact]
        public void Trial_key_pair_is_placeholder_after_secure_offline_cutover()
        {
            Assert.True(LicensePublicKey.IsTrialKeyPlaceholder,
                "The secure offline redesign should not ship a real embedded client-side trial signing key.");
            Assert.Contains("PLACEHOLDER_TRIAL_", LicensePublicKey.TrialPublicKeyXml, StringComparison.Ordinal);
            Assert.Contains("PLACEHOLDER_TRIAL_", LicensePublicKey.TrialPrivateKeyXml, StringComparison.Ordinal);
        }
    }
}
