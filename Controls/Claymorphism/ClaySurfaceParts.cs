// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClaySurfaceParts.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — recipe applied to template parts
//
// Holds the four named borders a clay ControlTemplate declares and
// applies a resolved ClayShadowStack to them. Internal: the controls own
// one; callers never do.
//
// The template is authored in XAML (matching the rest of the design
// system) and only the BRUSHES and the EFFECT are written from code,
// because those are objects the recipe computes rather than markup. The
// geometry stays declarative — ClayCornerRadius is a real CornerRadius
// DP so it flows through TemplateBinding without a converter.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Claymorphism
{
    /// <summary>The four borders a clay ControlTemplate must name.</summary>
    internal sealed class ClaySurfaceParts
    {
        /// <summary>
        /// The element carrying the fill and the outer drop. The shadow
        /// renders from this element's silhouette, so the drop host has to
        /// be the one with the opaque fill.
        /// </summary>
        public Border? DropHost { get; private set; }

        /// <summary>Inner roll toward the light.</summary>
        public Border? Highlight { get; private set; }

        /// <summary>
        /// Inner roll away from the light. Declared AFTER the highlight in
        /// the template so it paints on top: at the corners the two rolls
        /// overlap, and the crossover between them is what forms the
        /// rounded edge. Reversing them flattens the corner.
        /// </summary>
        public Border? Shade { get; private set; }

        /// <summary>
        /// The accessible boundary. Not part of the recipe — see the
        /// header note in ClayVisualFactory.BuildEdge.
        /// </summary>
        public Border? Edge { get; private set; }

        /// <summary>True once every part has been found.</summary>
        public bool Complete =>
            DropHost is not null && Highlight is not null
            && Shade is not null && Edge is not null;

        /// <summary>
        /// Resolves the parts from an applied template. A partial match is
        /// a template authoring error, so it is reported loudly rather
        /// than leaving half a recipe painted.
        /// <para>
        /// The parameter is FRAMEWORK ELEMENT, not Control. The comment
        /// that used to stand here claimed only Control carries a
        /// <c>Template</c> and FrameworkElement has no such member — that
        /// is simply wrong: Template is declared on FrameworkElement, and
        /// Control inherits it. The narrower signature was not caught
        /// earlier because every existing caller passes <c>this</c> from a
        /// Control, which satisfies both. ClaySlider's thumb is a Border
        /// carrying its own ControlTemplate, and it is exactly the case
        /// the narrow parameter could not express.
        /// </para>
        /// </summary>
        public void Bind(FrameworkElement root)
        {
            DropHost = root.Template?.FindName("PART_ClayDropHost", root) as Border;
            Highlight = root.Template?.FindName("PART_ClayHighlight", root) as Border;
            Shade = root.Template?.FindName("PART_ClayShade", root) as Border;
            Edge = root.Template?.FindName("PART_ClayEdge", root) as Border;
        }

        /// <summary>Applies a resolved three-shadow stack.</summary>
        public void Apply(ClayShadowStack stack, Color baseFill)
        {
            if (!Complete) return;

            var visuals = ClayVisualFactory.Build(stack, baseFill);

            DropHost!.Background = visuals.Fill
                ?? ClayVisualFactory.Frozen(new SolidColorBrush(baseFill));
            DropHost.Effect = visuals.Drop;

            Highlight!.Background = visuals.Light;
            Shade!.Background = visuals.Shade;

            // A disabled surface keeps its edge: a control that has faded
            // out entirely is a control the user cannot find. The recipe
            // has already removed its drop and highlight.
            Edge!.BorderThickness = new Thickness(1);
        }

        /// <summary>
        /// Falls back to a plain rounded surface, for when claymorphism is
        /// not the active family. Still honours the 3:1 boundary —
        /// a control that vanishes on a theme change is worse than one
        /// that looks plain.
        /// </summary>
        public void ApplyPlain(Color fill, Pen? edge)
        {
            if (!Complete) return;
            DropHost!.Background =
                ClayVisualFactory.Frozen(new SolidColorBrush(fill));
            DropHost.Effect = null;
            Highlight!.Background = null;
            Shade!.Background = null;
            // A Pen is NOT a Brush. Border.BorderBrush wants the
            // pen's BRUSH; a Border never reads a Pen's Thickness,
            // which is why BorderThickness below is the only thing
            // that actually sets the edge weight.
            Edge!.BorderBrush = edge?.Brush;
            Edge!.BorderThickness = new Thickness(1);
        }

        /// <summary>Attaches the accessible edge pen.</summary>
        public void SetEdge(Pen? pen)
        {
            // Brush, not Pen — see ApplyPlain.
            if (Edge is not null) Edge.BorderBrush = pen?.Brush;
        }

        /// <summary>
        /// Resolves the palette and applies the whole recipe. The single
        /// entry point every clay control uses, so the fallback path and
        /// the try/catch live in exactly one place.
        /// </summary>
        /// <remarks>
        /// The palette is reached for statically. It was previously a
        /// parameter, which does not even compile — a static class cannot
        /// be a parameter type — and the comment claimed it was there as a
        /// test seam so a test could drive a synthetic profile. Neither
        /// was true: the palette IS global state, so passing it in gained
        /// nothing, and the tests drive <c>ClayRecipe</c> directly against
        /// a synthetic profile rather than coming through here.
        /// </remarks>
        public static void Paint(
            ClaySurfaceParts parts,
            bool dark,
            SoftDepthTier depth,
            SoftInteractionState state,
            double radius,
            bool small,
            Color fill)
        {
            if (parts is null || !parts.Complete) return;

            var profile = ClayPalette.For(dark);
            if (profile is null)
            {
                // Another family is active. A plain rounded surface that
                // still honours the 3:1 boundary beats a control that
                // disappears when the user changes theme.
                parts.ApplyPlain(fill, null);
                return;
            }

            try
            {
                // ClayProfile is DATA; ClayRecipe is the type that knows
                // how to resolve it — the same call ClayPalette makes.
                // Calling Resolve on the profile read the wrong type
                // for the method, which does not exist there.
                var stack = new ClayRecipe(profile, dark)
                    .Resolve(depth, state, radius, small);
                if (stack is null) return;
                parts.SetEdge(ClayVisualFactory.BuildEdge(
                    profile, fill, dark));
                parts.Apply(stack, fill);
            }
            catch (Exception)
            {
                // A broken recipe is a cosmetic defect; a crash on theme
                // apply is a support ticket. Degrade to a plain surface.
                parts.ApplyPlain(fill, null);
            }
        }
    }
}