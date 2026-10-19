// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Holo/HoloSurface.cs
// PROJECT: ProGlassAutomation
// MODULE: Holographic / Iridescent — the panel
//
// ── A MATTE PANEL WITH A FOIL EDGE, NOT A RAINBOW PANEL ─────────────
//
// This control exists to make the family WRONG in the most likely way
// available, so it is worth saying plainly what it is not. It is not a
// surface filled with the spectrum. It is an opaque matte panel whose
// RIM carries the foil and whose ground stays exactly as legible as the
// theme's other panels.
//
// The design research is unambiguous that filling a surface with foil
// destroys the surface's purpose:
//
//   "Text on top of holographic loses contrast as the colours shift."
//   "Constrain the mesh to backgrounds and large surfaces. The moment
//    you put a mesh gradient on a card or button, you've lost the plot."
//
// A holographic panel that put the foil on its ground would be a panel
// whose text changes contrast continuously while the foil drifts. So
// the ground here is opaque, always, and HoloPalette.Build forces its
// alpha to 255 rather than trusting the caller's ramp.
//
// Hover raises the RIM and nothing else. That is the whole interaction
// vocabulary: a quiet matte field, and an edge that comes alive.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Holo;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Holo
{
    /// <summary>A holographic panel: matte ground, spectral rim.</summary>
    public class HoloSurface : ContentControl
    {
        public static readonly DependencyProperty HoloDepthProperty =
            DependencyProperty.Register(
                nameof(HoloDepth), typeof(SoftDepthTier),
                typeof(HoloSurface),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Resting, OnHoloChanged));

        public SoftDepthTier HoloDepth
        {
            get => (SoftDepthTier)GetValue(HoloDepthProperty);
            set => SetValue(HoloDepthProperty, value);
        }

        /// <summary>
        /// Freezes the foil at a fixed offset instead of animating it.
        /// <para>
        /// Defaults from the <c>MotionReduced</c> token that
        /// EightThemePainter publishes, which honours the OS
        /// "prefers reduced motion" setting — so a user who has asked
        /// for less motion gets none, without every host having to
        /// remember to pass the flag down.
        /// </para>
        /// <para>
        /// FREEZE, not "play faster". Shortening an animation does not
        /// reduce motion, it just makes the movement harder to predict;
        /// and WCAG 2.3.3 is about the vestibular response, which a
        /// 26-second loop sped up to two seconds produces perfectly
        /// well.
        /// </para>
        /// </summary>
        public static readonly DependencyProperty ReducedMotionProperty =
            DependencyProperty.Register(
                nameof(ReducedMotion), typeof(bool),
                typeof(HoloSurface),
                new PropertyMetadata(
                    false, OnHoloStateChanged));

        public bool ReducedMotion
        {
            get => (bool)GetValue(ReducedMotionProperty);
            set => SetValue(ReducedMotionProperty, value);
        }

        public static readonly DependencyProperty HoloSelectedProperty =
            DependencyProperty.Register(
                nameof(HoloSelected), typeof(bool),
                typeof(HoloSurface),
                new FrameworkPropertyMetadata(false, OnHoloChanged));

        public bool HoloSelected
        {
            get => (bool)GetValue(HoloSelectedProperty);
            set => SetValue(HoloSelectedProperty, value);
        }

        /// <summary>Current interaction state, readable by tests.</summary>
        public static readonly DependencyProperty HoloStateProperty =
            DependencyProperty.Register(
                nameof(HoloState), typeof(SoftInteractionState),
                typeof(HoloSurface),
                new PropertyMetadata(
                    SoftInteractionState.Rest, OnHoloStateChanged));

        public SoftInteractionState HoloState
        {
            get => (SoftInteractionState)GetValue(HoloStateProperty);
            set => SetValue(HoloStateProperty, value);
        }

        private readonly HoloSurfaceParts _parts = new HoloSurfaceParts();
        private bool _bound;
        private bool _wired;

        public HoloSurface()
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            // Seed from the theme's published preference, but only when
            // the host has not set a value itself — SetResourceReference
            // on an unset property, so an explicit local value wins.
            if (ReadLocalValue(ReducedMotionProperty)
                == DependencyProperty.UnsetValue)
            {
                SetResourceReference(
                    ReducedMotionProperty, "MotionReduced");
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (!_bound)
            {
                _parts.Bind(this);
                _bound = true;
                Wire();
            }
            Repaint();
        }

        private static void OnHoloChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HoloSurface s) s.Repaint();
        }

        private static void OnHoloStateChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HoloSurface s) s.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            // Repaint on a theme change. ReducedMotion needs no
            // subscription of its own: it is a dependency property with
            // OnHoloStateChanged as its change callback, so flipping it
            // — by the token, by the host, or by a test — repaints on
            // its own.
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
        }

        /// <summary>Recomputes and applies the construction.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            HoloSurfaceParts.Paint(
                _parts,
                dark: ThemeManager.CurrentTheme == AppTheme.Dark,
                ReducedMotion,
                HoloDepth,
                ResolveState(),
                HoloSelected,
                Paper(),
                Ink());
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            return HoloState;
        }

        private static Color Paper() =>
            Token("WindowBackgroundBrush");

        private static Color Ink() =>
            Token("PrimaryForegroundBrush");

        internal static Color Token(string key) =>
            (Application.Current?.TryFindResource(key)
                as SolidColorBrush)?.Color ?? Colors.Black;
    }

}
