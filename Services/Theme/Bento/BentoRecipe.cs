// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Bento/BentoRecipe.cs
// PROJECT: ProGlassAutomation
// MODULE: Bento Grid — the recipe for a composition
//
// ── THE ONE RULE THAT SHAPES EVERYTHING ──────────────────────────────
//
//     A cell never moves. It never scales. It never translates.
//
// This is inherited verbatim from Swiss, where the argument is made at
// its strongest — "the grid is a rational system and its GEOMETRY MUST
// NOT BE DISTURBED BY INTERACTION" — and it matters more here, because
// in bento the geometry IS the content. The 2×2 anchor is not a big
// card; it is the reason the eye lands in the upper left and reads
// outward. A hover that lifts a cell changes which cell is biggest,
// which destroys the hierarchy in the act of pointing at it.
//
// So Bento's entire interaction vocabulary is:
//   fill change, edge change, weight change. Never position or size.
//
// ── WHY THERE IS NO SHADOW BUDGET ─────────────────────────────────────
// Unlike clay and neumorphism, bento does not compose shadows, so there
// is no three-shadow problem to solve here. The separation between cells
// is done by the GUTTER — empty canvas — which is a layout decision, not
// a material one, and is why BentoPanel is the important file in this
// family and BentoSurface is the simple one.
//
// One shadow is still permitted: the focus ring's backing, and nothing
// else. The ring needs a surface that is visibly different from both the
// tile and the canvas in order to read as a ring rather than a border.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme.Eight;

namespace ProGlassAutomation.Services.Theme.Bento
{
    /// <summary>
    /// The spacing and motion system.
    /// <para>
    /// Every number here is traceable to a line in
    /// <c>Services/Theme/Bento/README.md</c>. They are not taste — each
    /// one is a relationship between two others, and the relationships
    /// are what stop a bento grid reading as scattered cards.
    /// </para>
    /// </summary>
    public sealed class BentoMetrics
    {
        /// <summary>
        /// The 8pt base. Everything else is a multiple.
        /// <para>
        /// Bento is less visibly mathematical than Swiss — there is no
        /// baseline grid for the eye to check — but the multiples still
        /// matter, because the gutter relationship in
        /// <see cref="Gutter"/> only reads as deliberate if the row
        /// height and padding agree with it.
        /// </para>
        /// </summary>
        public double Unit { get; } = 8;

        /// <summary>
        /// Cell gutter, 16px — two units.
        /// <para>
        /// The load-bearing number in the whole family. The gutter is what
        /// tells the eye "these are cells of one composition" rather than
        /// "these are separate cards". Too tight and the tiles fuse into a
        /// single block; too generous and the composition falls apart into
        /// unrelated objects. 16px sits at the tight end deliberately: the
        /// alternative failure (a disconnected grid) is the more common
        /// one and is also the more expensive to notice.
        /// </para>
        /// </summary>
        public double Gutter { get; } = 16;

        /// <summary>
        /// Inner padding inside a cell, 24px — 1.5× the gutter.
        /// <para>
        /// The research gives "roughly half the inner padding", which at
        /// a 16px gutter would be 32; 24 is the lower bound of the
        /// published 24–32 band and is chosen because a 104px row cannot
        /// carry 32px of padding top and bottom and still hold a label
        /// and a figure. Where the row height permits, this scales — see
        /// <see cref="PaddingFor"/>.
        /// </para>
        /// </summary>
        public double CellPadding { get; } = 24;

        /// <summary>
        /// Vertical breathing room around a whole grid, 48px — 3× the
        /// gutter, and comfortably above the 1.5× minimum the research
        /// sets. This is what makes a grid read as a discrete section
        /// rather than as part of the surrounding flow.
        /// </summary>
        public double SectionMargin { get; } = 48;

        /// <summary>
        /// Fixed row height, 104px.
        /// <para>
        /// FIXED is the requirement. Rows that size to their content are
        /// the mechanism by which a bento grid degenerates: one tile with
        /// an over-long label grows its row, every cell in that row grows
        /// with it, and the composition quietly becomes a uniform grid.
        /// A fixed row makes that failure impossible to produce by
        /// accident, which is the point.
        /// </para>
        /// </summary>
        public double RowHeight { get; } = 104;

        /// <summary>
        /// Units across on desktop, 4. Not 12: bento footprints count in
        /// units, and a 12-unit grid would need a 2×2 cell to be four
        /// page columns wide, which is a different composition.
        /// </summary>
        public int Columns { get; } = 4;

        /// <summary>
        /// Corner radius, 24px.
        /// <para>
        /// One radius for every cell, without exception — this is what
        /// makes the tiles read as one object. It is also constrained
        /// relative to the gutter: the gap must fall between HALF the
        /// radius and the full radius, or the cells stop reading as
        /// neighbours. At 16px against a 24px radius the gap sits
        /// between 12 and 24, so the composition holds.
        /// </para>
        /// </summary>
        public double Radius { get; } = 24;

        /// <summary>Cell edge, 1px. A real stroke, not a shadow.</summary>
        public double EdgeThickness { get; } = 1;

        /// <summary>
        /// Selected cell edge, 2px. A non-colour selection cue, paired
        /// with a heavier type weight, so selection never depends on hue
        /// alone (WCAG 1.4.1).
        /// </summary>
        public double EdgeSelectedThickness { get; } = 2;

        /// <summary>
        /// 120ms, and it is a ceiling. Bento is a fixed composition and
        /// motion is confined to fill and edge; anything approaching
        /// animation length is decoration, which is the opposite of the
        /// style's intent.
        /// </summary>
        public double MotionCeiling { get; } = 120;

        /// <summary>Body text, 1.5.</summary>
        public double BodyLineHeight { get; } = 1.5;

        /// <summary>The big figure in a cell, 1.1 — tight, so it reads as
        /// a number rather than as a paragraph that happens to be
        /// numeric.</summary>
        public double DisplayLineHeight { get; } = 1.1;

        /// <summary>
        /// Padding for a given footprint. The 2×2 anchor gets more room
        /// because it holds more content; a 1×1 gets less, because
        /// padding is the first thing that makes a small cell feel
        /// empty.
        /// </summary>
        public double PaddingFor(BentoSpan span) => span switch
        {
            BentoSpan.Unit => CellPadding - 8,
            BentoSpan.Wide => CellPadding,
            BentoSpan.Tall => CellPadding,
            BentoSpan.Hero => CellPadding + 8,
            _ => CellPadding,
        };
    }

    /// <summary>The resolved colours for one cell in one state.</summary>
    public sealed class BentoCell
    {
        public BentoCell(
            Color fill, Color ink, Color mutedInk, Color edge,
            double edgeThickness)
        {
            Fill = fill;
            Ink = ink;
            MutedInk = mutedInk;
            Edge = edge;
            EdgeThickness = edgeThickness;
        }

        /// <summary>
        /// The tile's own surface. Opaque — bento has no material brush,
        /// and needs none, since the cells are lit by layout rather than
        /// by a backdrop.
        /// </summary>
        public Color Fill { get; }

        public Color Ink { get; }
        public Color MutedInk { get; }

        /// <summary>
        /// The 1px stroke. Present at ALL times, including at rest — it is
        /// what holds the composition together in place of a shadow.
        /// </summary>
        public Color Edge { get; }

        public double EdgeThickness { get; }

        /// <summary>
        /// True when this cell is the anchor. Carried so controls can
        /// adjust type scale without re-deriving the span.
        /// </summary>
        public bool IsHero { get; internal set; }
    }

    /// <summary>The resolved construction for one cell in one state.</summary>
    public sealed class BentoStack
    {
        public BentoStack(
            SoftDepthTier tier,
            SoftInteractionState state,
            BentoCell cell,
            bool interactive,
            bool selected,
            bool motionAllowed)
        {
            Tier = tier;
            State = state;
            Cell = cell;
            Interactive = interactive;
            Selected = selected;
            MotionAllowed = motionAllowed;
        }

        public SoftDepthTier Tier { get; }
        public SoftInteractionState State { get; }
        public BentoCell Cell { get; }

        /// <summary>True if this cell reacts to the pointer at all.</summary>
        public bool Interactive { get; }

        public bool Selected { get; }

        /// <summary>
        /// False when the user has asked for reduced motion. Bento has
        /// almost nothing to animate, but the focus ring's fade is real
        /// motion and is suppressed here rather than at each call site.
        /// </summary>
        public bool MotionAllowed { get; }

        public bool Focused => State == SoftInteractionState.Focused;

        /// <summary>
        /// The focus ring brush, or null when the cell is not focused.
        /// <para>
        /// The ring is drawn OUTSIDE the cell and against the CANVAS, not
        /// the cell fill, so its contrast never depends on how similar
        /// the tile happens to be to its own background. At a 3px ring
        /// and a 3px offset it is a ring and not a border.
        /// </para>
        /// </summary>
        public Color? FocusRing => Focused ? Cell.Ink : (Color?)null;

        /// <summary>
        /// Deliberately EMPTY, and the emptiness is the contract.
        /// <para>
        /// Every other soft-UI family in this engine has a shadow budget
        /// to stay inside. Bento has no shadow vocabulary at all: depth is
        /// communicated by the gutter, which is a layout property, and a
        /// cell that grows a shadow stops being flush with the
        /// composition. This list exists so the property is available to
        /// uniform code and so the absence is explicit rather than an
        /// oversight.
        /// </para>
        /// </summary>
        public IReadOnlyList<object> Shadows { get; } =
            Array.Empty<object>();

        public bool HasShadow => false;

        /// <summary>One radius for every cell, always.</summary>
        public bool HasRadius => true;
    }

    /// <summary>The Bento material, derived from the family's ramp.</summary>
    public sealed class BentoProfile
    {
        public BentoProfile(
            Color canvas, Color surface, Color ink, Color mutedInk,
            Color edge, Color accent, BentoMetrics metrics)
        {
            Canvas = canvas;
            Surface = surface;
            Ink = ink;
            MutedInk = mutedInk;
            Edge = edge;
            Accent = accent;
            Metrics = metrics;
        }

        /// <summary>
        /// The ground the cells sit ON. It is a real, distinct plane, not
        /// the page: bento's gutter is empty space and empty space needs
        /// a colour to be empty against.
        /// </summary>
        public Color Canvas { get; }

        /// <summary>The tile fill.</summary>
        public Color Surface { get; }

        public Color Ink { get; }
        public Color MutedInk { get; }
        public Color Edge { get; }

        /// <summary>
        /// The accent. Spent on ONE thing at a time — the anchor's
        /// marker, or the selected cell, never both.
        /// </summary>
        public Color Accent { get; }

        public BentoMetrics Metrics { get; }
    }

    /// <summary>The recipe. Stateless.</summary>
    public sealed class BentoRecipe
    {
        private readonly BentoProfile _bento;
        private readonly bool _dark;

        public BentoRecipe(BentoProfile bento, bool dark)
        {
            _bento = bento ?? throw new ArgumentNullException(nameof(bento));
            _dark = dark;
        }

        /// <summary>
        /// How much a Hero cell lifts off the plain surface.
        /// <para>
        /// Small — deliberately. A hero that got its own material would
        /// stop being "the biggest cell of one composition" and become
        /// "the special card", and the grid would read as a card with
        /// seven accessories. Two percent of luminance is enough to tell
        /// the eye where the anchor is, and not enough to make it a
        /// different kind of object.
        /// </para>
        /// </summary>
        private const double HeroDelta = 0.02;

        /// <summary>How far the fill moves on hover. Fill only, never
        /// geometry — see the file header.</summary>
        private const double HoverDelta = 0.03;

        /// <summary>
        /// Resolves one cell.
        /// <para>
        /// <paramref name="span"/> participates because the footprint
        /// changes two things: the anchor gets its small luminance lift,
        /// and its edge thickens, because the anchor is the cell the eye
        /// must land on first and its edge is what the eye traces.
        /// </para>
        /// </summary>
        public BentoStack Resolve(
            BentoSpan span,
            SoftDepthTier declared,
            SoftInteractionState state,
            bool interactive,
            bool selected)
        {
            var surface = _bento.Surface;
            var canvas = _bento.Canvas;
            var ink = _bento.Ink;
            var edge = _bento.Edge;
            var muted = _bento.MutedInk;

            bool hero = span == BentoSpan.Hero;

            // The anchor lift is away from the canvas, so it always reads
            // as "closer" regardless of which mode is active.
            // One direction for both modes: the anchor always lifts
            // toward the lighter pole, because "closer to the light" is
            // the only reading that works in a dark canvas. Branching on
            // `_dark` here would look symmetric and be wrong — in dark
            // mode the lift has to go the same way as in light, just
            // from a lower base.
            Color fill = hero ? Mix(surface, Colors.White, HeroDelta)
                              : surface;

            Color edgeNow = edge;
            double edgeWidth = _bento.Metrics.EdThickness;

            // Hover shifts the FILL toward the ink, by a small amount. It
            // is never a translate and never a scale.
            //
            // A non-interactive cell does not react to the pointer at all.
            // A tile that lights up on hover is claiming to be a control,
            // and in a grid this large the claim is expensive: half the
            // cells are informational and a hover on those would tell the
            // user they can click them.
            if (interactive && state == SoftInteractionState.Hover)
            {
                fill = Mix(fill, ink, HoverDelta);
                edgeNow = Mix(edgeNow, ink, HoverDelta * 2);
            }

            // Press is the fill shift only, and it is capped by the
            // engine-wide accessibility constant. A bento cell is large,
            // so a press cue that is only a depth change would be
            // invisible at that size — the fill carries it.
            if (state == SoftInteractionState.Pressed)
            {
                fill = Mix(
                    fill, ink,
                    SoftUiAccessibility.PressedFillShift);
            }

            // Selected is a THICKER EDGE plus the accent, never a fill
            // change. Two non-colour cues — edge weight and type weight —
            // carry the state so it survives both greyscale and the
            // common colour-vision deficiencies.
            if (selected)
            {
                edgeNow = _bento.Accent;
                edgeWidth = _bento.Metrics.EdSelectedThickness;
            }

            // Disabled steps toward the canvas. It must remain visibly a
            // cell rather than vanishing, because a bento grid's structure
            // is positional: if a disabled cell disappeared the grid
            // would re-flow and every cell after it would move.
            if (state == SoftInteractionState.Disabled)
            {
                double k = _dark ? 0.45 : 0.55;
                fill = Mix(fill, canvas, k);
                edgeNow = Mix(edgeNow, canvas, k);
                muted = Mix(muted, canvas, k);
            }

            // Focus thickens the edge rather than relying on colour alone.
            // A keyboard user must be able to tell where they are on a
            // grid where nine cells can look alike.
            if (state == SoftInteractionState.Focused)
            {
                edgeNow = ink;
                edgeWidth = _bento.Metrics.EdSelectedThickness;
            }

            var cell = new BentoCell(
                fill, ink, muted, edgeNow, edgeWidth)
            {
                IsHero = hero,
            };

            // Reduced motion is read here, once, rather than at each
            // call site — the engine already has the canonical probe and
            // duplicating it is how two families end up disagreeing about
            // what the OS preference means. Read per resolve rather than
            // cached, because the user can flip the Ease of Access
            // toggle while the app is running.
            return new BentoStack(
                declared, state, cell, interactive, selected,
                motionAllowed:
                    !EightThemePainter.ReducedMotionRequested);
        }

        /// <summary>Linear channel mix. Same as the other recipes, so a
        /// Bento colour and a Swiss colour are computed identically.</summary>
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
