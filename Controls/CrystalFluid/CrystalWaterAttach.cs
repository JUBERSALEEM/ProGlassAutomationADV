// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/CrystalFluid/CrystalWaterAttach.cs
// PROJECT: ProGlassAutomation
//
// GLOBAL WATER MATERIAL (2026-09-06) — makes the Ios-17pm animated
// liquid-fill effect available to EVERY card in the app, not just the
// three hand-wired hosts (StatCard + the two Settings previews).
//
// Problem it solves
// -----------------
// Cards are <Border Style="…CardStyle"> elements. A Border is a
// Decorator: it has no ControlTemplate, so no Style/Template/DynamicResource
// can layer extra children (like water) into its visual tree — and the
// water is a multi-layer animated Control, not a Brush.
//
// What this does
// --------------
// Setting  crystal:CrystalWaterAttach.Attach="True"  on a Border wraps its
// single child in a Grid that stacks:
//
//      Border glass background  (painted by the Border itself, bottom)
//      CrystalWaterControl      (ZIndex 1 — the animated fill)
//      original card content    (ZIndex 5 — content stays dominant, §63)
//
// so the layer order matches the authored CrystalCard: glass → water →
// content. The control self-gates on the global family resource
// CrystalWaterDefaultEnabled via SetResourceReference (DynamicResource
// semantics): it is Collapsed and fully idle under Normal/Monet/etc. and
// turns on live the moment Ios-17pm is painted — no reload required.
//
// Layout fidelity
// ---------------
// Border insets its single child by (BorderThickness + Padding) and
// IGNORES the child's Margin. To let the water span the full card we zero
// the Border.Padding and move the exact same inset onto the content's
// Margin (a Grid DOES honour margins), so the content keeps its
// pixel-identical position. All original values are captured so Unwrap can
// restore them.
//
// Performance
// -----------
// One extra control per attached card. Under non-crystal families it is
// Collapsed with zero running storyboards (StartAnimations also bails on
// ReducedMotion / !IsVisible). The control already pauses on unload/hidden
// (virtualized lists stop off-screen). Opt any surface out by removing the
// single Setter.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ProGlassAutomation.Controls.CrystalFluid
{
    /// <summary>Attached "water material" for Border-hosted cards.</summary>
    public static class CrystalWaterAttach
    {
        public static readonly DependencyProperty AttachProperty =
            DependencyProperty.RegisterAttached(
                "Attach",
                typeof(bool),
                typeof(CrystalWaterAttach),
                new PropertyMetadata(false, OnAttachChanged));

        public static bool GetAttach(DependencyObject d) =>
            (bool)d.GetValue(AttachProperty);

        public static void SetAttach(DependencyObject d, bool value) =>
            d.SetValue(AttachProperty, value);

        private static void OnAttachChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Border border)
                return;

            if ((bool)e.NewValue)
            {
                if (border.IsLoaded)
                    EnsureWrapped(border);
                else
                    border.Loaded += OnBorderLoaded;
            }
            else
            {
                border.Loaded -= OnBorderLoaded;
                Unwrap(border);
            }
        }

        private static void OnBorderLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.Loaded -= OnBorderLoaded;
                EnsureWrapped(border);
            }
        }

        private static void EnsureWrapped(Border border)
        {
            // Nothing to layer under, or already wrapped (idempotent).
            var child = border.Child;
            if (child == null)
                return;
            if (child is Grid { Tag: WrapState })
                return;

            // Loaded has fired → the style (incl. BasedOn) is applied, so
            // these are the effective values the card is showing right now.
            var original = child;
            var padding = border.Padding;
            var state = new WrapState(
                original,
                padding,
                original.Margin,
                original.HorizontalAlignment,
                original.VerticalAlignment);

            var water = new CrystalWaterControl
            {
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            // Live family gate + ambient water level + the studio axes all
            // flow from resources (DynamicResource semantics → live theme
            // switches, no reload). Water corner radius follows the card's
            // own radius so the self-clip matches the glass.
            water.SetResourceReference(
                CrystalWaterControl.WaterEnabledProperty,
                "CrystalWaterDefaultEnabled");
            water.SetResourceReference(
                CrystalWaterControl.WaterLevelProperty,
                "CrystalDefaultWaterLevel");
            water.SetBinding(
                CrystalWaterControl.CornerRadiusProperty,
                new Binding(nameof(Border.CornerRadius)) { Source = border });

            var wrapper = new Grid { Tag = state };
            water.SetValue(Panel.ZIndexProperty, 1);
            original.SetValue(Panel.ZIndexProperty, 5);

            // Border insets its child by Padding and ignores child Margin;
            // zero the padding so the wrapper (and the water) span the
            // full card, then re-apply the exact inset as the content's
            // margin (honoured by the Grid). Border's own Background is
            // unaffected, so the glass still fills the whole card.
            border.Padding = new Thickness(0);
            original.Margin = padding;
            original.HorizontalAlignment = HorizontalAlignment.Stretch;
            original.VerticalAlignment = VerticalAlignment.Stretch;

            wrapper.Children.Add(water);
            wrapper.Children.Add(original);
            border.Child = wrapper;
        }

        private static void Unwrap(Border border)
        {
            if (border.Child is not Grid grid || grid.Tag is not WrapState state)
                return;
            if (!grid.Children.Contains(state.Original))
                return;

            grid.Children.Clear();
            state.Original.Margin = state.Margin;
            state.Original.HorizontalAlignment = state.HorizontalAlignment;
            state.Original.VerticalAlignment = state.VerticalAlignment;
            border.Padding = state.Padding;
            border.Child = state.Original;
        }

        private sealed class WrapState
        {
            public WrapState(
                UIElement original,
                Thickness padding,
                Thickness margin,
                HorizontalAlignment horizontalAlignment,
                VerticalAlignment verticalAlignment)
            {
                Original = original;
                Padding = padding;
                Margin = margin;
                HorizontalAlignment = horizontalAlignment;
                VerticalAlignment = verticalAlignment;
            }

            public UIElement Original { get; }
            public Thickness Padding { get; }
            public Thickness Margin { get; }
            public HorizontalAlignment HorizontalAlignment { get; }
            public VerticalAlignment VerticalAlignment { get; }
        }
    }
}
