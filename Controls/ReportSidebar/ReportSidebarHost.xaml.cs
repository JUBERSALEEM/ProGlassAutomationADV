// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/ReportSidebar/ReportSidebarHost.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Reports — shared Sheet Store-style right sidebar
//
// Animates the right detail column 0 ↔ 376px and the panel slide,
// mirroring the SheetStoreView choreography exactly. The host is a
// ContentControl: reports feed their own body via SidebarContent and
// toggle IsOpen from their selection state.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Animations;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace ProGlassAutomation.Controls.ReportSidebar
{
    public partial class ReportSidebarHost : UserControl
    {
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(
                nameof(IsOpen), typeof(bool), typeof(ReportSidebarHost),
                new PropertyMetadata(false, OnIsOpenChanged));

        public static readonly DependencyProperty CloseCommandProperty =
            DependencyProperty.Register(
                nameof(CloseCommand), typeof(ICommand), typeof(ReportSidebarHost),
                new PropertyMetadata(null));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title), typeof(string), typeof(ReportSidebarHost),
                new PropertyMetadata("DETAILS", OnHeaderChanged));

        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register(
                nameof(Subtitle), typeof(string), typeof(ReportSidebarHost),
                new PropertyMetadata("", OnHeaderChanged));

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(
                nameof(Icon), typeof(string), typeof(ReportSidebarHost),
                new PropertyMetadata("\uE8B7", OnHeaderChanged));

        public static readonly DependencyProperty SidebarContentProperty =
            DependencyProperty.Register(
                nameof(SidebarContent), typeof(object), typeof(ReportSidebarHost),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SidebarContentTemplateProperty =
            DependencyProperty.Register(
                nameof(SidebarContentTemplate), typeof(DataTemplate), typeof(ReportSidebarHost),
                new PropertyMetadata(null));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public ICommand? CloseCommand
        {
            get => (ICommand?)GetValue(CloseCommandProperty);
            set => SetValue(CloseCommandProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public object? SidebarContent
        {
            get => GetValue(SidebarContentProperty);
            set => SetValue(SidebarContentProperty, value);
        }

        /// <summary>
        /// DataTemplate rendered for the sidebar body. The template's
        /// DataContext is <see cref="SidebarContent"/> — reports set it to
        /// the report ViewModel so bindings read SelectedRow.* naturally.
        /// </summary>
        public DataTemplate? SidebarContentTemplate
        {
            get => (DataTemplate?)GetValue(SidebarContentTemplateProperty);
            set => SetValue(SidebarContentTemplateProperty, value);
        }

        public ReportSidebarHost()
        {
            InitializeComponent();
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ReportSidebarHost host)
                host.AnimatePanel((bool)e.NewValue);
        }

        private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ReportSidebarHost host)
            {
                host.HeaderTitle.Text = host.Title;
                host.HeaderSubtitle.Text = host.Subtitle ?? "";
                host.HeaderIcon.Text = host.Icon ?? "\uE8B7";
            }
        }

        /// <summary>
        /// Opens/closes with the Sheet Store animation: the detail column
        /// animates 0 ↔ 376px (GridLengthAnimation — WPF has no native
        /// animation for the GridLength-typed Width) and the panel
        /// translates 40px out/in.
        /// </summary>
        private void AnimatePanel(bool open)
        {
            if (DetailPanel == null || DetailColumn == null) return;

            if (open)
            {
                DetailPanel.Visibility = Visibility.Visible;
                DetailColumn.BeginAnimation(ColumnDefinition.WidthProperty,
                    new GridLengthAnimation
                    {
                        From = DetailColumn.Width,
                        To = new GridLength(376),
                        Duration = TimeSpan.FromMilliseconds(220),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    });

                var slide = new DoubleAnimation
                {
                    From = 40,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                PanelSlide.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
            }
            else
            {
                var slide = new DoubleAnimation
                {
                    From = 0,
                    To = 40,
                    Duration = TimeSpan.FromMilliseconds(160),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                slide.Completed += (_, __) =>
                {
                    DetailPanel.Visibility = Visibility.Collapsed;
                    PanelSlide.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
                };
                PanelSlide.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);

                DetailColumn.BeginAnimation(ColumnDefinition.WidthProperty,
                    new GridLengthAnimation
                    {
                        From = DetailColumn.Width,
                        To = new GridLength(0),
                        Duration = TimeSpan.FromMilliseconds(160),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                    });
            }
        }
    }
}
