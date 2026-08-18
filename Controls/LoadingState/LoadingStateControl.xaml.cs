using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Controls.LoadingState
{
    /// <summary>
    /// Shared, theme-adaptive loading spinner (rotating dashed ring + label).
    /// Views host this wherever they show an in-progress state, instead of
    /// each view inventing its own ⏳ emoji markup.
    /// </summary>
    public partial class LoadingStateControl : UserControl
    {
        public LoadingStateControl()
        {
            InitializeComponent();
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
    }
}
