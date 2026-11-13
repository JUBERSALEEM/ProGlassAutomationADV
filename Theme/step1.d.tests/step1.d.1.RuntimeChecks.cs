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
            var hostTextBoxStyle = new Style(typeof(TextBox));
            var hostComboBoxStyle = new Style(typeof(ComboBox));
            var hostDataGridStyle = new Style(typeof(DataGrid));
            var hostGridHeaderStyle = new Style(typeof(DataGridColumnHeader));
            var hostTokens = new ResourceDictionary
            {
                ["AccentBrush"] = Brushes.Purple,
                ["SurfacePrimaryBrush"] = Brushes.White,
                ["FontFamily.App"] = new FontFamily("Arial"),
                ["Density.ControlHeight"] = 23d,
                ["Radius.Control"] = new CornerRadius(2),
                ["PgPageBrush"] = Brushes.Black,
                ["PgSurfaceBrush"] = Brushes.Black,
                ["PgAccentBrush"] = Brushes.Black,
                ["Surface.Primary.BackgroundBrush"] = Brushes.Black,
                ["Surface.Primary.BorderBrush"] = Brushes.Black,
                ["Surface.Primary.BorderThickness"] = new Thickness(1),
                ["Surface.Primary.CornerRadius"] = new CornerRadius(2),
                ["Surface.Primary.Padding"] = new Thickness(4),
                ["PgText1Brush"] = Brushes.Black,
                ["PgCardRadius"] = new CornerRadius(2),
                ["PgFontBody"] = 10d,
                ["PrimaryButtonStyle"] = new Style(typeof(Button)),
                ["Button.Primary"] = new Style(typeof(Button)),
                ["TextBoxStyle"] = hostTextBoxStyle,
                ["Input.TextBox"] = new Style(typeof(TextBox)),
                ["SurfaceTextBox"] = new Style(typeof(TextBox)),
                ["Step4InputStyle"] = new Style(typeof(TextBox)),
                ["ComboBoxStyle"] = hostComboBoxStyle,
                ["ModernComboBoxStyle"] = new Style(typeof(ComboBox)),
                ["Step4ComboStyle"] = new Style(typeof(ComboBox)),
                ["SurfaceDataGrid"] = hostDataGridStyle,
                ["GridHeaderCell"] = hostGridHeaderStyle
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
            Check(app.TryFindResource("PgAccentBrush") is SolidColorBrush shellAccent &&
                  shellAccent.Color == ((SolidColorBrush)ImportedThemeRuntime.Engine.GetToken("AccentPrimary")).Color,
                "imported accent reaches the visible Meridian shell palette");
            Check(app.TryFindResource("PgSurfaceBrush") is Brush shellSurface &&
                  shellSurface == ImportedThemeRuntime.Engine.GetToken("SurfacePrimary"),
                "imported surface reaches the visible Meridian shell palette");
            Check(app.TryFindResource("PgCardRadius") is CornerRadius shellRadius &&
                  shellRadius == (CornerRadius)ImportedThemeRuntime.Engine.GetToken("CardCornerRadius"),
                "imported geometry reaches live Meridian card styles");
            Check(app.TryFindResource("PgFontBody") is double shellFont &&
                  shellFont == (double)ImportedThemeRuntime.Engine.GetToken("FontSizeBody"),
                "imported typography reaches live Meridian typography styles");
            Check(app.TryFindResource("Surface.Primary.BackgroundBrush") is Brush semanticSurface &&
                  semanticSurface == ImportedThemeRuntime.Engine.GetToken("SurfacePrimary"),
                "imported primary surface reaches the existing ERP semantic surface resource");
            Check(app.TryFindResource("Surface.Primary.BorderBrush") is Brush semanticBorder &&
                  semanticBorder == ImportedThemeRuntime.Engine.GetToken("BorderSubtle"),
                "imported border token reaches the existing ERP semantic border resource");
            Check(app.TryFindResource("Surface.Primary.CornerRadius") is CornerRadius semanticRadius &&
                  semanticRadius == (CornerRadius)ImportedThemeRuntime.Engine.GetToken("CardCornerRadius"),
                "imported card geometry reaches the existing ERP semantic surface resource");
            Check(app.TryFindResource("Surface.Primary.Padding") is Thickness semanticPadding &&
                  semanticPadding == (Thickness)ImportedThemeRuntime.Engine.GetToken("PaddingCard"),
                "imported card padding reaches the existing ERP semantic surface resource");
            Check(app.TryFindResource("Imported.SurfacePrimaryColor") is Color localSurfaceColor &&
                  localSurfaceColor == ((SolidColorBrush)ImportedThemeRuntime.Engine.GetToken("SurfacePrimary")).Color,
                "imported surface color is published for view-local brush dictionaries");
            Check(app.TryFindResource("PrimaryButtonStyle") is Style primaryStyle &&
                  ReferenceEquals(primaryStyle, ImportedThemeRuntime.Engine.GetResource("PrimaryButton")),
                "host primary-button style resolves to the active imported family style");
            Check(app.TryFindResource("TextBoxStyle") is Style themedTextBox &&
                  ReferenceEquals(themedTextBox.BasedOn, hostTextBoxStyle) && themedTextBox.Setters.Count > 0 &&
                  !themedTextBox.Setters.OfType<Setter>().Any(setter => setter.Property == Control.TemplateProperty),
                "explicit ERP text-box style keeps its base contract and receives package presentation setters");
            Check(app.TryFindResource("ComboBoxStyle") is Style themedComboBox &&
                  ReferenceEquals(themedComboBox.BasedOn, hostComboBoxStyle) && themedComboBox.Setters.Count > 0,
                "explicit ERP combo-box style keeps its base contract and receives package presentation setters");
            Check(app.TryFindResource("SurfaceDataGrid") is Style themedGrid &&
                  ReferenceEquals(themedGrid.BasedOn, hostDataGridStyle) && themedGrid.Setters.Count > 0,
                "explicit ERP data-grid style keeps its base contract and receives package presentation setters");
            Check(app.TryFindResource("GridHeaderCell") is Style themedGridHeader &&
                  ReferenceEquals(themedGridHeader.BasedOn, hostGridHeaderStyle) && themedGridHeader.Setters.Count > 0,
                "explicit ERP data-grid header style keeps its base contract and receives package presentation setters");
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
                Check(app.TryFindResource("TextBoxStyle") is Style switchedTextBox &&
                      ReferenceEquals(switchedTextBox.BasedOn, hostTextBoxStyle) && switchedTextBox.Setters.Count > 0,
                    family + " explicit TextBox style follows the active package without replacing its ERP base");
                Check(app.TryFindResource("ComboBoxStyle") is Style switchedCombo &&
                      ReferenceEquals(switchedCombo.BasedOn, hostComboBoxStyle) && switchedCombo.Setters.Count > 0,
                    family + " explicit ComboBox style follows the active package without replacing its ERP base");
                Check(app.TryFindResource("SurfaceDataGrid") is Style switchedGrid &&
                      ReferenceEquals(switchedGrid.BasedOn, hostDataGridStyle) && switchedGrid.Setters.Count > 0,
                    family + " explicit DataGrid style follows the active package without replacing its ERP base");
                Check(app.TryFindResource("PgAccentBrush") is Brush activeShellAccent &&
                      activeShellAccent == ImportedThemeRuntime.Engine.GetToken("AccentPrimary"),
                    family + " palette reaches the visible shell");
                Check(app.TryFindResource("PgCardRadius") is CornerRadius activeShellRadius &&
                      activeShellRadius == (CornerRadius)ImportedThemeRuntime.Engine.GetToken("CardCornerRadius"),
                    family + " geometry reaches visible cards");
                Check(app.TryFindResource("Surface.Primary.BackgroundBrush") is Brush activeSemanticSurface &&
                      activeSemanticSurface == ImportedThemeRuntime.Engine.GetToken("SurfacePrimary"),
                    family + " primary surface reaches the ERP semantic style vocabulary");
                Check(app.TryFindResource("Imported.SurfacePrimaryColor") is Color activeLocalSurfaceColor &&
                      activeLocalSurfaceColor == ((SolidColorBrush)ImportedThemeRuntime.Engine.GetToken("SurfacePrimary")).Color,
                    family + " family surface color is republished for view-local dictionaries");
                ResourceValidation.Validate(ImportedThemeRuntime.Engine.Load(
                    dark ? Appearance.Dark : Appearance.Light, expected), family + " / " + (dark ? "Dark" : "Light"));
                Check(app.Resources.MergedDictionaries.Count == slotCount, "theme switch keeps a single package slot");
            }

            var colors = new ColorPlateState(Plate: "Ocean",
                RoleHex: new Dictionary<string, string> { ["AccentPrimary"] = "#1F6B4A" });
            ImportedThemeRuntime.Engine.Overlay((package, mode) =>
                ColorPlateService.Apply(package, mode, colors, ImportedThemeRuntime.Engine.ActiveTheme));
            Check(ImportedThemeRuntime.Engine.GetToken("AccentPrimary") is Brush, "color plate publishes typed accent");
            Check(app.TryFindResource("AccentBrush") is SolidColorBrush platedAccent &&
                  platedAccent.Color == Color.FromRgb(0x1F, 0x6B, 0x4A),
                "custom color plate role reaches the ERP host resource graph");
            Check(app.TryFindResource("PgAccentBrush") is SolidColorBrush platedShellAccent &&
                  platedShellAccent.Color == Color.FromRgb(0x1F, 0x6B, 0x4A),
                "custom color plate role visibly reaches the Meridian shell");
            foreach (IconMarkStyle style in Enum.GetValues<IconMarkStyle>())
            {
                IconService.Apply(new IconCustomizationState(style));
                foreach (IconKind kind in Enum.GetValues<IconKind>())
                {
                    Geometry mark = IconService.Mark(kind, style);
                    Check(mark.IsFrozen, $"icon geometry is available and frozen: {kind} / {style}");
                }
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
