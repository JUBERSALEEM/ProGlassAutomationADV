// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Bento/BentoPanel.cs
// PROJECT: ProGlassAutomation
// MODULE: Bento Grid — the layout engine
//
// ── WHY THIS FILE EXISTS AT ALL ───────────────────────────────────────
//
// The research in Services/Theme/Bento/README.md is blunt about the one
// defect that ruins a bento grid:
//
//     visual order  ≠  reading order
//
// The CSS expression of that is `grid-auto-flow: dense`, which fills
// holes with later, smaller children and therefore places items out of
// sequence. The sources ban it outright. A sighted user reads the visual
// order; a screen reader and the Tab key walk the DOM. Get that wrong
// and the grid is two different dashboards.
//
// WPF has no `dense` flag, so the mistake cannot arrive by transcription.
// It arrives the same way regardless, through two doors:
//
//   1. A panel that "helpfully" back-fills a hole left by a 2x2 cell
//      with a later 1x1. This reproduces dense exactly, and it is the
//      tempting thing to write because a hole looks like a bug.
//
//   2. TabIndex used to force the tab order to match the visual order.
//      This is the WPF spelling of `tabindex > 0`, and it is the same
//      compounding debt: the next edit that reorders Children silently
//      desynchronises tab order from visual order, and nothing warns.
//
// So this panel does not ASK for the ordering guarantee. It cannot back-
// fill, it arranges strictly in declaration order, and it sets no
// TabIndex. Reading order, visual order, hit-test order and tab order
// are all the same sequence by construction, because there is only one
// sequence to get wrong.
//
// A hole is therefore REPORTED, not hidden. BentoDiagnostics collects it
// and BentoPanel exposes it, because a hole is an authoring error that
// should be visible at design time rather than silently absorbed.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ProGlassAutomation.Services.Theme.Bento;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Bento
{
    /// <summary>
    /// Layout problems found while placing cells.
    /// <para>
    /// A bento grid fails in ways that still LOOK like a bento grid --
    /// a uniform layout, a hole, two anchors. Nothing renders wrongly, so
    /// there is no exception and no crash to notice. These are the only
    /// signal, which is why they are values a test can assert on rather
    /// than something written to the debug output.
    /// </para>
    /// </summary>
    public sealed class BentoDiagnostics
    {
        internal BentoDiagnostics()
        {
            Issues = new List<string>();
        }

        /// <summary>Human-readable descriptions, in placement order.</summary>
        public IReadOnlyList<string> Issues { get; }

        /// <summary>
        /// More than one Hero in the grid. The anchor is unique by
        /// definition: "a bento without a clear anchor reads as a flat
        /// card row", and two anchors means neither is the anchor.
        /// </summary>
        public int HeroCount { get; internal set; }

        /// <summary>
        /// Cells left empty by the spans given. Non-zero is always an
        /// authoring error -- this panel will not fill them, because
        /// filling them is the exact defect described in the header.
        /// </summary>
        public int HoleCount { get; internal set; }

        /// <summary>
        /// Distinct footprints in use. The research caps this at three:
        /// beyond that the composition stops reading as a bento and
        /// starts reading as arbitrary.
        /// </summary>
        public int DistinctSpans { get; internal set; }

        /// <summary>True when nothing was found.</summary>
        public bool IsClean => Issues.Count == 0;

        /// <summary>One line summarising the layout, for tests and logs.</summary>
        public override string ToString() =>
            IsClean
                ? "bento: clean"
                : $"bento: {Issues.Count} issue(s); heroes={HeroCount}, "
                  + $"holes={HoleCount}, distinctSpans={DistinctSpans}";
    }

    /// <summary>
    /// The bento layout panel: fixed row height, fixed column count,
    /// declaration-order placement, no hole-filling.
    /// </summary>
    public class BentoPanel : Panel
    {
        // ── Footprint ────────────────────────────────────────────────────
        //
        // An attached property rather than a member on the child, so any
        // element can be a cell. A dedicated BentoCell type would force a
        // wrapper around every cell and change the visual tree; the
        // research wants each cell self-contained, which argues against
        // inserting an extra element between the tile and its content.

        /// <summary>Cell footprint, in grid units.</summary>
        public static readonly DependencyProperty SpanProperty =
            DependencyProperty.RegisterAttached(
                "Span", typeof(BentoSpan), typeof(BentoPanel),
                new FrameworkPropertyMetadata(BentoSpan.Unit));

        public static BentoSpan GetSpan(DependencyObject obj) =>
            (BentoSpan)obj.GetValue(SpanProperty);

        public static void SetSpan(DependencyObject obj, BentoSpan value) =>
            obj.SetValue(SpanProperty, value);

        /// <summary>
        /// Column span override. Exists so a caller can express a 3-wide
        /// cell when genuinely needed, but it is NOT part of the closed
        /// footprint vocabulary and BentoDiagnostics counts it, because
        /// free-form spans are how a bento quietly stops being one.
        /// </summary>
        public static readonly DependencyProperty ColumnSpanOverrideProperty =
            DependencyProperty.RegisterAttached(
                "ColumnSpanOverride", typeof(int), typeof(BentoPanel),
                new FrameworkPropertyMetadata(0));

        public static int GetColumnSpanOverride(DependencyObject obj) =>
            (int)obj.GetValue(ColumnSpanOverrideProperty);

        public static void SetColumnSpanOverride(
            DependencyObject obj, int value) =>
            obj.SetValue(ColumnSpanOverrideProperty, value);

        // ── Panel behaviour ──────────────────────────────────────────────

        /// <summary>
        /// Columns on desktop. Defaults to the palette's 4.
        /// <para>
        /// Four, not the twelve of a page grid. Bento footprints count in
        /// units, and on a twelve-unit grid a 2x2 would be four page
        /// columns wide -- a different composition from the one the
        /// research describes.
        /// </para>
        /// </summary>
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(
                nameof(Columns), typeof(int), typeof(BentoPanel),
                new FrameworkPropertyMetadata(4, OnLayoutInvalidated),
                IsValidColumns);

        public int Columns
        {
            get => (int)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        /// <summary>
        /// Below this width the grid collapses to a single column and
        /// every cell becomes 1x1.
        /// <para>
        /// The collapse preserves DECLARATION order, which is the whole
        /// reason the panel forbids reordering. The sources make the same
        /// point about authoring order: the HTML must already be written
        /// in the intended mobile order, because a single-column stack
        /// falls back to it exactly.
        /// </para>
        /// </summary>
        public static readonly DependencyProperty CollapseWidthProperty =
            DependencyProperty.Register(
                nameof(CollapseWidth), typeof(double), typeof(BentoPanel),
                new FrameworkPropertyMetadata(720.0, OnLayoutInvalidated));

        public double CollapseWidth
        {
            get => (double)GetValue(CollapseWidthProperty);
            set => SetValue(CollapseWidthProperty, value);
        }

        /// <summary>
        /// Problems found during the last measure pass. Null until the
        /// panel has been measured at least once.
        /// </summary>
        public BentoDiagnostics? Diagnostics { get; private set; }

        /// <summary>
        /// True when the last measure collapsed to a single column.
        /// </summary>
        public bool IsCollapsed { get; private set; }

        /// <summary>
        /// Raised after a measure pass finds something.
        /// <para>
        /// Deliberately an event and not a log line. A layout that is
        /// merely uninteresting must not announce itself, so this fires
        /// only when <see cref="BentoDiagnostics.IsClean"/> is false.
        /// </para>
        /// </summary>
        public event EventHandler<BentoDiagnostics>? DiagnosticsChanged;

        private static bool IsValidColumns(object value) =>
            value is int n && n >= 1 && n <= 12;

        private static void OnLayoutInvalidated(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BentoPanel p) p.InvalidateMeasure();
        }

        /// <summary>The spacing system for this panel.</summary>
        private static BentoMetrics Metrics =>
            BentoPalette.Light?.Metrics
            ?? BentoPalette.Dark?.Metrics
            ?? new BentoMetrics();

        // ── Layout ───────────────────────────────────────────────────────

        protected override Size MeasureOverride(Size availableSize)
        {
            var m = Metrics;

            // An infinite available width means the caller is measuring
            // without a constraint (a StackPanel parent, a designer). A
            // bento cannot be measured without one -- every cell's width
            // is a fraction of the total -- so fall back to the column
            // count as though the grid were exactly as wide as its
            // columns, rather than dividing by infinity.
            double width = double.IsInfinity(availableSize.Width)
                ? m.Columns * (m.RowHeight * 2) + (m.Columns + 1) * m.Gutter
                : availableSize.Width;

            bool collapsed = width < CollapseWidth;
            int columns = collapsed ? 1 : Columns;
            IsCollapsed = collapsed;

            var diag = new BentoDiagnostics();

            double gutter = m.Gutter;
            double rowHeight = m.RowHeight;
            double cellWidth = (width - gutter * (columns + 1)) / columns;

            var children = Children;
            int index = 0;
            int row = 0;
            int col = 0;
            int claimed = 0;
            int usedRows = 0;
            var spans = new HashSet<BentoSpan>();

            foreach (UIElement child in children)
            {
                if (child.Visibility == Visibility.Collapsed) continue;

                var span = GetSpan(child);
                spans.Add(span);

                if (span == BentoSpan.Hero) diag.HeroCount++;

                int spanCols = 1;
                int spanRows = 1;

                if (!collapsed)
                {
                    spanCols = ColumnsOf(span);
                    spanRows = RowsOf(span);

                    int overrideSpan = GetColumnSpanOverride(child);
                    if (overrideSpan > 0) spanCols = overrideSpan;

                    // A cell wider than the grid is not clamped silently:
                    // clamping would change its footprint without telling
                    // anyone, and every cell after it would shift.
                    if (spanCols > columns)
                    {
                        diag.Issues.Add(
                            $"child[{index}] spans {spanCols} columns but "
                            + $"the grid has {columns}");
                        spanCols = columns;
                    }

                    // Wrapping to the next row is what a grid does. What
                    // is forbidden is reaching BACKWARD to fill a hole,
                    // and nothing in this loop can do that -- col only
                    // ever increases, and a wrap resets it to zero.
                    if (col + spanCols > columns)
                    {
                        col = 0;
                        row++;
                    }
                }

                var cellSize = collapsed
                    ? new Size(
                        Math.Max(0, width - gutter * 2),
                        rowHeight)
                    : new Size(
                        cellWidth * spanCols + gutter * (spanCols - 1),
                        rowHeight * spanRows + gutter * (spanRows - 1));

                child.Measure(cellSize);

                claimed += collapsed ? 1 : spanCols * spanRows;
                usedRows = Math.Max(usedRows, row + spanRows);

                index++;
                col += spanCols;
            }

            // Claimed cells come from THIS pass. They used to be read off
            // a field that ArrangeOverride writes, and measure runs
            // first -- so the first layout of every panel counted the
            // whole grid as holes, and only the second was right.
            _claimedCells = claimed;

            diag.DistinctSpans = spans.Count;

            // Holes: cells in used rows that no child claimed.
            if (!collapsed && columns > 0 && children.Count > 0)
            {
                diag.HoleCount = (usedRows * columns) - _claimedCells;
            }

            if (diag.HeroCount > 1)
            {
                diag.Issues.Add(
                    $"{diag.HeroCount} Hero cells; the anchor must be "
                    + "unique -- a grid with two anchors reads as a flat "
                    + "card row");
            }
            if (diag.HoleCount > 0)
            {
                diag.Issues.Add(
                    $"{diag.HoleCount} empty cell(s) left by the spans "
                    + "given. BentoPanel will not fill them: back-filling "
                    + "reproduces grid-auto-flow: dense, which places "
                    + "items out of reading order");
            }
            if (diag.DistinctSpans > 3)
            {
                diag.Issues.Add(
                    $"{diag.DistinctSpans} distinct footprints; three is "
                    + "the ceiling before the composition stops reading "
                    + "as a bento");
            }
            if (!collapsed && spans.Count == 1)
            {
                diag.Issues.Add(
                    "every cell has the same footprint, which is a "
                    + "uniform card grid rather than a bento grid");
            }

            Diagnostics = diag;
            if (!diag.IsClean)
                DiagnosticsChanged?.Invoke(this, diag);

            double totalHeight =
                gutter + usedRows * (rowHeight + gutter);

            return new Size(
                double.IsInfinity(availableSize.Width)
                    ? double.PositiveInfinity
                    : width,
                totalHeight);
        }

        private int _claimedCells;

        protected override Size ArrangeOverride(Size finalSize)
        {
            var m = Metrics;
            double gutter = m.Gutter;
            double rowHeight = m.RowHeight;

            int columns = IsCollapsed ? 1 : Columns;
            double cellWidth =
                (finalSize.Width - gutter * (columns + 1)) / columns;

            int row = 0;
            int col = 0;
            _claimedCells = 0;

            var children = Children;

            foreach (UIElement child in children)
            {
                if (child.Visibility == Visibility.Collapsed) continue;

                var span = GetSpan(child);
                int spanCols = 1;
                int spanRows = 1;

                if (!IsCollapsed)
                {
                    spanCols = ColumnsOf(span);
                    spanRows = RowsOf(span);

                    int overrideSpan = GetColumnSpanOverride(child);
                    if (overrideSpan > 0) spanCols = overrideSpan;
                    if (spanCols > columns) spanCols = columns;

                    if (col + spanCols > columns)
                    {
                        col = 0;
                        row++;
                    }
                }

                double x = gutter + col * (cellWidth + gutter);
                double y = gutter + row * (rowHeight + gutter);

                double w = IsCollapsed
                    ? Math.Max(0, finalSize.Width - gutter * 2)
                    : cellWidth * spanCols + gutter * (spanCols - 1);
                double h = IsCollapsed
                    ? rowHeight
                    : rowHeight * spanRows + gutter * (spanRows - 1);

                child.Arrange(new Rect(x, y, w, h));

                _claimedCells += IsCollapsed ? 1 : spanCols * spanRows;
                col += spanCols;
            }

            return finalSize;
        }

        // ── Footprint arithmetic ─────────────────────────────────────────

        internal static int ColumnsOf(BentoSpan span) => span switch
        {
            BentoSpan.Unit => 1,
            BentoSpan.Wide => 2,
            BentoSpan.Tall => 1,
            BentoSpan.Hero => 2,
            _ => 1,
        };

        internal static int RowsOf(BentoSpan span) => span switch
        {
            BentoSpan.Unit => 1,
            BentoSpan.Wide => 1,
            BentoSpan.Tall => 2,
            BentoSpan.Hero => 2,
            _ => 1,
        };

        // ── Accessibility ────────────────────────────────────────────────

        /// <summary>
        /// Tab order follows reading order, which is declaration order,
        /// which is the order this panel lays out in.
        /// <para>
        /// Set once, here, and no child ever sets TabIndex. Setting
        /// TabIndex to force agreement with the visual layout is the
        /// documented failure mode: it looks like a fix and it is a trap,
        /// because the next edit that reorders Children desynchronises the
        /// two and nothing reports it. One order, used by everything, is
        /// the only version of this that stays correct.
        /// </para>
        /// </summary>
        static BentoPanel()
        {
            KeyboardNavigation.SetTabNavigation(
                DependencyProperty, KeyboardNavigationMode.Continue);
        }
    }
}
