// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Swiss/SwissRecipe.cs
// PROJECT: ProGlassAutomation
// MODULE: Swiss International Style — the recipe for an absence
//
// ── THIS IS THE INVERSE PROBLEM ──────────────────────────────────────
// Clay and neumorphism both COMPOSE shadows. Their difficulty is that
// WPF cannot express three at once and they have to invent ways round it.
//
// Swiss has the opposite problem, and it is easy to get wrong precisely
// because it is so easy. The style's single hardest constraint is:
//
//     box-shadow: none  — at rest, on hover, on focus, on press, always.
//
// Every other family in this engine has a shadow budget to stay inside.
// Swiss has no budget at all: any shadow that appears is a defect. That
// makes the absence a CONTRACT rather than a default, which is why this
// file produces a construction with a permanently empty shadow list
// rather than a construction that happens to use small numbers.
//
// The corollary is the important part. With no shadow to carry depth,
// the style has to carry hierarchy some other way, and it does it with
// three devices only:
//
//   1. HAIRLINE RULES. 1px, horizontal. Structural, not decorative —
//      they are the grid made visible at section boundaries.
//   2. TYPOGRAPHY. Size and weight only. Never colour, never illustration.
//   3. COLOUR INVERSION. Black on white flips to white on black.
//
// ── WHY INVERSION, AND NOT A LIFT ────────────────────────────────────
// The obvious hover for a "flat" design is to lift the element. That is
// exactly wrong here: the grid is a rational system and its GEOMETRY
// MUST NOT BE DISTURBED BY INTERACTION. A card that rises on hover has
// stopped sitting on the grid, and the grid is the entire composition.
//
// So hover changes colour and border-colour, and nothing else. No
// translate, no scale, no shadow. The one movement the style permits is
// a directional arrow glyph sliding along its own baseline, because that
// is a typographic element rather than a spatial one.
//
// ── WHY THIS IS THE MOST ACCESSIBLE FAMILY IN THE ENGINE ─────────────
// Black-on-white inverting to white-on-black is roughly 20:1. Nothing
// else here gets near it — not the 4.5:1 the other families have to work
// for, and certainly not neumorphism's structural 1.3:1. The style that
// forbids decoration ends up with the strongest contrast guarantee of the
// eight, and that is worth knowing when picking one.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Services.Theme.Swiss
{
    /// <summary>
    /// One state of a Swiss surface: flat colours and a hairline. No
    /// shadow, no gradient, no radius. Deliberately a value type with no
    /// Effect anywhere in it — the compiler is what guarantees the
    /// absence, not a convention.
    /// </summary>
    public sealed class SwissContrast
    {
        internal SwissContrast(
            Color fill, Color ink, Color mutedInk,
            Color rule, double ruleThickness, double radius)
        {
            Fill = fill;
            Ink = ink;
            MutedInk = mutedInk;
            Rule = rule;
            RuleThickness = ruleThickness;
            Radius = radius;
        }

        /// <summary>The surface fill. Flat and unmodulated.</summary>
        public Color Fill { get; }

        /// <summary>Primary ink. Flush left, ragged right, never centred.</summary>
        public Color Ink { get; }

        /// <summary>
        /// Secondary ink. Still full contrast — Swiss builds hierarchy
        /// from size and weight, so "muted" here means SMALLER and
        /// LIGHTER, never washed out. A muted tint that drops below
        /// 4.5:1 would be importing the exact accessibility failure the
        /// rest of this engine spends so much effort avoiding.
        /// </summary>
        public Color MutedInk { get; }

        /// <summary>The hairline. Horizontal, 1px, structural.</summary>
        public Color Rule { get; }

        public double RuleThickness { get; }

        /// <summary>
        /// Always zero. Exposed so a test can assert it rather than infer
        /// it, and so a template has one authoritative source instead of
        /// hard-coding 0 and hoping the recipe agrees.
        /// </summary>
        public double Radius { get; }
    }

    /// <summary>The spacing and motion system.</summary>
    public sealed class SwissMetrics
    {
        public SwissMetrics(
            double unit, double gutter, double margin,
            double ruleThickness, double ruleSelectedThickness,
            double motionCeiling, double bodyLineHeight,
            double displayLineHeight)
        {
            Unit = unit;
            Gutter = gutter;
            Margin = margin;
            RuleThickness = ruleThickness;
            RuleSelectedThickness = ruleSelectedThickness;
            MotionCeiling = motionCeiling;
            BodyLineHeight = bodyLineHeight;
            DisplayLineHeight = displayLineHeight;
        }

        /// <summary>
        /// The 8pt base. Every dimension is a multiple of this. Swiss is
        /// mathematical: no 23.5px, no 17px gaps, because an off-grid
        /// measurement is the first thing the grid makes visible.
        /// </summary>
        public double Unit { get; }

        /// <summary>Column gutter, 24px.</summary>
        public double Gutter { get; }

        /// <summary>Outer margin, 64px on desktop.</summary>
        public double Margin { get; }

        /// <summary>The hairline weight. 1px.</summary>
        public double RuleThickness { get; }

        /// <summary>
        /// A SELECTED item's rule. 2px — and the weight change is the
        /// selection signal, alongside the 600 weight the typography
        /// carries. Both are non-colour cues, which is what makes the
        /// selected state readable without relying on hue.
        /// </summary>
        public double RuleSelectedThickness { get; }

        /// <summary>
        /// 150ms, and it is a CEILING not a default. Anything slower
        /// reads as decoration, which is the one thing this style exists
        /// to abolish.
        /// </summary>
        public double MotionCeiling { get; }

        public double BodyLineHeight { get; }
        public double DisplayLineHeight { get; }
    }

    /// <summary>The resolved construction for one surface in one state.</summary>
    public sealed class SwissStack
    {
        internal SwissStack(
            SoftDepthTier tier,
            SoftInteractionState state,
            SwissContrast contrast,
            bool inverted,
            bool selected,
            bool focused)
        {
            Tier = tier;
            State = state;
            Contrast = contrast;
            Inverted = inverted;
            Selected = selected;
            Focused = focused;
        }

        public SoftDepthTier Tier { get; }
        public SoftInteractionState State { get; }
        public SwissContrast Contrast { get; }

        /// <summary>
        /// True when fill and ink have swapped. This is the state machine:
        /// not a subtler fill, not a lighter border — the whole surface
        /// turns over.
        /// </summary>
        public bool Inverted { get; }

        /// <summary>
        /// True when the item is selected. Selection is carried by the
        /// heavier rule and the heavier weight, never by colour alone.
        /// </summary>
        public bool Selected { get; }

        /// <summary>
        /// True on keyboard focus.
        /// <para>
        /// Focus is deliberately NOT an inversion. Hovering a Swiss
        /// button turns it over; focusing it draws a 2px ring instead.
        /// Both are maximal-contrast signals, but they answer different
        /// questions — "can I press this" versus "where am I typing" —
        /// and collapsing them would leave a keyboard user unable to tell
        /// which of the two the app currently thinks is true.
        /// </para>
        /// <para>
        /// This property exists so the template can show the ring without
        /// re-deriving the state. A template that had to test the state
        /// enum itself would be a second copy of the state machine, and
        /// two copies of a state machine drift.
        /// </para>
        /// </summary>
        public bool Focused { get; }

        /// <summary>
        /// Always an empty list. Not decorative — it is how the type
        /// system proves the absence at every call site, so there is no
        /// path by which a Swiss surface can acquire a shadow.
        /// </summary>
        public IReadOnlyList<object> Shadows { get; } =
            Array.Empty<object>();

        /// <summary>There is never a shadow. Named so callers read as a
        /// question rather than as an assumption.</summary>
        public bool HasShadow => false;

        /// <summary>There is never a radius.</summary>
        public bool HasRadius => Contrast.Radius > 0.0;
    }

    /// <summary>The Swiss material, derived from the family's ramp.</summary>
    public sealed class SwissProfile
    {
        public SwissProfile(
            Color paper, Color ink, Color mutedInk, Color rule,
            Color accent, SwissMetrics metrics)
        {
            Paper = paper;
            Ink = ink;
            MutedInk = mutedInk;
            Rule = rule;
            Accent = accent;
            Metrics = metrics;
        }

        /// <summary>
        /// The ground. Flat, unmodulated, and NOT pure white — the style
        /// is paper, not a screen, and a pure white ground makes every
        /// hairline look like a rendering artefact rather than a drawn
        /// rule.
        /// </summary>
        public Color Paper { get; }

        public Color Ink { get; }
        public Color MutedInk { get; }
        public Color Rule { get; }

        /// <summary>
        /// The single accent. Spent on ONE element — a marker, a rule, a
        /// word. More than one accent at a time and it stops being an
        /// accent and becomes decoration, which is the thing this style
        /// was invented to be free of.
        /// </summary>
        public Color Accent { get; }

        public SwissMetrics Metrics { get; }
    }

    /// <summary>The recipe. Stateless.</summary>
    public sealed class SwissRecipe
    {
        private readonly SwissProfile _swiss;
        private readonly bool _dark;

        public SwissRecipe(SwissProfile swiss, bool dark)
        {
            _swiss = swiss ?? throw new ArgumentNullException(nameof(swiss));
            _dark = dark;
        }

        /// <summary>
        /// Resolves one surface.
        /// <para>
        /// <paramref name="interactive"/> and <paramref name="selected"/>
        /// are the whole model, because this style has no depth axis. A
        /// tier still exists so the engine's vocabulary is uniform, but it
        /// does not change the rendering — which is the honest answer.
        /// Pretending a Swiss modal sits at a different depth from a Swiss
        /// card would be inventing depth the style has explicitly
        /// abolished.
        /// </para>
        /// </summary>
        public SwissStack Resolve(
            SoftDepthTier declared,
            SoftInteractionState state,
            bool interactive,
            bool selected)
        {
            var paper = _swiss.Paper;
            var ink = _swiss.Ink;
            var rule = _swiss.Rule;
            var muted = _swiss.MutedInk;

            // Pressed is INVERTED, not dimmed. A press turns the surface
            // over — the strongest state change in the whole engine, and
            // the only one the style permits.
            bool invert = state == SoftInteractionState.Pressed;

            // Hover for an INTERACTIVE element turns it over too. For a
            // non-interactive surface hover does nothing at all: a panel
            // that reacts to the pointer is a panel claiming to be a
            // control, and in a style whose whole vocabulary is structure
            // that claim is the expensive kind of lie.
            if (interactive && state == SoftInteractionState.Hover) invert = true;

            Color fill, inkNow;
            if (invert)
            {
                fill = ink;
                inkNow = paper;
            }
            else
            {
                fill = paper;
                inkNow = ink;
            }

            // Disabled is the one state that does NOT invert, because an
            // inverted disabled control is more prominent than an
            // enabled one. It steps toward the paper instead.
            if (state == SoftInteractionState.Disabled)
            {
                fill = Mix(fill, paper, 0.55);
                inkNow = Mix(inkNow, paper, 0.45);
                muted = Mix(muted, paper, 0.45);
                rule = Mix(rule, paper, 0.55);
            }

            // Selection changes the RULE WEIGHT, not its colour. A
            // selected item is a heavier line under a heavier word, and
            // both of those survive a greyscale print, a colour-blind
            // reader and a high-contrast mode.
            double thickness = selected
                ? _swiss.Metrics.RuleSelectedThickness
                : _swiss.Metrics.RuleThickness;

            var contrast = new SwissContrast(
                fill, inkNow, muted, rule, thickness, radius: 0.0);

            return new SwissStack(
                declared, state, contrast, invert, selected,
                focused: state == SoftInteractionState.Focused);
        }

        internal static Color Mix(Color a, Color b, double k)
        {
            double t = Math.Clamp(k, 0, 1);
            return Color.FromArgb(
                a.A,
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }
    }
}