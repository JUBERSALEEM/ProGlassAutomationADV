// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Shared/ActivityPanel.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — notifications / activity popup
//
// Last 20 audit rows. Today / All is a filter on that list. There is no
// unread store, so this panel does not mark rows read and does not show
// a badge. Session ids stay off the row.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Audit;
using ProGlassAutomation.Services.Audit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProGlassAutomation.Views.Shared
{
    public partial class ActivityPanel : UserControl
    {
        private List<ActivityLogEntry> _loaded = new();
        private bool _todayOnly;

        public ActivityPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private void TodayFilter_Click(object sender, RoutedEventArgs e)
        {
            _todayOnly = true;
            Render();
        }

        private void AllFilter_Click(object sender, RoutedEventArgs e)
        {
            _todayOnly = false;
            Render();
        }

        public async Task LoadAsync()
        {
            var entries = await AuditService.GetRecentAsync(20);
            _loaded = entries?.ToList() ?? new List<ActivityLogEntry>();
            Render();
        }

        private void Render()
        {
            PaintFilters();
            ActivityList.Children.Clear();

            IEnumerable<ActivityLogEntry> rows = _loaded;
            if (_todayOnly)
                rows = _loaded.Where(e => e.TimestampUtc.ToLocalTime().Date == DateTime.Today);

            var list = rows.ToList();
            if (list.Count == 0)
            {
                ActivityList.Children.Add(new TextBlock
                {
                    Text = _todayOnly ? TodayEmptyText() : "No activity yet.",
                    FontSize = 12,
                    Foreground = (Brush)FindResource("MutedForegroundBrush"),
                    Margin = new Thickness(4, 8, 4, 8)
                });
                return;
            }

            foreach (var entry in list)
                ActivityList.Children.Add(BuildRow(entry));
        }

        private void PaintFilters()
        {
            Paint(TodayFilter, _todayOnly);
            Paint(AllFilter, !_todayOnly);
        }

        private string TodayEmptyText()
        {
            try
            {
                if (TryFindResource("Shell.NoActivityToday") is string text && text.Length > 0)
                    return text;
            }
            catch { }
            return "No activity today.";
        }

        private void Paint(Button button, bool selected)
        {
            button.Background = (Brush)FindResource(selected ? "AccentBrush" : "SurfacePrimaryBrush");
            button.Foreground = (Brush)FindResource(selected ? "OnAccentForegroundBrush" : "TextPrimaryBrush");
            button.BorderBrush = (Brush)FindResource(selected ? "AccentBrush" : "BorderDefaultBrush");
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
            actionLine.Children.Add(new Border
            {
                Width = 6, Height = 6, CornerRadius = new CornerRadius(3),
                Background = (Brush)FindResource("AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            var title = string.IsNullOrWhiteSpace(e.Action)
                ? e.Section
                : $"{e.Section} · {e.Action}";
            actionLine.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("PrimaryForegroundBrush"),
                TextWrapping = TextWrapping.Wrap
            });
            stack.Children.Add(actionLine);

            string actor = string.IsNullOrWhiteSpace(e.UserDisplayName)
                ? e.Username
                : e.UserDisplayName;
            if (string.IsNullOrWhiteSpace(actor)) actor = "system";
            if (!string.IsNullOrWhiteSpace(e.UserRole))
                actor += $" ({e.UserRole})";

            stack.Children.Add(new TextBlock
            {
                Text = actor,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("PrimaryForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(14, 3, 0, 0)
            });

            if (!string.IsNullOrWhiteSpace(e.Details))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = e.Details,
                    FontSize = 10.5,
                    Foreground = (Brush)FindResource("SecondaryForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(14, 2, 0, 0),
                    MaxHeight = 34
                });
            }

            string when = e.TimestampUtc.ToLocalTime().ToString("dd MMM HH:mm");
            string record = $"{e.EntityType} {e.EntityId}".Trim();
            if (!string.IsNullOrWhiteSpace(record))
                when += " · " + record;

            stack.Children.Add(new TextBlock
            {
                Text = when,
                FontSize = 9,
                Foreground = (Brush)FindResource("MutedForegroundBrush"),
                Margin = new Thickness(14, 4, 0, 0)
            });

            row.Child = stack;
            return row;
        }
    }
}
