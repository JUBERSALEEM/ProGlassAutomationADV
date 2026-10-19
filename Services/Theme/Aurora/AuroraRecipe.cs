// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Aurora/AuroraRecipe.cs
// PROJECT: ProGlassAutomation
// MODULE: Aurora — the backdrop-driven family
//
// ── THE ONE RULE NO OTHER FAMILY HAS ──────────────────────────────────
//
// The other seven families are surface-first: the control is the subject
// and the backdrop is stage dressing. Aurora inverts that. The sky is
// built first, and every control is described relative to where it sits in
// front of it.
//
// Which produces a rule none of them need:
//
//     THE MESH NEVER TOUCHES A CONTROL.
//
// Not on a card, not on a button, not on a row. And that is not a taste
// rule -- it is the identical failure that disqualified glassmorphism,
// arriving from the other direction. Glassmorphism broke because the
// backdrop moved behind the text. Aurora's mesh would break for the same
// reason: the colours under a label shift, so the contrast under that
// label shifts with them, and WCAG 1.4.3 demands 4.5:1 at the WORST
// moment, not on average.
//
// So AuroraPlacement exists, and it is the family:
//
//     Backdrop   the sky itself. Carries no text, ever.
//     Panel      a KPI card. Translucent enough to feel the sky.
//     Content    a data table. Held opaque. This is where reading happens.
//     Chrome     nav rail, status strip.
//
// ── WHY THERE IS NO BlurEffect ANYWHERE IN THIS FILE ──────────────────
//
// The sources blur their blobs with `filter: blur(60-80px)` because a CSS
// div has a hard edge that needs softening. A WPF RadialGradientBrush
// that fades to a transparent stop is ALREADY soft -- the blur is
// structural, not applied. So AuroraMesh builds soft falloff in the brush
// and never instantiates a BlurEffect.
//
// That is not a micro-optimisation. It is what makes rule 5 of the README
// possible: drift animates TRANSFORMS, and a transform on an un-blurred,
// un-effect-ed element is composited by the GPU rather than re-rasterised.
//
// ── NO SHADOW, LIKE GLASSMORPHISM ─────────────────────────────────────
//
// AuroraStack.Shadows is empty and HasShadow is false. There is no
// elevation ladder here to encode: the depth you perceive is the aurora
// behind the panel, not an object sitting at a height. A drop shadow
// would compete with the sky for the job the sky is already doing.
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ProGlassAutomation.Services.Theme.Eight;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Services.Theme.Aurora
{
    /// <summary>Where an Aurora surface is allowed to sit.</summary>
    public enum AuroraPlacement
    {
        /// <summary>The sky. No text, ever.</summary>
        Backdrop = 0,

        /// <summary>A card. Translucent; the sky is visible through it.</summary>
        Panel = 1,

        /// <summary>A reading surface. Held opaque by construction.</summary>
        Content = 2,

        /// <summary>Nav rail, status strip. Where the translucency lives.</summary>
        Chrome = 3,
    }

    /// <summary>The Aurora measurement system.</summary>
    public sealed class AuroraMetrics
    {
        /// <summary>
        /// THREE, and it is not a hint.
        /// <para>
        /// "Three blobs is the minimum for 'aurora' -- two reads as a
        /// gradient, three reads as weather." Fewer is not a sparser
        /// version of this family, it is a different one.
        /// </para>
        /// </summary>
        public int BlobCount => 3;

        /// <summary>
        /// 42 seconds for one full drift cycle.
        /// <para>
        /// "Animate at glacial speed. If a user can consciously track the
        /// movement, it's too fast. Think 20-60 second loops minimum."
        /// 42 sits mid-band: long enough that the sky is never legible as
        /// motion, short enough that two screens apart still feel related.
        /// </para>
        /// </summary>
        public double DriftPeriod => 42;

        /// <summary>Corner radius, 20px. Pills override this to half height.</summary>
        public double Radius => 20;

        /// <summary>
        /// The rim. Always drawn -- see the README's accessibility note.
        /// </summary>
        public double RimThickness => 1;

        /// <summary>
        /// A Panel's alpha.
        /// <para>
        /// Low enough that the sky genuinely reads through the card,
        /// which is the whole point. The contrast risk is paid for
        /// downstream: ink is measured against the WORST backdrop in
        /// AuroraPalette.EnsureWorstCase, never against this average.
        /// </para>
        /// </summary>
        public double PanelOpacity => 0.55;

        /// <summary>
        /// A Content surface's alpha floor.
        /// <para>
        /// Held far higher than Panel, and for the same reason
        /// GlassPalette holds content at 0.78: this is where people
        /// read dense text, and the sky behind it never stops moving.
        /// </para>
        /// </summary>
        public double ContentOpacity => 0.86;

        /// <summary>The chrome alpha, for the nav rail.</summary>
        public double ChromeOpacity => 0.42;

        /// <summary>
        /// Accent glow. RESERVED.
        /// <para>
        /// Glow marks the primary action and the active nav item, and
        /// nothing else. A family where every surface glows has no
        /// hierarchy left; a family where two do has a starting point.
        /// </para>
        /// </summary>
        public double GlowOpacity => 0.35;

        /// <summary>Blur radius for the accent glow halo.</summary>
        public double GlowRadius => 18;

        /// <summary>Press dip. Matches Clay's 0.98 -- the gesture is shared.</summary>
        public double PressScale => 0.98;

        /// <summary>Press duration, ms.</summary>
        public double MotionCeiling => 120;

        /// <summary>The blob field's spread, as a fraction of the host.</summary>
        public double BlobSpread => 1.35;
    }

    /// <summary>One resolved surface: fill, ink, rim, glow.</summary>
    public sealed class AuroraCell
    {
        public AuroraCell(
            Color fill, Color ink, Color mutedInk,
            Color rimLow, Color rimHigh, double rimThickness,
            Color glow, double glowOpacity, bool translucent)
        {
            Fill = fill;
            Ink = ink;
            MutedInk = mutedInk;
            RimLow = rimLow;
            RimHigh = rimHigh;
            RimThickness = rimThickness;
            Glow = glow;
            GlowOpacity = glowOpacity;
            Translucent = translucent;
        }

        public Color Fill { get; }
        public Color Ink { get; }
        public Color MutedInk { get; }

        /// <summary>
        /// The rim's two ends. Aurora's signature is that the rim is a
        /// GRADIENT, teal to violet, echoing the field's own orbit --
        /// and it is what keeps a translucent panel findable while the
        /// sky moves behind it.
        /// </summary>
        public Color RimLow { get; }
        public Color RimHigh { get; }
        public double RimThickness { get; }

        public Color Glow { get; }
        public double GlowOpacity { get; }

        /// <summary>False when placement or high contrast forced opaque.</summary>
        public bool Translucent { get; }
    }

    /// <summary>The resolved construction for one surface in one state.</summary>
    public sealed class AuroraStack
    {
        public AuroraStack(
            AuroraPlacement placement,
            SoftDepthTier tier,
            SoftInteractionState state,
            AuroraCell cell,
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

        public AuroraPlacement Placement { get; }
        public SoftDepthTier Tier { get; }
        public SoftInteractionState State { get; }
        public AuroraCell Cell { get; }
        public bool Interactive { get; }
        public bool Selected { get; }
        public bool MotionAllowed { get; }

        public bool Focused => State == SoftInteractionState.Focused;

        public Color? FocusRing => Focused ? Cell.Ink : (Color?)null;

        /// <summary>Always empty. See the file header.</summary>
        public System.Collections.Generic.IReadOnlyList<object> Shadows { get; } =
            Array.Empty<object>();

        public bool HasShadow => false;
        public bool HasRadius => true;
    }

    /// <summary>The Aurora material for one darkness.</summary>
    public sealed class AuroraProfile
    {
        public AuroraProfile(
            Color baseColor,
            Color blobIndigo, Color blobViolet, Color blobTeal,
            Color ink, Color mutedInk,
            Color rimLow, Color rimHigh,
            Color accent, Color accentForeground,
            Color glow, AuroraMetrics metrics)
        {
            Base = baseColor;
            BlobIndigo = blobIndigo;
            BlobViolet = blobViolet;
            BlobTeal = blobTeal;
            Ink = ink;
            MutedInk = mutedInk;
            RimLow = rimLow;
            RimHigh = rimHigh;
            Accent = accent;
            AccentForeground = accentForeground;
            Glow = glow;
            Metrics = metrics;
        }

        /// <summary>The void. Never white -- see README rule 1.</summary>
        public Color Base { get; }

        /// <summary>The dominant hue. 55% of the field; see rule 3.</summary>
        public Color BlobIndigo { get; }

        /// <summary>Orbiting colour, centre-right.</summary>
        public Color BlobViolet { get; }

        /// <summary>Orbiting colour, lower-right.</summary>
        public Color BlobTeal { get; }

        public Color Ink { get; }
        public Color MutedInk { get; }
        public Color RimLow { get; }
        public Color RimHigh { get; }
        public Color Accent { get; }
        public Color AccentForeground { get; }
        public Color Glow { get; }
        public AuroraMetrics Metrics { get; }
    }

    /// <summary>The recipe. Stateless apart from the dark flag.</summary>
    public sealed class AuroraRecipe
    {
        private readonly AuroraProfile _aurora;
        private readonly bool _dark;

        public AuroraRecipe(AuroraProfile aurora, bool dark)
        {
            _aurora = aurora ?? throw new ArgumentNullException(nameof(aurora));
            _dark = dark;
        }

        public AuroraStack Resolve(
            AuroraPlacement placement,
            SoftDepthTier tier,
            SoftInteractionState state,
            bool interactive,
            bool selected)
        {
            var m = _aurora.Metrics;

            // ── PLACEMENT DECIDES OPACITY, AND BACKDROP DECIDES NOTHING ──
            //
            // Backdrop has no ink and no rim: it is sky. Content is held
            // opaque because that is where dense text is read against a
            // background that never stops drifting.
            bool forcedOpaque =
                placement == AuroraPlacement.Backdrop
                || placement == AuroraPlacement.Content
                || AuroraPalette.TransparencySuppressed;

            double alpha = placement switch
            {
                AuroraPlacement.Backdrop => 1.0,
                AuroraPlacement.Content => m.ContentOpacity,
                AuroraPlacement.Chrome => m.ChromeOpacity,
                _ => m.PanelOpacity,
            };

            Color baseFill = _aurora.Base;

            // Hover LIGHTENS the panel rather than moving it. The sky
            // behind belongs to the app, not to the card -- sliding the
            // card would slide the sky with it, which is the one motion
            // this family never performs.
            if (interactive && state == SoftInteractionState.Hover)
                alpha = Math.Min(1.0, alpha + 0.10);

            // Press deepens: a pressed thing moves away from you. Shared
            // across every translucent family in this engine.
            if (state == SoftInteractionState.Pressed)
                alpha = Math.Min(1.0, alpha + 0.06);

            Color fill = WithAlpha(baseFill, alpha);

            Color ink = _aurora.Ink;
            Color muted = _aurora.MutedInk;
            Color rimLow = _aurora.RimLow;
            Color rimHigh = _aurora.RimHigh;
            double rimWidth = m.RimThickness;

            Color glow = _aurora.Glow;
            double glowOpacity = 0.0;

            if (state == SoftInteractionState.Disabled)
            {
                fill = Mix(fill, _aurora.Base, 0.45);
                rimLow = Mix(rimLow, _aurora.Base, 0.45);
                rimHigh = Mix(rimHigh, _aurora.Base, 0.45);
                muted = Mix(muted, _aurora.Base, 0.45);
            }

            if (state == SoftInteractionState.Hover && interactive)
                glowOpacity = m.GlowOpacity * 0.45;

            if (state == SoftInteractionState.Pressed)
                glowOpacity = m.GlowOpacity * 0.30;

            // Selected and focused both brighten the rim and grow it by a
            // pixel. Never both by two -- a 2px rim reads as a border
            // style choice, and the focus cue has to be unmistakable.
            if (selected)
            {
                rimLow = _aurora.Accent;
                rimHigh = Mix(_aurora.Accent, _aurora.BlobTeal, 0.5);
                rimWidth = m.RimThickness + 1;
                glowOpacity = Math.Max(glowOpacity, m.GlowOpacity * 0.7);
            }

            if (state == SoftInteractionState.Focused)
            {
                rimLow = _aurora.Accent;
                rimHigh = Mix(_aurora.Accent, _aurora.BlobViolet, 0.5);
                rimWidth = m.RimThickness + 1;
            }

            if (state == SoftInteractionState.Disabled)
                glowOpacity = 0.0;

            var cell = new AuroraCell(
                fill, ink, muted, rimLow, rimHigh, rimWidth,
                glow, glowOpacity, translucent: !forcedOpaque);

            return new AuroraStack(
                placement, tier, state, cell, interactive, selected,
                motionAllowed: !ReducedMotion());
        }

        private static bool ReducedMotion()
        {
            try { return EightThemePainter.ReducedMotionRequested; }
            catch (Exception) { return false; }
        }

        internal static Color WithAlpha(Color c, double alpha) =>
            Color.FromArgb(
                (byte)Math.Round(Math.Clamp(alpha, 0.0, 1.0) * 255),
                c.R, c.G, c.B);

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

    /// <summary>Where each of the three blobs sits and how strongly.</summary>
    public readonly struct AuroraBlobSpec
    {
        public AuroraBlobSpec(
            double cx, double cy, double radius, double strength,
            TimeSpan phase)
        {
            Cx = cx; Cy = cy; Radius = radius;
            Strength = strength; Phase = phase;
        }

        /// <summary>Centre, as a fraction of the host.</summary>
        public double Cx { get; }
        public double Cy { get; }
        public double Radius { get; }

        /// <summary>Alpha at the blob's core.</summary>
        public double Strength { get; }

        /// <summary>Offset into the drift cycle, so they are never in step.</summary>
        public TimeSpan Phase { get; }
    }

    /// <summary>
    /// The sky. Builds the base wash and the three blobs, and drifts them
    /// on transforms only.
    /// </summary>
    public static class AuroraMesh
    {
        /// <summary>
        /// The three blobs.
        /// <para>
        /// Indigo at 0.26,0.22 and strength 0.62 is the ANCHOR, and it
        /// carries 0.62 of a 1.16 total -- 53% of the field weight, per
        /// README rule 3 ("never give three colours equal weight or you
        /// get mud"). The orbit is deliberately restrained at 0.32 and
        /// 0.22: three strong fields read as three gradients, not as
        /// weather.
        ///
        /// Violet and teal sit on OPPOSITE sides of the field, so the
        /// composition reads as a diagonal rather than a left-to-right
        /// ramp, and their phases are 0/14s/28s so they never arrive
        /// anywhere together.
        /// </para>
        /// </summary>
        public static IReadOnlyList<AuroraBlobSpec> Blobs { get; } =
            new[]
            {
                new AuroraBlobSpec(0.26, 0.22, 0.85, 0.62, TimeSpan.Zero),
                new AuroraBlobSpec(0.74, 0.30, 0.70, 0.32,
                    TimeSpan.FromSeconds(14)),
                new AuroraBlobSpec(0.70, 0.84, 0.65, 0.22,
                    TimeSpan.FromSeconds(28)),
            };

        /// <summary>The deep wash under the blobs.</summary>
        public static LinearGradientBrush BaseWash(AuroraProfile p, bool dark)
        {
            // Not a flat fill: a flat fill has nothing for the blobs to
            // modulate, and the falloff at the bottom edge is what stops
            // the sky reading as a texture rather than a void.
            var top = dark ? p.Base : Mix(p.Base, Colors.Black, 0.35);
            var bottom = Mix(p.Base, Colors.Black, dark ? 0.55 : 0.70);

            var b = new LinearGradientBrush(
                Color.FromArgb(255, top.R, top.G, top.B),
                Color.FromArgb(255, bottom.R, bottom.G, bottom.B),
                new Point(0.2, 0.0),
                new Point(0.8, 1.0));
            b.Freeze();
            return b;
        }

        /// <summary>
        /// One blob's fill: a soft radial falloff to fully transparent.
        /// <para>
        /// This IS the blur. A CSS author needs `filter: blur(70px)`
        /// because their div has a hard rectangular edge; a gradient that
        /// reaches alpha 0 by 100% has no edge at all. See the file header
        /// for why that difference is worth more than it looks.
        /// </para>
        /// </summary>
        public static RadialGradientBrush BlobBrush(Color colour, double strength)
        {
            double s = Math.Clamp(strength, 0.0, 1.0);

            var b = new RadialGradientBrush();
            b.GradientStops.Add(new GradientStop(
                AuroraRecipe.WithAlpha(colour, s), 0.0));
            b.GradientStops.Add(new GradientStop(
                AuroraRecipe.WithAlpha(colour, s * 0.55), 0.32));
            b.GradientStops.Add(new GradientStop(
                AuroraRecipe.WithAlpha(colour, s * 0.18), 0.66));
            b.GradientStops.Add(new GradientStop(
                AuroraRecipe.WithAlpha(colour, 0.0), 1.0));

            b.Center = new Point(0.5, 0.5);
            b.RadiusX = 0.5;
            b.RadiusY = 0.5;
            b.GradientUnits = GradientUnits.ObjectBoundingBox;
            b.Freeze();
            return b;
        }

        /// <summary>
        /// A drift transform for one blob, or null when motion is off.
        /// <para>
        /// Returns a composed <see cref="TransformGroup"/> so the caller
        /// only has to hand it to RenderTransform. When
        /// <paramref name="motionAllowed"/> is false it returns null and
        /// the caller leaves the blob at its composed resting pose --
        /// which is a designed still, not a degraded one.
        /// </para>
        /// </summary>
        public static TransformGroup? Drift(
            int index, AuroraMetrics metrics, bool motionAllowed)
        {
            var spec = Blobs[index];
            if (!motionAllowed) return null;

            var period = TimeSpan.FromSeconds(metrics.DriftPeriod);

            // Phase offset via BeginTime, so all three share one cycle
            // length but never arrive anywhere together.
            TimeSpan begin = TimeSpan.FromTicks(
                period.Ticks * spec.Phase.TotalSeconds
                / Math.Max(1.0, metrics.DriftPeriod));

            // Amplitude scales with the blob, so the big indigo field
            // moves least. Small teal travels furthest.
            double amp = 0.10 - 0.02 * index;

            var translate = new TranslateTransform();
            var scale = new ScaleTransform();
            var group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(translate);
            group.Freeze();

            var moveX = new DoubleAnimation(0, -amp * 60,
                period, FillBehavior.HoldEnd)
            {
                BeginTime = begin,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            };
            var moveY = new DoubleAnimation(0, amp * 40,
                period, FillBehavior.HoldEnd)
            {
                BeginTime = begin,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            };
            var breathe = new DoubleAnimation(
                1.0, 1.0 + 0.06 * (1.0 - 0.3 * index),
                period, FillBehavior.HoldEnd)
            {
                BeginTime = begin,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            };

            // Transform properties only. No GradientStop is ever
            // animated -- that would re-rasterise the sky every frame,
            // which is the single most expensive mistake available here.
            translate.BeginAnimation(TranslateTransform.XProperty, moveX);
            translate.BeginAnimation(TranslateTransform.YProperty, moveY);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, breathe);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, breathe);

            return group;
        }
    }
}
