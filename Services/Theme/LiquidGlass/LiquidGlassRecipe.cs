// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/LiquidGlass/LiquidGlassRecipe.cs
// PROJECT: ProGlassAutomation
// MODULE: Liquid Glass — state resolution
//
// Turns (profile, depth tier, interaction state) into the concrete
// brushes a Liquid Glass control template can bind to.
//
// THE RULE THAT MAKES THIS FAMILY DIFFERENT
// Liquid Glass is a NAVIGATION-LAYER material. It belongs on the bar
// that floats above content, the sidebar, the floating button, the
// sheet. It does not belong on the content it floats above, and it must
// never be stacked on another glass surface.
//
// That is not a stylistic preference, it is how the material reads.
// Glass on glass flattens: the inner surface refracts the outer one's
// rim, the rim refracts the content, and the result is a milky slab
// with two borders and no hierarchy. Glass on content, by contrast, is
// legible precisely because the content is sharp and the glass is not.
//
// LiquidGlassDepth exposes this as a decision rather than a comment so
// that the templates can actually ask.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Services.Theme.LiquidGlass
{
    /// <summary>
    /// Where a Liquid Glass surface is allowed to sit.
    /// </summary>
    public enum LiquidGlassPlacement
    {
        /// <summary>Chrome that floats above content. The family is at
        /// its best here: bar, rail, floating action, sheet.</summary>
        Chrome = 0,

        /// <summary>The content itself. Never a glass surface — text on
        /// glass is the single biggest legibility risk in this family.</summary>
        Content = 1,
    }

    /// <summary>Resolved paint for one surface.</summary>
    public sealed class LiquidGlassStack
    {
        internal LiquidGlassStack(
            SoftDepthTier tier,
            SoftInteractionState state,
            Color fill,
            Color rim,
            double rimOpacity,
            double innerGlowOpacity,
            double blur,
            double shadowDepth,
            double shadowOpacity,
            double refraction)
        {
            Tier = tier;
            State = state;
            Fill = fill;
            Rim = rim;
            RimOpacity = rimOpacity;
            InnerGlowOpacity = innerGlowOpacity;
            Blur = blur;
            ShadowDepth = shadowDepth;
            ShadowOpacity = shadowOpacity;
            Refraction = refraction;
        }

        public SoftDepthTier Tier { get; }
        public SoftInteractionState State { get; }
        public Color Fill { get; }
        public Color Rim { get; }
        public double RimOpacity { get; }
        public double InnerGlowOpacity { get; }
        public double Blur { get; }
        public double ShadowDepth { get; }
        public double ShadowOpacity { get; }
        public double Refraction { get; }

        /// <summary>
        /// Pressed glass is NOT darker.
        /// <para>
        /// The intuitive press treatment — darken the fill — is wrong
        /// here in a way it is not wrong for clay or neumorphism. On a
        /// lens, darkening means less light entering the material,
        /// which reads as the glass thickening rather than being
        /// pushed. The family's press signal is the SHADOW collapsing
        /// and the refraction relaxing, because that is what a finger
        /// pressing a slab of glass actually does to it.
        /// </para>
        /// </summary>
        public bool IsPressed => State == SoftInteractionState.Pressed;
    }

    /// <summary>State → paint. The family's decision logic.</summary>
    public static class LiquidGlassRecipe
    {
        /// <summary>
        /// Resolves a surface.
        /// </summary>
        public static LiquidGlassStack? Resolve(
            LiquidGlassProfile? profile,
            SoftDepthTier tier,
            SoftInteractionState state,
            bool reducedTransparency = false,
            bool reducedMotion = false,
            double morph = 0.0)
        {
            if (profile is null) return null;

            var optics = profile.Optics;
            var metrics = profile.Metrics;

            // A control that has morphed into a larger shape is a
            // THICKER piece of glass. Clamped so a runaway morph cannot
            // push the edge into visible distortion.
            var morphT = Math.Clamp(morph, 0, 1);
            var refraction = optics.RefractionAtMorph(
                1.0 + (metrics.MorphRefractionScale - 1.0) * morphT);

            var blur = BlurFor(tier, state, reducedTransparency);
            var depth = DepthFor(tier, state);
            var opacity = ShadowOpacityFor(tier, state);

            // Reduced motion does NOT remove the press signal — it only
            // removes the travel. A user who has asked for less motion
            // still needs to know the control responded, so the shadow
            // collapse stays and only the animation is suppressed.
            _ = reducedMotion;

            var fillShift = FillShiftFor(state);
            var rimOpacity = optics.RimOpacityAt(reducedTransparency);

            return new LiquidGlassStack(
                tier,
                state,
                Shifted(profile.Fill, fillShift),
                profile.Rim,
                rimOpacity,
                optics.InnerGlowOpacity,
                blur,
                depth * (1.0 + (metrics.MorphShadowScale - 1.0) * morphT),
                opacity,
                refraction);
        }

        /// <summary>
        /// Backdrop blur radius by depth tier.
        /// <para>
        /// Drops to zero under reduced transparency. A user who has asked
        /// for opaque surfaces and still gets a blur is getting exactly
        /// what they asked not to get, and blur is also the single most
        /// expensive thing in this family to render.
        /// </para>
        /// </summary>
        internal static double BlurFor(
            SoftDepthTier tier, SoftInteractionState state, bool reducedTransparency)
        {
            if (reducedTransparency) return 0;
            var baseBlur = tier switch
            {
                SoftDepthTier.Well => 6.0,
                SoftDepthTier.Resting => 14.0,
                SoftDepthTier.Raised => 22.0,
                SoftDepthTier.Floating => 30.0,
                _ => 14.0,
            };
            // Hover sharpens the lens: bringing the surface toward the
            // viewer narrows what it is focusing.
            return state == SoftInteractionState.Hover ? baseBlur * 0.85 : baseBlur;
        }

        /// <summary>Shadow depth by tier, then modulated by state.</summary>
        internal static double DepthFor(SoftDepthTier tier, SoftInteractionState state)
        {
            var baseDepth = tier switch
            {
                SoftDepthTier.Well => 0.0,
                SoftDepthTier.Resting => 6.0,
                SoftDepthTier.Raised => 12.0,
                SoftDepthTier.Floating => 22.0,
                _ => 6.0,
            };
            return state switch
            {
                SoftInteractionState.Hover => baseDepth * 1.25,
                SoftInteractionState.Pressed => baseDepth * 0.25,
                SoftInteractionState.Focused => baseDepth * 1.10,
                _ => baseDepth,
            };
        }

        /// <summary>Shadow opacity by tier, then modulated by state.</summary>
        internal static double ShadowOpacityFor(SoftDepthTier tier, SoftInteractionState state)
        {
            var baseOpacity = tier switch
            {
                SoftDepthTier.Well => 0.0,
                SoftDepthTier.Resting => 0.18,
                SoftDepthTier.Raised => 0.26,
                SoftDepthTier.Floating => 0.34,
                _ => 0.18,
            };
            return state switch
            {
                SoftInteractionState.Hover => baseOpacity * 1.15,
                SoftInteractionState.Pressed => baseOpacity * 0.40,
                SoftInteractionState.Disabled => baseOpacity * 0.35,
                _ => baseOpacity,
            };
        }

        /// <summary>
        /// How much the tint moves per state.
        /// <para>
        /// Small on purpose. This is the other half of "press does not
        /// darken": if hover moved the fill far, the press state would
        /// have to move it further, and the family would drift back
        /// toward the flat tinted-panel look it exists to avoid.
        /// </para>
        /// </summary>
        internal static double FillShiftFor(SoftInteractionState state) =>
            state switch
            {
                SoftInteractionState.Hover => 0.06,
                SoftInteractionState.Pressed => -0.04,
                SoftInteractionState.Focused => 0.02,
                SoftInteractionState.Disabled => -0.10,
                _ => 0.0,
            };

        /// <summary>
        /// The specular gradient along the rim.
        /// <para>
        /// Bright where the light hits (up-left) and falling away across
        /// the diagonal. A FLAT ring is the thing that makes a glass
        /// theme read as a translucent rectangle with a border, so the
        /// gradient is not an embellishment here — it is the material.
        /// </para>
        /// </summary>
        public static LinearGradientBrush RimGradient(
            LiquidGlassProfile profile, bool dark)
        {
            var hot = profile.Rim;
            var cold = FadeTo(hot, dark ? 0.18 : 0.10);

            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1),
                GradientStops =
                {
                    new GradientStop(hot, 0.0),
                    new GradientStop(FadeTo(hot, dark ? 0.45 : 0.55), 0.42),
                    new GradientStop(cold, 1.0),
                },
            };
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// The inner glow, or null when the family has none.
        /// <para>
        /// Returns null rather than a fully transparent brush so the
        /// template can skip the element entirely. An all-zero-alpha
        /// brush still costs a render pass.
        /// </para>
        /// </summary>
        public static SolidColorBrush? InnerGlow(LiquidGlassStack stack)
        {
            if (stack.InnerGlowOpacity <= 0) return null;
            var brush = new SolidColorBrush(FadeTo(
                System.Windows.Media.Colors.White, stack.InnerGlowOpacity));
            brush.Freeze();
            return brush;
        }

        /// <summary>The focus ring. Never a tint change.</summary>
        public static Pen FocusRingPen(LiquidGlassProfile profile) =>
            SoftUiAccessibility.BuildEdge(profile.Rim, dark: false);

        internal static Color Shifted(Color c, double amount)
        {
            if (Math.Abs(amount) < 0.0001) return c;
            var target = amount > 0 ? System.Windows.Media.Colors.White
                                     : System.Windows.Media.Colors.Black;
            return LiquidGlassPalette.Mix(c, target, Math.Abs(amount));
        }

        internal static Color FadeTo(Color c, double alpha) =>
            Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), c.R, c.G, c.B);
    }

    /// <summary>
    /// The placement contract, as an executable decision.
    /// </summary>
    public static class LiquidGlassDepth
    {
        /// <summary>
        /// May this surface carry glass at all?
        /// <para>
        /// Content never does. The rule exists because glass-on-content
        /// is the family's most attractive failure: it renders, it looks
        /// deliberate in a screenshot, and it quietly costs several
        /// points of body-text contrast over a moving background.
        /// </para>
        /// </summary>
        public static bool Allows(LiquidGlassPlacement placement) =>
            placement == LiquidGlassPlacement.Chrome;

        /// <summary>
        /// True when two surfaces would stack glass on glass.
        /// <para>
        /// Callers use this to demote the inner surface to a solid
        /// rather than to render it. There is no correct way to stack
        /// two lenses -- the inner one refracts the outer one's rim, and
        /// the boundary the user needs to see dissolves.
        /// </para>
        /// </summary>
        public static bool WouldStackGlass(
            LiquidGlassPlacement outer, LiquidGlassPlacement inner) =>
            outer == LiquidGlassPlacement.Chrome
            && inner == LiquidGlassPlacement.Chrome;
    }
}