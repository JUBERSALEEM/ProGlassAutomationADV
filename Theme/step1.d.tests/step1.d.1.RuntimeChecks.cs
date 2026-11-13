using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Theming;
using ProGlassAutomation.Customization;
using ProGlassAutomation.ThemeTests;

namespace ProGlassAutomation.ThemeTests;

/// <summary>Windows STA runtime checks for the imported packages inside the host ERP.</summary>
internal static class RuntimeChecks
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 1 || (args.Length == 1 && args[0] != "--contracts-only"))
            throw new ArgumentException("Usage: [--contracts-only]");
        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            // Seed resources in a merged host dictionary, matching the ERP's
            // real App.xaml layout. The bridge must find these keys through the
            // resource graph (not just direct entries on Application.Resources).
            var hostTokens = new ResourceDictionary
            {
                ["AccentBrush"] = Brushes.Purple,
                ["SurfacePrimaryBrush"] = Brushes.White,
                ["FontFamily.App"] = new FontFamily("Arial"),
                ["Density.ControlHeight"] = 23d,
                ["Radius.Control"] = new CornerRadius(2),
                ["PrimaryButtonStyle"] = new Style(typeof(Button)),
                ["Button.Primary"] = new Style(typeof(Button))
            };
            app.Resources.MergedDictionaries.Add(hostTokens);
            ContractChecks.Run();
            if (args.Contains("--contracts-only"))
            {
                Console.WriteLine("PASS: nine imported packages, contract, contrast and persistence checks.");
                return 0;
            }

            ImportedThemeRuntime.Initialize(app.Resources,
                VisualThemeEngine.BentoGrid, dark: false, persistCustomization: false);
            Check(ImportedThemeRuntime.IsInitialized, "imported theme runtime initialized");
            Check(app.Resources.Contains("AccentBrush"), "theme bridge publishes root overrides for merged host tokens");
            Check(app.TryFindResource("AccentBrush") is SolidColorBrush publishedAccent &&
                  publishedAccent.Color == ((SolidColorBrush)ImportedThemeRuntime.Engine.GetToken("AccentPrimary")).Color,
                "accent maps from package into real host resource graph");
            Check(app.TryFindResource("FontFamily.App") is FontFamily publishedFont &&
                  publishedFont.Source == ((FontFamily)ImportedThemeRuntime.Engine.GetToken("FontFamilyBody")).Source,
                "font family maps into real host resource graph");
            Check(app.TryFindResource("Density.ControlHeight") is double publishedHeight && publishedHeight > 0,
                "control geometry maps into real host resource graph");
            Check(app.TryFindResource("PrimaryButtonStyle") is Style primaryStyle &&
                  ReferenceEquals(primaryStyle, ImportedThemeRuntime.Engine.GetResource("PrimaryButton")),
                "host primary-button style resolves to the active imported family style");
            int slotCount = app.Resources.MergedDictionaries.Count;
            var families = new[]
            {
                VisualThemeEngine.BentoGrid, VisualThemeEngine.Fluent,
                VisualThemeEngine.Claymorphism, VisualThemeEngine.Neumorphism,
                VisualThemeEngine.LiquidGlass, VisualThemeEngine.Glassmorphism,
                VisualThemeEngine.SpatialImmersive, VisualThemeEngine.SwissInternational,
                VisualThemeEngine.HolographicIridescent
            };
            foreach (string family in families)
            foreach (bool dark in new[] { false, true })
            {
                ImportedThemeRuntime.ApplyFromHost(family, dark);
                string expected = ToImportedName(family);
                Check(ImportedThemeRuntime.Engine.ActiveTheme == expected, family + " package active");
                Check(ImportedThemeRuntime.Engine.ActiveAppearance == (dark ? Appearance.Dark : Appearance.Light), family + " mode active");
                Check(ReferenceEquals(app.TryFindResource("PrimaryButtonStyle"),
                    ImportedThemeRuntime.Engine.GetResource("PrimaryButton")), family + " explicit button style active");
                ResourceValidation.Validate(ImportedThemeRuntime.Engine.Load(
                    dark ? Appearance.Dark : Appearance.Light, expected), family + " / " + (dark ? "Dark" : "Light"));
                Check(app.Resources.MergedDictionaries.Count == slotCount, "theme switch keeps a single package slot");
            }

            var colors = new ColorPlateState(Plate: "Ocean");
            ImportedThemeRuntime.Engine.Overlay((package, mode) =>
                ColorPlateService.Apply(package, mode, colors, ImportedThemeRuntime.Engine.ActiveTheme));
            Check(ImportedThemeRuntime.Engine.GetToken("AccentPrimary") is Brush, "color plate publishes typed accent");
            foreach (IconMarkStyle style in Enum.GetValues<IconMarkStyle>())
            {
                IconService.Apply(new IconCustomizationState(style));
                Geometry mark = IconService.Mark(IconKind.Theme, style);
                Check(mark is not null && mark.IsFrozen, "icon geometry is available and frozen: " + style);
            }
            Console.WriteLine("PASS: host adapter, all 18 theme/mode activations, color plate overlay and icon styles.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally { app.Shutdown(); }
    }

    private static string ToImportedName(string family) => family switch
    {
        VisualThemeEngine.BentoGrid => ThemeService.Bento,
        VisualThemeEngine.Fluent => ThemeService.Fluent,
        VisualThemeEngine.Claymorphism => ThemeService.Clay,
        VisualThemeEngine.Neumorphism => ThemeService.Neumorphism,
        VisualThemeEngine.LiquidGlass => ThemeService.LiquidGlass,
        VisualThemeEngine.Glassmorphism => ThemeService.Glassmorphism,
        VisualThemeEngine.SpatialImmersive => ThemeService.Spatial,
        VisualThemeEngine.SwissInternational => ThemeService.Swiss,
        VisualThemeEngine.HolographicIridescent => ThemeService.Holographic,
        _ => throw new ArgumentException("Unknown host family", nameof(family))
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Runtime check failed: " + message);
    }
}
