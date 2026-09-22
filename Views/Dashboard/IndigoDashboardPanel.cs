using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Dashboard
{
    /// <summary>
    /// Width-driven dashboard layout in device-independent pixels. Keeps every
    /// child; no fixed row height, clipping, collection truncation or VM mutation.
    /// Suitable both for bento sections and an ItemsControl's KPI items panel.
    /// </summary>
    public sealed class IndigoDashboardPanel : Panel
    {
        public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(
            nameof(MaxColumns), typeof(int), typeof(IndigoDashboardPanel),
            new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure),
            value => (int)value >= 1 && (int)value <= 12);
        public static readonly DependencyProperty MinimumColumnWidthProperty = DependencyProperty.Register(
            nameof(MinimumColumnWidth), typeof(double), typeof(IndigoDashboardPanel),
            new FrameworkPropertyMetadata(360.0, FrameworkPropertyMetadataOptions.AffectsMeasure), PositiveFinite);
        public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
            nameof(Gap), typeof(double), typeof(IndigoDashboardPanel),
            new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsMeasure),
            value => double.IsFinite((double)value) && (double)value >= 0);
        public static readonly DependencyProperty FirstColumnWeightProperty = DependencyProperty.Register(
            nameof(FirstColumnWeight), typeof(double), typeof(IndigoDashboardPanel),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsMeasure), PositiveFinite);

        public int MaxColumns { get => (int)GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }
        public double MinimumColumnWidth { get => (double)GetValue(MinimumColumnWidthProperty); set => SetValue(MinimumColumnWidthProperty, value); }
        public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }
        public double FirstColumnWeight { get => (double)GetValue(FirstColumnWeightProperty); set => SetValue(FirstColumnWeightProperty, value); }

        private static bool PositiveFinite(object value) => double.IsFinite((double)value) && (double)value > 0;

        private int Columns(double width, int count)
        {
            int maximum = Math.Min(MaxColumns, Math.Max(1, count));
            if (double.IsPositiveInfinity(width)) return maximum;
            // Weighted two-column sections require the narrower column to fit
            // the minimum too; don't squeeze the secondary card below it.
            if (maximum == 2)
            {
                double weight = Math.Max(FirstColumnWeight, 1 / FirstColumnWeight);
                return width >= MinimumColumnWidth * (1 + weight) + Gap ? 2 : 1;
            }
            return Math.Max(1, (int)Math.Min(maximum, Math.Floor((Math.Max(0, width) + Gap) / (MinimumColumnWidth + Gap))));
        }

        private double ColumnWidth(double width, int columns, int column)
        {
            double usable = Math.Max(0, width - Gap * (columns - 1));
            if (columns != 2) return usable / columns;
            // Form the ratio before multiplying: a large weight should not
            // overflow an otherwise ordinary finite arrange width.
            return column == 0
                ? usable * (FirstColumnWeight / (FirstColumnWeight + 1))
                : usable / (FirstColumnWeight + 1);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var children = InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToArray();
            if (children.Length == 0) return new Size();
            int columns = Columns(availableSize.Width, children.Length);
            double width = availableSize.Width;
            if (double.IsPositiveInfinity(width))
            {
                // Size-to-content / designer hosts can measure with infinity.
                // Equal-width fallback would shrink a 4:1 action column to
                // 40% of its requested minimum even though space is unbounded.
                double totalWeight = columns == 2
                    ? 1 + Math.Max(FirstColumnWeight, 1 / FirstColumnWeight)
                    : columns;
                width = MinimumColumnWidth * totalWeight + Gap * (columns - 1);
            }
            double height = 0;
            for (int start = 0; start < children.Length; start += columns)
            {
                double rowHeight = 0;
                for (int column = 0; column < columns && start + column < children.Length; column++)
                {
                    var child = children[start + column];
                    child.Measure(new Size(ColumnWidth(width, columns, column), double.PositiveInfinity));
                    rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
                }
                height += rowHeight + (start == 0 ? 0 : Gap);
            }
            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var children = InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToArray();
            int columns = Columns(finalSize.Width, children.Length);
            double y = 0;
            for (int start = 0; start < children.Length; start += columns)
            {
                double rowHeight = children.Skip(start).Take(columns).Max(c => c.DesiredSize.Height);
                double x = 0;
                for (int column = 0; column < columns && start + column < children.Length; column++)
                {
                    double width = ColumnWidth(finalSize.Width, columns, column);
                    children[start + column].Arrange(new Rect(x, y, width, rowHeight));
                    x += width + Gap;
                }
                y += rowHeight + Gap;
            }
            return finalSize;
        }
    }
}
