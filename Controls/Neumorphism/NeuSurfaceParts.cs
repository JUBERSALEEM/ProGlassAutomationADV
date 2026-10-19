// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Neumorphism/NeuSurfaceParts.cs
// PROJECT: ProGlassAutomation
// MODULE: Neumorphism — recipe applied to template parts
//
// ── FIVE BORDERS, AND THE MIDDLE TWO ARE THE RECIPE ─────────────────
//
//   PART_NeuEdge        the accessible boundary (not the recipe)
//   PART_NeuInsetShade  inner shade  ─┐ only when dented
//   PART_NeuInsetLight  inner light  ─┘
//   PART_NeuShadeHost   fill + shade offset away from the light
//   PART_NeuLightHost   fill + highlight offset toward the light
//
// The mirrored OUTSET pair needs two stacked elements rather than two
// effects on one, because WPF allows a single Effect per element. Both
// hosts carry the SAME opaque fill, so the upper one completely covers
// the lower and only their outside shadows differ — which is the WPF
// shape of the canonical CSS pair
//
//     8px 8px 16px dark,  -8px -8px 16px white
//
// The ORDER is load-bearing. Shade host first, light host second: the
// highlight must be the one on top, or the shade's soft outer edge
// clips it and the extrusion flattens on every surface on the page.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.Neu;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Neumorphism
{
    /// <summary>The borders a neumorphic ControlTemplate must name.</summary>
    internal sealed class NeuSurfaceParts
    {
        /// <summary>The outer boundary. Not part of the recipe.</summary>
        public Border? Edge { get; private set; }

        /// <summary>Inner shade — dented surfaces only.</summary>
        public Border? InsetShade { get; private set; }

        /// <summary>Inner highlight — dented surfaces only.</summary>
        public Border? InsetLight { get; private set; }

        /// <summary>Carries the fill and the shade cast AWAY from the light.</summary>
        public Border? ShadeHost { get; private set; }

        /// <summary>Carries the fill and the highlight cast TOWARD it.</summary>
        public Border? LightHost { get; private set; }

        public bool Complete =>
            Edge is not null && InsetShade is not null
            && InsetLight is not null
            && ShadeHost is not null && LightHost is not null;

        public void Bind(FrameworkElement root)
        {
            Edge = root.Template.FindName("PART_NeuEdge", root) as Border;
            InsetShade =
                root.Template.FindName("PART_NeuInsetShade", root) as Border;
            InsetLight =
                root.Template.FindName("PART_NeuInsetLight", root) as Border;
            ShadeHost =
                root.Template.FindName("PART_NeuShadeHost", root) as Border;
            LightHost =
                root.Template.FindName("PART_NeuLightHost", root) as Border;
        }

        /// <summary>Applies a resolved neumorphic construction.</summary>
        public void Apply(NeuShadowStack stack, Color baseFill)
        {
            if (!Complete) return;

            var fill = NeuVisualFactory.Pressed(
                NeuVisualFactory.Fill(baseFill, stack), stack);

            var brush = Solid(fill);

            // The inset pair, only while dented.
            InsetShade!.Background = InsetBrush(stack.InsetShade);
            InsetLight!.Background = InsetBrush(stack.InsetLight);

            ShadeHost!.Background = brush;
            ShadeHost.Effect = NeuVisualFactory.OutsetShade(stack);

            LightHost!.Background = brush;
            LightHost.Effect = NeuVisualFactory.OutsetLight(stack);

            // A dented surface has NO outset at all. Rendering both is the
            // classic mistake: an extruded object that is also dented is
            // not one object, and the eye reads it as two objects badly
            // overlapping rather than as one pressed surface.
            Edge!.BorderThickness = new Thickness(1);
        }

        /// <summary>
        /// Falls back to a plain rounded surface, for when neumorphism is
        /// not the active family. Still draws the edge — a control that
        /// vanishes on a theme change is worse than a plain one.
        /// </summary>
        public void ApplyPlain(Color fill, Pen? edge)
        {
            if (!Complete) return;
            var brush = Solid(fill);
            ShadeHost!.Background = brush;
            ShadeHost.Effect = null;
            LightHost!.Background = brush;
            LightHost.Effect = null;
            InsetLight!.Background = null;
            InsetShade!.Background = null;
            Edge!.BorderBrush = edge;
            Edge!.BorderThickness = new Thickness(1);
        }

        public void SetEdge(Pen? pen)
        {
            if (Edge is not null) Edge.BorderBrush = pen;
        }

        /// <summary>
        /// Paints one inset layer. WPF has no native inset shadow, so the
        /// layer is a frozen radial-free vertical gradient standing in for
        /// the roll — the same trade the clay engine makes, and for the
        /// same reason: a per-element BlurEffect on every button in a
        /// scrolling grid is a frame-rate problem long before it is a
        /// visual one.
        /// </summary>
        private static Brush? InsetBrush(NeuShadowLayer? layer)
        {
            if (layer is null) return null;

            // The inset pair is offset along the light diagonal, so a
            // LinearGradientBrush between the two corners reproduces the
            // dent's shading without needing a shadow at all.
            var g = new LinearGradientBrush
            {
                StartPoint = layer.Direction == SoftLight.HighlightDirection
                    ? SoftLight.ShadeOrigin
                    : SoftLight.HighlightOrigin,
                EndPoint = layer.Direction == SoftLight.HighlightDirection
                    ? SoftLight.HighlightOrigin
                    : SoftLight.HighlightFar,
            };
            g.GradientStops.Add(new GradientStop(
                layer.Color, 0.0));
            g.GradientStops.Add(new GradientStop(
                Transparent(layer.Color), 1.0));
            g.Freeze();
            return g;
        }

        private static Color Transparent(Color c) =>
            Color.FromArgb(0, c.R, c.G, c.B);

        private static SolidColorBrush Solid(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>
        /// Resolves the palette and applies the whole construction — the
        /// single entry point every neumorphic control uses.
        /// </summary>
        public static void Paint(
            NeuSurfaceParts parts,
            bool dark,
            SoftDepthTier depth,
            SoftInteractionState state,
            double radius,
            Color baseFill)
        {
            if (parts is null || !parts.Complete) return;

            var profile = NeuPalette.For(dark);
            if (profile is null)
            {
                parts.ApplyPlain(baseFill, null);
                return;
            }

            try
            {
                var stack = new NeuRecipe(profile, dark)
                    .Resolve(depth, state, radius);
                if (stack is null) return;
                parts.SetEdge(
                    SoftUiAccessibility.BuildEdge(baseFill, dark));
                parts.Apply(stack, baseFill);
            }
            catch (Exception)
            {
                // A broken recipe is a cosmetic defect; a crash on theme
                // apply is a support ticket.
                parts.ApplyPlain(baseFill, null);
            }
        }
    }
}