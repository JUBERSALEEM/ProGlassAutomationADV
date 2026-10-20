// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Glassmorphism/GlassField.cs
// PROJECT: ProGlassAutomation
// MODULE: Glassmorphism — the input pane
//
// A text field is CONTENT, and this control hard-codes that.
//
// GlassSurface exposes GlassPlacement and defaults it to Content;
// GlassButton defaults it to Chrome. This one does not expose it at
// all. A field is a text surface by definition, the research puts
// text-heavy cards in the 0.40-0.55 band, and the engine's own floor
// for content is 0.78 — so there is no configuration in which this
// control becomes translucent, and no reason to let one be requested.
//
// ── WHY NO RECESS ─────────────────────────────────────────────────────
//
// Same as BentoField and SpatialField, and the reasoning strengthens as
// the surface gets busier. An inset is a second depth cue inside a
// surface that is already carrying one, and on a frosted pane the two
// cues do not compose — the inner well competes with the rim for the
// eye and the pane stops reading as glass.
//
// ── WHY THE FIELD IS PLACED ON A PANEL PLANE EVEN THOUGH THIS FAMILY ──
//
// has no planes
//
// It does not. GlassPalette exposes no plane, which is correct: a
// uniform blur means there is no ladder to sit on. The only placement
// distinction is Content versus Chrome, and this is Content. The note is
// here because the other two families will have suggested otherwise by
// the time anyone edits this file.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Glassmorphism;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Glassmorphism
{
    /// <summary>A glass field. Always opaque — see the file header.</summary>
    public class GlassField : TextBox
    {
        public static readonly DependencyProperty GlassFocusedProperty =
            DependencyProperty.Register(
                nameof(GlassFocused), typeof(bool),
                typeof(GlassField),
                new PropertyMetadata(false, OnGlassChanged));

        public bool GlassFocused
        {
            get => (bool)GetValue(GlassFocusedProperty);
            set => SetValue(GlassFocusedProperty, value);
        }

        private readonly GlassSurfaceParts _parts = new GlassSurfaceParts();
        private bool _bound;
        private bool _wired;

        public GlassField()
        {
            HorizontalContentAlignment = HorizontalAlignment.Left;
            VerticalContentAlignment = VerticalAlignment.Center;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            AcceptsReturn = true;
            TextWrapping = TextWrapping.Wrap;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
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

        private static void OnGlassChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GlassField f) f.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
            GlassPalette.Changed += (_, _) => Repaint();

            DependencyPropertyDescriptor.FromProperty(
                IsKeyboardFocusedProperty, typeof(KeyboardNavigation))
                .AddValueChanged(this, (_, _) => Repaint());

            DependencyPropertyDescriptor.FromProperty(
                IsMouseOverProperty, typeof(UIElement))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            GlassSurfaceParts.Paint(
                _parts, dark,
                placement: GlassPlacement.Content,
                tier: SoftDepthTier.Resting,
                ResolveState(), interactive: true, selected: false,
                GlassSurface.Fill(), GlassSurface.Ink());
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (IsKeyboardFocused || GlassFocused)
                return SoftInteractionState.Focused;
            if (IsMouseOver) return SoftInteractionState.Hover;
            return SoftInteractionState.Rest;
        }
    }
}
