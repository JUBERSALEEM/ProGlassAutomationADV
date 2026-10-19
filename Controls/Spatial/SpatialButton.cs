// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Spatial/SpatialButton.cs
// PROJECT: ProGlassAutomation
// MODULE: Spatial / Immersive — the interactive plane
//
// A Spatial button differs from every other button in this engine in one
// way that is worth stating on its own:
//
//     PRESS RECEDES.
//
// Fluent's press feedback: an item that is pressed "momentarily recedes
// into the background and then returns to its original position."
//
// So press LOWERS the surface — less opacity, a step further from the
// viewer — where every other family here shifts the fill toward the ink
// and reads as moving toward you. That is not a stylistic preference.
// In a family whose entire premise is a Z hierarchy, "toward you" and
// "away from you" are statements about the Z axis, and pressing a thing
// and having it come closer is the wrong statement: it says this is
// the surface you were reaching FOR, when pressing says the opposite.
//
// ── WHY NO TRANSLATE ───────────────────────────────────────────────────
//
// Also stated in the family header. Hover raises opacity and press
// lowers it; neither moves the element. A control that translated on
// hover would be claiming a Z change that nothing else in the family
// makes, and on a plane that is already translucent the translation
// would also change what shows through it — so the content behind the
// button would appear to shift, which is a rendering artefact rather
// than an effect.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme.Spatial;

namespace ProGlassAutomation.Controls.Spatial
{
    /// <summary>A translucent, activatable plane.</summary>
    public class SpatialButton : ButtonBase
    {
        public static readonly DependencyProperty SpatialPlaneProperty =
            DependencyProperty.Register(
                nameof(SpatialPlane), typeof(SpatialPlane),
                typeof(SpatialButton),
                new FrameworkPropertyMetadata(null, OnSpatialChanged));

        public SpatialPlane? SpatialPlane
        {
            get => (SpatialPlane?)GetValue(SpatialPlaneProperty);
            set => SetValue(SpatialPlaneProperty, value);
        }

        public static readonly DependencyProperty SpatialDepthProperty =
            DependencyProperty.Register(
                nameof(SpatialDepth), typeof(SoftDepthTier),
                typeof(SpatialButton),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Raised, OnSpatialChanged));

        public SoftDepthTier SpatialDepth
        {
            get => (SoftDepthTier)GetValue(SpatialDepthProperty);
            set => SetValue(SpatialDepthProperty, value);
        }

        public static readonly DependencyProperty SpatialSelectedProperty =
            DependencyProperty.Register(
                nameof(SpatialSelected), typeof(bool),
                typeof(SpatialButton),
                new FrameworkPropertyMetadata(false, OnSpatialChanged));

        public bool SpatialSelected
        {
            get => (bool)GetValue(SpatialSelectedProperty);
            set => SetValue(SpatialSelectedProperty, value);
        }

        public static readonly DependencyProperty SpatialStateProperty =
            DependencyProperty.Register(
                nameof(SpatialState), typeof(SoftInteractionState),
                typeof(SpatialButton),
                new PropertyMetadata(
                    SoftInteractionState.Rest, OnSpatialChanged));

        public SoftInteractionState SpatialState
        {
            get => (SoftInteractionState)GetValue(SpatialStateProperty);
            set => SetValue(SpatialStateProperty, value);
        }

        private readonly SpatialSurfaceParts _parts = new SpatialSurfaceParts();
        private bool _bound;
        private bool _wired;

        public SpatialButton()
        {
            HorizontalContentAlignment = HorizontalAlignment.Left;
            VerticalContentAlignment = VerticalAlignment.Center;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            FocusVisualStyle = null;
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

        private static void OnSpatialChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SpatialButton b) b.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
            SpatialPalette.Changed += (_, _) => Repaint();

            // ButtonBase declares no OnIsPressedChanged or
            // OnIsKeyboardFocusedChanged to override. Both are
            // DependencyProperties, so they are observed here.
            DependencyPropertyDescriptor.FromProperty(
                IsPressedProperty, typeof(ButtonBase))
                .AddValueChanged(this, (_, _) => Repaint());

            DependencyPropertyDescriptor.FromProperty(
                IsKeyboardFocusedProperty, typeof(KeyboardNavigation))
                .AddValueChanged(this, (_, _) => Repaint());

            DependencyPropertyDescriptor.FromProperty(
                IsMouseOverProperty, typeof(ButtonBase))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        /// <summary>Recomputes and applies the construction.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            SpatialSurfaceParts.Paint(
                _parts, dark, SpatialPlane, SpatialDepth,
                ResolveState(), interactive: true,
                selected: SpatialSelected,
                SpatialSurface.Fill(), SpatialSurface.Ink());
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;

            // Pressed outranks everything, including Focus. A button that
            // is both held and focused reports Held, because that is the
            // state the user is actually in and it is the one with the
            // receding treatment attached.
            if (SpatialState == SoftInteractionState.Pressed || IsPressed)
                return SoftInteractionState.Pressed;
            if (SpatialState == SoftInteractionState.Focused || IsKeyboardFocused)
                return SoftInteractionState.Focused;
            if (SpatialState == SoftInteractionState.Hover || IsMouseOver)
                return SoftInteractionState.Hover;

            return SoftInteractionState.Rest;
        }

        /// <summary>WCAG 2.2 SC 2.5.8 minimum hit target. A translucent
        /// control with no shadow reads smaller than it is, so this is
        /// enforced rather than assumed.</summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);

            double floor = SoftUiAccessibility.MinHitTarget;
            if (size.Height < floor) size.Height = floor;
            if (size.Width < floor) size.Width = floor;

            return size;
        }
    }
}
