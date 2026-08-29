using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class LicenseGeneratorCompatibilitySourceTests
    {
        [Fact]
        public void Generator_project_now_references_the_contract_library_instead_of_linking_legacy_app_license_sources()
        {
            string root = LocateProjectRoot();
            string csproj = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "ProGlassAutomation.LicenseGenerator.csproj"));

            Assert.Contains("Licensing\\ProGlass.Licensing.csproj", csproj);
            Assert.DoesNotContain("Services\\Licensing\\LicensePayload.cs", csproj);
            Assert.DoesNotContain("Services\\Licensing\\LicenseSigner.cs", csproj);
        }

        [Fact]
        public void Generator_core_emits_signed_offline_activation_responses_not_base64_license_keys()
        {
            string root = LocateProjectRoot();
            string core = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "LicenseGeneratorCore.cs"));

            Assert.Contains("GenerateActivationResponse", core);
            Assert.Contains("OfflineActivationService.SignResponseJson", core);
            Assert.Contains("OfflineActivationService.VerifyResponse", core);
            Assert.Contains("ActivationRequestExtension = \".pgrequest\"", core);
            Assert.Contains("ActivationResponseExtension = \".lic\"", core);
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
        public void Generator_ui_is_worded_as_offline_request_response_flow()
        {
            string root = LocateProjectRoot();
            string xaml = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "MainWindow.xaml"));
            string code = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "MainWindow.xaml.cs"));

            Assert.Contains("LOAD .PGKEY", xaml);
            Assert.Contains("LOAD .PGREQUEST", xaml);
            Assert.Contains("GENERATE RESPONSE", xaml);
            Assert.Contains("COPY RESPONSE JSON", xaml);
            Assert.Contains("GenerateActivationResponse", code);
            Assert.Contains("ParseActivationRequestText", code);
            Assert.Contains("IssuerKeyMaterial.LoadFromFile", code);
            Assert.Contains("LicenseGeneratorCore.RunSelfTest", code);
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
