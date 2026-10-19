// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Bento/BentoField.cs
// PROJECT: ProGlassAutomation
// MODULE: Bento Grid — the input cell
//
// A bento cell that accepts text. The cell around it is exactly the cell
// BentoSurface draws, which is the point: an input sitting in a grid
// should look like a cell that happens to be editable, not like a form
// control that happens to be in a grid.
//
// ── WHY THERE IS NO RECESS ────────────────────────────────────────────
//
// Every other soft-UI family treats a text field as the one element
// that wants a WELL -- an inset, depth moving inward, because you are
// writing into the surface rather than pressing it. That instinct is
// correct for clay and neumorphism and wrong here.
//
// A bento's structure is carried entirely by the gutter: cells are
// separated by empty canvas, and that is what makes the composition
// read as one object. An inset field introduces a second, competing
// depth axis inside an already-flat cell, and the grid stops being a
// set of peers. The field gets the same edge as everything else, and
// earns its "this is editable" signal from the caret and the focus ring
// rather than from a recess.
//
// The focus ring therefore does real work here in a way it does not for
// a button: a flat cell containing text is, on its own, ambiguous.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Bento;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Bento
{
    /// <summary>A bento cell that accepts text.</summary>
    public class BentoField : TextBox
    {
        /// <summary>Which footprint this field occupies.</summary>
        public static readonly DependencyProperty BentoSpanProperty =
            DependencyProperty.Register(
                nameof(BentoSpan), typeof(Services.Theme.Bento.BentoSpan),
                typeof(BentoField),
                new FrameworkPropertyMetadata(
                    Services.Theme.Bento.BentoSpan.Wide, OnBentoChanged));

        public Services.Theme.Bento.BentoSpan BentoSpan
        {
            get => (Services.Theme.Bento.BentoSpan)
                GetValue(BentoSpanProperty);
            set => SetValue(BentoSpanProperty, value);
        }

        public static readonly DependencyProperty BentoDepthProperty =
            DependencyProperty.Register(
                nameof(BentoDepth), typeof(SoftDepthTier),
                typeof(BentoField),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Resting, OnBentoChanged));

        public SoftDepthTier BentoDepth
        {
            get => (SoftDepthTier)GetValue(BentoDepthProperty);
            set => SetValue(BentoDepthProperty, value);
        }

        public static readonly DependencyProperty BentoFocusedProperty =
            DependencyProperty.Register(
                nameof(BentoFocused), typeof(bool),
                typeof(BentoField),
                new PropertyMetadata(false, OnBentoChanged));

        public bool BentoFocused
        {
            get => (bool)GetValue(BentoFocusedProperty);
            set => SetValue(BentoFocusedProperty, value);
        }

        private readonly BentoSurfaceParts _parts = new BentoSurfaceParts();
        private bool _bound;
        private bool _wired;

        public BentoField()
        {
            HorizontalContentAlignment = HorizontalAlignment.Left;
            VerticalContentAlignment = VerticalAlignment.Center;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            // AcceptsReturn so a multi-line cell can be written into. The
            // single-line default would swallow Enter, which in a grid
            // means the user cannot complete the field.
            AcceptsReturn = true;
            TextWrapping = TextWrapping.Wrap;
            VerticalScrollBarVisibility = ScrollVisibility.Auto;
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
            if (d is BentoField f) f.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());

            // TextBox has no OnIsKeyboardFocusedChanged to override, so
            // the same DependencyPropertyDescriptor route as BentoButton.
            DependencyPropertyDescriptor.FromProperty(
                IsKeyboardFocusedProperty, typeof(KeyboardNavigation))
                .AddValueChanged(this, (_, _) => Repaint());

            DependencyPropertyDescriptor.FromProperty(
                IsMouseOverProperty, typeof(UIElement))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        /// <summary>Recomputes and applies the construction.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            BentoSurfaceParts.Paint(
                _parts, dark, BentoSpan, BentoDepth,
                ResolveState(), interactive: true, selected: false,
                BentoSurface.Fill(dark), BentoSurface.Ink(dark));
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (IsKeyboardFocused || BentoFocused)
                return SoftInteractionState.Focused;
            if (IsMouseOver) return SoftInteractionState.Hover;
            return SoftInteractionState.Rest;
        }
    }
}
