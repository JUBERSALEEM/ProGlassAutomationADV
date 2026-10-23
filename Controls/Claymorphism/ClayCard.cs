// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClayCard.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the structured card
//
// ClaySurface is a blank mould: give it one child. ClayCard is the unit
// the reference actually ships — the thing on every screen in the
// references, with a header, a body and an optional footer.
//
// ── WHY THIS IS NOT "ClaySurface WITH THREE PROPERTIES" ───────────────
// Because the header is not decoration. On the reference cards the
// header carries the avatar, the title AND the status, and those three
// must keep their alignment even when the title wraps to two lines. That
// is layout, so it belongs in a template rather than in three DPs the
// caller has to remember to align.
//
// ── WHO OWNS THE CONTENT ─────────────────────────────────────────────
// Header, Body and Footer are all ContentPresenters, and ClayCard's own
// Content is a FOURTH slot beneath them. A caller that sets Content on a
// card already using Header gets the body, not an overlap — which is the
// behaviour ClaySurface cannot offer, because it only has one slot.
//
// ── INTERACTIVITY ─────────────────────────────────────────────────────
// A card is NOT focusable by default. The reference cards are clickable,
// but making every card a tab stop is how a grid of 40 tasks turns the
// tab order into a maze. Callers that want keyboard reach opt in with
// Focusable, and the focus ring then has something to attach to.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Claymorphism
{
    /// <summary>A claymorphic card: header, body, footer.</summary>
    public class ClayCard : ContentControl
    {
        /// <summary>Card corner radius. The reference floor is 28.</summary>
        public static readonly DependencyProperty ClayCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ClayCornerRadius), typeof(CornerRadius),
                typeof(ClayCard),
                new FrameworkPropertyMetadata(
                    new CornerRadius(28), OnClayChanged));

        public CornerRadius ClayCornerRadius
        {
            get => (CornerRadius)GetValue(ClayCornerRadiusProperty);
            set => SetValue(ClayCornerRadiusProperty, value);
        }

        /// <summary>Card colour. Transparent uses the ramp's surface.</summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClayCard),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        /// <summary>Header content: avatar, title, status.</summary>
        public static readonly DependencyProperty ClayHeaderProperty =
            DependencyProperty.Register(
                nameof(ClayHeader), typeof(object),
                typeof(ClayCard),
                new FrameworkPropertyMetadata(null, OnClayChanged));

        public object? ClayHeader
        {
            get => GetValue(ClayHeaderProperty);
            set => SetValue(ClayHeaderProperty, value);
        }

        /// <summary>Footer content: dates, subtask counts, actions.</summary>
        public static readonly DependencyProperty ClayFooterProperty =
            DependencyProperty.Register(
                nameof(ClayFooter), typeof(object),
                typeof(ClayCard),
                new FrameworkPropertyMetadata(null, OnClayChanged));

        public object? ClayFooter
        {
            get => GetValue(ClayFooterProperty);
            set => SetValue(ClayFooterProperty, value);
        }

        /// <summary>Accent used by the header's leading marker.</summary>
        public static readonly DependencyProperty ClayAccentProperty =
            DependencyProperty.Register(
                nameof(ClayAccent), typeof(Color),
                typeof(ClayCard),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayAccent
        {
            get => (Color)GetValue(ClayAccentProperty);
            set => SetValue(ClayAccentProperty, value);
        }

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private bool _bound;
        private bool _wired;

        public ClayCard()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            Focusable = false;
            Padding = new Thickness(20);
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
            if (d is ClayCard c) c.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
            DependencyPropertyDescriptor.FromProperty(
                IsMouseOverProperty, typeof(UIElement))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (IsKeyboardFocused) return SoftInteractionState.Focused;
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
                ResolveFill());

            if (Template?.FindName("ClayCardAccentBrush", this)
                is SolidColorBrush b)
            {
                var accent = ClayAccent;
                if (accent.A == 0)
                {
                    accent = Application.Current?.TryFindResource(
                        "AccentBrush") is SolidColorBrush ab
                        ? ab.Color : Colors.Gray;
                }
                b.Color = accent;
            }
        }

        private Color ResolveFill()
        {
            var baseFill = ClayFill;
            if (baseFill.A == 0)
            {
                var token = Application.Current?.TryFindResource(
                    "SurfacePrimaryBrush") as SolidColorBrush;
                baseFill = token?.Color ?? Colors.White;
            }
            if (!IsEnabled) return ClaySurface.Shade(baseFill, 0.45);
            // Hover lifts the FILL, not the tier. A card that grows a tier
            // on hover is a card that resizes under the pointer, and a
            // list of them reflows as the user moves across it.
            if (IsMouseOver) return ClaySurface.Lighten(baseFill, 0.05);
            return baseFill;
        }
    }
}
