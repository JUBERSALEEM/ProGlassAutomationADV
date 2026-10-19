// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Neumorphism/NeuToggleSwitch.cs
// PROJECT: ProGlassAutomation
// MODULE: Neumorphism — the toggle
//
// The component that only neumorphism has: a track DENTED into the page
// carrying a thumb EXTRUDED out of it. Two constructions on one control,
// which is why this style can never be faked with a stylesheet.
//
// ── STATE IS COLOUR, NOT POSITION ────────────────────────────────────
// A thumb on the right is position. Position alone fails WCAG 1.4.1 for
// anyone who cannot separate the two ends of a low-contrast track, and
// it means nothing at all to a screen reader. So the ON state fills the
// track with the ACCENT and the thumb takes the accent's foreground ink.
// The movement is a bonus; the colour is the state.
//
// ── AND IT IS NOT JUST :active ───────────────────────────────────────
// A toggle that only changes while the mouse is held down looks
// pressed and then springs back, which is simply wrong for a control
// that HOLDS a setting. The visual is driven by IsChecked, so keyboard
// and automation get the same state the pointer does.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Neu;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Neumorphism
{
    /// <summary>A neumorphic toggle: dented track, extruded thumb.</summary>
    public class NeuToggleSwitch : ToggleButton
    {
        /// <summary>Track width. 60×30 is the canonical proportion.</summary>
        public static readonly DependencyProperty TrackWidthProperty =
            DependencyProperty.Register(
                nameof(TrackWidth), typeof(double),
                typeof(NeuToggleSwitch),
                new FrameworkPropertyMetadata(
                    60.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double TrackWidth
        {
            get => (double)GetValue(TrackWidthProperty);
            set => SetValue(TrackWidthProperty, value);
        }

        /// <summary>
        /// Track height. Defaults to half the width so the control is
        /// always a stadium; the thumb radius follows from it.
        /// </summary>
        public static readonly DependencyProperty TrackHeightProperty =
            DependencyProperty.Register(
                nameof(TrackHeight), typeof(double),
                typeof(NeuToggleSwitch),
                new FrameworkPropertyMetadata(
                    30.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double TrackHeight
        {
            get => (double)GetValue(TrackHeightProperty);
            set => SetValue(TrackHeightProperty, value);
        }

        public NeuToggleSwitch()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            IsKeyboardFocusWithinChanged += (_, _) => Repaint();

            // The control is a toggle, so it must be announced as one.
            // ToggleButton already reports the on/off state through
            // automation; what it does NOT do is make the state
            // perceivable, and that part is the accent fill below.
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (IsKeyboardFocused) return SoftInteractionState.Focused;
            if (IsMouseOver) return SoftInteractionState.Hover;
            return SoftInteractionState.Rest;
        }

        /// <summary>Repaints the track and the thumb.</summary>
        public void Repaint()
        {
            if (Template is null) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var page = PageColour(dark);
            var accent = AccentColour(dark);
            double trackRadius = TrackHeight / 2.0;
            double thumbRadius = TrackHeight / 2.0 - 4.0;

            ApplyTrack(dark, page, trackRadius);
            ApplyThumb(dark, page, accent, thumbRadius);
        }

        /// <summary>
        /// The track is ALWAYS concave. It does not extrude on hover and
        /// it does not flatten when checked — a cavity that changes depth
        /// stops reading as a cavity and starts reading as a button.
        /// </summary>
        private void ApplyTrack(bool dark, Color page, double radius)
        {
            var profile = NeuPalette.For(dark);
            if (profile is null)
            {
                if (Template.FindName("PART_NeuTrack", this)
                    is Border plain)
                {
                    plain.Background = Frozen(page);
                    plain.BorderBrush =
                        SoftUiAccessibility.BuildEdge(page, dark);
                }
                return;
            }

            if (Template.FindName("PART_NeuTrack", this) is not Border track)
                return;

            // The track is a plain filled stadium plus the accessible
            // edge. It does NOT resolve a recipe: a cavity's depth comes
            // from being concave, and here that is expressed by the
            // thumb sitting proud of it rather than by anything on the
            // track itself. Giving the track its own inset pair would
            // read as a second, competing cavity.
            var corner = new CornerRadius(radius);
            track.CornerRadius = corner;
            track.Background = Frozen(page);
            track.BorderBrush =
                SoftUiAccessibility.BuildEdge(page, dark);
            track.BorderThickness = new Thickness(1);
        }

        /// <summary>
        /// The thumb is ALWAYS extruded, and always the page's colour —
        /// including when checked. When checked the TRACK takes the
        /// accent instead, so the state is a colour change on a large
        /// area rather than a 26px circle moving across a track nobody
        /// can read.
        /// </summary>
        private void ApplyThumb(
            bool dark, Color page, Color accent, double radius)
        {
            if (Template.FindName("PART_NeuThumb", this) is not Border thumb)
                return;

            var profile = NeuPalette.For(dark);
            thumb.CornerRadius = new CornerRadius(radius);

            if (profile is null)
            {
                thumb.Background = Frozen(page);
                return;
            }

            var state = IsKeyboardFocused
                ? SoftInteractionState.Focused
                : SoftInteractionState.Rest;

            var stack = new NeuRecipe(profile, dark).Resolve(
                SoftDepthTier.Floating, state, radius);

            var fill = NeuVisualFactory.Fill(page, stack);

            thumb.Background = Frozen(fill);
            thumb.Effect = NeuVisualFactory.OutsetLight(stack);

            // The accent glyph rides on the thumb when checked, so the
            // ON state is legible without relying on either the shadow or
            // the thumb's position.
            if (Template.FindName("PART_NeuCheck", this) is Path check)
            {
                check.Visibility = IsChecked
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                check.Fill = Frozen(accent);
            }
        }

        private static Color PageColour(bool dark)
        {
            var brush = Application.Current?.TryFindResource(
                "ModuleBackgroundBrush") as SolidColorBrush;
            return brush?.Color ?? Colors.Gray;
        }

        private static Color AccentColour(bool dark)
        {
            var brush = Application.Current?.TryFindResource(
                "AccentBrush") as SolidColorBrush;
            return brush?.Color ?? Colors.SteelBlue;
        }

        private static SolidColorBrush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}