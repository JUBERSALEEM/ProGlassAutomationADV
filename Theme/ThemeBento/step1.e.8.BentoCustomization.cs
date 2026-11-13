using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Customization;
using ProGlassAutomation.Theming;

namespace ProGlassAutomation.ThemeBento;

public enum BentoAccent { Ocean, Indigo, Teal }
public enum BentoDensity { Compact, Comfortable, Spacious }
public enum BentoShape { Crisp, Soft, Rounded, Pill }
public enum BentoType { Small, Standard, Large }
public sealed record BentoPreferences(BentoAccent Accent = BentoAccent.Ocean,
    BentoDensity Density = BentoDensity.Compact, BentoShape Shape = BentoShape.Rounded,
    BentoType Typography = BentoType.Standard);

/// <summary>Curated, theme-owned customization. Backward compat shim delegates to extended model.</summary>
public static class BentoCustomization
{
    public static void Apply(ResourceDictionary package, Appearance mode, BentoPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(preferences);
        var state = new BentoCustomizationState(preferences.Accent, preferences.Density, preferences.Shape, preferences.Typography);
        ApplyExtended(package, mode, state);
    }
    public static void ApplyExtended(ResourceDictionary package, Appearance mode, BentoCustomizationState state)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(state);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (state.Validate() is not null) throw new ArgumentException(state.Validate());
        // Accent — curated, validated pairs
        string[]? accent = (state.Accent, mode) switch
        {
            (BentoAccent.Indigo, Appearance.Light) => ["#5440C8", "#4632B5", "#382690", "#EDE9FF", "#FFFFFF"],
            (BentoAccent.Indigo, Appearance.Dark) => ["#B5A5FF", "#C8BCFF", "#A08FE8", "#2A2448", "#191331"],
            (BentoAccent.Teal, Appearance.Light) => ["#006B61", "#00594F", "#00473F", "#E1F4EF", "#FFFFFF"],
            (BentoAccent.Teal, Appearance.Dark) => ["#75DFCD", "#99EDDE", "#62C9B7", "#173F3B", "#092923"],
            _ => null
        };
        if (accent is not null)
        {
            string[] keys = ["AccentPrimary", "AccentHover", "AccentPressed", "AccentSoft", "TextOnAccent"];
            for (int i = 0; i < keys.Length; i++) package[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accent[i]));
            package["FocusIndicator"] = package["AccentPrimary"];
            package["StateSelected"] = package["AccentSoft"];
        }
        // Canvas — tinted background variants, all validated to keep 4.5:1 on text
        string? canvas = (state.Canvas, mode) switch
        {
            (BentoCanvas.Warm, Appearance.Light) => "#F5EFE6",
            (BentoCanvas.Cool, Appearance.Light) => "#E6EEF9",
            (BentoCanvas.Warm, Appearance.Dark) => "#141B2E",
            (BentoCanvas.Cool, Appearance.Dark) => "#0B1425",
            _ => null
        };
        if (canvas is not null) package["ApplicationBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(canvas));
        // Surface style
        if (state.SurfaceStyle == BentoSurfaceStyle.Elevated)
        {
            if (mode == Appearance.Light) { package["SurfacePrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")); package["SurfaceSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF3FB")); package["SurfaceElevated"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")); }
            else { package["SurfacePrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2E4A")); package["SurfaceSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#253A5C")); package["SurfaceElevated"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A4266")); }
        }
        else if (state.SurfaceStyle == BentoSurfaceStyle.Contrast)
        {
            if (mode == Appearance.Light) { package["SurfacePrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")); package["SurfaceSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9EEF6")); package["SurfaceElevated"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFF")); }
            else { package["SurfacePrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16223B")); package["SurfaceSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1C2D4D")); package["SurfaceElevated"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22365B")); }
        }
        // Text contrast — High sharpens primary text subtly
        if (state.TextContrast == BentoTextContrast.High)
        {
            package["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(mode == Appearance.Light ? "#0F1D33" : "#FFFFFF"));
            package["TextMuted"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(mode == Appearance.Light ? "#3E4E68" : "#C8D6EE"));
        }
        // Border intensity
        if (state.BorderIntensity == BentoBorderIntensity.Strong)
            package["BorderSubtle"] = package["BorderStrong"];
        else if (state.BorderIntensity == BentoBorderIntensity.Hidden)
            package["BorderSubtle"] = new SolidColorBrush(Colors.Transparent);
        // Shadow intensity maps to border thickness/elevation (no blur)
        package["BorderThicknessControl"] = state.Shadow switch { BentoShadowIntensity.None => new Thickness(0), BentoShadowIntensity.Elevated => new Thickness(1.2), _ => new Thickness(1) };
        bool compact = state.Density == BentoDensity.Compact;
        package["ControlMinHeight"] = state.Density switch { BentoDensity.Compact => 32d, BentoDensity.Comfortable => 38d, _ => 44d };
        double spacingFactor = state.Spacing switch { BentoSpacing.Compact => 0.85, BentoSpacing.Spacious => 1.25, _ => 1.0 };
        package["SpacingSmall"] = 4 * spacingFactor;
        package["SpacingMedium"] = 8 * spacingFactor;
        package["SpacingLarge"] = 16 * spacingFactor;
        package["SpacingSection"] = 24 * spacingFactor;
        package["PaddingControl"] = compact ? new Thickness(10, 5, 10, 5) : new Thickness(14, 8, 14, 8);
        double pad = state.CardPadding switch { BentoCardPadding.Compact => 10, BentoCardPadding.Spacious => 20, _ => 16 };
        package["PaddingCard"] = new Thickness(pad);
        package["PaddingPlate"] = new Thickness(state.CardPadding == BentoCardPadding.Compact ? 8 : state.CardPadding == BentoCardPadding.Spacious ? 16 : 12);
        package["PaddingSelection"] = compact ? new Thickness(10, 5, 32, 5) : new Thickness(14, 8, 38, 8);
        double size = state.Typography switch { BentoType.Small => 12d, BentoType.Large => 14d, _ => 13d };
        package["FontSizeBody"] = size;
        package["FontSizeCaption"] = Math.Max(11d, size - 1);
        package["FontSizeSection"] = size + 4;
        package["FontSizeTitle"] = size + 7;
        package["FontSizeDisplay"] = size + 15;
        package["FontWeightHeading"] = state.FontWeight switch { BentoFontWeight.Medium => FontWeights.Medium, BentoFontWeight.Semibold => FontWeights.Bold, _ => FontWeights.SemiBold };
        package["FontWeightBody"] = state.FontWeight switch { BentoFontWeight.Medium => FontWeights.Medium, BentoFontWeight.Semibold => FontWeights.SemiBold, _ => FontWeights.Normal };
        double radius = SharedGeometry.CardRadius(SharedGeometry.FromShape(state.Shape));
        if (state.LayoutPreset == BentoLayoutPreset.CompactDense) radius = Math.Max(6, radius - 2);
        package["CardCornerRadius"] = new CornerRadius(radius);
        package["ControlCornerRadius"] = new CornerRadius(radius / 2);
        package["BentoGutter"] = Math.Max(8, 16 * spacingFactor);
        double row = state.Density switch { BentoDensity.Compact => 152d, BentoDensity.Comfortable => 176d, _ => 200d };
        if (state.LayoutPreset == BentoLayoutPreset.CompactDense) row = Math.Max(132d, row - 16);
        package["BentoRowHeight"] = row;
        package["ChartPlotHeight"] = state.Density switch { BentoDensity.Compact => 140d, BentoDensity.Comfortable => 168d, _ => 196d };
        package["PaddingPage"] = compact ? new Thickness(12) : new Thickness(16);
    }
}
