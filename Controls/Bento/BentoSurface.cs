// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Bento/BentoSurface.cs
// PROJECT: ProGlassAutomation
// MODULE: Bento Grid — the cell
//
// A bento cell is a box with a fixed footprint, which sounds trivial
// until you notice what it is NOT allowed to do:
//
//   • It does not size itself to its content. The SPAN sets the box.
//   • It does not translate or scale on hover. Ever.
//   • It does not react to the pointer unless it is interactive.
//
// ── WHY IT DOES NOT GROW ──────────────────────────────────────────────
//
// The research is explicit that tile size reflects data PRIORITY, not
// content volume, and that rows must be a fixed unit. A control that
// measured itself would undo both: one over-long caption grows its row,
// every cell in that row grows with it, and the composition quietly
// becomes the uniform card grid the whole style exists to avoid. So the
// cell clips and the caller lays out.
//
// ── WHY IT NEVER MOVES ────────────────────────────────────────────────
//
// Inherited from Swiss, where the argument is made plainly: the grid is
// a rational system and its geometry must not be disturbed by
// interaction. In bento the geometry is even more load-bearing, because
// the 2x2 anchor's entire job is to be the cell the eye lands on first.
// A hover that lifts a cell changes which cell is biggest, which
// destroys the hierarchy in the act of pointing at it.
//
// ── THE ACCESSIBLE NAME GOES HERE ─────────────────────────────────────
//
// The most-missed requirement in the research. A cell showing $142,000
// means nothing to a screen reader without context, and the fix is to
// label the CONTAINER -- the cell -- rather than the number inside it:
//
//     <b:BentoSurface AutomationProperties.Name="Monthly recurring
//                                                revenue: $142,000">
//
// Setting the name on the inner TextBlock instead is the same mistake in
// a different place, and is why this control documents the property
// rather than leaving it to the template author.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Bento;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Bento
{
    /// <summary>A bento cell: a rounded, edged, fixed-footprint tile.</summary>
    public class BentoSurface : ContentControl
    {
        /// <summary>Which footprint this cell occupies in its panel.</summary>
        public static readonly DependencyProperty BentoSpanProperty =
            DependencyProperty.Register(
                nameof(BentoSpan), typeof(Services.Theme.Bento.BentoSpan),
                typeof(BentoSurface),
                new FrameworkPropertyMetadata(
                    Services.Theme.Bento.BentoSpan.Unit, OnBentoChanged));

        public Services.Theme.Bento.BentoSpan BentoSpan
        {
            get => (Services.Theme.Bento.BentoSpan)
                GetValue(BentoSpanProperty);
            set => SetValue(BentoSpanProperty, value);
        }

        /// <summary>Which depth tier. Accepted for vocabulary uniformity;
        /// bento renders every tier identically, because a cell that
        /// changed corner radius or fill at a higher tier would stop
        /// matching the cells beside it.</summary>
        public static readonly DependencyProperty BentoDepthProperty =
            DependencyProperty.Register(
                nameof(BentoDepth), typeof(SoftDepthTier),
                typeof(BentoSurface),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Resting, OnBentoChanged));

        public SoftDepthTier BentoDepth
        {
            get => (SoftDepthTier)GetValue(BentoDepthProperty);
            set => SetValue(BentoDepthProperty, value);
        }

        /// <summary>
        /// True for cells the user can act on.
        /// <para>
        /// False means the cell does not react to the pointer AT ALL.
        /// That is not an oversight. In a grid this large most cells are
        /// informational, and a hover on those would tell the user they
        /// can be clicked -- which is the expensive kind of lie in a
        /// composition whose entire structure is positional.
        /// </para>
        /// </summary>
        public static readonly DependencyProperty BentoInteractiveProperty =
            DependencyProperty.Register(
                nameof(BentoInteractive), typeof(bool),
                typeof(BentoSurface),
                new FrameworkPropertyMetadata(false, OnBentoChanged));

        public bool BentoInteractive
        {
            get => (bool)GetValue(BentoInteractiveProperty);
            set => SetValue(BentoInteractiveProperty, value);
        }

        /// <summary>Selected state: a heavier edge and a heavier weight,
        /// never a fill change, so selection survives greyscale.</summary>
        public static readonly DependencyProperty BentoSelectedProperty =
            DependencyProperty.Register(
                nameof(BentoSelected), typeof(bool),
                typeof(BentoSurface),
                new FrameworkPropertyMetadata(false, OnBentoChanged));

        public bool BentoSelected
        {
            get => (bool)GetValue(BentoSelectedProperty);
            set => SetValue(BentoSelectedProperty, value);
        }

        /// <summary>Current interaction state, readable by tests.</summary>
        public static readonly DependencyProperty BentoStateProperty =
            DependencyProperty.Register(
                nameof(BentoState), typeof(SoftInteractionState),
                typeof(BentoSurface),
                new PropertyMetadata(
                    SoftInteractionState.Rest, OnBentoChanged));

        public SoftInteractionState BentoState
        {
            get => (SoftInteractionState)GetValue(BentoStateProperty);
            set => SetValue(BentoStateProperty, value);
        }

        private readonly BentoSurfaceParts _parts = new BentoSurfaceParts();
        private bool _bound;
        private bool _wired;

        public BentoSurface()
        {
            HorizontalContentAlignment = HorizontalAlignment.Left;
            VerticalContentAlignment = VerticalAlignment.Top;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            // Do not stretch to fill. A cell that filled its box would
            // push its content apart from the padding the recipe asked
            // for, and the padding is what tells the eye the cell has
            // room to breathe.
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (!_bound)
            {
                _parts.Bind(this);
                _bound = true;
                Wire();
            }

            Repaint();
        }

        private static void OnBentoChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BentoSurface s) s.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
        }

        /// <summary>Recomputes and applies the construction.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            BentoSurfaceParts.Paint(
                _parts, dark, BentoSpan, BentoDepth,
                ResolveState(), BentoInteractive, BentoSelected,
                Fill(dark), Ink(dark));
        }

        private SoftInteractionState ResolveState()
        {
            // Disabled outranks every other state, and is checked first so
            // a hovered-but-disabled cell cannot report Hover.
            if (!IsEnabled) return SoftInteractionState.Disabled;

            // A non-interactive cell is always at rest. It must not even
            // show a focus ring as a "maybe you can" hint.
            if (!BentoInteractive) return SoftInteractionState.Rest;

            return BentoState;
        }

        /// <summary>
        /// The accessible name for this cell.
        /// <para>
        /// Set this on the BentoSurface, not on the TextBlock inside it.
        /// The research is explicit that the label belongs at container
        /// level, and a screen reader announces the container -- a label
        /// on the inner text is announced as part of the text and the
        /// cell itself still reads as an unlabelled group.
        /// </para>
        /// </summary>
        public string AccessibleName
        {
            get => AutomationProperties.GetName(this);
            set => AutomationProperties.SetName(this, value);
        }

        /// <summary>
        /// The fallback fill, read from the host dictionary.
        /// <para>
        /// Used only when no Bento profile is published, so a Bento cell
        /// dropped into a non-Bento theme renders as a readable surface
        /// instead of an invisible one. The key is the same in both modes
        /// because the host dictionary follows the app theme, which is
        /// why <paramref name="dark"/> is deliberately unused rather than
        /// branching to a second key that may not exist.
        /// </para>
        /// </summary>
        internal static Color Fill(bool dark) =>
            Token("WindowBackgroundBrush");

        internal static Color Ink(bool dark) =>
            Token("PrimaryForegroundBrush");

        internal static Color Token(string key) =>
            (Application.Current?.TryFindResource(key)
                as SolidColorBrush)?.Color ?? Colors.Black;
    }
}
