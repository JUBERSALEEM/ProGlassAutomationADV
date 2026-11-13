using System.IO;
using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Customization;
using ProGlassAutomation.Theming;
using ProGlassAutomation.ThemeBento;
using ProGlassAutomation.ThemeClaymorphism;
using ProGlassAutomation.ThemeLiquidGlass;
using ProGlassAutomation.ThemeNeumorphism;

namespace ProGlassAutomation.ThemeTests;
internal static class CustomizationPersistenceChecks
{
    public static void Run()
    {
        // Every customization control must affect UI — test via resource mutation
        var light = ThemeService.Load(Appearance.Light);
        var state = new BentoCustomizationState(BentoAccent.Indigo, BentoDensity.Compact, BentoShape.Rounded, BentoType.Large, BentoCanvas.Warm, BentoSurfaceStyle.Elevated, BentoTextContrast.High, BentoBorderIntensity.Strong, BentoShadowIntensity.Elevated, BentoSpacing.Spacious, BentoCardPadding.Spacious, BentoFontWeight.Semibold, BentoLayoutPreset.Executive, true, false, false);
        BentoCustomization.ApplyExtended(light, Appearance.Light, state);
        // Validate all appearance properties mutated
        if ((double)light["ControlMinHeight"] != 32) throw new Exception("Compact density height");
        if ((double)light["FontSizeBody"] != 14) throw new Exception("Large typography");
        if (((System.Windows.CornerRadius)light["CardCornerRadius"]).TopLeft != 18) throw new Exception("Rounded card radius");
        // Canvas tint applied
        if (((System.Windows.Media.SolidColorBrush)light["ApplicationBackground"]).Color.ToString() != "#FFF5EFE6") throw new Exception("Warm canvas not applied");
        // Save/load roundtrip
        var clayState = new ClayCustomizationState(ClayAccent.Berry, ClayVolume.Deep, ClayPlumpness.Puffy, ClayDensity.Compact, ClayCanvas.Cool, ClayType.Large, ClaySpacing.Spacious, ClayTextContrast.High, ClaySheen.Gloss, ClayWeight.Semibold);
        var neuState = new NeumorphCustomizationState(NeumorphDepth.Deep, NeumorphBlur.Diffuse, NeumorphIntensity.Strong);
        var liquidState = new LiquidCustomizationState(LiquidFrost.Clear, LiquidSpecular.Bright, LiquidTint.Warm);
        var fontState = new FontCustomizationState("Georgia", false, true, true);
        var colorState = new ColorPlateState("Forest", new Dictionary<string, string> { ["AccentPrimary"] = "#1F6B4A" });
        var iconState = new IconCustomizationState(IconMarkStyle.Duotone);
        var saved = new SavedCustomization(2, Appearance.Dark, state, null, null, [new NamedPreset("My Test", state, Appearance.Dark)], ThemeCatalog.FluentName, clayState, [new ClayNamedPreset("Berry Night", clayState, Appearance.Dark)], new SharedGeometryState(CardRounding.Pill), neuState, [new NeumorphNamedPreset("Deep Night", neuState, Appearance.Dark)], fontState, colorState, liquidState, [new LiquidNamedPreset("Clear Night", liquidState, Appearance.Dark)], iconState);
        var tmp = Path.Combine(Path.GetTempPath(), "proglass-test-" + Guid.NewGuid() + ".json");
        CustomizationStore.WriteFile(saved, tmp);
        var fromFile = CustomizationStore.ReadFile(tmp);
        if (fromFile.Preferences.Accent != BentoAccent.Indigo || fromFile.Theme != ThemeCatalog.FluentName) throw new Exception("Store write/read roundtrip");
        if (fromFile.Clay?.Accent != ClayAccent.Berry) throw new Exception("Store clay roundtrip");
        if (fromFile.Geometry?.Rounding != CardRounding.Pill) throw new Exception("Store geometry roundtrip");
        if (fromFile.Neumorph?.Depth != NeumorphDepth.Deep) throw new Exception("Store neumorph roundtrip");
        if (fromFile.Liquid?.Frost != LiquidFrost.Clear) throw new Exception("Store liquid roundtrip");
        if (fromFile.Font?.Family != "Georgia" || fromFile.Font.BodyItalic != true || fromFile.Font.BodyUnderline != true) throw new Exception("Store font roundtrip");
        if (fromFile.Colors?.Plate != "Forest" || fromFile.Colors.RoleHex is null || fromFile.Colors.RoleHex["AccentPrimary"] != "#1F6B4A") throw new Exception("Store color plate roundtrip");
        if (fromFile.Icons?.Style != IconMarkStyle.Duotone) throw new Exception("Store icon roundtrip");
        if (fromFile.Colors.CardSaturation != 0 || ColorPlateService.ParseHex(fromFile.Colors.CardTint) is null) throw new Exception("Store card tint defaults");
        File.WriteAllText(tmp, "{\"Version\":1}");
        try { CustomizationStore.ReadFile(tmp); throw new Exception("Version 1 import must fail"); }
        catch (ArgumentException) { }
        File.WriteAllBytes(tmp, new byte[CustomizationStore.MaxBytes + 1]);
        try { CustomizationStore.ReadFile(tmp); throw new Exception("Oversized import must fail"); }
        catch (ArgumentException) { }
        File.WriteAllText(tmp, "{}");
        try { CustomizationStore.ReadFile(tmp); throw new Exception("Empty import must fail"); }
        catch (ArgumentException) { }
        File.WriteAllText(tmp, "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"></ResourceDictionary>");
        try { CustomizationStore.ReadFile(tmp); throw new Exception("XAML import must fail"); }
        catch (ArgumentException) { }
        File.Delete(tmp);
        var json = System.Text.Json.JsonSerializer.Serialize(saved, new System.Text.Json.JsonSerializerOptions{ WriteIndented=true, Converters={ new System.Text.Json.Serialization.JsonStringEnumConverter()}});
        var reloaded = System.Text.Json.JsonSerializer.Deserialize<SavedCustomization>(json, new System.Text.Json.JsonSerializerOptions{ Converters={ new System.Text.Json.Serialization.JsonStringEnumConverter()}, PropertyNameCaseInsensitive=true});
        if (reloaded!.Preferences.Accent != BentoAccent.Indigo) throw new Exception("Roundtrip accent");
        if (reloaded.Theme != ThemeCatalog.FluentName) throw new Exception("Roundtrip theme");
        if (reloaded.Clay?.Accent != ClayAccent.Berry) throw new Exception("Roundtrip clay accent");
        if (reloaded.Clay?.Typography != ClayType.Large || reloaded.Clay?.Sheen != ClaySheen.Gloss) throw new Exception("Roundtrip clay extras");
        if (reloaded.ClayPresets is not { Count: 1 } || reloaded.ClayPresets[0].Name != "Berry Night") throw new Exception("Clay preset roundtrip");
        if (reloaded.Neumorph?.Blur != NeumorphBlur.Diffuse || reloaded.NeumorphPresets is not { Count: 1 } || reloaded.NeumorphPresets[0].Name != "Deep Night") throw new Exception("Neumorph preset roundtrip");
        if (reloaded.Liquid?.Tint != LiquidTint.Warm || reloaded.LiquidPresets is not { Count: 1 } || reloaded.LiquidPresets[0].Name != "Clear Night") throw new Exception("Liquid preset roundtrip");
        if (reloaded.Font?.Family != "Georgia" || reloaded.Font.BodyItalic != true) throw new Exception("Roundtrip font");
        if (reloaded.Colors?.Plate != "Forest") throw new Exception("Roundtrip color plate");
        var overlay = ThemeService.Load(Appearance.Light);
        FontService.Apply(overlay, new FontCustomizationState("Georgia", true, true, true));
        ColorPlateService.Apply(overlay, Appearance.Light, new ColorPlateState("Slate"));
        ResourceValidation.Validate(overlay);
        if ((FontStyle)overlay["FontStyleBody"] != FontStyles.Italic) throw new Exception("Font italic overlay");
        if ((overlay["TextDecorationsBody"] as TextDecorationCollection) is not { Count: 1 }) throw new Exception("Font underline overlay");
        if (((SolidColorBrush)overlay["AccentPrimary"]).Color.ToString() != "#FF445060") throw new Exception("Slate plate overlay");
        if (reloaded.CustomPresets!.Count != 1 || reloaded.CustomPresets[0].Name != "My Test") throw new Exception("Custom preset roundtrip");
        // Built-in protected
        if (!PresetCatalog.IsBuiltIn("Balanced Bento")) throw new Exception("Built-in recognition");
        if (!ClayCustomization.IsBuiltIn("Rose Plush") || !ClayCustomization.IsBuiltIn("Coral sculpted") || ClayCustomization.BuiltIn.Count != 11) throw new Exception("Clay built-in recognition");
        if (PresetCatalog.IsBuiltIn("My Test")) throw new Exception("Custom not built-in");
        if (PresetCatalog.BuiltIn.Count != 5) throw new Exception("Five built-in presets required");
        // Corrupt handling — invalid enum is not a defined BentoAccent (fixture uses Indigo, not Ocean).
        var corrupt = json.Replace("\"Accent\": \"Indigo\"", "\"Accent\": 999");
        if (corrupt == json) throw new Exception("Corrupt fixture must rewrite Bento Accent");
        var fallback = System.Text.Json.JsonSerializer.Deserialize<SavedCustomization>(corrupt, new System.Text.Json.JsonSerializerOptions{ Converters={ new System.Text.Json.Serialization.JsonStringEnumConverter()}, PropertyNameCaseInsensitive=true});
        if (fallback!.Preferences.Validate() == null) throw new Exception("Corrupt should fail validation");
        // Reset produces defaults
        var defaults = new BentoCustomizationState();
        if (defaults.LayoutPreset != BentoLayoutPreset.Balanced) throw new Exception("Default preset");
        // Chart data not mutated by display options
        var ds = new ProGlassAutomation.Analytics.ChartDataset("T", ProGlassAutomation.Analytics.ChartKind.Columns, ["A"], [new ProGlassAutomation.Analytics.ChartSeriesData("S", [1])], "u", "summary");
        var vmChart = new ChartCustomizationViewModel();
        vmChart.SelectedDataset = ds;
        vmChart.ChartTitle = "Display only";
        vmChart.SelectedKind = ProGlassAutomation.Analytics.ChartKind.Lines;
        vmChart.ShowGrid = false;
        if (vmChart.PreviewDataset.Series[0].Values[0] != 1) throw new Exception("Chart source values unchanged");
        if (ds.Series[0].Values[0] != 1) throw new Exception("Chart source dataset mutated");
        Console.WriteLine("PASS: customization controls affect resources, save/load, built-in protection, corrupt fallback, chart isolation.");
    }
}
