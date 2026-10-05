// ═══════════════════════════════════════════════════════════════════════
// FILE: Animations/GridLengthAnimation.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — animated column widths
//
// WPF has no built-in animation for ColumnDefinition.Width (a
// GridLength). This is the single reusable implementation:
//   • SheetStoreView (the original, formerly internal copy)
//   • ReportSidebarHost (Sheet Store-style panel slide)
//   • SalesReportView (collapsible reports rail)
// all animate the same way through this one class.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace ProGlassAutomation.Animations
{
    /// <summary>
    /// Animates a <see cref="GridLength"/> property (e.g.
    /// ColumnDefinition.Width) between two pixel values with an optional
    /// easing function.
    /// </summary>
    public sealed class GridLengthAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(GridLength);

        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register(nameof(From), typeof(GridLength), typeof(GridLengthAnimation));

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register(nameof(To), typeof(GridLength), typeof(GridLengthAnimation));

        public GridLength From
        {
            get => (GridLength)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public GridLength To
        {
            get => (GridLength)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public static readonly DependencyProperty EasingFunctionProperty =
            DependencyProperty.Register(
                nameof(EasingFunction),
                typeof(IEasingFunction),
                typeof(GridLengthAnimation));

        public IEasingFunction EasingFunction
        {
            get => (IEasingFunction)GetValue(EasingFunctionProperty);
            set => SetValue(EasingFunctionProperty, value);
        }

        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

        public override object GetCurrentValue(
            object defaultOriginValue,
            object defaultDestinationValue,
            AnimationClock animationClock)
        {
            if (animationClock.CurrentProgress == null) return From;

            double progress = animationClock.CurrentProgress.Value;
            if (EasingFunction != null)
                progress = EasingFunction.Ease(progress);

            double from = From.Value;
            double to = To.Value;
            return new GridLength(from + (to - from) * progress, GridUnitType.Pixel);
        }
    }
}
