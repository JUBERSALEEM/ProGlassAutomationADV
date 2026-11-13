using System.Windows;
using System.Windows.Media;
using System.ComponentModel;
using ProGlassAutomation.Theming;
using ProGlassAutomation.ThemeBento;
using ProGlassAutomation.ThemeClaymorphism;
using ProGlassAutomation.ThemeLiquidGlass;
using ProGlassAutomation.ThemeNeumorphism;
using ProGlassAutomation.Customization;

namespace ProGlassAutomation.ThemeTests;

internal static class CustomizationChecks
{
    public static void Run()
    {
        int combinations = 0;
        foreach (var mode in Enum.GetValues<Appearance>())
        foreach (var accent in Enum.GetValues<BentoAccent>())
        foreach (var density in Enum.GetValues<BentoDensity>())
        foreach (var shape in Enum.GetValues<BentoShape>())
        foreach (var type in Enum.GetValues<BentoType>())
        {
            var preferences = new BentoPreferences(accent, density, shape, type);
            var package = ThemeService.Load(mode, configure: (candidate, appearance) => BentoCustomization.Apply(candidate, appearance, preferences));
            ResourceValidation.Validate(package);
            ContractChecks.Contrast(package, "TextOnAccent", "AccentPrimary", 4.5);
            ContractChecks.Contrast(package, "AccentPrimary", "AccentSoft", 4.5);
            if (package[typeof(ProGlassAutomation.Analytics.BentoChart)] is not Style chartStyle || !chartStyle.IsSealed)
                throw new Exception("Chart adapter style must be sealed.");
            combinations++;
        }
        var resources = new ResourceDictionary();
        var service = new ThemeService();
        service.Initialize(resources);
        service.Apply(ThemeService.Bento, Appearance.Light,
            (candidate, appearance) => BentoCustomization.Apply(candidate, appearance, new BentoPreferences(BentoAccent.Teal)));
        service.Apply(ThemeService.Bento, Appearance.Dark);
        if (((SolidColorBrush)service.GetToken("AccentPrimary")).Color != (Color)ColorConverter.ConvertFromString("#75DFCD"))
            throw new Exception("Mode changes discarded the active theme customization.");
        object before = service.GetToken("SurfacePrimary");
        ContractChecks.ExpectFailure(() => service.Apply(ThemeService.Bento, Appearance.Light,
            (candidate, _) => candidate["SpacingMedium"] = double.NaN), "invalid customization");
        if (service.ActiveAppearance != Appearance.Dark || !ReferenceEquals(before, service.GetToken("SurfacePrimary")))
            throw new Exception("Rejected customization changed the active package.");
        ContractChecks.ExpectFailure(() => ThemeService.Load(Appearance.Light, configure: (candidate, appearance) =>
            BentoCustomization.Apply(candidate, appearance, new BentoPreferences((BentoAccent)999))), "invalid preset");
        int notified = 0;
        PropertyChangedEventHandler failing = (_, _) => throw new InvalidOperationException("Deliberate consumer failure");
        service.PropertyChanged += failing;
        service.PropertyChanged += (_, _) => notified++;
        try { service.Apply(ThemeService.Bento, Appearance.Light); throw new Exception("Notification failure was not reported."); }
        catch (ThemeNotificationException)
        {
            if (service.ActiveAppearance != Appearance.Light || notified != 2)
                throw new Exception("Notification failure hid activation or prevented other consumers from refreshing.");
        }
        finally { service.PropertyChanged -= failing; }
        int clayCombinations = 0;
        foreach (var mode in Enum.GetValues<Appearance>())
        foreach (var accent in Enum.GetValues<ClayAccent>())
        foreach (var volume in Enum.GetValues<ClayVolume>())
        {
            var clay = new ClayCustomizationState(accent, volume);
            var package = ThemeService.Load(mode, ThemeService.Clay, (candidate, appearance) => ClayCustomization.Apply(candidate, appearance, clay));
            ResourceValidation.Validate(package);
            ContractChecks.Contrast(package, "TextOnAccent", "AccentPrimary", 4.5);
            ContractChecks.Contrast(package, "AccentPrimary", "AccentSoft", 4.5);
            clayCombinations++;
        }
        var clayFull = new ClayCustomizationState(ClayAccent.Sage, ClayVolume.Deep, ClayPlumpness.Puffy, ClayDensity.Compact, ClayCanvas.Cool, ClayType.Large, ClaySpacing.Spacious, ClayTextContrast.High, ClaySheen.Gloss, ClayWeight.Semibold);
        var clayPackage = ThemeService.Load(Appearance.Light, ThemeService.Clay, (candidate, appearance) => ClayCustomization.Apply(candidate, appearance, clayFull));
        ResourceValidation.Validate(clayPackage);
        if ((double)clayPackage["FontSizeBody"] != 14) throw new Exception("Clay large type");
        if (((CornerRadius)clayPackage["CardCornerRadius"]).TopLeft != 40) throw new Exception("Clay puffy radius");
        if ((double)clayPackage["ControlMinHeight"] != 36) throw new Exception("Clay compact height");
        if ((double)clayPackage["SpacingMedium"] != 12) throw new Exception("Clay spacious spacing");
        foreach (object key in clayPackage.Keys)
            if (Equals(key, "ClayDropBrush") || Equals(key, "ClayInsetLightBrush"))
                throw new Exception("Clay extras must stay in the extras dictionary, not the package root.");
        if (((SolidColorBrush)clayPackage["ApplicationBackground"]).Color != (Color)ColorConverter.ConvertFromString("#E8EEF9")) throw new Exception("Clay cool canvas");
        if (((SolidColorBrush)clayPackage["TextPrimary"]).Color != (Color)ColorConverter.ConvertFromString("#10143A")) throw new Exception("Clay high text");
        ContractChecks.Contrast(clayPackage, "TextPrimary", "ApplicationBackground", 4.5);
        if (ClayCustomization.BuiltIn.Count != 11) throw new Exception("Eleven clay built-in presets required");
        ContractChecks.ExpectFailure(() => ThemeService.Load(Appearance.Light, ThemeService.Clay, (candidate, appearance) =>
            ClayCustomization.Apply(candidate, appearance, new ClayCustomizationState((ClayAccent)999))), "invalid clay preset");
        if (ThemeCatalog.Find(ThemeService.Clay).Geometry != ThemeGeometry.ThemeOwned) throw new Exception("Clay owns radius");
        if (ThemeCatalog.Find(ThemeService.Fluent).Geometry != ThemeGeometry.SharedTokens) throw new Exception("Fluent accepts shared geometry");
        if (ThemeCatalog.Find(ThemeService.Neumorphism).Geometry != ThemeGeometry.SharedTokens) throw new Exception("Neumorphism accepts shared geometry");
        if (ThemeCatalog.Find(ThemeService.LiquidGlass).Geometry != ThemeGeometry.SharedTokens) throw new Exception("Liquid Glass accepts shared geometry");
        var fluentRounded = ThemeService.Load(Appearance.Light, ThemeService.Fluent, SharedGeometry.Compose(ThemeService.Fluent, new BentoCustomizationState(), new ClayCustomizationState(), new SharedGeometryState(CardRounding.Pill)));
        ResourceValidation.Validate(fluentRounded);
        if (((CornerRadius)fluentRounded["CardCornerRadius"]).TopLeft != 26) throw new Exception("Fluent shared pill rounding");
        var neuRounded = ThemeService.Load(Appearance.Light, ThemeService.Neumorphism, SharedGeometry.Compose(ThemeService.Neumorphism, new BentoCustomizationState(), new ClayCustomizationState(), new SharedGeometryState(CardRounding.Pill)));
        ResourceValidation.Validate(neuRounded);
        if (((CornerRadius)neuRounded["CardCornerRadius"]).TopLeft != 26) throw new Exception("Neumorphism shared pill rounding");
        var neuDeep = ThemeService.Load(Appearance.Light, ThemeService.Neumorphism, SharedGeometry.Compose(ThemeService.Neumorphism, new BentoCustomizationState(), new ClayCustomizationState(), new SharedGeometryState(), new NeumorphCustomizationState(NeumorphDepth.Deep, NeumorphBlur.Diffuse, NeumorphIntensity.Strong)));
        ResourceValidation.Validate(neuDeep);
        if ((double)neuDeep["NeumorphDistance"] != 10d) throw new Exception("Neumorph deep distance");
        if ((double)neuDeep["NeumorphBlur"] != 24d) throw new Exception("Neumorph diffuse blur");
        foreach (object key in neuDeep.Keys)
            if (Equals(key, "NeumorphDistance") || Equals(key, "NeumorphBlur"))
                throw new Exception("Neumorph extras must stay in the extras dictionary, not the package root.");
        if (NeumorphCustomization.BuiltIn.Count != 4) throw new Exception("Four neumorph built-in presets required");
        ContractChecks.ExpectFailure(() => ThemeService.Load(Appearance.Light, ThemeService.Neumorphism, (candidate, appearance) =>
            NeumorphCustomization.Apply(candidate, appearance, new NeumorphCustomizationState((NeumorphDepth)999))), "invalid neumorph preset");
        var liquidRounded = ThemeService.Load(Appearance.Light, ThemeService.LiquidGlass, SharedGeometry.Compose(ThemeService.LiquidGlass, new BentoCustomizationState(), new ClayCustomizationState(), new SharedGeometryState(CardRounding.Pill)));
        ResourceValidation.Validate(liquidRounded);
        if (((CornerRadius)liquidRounded["CardCornerRadius"]).TopLeft != 26) throw new Exception("Liquid Glass shared pill rounding");
        var liquidClear = ThemeService.Load(Appearance.Light, ThemeService.LiquidGlass, SharedGeometry.Compose(ThemeService.LiquidGlass, new BentoCustomizationState(), new ClayCustomizationState(), new SharedGeometryState(), null, null, null, new LiquidCustomizationState(LiquidFrost.Clear, LiquidSpecular.Bright, LiquidTint.Cool)));
        ResourceValidation.Validate(liquidClear);
        if ((double)liquidClear["LiquidFrostOpacity"] != 0.22d) throw new Exception("Liquid clear frost");
        foreach (object key in liquidClear.Keys)
            if (Equals(key, "LiquidFrostOpacity") || Equals(key, "LiquidFaceBrush"))
                throw new Exception("Liquid extras must stay in the extras dictionary, not the package root.");
        if (LiquidCustomization.BuiltIn.Count != 4) throw new Exception("Four liquid built-in presets required");
        ContractChecks.ExpectFailure(() => ThemeService.Load(Appearance.Light, ThemeService.LiquidGlass, (candidate, appearance) =>
            LiquidCustomization.Apply(candidate, appearance, new LiquidCustomizationState((LiquidFrost)999))), "invalid liquid preset");
        Console.WriteLine($"PASS: {combinations} customization/mode combinations, {clayCombinations} clay combinations, immutable helpers, preference retention, rejected candidates and notification-failure semantics.");
    }
}
