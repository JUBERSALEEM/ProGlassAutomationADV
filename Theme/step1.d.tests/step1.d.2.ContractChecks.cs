using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Theming;

namespace ProGlassAutomation.ThemeTests;

internal static class ContractChecks
{
    public static void Run()
    {
        CustomizationChecks.Run();
        AnalyticsChecks.DataContracts();
        CustomizationPersistenceChecks.Run();
        foreach (string theme in ThemeService.InstalledThemes)
        {
            foreach (var mode in Enum.GetValues<Appearance>())
            {
                var resources = ThemeService.Load(mode, theme);
                ResourceValidation.Validate(resources, theme + " / " + mode);
                string[] extra = resources.Keys.Cast<object>().OfType<string>().Except(ThemeContract.Required.Keys).ToArray();
                if (extra.Length != 0) throw new Exception(theme + " undocumented tokens: " + string.Join(", ", extra));
                var foregrounds = new[] { "TextPrimary", "TextSecondary", "TextMuted" };
                foreach (string foreground in foregrounds)
                    foreach (string surface in new[] { "ApplicationBackground", "SurfacePrimary", "SurfaceSecondary", "SurfaceElevated", "StateHover", "StatePressed", "StateSelected", "AccentSoft" })
                        Contrast(resources, foreground, surface, 4.5);
                Contrast(resources, "TextOnAccent", "AccentPrimary", 4.5);
                Contrast(resources, "TextOnAccent", "AccentHover", 4.5);
                Contrast(resources, "TextOnAccent", "AccentPressed", 4.5);
                foreach (string status in new[] { "Success", "Warning", "Error", "Information" })
                    Contrast(resources, "Status" + status, "Status" + status + "Surface", 4.5);
                if (theme == ThemeService.Bento)
                    VerifyStyleFailures(resources);
                resources.Remove("TextPrimary");
                ExpectFailure(() => ResourceValidation.Validate(resources), "missing token");
                resources["TextPrimary"] = "not a brush";
                ExpectFailure(() => ResourceValidation.Validate(resources), "wrong token type");
                resources["TextPrimary"] = new SolidColorBrush();
                resources["SpacingMedium"] = double.NaN;
                ExpectFailure(() => ResourceValidation.Validate(resources), "invalid numeric token");
            }
        }
    }

    private static void VerifyStyleFailures(ResourceDictionary resources)
    {
        var styles = resources.MergedDictionaries.Single();
        var original = styles["PrimaryButton"];
        styles.Remove("PrimaryButton");
        ExpectFailure(() => ResourceValidation.Validate(resources), "missing style with active fallback present", "Missing required style: PrimaryButton");
        styles["PrimaryButton"] = new Style(typeof(System.Windows.Controls.TextBox));
        ExpectFailure(() => ResourceValidation.Validate(resources), "wrong style target", "must target Button");
        styles["PrimaryButton"] = original;
        resources.MergedDictionaries.Add(new ResourceDictionary { ["SurfacePrimary"] = new SolidColorBrush() });
        ExpectFailure(() => ResourceValidation.Validate(resources), "duplicate merged key", "Duplicate resource key");
        resources.MergedDictionaries.RemoveAt(resources.MergedDictionaries.Count - 1);
        var fresh = ThemeService.Load(Appearance.Light);
        ResourceValidation.Validate(fresh);
        if (!ReferenceEquals(fresh["PrimaryButton"], original)) throw new Exception("Bento style identity changed unexpectedly.");
        if (ReferenceEquals(fresh.MergedDictionaries[0], styles)) throw new Exception("Candidates share a mutable style dictionary.");
        if (Application.Current.TryFindResource("PrimaryButton") is not Style)
            throw new Exception("Negative package tests damaged the installed style.");
    }

    public static void ExpectFailure(Action action, string scenario, string? expectedDiagnostic = null)
    {
        try { action(); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            if (expectedDiagnostic is not null && !e.Message.Contains(expectedDiagnostic))
                throw new Exception($"Wrong diagnostic for {scenario}: {e.Message}", e);
            return;
        }
        throw new Exception("Expected failure for " + scenario);
    }

    internal static void Contrast(ResourceDictionary resources, string text, string surface, double minimum)
    {
        static double Luminance(Color c)
        {
            static double Linear(byte x) { double s = x / 255d; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
            return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        }
        double a = Luminance(((SolidColorBrush)resources[text]).Color);
        double b = Luminance(((SolidColorBrush)resources[surface]).Color);
        double ratio = (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
        if (ratio < minimum) throw new Exception($"Contrast {text}/{surface}: {ratio:0.00}, requires {minimum}.");
    }
}
