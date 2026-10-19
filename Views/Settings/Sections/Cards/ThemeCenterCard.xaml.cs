// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/Cards/ThemeCenterCard.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Theme Center — one selectable appearance family
//
// WHAT THIS CLASS ACTUALLY DOES
// ───────────────────────────────────────────────────────────────────────
// It paints the miniature from the family's OWN recipe. That is the whole
// design: the preview and the application read the same EightThemeProfile,
// so a family cannot look one way in the picker and another way in the
// app. Every one of the eight has a way to be wrong — a Swiss card with a
// drop shadow, a clay card at 16px radius, a neumorphic card whose fill
// drifted off its page colour — and in all three cases the card shows the
// same wrong thing the user would get, which is exactly when a preview is
// worth having.
//
// WHY THE PREVIEW IS BUILT IN CODE AND NOT IN XAML
// ───────────────────────────────────────────────────────────────────────
// The values come from the recipe, not from markup, so a family is
// re-previewed automatically when its profile changes. There is no second
// source of truth to fall out of sync.
//
// NEVER THROWS. This control lives inside a scrollable settings page and
// is instantiated eight times at once. An exception during a template load
// would take the whole Settings window down, which is the exact
// "Settings page is now empty" symptom this subsystem has produced before.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme.Eight;
using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProGlassAutomation.Views.Settings.Sections.Cards
{
    public partial class ThemeCenterCard : UserControl
    {
        private string _identity = "";
        private bool _isSelected;
        private bool _isDark;
        private static readonly DependencyProperty IsSelectedFamilyProperty =
            DependencyProperty.Register(
                nameof(IsSelectedFamily), typeof(bool),
                typeof(ThemeCenterCard),
                new PropertyMetadata(false, OnSelectionChanged));

        public ThemeCenterCard()
        {
            InitializeComponent();
            RootButton.Click += (_, _) => SelectionRequested?.Invoke(_identity);
            DataContextChanged += (_, _) => Rebind();
            Loaded += (_, _) => Rebind();
            IsVisibleChanged += (_, _) => { if (IsVisible) Rebind(); };
        }

        /// <summary>True when this card's family is the one currently
        /// selected. Drives the accent border, not the elevation —
        /// five of the eight families forbid or punish shadows, so a
        /// selected card that only changed its height would be invisible on
        /// exactly those families.</summary>
        public bool IsSelectedFamily
        {
            get => (bool)GetValue(IsSelectedFamilyProperty);
            set => SetValue(IsSelectedFamilyProperty, value);
        }

        private static void OnSelectionChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((ThemeCenterCard)d).ApplySelectionVisual();

        /// <summary>Raised with the family identity when this card is
        /// activated. Bound from the Settings view model.</summary>
        public event Action<string>? SelectionRequested;

        private void Rebind()
        {
            if (DataContext is not ViewModels.Settings.SettingsViewModel vm) return;
            if (vm.SelectedFamilyOption is not { } row) return;

            _identity = row.Key;
            _isSelected = string.Equals(
                row.Key, vm.SelectedVisualTheme, StringComparison.Ordinal);
            _isDark = Services.ThemeManager.CurrentTheme == AppTheme.Dark;

            NameText.Text = row.Display;
            TaglineText.Text = row.Tagline;
            SceneText.Text = row.SceneType.Length > 0
                ? row.SceneType
                : row.SceneLabel;

            try
            {
                PaintPreview(EightThemeCatalog.Resolve(row.Key), _isDark);
            }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Debug(
                    $"Theme Center preview for '{row.Key}' degraded: {ex.Message}");
            }

            ApplySelectionVisual();
        }

        /// <summary>
        /// Paints the miniature. Every value is read from the profile —
        /// corner radius, surface alpha, shadow blur/depth/opacity and the
        /// rim — so the card is a faithful reduction rather than a
        /// stylised stand-in.
        /// </summary>
        private void PaintPreview(EightThemeProfile profile, bool dark)
        {
            var ramp = profile.Ramp(dark);

            // Stage: the family's own code-drawn scene. This is the same
            // brush the module layer gets, which is why a family whose
            // material forbids a backdrop (Swiss, neumorphism) still shows
            // its correct ground here rather than the live wallpaper.
            PreviewStage.Fill = EightThemeScenes.For(profile.Identity, dark);

            double radius = profile.Geometry.Card;
            double scale = Math.Clamp(radius / 48.0, 0.0, 1.0);

            PreviewAnchor.CornerRadius = new CornerRadius(
                Math.Max(0, radius * (0.45 + 0.55 * scale)));
            PreviewSmallA.CornerRadius = PreviewSmallB.CornerRadius =
                new CornerRadius(Math.Max(0, radius * 0.45));
            PreviewAccent.CornerRadius = new CornerRadius(
                Math.Max(0, profile.Geometry.Control * 0.5));

            // Surfaces: the SAME alpha rule the painter applies. Opaque
            // families are opaque here too, which is the point — a preview
            // that showed a translucent card for Swiss would be lying.
            // Same single-source-of-truth rule as the live painter: the
            // ramp's own alpha. If the preview disagreed with the app the
            // Theme Center would be lying about what it does.
            PreviewAnchor.Background = Solid(Compose(ramp.Surface, profile.Material.Kind));
            PreviewSmallA.Background = Solid(Compose(ramp.SurfaceSecondary, profile.Material.Kind));
            PreviewSmallB.Background = Solid(Compose(ramp.SurfaceDetail, profile.Material.Kind));
            PreviewAccent.Background = Solid(Compose(ramp.Accent, EightMaterialKind.Solid));

            var rim = profile.Material.SpecularRim
                ? profile.Material.RimTint
                : ramp.BorderDefault;
            var rimBrush = RimBrush(
                rim, profile.Material.RimOpacity, profile.Material.Kind);
            PreviewSmallA.BorderBrush = rimBrush;
            PreviewSmallB.BorderBrush = rimBrush;
            PreviewAnchor.BorderBrush = rimBrush;
            PreviewAnchor.BorderThickness = new Thickness(1);

            // Elevation: the family's own numbers, halved so a 56px blur
            // does not blow out a 84px preview. A family that ships no
            // shadow gets none here either.
            if (profile.Elevation.Kind == EightElevationKind.None)
            {
                PreviewAnchor.Effect = null;
            }
            else
            {
                PreviewAnchorShadow.BlurRadius = Math.Min(
                    profile.Elevation.LowBlur * 0.5, 28);
                PreviewAnchorShadow.ShadowDepth = Math.Min(
                    profile.Elevation.LowDepth * 0.5, 4);
                PreviewAnchorShadow.Opacity = Math.Clamp(
                    profile.Elevation.LowOpacity * 0.7, 0.0, 0.7);
                PreviewAnchorShadow.Color = ramp.Shadow;
            }
        }

        /// <summary>
        /// Normalises a preview fill's alpha by the same rule the live
        /// painter uses: opaque families are forced fully opaque so the
        /// preview cannot quietly advertise translucency they do not
        /// render, and translucent families keep the ramp's own per-level
        /// alpha.
        /// </summary>
        private static Color Compose(Color c, EightMaterialKind kind)
        {
            if (kind is EightMaterialKind.Solid
                or EightMaterialKind.Extruded
                or EightMaterialKind.Inflated
                or EightMaterialKind.Foil)
                return Color.FromArgb(255, c.R, c.G, c.B);
            return c;
        }

        /// <summary>The keyline brush. Scales the family's rim opacity,
        /// which is the accessibility compensation on the extrude
        /// families — their boundary is carried by shadow, not by colour,
        /// so the keyline is not decoration.</summary>
        private static Brush RimBrush(
            Color c, double rimOpacity, EightMaterialKind kind)
        {
            var scaled = Compose(c, kind);
            if (rimOpacity < 0.999 && scaled.A != 255)
                scaled = Color.FromArgb(
                    (byte)Math.Round(
                        Math.Clamp(rimOpacity, 0, 1) * (scaled.A / 255.0)),
                    scaled.R, scaled.G, scaled.B);
            return Services.Theme.Eight.EightThemePainter.FrozenSolid(scaled);
        }

        private static SolidColorBrush Solid(Color c) =>
            Services.Theme.Eight.EightThemePainter.FrozenSolid(c);

        private void ApplySelectionVisual()
        {
            var accent = Services.ThemeManager.AccentColor;
            CardFrame.BorderBrush = _isSelected
                ? Services.Theme.Eight.EightThemePainter.FrozenSolid(accent)
                : Services.Theme.Eight.EightThemePainter.FrozenSolid(
                    Services.ThemeManager.CurrentTheme == AppTheme.Dark
                        ? Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)
                        : Color.FromArgb(0x1F, 0x00, 0x00, 0x00));
            CardFrame.BorderThickness = new Thickness(_isSelected ? 2 : 1);

            // The check mark is a non-colour state cue. It matters most on
            // the families whose whole identity is low-contrast surfaces:
            // on neumorphism a border-weight change alone is close to
            // invisible, and colour alone would fail anyone who cannot
            // separate the accent from the surface.
            AutomationProperties.SetName(
                RootButton, _isSelected
                    ? $"{NameText.Text} — selected"
                    : NameText.Text);
            ToolTip = _isSelected
                ? $"Selected — {NameText.Text}"
                : $"Select {NameText.Text}";
        }
    }
}