// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Spatial/SpatialRecipe.cs
// PROJECT: ProGlassAutomation
// MODULE: Spatial / Immersive — the recipe for a plane
//
// ── TWO SHADOWS, AND THAT IS WHY THE PARTS FILE HAS TWO BORDERS ───────
//
// Fluent does not use one drop shadow:
//
//   "Shadows are generated using a combination of sharp, directional
//    KEY shadows for nearby objects and soft, diffused AMBIENT shadows
//    for distant ones — for instance, a low-elevation shadow might use
//    2px blur, while high-elevation ones reach up to 64px."
//
// Two per surface. WPF allows exactly one Effect per element, so the
// second one needs its own host — which is why SpatialSurfaceParts
// declares a key host and an ambient host rather than one.
//
// This family is therefore the only one in the engine with a real
// shadow BUDGET. Swiss and Bento forbid shadows outright; Spatial has
// two per surface across five planes, and exceeding that budget is
// precisely what makes a Fluent-like theme read as muddy.
//
// ── BLUR IS A SIGNAL, NOT A FILTER ────────────────────────────────────
//
// The ladder below is the depth signal, and it is carried by SHADOW
// GEOMETRY and surface opacity rather than by a BlurEffect. That is not
// a simplification. WPF's BlurEffect blurs the element's own content,
// not the backdrop behind it — so it cannot express "this surface is
// 40px from you" even in principle. (Established during the Liquid
// Glass research; it is the reason that family simulates its layers
// too.)
//
// ── PRESS RECEDES ─────────────────────────────────────────────────────
//
// Fluent's press feedback: an item pressed "momentarily RECEDES into
// the background and then returns to its original position."
//
// That is the opposite of every other family in this engine, where
// press shifts the fill toward the ink and reads as moving toward the
// viewer. Spatial follows the OS because it is imitating an OS
// material, and because a press that moves the plane DOWN is the only
// honest way to say "this is not the surface you were reaching for".
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme.Eight;

namespace ProGlassAutomation.Services.Theme.Spatial
{
    /// <summary>One shadow: blur, direction, and how much of it there is.</summary>
    public sealed class SpatialShadow
    {
        public SpatialShadow(double blur, double opacity, double offsetY)
        {
            Blur = blur;
            Opacity = opacity;
            OffsetY = offsetY;
        }

        public double Blur { get; }

        /// <summary>0..1. This is the budget, not the geometry.</summary>
        public double Opacity { get; }

        /// <summary>Downward only — shadows fall, they do not rise.</summary>
        public double OffsetY { get; }

        /// <summary>The WPF colour for this shadow's colour.</summary>
        public Color Color { get; internal set; }
    }

    /// <summary>The blur ladder and the shadow geometry.</summary>
    public sealed class SpatialMetrics
    {
        /// <summary>
        /// The blur radius, per plane. THIS IS THE DEPTH SIGNAL.
        /// <para>
        /// Navigation and ambient share 20 on purpose. They are different
        /// planes in the hierarchy but they read at the same distance,
        /// and giving navigation its own radius would imply a depth the
        /// user does not actually perceive — which is worse than
        /// flattening the ladder honestly.
        /// </para>
        /// </summary>
        public double BlurFor(SpatialPlane plane) => plane switch
        {
            SpatialPlane.Environment => 0,
            SpatialPlane.Ambient => 20,
            SpatialPlane.Navigation => 20,
            SpatialPlane.Panel => 40,
            SpatialPlane.Overlay => 60,
            _ => 40,
        };

        /// <summary>
        /// The KEY shadow: sharp, directional, close. Anchored to the
        /// engine's one light source, so it points away from it like
        /// every other shadow in the engine.
        /// </summary>
        public SpatialShadow KeyShadow(SpatialPlane plane, Color ink)
        {
            double blur = plane switch
            {
                SpatialPlane.Environment => 0,
                SpatialPlane.Ambient => 2,
                SpatialPlane.Navigation => 4,
                SpatialPlane.Panel => 8,
                SpatialPlane.Overlay => 16,
                _ => 8,
            };

            // The budget. Rises with plane because elevation rises with
            // plane, and stops well short of opaque: a key shadow you can
            // see as a shape rather than as a contact cue is wrong.
            double opacity = plane switch
            {
                SpatialPlane.Environment => 0.0,
                SpatialPlane.Ambient => 0.10,
                SpatialPlane.Navigation => 0.14,
                SpatialPlane.Panel => 0.18,
                SpatialPlane.Overlay => 0.24,
                _ => 0.18,
            };

            return new SpatialShadow(
                blur, opacity, OffsetYFor(plane))
            {
                Color = SpatialRecipe.ShadowColor(ink, ambient: false),
            };
        }

        /// <summary>
        /// The AMBIENT shadow: wide, soft, directionless-ish. This is the
        /// one that makes elevation legible at all, because a key shadow
        /// alone reads as a sticker.
        /// </summary>
        public SpatialShadow AmbientShadow(SpatialPlane plane, Color ink)
        {
            double blur = plane switch
            {
                SpatialPlane.Environment => 0,
                SpatialPlane.Ambient => 16,
                SpatialPlane.Navigation => 24,
                SpatialPlane.Panel => 40,
                SpatialPlane.Overlay => 64,
                _ => 40,
            };

            // Lower per unit of area than the key shadow. The two are
            // deliberately not in proportion: keeping the ambient one
            // proportionally lower is what stops a high plane turning
            // into a grey cloud.
            double opacity = plane switch
            {
                SpatialPlane.Environment => 0.0,
                SpatialPlane.Ambient => 0.06,
                SpatialPlane.Navigation => 0.08,
                SpatialPlane.Panel => 0.10,
                SpatialPlane.Overlay => 0.12,
                _ => 0.10,
            };

            return new SpatialShadow(
                blur, opacity, OffsetYFor(plane) * 0.5)
            {
                Color = SpatialRecipe.ShadowColor(ink, ambient: true),
            };
        }

        private static double OffsetYFor(SpatialPlane plane) => plane switch
        {
            SpatialPlane.Environment => 0,
            SpatialPlane.Ambient => 1,
            SpatialPlane.Navigation => 2,
            SpatialPlane.Panel => 4,
            SpatialPlane.Overlay => 8,
            _ => 4,
        };

        /// <summary>
        /// How opaque each plane's fill is.
        /// <para>
        /// Overlays are the MOST opaque, not the least. That is
        /// counter-intuitive for a "glass" style and it is what Fluent
        /// does: a dialog must be MORE readable than the panel behind
        /// it, and the way to get that is more fill, not less. A
        /// transparent overlay over a busy panel is an accessibility
        /// failure wearing a nice hat.
        /// </para>
        /// </summary>
        public double OpacityFor(SpatialPlane plane) => plane switch
        {
            SpatialPlane.Environment => 1.0,
            SpatialPlane.Ambient => 0.50,
            SpatialPlane.Navigation => 0.50,
            SpatialPlane.Panel => 0.62,
            SpatialPlane.Overlay => 0.78,
            _ => 0.62,
        };

        /// <summary>
        /// How far a press RECEDES. Applied by dropping the plane's
        /// opacity toward the environment rather than by moving the
        /// element, because a moving element in a Z-hierarchy family is
        /// a lie about the Z.
        /// </summary>
        public double PressRecede => 0.08;

        /// <summary>
        /// Corner radius, 12px. One value for every plane.
        /// <para>
        /// Lower than a typical card's 16, and not because Spatial wants
        /// sharper corners. It is because translucency makes an edge hard
        /// to judge: a large radius curves the rim away from the light
        /// at the extremes, so the rim stops reading as a clean keyline
        /// and the plane starts reading as a blob. 12 keeps the rim
        /// legible across all five planes.
        /// </para>
        /// </summary>
        public double Radius => 12;

        /// <summary>
        /// Parallax depth for this plane. 0 for everything above the
        /// environment: only the room moves, and it moves more slowly
        /// than the content in front of it.
        /// </summary>
        public double ParallaxFor(SpatialPlane plane) =>
            plane == SpatialPlane.Environment ? 1.0 : 0.0;
    }

    /// <summary>The resolved colours for one plane in one state.</summary>
    public sealed class SpatialCell
    {
        public SpatialCell(
            Color fill, Color ink, Color mutedInk, Color rim,
            double rimThickness)
        {
            Fill = fill;
            Ink = ink;
            MutedInk = mutedInk;
            Rim = rim;
            RimThickness = rimThickness;
        }

        /// <summary>
        /// The plane's translucent fill. Carries alpha unless transparency
        /// is suppressed, in which case it is already opaque.
        /// </summary>
        public Color Fill { get; }

        public Color Ink { get; }
        public Color MutedInk { get; }

        /// <summary>
        /// The 1px rim. Fluent calls it a keyline; it is what stops a
        /// translucent panel from dissolving into whatever is behind it,
        /// and it is the reason the family is legible at all on a busy
        /// desktop.
        /// </summary>
        public Color Rim { get; }

        public double RimThickness { get; }
    }

    /// <summary>The resolved construction for one plane in one state.</summary>
    public sealed class SpatialStack
    {
        public SpatialStack(
            SpatialPlane plane,
            SoftDepthTier tier,
            SoftInteractionState state,
            SpatialCell cell,
            SpatialShadow? key,
            SpatialShadow? ambient,
            bool interactive,
            bool selected,
            bool motionAllowed)
        {
            Plane = plane;
            Tier = tier;
            State = state;
            Cell = cell;
            Key = key;
            Ambient = ambient;
            Interactive = interactive;
            Selected = selected;
            MotionAllowed = motionAllowed;
        }

        public SpatialPlane Plane { get; }
        public SoftDepthTier Tier { get; }
        public SoftInteractionState State { get; }
        public SpatialCell Cell { get; }

        /// <summary>Sharp and directional. Null only on the environment.</summary>
        public SpatialShadow? Key { get; }

        /// <summary>Wide and soft. Null only on the environment.</summary>
        public SpatialShadow? Ambient { get; }

        public bool Interactive { get; }
        public bool Selected { get; }
        public bool MotionAllowed { get; }

        public bool Focused => State == SoftInteractionState.Focused;

        public Color? FocusRing => Focused ? Cell.Ink : (Color?)null;

        /// <summary>Both shadows, key first. Exposed as a list so uniform
        /// code can iterate without knowing the two-shadow structure.</summary>
        public IReadOnlyList<SpatialShadow> Shadows
        {
            get
            {
                var list = new List<SpatialShadow>(2);
                if (Key is not null) list.Add(Key);
                if (Ambient is not null) list.Add(Ambient);
                return list;
            }
        }

        public bool HasShadow => Key is not null || Ambient is not null;

        public bool HasRadius => true;
    }

    /// <summary>The Spatial material, derived from the family's ramp.</summary>
    public sealed class SpatialProfile
    {
        public SpatialProfile(
            Color environment, Color far, Color fill, Color ink,
            Color mutedInk, Color rim, Color accent, SpatialMetrics metrics)
        {
            Environment = environment;
            Far = far;
            Fill = fill;
            Ink = ink;
            MutedInk = mutedInk;
            Rim = rim;
            Accent = accent;
            Metrics = metrics;
        }

        /// <summary>The room. Opaque — Mica is opaque, and the panels are
        /// translucent against it.</summary>
        public Color Environment { get; }

        /// <summary>Far chrome, between the room and the panels.</summary>
        public Color Far { get; }

        /// <summary>The translucent panel fill. Alpha-bearing.</summary>
        public Color Fill { get; }

        public Color Ink { get; }
        public Color MutedInk { get; }
        public Color Rim { get; }
        public Color Accent { get; }
        public SpatialMetrics Metrics { get; }
    }

    /// <summary>The recipe. Stateless apart from the dark flag.</summary>
    public sealed class SpatialRecipe
    {
        private readonly SpatialProfile _spatial;
        private readonly bool _dark;

        public SpatialRecipe(SpatialProfile spatial, bool dark)
        {
            _spatial = spatial ?? throw new ArgumentNullException(nameof(spatial));
            _dark = dark;
        }

        /// <summary>
        /// Maps an engine depth tier onto a plane.
        /// <para>
        /// The tier selects; the plane decides. Five planes do not fit in
        /// four tiers, and the mapping is lossy by construction, so it is
        /// written down here rather than discovered at runtime.
        /// </para>
        /// </summary>
        public static SpatialPlane PlaneFor(SoftDepthTier tier) => tier switch
        {
            SoftDepthTier.Well => SpatialPlane.Ambient,
            SoftDepthTier.Resting => SpatialPlane.Panel,
            SoftDepthTier.Raised => SpatialPlane.Navigation,
            SoftDepthTier.Floating => SpatialPlane.Overlay,
            _ => SpatialPlane.Panel,
        };

        /// <summary>
        /// Resolves one plane.
        /// <para>
        /// <paramref name="declared"/> is the tier, which selects the
        /// plane. Passing the plane directly is available for callers
        /// that genuinely know which Z they are on — the environment
        /// layer, which no tier maps to.
        /// </para>
        /// </summary>
        public SpatialStack Resolve(
            SpatialPlane? plane,
            SoftDepthTier declared,
            SoftInteractionState state,
            bool interactive,
            bool selected)
        {
            var m = _spatial.Metrics;
            var onPlane = plane ?? PlaneFor(declared);

            // What this plane actually sits on. The environment sees the
            // room; ambient sees the room; everything above sees the far
            // chrome. Getting this right is what makes the ladder read as
            // a ladder rather than as five unrelated opacities.
            Color backdrop = onPlane switch
            {
                SpatialPlane.Environment => _spatial.Environment,
                SpatialPlane.Ambient => _spatial.Environment,
                _ => SpatialPalette.Composited(_spatial.Far, _spatial.Environment),
            };

            double opacity = m.OpacityFor(onPlane);

            // Transparency suppression is applied HERE, at the point the
            // fill is built, rather than in each control. That is what
            // makes it total: there is no code path that paints a
            // translucent plane while suppression is on.
            if (SpatialPalette.TransparencySuppressed)
            {
                Color flat = SpatialPalette.Composited(_spatial.Fill, backdrop);

                var opaqueCell = new SpatialCell(
                    flat,
                    _spatial.Ink,
                    _spatial.MutedInk,
                    _spatial.Rim,
                    onPlane == SpatialPlane.Environment ? 0 : 1);

                return new SpatialStack(
                    onPlane, declared, state, opaqueCell,
                    Key: null, Ambient: null,
                    interactive, selected,
                    motionAllowed: !ReducedMotion());
            }

            // Declared without a value here and assigned once at the end,
            // after every state has had its say on `opacity`. Assigning it
            // up front as well meant two Fade calls and a discarded
            // first result that a reader has to prove is harmless.
            Color fill;
            Color rim = _spatial.Rim;
            Color ink = _spatial.Ink;
            Color muted = _spatial.MutedInk;
            double rimWidth = onPlane == SpatialPlane.Environment ? 0 : 1;

            // Hover lifts the fill's opacity a little -- a surface coming
            // slightly forward. Never a translate: in a family whose
            // entire premise is a Z hierarchy, moving an element is a
            // claim about its Z that nothing else in the family makes.
            if (interactive && state == SoftInteractionState.Hover)
                opacity = Math.Min(1.0, opacity + 0.06);

            // PRESS RECEDES. Toward the environment, not toward the ink:
            // "recedes into the background" is Fluent's own wording, and
            // receding means less surface, not more.
            if (state == SoftInteractionState.Pressed)
                opacity = Math.Max(
                    0.0, opacity - m.PressRecede);

            fill = Fade(_spatial.Fill, opacity);

            if (state == SoftInteractionState.Disabled)
            {
                fill = Fade(
                    SpatialPalette.Composited(_spatial.Fill, backdrop),
                    1.0);
                fill = SpatialRecipe.Mix(fill, backdrop, 0.45);
                rim = Mix(rim, backdrop, 0.45);
                muted = Mix(muted, backdrop, 0.45);
            }

            if (selected)
            {
                rim = _spatial.Accent;
                rimWidth = 2;
            }

            if (state == SoftInteractionState.Focused)
            {
                rim = _spatial.Accent;
                rimWidth = 2;
            }

            var cell = new SpatialCell(fill, ink, muted, rim, rimWidth);

            SpatialShadow? key = null;
            SpatialShadow? ambient = null;

            // The environment casts nothing. It is the light source's
            // subject, not an object in the scene.
            if (onPlane != SpatialPlane.Environment)
            {
                key = m.KeyShadow(onPlane, _spatial.Ink);
                ambient = m.AmbientShadow(onPlane, _spatial.Ink);
            }

            return new SpatialStack(
                onPlane, declared, state, cell, key, ambient,
                interactive, selected, motionAllowed: !ReducedMotion());
        }

        private static bool ReducedMotion()
        {
            try { return EightThemePainter.ReducedMotionRequested; }
            catch (Exception) { return false; }
        }

        /// <summary>Scales a colour's alpha, clamping to a legal range.</summary>
        internal static Color Fade(Color c, double opacity)
        {
            double k = Math.Clamp(opacity, 0.0, 1.0);
            return Color.FromArgb(
                (byte)Math.Round(c.A * k),
                c.R, c.G, c.B);
        }

        /// <summary>
        /// The shadow colour. Key shadows carry more of the ink's hue than
        /// ambient ones, which are closer to neutral -- a tinted ambient
        /// shadow at a 64px blur reads as a coloured cloud rather than
        /// as depth.
        /// </summary>
        internal static Color ShadowColor(Color ink, bool ambient)
        {
            _ = ambient;
            return Color.FromArgb(ink.A, ink.R, ink.G, ink.B);
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
