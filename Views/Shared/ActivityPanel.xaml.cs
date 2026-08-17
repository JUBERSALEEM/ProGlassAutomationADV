// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Shared/ActivityPanel.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — notifications / activity popup
//
// Non-blocking popup for the top-bar notification icon. Shows the most
// recent audit-trail activity, loaded on open. A badge footer links to the
// full Logs & Activity (Diagnostics) module when clicked.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Audit;
using ProGlassAutomation.Services.Audit;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.Views.Shared
{
    public partial class ActivityPanel : UserControl
    {
        public ActivityPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        public async Task LoadAsync()
        {
            ActivityList.Children.Clear();
            var entries = await AuditService.GetRecentAsync(20);

            if (entries == null || entries.Count == 0)
            {
                ActivityList.Children.Add(new TextBlock
                {
                    Text = "No activity yet.",
                    FontSize = 12,
                    Foreground = (Brush)FindResource("MutedForegroundBrush"),
                    Margin = new Thickness(4, 8, 4, 8)
                });
                return;
            }

            foreach (var entry in entries)
                ActivityList.Children.Add(BuildRow(entry));
        }

        private Border BuildRow(ActivityLogEntry e)
        {
            var row = new Border
            {
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                CornerRadius = new CornerRadius(8),
                Background = (Brush)FindResource("MenuHoverBrush"),
                SnapsToDevicePixels = true
            };

            var stack = new StackPanel();

            var actionLine = new StackPanel { Orientation = Orientation.Horizontal };
            var dot = new Border
            {
                Width = 6, Height = 6, CornerRadius = new CornerRadius(3),
                Background = (Brush)FindResource("AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var section = new TextBlock
            {
                Text = e.Section,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("PrimaryForegroundBrush")
            };
            actionLine.Children.Add(dot);
            actionLine.Children.Add(section);
            stack.Children.Add(actionLine);

            var detail = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(e.Details)
                    ? e.Action
                    : $"{e.Action}: {e.Details}",
                FontSize = 10.5,
                Foreground = (Brush)FindResource("SecondaryForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(14, 3, 0, 0),
                MaxHeight = 36
            };
            stack.Children.Add(detail);

            var time = new TextBlock
            {
                Text = e.TimestampUtc.ToLocalTime().ToString("dd MMM HH:mm"),
                FontSize = 9,
                Foreground = (Brush)FindResource("MutedForegroundBrush"),
                Margin = new Thickness(14, 4, 0, 0)
            };
            stack.Children.Add(time);

            row.Child = stack;
            return row;
        }
    }
}
