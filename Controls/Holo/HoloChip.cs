// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Holo/HoloChip.cs
// PROJECT: ProGlassAutomation
// MODULE: Holographic / Iridescent — the chip
//
// ══════════════════════════════════════════════════════════════════════
// THIS IS THE ONE CONTROL WHERE THE FOIL GOES UNDER A GLYPH, and it is
// the control that decides whether this family is shippable.
// ══════════════════════════════════════════════════════════════════════
//
// The research describes exactly this control — a segmented view switch
// whose selected "thumb" wears a holographic foil — and exactly its
// trap, twice, in the same paragraph:
//
//   "Iridescent foils can wreck icon contrast — use mix-blend-mode or a
//    solid icon chip so glyphs stay legible on every hue."
//   "Keep the effect on the thumb, not the whole control, so unselected
//    icons stay calm and readable."
//
// ── SO THE CHIP IS FIVE LAYERS, AND EVERY ONE IS EARNED ─────────────
//
//   1. MATTE GROUND      the family's opaque ground. Never foil.
//   2. FOIL RIM          the spectrum, on the edge.
//   3. GLYPH CHIP        OPAQUE, and sized to the glyph.
//   4. GLYPH             ink, measured against the WORST band.
//   5. FOCUS RING        from the ramp.
//
// Layer 3 is the one that makes the rest legal. Without an opaque chip
// behind the glyph there is no guarantee available, because the glyph
// lands on a different band every time the foil drifts — which is the
// definition of not being able to promise anything at all.
//
// Layer 4's ink comes from HoloRecipe.ChipInk, which measures against
// EVERY band rather than the one visible at the moment of the click.
// With the bands solved to a common luminance the family's own ink
// already clears ~6:1 across the whole spectrum, so this normally costs
// nothing and is there for the case where a host supplies its own.
//
// ── AND UNSELECTED CHIPS ARE DELIBERATELY INERT ─────────────────────
//
// An unselected chip carries no foil at all. That is the research's
// "keep the effect on the thumb" instruction taken literally, and it is
// also the difference between a view switch and a novelty.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services;
using System.ComponentModel;
using System.Windows.Documents;
using ProGlassAutomation.Services.Theme.Holo;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;

namespace ProGlassAutomation.Controls.Holo
{
    /// <summary>A holographic chip. The foil reaches the glyph only
    /// because an opaque chip is guaranteed to stand between them.</summary>
    public class HoloChip : ContentControl
    {
        /// <summary>
        /// Whether this chip is the selected one. Only a selected chip
        /// wears foil; an unselected one is a plain matte pill.
        /// </summary>
        public static readonly DependencyProperty HoloSelectedProperty =
            DependencyProperty.Register(
                nameof(HoloSelected), typeof(bool),
                typeof(HoloChip),
                new FrameworkPropertyMetadata(false, OnHoloChanged));

        public bool HoloSelected
        {
            get => (bool)GetValue(HoloSelectedProperty);
            set => SetValue(HoloSelectedProperty, value);
        }

        public static readonly DependencyProperty ReducedMotionProperty =
            DependencyProperty.Register(
                nameof(ReducedMotion), typeof(bool),
                typeof(HoloChip),
                new PropertyMetadata(
                    false, OnHoloStateChanged));

        public bool ReducedMotion
        {
            get => (bool)GetValue(ReducedMotionProperty);
            set => SetValue(ReducedMotionProperty, value);
        }

        /// <summary>Current interaction state, readable by tests.</summary>
        public static readonly DependencyProperty HoloStateProperty =
            DependencyProperty.Register(
                nameof(HoloState), typeof(SoftInteractionState),
                typeof(HoloChip),
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

        public HoloChip()
        {
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
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
            if (d is HoloChip c) c.Repaint();
        }

        private static void OnHoloStateChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HoloChip c) c.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
            DependencyPropertyDescriptor.FromProperty(
                IsKeyboardFocusWithinProperty, typeof(Control))
                .AddValueChanged(this, (_, _) => Repaint());
            DependencyPropertyDescriptor.FromProperty(
                IsMouseOverProperty, typeof(Control))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        /// <summary>Recomputes and applies the construction.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            HoloSurfaceParts.Paint(
                _parts, dark, ReducedMotion,
                SoftDepthTier.Well,
                ResolveState(),
                HoloSelected,
                HoloSurface.Token("WindowBackgroundBrush"),
                HoloSurface.Token("PrimaryForegroundBrush"));

            ApplyGlyph();
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (HoloSelected) return SoftInteractionState.Focused;
            return HoloState;
        }

        /// <summary>
        /// Layers 3 and 4: the opaque chip and its measured ink.
        /// <para>
        /// The chip is given the CHIP INK as its own background rather
        /// than the ground, so the glyph sits on the colour that was
        /// verified against the spectrum. The ink it was measured
        /// against is the foil's own ink when the foil is showing, and
        /// the ground's ink when it is not — which is what makes the
        /// same code correct for both the selected and unselected chip.
        /// </para>
        /// </summary>
        private void ApplyGlyph()
        {
            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var profile = HoloPalette.For(dark);
            if (profile is null) return;

            var recipe = new HoloRecipe(profile, dark, ReducedMotion);

            // Only a foil-bearing chip needs the spectrum check. An
            // unselected chip has no spectrum behind the glyph, so
            // running the worst-band measurement would be measuring
            // against something that is not there.
            var ink = HoloSelected
                ? recipe.ChipInk(profile.Ink)
                : profile.Ink;

            if (Template?.FindName("PART_HoloGlyphChip", this)
                is Border chip)
            {
                chip.Background = Frozen(ink);
                chip.Visibility = HoloSelected
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            // A ContentPresenter, NOT a TextBlock. The glyph's own
            // colour travels through TextElement's inheritance rather
            // than a plain Foreground, because the content is whatever
            // the host supplied — a TextBlock, an icon, a Path — and
            // those do not share a Foreground property between them.
            if (Template?.FindName("PART_HoloGlyphInk", this)
                is ContentPresenter glyph)
            {
                TextElement.SetForeground(
                    glyph, Frozen(HoloSelected ? profile.Ground : ink));
            }
        }

        private static SolidColorBrush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}