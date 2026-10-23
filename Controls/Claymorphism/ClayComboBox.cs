// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClayComboBox.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the dropdown
//
// A closed state that is a clay Surface, and an open state that is a
// floating ClayMenuRow list over it.
//
// ── THE POPUP IS THE HARD PART ───────────────────────────────────────
// A ComboBox popup is NOT in the control's visual tree, so a clay
// surface inside it does NOT inherit the control's template resources —
// and, worse, it is measured before it is shown, so a recipe that reads
// ActualWidth paints at zero. This control therefore resolves the menu
// palette ONCE into a frozen brush dictionary and applies it to the
// popup explicitly on Opened, rather than hoping inheritance does it.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.ComponentModel;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Claymorphism
{
    /// <summary>A claymorphic dropdown.</summary>
    public class ClayComboBox : ComboBox
    {
        /// <summary>Corner radius of the closed state.</summary>
        public static readonly DependencyProperty ClayCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ClayCornerRadius), typeof(CornerRadius),
                typeof(ClayComboBox),
                new FrameworkPropertyMetadata(
                    new CornerRadius(20), OnClayChanged));

        public CornerRadius ClayCornerRadius
        {
            get => (CornerRadius)GetValue(ClayCornerRadiusProperty);
            set => SetValue(ClayCornerRadiusProperty, value);
        }

        /// <summary>Surface colour. Transparent uses the ramp's surface.</summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClayComboBox),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private bool _bound;
        private bool _wired;

        public ClayComboBox()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            // A ComboBox is not editable here, so it must not pretend to
            // accept text — the default template allows a caret into a
            // field that discards it.
            IsEditable = false;
            IsTextSearchEnabled = false;
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

        private static void OnClayChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ClayComboBox c) c.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());

            DependencyPropertyDescriptor.FromProperty(
                IsDropDownOpenProperty, typeof(ComboBox))
                .AddValueChanged(this, (_, _) => OnDropDownToggled());

            // The popup lives outside the visual tree, so it cannot be
            // reached through OnApplyTemplate. This is the only reliable
            // moment at which it exists.
            DropDownOpened += (_, _) => PaintPopup();
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (IsDropDownOpen) return SoftInteractionState.Pressed;
            if (IsKeyboardFocusWithin) return SoftInteractionState.Focused;
            if (IsMouseOver) return SoftInteractionState.Hover;
            return SoftInteractionState.Rest;
        }

        /// <summary>Recomputes and applies the whole recipe.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;
            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            ClaySurfaceParts.Paint(
                _parts, dark, SoftDepthTier.Raised, ResolveState(),
                ClaySurface.TopLeft(ClayCornerRadius),
                small: false,
                ResolveFill(dark));
        }

        private Color ResolveFill(bool dark)
        {
            var baseFill = ClayFill;
            if (baseFill.A == 0)
            {
                var token = Application.Current?.TryFindResource(
                    "SurfacePrimaryBrush") as SolidColorBrush;
                baseFill = token?.Color ?? Colors.White;
            }
            if (!IsEnabled) return ClaySurface.Shade(baseFill, 0.55);
            if (IsDropDownOpen) return ClaySurface.Lighten(baseFill, 0.06);
            return baseFill;
        }

        /// <summary>Repaints the closed state when the popup opens/closes.</summary>
        private void OnDropDownToggled() => Repaint();

        /// <summary>
        /// Gives the popup its own frozen surface colour.
        /// <para>
        /// The popup cannot inherit the control's resources, and it is
        /// measured before it is shown — so anything that reads
        /// ActualWidth at this point reads zero and paints nothing. A
        /// frozen brush resolved from the palette sidesteps both.
        /// </para>
        /// </summary>
        private void PaintPopup()
        {
            // ComboBox REQUIRES the popup to be named PART_Popup; the control
            // looks it up by that exact name and silently does nothing
            // without it. A bespoke name would render a box that never
            // opens.
            var popup = Template?.FindName("PART_Popup", this) as Popup;
            if (popup?.Child is not Border root) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var profile = ClayPalette.For(dark);
            Color surface = Colors.White;

            if (profile is not null)
            {
                try
                {
                    var stack = new ClayRecipe(profile, dark)
                        .Resolve(SoftDepthTier.Floating,
                                 SoftInteractionState.Rest, 22);
                    if (stack is not null)
                    {
                        var token = Application.Current?.TryFindResource(
                            "SurfacePrimaryBrush") as SolidColorBrush;
                        var baseFill = token?.Color ?? Colors.White;
                        surface = stack.FillLift > 0.001
                            ? ClayRecipe.Lift(baseFill, stack.FillLift)
                            : baseFill;
                    }
                }
                catch (Exception)
                {
                    // Cosmetic failure only — leave the popup flat rather
                    // than throwing out of an Opened handler.
                    surface = Colors.White;
                }
            }

            root.Background = ClayVisualFactory.Frozen(
                new SolidColorBrush(surface));
        }
    }
}
