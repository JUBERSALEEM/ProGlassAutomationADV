using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent;

public sealed class OfflineLicensingHardeningSourceTests
{
    [Fact]
    public void Customer_app_embeds_no_private_key_material()
    {
        string src = File.ReadAllText(Path.Combine(Root(),
            "Services", "Licensing", "LicensePublicKey.cs"));

        Assert.DoesNotContain("PrivateKeyXml", src, StringComparison.Ordinal);
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
    public void Activation_view_uses_key_paste_and_plan_language_not_request_response_language()
    {
        // The view is a CODE-ONLY control (2026-08-29) — the XAML and
        // its generated partial are gone, so the language is pinned in
        // the code-behind and the whole InitializeComponent/x:Name
        // failure class is structurally impossible.
        string code = File.ReadAllText(Path.Combine(Root(),
            "Views", "Licensing", "LicenseActivationView.xaml.cs"));

        Assert.Contains("APPLY LICENSE KEY", code, StringComparison.Ordinal);
        Assert.Contains("GET LICENSE KEY", code, StringComparison.Ordinal);
        Assert.Contains("PLANS — GET A LICENSE KEY", code, StringComparison.Ordinal);
        Assert.DoesNotContain("GENERATE ACTIVATION REQUEST", code, StringComparison.Ordinal);
        Assert.DoesNotContain("IMPORT SIGNED ACTIVATION RESPONSE", code, StringComparison.Ordinal);

        Assert.False(File.Exists(Path.Combine(Root(),
                "Views", "Licensing", "LicenseActivationView.xaml")),
            "LicenseActivationView must stay code-only — the XAML-generated " +
            "partial was a recurring CS0103 failure point.");
    }

    [Fact]
    public void Activation_view_enforces_plan_mismatch_guards_on_paste()
    {
        // 2026-08-29: pasting a key must go through armor check →
        // signature verification → plan-conflict dialogs → install.
        string code = File.ReadAllText(Path.Combine(Root(),
            "Views", "Licensing", "LicenseActivationView.xaml.cs"));

        Assert.Contains("LicenseKeyArmor.IsArmoredBlock", code, StringComparison.Ordinal);
        Assert.Contains("ProGlassLicense.VerifyText", code, StringComparison.Ordinal);
        Assert.Contains("LicensePlanCatalog", code, StringComparison.Ordinal);
        Assert.Contains("ConfirmPlanChange", code, StringComparison.Ordinal);
        Assert.Contains("already active", code, StringComparison.Ordinal);
        Assert.Contains("wrong key", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Files_importing_both_licensing_namespaces_pin_bare_LicenseService_to_the_app_service()
    {
        // CS0104 regression guard (2026-08-29): ProGlass.Licensing and
        // ProGlassAutomation.Services.Licensing BOTH define
        // LicenseService. Any file importing both namespaces AND using
        // the bare name must carry the using-alias that pins it to the
        // APP service — otherwise Visual Studio reports CS0104.
        // (The two app VIEW files go further: they never import the
        // contract namespace at all — see the next test.)
        const string alias =
            "using LicenseService = ProGlassAutomation.Services.Licensing.LicenseService;";

        foreach (string path in Directory.GetFiles(Root(), "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains("\\obj\\") || path.Contains("\\bin\\"))
                continue;

            string src = File.ReadAllText(path);
            if (!src.Contains("using ProGlass.Licensing;", StringComparison.Ordinal)
                || !src.Contains("using ProGlassAutomation.Services.Licensing;", StringComparison.Ordinal))
                continue;

            bool usesBare = System.Text.RegularExpressions.Regex.IsMatch(
                src, @"(^|[^.A-Za-z])LicenseService[.(]");
            if (usesBare)
            {
                Assert.True(src.Contains(alias, StringComparison.Ordinal),
                    path + " uses bare LicenseService while importing both licensing " +
                    "namespaces — add the using-alias pinning it to the app service.");
            }
        }
    }

    [Fact]
    public void App_licensing_views_never_import_the_contract_namespace()
    {
        // CS0104 regression guard (stronger form, 2026-08-29): the two
        // app licensing views must NOT import ProGlass.Licensing at
        // all — contract types are fully qualified. With only the app
        // namespace imported, a bare LicenseService can never be
        // ambiguous in these files.
        string view = File.ReadAllText(Path.Combine(Root(),
            "Views", "Licensing", "LicenseActivationView.xaml.cs"));
        string dialog = File.ReadAllText(Path.Combine(Root(),
            "Views", "Licensing", "LicenseActivatedDialog.cs"));

        Assert.DoesNotContain("using ProGlass.Licensing;", view, StringComparison.Ordinal);
        Assert.DoesNotContain("using ProGlass.Licensing;", dialog, StringComparison.Ordinal);
        Assert.Contains("ProGlass.Licensing.LicenseKeyArmor", view, StringComparison.Ordinal);
        Assert.Contains("ProGlass.Licensing.LicensePlanCatalog", view, StringComparison.Ordinal);
        Assert.Contains("ProGlass.Licensing.LicensePlanCatalog", dialog, StringComparison.Ordinal);
    }

    [Fact]
    public void Activation_dialog_imports_documents_for_TextElement()
    {
        // CS0103 regression guard (2026-08-29): the dialog styles
        // TextBlock foregrounds via TextElement.ForegroundProperty,
        // which lives in System.Windows.Documents.
        string src = File.ReadAllText(Path.Combine(Root(),
            "Views", "Licensing", "LicenseActivatedDialog.cs"));

        Assert.Contains("using System.Windows.Documents;", src, StringComparison.Ordinal);
        Assert.Contains("TextElement.ForegroundProperty", src, StringComparison.Ordinal);
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
