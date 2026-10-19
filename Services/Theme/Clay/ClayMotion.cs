// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Clay/ClayMotion.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the press spring
//
// ── WHY THIS FILE EXISTS ──────────────────────────────────────────────
// The clay profile declares a motion curve -- easeOut "0.34,1.56 0.64,1.0"
// -- and every clay control ignored it. Press was an instant XAML
// Trigger, so the "squishy" that the profile's own comment promises
// ("it is the one place overshoot is correct: clay is squishy") was
// configured and never reached the screen.
//
// ── WHY THE SPRING IS BAKED, NOT EVALUATED ───────────────────────────
// WPF has no cubic-bezier easing function. The options were a fixed
// easing class (SpringOut/BackOut approximate the shape but not the
// family profile) or sampling the curve. Sampling is used here: the
// bezier is evaluated once into a short keyframe set, so the motion is
// the ACTUAL declared curve and changing the profile's easing string
// changes the animation rather than requiring a code change.
//
// ── ONLY THE TRANSFORM ANIMATES ──────────────────────────────────────
// Nothing here touches a blur radius, a shadow or a fill. Interpolating
// a DropShadowEffect's BlurRadius forces a re-rasterisation every frame
// for every animated control, which on a scrolling grid is the single
// most expensive mistake available to a soft-UI theme. The mould stays
// still; only the position moves. That is also why the press reads as
// a press and not as a repaint.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

using ProGlassAutomation.Services.Theme.Eight;

namespace ProGlassAutomation.Services.Theme.Clay
{
    /// <summary>Builds the clay press spring. Stateless.</summary>
    public static class ClayMotion
    {
        /// <summary>
        /// The family spring, kept as a fallback. The profile's own value
        /// is preferred; this exists so a synthetic profile built in a
        /// test still animates rather than silently doing nothing.
        /// </summary>
        public const string DefaultEase =
            "0.34,1.56 0.64,1.0";

        /// <summary>Press duration, from the family's fast token.</summary>
        public static TimeSpan PressDuration =>
            TimeSpan.FromMilliseconds(120);

        /// <summary>
        /// True when the press must be instantaneous. Read per call and
        /// never cached: the user can flip the Ease of Access toggle
        /// while the app is running.
        /// </summary>
        public static bool ReducedMotion =>
            EightThemePainter.ReducedMotionRequested;

        /// <summary>
        /// Builds the press animation: scale and translate together, on
        /// one clock, so they cannot drift apart.
        /// </summary>
        /// <param name="scale">The template's ScaleTransform.</param>
        /// <param name="translate">The template's TranslateTransform.</param>
        /// <param name="pressed">Target state.</param>
        /// <param name="ease">
        /// The family's easing string, e.g. "0.34,1.56 0.64,1.0".
        /// Parsed defensively; anything unparseable falls back to the
        /// default spring rather than throwing out of a press handler.
        /// </param>
        public static void Press(
            ScaleTransform scale,
            TranslateTransform translate,
            bool pressed,
            string ease = DefaultEase,
            double pressedScale = 0.98,
            double pressedDepth = 2.0)
        {
            if (scale is null || translate is null) return;

            double toScale = pressed ? pressedScale : 1.0;
            double toY = pressed ? pressedDepth : 0.0;

            if (ReducedMotion)
            {
                // Reduced motion means no tween. The END STATE is still
                // applied -- skipping the movement entirely would leave a
                // control that gives no feedback at all.
                scale.ScaleX = toScale;
                scale.ScaleY = toScale;
                translate.Y = toY;
                return;
            }

            var duration = PressDuration;

            var scaleAnim = Bake(
                scale.ScaleX, toScale, duration, ease);
            var translateAnim = Bake(translate.Y, toY, duration, ease);

            var group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(translate);

            // Freeze the transforms before animating. Animating a
            // Transform that has been frozen elsewhere in the tree throws
            // at the first frame, not at the call site, which makes it a
            // miserable bug to trace back to here.
            if (scale.CanFreeze) scale.Freeze();
            if (translate.CanFreeze) translate.Freeze();

            group.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            group.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            group.BeginAnimation(TranslateTransform.YProperty, translateAnim);
        }

        /// <summary>
        /// Samples a cubic-bezier into a keyframed DoubleAnimation.
        /// <para>
        /// WPF cannot express cubic-bezier, and the alternative -- a
        /// stock SpringOut easing -- ignores the profile entirely, so the
        /// curve declared in EightThemeProfiles would never be the curve
        /// the user sees. Sampling costs one evaluation per keyframe at
        /// build time and buys an animation that IS the declared curve.
        /// </para>
        /// </summary>
        private static DoubleAnimation Bake(
            double from, double to, TimeSpan duration, string ease)
        {
            var anim = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = duration,
                FillBehavior = FillBehavior.Stop,
            };

            double[] xs, ys;
            if (!TryParseBezier(ease, out xs, out ys))
            {
                // Unparseable easing must degrade to something that still
                // moves, never to an exception from a press handler.
                anim.EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseOut,
                };
                return anim;
            }

            // 12 samples is enough to read as a smooth spring and cheap
            // enough that a grid full of these is still a handful of
            // keyframes each.
            const int Samples = 12;
            var frames = new DoubleKeyFrameCollection();
            for (int i = 0; i <= Samples; i++)
            {
                double t = (double)i / Samples;
                frames.Add(new DoubleKeyFrame(
                    from + (to - from) * Evaluate(xs, ys, t),
                    KeyTime.FromPercent(t)));
            }

            anim.KeyFrames = frames;
            return anim;
        }

        /// <summary>
        /// Parses "x1,y1 x2,y2" into control-point arrays.
        /// </summary>
        private static bool TryParseBezier(
            string ease, out double[] xs, out double[] ys)
        {
            xs = null;
            ys = null;
            if (string.IsNullOrWhiteSpace(ease)) return false;

            var parts = ease.Split(
                new[] { ' ', ',', '\t' },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return false;

            var px = new double[2];
            var py = new double[2];
            for (int i = 0; i < 2; i++)
            {
                if (!double.TryParse(parts[i * 2], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out px[i])) return false;
                if (!double.TryParse(parts[i * 2 + 1], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out py[i])) return false;
            }

            xs = px;
            ys = py;
            return true;
        }

        /// <summary>
        /// Evaluates the cubic bezier at t, by solving for the curve
        /// parameter with Newton-Raphson and falling back to bisection.
        /// </summary>
        private static double Evaluate(
            double[] xs, double[] ys, double t)
        {
            // Newton-Raphson converges in a couple of steps for a well
            // behaved easing; the bisection fallback covers the
            // near-flat control points that make it stall.
            double u = t;
            for (int i = 0; i < 8; i++)
            {
                double x = Bezier(u, xs[0], xs[1]);
                double dx = BezierDeriv(u, xs[0], xs[1]);
                if (Math.Abs(dx) < 1e-6) break;
                double err = x - t;
                if (Math.Abs(err) < 1e-5) return Bezier(u, ys[0], ys[1]);
                u -= err / dx;
            }

            double lo = 0, hi = 1;
            u = t;
            for (int i = 0; i < 24; i++)
            {
                double x = Bezier(u, xs[0], xs[1]);
                if (Math.Abs(x - t) < 1e-5) break;
                if (x > t) hi = u; else lo = u;
                u = (lo + hi) / 2;
            }
            return Bezier(u, ys[0], ys[1]);
        }

        private static double Bezier(double u, double p1, double p2) =>
            3 * u * (1 - u) * (1 - u) * p1
            + 3 * u * u * (1 - u) * p2
            + u * u * u;

        private static double BezierDeriv(double u, double p1, double p2) =>
            3 * (1 - u) * (1 - u) * p1
            + 6 * u * (1 - u) * (p2 - p1)
            + 3 * u * u * (1 - p2);
    }
}
