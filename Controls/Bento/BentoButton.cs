// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Bento/BentoButton.cs
// PROJECT: ProGlassAutomation
// MODULE: Bento Grid — the interactive cell
//
// A bento cell that the user can click. Everything BentoSurface says
// about geometry applies here too, and it matters MORE, because this is
// the control that is most likely to be given a hover animation by
// somebody.
//
// ── WHAT A BENTO BUTTON MAY DO ON HOVER ───────────────────────────────
//
//   Fill          yes, a small shift toward the ink
//   Edge          yes, a slightly heavier edge
//   Translate     NO
//   Scale         NO
//   Shadow        NO -- it has no shadow vocabulary to begin with
//
// The rule is not a preference. The cell's SIZE is what ranks it in the
// composition, and the whole reading order of the grid is built on cells
// keeping their footprints. A button that grows on hover makes itself
// look like the anchor while the pointer is over it, which is a
// different hierarchy from the one the user is reading.
//
// ── WHY THE FOCUS RING IS NOT OPTIONAL HERE ───────────────────────────
//
// A button is usually the largest interactive thing on screen, and size
// is an affordance. That is precisely why the ring matters: it is the
// only thing that distinguishes "focusable" from "big". Soft-UI themes
// routinely delete focus rings because a shadow reads as a focus state
// closely enough that they look like damage -- and this family has no
// shadow to read as anything, so there is no substitute.
//
// ── STATE WIRING ──────────────────────────────────────────────────────
//
// ButtonBase raises no OnIsPressedChanged or OnIsKeyboardFocusedChanged
// to override. Both are DependencyProperties, so they are observed with
// DependencyPropertyDescriptor rather than subclassed. See Wire().
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Bento;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Bento
{
    /// <summary>A bento cell that can be activated.</summary>
    public class BentoButton : ButtonBase
    {
        /// <summary>Which footprint this button occupies.</summary>
        public static readonly DependencyProperty BentoSpanProperty =
            DependencyProperty.Register(
                nameof(BentoSpan), typeof(Services.Theme.Bento.BentoSpan),
                typeof(BentoButton),
                new FrameworkPropertyMetadata(
                    Services.Theme.Bento.BentoSpan.Unit, OnBentoChanged));

        public Services.Theme.Bento.BentoSpan BentoSpan
        {
            get => (Services.Theme.Bento.BentoSpan)
                GetValue(BentoSpanProperty);
            set => SetValue(BentoSpanProperty, value);
        }

        public static readonly DependencyProperty BentoDepthProperty =
            DependencyProperty.Register(
                nameof(BentoDepth), typeof(SoftDepthTier),
                typeof(BentoButton),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Raised, OnBentoChanged));

        public SoftDepthTier BentoDepth
        {
            get => (SoftDepthTier)GetValue(BentoDepthProperty);
            set => SetValue(BentoDepthProperty, value);
        }

        public static readonly DependencyProperty BentoSelectedProperty =
            DependencyProperty.Register(
                nameof(BentoSelected), typeof(bool),
                typeof(BentoButton),
                new FrameworkPropertyMetadata(false, OnBentoChanged));

        public bool BentoSelected
        {
            get => (bool)GetValue(BentoSelectedProperty);
            set => SetValue(BentoSelectedProperty, value);
        }

        public static readonly DependencyProperty BentoStateProperty =
            DependencyProperty.Register(
                nameof(BentoState), typeof(SoftInteractionState),
                typeof(BentoButton),
                new PropertyMetadata(
                    SoftInteractionState.Rest, OnBentoChanged));

        public SoftInteractionState BentoState
        {
            get => (SoftInteractionState)GetValue(BentoStateProperty);
            set => SetValue(BentoStateProperty, value);
        }

        private readonly BentoSurfaceParts _parts = new BentoSurfaceParts();
        private bool _bound;
        private bool _wired;

        public BentoButton()
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

        private static void OnBentoChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BentoButton b) b.Repaint();
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

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            // Always interactive. This IS the interactive cell; there is
            // no state in which it stops being activatable, and a button
            // that did would be reporting a lie to the accessibility tree.
            BentoSurfaceParts.Paint(
                _parts, dark, BentoSpan, BentoDepth,
                ResolveState(), interactive: true,
                selected: BentoSelected,
                BentoSurface.Fill(dark), BentoSurface.Ink(dark));
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;

            // Pressed first: a pressed-and-hovered button is pressed, and
            // reporting Hover here would drop the only state that a
            // keyboard user can reach by holding Space.
            if (BentoState == SoftInteractionState.Pressed || IsPressed)
                return SoftInteractionState.Pressed;
            if (BentoState == SoftInteractionState.Focused || IsKeyboardFocused)
                return SoftInteractionState.Focused;
            if (BentoState == SoftInteractionState.Hover || IsMouseOver)
                return SoftInteractionState.Hover;

            return SoftInteractionState.Rest;
        }

        // ── Accessibility ────────────────────────────────────────────────
        //
        // No OnCreateAutomationPeer override. ButtonBase and TextBox
        // already return ButtonAutomationPeer and TextBoxAutomationPeer
        // respectively, so overriding them would restate the framework's
        // own answer and add a using directive for nothing. The
        // accessible NAME is the part that needs attention here, and that
        // is set from the template or by the caller -- on the container,
        // which is why BentoSurface exposes AccessibleName.

        /// <summary>
        /// A button's minimum size. WCAG 2.2 SC 2.5.8.
        /// <para>
        /// A bento cell is comfortably wider than tall, so a one-unit cell
        /// can fall under the target height on its own. The height is
        /// floored here rather than in the template, because the template
        /// cannot see the footprint the caller chose.
        /// </para>
        /// </summary>
        public static readonly DependencyProperty MinCellHeightProperty =
            DependencyProperty.Register(
                nameof(MinCellHeight), typeof(double),
                typeof(BentoButton),
                new PropertyMetadata(0.0));

        public double MinCellHeight
        {
            get => (double)GetValue(MinCellHeightProperty);
            set => SetValue(MinCellHeightProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);

            double floor = MinCellHeight > 0
                ? MinCellHeight
                : SoftUiAccessibility.MinHitTarget;

            if (size.Height < floor)
                size.Height = floor;

            return size;
        }
    }
}
