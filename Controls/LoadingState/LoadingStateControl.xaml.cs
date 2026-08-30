using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace ProGlassAutomation.Controls.LoadingState
{
    /// <summary>
    /// Shared, theme-adaptive loading spinner (rotating dashed ring + label).
    /// Views host this wherever they show an in-progress state, instead of
    /// each view inventing its own ⏳ emoji markup.
    ///
    /// PERF-12: the spinner storyboard only runs while the control is
    /// visible. List pages keep this control in the tree and toggle
    /// Visibility while loading; a permanently running RepeatBehavior=
    /// Forever animation on every one of those pages keeps a live clock +
    /// per-frame invalidation alive even when nothing is loading.
    /// </summary>
    public partial class LoadingStateControl : UserControl
    {
        private readonly Storyboard _spinStoryboard;
        private bool _attached;

        public LoadingStateControl()
        {
            InitializeComponent();

            // One animation, created once, started/stopped with visibility.
            var spin = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromMilliseconds(800)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(spin, SpinnerRotate);
            // Target is the RotateTransform itself → animate its Angle
            // directly (mirrors the original XAML TargetProperty="Angle").
            Storyboard.SetTargetProperty(spin, new PropertyPath("Angle"));
            _spinStoryboard = new Storyboard();
            _spinStoryboard.Children.Add(spin);

            Loaded += OnControlLoaded;
            Unloaded += OnControlUnloaded;
        }

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(
                nameof(Message), typeof(string), typeof(LoadingStateControl),
                new PropertyMetadata("Loading…"));

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            if (_attached) return;
            _attached = true;
            IsVisibleChanged += OnIsVisibleChanged;
            UpdateSpinnerState();
        }

        private void OnControlUnloaded(object sender, RoutedEventArgs e)
        {
            if (!_attached) return;
            _attached = false;
            IsVisibleChanged -= OnIsVisibleChanged;
            _spinStoryboard.Stop(this);
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
            => UpdateSpinnerState();

        private void UpdateSpinnerState()
        {
            if (IsVisible)
                _spinStoryboard.Begin(this, true);
            else
                _spinStoryboard.Stop(this);
        }
    }
}
