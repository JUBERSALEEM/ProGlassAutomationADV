// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Holo/HoloButton.cs
// PROJECT: ProGlassAutomation
// MODULE: Holographic / Iridescent — the button
//
// ── THE GROUND STAYS MATTE WHEN YOU PRESS IT ─────────────────────────
//
// Every other family here signals a press by changing the surface
// underneath the label: Swiss inverts it, neumorphism dents it, clay
// inflates it. Holographic deliberately does NOT, and that is the whole
// design rather than an omission.
//
// If the press flooded the ground with foil, the label would change
// contrast on the frame the user pressed — the worst possible moment,
// because they are looking straight at it. So the press raises the RIM
// and brightens the sheen, and the matte ground the text lives on does
// not move a single bit.
//
// The result is a control that looks like it is lighting up from the
// edges inward. Which is, conveniently, also what real foil does when
// you tilt it toward a light.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;

namespace ProGlassAutomation.Controls.Holo
{
    /// <summary>A holographic button whose state lives on its rim.</summary>
    public class HoloButton : Button
    {
        public static readonly DependencyProperty ReducedMotionProperty =
            DependencyProperty.Register(
                nameof(ReducedMotion), typeof(bool),
                typeof(HoloButton),
                new PropertyMetadata(
                    false, OnHoloChanged));

        public bool ReducedMotion
        {
            get => (bool)GetValue(ReducedMotionProperty);
            set => SetValue(ReducedMotionProperty, value);
        }

        private readonly HoloSurfaceParts _parts = new HoloSurfaceParts();
        private bool _bound;
        private bool _wired;

        public HoloButton()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

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
            if (d is HoloButton b) b.Repaint();
        }

        /// <summary>
        /// Precedence: disabled beats pressed, because a disabled button
        /// that still brightens is the brightest thing on the screen;
        /// and pressed beats focused, because a button clicked out of
        /// must not flicker through its focus ring on the way back.
        /// </summary>
        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (IsPressed) return SoftInteractionState.Pressed;
            if (IsKeyboardFocused) return SoftInteractionState.Focused;
            if (IsMouseOver) return SoftInteractionState.Hover;
            return SoftInteractionState.Rest;
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());

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

            HoloSurfaceParts.Paint(
                _parts,
                dark: ThemeManager.CurrentTheme == AppTheme.Dark,
                ReducedMotion,
                SoftDepthTier.Raised,
                ResolveState(),
                selected: false,
                HoloSurface.Token("WindowBackgroundBrush"),
                HoloSurface.Token("PrimaryForegroundBrush"));
        }
    }
}