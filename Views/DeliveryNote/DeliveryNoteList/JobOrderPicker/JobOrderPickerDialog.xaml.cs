using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.Views.DeliveryNote.DeliveryNoteList.JobOrderPicker
{
    /// <summary>
    /// Frameless Job Order picker dialog.
    /// Shows all Job Orders — user clicks one to select, then confirms.
    /// Returns SelectedJobOrder (non-null) when DialogResult = true.
    ///
    /// Phase 10.7a: Created for "Create DN" flow from DN list view.
    ///
    /// Gotcha #16: No ": Window" on partial — XAML-generated partial has it.
    /// Gotcha #37: Constructor with InitializeComponent() kept.
    /// Gotcha #30: Namespace matches folder path.
    /// </summary>
    public partial class JobOrderPickerDialog
    {
        // ── Result ────────────────────────────────────────────────────────
        public SavedJobOrder? SelectedJobOrder { get; private set; }

        // ── State ─────────────────────────────────────────────────────────
        private List<SavedJobOrder> _allJobs = new();
        private Dictionary<string, List<string>> _dnsByJob = new(StringComparer.OrdinalIgnoreCase);
        private SavedJobOrder? _highlighted;

        // ─────────────────────────────────────────────────────────────────

        public JobOrderPickerDialog(
            List<SavedJobOrder> jobOrders,
            IEnumerable<ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote.DeliveryNoteListItem>? deliveryNotes = null)
        {
            InitializeComponent();
            _allJobs = jobOrders
                .OrderByDescending(j => j.CreatedAt)
                .ToList();
            if (deliveryNotes != null)
            {
                _dnsByJob = deliveryNotes
                    .Where(d => !string.IsNullOrWhiteSpace(d.JobOrderId))
                    .GroupBy(d => d.JobOrderId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => x.DeliveryNoteId ?? "").ToList(),
                        StringComparer.OrdinalIgnoreCase);
            }
            RenderList(_allJobs);
        }

        // ── Render job order rows ─────────────────────────────────────────

        private void RenderList(List<SavedJobOrder> jobs)
        {
            JobOrderList.Children.Clear();

            if (jobs.Count == 0)
            {
                var empty = new TextBlock
                {
                    Text = "No job orders found.",
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 32, 0, 0),
                    Opacity = 0.5
                };
                JobOrderList.Children.Add(empty);
                return;
            }

            foreach (var jo in jobs)
            {
                var row = BuildRow(jo);
                JobOrderList.Children.Add(row);
            }
        }

        private Border BuildRow(SavedJobOrder jo)
        {
            // Status pill
            var statusPill = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 2, 8, 2),
                Background = GetStatusBrush(jo.Status),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            statusPill.Child = new TextBlock
            {
                Text = jo.Status,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };

            // JO ID
            var idBlock = new TextBlock
            {
                Text = jo.JobOrderId,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources["PrimaryForegroundBrush"]
            };

            // Customer · Project
            var subBlock = new TextBlock
            {
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = (Brush)Application.Current.Resources["SecondaryForegroundBrush"],
                Text = $"{jo.CustomerName}  ·  {jo.ProjectName}"
            };

            // Date
            var dateBlock = new TextBlock
            {
                Text = jo.DeliveryDate.HasValue
                    ? $"📅 {jo.DeliveryDate.Value:dd MMM yyyy}"
                    : "📅 No date",
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 0),
                Foreground = (Brush)Application.Current.Resources["SecondaryForegroundBrush"],
                Opacity = 0.8
            };

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(idBlock);
            infoStack.Children.Add(subBlock);
            infoStack.Children.Add(dateBlock);

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(infoStack, 0);
            Grid.SetColumn(statusPill, 1);
            rowGrid.Children.Add(infoStack);
            rowGrid.Children.Add(statusPill);

            var row = new Border
            {
                Tag = jo,
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 6),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Child = rowGrid
            };

            SetRowDeselected(row);

            row.MouseLeftButtonUp += OnRowClick;
            row.MouseEnter += (s, _) =>
            {
                if (row.Tag is SavedJobOrder j && j != _highlighted)
                    row.BorderBrush = (Brush)Application.Current.Resources["AccentBrush"];
            };
            row.MouseLeave += (s, _) =>
            {
                if (row.Tag is SavedJobOrder j && j != _highlighted)
                    SetRowDeselected(row);
            };

            return row;
        }

        private void OnRowClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border clickedRow) return;
            if (clickedRow.Tag is not SavedJobOrder jo) return;

            // Deselect all rows
            foreach (var child in JobOrderList.Children)
            {
                if (child is Border b) SetRowDeselected(b);
            }

            // Select clicked row
            SetRowSelected(clickedRow);
            _highlighted = jo;
            SelectedJobOrder = jo;

            // Update footer
            SelectedLabel.Text = $"Selected: {jo.JobOrderId}  ·  {jo.CustomerName}";
            ConfirmButton.IsEnabled = true;
        }

        private static void SetRowSelected(Border row)
        {
            row.Background = new SolidColorBrush(Color.FromRgb(238, 242, 255));
            row.BorderBrush = new LinearGradientBrush(
                Color.FromRgb(67, 56, 202),
                Color.FromRgb(99, 102, 241),
                new Point(0, 0), new Point(1, 1));
            row.BorderThickness = new Thickness(2);
        }

        private static void SetRowDeselected(Border row)
        {
            row.Background = new SolidColorBrush(Colors.Transparent);
            row.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            row.BorderThickness = new Thickness(1);
        }

        private static Brush GetStatusBrush(string status) => status switch
        {
            "Released" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
            "InProduction" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            "Completed" => new SolidColorBrush(Color.FromRgb(99, 102, 241)),
            "Delivered" => new SolidColorBrush(Color.FromRgb(107, 114, 128)),
            _ => new SolidColorBrush(Color.FromRgb(156, 163, 175))
        };

        // ── Search ────────────────────────────────────────────────────────

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            var q = SearchBox.Text.Trim().ToLowerInvariant();

            var filtered = string.IsNullOrWhiteSpace(q)
                ? _allJobs
                : _allJobs.Where(j => MatchesSearch(j, q)).ToList();

            _highlighted = null;
            SelectedJobOrder = null;
            SelectedLabel.Text = "No job order selected";
            ConfirmButton.IsEnabled = false;

            RenderList(filtered);
        }

        private bool MatchesSearch(SavedJobOrder j, string q)
        {
            if ((j.JobOrderId ?? "").ToLowerInvariant().Contains(q)) return true;
            if ((j.CustomerName ?? "").ToLowerInvariant().Contains(q)) return true;
            if ((j.ProjectName ?? "").ToLowerInvariant().Contains(q)) return true;
            if ((j.SourceProformaId ?? "").ToLowerInvariant().Contains(q)) return true;
            if (_dnsByJob.TryGetValue(j.JobOrderId ?? "", out var dns) &&
                dns.Any(id => (id ?? "").ToLowerInvariant().Contains(q)))
                return true;
            return false;
        }

        // ── Buttons ───────────────────────────────────────────────────────

        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            if (SelectedJobOrder == null) return;
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}