using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class LicenseGeneratorCompatibilitySourceTests
    {
        [Fact]
        public void Generator_project_links_the_app_compatible_license_contract_instead_of_the_new_contract_library()
        {
            string root = LocateProjectRoot();
            string csproj = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "ProGlassAutomation.LicenseGenerator.csproj"));

            Assert.Contains("Services\\Licensing\\LicensePayload.cs", csproj);
            Assert.Contains("Services\\Licensing\\LicenseSigner.cs", csproj);
            Assert.DoesNotContain("Licensing\\ProGlass.Licensing.csproj", csproj);
            Assert.DoesNotContain("System.Security.Cryptography.ProtectedData", csproj);
        }

        [Fact]
        public void Generator_core_emits_app_compatible_dot_lic_json_and_paste_key()
        {
            string root = LocateProjectRoot();
            string core = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "LicenseGeneratorCore.cs"));

            Assert.Contains("LicenseSigner.SignToFile", core);
            Assert.Contains("LicensePayload", core);
            Assert.Contains("Convert.ToBase64String", core);
            Assert.Contains("LicenseFileExtension = \".lic\"", core);
            Assert.Contains("InspectText", core);
            Assert.Contains("TryDecodeLicenseJson", core);
            Assert.Contains("DefaultFeatures(LicenseEdition.Business)", core);
        }

        [Fact]
        public void Generator_has_an_embedded_app_compatible_signing_pair_and_the_app_trusts_its_public_half()
        {
            string root = LocateProjectRoot();
            string keys = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "AppCompatibleIssuerKeys.cs"));
            string appKeys = File.ReadAllText(Path.Combine(root,
                "Services", "Licensing", "LicensePublicKey.cs"));
            string verifier = File.ReadAllText(Path.Combine(root,
                "Services", "Licensing", "LicenseVerifier.cs"));

            Assert.Contains("PublicKeyXml", keys);
            Assert.Contains("PrivateKeyXml", keys);
            Assert.Contains("LegacyPublicKeyXml", keys);
            Assert.Contains("TrustedPublicKeys", keys);
            Assert.Contains("GeneratorCompatiblePublicKeyXml", appKeys);
            Assert.Contains("ProductionPublicKeys", appKeys);
            Assert.Contains("TryVerifyWithAnyProductionKey", verifier);
            Assert.Contains("LicensePublicKey.ProductionPublicKeys", verifier);
            Assert.Contains("fingerprint(s)", verifier);
        }

        [Fact]
        public void Generator_ui_is_now_explicitly_worded_as_app_compatible_save_file_or_paste_key_flow()
        {
            string root = LocateProjectRoot();
            string xaml = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "MainWindow.xaml"));
            string code = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "MainWindow.xaml.cs"));

            Assert.Contains("GENERATE .LIC", xaml);
            Assert.Contains("COPY PASTE KEY", xaml);
            Assert.Contains("Portable (shared .lic, activation-limited)", xaml);
            Assert.Contains("Machine-bound (customer fingerprint required)", xaml);
            Assert.Contains("BuildRequest()", code);
            Assert.Contains("LicenseGeneratorCore.GenerateLicense", code);
            Assert.Contains("LicenseGeneratorCore.InspectFile", code);
            Assert.Contains("RunSelfTest", code);
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
