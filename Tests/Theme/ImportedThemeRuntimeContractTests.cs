using System.IO;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Theming;
using Xunit;

namespace ProGlassAutomation.Tests.Theme;

public sealed class ImportedThemeRuntimeContractTests
{
    [Fact]
    public void ImportedCatalogContainsAllNineFamilies()
    {
        Assert.Equal(9, ThemeService.InstalledThemes.Count);
        Assert.Contains(ThemeService.Bento, ThemeService.InstalledThemes);
        Assert.Contains(ThemeService.Clay, ThemeService.InstalledThemes);
        Assert.Contains(ThemeService.Fluent, ThemeService.InstalledThemes);
        Assert.Contains(ThemeService.Neumorphism, ThemeService.InstalledThemes);
        Assert.Contains(ThemeService.LiquidGlass, ThemeService.InstalledThemes);
        Assert.Contains(ThemeService.Glassmorphism, ThemeService.InstalledThemes);
        Assert.Contains(ThemeService.Spatial, ThemeService.InstalledThemes);
        Assert.Contains(ThemeService.Swiss, ThemeService.InstalledThemes);
        Assert.Contains(ThemeService.Holographic, ThemeService.InstalledThemes);
    }

    [Fact]
    public void CurrentApplicationRoutesRuntimeAndStudioThroughImportedEngine()
    {
        string root = FindRepositoryRoot();
        string runtime = File.ReadAllText(Path.Combine(root, "Services/Theme/ImportedThemeRuntime.cs"));
        string pipeline = File.ReadAllText(Path.Combine(root, "Services/Theme/ThemePipeline.cs"));
        string startup = File.ReadAllText(Path.Combine(root, "App.StartupCore.cs"));
        string settings = File.ReadAllText(Path.Combine(root, "Views/Settings/Sections/SettingsThemesSection.xaml"));
        string icons = File.ReadAllText(Path.Combine(root, "Themes/Professional/ProfessionalShell.xaml"));
        string dashboardIcons = File.ReadAllText(Path.Combine(root, "Views/Dashboard/IndigoDashboardResources.xaml"));
        string settingsIcons = File.ReadAllText(Path.Combine(root, "Views/Settings/SettingsSectionStyles.xaml"));
        string iconCatalog = File.ReadAllText(Path.Combine(root, "Theme/step1.g.Customization/step1.g.8.IconService.cs"));
        Assert.Contains("SharedGeometry.Compose", runtime);
        Assert.Contains("ColorPlateState", runtime);
        Assert.Contains("IconService.Apply", runtime);
        Assert.Contains("HasHostResource", runtime);
        Assert.Contains("SurfacePrimaryBrush", runtime);
        Assert.Contains("TypographyTitleSize", runtime);
        Assert.Contains("PublishHostStyleAliases", runtime);
        Assert.Contains("GetResource(object key)", File.ReadAllText(Path.Combine(root, "Theme/step1.a.ThemeEngine/step1.a.2.ThemeService.cs")));
        Assert.Contains("ImportedThemeRuntime.ApplyFromHost", pipeline);
        Assert.Contains("ImportedThemeRuntime.Initialize", startup);
        Assert.Contains("OpenImportedThemeStudioCommand", settings);
        Assert.Contains("ImportedIconMark", icons);
        Assert.Contains("ImportedIconMark", dashboardIcons);
        Assert.Contains("ImportedIconMark", settingsIcons);
        Assert.Contains("IconKind.Search", iconCatalog);
        Assert.Contains("IconKind.Add", iconCatalog);
        Assert.Contains("IconKind.Delete", iconCatalog);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ProGlassAutomation.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not find the application repository root.");
    }
}
