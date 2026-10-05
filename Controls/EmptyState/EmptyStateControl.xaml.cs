using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Controls.EmptyState
{
    /// <summary>
    /// Shared empty/no-data state (§30). Styled like the Overview
    /// Dashboard cards. Explains what belongs in the section and what
    /// the user can do — no blank screens anywhere.
    /// </summary>
    public partial class EmptyStateControl : UserControl
    {
        public EmptyStateControl()
        {
            InitializeComponent();
        }

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(
                nameof(Icon), typeof(string), typeof(EmptyStateControl),
                new PropertyMetadata("🗂"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title), typeof(string), typeof(EmptyStateControl),
                new PropertyMetadata("Nothing here yet"));

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(
                nameof(Message), typeof(string), typeof(EmptyStateControl),
                new PropertyMetadata(""));

        /// <summary>Optional button label. Empty hides the button.</summary>
        public string ActionText
        {
            get => (string)GetValue(ActionTextProperty);
            set => SetValue(ActionTextProperty, value);
        }

        public static readonly DependencyProperty ActionTextProperty =
            DependencyProperty.Register(
                nameof(ActionText), typeof(string), typeof(EmptyStateControl),
                new PropertyMetadata(""));

        public ICommand ActionCommand
        {
            get => (ICommand)GetValue(ActionCommandProperty);
            set => SetValue(ActionCommandProperty, value);
        }

        public static readonly DependencyProperty ActionCommandProperty =
            DependencyProperty.Register(
                nameof(ActionCommand), typeof(ICommand),
                typeof(EmptyStateControl),
                new PropertyMetadata(null));
    }
}
