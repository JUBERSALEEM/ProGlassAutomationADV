// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/LicenseGeneratorDesignSourceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Source-level invariants for the License Generator during
//         production-key rotation. These tests pin the fail-closed state:
//         no embedded private key, no DPAPI-cache fallback, and the old
//         public key retained only for a deliberate customer migration.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Text.RegularExpressions;
using ProGlassAutomation.Services.Licensing;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class LicenseGeneratorDesignSourceTests
    {
        [Fact]
        public void Generator_project_references_the_contract_library_instead_of_app_license_sources()
        {
            string root = LocateProjectRoot();
            string csproj = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "ProGlassAutomation.LicenseGenerator.csproj"));

            Assert.Contains("Licensing\\ProGlass.Licensing.csproj", csproj);
            Assert.DoesNotContain("Services\\Licensing\\LicensePayload.cs", csproj);
            Assert.DoesNotContain("Services\\Licensing\\LicenseSigner.cs", csproj);
        }

        [Fact]
        public void Generator_core_fails_closed_until_a_replacement_signer_is_approved()
        {
            string root = LocateProjectRoot();
            string core = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "LicenseGeneratorCore.cs"));
            string protection = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "KeyProtection.cs"));
            string signingKey = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "ProductionSigningKey.cs"));

            Assert.Contains("KeyProtection.GetPrivateKey()", core);
            Assert.Contains("throw new InvalidOperationException", protection);
            Assert.Contains("License issuance is disabled", protection);
            Assert.DoesNotContain("ProtectedData.Unprotect", protection);
            Assert.DoesNotContain("ProductionSigningKey.PrivateKeyXml", protection);
            Assert.DoesNotContain("PrivateKeyXml", signingKey, StringComparison.Ordinal);
            foreach (string privateRsaElement in new[]
                     { "<D>", "<P>", "<Q>", "<DP>", "<DQ>", "<InverseQ>" })
            {
                Assert.DoesNotContain(privateRsaElement, signingKey, StringComparison.Ordinal);
            }

            // The plan catalog remains shared with the customer app, but
            // catalog compatibility does not enable signing.
            string catalog = File.ReadAllText(Path.Combine(root,
                "Licensing", "Contracts", "LicensePlanCatalog.cs"));
            Assert.Contains("LicensePlanCatalog", core);
            Assert.Contains("\"7 Days\"", catalog);
            Assert.Contains("\"2 Years\"", catalog);
        }

        [Fact]
        public void Generator_public_key_matches_app_compatibility_key_during_rotation()
        {
            string root = LocateProjectRoot();
            string src = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "ProductionSigningKey.cs"));

            // Retain only the public half while existing licenses are
            // migrated deliberately; no private half may remain in source.
            var match = Regex.Match(src, "<RSAKeyValue>.*?</RSAKeyValue>", RegexOptions.Singleline);
            Assert.True(match.Success, "The compatibility file must retain its public RSAKeyValue.");
            Assert.Equal(LicensePublicKey.GeneratorCompatiblePublicKeyXml, match.Value);
            Assert.DoesNotContain("PrivateKeyXml", src, StringComparison.Ordinal);
            foreach (string privateRsaElement in new[]
                     { "<D>", "<P>", "<Q>", "<DP>", "<DQ>", "<InverseQ>" })
            {
                Assert.DoesNotContain(privateRsaElement, src, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Generator_ui_discloses_the_pause_and_disables_issuance()
        {
            string root = LocateProjectRoot();
            string xaml = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "MainWindow.xaml"));

            Assert.Contains("ISSUANCE PAUSED", xaml);
            Assert.Contains("IsEnabled=\"False\"", xaml);
            Assert.Contains("COPY KEY", xaml);
            Assert.Contains("7 DAYS", xaml);
            Assert.Contains("30 DAYS", xaml);
            Assert.Contains("6 MONTHS", xaml);
            Assert.Contains("1 YEAR", xaml);
            Assert.Contains("2 YEARS", xaml);

            // No key-file or request/response surface remains.
            Assert.DoesNotContain("LOAD .PGKEY", xaml);
            Assert.DoesNotContain("LOAD .PGREQUEST", xaml);
            Assert.DoesNotContain("GENERATE RESPONSE", xaml);
            Assert.DoesNotContain("ActivationRequestBox", xaml);
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
    }
}
