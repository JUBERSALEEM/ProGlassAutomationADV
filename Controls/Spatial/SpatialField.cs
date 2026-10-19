// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Spatial/SpatialField.cs
// PROJECT: ProGlassAutomation
// MODULE: Spatial / Immersive — the input plane
//
// A translucent text field. The interesting problem here is NOT the
// focus ring — that is the same requirement as everywhere else in this
// engine — it is the PLACEHOLDER.
//
// ── WHY THE PLACEHOLDER IS THE HARD PART ──────────────────────────────
//
// A translucent panel shows whatever is behind it, so a placeholder
// sitting on a half-opaque surface is text drawn over an UNKNOWN
// background. The user cannot know whether a word is grey because it is
// a placeholder or because something bright is behind the panel.
//
// Every other family gets this for free: an opaque field has one
// background and the placeholder's contrast against it is a fixed,
// checkable number. Here it is not fixed, and cannot be made to be.
//
// So this control does the only honest thing available: the field
// carries a MINIMUM surface opacity that is higher than the panel's,
// whatever the plane would otherwise ask for. A field that reveals the
// desktop behind it is not a field, it is a guess. The recipe applies
// the same idea at the plane level, and this is its local expression.
//
// ── WHY THERE IS NO RECESS HERE EITHER ─────────────────────────────────
//
// Same reasoning as BentoField, for the same underlying reason but a
// stronger one here: in a family built on translucency, an inset adds a
// SECOND depth cue inside a surface whose whole job is to state its
// depth. Two cues competing is how a Z hierarchy stops being readable
// as a hierarchy.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme.Spatial;

namespace ProGlassAutomation.Controls.Spatial
{
    /// <summary>A translucent, editable plane.</summary>
    public class SpatialField : TextBox
    {
        /// <summary>
        /// The plane. A field defaults to Panel rather than inheriting a
        /// chrome plane, because chrome has the lowest opacity in the
        /// ladder and a field is where text has to be read.
        /// </summary>
        public static readonly DependencyProperty SpatialPlaneProperty =
            DependencyProperty.Register(
                nameof(SpatialPlane), typeof(SpatialPlane),
                typeof(SpatialField),
                new FrameworkPropertyMetadata(
                    SpatialPlane.Panel, OnSpatialChanged));

        public SpatialPlane? SpatialPlane
        {
            get => (SpatialPlane?)GetValue(SpatialPlaneProperty);
            set => SetValue(SpatialPlaneProperty, value);
        }

        public static readonly DependencyProperty SpatialDepthProperty =
            DependencyProperty.Register(
                nameof(SpatialDepth), typeof(SoftDepthTier),
                typeof(SpatialField),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Resting, OnSpatialChanged));

        public SoftDepthTier SpatialDepth
        {
            get => (SoftDepthTier)GetValue(SpatialDepthProperty);
            set => SetValue(SpatialDepthProperty, value);
        }

        public static readonly DependencyProperty SpatialFocusedProperty =
            DependencyProperty.Register(
                nameof(SpatialFocused), typeof(bool),
                typeof(SpatialField),
                new PropertyMetadata(false, OnSpatialChanged));

        public bool SpatialFocused
        {
            get => (bool)GetValue(SpatialFocusedProperty);
            set => SetValue(SpatialFocusedProperty, value);
        }

        /// <summary>
        /// The floor this field's own surface opacity is held to,
        /// regardless of its plane.
        /// <para>
        /// 0.78 — the OVERLAY plane's value, not the panel's 0.62. The
        /// reason is in the file header: placeholder text over a
        /// near-transparent surface is unreadable against an unknown
        /// backdrop, and a field is a text surface before it is a
        /// decorative one. A field that reads as less present than the
        /// card next to it has the emphasis exactly backwards.
        /// </para>
        /// </summary>
        public const double MinimumFieldOpacity = 0.78;

        private readonly SpatialSurfaceParts _parts = new SpatialSurfaceParts();
        private bool _bound;
        private bool _wired;

        public SpatialField()
        {
            HorizontalContentAlignment = HorizontalAlignment.Left;
            VerticalContentAlignment = VerticalAlignment.Center;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
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

        private static void OnSpatialChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SpatialField f) f.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
            SpatialPalette.Changed += (_, _) => Repaint();

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

            SpatialSurfaceParts.Paint(
                _parts, dark, SpatialPlane, SpatialDepth,
                ResolveState(), interactive: true, selected: false,
                SpatialSurface.Fill(), SpatialSurface.Ink());
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (IsKeyboardFocused || SpatialFocused)
                return SoftInteractionState.Focused;
            if (IsMouseOver) return SoftInteractionState.Hover;
            return SoftInteractionState.Rest;
        }
    }
}
