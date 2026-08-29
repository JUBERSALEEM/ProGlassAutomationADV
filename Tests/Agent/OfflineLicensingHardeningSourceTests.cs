using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent;

public sealed class OfflineLicensingHardeningSourceTests
{
    [Fact]
    public void Generator_public_trust_file_does_not_embed_a_private_key_literal()
    {
        string src = File.ReadAllText(Path.Combine(Root(),
            "Tools", "LicenseGenerator", "AppCompatibleIssuerKeys.cs"));

        Assert.DoesNotContain("PrivateKeyXml =>", src, StringComparison.Ordinal);
        Assert.DoesNotContain("<D>", src, StringComparison.Ordinal);
        Assert.DoesNotContain("<P>", src, StringComparison.Ordinal);
        Assert.DoesNotContain("<Q>", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Client_trial_key_vault_is_placeholder_only_after_secure_cutover()
    {
        string src = File.ReadAllText(Path.Combine(Root(),
            "Services", "Licensing", "TrialKeyVault.cs"));

        Assert.Contains("PLACEHOLDER_TRIAL_PRIVATE_KEY_REMOVED", src, StringComparison.Ordinal);
        Assert.DoesNotContain("<RSAKeyValue>", src, StringComparison.Ordinal);
        Assert.DoesNotContain("<D>", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Activation_view_uses_request_response_language_not_base64_license_key_language()
    {
        string xaml = File.ReadAllText(Path.Combine(Root(),
            "Views", "Licensing", "LicenseActivationView.xaml"));

        Assert.Contains("GENERATE ACTIVATION REQUEST", xaml, StringComparison.Ordinal);
        Assert.Contains("IMPORT SIGNED ACTIVATION RESPONSE", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("PASTE A LICENSE KEY", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void License_service_exposes_secure_activation_request_api()
    {
        string src = File.ReadAllText(Path.Combine(Root(),
            "Services", "Licensing", "LicenseService.cs"));

        Assert.Contains("CreateActivationRequestJson", src, StringComparison.Ordinal);
        Assert.Contains("ExportActivationRequest", src, StringComparison.Ordinal);
        Assert.Contains("Base64-wrapped whole-license activation is no longer accepted", src, StringComparison.Ordinal);
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var probe = Path.Combine(dir.FullName, "ProGlassAutomation.csproj");
            if (File.Exists(probe))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
