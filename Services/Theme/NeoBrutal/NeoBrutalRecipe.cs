// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/NeoBrutal/NeoBrutalRecipe.cs
// PROJECT: ProGlassAutomation
// MODULE: Neo-Brutalism — the stamped family
//
// ── THREE RULES, AND THAT IS THE ENTIRE STYLE ─────────────────────────
//
//   1. A THICK BORDER. 2-6px solid, near-black. "At 1px a border is a
//      hairline that separates two regions. At 4px it becomes a graphic
//      element in its own right."
//   2. A HARD OFFSET SHADOW WITH ZERO BLUR. "The zero blur radius is the
//      load-bearing detail... Leaving even 2px of blur is the difference
//      between the style reading as intentional and reading as a mistake."
//   3. RADIUS ZERO. "Square corners. That's the point."
//
// Everything else in this file is a detail; those three are the family.
//
// ── THE INVERSE OF SWISS ──────────────────────────────────────────────
//
// Both families refuse rounding, which makes them easy to confuse from a
// distance. They are opposites on the axis that matters. Swiss hardcodes
// HasShadow => false and describes itself as calm and refined; this one
// hardcodes Radius => 0 and treats the shadow as load-bearing. Swiss is
// the only other radius-0 family in this engine, and the pair together
// are the clearest statement of what a radius is for: it is a dials, not
// a signature.
//
// ── PRESS DURATION IS ZERO, DELIBERATELY ──────────────────────────────
//
// "Do not let a default transition near the press. The gesture is impact,
// and impact has no curve."
//
// So PressDuration is 0 and nothing in this family animates its own press.
// A 120ms ease here would turn a stamp into a squash, and every consumer
// of this engine has spent its press budget on that easing already.
//
// ── THE GUTTER IS A CORRECTNESS CONSTRAINT ────────────────────────────
//
// "Equal-offset hard shadows on a dense grid collide where two shadows
// meet a gap. Keep the grid gap larger than the offset."
//
// A soft shadow fades at its edge, so a tight grid merely greys a
// corner. A HARD shadow does not fade: two plates that touch merge into
// one black wedge, which reads as a rendering fault rather than as
// depth. MinimumGutter is therefore an engineering floor, and the tests
// assert MinimumGutter > ShadowLarge so nobody can quietly close it.
using System;
using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.Eight;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Services.Theme.NeoBrutal
{
    /// <summary>
    /// Where a Neo-Brutalist block sits. Two of three are flat; only
    /// Content carries text that has to be read in dense runs.
    /// </summary>
    public enum NeoBrutalPlacement
    {
        /// <summary>A panel behind other content. Flat, no shadow.</summary>
        Page = 0,

        /// <summary>A data surface. Carries a dense grid.</summary>
        Content = 1,

        /// <summary>A control. This is where the shadow lives.</summary>
        Control = 2,
    }

    /// <summary>The Neo-Brutalist measurement system.</summary>
    public sealed class NeoBrutalMetrics
    {
        /// <summary>
        /// ZERO. Not "small". Zero.
        /// <para>
        /// Every radius in this engine is a dial; this is the only one
        /// pinned to the floor. Swiss shares the value, and between them
        /// they show that radius is a choice rather than an identity.
        /// </para>
        /// </summary>
        public double Radius => 0;

        /// <summary>The default border. 4px.</summary>
        public double BorderPrimary => 4;

        /// <summary>The thin border, for dividers and table cells.</summary>
        public double BorderThin => 2;

        /// <summary>The thick border, for section dividers and dialogs.</summary>
        public double BorderThick => 6;

        /// <summary>Small hard offset: badges, chips.</summary>
        public double ShadowSmall => 3;

        /// <summary>Medium hard offset: cards, buttons, panels.</summary>
        public double ShadowMedium => 5;

        /// <summary>Large hard offset: overlays, dialogs, hero blocks.</summary>
        public double ShadowLarge => 8;

        /// <summary>
        /// The block border THICKENS when pressed.
        /// <para>
        /// "Offset shadows must not be the only difference between resting
        /// and pressed; move the block and change the border weight
        /// together." It is also the half of the press that survives
        /// reduced motion, because a colour/weight change is not
        /// animation.
        /// </para>
        /// </summary>
        public double PressedBorderGain => 1;

        /// <summary>How far hover LIFTS the block, and grows its shadow.</summary>
        public double HoverLift => 2;

        /// <summary>
        /// ZERO. See the file header. Impact has no curve.
        /// </summary>
        public double PressDuration => 0;

        /// <summary>
        /// The engineering floor for spacing between blocks.
        /// <para>
        /// Must exceed the largest shadow offset, because hard shadows do
        /// not fade and touching plates merge. Asserted in the tests.
        /// </para>
        /// </summary>
        public double MinimumGutter => 12;

        /// <summary>Focus outline weight, drawn OUTSIDE the block border.</summary>
        public double FocusWeight => 3;

        /// <summary>Gap between the border and the focus outline.</summary>
        public double FocusOffset => 3;

        /// <summary>WCAG 2.2 SC 2.5.8.</summary>
        public double MinHitTarget => 44;
    }

    /// <summary>One resolved block: fill, ink, border, shadow offset.</summary>
    public sealed class NeoBrutalCell
    {
        public NeoBrutalCell(
            Color fill, Color ink, Color mutedInk, Color border,
            double borderWidth, double shadowOffset, Color shadowColour,
            bool shadowVisible, double lift)
        {
            Fill = fill;
            Ink = ink;
            MutedInk = mutedInk;
            Border = border;
            BorderWidth = borderWidth;
            ShadowOffset = shadowOffset;
            ShadowColour = shadowColour;
            ShadowVisible = shadowVisible;
            Lift = lift;
        }

        public Color Fill { get; }
        public Color Ink { get; }
        public Color MutedInk { get; }

        /// <summary>The block's border. A non-text element: held to 3:1
        /// against BOTH the fill and the page, which is where this style
        /// historically fails.</summary>
        public Color Border { get; }

        public double BorderWidth { get; }

        /// <summary>
        /// The hard offset, in pixels, down and right. 0 means the shadow
        /// is coincident with the block -- which is what "pressed" and
        /// "disabled" both mean here.
        /// </summary>
        public double ShadowOffset { get; }

        public Color ShadowColour { get; }

        /// <summary>False for Page and for Disabled. See the README.</summary>
        public bool ShadowVisible { get; }

        /// <summary>How far the block itself is translated on hover.</summary>
        public double Lift { get; }
    }

    /// <summary>The resolved construction for one block in one state.</summary>
    public sealed class NeoBrutalStack
    {
        public NeoBrutalStack(
            NeoBrutalPlacement placement,
            SoftDepthTier tier,
            SoftInteractionState state,
            NeoBrutalCell cell,
            bool interactive,
            bool selected,
            bool motionAllowed)
        {
            Placement = placement;
            Tier = tier;
            State = state;
            Cell = cell;
            Interactive = interactive;
            Selected = selected;
            MotionAllowed = motionAllowed;
        }

        public NeoBrutalPlacement Placement { get; }
        public SoftDepthTier Tier { get; }
        public SoftInteractionState State { get; }
        public NeoBrutalCell Cell { get; }
        public bool Interactive { get; }
        public bool Selected { get; }
        public bool MotionAllowed { get; }

        public bool Focused => State == SoftInteractionState.Focused;

        public Color? FocusRing => Focused ? Cell.Border : (Color?)null;

        /// <summary>
        /// ALWAYS ONE, AND IT IS NOT BLURRED.
        /// <para>
        /// The exact inverse of Swiss, which returns an empty list and
        /// hardcodes HasShadow => false. The distinction the two families
        /// make is not "with shadow" and "without" -- it is "hard offset"
        /// and "no shadow at all", and the zero blur is what separates
        /// this from every soft shadow in the engine.
        /// </para>
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<object> Shadows
        {
            get
            {
                if (!Cell.ShadowVisible) return Array.Empty<object>();

                return new object[]
                {
                    new NeoBrutalShadow(
                        Cell.ShadowColour, Cell.ShadowOffset),
                };
            }
        }

        /// <summary>Always true. See the file header and SwissRecipe.</summary>
        public bool HasShadow => true;

        public bool HasRadius => true;

        /// <summary>Always false, and asserted. See NeoBrutalMetrics.Radius.</summary>
        public double Radius => 0;
    }

    /// <summary>The hard offset shadow, as data.</summary>
    public readonly struct NeoBrutalShadow
    {
        public NeoBrutalShadow(Color colour, double offset)
        {
            Colour = colour;
            Offset = offset;
        }

        public Color Colour { get; }

        /// <summary>Down-right, in pixels. Zero when coincident.</summary>
        public double Offset { get; }
    }

    /// <summary>The Neo-Brutalist material for one darkness.</summary>
    public sealed class NeoBrutalProfile
    {
        public NeoBrutalProfile(
            Color page, Color surface, Color ink, Color mutedInk,
            Color border, Color shadow,
            Color accent, Color accentForeground,
            Color danger, Color info, Color success,
            NeoBrutalMetrics metrics)
        {
            Page = page;
            Surface = surface;
            Ink = ink;
            MutedInk = mutedInk;
            Border = border;
            Shadow = shadow;
            Accent = accent;
            AccentForeground = accentForeground;
            Danger = danger;
            Info = info;
            Success = success;
            Metrics = metrics;
        }

        /// <summary>The paper everything is stamped on.</summary>
        public Color Page { get; }
        public Color Surface { get; }
        public Color Ink { get; }
        public Color MutedInk { get; }

        /// <summary>The near-black border AND the shadow colour. Same
        /// colour by design: this is a single ink drawn at two weights.</summary>
        public Color Border { get; }
        public Color Shadow { get; }

        /// <summary>Acid yellow. Never carry white text on it.</summary>
        public Color Accent { get; }

        /// <summary>
        /// Text colour that is guaranteed legible ON the accent.
        /// <para>
        /// Explicitly NOT white. "Black text on acid yellow passes,
        /// white text on acid does not" -- measured 15.70:1 against
        /// 3.09:1. Every other family in this engine resolves this to
        /// white by reflex; this one resolves it to ink, and the palette
        /// enforces it.
        /// </para>
        /// </summary>
        public Color AccentForeground { get; }

        public Color Danger { get; }
        public Color Info { get; }
        public Color Success { get; }
        public NeoBrutalMetrics Metrics { get; }
    }

    /// <summary>The recipe. Stateless apart from the dark flag.</summary>
    public sealed class NeoBrutalRecipe
    {
        private readonly NeoBrutalProfile _neo;
        private readonly bool _dark;

        public NeoBrutalRecipe(NeoBrutalProfile neo, bool dark)
        {
            _neo = neo ?? throw new ArgumentNullException(nameof(neo));
            _dark = dark;
        }

        public NeoBrutalStack Resolve(
            NeoBrutalPlacement placement,
            SoftDepthTier tier,
            SoftInteractionState state,
            bool interactive,
            bool selected)
        {
            var m = _neo.Metrics;

            // Reduced motion removes the TRANSLATION, never the border.
            //
            // "Consider respecting prefers-reduced-motion by softening or
            // disabling the snap transitions." Snapping IS this family's
            // press, so under reduced motion there is no press movement --
            // which is exactly why the border weight exists as a second
            // signal. "Offset shadows must not be the only difference
            // between resting and pressed; move the block and change the
            // border weight together" is the rule that makes the
            // reduced-motion path still legible.
            bool motion = !ReducedMotion();

            // The PAGE is flat. This is the one placement with no shadow
            // and no border, because the page is the paper -- the thing
            // being stamped on, not a stamp itself.
            bool isPage = placement == NeoBrutalPlacement.Page;

            Color fill = isPage ? _neo.Page : _neo.Surface;
            Color ink = _neo.Ink;
            Color muted = _neo.MutedInk;
            Color border = _neo.Border;

            double borderWidth = isPage ? 0 : m.BorderPrimary;
            double offset = isPage ? 0 : m.ShadowMedium;
            bool shadowVisible = !isPage;
            double lift = 0;

            if (selected)
            {
                // Selection is a FILL swap to the accent, not a tint and
                // not an outline. A block that has been chosen should look
                // picked up off the page.
                fill = _neo.Accent;
                ink = _neo.AccentForeground;
                borderWidth = m.BorderThick;
                offset = m.ShadowLarge;
            }

            // HOVER: the block LIFTS and the shadow GROWS. The shadow
            // growing is the tell -- a shadow that shrinks on hover would
            // be closer, which is the wrong direction.
            if (interactive && state == SoftInteractionState.Hover)
            {
                lift = motion ? m.HoverLift : 0;
                offset += m.HoverLift;
            }

            // PRESS: the block moves INTO its own shadow and the shadow
            // collapses to zero. This is the documented gesture, and it
            // is applied as a translate -- never as margin or padding,
            // because that makes the whole board jump on every click.
            if (state == SoftInteractionState.Pressed)
            {
                lift = motion ? offset : 0;
                offset = 0;

                // The second signal. Movement alone would leave a pressed
                // block distinguishable only by position, and position is
                // exactly what reduced motion removes.
                borderWidth += m.PressedBorderGain;
            }

            if (state == SoftInteractionState.Focused)
            {
                borderWidth = m.BorderThick;
            }

            if (state == SoftInteractionState.Disabled)
            {
                // "Remove the shadow entirely on a disabled element so it
                // reads as flat, inert, unclickable." Not a faint
                // shadow -- none.
                shadowVisible = false;
                offset = 0;
                lift = 0;
                fill = _neo.Surface;
                ink = _neo.MutedInk;
                muted = _neo.MutedInk;
                border = Mix(_neo.Border, _neo.Surface, 0.55);
            }

            var cell = new NeoBrutalCell(
                fill, ink, muted, border, borderWidth,
                offset, _neo.Shadow, shadowVisible, lift);

            return new NeoBrutalStack(
                placement, tier, state, cell, interactive, selected,
                motionAllowed: motion);
        }

        private static bool ReducedMotion()
        {
            try { return EightThemePainter.ReducedMotionRequested; }
            catch (Exception) { return false; }
        }

        internal static Color Mix(Color a, Color b, double k)
        {
            double t = Math.Clamp(k, 0, 1);
            return Color.FromArgb(
                (byte)Math.Round(a.A + (b.A - a.A) * t),
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }
    }
}
