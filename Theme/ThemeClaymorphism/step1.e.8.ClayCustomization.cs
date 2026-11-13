using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ProGlassAutomation.Theming;

namespace ProGlassAutomation.ThemeClaymorphism;

public enum ClayAccent { Rose, Berry, Sage }
public enum ClayVolume { Soft, Plush, Deep }
public enum ClayPlumpness { Modest, Standard, Puffy }
public enum ClayDensity { Compact, Comfortable }
public enum ClayCanvas { Pastel, Warm, Cool }
public enum ClayType { Small, Standard, Large }
public enum ClaySpacing { Compact, Comfortable, Spacious }
public enum ClayTextContrast { Standard, High }
public enum ClaySheen { Quiet, Standard, Gloss }
public enum ClayWeight { Regular, Medium, Semibold }
public enum ClayLighting { Directional, Balanced }
public enum ClayRelief { Raised, Inset }
public enum ClayEffects { Full, Reduced }
public enum ClayRecipe { Standard, Coral, Clayhub, PuffyStudio, DailyProgress, Clayboard, Clayshop, Clayplay }

public sealed record ClayCustomizationState(
    ClayAccent Accent = ClayAccent.Rose,
    ClayVolume Volume = ClayVolume.Plush,
    ClayPlumpness Plumpness = ClayPlumpness.Standard,
    ClayDensity Density = ClayDensity.Comfortable,
    ClayCanvas Canvas = ClayCanvas.Pastel,
    ClayType Typography = ClayType.Standard,
    ClaySpacing Spacing = ClaySpacing.Comfortable,
    ClayTextContrast TextContrast = ClayTextContrast.Standard,
    ClaySheen Sheen = ClaySheen.Standard,
    ClayWeight Weight = ClayWeight.Regular,
    ClayLighting Lighting = ClayLighting.Directional,
    ClayRelief Relief = ClayRelief.Raised,
    ClayEffects Effects = ClayEffects.Full,
    ClayRecipe Recipe = ClayRecipe.Standard)
{
    public string? Validate()
    {
        if (!Enum.IsDefined(Accent) || !Enum.IsDefined(Volume) || !Enum.IsDefined(Plumpness) ||
            !Enum.IsDefined(Density) || !Enum.IsDefined(Canvas) || !Enum.IsDefined(Typography) ||
            !Enum.IsDefined(Spacing) || !Enum.IsDefined(TextContrast) || !Enum.IsDefined(Sheen) ||
            !Enum.IsDefined(Weight) || !Enum.IsDefined(Lighting) || !Enum.IsDefined(Relief) ||
            !Enum.IsDefined(Effects) || !Enum.IsDefined(Recipe))
            return "Unsupported clay customization choice.";
        return null;
    }
}

public sealed record ClayNamedPreset(string Name, ClayCustomizationState State, Appearance Appearance);

/// <summary>Clay-owned customization. Never mutates Bento packages or Bento state.</summary>
public static class ClayCustomization
{
    public static IReadOnlyList<ClayNamedPreset> BuiltIn { get; } =
    [
        new("Rose Plush", new ClayCustomizationState(), Appearance.Light),
        new("Berry Puffy", new ClayCustomizationState(ClayAccent.Berry, ClayVolume.Deep, ClayPlumpness.Puffy, ClayDensity.Comfortable, ClayCanvas.Pastel, ClayType.Large, ClaySpacing.Spacious, ClayTextContrast.Standard, ClaySheen.Gloss), Appearance.Light),
        new("Sage Soft", new ClayCustomizationState(ClayAccent.Sage, ClayVolume.Soft, ClayPlumpness.Modest, ClayDensity.Compact, ClayCanvas.Cool, ClayType.Small, ClaySpacing.Compact, ClayTextContrast.High, ClaySheen.Quiet), Appearance.Light),
        new("Night Berry", new ClayCustomizationState(ClayAccent.Berry, ClayVolume.Deep, ClayPlumpness.Standard, ClayDensity.Comfortable, ClayCanvas.Pastel, ClayType.Standard, ClaySpacing.Comfortable, ClayTextContrast.High, ClaySheen.Gloss), Appearance.Dark),
        new("Coral sculpted", new ClayCustomizationState(Volume: ClayVolume.Deep, Sheen: ClaySheen.Gloss, Recipe: ClayRecipe.Coral), Appearance.Light),
        new("Clayhub", new ClayCustomizationState(Volume: ClayVolume.Plush, Recipe: ClayRecipe.Clayhub), Appearance.Light),
        new("Puffy studio", new ClayCustomizationState(Volume: ClayVolume.Deep, Plumpness: ClayPlumpness.Puffy, Sheen: ClaySheen.Gloss, Recipe: ClayRecipe.PuffyStudio), Appearance.Light),
        new("Daily progress", new ClayCustomizationState(Volume: ClayVolume.Deep, Plumpness: ClayPlumpness.Puffy, Sheen: ClaySheen.Gloss, Recipe: ClayRecipe.DailyProgress), Appearance.Light),
        new("Clayboard", new ClayCustomizationState(Volume: ClayVolume.Soft, Plumpness: ClayPlumpness.Modest, Recipe: ClayRecipe.Clayboard), Appearance.Light),
        new("Clayshop", new ClayCustomizationState(Recipe: ClayRecipe.Clayshop), Appearance.Light),
        new("Clayplay", new ClayCustomizationState(Recipe: ClayRecipe.Clayplay), Appearance.Light),
    ];

    public static bool IsBuiltIn(string name) => BuiltIn.Any(p => p.Name == name);

    public static void Apply(ResourceDictionary package, Appearance mode, ClayCustomizationState state)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(state);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (state.Validate() is not null) throw new ArgumentException(state.Validate());

        string[]? accent = (state.Accent, mode) switch
        {
            (ClayAccent.Berry, Appearance.Light) => ["#5B3CC4", "#4C30A8", "#3D268C", "#EDE4FF", "#FFFFFF"],
            (ClayAccent.Berry, Appearance.Dark) => ["#C4B5FF", "#D4C8FF", "#A898E8", "#2E2450", "#1A1730"],
            (ClayAccent.Sage, Appearance.Light) => ["#146C45", "#0F5738", "#0C452C", "#D4F5E4", "#FFFFFF"],
            (ClayAccent.Sage, Appearance.Dark) => ["#5EE0C0", "#7AE8CC", "#4BC8A8", "#1A3A30", "#1A1730"],
            _ => null
        };
        if (accent is not null)
        {
            string[] keys = ["AccentPrimary", "AccentHover", "AccentPressed", "AccentSoft", "TextOnAccent"];
            for (int i = 0; i < keys.Length; i++)
                package[keys[i]] = Brush(accent[i]);
            package["FocusIndicator"] = package["AccentPrimary"];
            package["StateSelected"] = package["AccentSoft"];
            package["ChartSeries1"] = package["AccentPrimary"];
        }

        var canvas = (state.Canvas, mode) switch
        {
            (ClayCanvas.Warm, Appearance.Light) => ("#F7EFE6", "#EDE3D6", "#F3E0CC"),
            (ClayCanvas.Cool, Appearance.Light) => ("#E8EEF9", "#DCE5F3", "#D4DFF0"),
            (ClayCanvas.Warm, Appearance.Dark) => ("#241820", "#1C1418", "#322028"),
            (ClayCanvas.Cool, Appearance.Dark) => ("#141A30", "#101428", "#1C2448"),
            _ => ((string?)null, (string?)null, (string?)null)
        };
        if (canvas.Item1 is not null)
        {
            package["ApplicationBackground"] = Brush(canvas.Item1);
            package["SurfaceSecondary"] = Brush(canvas.Item2!);
            package["SurfacePrimary"] = Brush(canvas.Item3!);
            package["SurfaceElevated"] = Brush(canvas.Item3!);
        }

        if (state.TextContrast == ClayTextContrast.High)
        {
            package["TextPrimary"] = Brush(mode == Appearance.Light ? "#10143A" : "#FFFFFF");
            package["TextMuted"] = Brush(mode == Appearance.Light ? "#3A4270" : "#D2CCEE");
        }

        double height = state.Density == ClayDensity.Compact ? 36d : 40d;
        double card = state.Plumpness switch { ClayPlumpness.Modest => 24d, ClayPlumpness.Puffy => 40d, _ => 36d };
        double pill = height / 2d;
        package["CardCornerRadius"] = new CornerRadius(card);
        package["ControlCornerRadius"] = new CornerRadius(pill);
        package["ChipCornerRadius"] = new CornerRadius(pill);
        package["ChoiceCornerRadius"] = new CornerRadius(Math.Max(11d, pill));
        package["ControlMinHeight"] = height;
        package["PaddingControl"] = state.Density == ClayDensity.Compact ? new Thickness(12, 8, 12, 8) : new Thickness(16, 10, 16, 10);

        double spacing = state.Spacing switch { ClaySpacing.Compact => 0.85, ClaySpacing.Spacious => 1.2, _ => 1 };
        package["SpacingSmall"] = 6 * spacing;
        package["SpacingMedium"] = 10 * spacing;
        package["SpacingLarge"] = 18 * spacing;
        package["SpacingSection"] = 28 * spacing;
        package["PaddingCard"] = new Thickness(state.Spacing == ClaySpacing.Compact ? 10 : state.Spacing == ClaySpacing.Spacious ? 16 : 12);
        package["PaddingPlate"] = new Thickness(state.Spacing == ClaySpacing.Compact ? 8 : state.Spacing == ClaySpacing.Spacious ? 14 : 10);
        package["BentoGutter"] = 20 * spacing;
        package["BentoRowHeight"] = (state.Density == ClayDensity.Compact ? 128d : 140d) * spacing;

        double size = state.Typography switch { ClayType.Small => 12d, ClayType.Large => 14d, _ => 13d };
        package["FontSizeBody"] = size;
        package["FontSizeCaption"] = 12d;
        package["FontSizeSection"] = size + 3;
        package["FontWeightHeading"] = state.Weight switch { ClayWeight.Medium => FontWeights.Medium, ClayWeight.Semibold => FontWeights.SemiBold, _ => FontWeights.Bold };
        package["FontWeightBody"] = state.Weight switch { ClayWeight.Medium => FontWeights.Medium, ClayWeight.Semibold => FontWeights.SemiBold, _ => FontWeights.Normal };

        string drop = mode == Appearance.Light
            ? state.Volume switch { ClayVolume.Soft => "33", ClayVolume.Deep => "99", _ => "66" }
            : state.Volume switch { ClayVolume.Soft => "66", ClayVolume.Deep => "CC", _ => "99" };
        if (mode == Appearance.Light)
        {
            string shade = state.Volume switch { ClayVolume.Soft => "22", ClayVolume.Deep => "55", _ => "33" };
            Tint(package, "ClayDropBrush", drop, "5B4A9A");
            Tint(package, "ClayInsetShadeBrush", shade, "5B4A9A");
            Tint(package, "ClayDropPositive", drop, "146C45");
            Tint(package, "ClayShadePositive", shade, "146C45");
            Tint(package, "ClayDropFeature", drop, "5B4A9A");
            Tint(package, "ClayShadeFeature", shade, "5B4A9A");
            Tint(package, "ClayDropInformation", drop, "2158A8");
            Tint(package, "ClayShadeInformation", shade, "2158A8");
            Tint(package, "ClayDropAttention", drop, "C27A00");
            Tint(package, "ClayShadeAttention", shade, "C27A00");
            Tint(package, "ClayDropAccent", drop, "C81E1E");
            Tint(package, "ClayShadeAccent", shade, "C81E1E");
            Tint(package, "ClayDropHero", drop, "7B5BB5");
            Tint(package, "ClayShadeHero", shade, "7B5BB5");
        }
        else
        {
            string darkShade = state.Volume switch { ClayVolume.Soft => "44", ClayVolume.Deep => "88", _ => "66" };
            Tint(package, "ClayDropBrush", drop, "140E0C");
            Tint(package, "ClayInsetShadeBrush", darkShade, "000000");
            Tint(package, "ClayDropPositive", drop, "1A3A30");
            Tint(package, "ClayShadePositive", darkShade, "1A3A30");
            Tint(package, "ClayDropFeature", drop, "2E2450");
            Tint(package, "ClayShadeFeature", darkShade, "2E2450");
            Tint(package, "ClayDropInformation", drop, "1E2A48");
            Tint(package, "ClayShadeInformation", darkShade, "1E2A48");
            Tint(package, "ClayDropAttention", drop, "3A2A20");
            Tint(package, "ClayShadeAttention", darkShade, "3A2A20");
            Tint(package, "ClayDropAccent", drop, "FF8A80");
            Tint(package, "ClayShadeAccent", darkShade, "FF8A80");
            Tint(package, "ClayDropHero", drop, "140E0C");
            Tint(package, "ClayShadeHero", darkShade, "140E0C");
        }

        SetOwned(package, "ClayInsetLightBrush", Brush(mode == Appearance.Light
            ? state.Sheen switch { ClaySheen.Quiet => "#99FFFFFF", ClaySheen.Gloss => "#FFFFFFFF", _ => "#F2FFFFFF" }
            : state.Sheen switch { ClaySheen.Quiet => "#44FFFFFF", ClaySheen.Gloss => "#CCFFFFFF", _ => "#99FFFFFF" }));

        bool reduced = state.Effects == ClayEffects.Reduced;
        bool inset = state.Relief == ClayRelief.Inset;
        bool balanced = state.Lighting == ClayLighting.Balanced;
        double near = state.Volume switch { ClayVolume.Soft => 2d, ClayVolume.Deep => 5d, _ => 3d };
        double far = state.Volume switch { ClayVolume.Soft => 3d, ClayVolume.Deep => 7d, _ => 4d };
        double blur = state.Volume switch { ClayVolume.Soft => 10d, ClayVolume.Deep => 20d, _ => 14d };
        double distance = state.Volume switch { ClayVolume.Soft => 3d, ClayVolume.Deep => 7d, _ => 5d };
        SetOwned(package, "ClayShowDrop", !reduced && !inset);
        SetOwned(package, "ClayDropMargin", reduced || inset ? new Thickness(0) : new Thickness(near, far, 0, 0));
        SetOwned(package, "ClayFaceMargin", reduced || inset ? new Thickness(0) : new Thickness(1));
        SetOwned(package, "ClayLiftMargin", reduced || inset ? new Thickness(0) : new Thickness(0, 0, near, far));
        SetOwned(package, "ClayDropFarMargin", reduced || inset ? new Thickness(0) : new Thickness(near + 3, far + 4, 0, 0));
        SetOwned(package, "ClayLiftDropMargin", new Thickness(near + 2, far + 3, 0, 0));
        SetOwned(package, "ClayLiftFaceMargin", new Thickness(0, 0, near + 2, far + 3));
        SetOwned(package, "ClayBlur", reduced ? 4d : blur);
        SetOwned(package, "ClayDistance", reduced || inset ? 0d : distance);
        SetOwned(package, "ClayLiftDistance", distance + 3d);
        SetOwned(package, "ClayRimWidth", reduced ? 3d : state.Volume switch { ClayVolume.Soft => 4d, ClayVolume.Deep => 8d, _ => 6d });
        SetOwned(package, "ClayGutter", reduced || inset ? new Thickness(2) : new Thickness(Math.Ceiling(distance + blur * 0.35)));
        string shadow = (mode, state.Canvas) switch
        {
            (Appearance.Light, ClayCanvas.Warm) => "#C4A890",
            (Appearance.Light, ClayCanvas.Cool) => "#8A9BB8",
            (Appearance.Light, _) => "#A89CBC",
            (Appearance.Dark, ClayCanvas.Cool) => "#0A1018",
            _ => "#140E0C"
        };
        string highlight = mode == Appearance.Light
            ? "#FFFFFF"
            : state.Canvas == ClayCanvas.Cool ? "#A8B8D0" : "#C4A990";
        SetOwned(package, "ClayShadowColor", (Color)ColorConverter.ConvertFromString(shadow));
        SetOwned(package, "ClayHighlightColor", (Color)ColorConverter.ConvertFromString(highlight));
        if (reduced)
        {
            SetOwned(package, "ClayShadeThickness", new Thickness(2));
            SetOwned(package, "ClayLightThickness", new Thickness(2));
            SetOwned(package, "ClayWellShadeThickness", new Thickness(2));
            SetOwned(package, "ClayWellLightThickness", new Thickness(2));
        }
        else if (inset)
        {
            SetOwned(package, "ClayShadeThickness", balanced ? new Thickness(4) : new Thickness(7, 7, 0, 0));
            SetOwned(package, "ClayLightThickness", balanced ? new Thickness(4) : new Thickness(0, 0, 6, 6));
            SetOwned(package, "ClayWellShadeThickness", balanced ? new Thickness(4) : new Thickness(8, 8, 0, 0));
            SetOwned(package, "ClayWellLightThickness", balanced ? new Thickness(4) : new Thickness(0, 0, 6, 6));
        }
        else
        {
            SetOwned(package, "ClayShadeThickness", balanced ? new Thickness(4, 4, 5, 5) : new Thickness(0, 0, 7, 7));
            SetOwned(package, "ClayLightThickness", balanced ? new Thickness(5, 5, 4, 4) : new Thickness(6, 6, 0, 0));
            SetOwned(package, "ClayWellShadeThickness", balanced ? new Thickness(5) : new Thickness(8, 8, 0, 0));
            SetOwned(package, "ClayWellLightThickness", balanced ? new Thickness(5) : new Thickness(0, 0, 6, 6));
        }

        double bevel = reduced ? 0d : state.Volume switch { ClayVolume.Soft => 8d, ClayVolume.Deep => 18d, _ => 12d };
        double controlBevel = reduced ? 0d : state.Volume switch { ClayVolume.Soft => 3d, ClayVolume.Deep => 6d, _ => 5d };
        double highlightAmt = mode == Appearance.Dark
            ? 0.22
            : state.Sheen switch { ClaySheen.Quiet => 0.40, ClaySheen.Gloss => 0.78, _ => 0.58 };
        double glossAmt = reduced ? 0d : state.Sheen switch { ClaySheen.Quiet => 0.12, ClaySheen.Gloss => 0.50, _ => 0.22 };
        double shadeAmt = mode == Appearance.Dark ? 0.35 : state.Volume switch { ClayVolume.Soft => 0.16, ClayVolume.Deep => 0.58, _ => 0.22 };
        SetOwned(package, "ClayLighting", balanced ? ClayLighting.Balanced : ClayLighting.Directional);
        SetOwned(package, "ClayInset", bevel);
        SetOwned(package, "ClayControlInset", controlBevel);
        SetOwned(package, "ClaySmallInset", Math.Min(controlBevel, 2d));
        SetOwned(package, "ClayHighlight", highlightAmt);
        SetOwned(package, "ClayShade", shadeAmt);
        SetOwned(package, "ClayGloss", glossAmt);
        SetOwned(package, "ClayGlossFocus", state.Sheen == ClaySheen.Gloss ? 24d : 28d);
        SetOwned(package, "ClayCurvature", state.Volume == ClayVolume.Deep ? 1.25d : 1.2d);
        SetOwned(package, "ClayLightElevation", 45d);
        SetOwned(package, "ClayLightAngle", 135d);
        SetOwned(package, "ClayRimOpacity", reduced ? 0d : 0.35d);
        SetOwned(package, "ClayBorder", reduced ? 0d : 1d);
        SetOwned(package, "ClayPressedShade", Math.Min(0.4, shadeAmt + 0.08));
        SetOwned(package, "ClaySurfaceShadowTint", 0d);
        SetOwned(package, "ClayShadeColor", (Color)ColorConverter.ConvertFromString(shadow));
        SetOwned(package, "ClayEdge", Brush(mode == Appearance.Light ? "#5A5478" : "#C8B8A8"));
        double opacity = mode == Appearance.Light ? 0.20 : 0.40;
        if (reduced)
        {
            SetOwned(package, "ClayAmbientShadow", Shadow(shadow, 0, 0, 0, 0));
            SetOwned(package, "ClayControlAmbientShadow", Shadow(shadow, 0, 0, 0, 0));
            SetOwned(package, "ClayShadow", Shadow(shadow, 0, 0, 0, 0));
            SetOwned(package, "ClayControlShadow", Shadow(shadow, 0, 0, 0, 0));
            SetOwned(package, "ClayLiftShadow", Shadow(shadow, 0, 0, 0, 0));
        }
        else
        {
            SetOwned(package, "ClayAmbientShadow", Shadow(shadow, Math.Min(72, blur * 1.7), 0, opacity * 0.4, 0));
            SetOwned(package, "ClayControlAmbientShadow", Shadow(shadow, Math.Min(20, blur * 1.3), 0, opacity * 0.32, 0));
            SetOwned(package, "ClayShadow", Shadow(shadow, blur, distance, opacity, 315));
            SetOwned(package, "ClayControlShadow", Shadow(shadow, Math.Min(blur, 10), Math.Min(distance, 2), opacity * 0.8, 315));
            SetOwned(package, "ClayLiftShadow", Shadow(shadow, blur + 4, distance + 2, opacity + 0.04, 315));
        }

        if (mode == Appearance.Light && state.Recipe != ClayRecipe.Standard)
            ApplyRecipe(package, state.Recipe);
    }

    private static void ApplyRecipe(ResourceDictionary package, ClayRecipe recipe)
    {
        switch (recipe)
        {
            case ClayRecipe.Coral:
                Paint(package, "#F4D0CF", "#F0B5B4", "#F4D0CF", "#F7E8E6", "#291E2C", "#50353D", "#B52E3B", "#A81818", "#8E1414", "#FFE8E4", "#914651", "#0C4A30", 28, 10);
                Material(package, 18, 6, 0.78, 0.58, 0.50, 24, 1.25, 40, 0.12, "#8E303D", 18, 4, 0.28);
                break;
            case ClayRecipe.Clayhub:
                Colorful(package, "#DED1FA", "#C9EFDE", "#FBE1CD", "#BFE2F8", 24, 11, 0.30, 0.35, 1.1, 3);
                break;
            case ClayRecipe.PuffyStudio:
                Colorful(package, "#D9BDF6", "#B8EFD9", "#FFD9C2", "#B8DDFB", 30, 18, 0.65, 0.44, 1.35, 5);
                break;
            case ClayRecipe.DailyProgress:
                Colorful(package, "#D1B6F4", "#B4E8CE", "#FFD4B3", "#C7DBF4", 32, 17, 0.48, 0.40, 1.2, 5);
                break;
            case ClayRecipe.Clayboard:
                Colorful(package, "#E7DDF7", "#D1F0E6", "#FCE5D0", "#B8DFFA", 18, 7, 0.12, 0.22, 1.0, 2);
                break;
            case ClayRecipe.Clayshop:
                Colorful(package, "#CCAFF0", "#A8DCC7", "#F5CBAF", "#AED8E9", 24, 10, 0.20, 0.36, 1.15, 3);
                break;
            case ClayRecipe.Clayplay:
                Colorful(package, "#C3AAF6", "#AEEDD7", "#FFD0B6", "#ACE0F4", 22, 12, 0.30, 0.37, 1.2, 3);
                break;
        }
    }

    private static void Colorful(ResourceDictionary package, string lavender, string mint, string peach, string blue,
        double radius, double bevel, double gloss, double shade, double curvature, double depth)
    {
        Paint(package, "#F6F6FA", "#F9F7FD", "#F6F6FA", "#F9F7FD", "#171B37", "#41465E", "#B52E3B", "#A81818", "#8E1414", "#FFE8E4", "#817398", "#0C4A30", radius, 10);
        package["SurfaceFeature"] = Brush(lavender);
        package["SurfacePositive"] = Brush(mint);
        package["SurfaceAttention"] = Brush(peach);
        package["SurfaceInformation"] = Brush(blue);
        package["HeroBackground"] = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(lavender), (Color)ColorConverter.ConvertFromString("#F9F7FD"), new Point(0, 0), new Point(1, 1));
        Material(package, bevel, bevel <= 7 ? 3d : 5d, Math.Min(0.78, 0.40 + gloss), shade, gloss, 30, curvature, 45, 0.14, "#675080", 16, depth, 0.25);
    }

    private static void Paint(ResourceDictionary package, string canvas, string surface, string secondary, string elevated,
        string text, string muted, string accent, string hover, string pressed, string soft, string edge, string heroAccent,
        double cardRadius, double controlRadius)
    {
        package["ApplicationBackground"] = Brush(canvas);
        package["SurfacePrimary"] = Brush(surface);
        package["SurfaceSecondary"] = Brush(secondary);
        package["SurfaceElevated"] = Brush(elevated);
        package["TextPrimary"] = Brush(text);
        package["TextSecondary"] = Brush(muted);
        package["TextMuted"] = Brush(muted);
        package["AccentPrimary"] = Brush(accent);
        package["AccentHover"] = Brush(hover);
        package["AccentPressed"] = Brush(pressed);
        package["AccentSoft"] = Brush(soft);
        package["TextOnAccent"] = Brush("#FFFFFF");
        package["FocusIndicator"] = package["AccentPrimary"];
        package["StateSelected"] = package["AccentSoft"];
        package["BorderSubtle"] = Brush(canvas);
        package["BorderStrong"] = Brush(edge);
        package["ChartAxis"] = Brush(muted);
        package["ChartSeries1"] = package["AccentPrimary"];
        package["HeroForeground"] = Brush(text);
        package["HeroSecondaryText"] = Brush(muted);
        package["HeroAccent"] = Brush(heroAccent);
        package["HeroBackground"] = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(canvas), (Color)ColorConverter.ConvertFromString(elevated), new Point(0, 0), new Point(1, 1));
        package["CardCornerRadius"] = new CornerRadius(cardRadius);
        package["ControlCornerRadius"] = new CornerRadius(controlRadius);
        package["ChipCornerRadius"] = new CornerRadius(controlRadius);
        package["ChoiceCornerRadius"] = new CornerRadius(Math.Max(11d, controlRadius));
        SetOwned(package, "ClayEdge", Brush(edge));
    }

    private static void Material(ResourceDictionary package, double inset, double controlInset, double highlight, double shade,
        double gloss, double glossFocus, double curvature, double elevation, double rim, string shadow, double blur, double depth, double opacity)
    {
        SetOwned(package, "ClayInset", inset);
        SetOwned(package, "ClayControlInset", controlInset);
        SetOwned(package, "ClaySmallInset", Math.Min(controlInset, 2d));
        SetOwned(package, "ClayHighlight", highlight);
        SetOwned(package, "ClayShade", shade);
        SetOwned(package, "ClayGloss", gloss);
        SetOwned(package, "ClayGlossFocus", glossFocus);
        SetOwned(package, "ClayCurvature", curvature);
        SetOwned(package, "ClayLightElevation", elevation);
        SetOwned(package, "ClayRimOpacity", rim);
        SetOwned(package, "ClayPressedShade", Math.Min(0.4, shade + 0.08));
        SetOwned(package, "ClayShadeColor", (Color)ColorConverter.ConvertFromString(shadow));
        SetOwned(package, "ClayShadowColor", (Color)ColorConverter.ConvertFromString(shadow));
        SetOwned(package, "ClayAmbientShadow", Shadow(shadow, Math.Min(72, blur * 1.7), 0, opacity * 0.4, 0));
        SetOwned(package, "ClayControlAmbientShadow", Shadow(shadow, Math.Min(20, blur * 1.3), 0, opacity * 0.32, 0));
        SetOwned(package, "ClayShadow", Shadow(shadow, blur, depth, opacity, 315));
        SetOwned(package, "ClayControlShadow", Shadow(shadow, Math.Min(blur, 10), Math.Min(depth, 2), opacity * 0.8, 315));
        SetOwned(package, "ClayLiftShadow", Shadow(shadow, blur + 4, depth + 2, opacity + 0.04, 315));
    }

    private static DropShadowEffect Shadow(string color, double blur, double depth, double opacity, double direction)
    {
        var effect = new DropShadowEffect
        {
            Color = (Color)ColorConverter.ConvertFromString(color),
            BlurRadius = blur,
            ShadowDepth = depth,
            Opacity = opacity,
            Direction = direction,
            RenderingBias = RenderingBias.Quality
        };
        effect.Freeze();
        return effect;
    }

    private static SolidColorBrush Brush(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));

    private static void Tint(ResourceDictionary package, string key, string alpha, string rgb) =>
        SetOwned(package, key, Brush($"#{alpha}{rgb}"));

    /// <summary>
    /// Replace a key in the dictionary that already owns it. Assigning extras onto the
    /// package root duplicates merged keys and Clay fails validation on switch.
    /// </summary>
    private static void SetOwned(ResourceDictionary package, string key, object value)
    {
        if (ReplaceOwn(package, key, value)) return;
        package[key] = value;
    }

    private static bool ReplaceOwn(ResourceDictionary dictionary, string key, object value)
    {
        foreach (object existing in dictionary.Keys)
        {
            if (!Equals(existing, key)) continue;
            dictionary[key] = value;
            return true;
        }
        foreach (ResourceDictionary child in dictionary.MergedDictionaries)
            if (ReplaceOwn(child, key, value)) return true;
        return false;
    }
}
