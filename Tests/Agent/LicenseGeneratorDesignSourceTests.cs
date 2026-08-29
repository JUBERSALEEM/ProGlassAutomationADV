// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/LicenseGeneratorDesignSourceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Source-level invariants for the License Generator (2026 simple
//         flow). These tests pin the DESIGN of the vendor tool the way
//         a style/lint gate would: the tool signs license keys only,
//         never embeds private keys, and keeps the old activation
//         request/response machinery out of the UI.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
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
        public void Generator_core_signs_armored_license_keys_and_never_raw_base64_blobs()
        {
            string root = LocateProjectRoot();
            string core = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "LicenseGeneratorCore.cs"));

            Assert.Contains("GenerateLicenseKey", core);
            Assert.Contains("LicenseKeyArmor.Encode", core);
            Assert.Contains("LicenseService.Create", core);
            Assert.Contains("LicenseService.VerifyAndValidate", core);

            // The activation request/response signing path was removed
            // from the tool core in the simple-flow rewrite.
            Assert.DoesNotContain("OfflineActivationService.SignResponseJson", core);
            Assert.DoesNotContain("GenerateActivationResponse", core);
            Assert.DoesNotContain("Convert.ToBase64String", core);
            Assert.DoesNotContain("TryDecodeLicenseJson", core);
        }

        [Fact]
        public void Generator_uses_public_trust_hints_only_and_external_key_material_for_signing()
        {
            string root = LocateProjectRoot();
            string keys = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "AppCompatibleIssuerKeys.cs"));
            string keyMaterial = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "IssuerKeyMaterial.cs"));
            string appKeys = File.ReadAllText(Path.Combine(root,
                "Services", "Licensing", "LicensePublicKey.cs"));

            Assert.Contains("TrustedPublicKeys", keys);
            Assert.DoesNotContain("PrivateKeyXml =>", keys);
            Assert.Contains("privateKeyXml", keyMaterial, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Generate(string keyId, string purpose)", keyMaterial);
            Assert.Contains("GeneratorCompatiblePublicKeyXml", appKeys);
        }

        [Fact]
        public void Generator_ui_is_worded_as_the_key_generation_flow()
        {
            string root = LocateProjectRoot();
            string xaml = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "MainWindow.xaml"));
            string code = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "MainWindow.xaml.cs"));

            Assert.Contains("LOAD .PGKEY", xaml);
            Assert.Contains("GENERATE LICENSE KEY", xaml);
            Assert.Contains("COPY KEY", xaml);
            Assert.Contains("GenerateLicenseKey", code);
            Assert.Contains("IssuerKeyMaterial.LoadFromFile", code);
            Assert.Contains("LicenseGeneratorCore.RunSelfTest", code);

            // The request/response flow must not appear in the UI.
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
