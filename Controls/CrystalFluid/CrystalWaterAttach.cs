// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/CrystalFluid/CrystalWaterAttach.cs
// PROJECT: ProGlassAutomation
//
// GLOBAL WATER MATERIAL (2026-09-06, v2 — auto-detect) — the Ios-17pm
// animated liquid-fill effect is now applied to EVERY glass card in the
// app WITHOUT per-style enumeration. The companion type
// CrystalWaterGlobal registers a class handler on Border.Loaded that
// calls TryAutoAttach for every Border, so new views/cards inherit water
// automatically — no style edits, no re-typing, no "19 files".
//
// How a card is recognised (deterministic, not heuristic)
// -------------------------------------------------------
// A "card" in this app is a <Border> whose Background resolves to one of
// the glass SURFACE brushes the theme engine publishes (GlassUltraClearBrush,
// GlassSurfaceBrush, ModuleBackgroundBrush, ElevatedSurfaceBrush, …) AND
// whose corners are rounded. We compare the Border's effective Background
// to the live resource instance by reference (DynamicResource shares the
// instance) and read the effective CornerRadius, so:
//   · glass cards (styled or inline <Border.Style>)  → wrapped,
//   · divider rails / icon chips / tint & accent fills → never wrapped,
//   · rectangular glass chrome (tab strips, station rails, footer bars,
//     work surfaces — radius 0) → never wrapped.
//
// Layer order (authored CrystalCard): glass → CrystalWaterControl (ZIndex 1)
// → content (ZIndex 5). Water self-gates on the global family resource
// CrystalWaterDefaultEnabled + CrystalDefaultWaterLevel via
// SetResourceReference (DynamicResource semantics → live family switch, no
// reload), and reads the studio axes through the same tokens.
//
// Guards
// ------
//   · opt-out:  crystal:CrystalWaterAttach.Attach="False"  (never wrap)
//   · force:    crystal:CrystalWaterAttach.Attach="True"   (wrap non-glass host)
//   · skip Borders with no child (rails/dividers/backdrops),
//   · skip Borders already hosting a CrystalWaterControl (StatCard, CrystalCard),
//   · skip Borders inside another card surface (outermost card per region
//     wins — prevents stacked double water-lines in nested panels).
//
// Layout fidelity
// ---------------
// Border insets its single child by (BorderThickness + Padding) and IGNORES
// the child's Margin. The wrapper zeroes Border.Padding and re-applies the
// exact inset as the content's Margin (a Grid honours margins), so content
// stays pixel-identical while the water spans the full card. Corner radius
// follows the card's own CornerRadius so the water's rounded clip matches.
//
// Crash safety (2026-09-07, ERR-21172E)
// -------------------------------------
// Reparenting the card's child (Border.Child = wrapper) re-activates every
// binding in the card's subtree. Run.Text is TwoWay-by-default in WPF, so a
// <Run Text="{Binding SomeReadOnlyProperty}"/> inside a card throws
// "A TwoWay or OneWayToSource binding cannot work on the read-only
// property ..." mid-wrap and crashes the app. EnsureWrapped is therefore
// wrapped in a try/catch that rolls the card back to its untouched state
// on ANY failure — the card simply keeps its normal (unwatered) look, and
// the family gate keeps the water control invisible anyway.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ProGlassAutomation.Controls.CrystalFluid
{
    /// <summary>
    /// Attached "water material" for Border-hosted cards. Attach is a
    /// tri-state override: null (default) = auto-detect glass surfaces,
    /// true = force, false = never.
    /// </summary>
    public static class CrystalWaterAttach
    {
        /// <summary>Glass SURFACE brushes the engine repaints per family.
        /// Only these (never hover/tint/accent fills) qualify a Border as a
        /// card surface.</summary>
        private static readonly string[] GlassSurfaceKeys =
        {
            "GlassSurfaceBrush",
            "GlassUltraClearBrush",
            "GlassCrystalBrush",
            "GlassFrostedBrush",
            "GlassSurfaceElevated",
            "GlassSurfaceUltra",
            "ModuleBackgroundBrush",
            "ElevatedSurfaceBrush",
            "SideMenuBackgroundBrush",
            "AboutInteractiveBrush",
            "LiquidCard",
        };

        public static readonly DependencyProperty AttachProperty =
            DependencyProperty.RegisterAttached(
                "Attach",
                typeof(bool?),
                typeof(CrystalWaterAttach),
                new PropertyMetadata(null, OnAttachChanged));

        public static bool? GetAttach(DependencyObject d) =>
            (bool?)d.GetValue(AttachProperty);

        public static void SetAttach(DependencyObject d, bool? value) =>
            d.SetValue(AttachProperty, value);

        /// <summary>Entry point for the global class handler
        /// (CrystalWaterGlobal). Safe to call on every Border.Loaded.</summary>
        public static void TryAutoAttach(Border border)
        {
            if (border == null)
                return;

            var mode = GetAttach(border);
            if (mode == false)
                return;                          // explicit opt-out

            if (border.Child == null)
                return;                          // rail / divider / backdrop

            if (border.Child is Grid { Tag: WrapState })
                return;                          // already wrapped

            if (mode == true)
            {
                EnsureWrapped(border);           // forced host
                return;
            }

            if (!IsCardSurface(border))
                return;                          // not a card surface
            if (ContainsWaterControl(border))
                return;                          // StatCard/CrystalCard already host water
            if (HasCardAncestor(border))
                return;                          // outermost card per region wins

            EnsureWrapped(border);
        }

        private static void OnAttachChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Border border)
                return;

            if ((bool?)e.NewValue == false)
            {
                border.Loaded -= OnBorderLoaded;
                Unwrap(border);
                return;
            }

            if (border.IsLoaded)
                TryAutoAttach(border);
            else
                border.Loaded += OnBorderLoaded;
        }

        private static void OnBorderLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.Loaded -= OnBorderLoaded;
                TryAutoAttach(border);
            }
        }

        // ── Card detection ──────────────────────────────────────────────

        /// <summary>
        /// A Border is a "card surface" iff (1) its Background resolves to
        /// one of the engine's glass SURFACE brushes and (2) it has rounded
        /// corners. Rule (2) is what separates cards from rectangular
        /// chrome (tab strips, station rails, footer bars, work surfaces —
        /// all glass-tinted but radius 0). Deterministic on the effective
        /// DP values, never a size guess.
        /// </summary>
        private static bool IsCardSurface(Border border)
        {
            var background = border.Background;
            if (background == null)
                return false;

            var radius = border.CornerRadius;
            if (radius.TopLeft <= 0 && radius.TopRight <= 0 &&
                radius.BottomLeft <= 0 && radius.BottomRight <= 0)
            {
                return false;
            }

            foreach (var key in GlassSurfaceKeys)
            {
                if (border.TryFindResource(key) is Brush brush &&
                    ReferenceEquals(brush, background))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsWaterControl(DependencyObject root)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is CrystalWaterControl)
                    return true;
                if (ContainsWaterControl(child))
                    return true;
            }
            return false;
        }

        private static bool HasCardAncestor(Border border)
        {
            for (var p = VisualTreeHelper.GetParent(border);
                 p != null;
                 p = VisualTreeHelper.GetParent(p))
            {
                if (p is Border ancestor && IsCardSurface(ancestor))
                    return true;
            }
            return false;
        }

        // ── Wrap / unwrap ───────────────────────────────────────────────

        private static void EnsureWrapped(Border border)
        {
            var child = border.Child;
            if (child == null)
                return;
            if (child is Grid { Tag: WrapState })
                return;

            // Loaded has fired, so the style (incl. BasedOn / inline
            // Border.Style) is applied and these are the effective values.
            // Margin / alignment are FrameworkElement properties, so the
            // child is re-typed from UIElement (Border.Child's static type).
            if (child is not FrameworkElement original)
                return;   // non-Framework content carries no margin/alignment

            // Capture the untouched state up front so ANY failure below can
            // restore the card pixel-identically.
            var padding = border.Padding;
            var margin = original.Margin;
            var hAlign = original.HorizontalAlignment;
            var vAlign = original.VerticalAlignment;

            try
            {
                var state = new WrapState(
                    original, padding, margin, hAlign, vAlign);

                var water = new CrystalWaterControl
                {
                    IsHitTestVisible = false,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                };

                // Live family gate + ambient level + studio axes (DynamicResource
                // semantics, live theme switches, no reload). Radius follows the
                // card so the rounded self-clip matches the glass.
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
                // transfer the inset to the content's margin (honoured by the
                // Grid) so the content is pixel-identical and the water spans
                // the full card. Alignment is left untouched: the Grid honours
                // it exactly as the Border did.
                border.Padding = new Thickness(0);
                original.Margin = padding;

                // Detach the original child BEFORE reparenting it into the
                // wrapper. WPF throws "Specified element is already the
                // logical child of another element" when an element is added
                // while still connected to its old parent.
                border.Child = null;
                wrapper.Children.Add(water);
                wrapper.Children.Add(original);

                // ERR-21172E: this is the re-parenting step that re-activates
                // every binding in the card's subtree. If one of them throws
                // (e.g. a TwoWay-by-default Run.Text binding on a read-only
                // property), the catch below rolls the card back untouched.
                border.Child = wrapper;
            }
            catch (Exception ex)
            {
                // Roll the card back to its exact pre-wrap state. The
                // failure is a UI-binding activation fault, not a broken
                // card — leaving it unwrapped (and unwatered) is the safe
                // outcome, and the app must keep running.
                try
                {
                    if (border.Child is Grid grid && grid.Tag is WrapState)
                        grid.Children.Clear();
                    original.Margin = margin;
                    original.HorizontalAlignment = hAlign;
                    original.VerticalAlignment = vAlign;
                    border.Padding = padding;
                    border.Child = original;
                }
                catch
                {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("catch suppressed");
            }

                ProGlassAutomation.Services.Logging.AppLogService.Debug(
                    "[CrystalWater] skipped wrapping a card (binding "
                    + "re-activation failed): " + ex.Message);
            }
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
                FrameworkElement original,
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

            public FrameworkElement Original { get; }
            public Thickness Padding { get; }
            public Thickness Margin { get; }
            public HorizontalAlignment HorizontalAlignment { get; }
            public VerticalAlignment VerticalAlignment { get; }
        }
    }
}
