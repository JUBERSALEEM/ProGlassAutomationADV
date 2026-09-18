using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Services.Theme.Engine
{
    /// <summary>
    /// Authoritative ProGlass theme publisher. VisualThemeEngine still
    /// paints family-specific materials (Monet frost, Crystal water stage);
    /// this engine then fills the TokenStore and dual-writes compatibility
    /// keys so the rest of the app has one vocabulary.
    /// </summary>
    public static class ProGlassThemeEngine
    {
        public static ProGlassThemeFamily Family { get; private set; } = ProGlassThemeFamily.Industrial;
        public static ProGlassThemeMode Mode { get; private set; } = ProGlassThemeMode.Light;
        public static ProGlassMaterial Material { get; private set; } = ProGlassMaterial.Frosted;
        public static ProGlassDensity Density { get; private set; } = ProGlassDensity.Compact;

        public static ProGlassThemeFamily ParseFamily(string? persisted)
        {
            return persisted?.Trim() switch
            {
                VisualThemeEngine.Monet or "Monet" => ProGlassThemeFamily.Monet,
                VisualThemeEngine.CrystalFluid or "Ios-17pm" => ProGlassThemeFamily.Crystal,
                VisualThemeEngine.Ios26 or "iOS 26" => ProGlassThemeFamily.Ios26,
                _ => ProGlassThemeFamily.Industrial
            };
        }

        public static string PersistFamily(ProGlassThemeFamily family) => family switch
        {
            ProGlassThemeFamily.Monet => VisualThemeEngine.Monet,
            ProGlassThemeFamily.Crystal => VisualThemeEngine.CrystalFluid,
            ProGlassThemeFamily.Ios26 => VisualThemeEngine.Ios26,
            _ => VisualThemeEngine.NormalIndustrial
        };

        /// <summary>
        /// Glass|Solid|Transparent persist as Frosted|Solid|Soft.
        /// Crystal is an extra material, not a fourth family.
        /// </summary>
        public static ProGlassMaterial ParseMaterial(string? mode) => mode?.Trim() switch
        {
            "Solid" => ProGlassMaterial.Solid,
            "Transparent" or "Soft" => ProGlassMaterial.Soft,
            "Crystal" => ProGlassMaterial.Frosted,
            "Glass" or "Frosted" => ProGlassMaterial.Frosted,
            _ => ProGlassMaterial.Frosted
        };

        public static string PersistMaterial(ProGlassMaterial material) => material switch
        {
            ProGlassMaterial.Solid => "Solid",
            ProGlassMaterial.Soft => "Transparent",
            ProGlassMaterial.Crystal => "Crystal",
            _ => "Glass"
        };

        public static ProGlassDensity ParseDensity(string? density) => density?.Trim() switch
        {
            "Normal" => ProGlassDensity.Normal,
            "Comfortable" => ProGlassDensity.Comfortable,
            "Spacious" => ProGlassDensity.Spacious,
            _ => ProGlassDensity.Compact
        };

        /// <summary>Read current appearance + Application resources; write TokenStore.</summary>
        public static void Publish()
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return;

            try
            {
                var appearance = App.Config?.Appearance;
                Family = ParseFamily(appearance?.VisualTheme);
                Mode = ThemeManager.CurrentTheme == AppTheme.Dark
                    ? ProGlassThemeMode.Dark
                    : ProGlassThemeMode.Light;
                Material = ParseMaterial(appearance?.SurfaceMaterialMode);
                Density = ParseDensity(appearance?.DisplayDensity);

                foreach (var (semantic, source, fallback) in ThemeCompatibilityHub.SemanticSources)
                    CopyBrush(resources, semantic, source, fallback);

                EnsurePaletteFallbacks(resources);

                // Semantic alias layer (SemanticTokens / LayoutTokens /
                // GlobalDesignSystem compatibility keys). Their XAML entries
                // hold CONCRETE fallback values — never ResourceReference
                // markers — and this keeps those aliases live-re-tinted on
                // every family / mode / accent change.
                DualWriteSemanticAliases(resources);

                resources["ProGlass.Family"] = Family.ToString();
                resources["ProGlass.Mode"] = Mode.ToString();
                resources["ProGlass.Material"] = Material.ToString();
                resources["ProGlass.Density"] = Density.ToString();

                ApplyMaterialTokens(resources);
                DualWriteLegacy(resources);

                bool reduced = appearance?.ReducedMotion == true;
                resources["ReducedMotion"] = reduced;
                if (reduced)
                    resources["MotionSpeedRatio"] = 0.0;
                else
                    resources["MotionSpeedRatio"] = 1.0;

                resources["CrystalWaterDefaultEnabled"] = false;
                resources["CrystalAmbientHostVisibility"] = Visibility.Collapsed;
                resources["CrystalPreviewHostVisibility"] = Visibility.Collapsed;
            }
            catch (System.Exception ex)
            {
                AppLogService.Debug("[ProGlassThemeEngine] Publish degraded: " + ex.Message);
            }
        }

        /// <summary>
        /// Material tokens only. Family does not force Glass; Industrial
        /// + Crystal material is legal; Crystal + Solid is opaque, no water.
        /// </summary>
        private static void ApplyMaterialTokens(ResourceDictionary resources)
        {
            double fill = Material switch
            {
                ProGlassMaterial.Solid => 1.0,
                ProGlassMaterial.Soft => 0.72,
                ProGlassMaterial.Crystal => 0.42,
                _ => 0.88
            };
            resources["ProGlassMaterial.FillOpacity"] = fill;
            resources["ProGlassMaterial.Name"] = Material.ToString();

            if (Material != ProGlassMaterial.Solid) return;

            // Solid wins over family frost: copy opaque elevated fill.
            CopyBrush(resources, "SurfacePrimaryBrush", "ElevatedSurfaceBrush", "WindowBackgroundBrush");
            CopyBrush(resources, "SurfaceSecondaryBrush", "WindowBackgroundBrush", "ElevatedSurfaceBrush");
        }

        /// <summary>
        /// New owns old: leftover Glass/Liquid DynamicResource keys share
        /// the TokenStore brush instance so they track family/mode without
        /// a MergedDictionary swap.
        /// </summary>
        private static void DualWriteLegacy(ResourceDictionary resources)
        {
            foreach (var pair in ThemeCompatibilityHub.LegacyToSemantic)
            {
                var semantic = TryBrush(resources, pair.Value);
                if (semantic == null) continue;
                resources[pair.Key] = semantic;
            }
        }

        private static Brush? TryBrush(ResourceDictionary resources, string key)
        {
            try
            {
                if (resources.Contains(key) && resources[key] is Brush b)
                    return b;
            }
            catch (System.Exception ex)
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug($"[foreach] suppressed: {ex.Message}");
            }
            return null;
        }

        private static void CopyBrush(
            ResourceDictionary resources, string target, string source, string fallback)
        {
            Brush? live = TryBrush(resources, source) ?? TryBrush(resources, fallback);
            if (live == null) return;

            var clone = live.CloneCurrentValue();
            if (clone.CanFreeze) clone.Freeze();
            resources[target] = clone;
        }

        /// <summary>
        /// Live dual-writes for the semantic alias layer. The alias XAML
        /// files (Themes/Styles/SemanticTokens.xaml,
        /// Themes/ProGlassDesignSystem/Compatibility/LegacyAliases.xaml and
        /// the compatibility section of GlobalDesignSystem.xaml) store
        /// concrete fallback values — a &lt;DynamicResource x:Key=...&gt; entry
        /// would instead store a ResourceReferenceExpression marker as the
        /// resource value and crash the first property assignment after the
        /// splash. This method re-points those aliases at the live
        /// engine-managed palette on every Publish so light/dark and family
        /// switches re-tint every semantic consumer.
        /// </summary>
        private static void DualWriteSemanticAliases(ResourceDictionary resources)
        {
            CopyBrush(resources, "SurfaceWindowBrush", "WindowBackgroundBrush", "WindowBackgroundBrush");
            CopyBrush(resources, "SurfaceWorkspaceBrush", "ModuleBackgroundBrush", "SurfacePrimaryBrush");
            CopyBrush(resources, "SurfaceCardBrush", "GlassUltraClearBrush", "SurfacePrimaryBrush");
            CopyBrush(resources, "SurfaceElevatedBrush", "ElevatedSurfaceBrush", "WindowBackgroundBrush");
            CopyBrush(resources, "SurfacePopupBrush", "PopupSurfaceBrush", "ElevatedSurfaceBrush");
            CopyBrush(resources, "SurfaceSidebarBrush", "SideMenuBackgroundBrush", "WindowBackgroundBrush");
            CopyBrush(resources, "TextMutedBrush", "MutedForegroundBrush", "TextTertiaryBrush");
            CopyBrush(resources, "FocusBrush", "LiquidFocusBrush", "BorderFocusBrush");

            // Radius hierarchy follows the user-set card radius (published by
            // ThemeManager.ApplyCardCornerRadius before this runs at startup).
            CopyCorner(resources, "RadiusNone", "Radius.None", 0);
            CopyCorner(resources, "RadiusCompact", "Radius.Small", 6);
            CopyCorner(resources, "RadiusControl", "ControlCornerRadius", 8);
            CopyCorner(resources, "RadiusCard", "CardCornerRadius", 12);
            CopyCorner(resources, "RadiusLarge", "CardCornerRadiusLarge", 16);
            CopyCorner(resources, "RadiusDialog", "CardCornerRadiusLarge", 16);
        }

        private static void CopyCorner(
            ResourceDictionary resources, string target, string source, double fallback)
        {
            if (resources.Contains(source) && resources[source] is CornerRadius corner)
                resources[target] = corner;
            else
                resources[target] = new CornerRadius(fallback);
        }

        private static void EnsurePaletteFallbacks(ResourceDictionary resources)
        {
            var p = ProGlassPalettes.Resolve(Family, Mode);
            EnsureSolid(resources, "SurfacePrimaryBrush", p.Surface);
            EnsureSolid(resources, "SurfaceSecondaryBrush", p.Elevated);
            EnsureSolid(resources, "SurfaceTertiaryBrush", p.Elevated);
            EnsureSolid(resources, "SurfaceOverlayBrush", p.Elevated);
            EnsureSolid(resources, "SurfaceInputBrush", p.Input);
            EnsureSolid(resources, "TextPrimaryBrush", p.Text);
            EnsureSolid(resources, "TextSecondaryBrush", p.TextMuted);
            EnsureSolid(resources, "TextTertiaryBrush", p.TextMuted);
            EnsureSolid(resources, "BorderSubtleBrush", p.Border);
            EnsureSolid(resources, "BorderStrongBrush", p.Border);
            EnsureSolid(resources, "BorderDefaultBrush", p.Border);
            EnsureSolid(resources, "InputBorderBrush", p.Border);
            EnsureSolid(resources, "AccentBrush", p.Accent);
            EnsureSolid(resources, "WindowBackgroundBrush", p.Window);
            EnsureSolid(resources, "ModuleBackgroundBrush", p.Surface);
            EnsureSolid(resources, "ElevatedSurfaceBrush", p.Elevated);
            EnsureSolid(resources, "OutlineBrush", p.Border);
            EnsureSolid(resources, "SuccessSoftBrush", Color.FromArgb(0x22, 0x16, 0xA3, 0x4A));
            EnsureSolid(resources, "WarningSoftBrush", Color.FromArgb(0x22, 0xD9, 0x77, 0x06));
            EnsureSolid(resources, "DangerSoftBrush", Color.FromArgb(0x22, 0xB9, 0x1C, 0x1C));
            EnsureSolid(resources, "InfoSoftBrush", Color.FromArgb(0x22, 0x02, 0x84, 0xC7));
        }

        private static void EnsureSolid(ResourceDictionary resources, string key, Color color)
        {
            if (TryBrush(resources, key) != null) return;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[key] = brush;
        }

        /// <summary>
        /// Exports the active corporate theme pack as a serialized JSON profile.
        /// </summary>
        public static string ExportThemeProfile()
        {
            var p = new ProGlassThemeProfile
            {
                Family = Family.ToString(),
                Mode = Mode.ToString(),
                Material = Material.ToString(),
                Density = Density.ToString(),
                AccentColorHex = ThemeManager.AccentColor.ToString(),
                CardCornerRadius = ThemeManager.CardCornerRadius,
                CardEdgeThickness = ThemeManager.CardEdgeThickness,
                UseGradients = ThemeManager.UseGradients,
                UseDefinedOutline = ThemeManager.UseDefinedOutline
            };
            return System.Text.Json.JsonSerializer.Serialize(p, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Imports and applies a corporate theme pack JSON profile.
        /// </summary>
        public static bool ImportThemeProfile(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return false;
                var p = System.Text.Json.JsonSerializer.Deserialize<ProGlassThemeProfile>(json);
                if (p == null) return false;

                if (!string.IsNullOrWhiteSpace(p.Family))
                {
                    var familyConst = PersistFamily(ParseFamily(p.Family));
                    VisualThemeEngine.Apply(familyConst);
                }

                if (!string.IsNullOrWhiteSpace(p.Mode))
                {
                    bool isDark = p.Mode.Equals("Dark", System.StringComparison.OrdinalIgnoreCase);
                    ThemePipeline.Rebuild(isDark, "Theme profile import");
                }

                if (!string.IsNullOrWhiteSpace(p.AccentColorHex))
                {
                    var color = (Color)ColorConverter.ConvertFromString(p.AccentColorHex);
                    ThemeManager.ApplyAccent(color);
                }

                if (p.CardCornerRadius > 0)
                    ThemeManager.ApplyCardCornerRadius(p.CardCornerRadius);

                if (!string.IsNullOrWhiteSpace(p.Density))
                    ThemeManager.ApplyDisplayDensity(p.Density);

                ThemeManager.ApplyGradientPreference(p.UseGradients);
                ThemeManager.ApplyOutline(p.UseDefinedOutline);

                return true;
            }
            catch (System.Exception ex)
            {
                AppLogService.Warning($"[ThemeProfile] Import failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>Corporate brand theme pack representation.</summary>
    public sealed class ProGlassThemeProfile
    {
        public string Family { get; set; } = "Industrial";
        public string Mode { get; set; } = "Light";
        public string Material { get; set; } = "Frosted";
        public string Density { get; set; } = "Compact";
        public string AccentColorHex { get; set; } = "#0369A1";
        public double CardCornerRadius { get; set; } = 12.0;
        public double CardEdgeThickness { get; set; } = 1.0;
        public bool UseGradients { get; set; } = false;
        public bool UseDefinedOutline { get; set; } = true;
    }
}
