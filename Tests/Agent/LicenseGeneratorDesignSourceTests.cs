// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/LicenseGeneratorDesignSourceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Source-level invariants for the License Generator (2026
//         single-click plan flow). These tests pin the DESIGN of the
//         vendor tool: one embedded production key, plan-based signing,
//         armored keys, and the guarantee that the app trusts EXACTLY
//         the public half the generator signs with.
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
        public void Generator_core_signs_plan_based_armored_keys_and_never_raw_blobs()
        {
            string root = LocateProjectRoot();
            string core = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "LicenseGeneratorCore.cs"));

            Assert.Contains("GenerateLicenseKey", core);
            Assert.Contains("LicenseKeyArmor.Encode", core);
            Assert.Contains("LicenseService.Create", core);
            Assert.Contains("LicenseService.VerifyAndValidate", core);
            Assert.Contains("ProductionSigningKey", core);
            Assert.Contains("\"7 Days\"", core);
            Assert.Contains("\"2 Years\"", core);

            // The activation request/response signing path is gone.
            Assert.DoesNotContain("OfflineActivationService.SignResponseJson", core);
            Assert.DoesNotContain("GenerateActivationResponse", core);
            Assert.DoesNotContain("Convert.ToBase64String", core);
        }

        [Fact]
        public void App_trust_ring_accepts_exactly_the_generator_s_embedded_public_key()
        {
            string root = LocateProjectRoot();
            string src = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "ProductionSigningKey.cs"));

            // The first <RSAKeyValue>…</RSAKeyValue> literal in the file
            // is the PublicKeyXml constant; it must be byte-identical to
            // the public half embedded in the customer app. That is what
            // makes "generate → paste → instant active" airtight: the app
            // can never accept a key this generator did not sign.
            var match = Regex.Match(src, "<RSAKeyValue>.*?</RSAKeyValue>", RegexOptions.Singleline);
            Assert.True(match.Success, "ProductionSigningKey must embed a public RSAKeyValue literal.");

            Assert.Equal(LicensePublicKey.GeneratorCompatiblePublicKeyXml, match.Value);
            Assert.Contains("PrivateKeyXml", src);
        }

        [Fact]
        public void Generator_ui_is_worded_as_the_single_click_plan_flow()
        {
            string root = LocateProjectRoot();
            string xaml = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "MainWindow.xaml"));

            Assert.Contains("GENERATE LICENSE KEY", xaml);
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
