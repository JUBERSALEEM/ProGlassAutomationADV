using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.Views.TaxInvoice.TaxInvoiceList
{
    /// <summary>
    /// Dialog for selecting a Proforma Invoice to create a Tax Invoice from.
    ///
    /// Loads all saved PIs from App.Invoices (IProformaInvoiceRepository).
    /// Filters by search text in real time.
    /// Returns SelectedProformaId on confirm.
    ///
    /// FIX: p.GrandTotal → p.GrandTotalVatInclusive
    ///      (confirmed from ProformaInvoicePdfLayout.cs: entity.GrandTotalVatInclusive)
    /// FIX: All Thickness constructors verified — WPF accepts 1 or 4 args only.
    /// </summary>
    public partial class ProformaPickerDialog : Window
    {
        // ── Result ────────────────────────────────────────────────────────

        public string SelectedProformaId { get; private set; } = "";

        // ── State ─────────────────────────────────────────────────────────

        private List<ProformaPickerItem> _allItems = new();
        private string _selectedPiId = "";

        // ── Constructor ───────────────────────────────────────────────────

        public ProformaPickerDialog()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            Loaded += OnLoaded;
        }

        // ── Load ──────────────────────────────────────────────────────────

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (App.Invoices == null)
                {
                    ShowError("Proforma Invoice database not initialized.");
                    return;
                }

                var proformas = await App.Invoices.GetAllAsync();

                if (proformas == null || proformas.Count == 0)
                {
                    ShowError(
                        "No Proforma Invoices found.\n\n" +
                        "Create a Proforma Invoice first, then " +
                        "generate a Tax Invoice from it.");
                    return;
                }

                _allItems = proformas
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new ProformaPickerItem
                    {
                        ProformaId = p.ProformaId ?? "",
                        CustomerName = p.CustomerName ?? "",
                        ProjectName = p.ProjectName ?? "",

                        // FIX: GrandTotal → GrandTotalVatInclusive
                        // Confirmed from ProformaInvoicePdfLayout.cs:
                        //   GrandTotal = entity.GrandTotalVatInclusive
                        GrandTotal = p.GrandTotalVatInclusive,

                        Status = p.Status ?? "",
                        CreatedAt = p.CreatedAt
                    })
                    .ToList();

                RenderList(_allItems);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load Proforma Invoices:\n{ex.Message}");
            }
        }

        // ── Search ────────────────────────────────────────────────────────

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(query))
            {
                RenderList(_allItems);
                return;
            }

            var filtered = _allItems.Where(p =>
                p.ProformaId.ToLowerInvariant().Contains(query) ||
                p.CustomerName.ToLowerInvariant().Contains(query) ||
                p.ProjectName.ToLowerInvariant().Contains(query))
                .ToList();

            RenderList(filtered);
        }

        // ── Render ────────────────────────────────────────────────────────

        private void RenderList(List<ProformaPickerItem> items)
        {
            PiList.Children.Clear();

            if (items.Count == 0)
            {
                PiList.Children.Add(new TextBlock
                {
                    Text = "No results found.",
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    // FIX: Thickness(double left, double top, double right, double bottom)
                    // Must be 1 or 4 args — using 4 args here
                    Margin = new Thickness(0, 24, 0, 0),
                    Foreground = SystemColors.GrayTextBrush
                });
                return;
            }

            foreach (var item in items)
                PiList.Children.Add(BuildPiCard(item));
        }

        private Border BuildPiCard(ProformaPickerItem item)
        {
            bool isSelected = item.ProformaId == _selectedPiId;

            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                // FIX: 4 args — left, top, right, bottom
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 8),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                Cursor = Cursors.Hand,
                Tag = item.ProformaId
            };

            card.SetResourceReference(Border.BackgroundProperty,
                "ModuleBackgroundBrush");
            card.SetResourceReference(Border.BorderBrushProperty,
                isSelected ? "AccentBrush" : "BorderBrush");

            card.MouseEnter += (_, _) =>
            {
                if (card.Tag?.ToString() != _selectedPiId)
                    card.SetResourceReference(
                        Border.BackgroundProperty, "MenuHoverBrush");
            };
            card.MouseLeave += (_, _) =>
            {
                if (card.Tag?.ToString() != _selectedPiId)
                    card.SetResourceReference(
                        Border.BackgroundProperty, "ModuleBackgroundBrush");
            };

            card.MouseLeftButtonUp += (_, _) => SelectItem(item.ProformaId);

            // ── Card content ──────────────────────────────────────────────

            var stack = new StackPanel();

            // Row 1: PI number + amount
            var topRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                // FIX: 4 args
                Margin = new Thickness(0, 0, 0, 4)
            };

            var piLabel = new TextBlock
            {
                Text = $"📋  {item.ProformaId}",
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            piLabel.SetResourceReference(
                TextBlock.ForegroundProperty, "PrimaryForegroundBrush");

            var amountLabel = new TextBlock
            {
                // FIX: Uses GrandTotalVatInclusive from ProformaPickerItem.GrandTotal
                Text = $"  AED {item.GrandTotal:N2}",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                // FIX: 4 args
                Margin = new Thickness(12, 0, 0, 0),
                Foreground = new SolidColorBrush(
                    Color.FromRgb(4, 120, 87))  // #047857 green
            };

            topRow.Children.Add(piLabel);
            topRow.Children.Add(amountLabel);

            // Row 2: Customer · Project
            var metaRow = new TextBlock
            {
                FontSize = 11,
                // FIX: 4 args
                Margin = new Thickness(0, 0, 0, 2),
                Text = $"{item.CustomerName}  ·  {item.ProjectName}",
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            metaRow.SetResourceReference(
                TextBlock.ForegroundProperty, "SecondaryForegroundBrush");

            // Row 3: Date · Status
            var dateRow = new TextBlock
            {
                Text = $"📅  {item.CreatedAt:dd MMM yyyy}  ·  {item.Status}",
                FontSize = 10
            };
            dateRow.SetResourceReference(
                TextBlock.ForegroundProperty, "MutedForegroundBrush");

            stack.Children.Add(topRow);
            stack.Children.Add(metaRow);
            stack.Children.Add(dateRow);

            card.Child = stack;
            return card;
        }

        // ── Selection ─────────────────────────────────────────────────────

        private void SelectItem(string proformaId)
        {
            _selectedPiId = proformaId;

            string q = SearchBox.Text.Trim().ToLowerInvariant();
            var source = string.IsNullOrEmpty(q)
                ? _allItems
                : _allItems.Where(p =>
                    p.ProformaId.ToLowerInvariant().Contains(q) ||
                    p.CustomerName.ToLowerInvariant().Contains(q) ||
                    p.ProjectName.ToLowerInvariant().Contains(q))
                    .ToList();

            RenderList(source);

            var selected = _allItems
                .FirstOrDefault(p => p.ProformaId == proformaId);

            SelectedLabel.Text = selected != null
                ? $"Selected: {selected.ProformaId} — {selected.CustomerName}"
                : "No selection";

            ConfirmButton.IsEnabled =
                !string.IsNullOrWhiteSpace(_selectedPiId);
        }

        // ── Dialog result ─────────────────────────────────────────────────

        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedPiId)) return;
            SelectedProformaId = _selectedPiId;
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // ── Error ─────────────────────────────────────────────────────────

        private void ShowError(string message)
        {
            PiList.Children.Clear();
            PiList.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                // FIX: 4 args
                Margin = new Thickness(16, 24, 16, 0),
                Foreground = new SolidColorBrush(
                    Color.FromRgb(185, 28, 28))  // #B91C1C
            });
        }
    }

    // ── DTO ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Lightweight PI summary for picker display.
    /// GrandTotal maps to SavedProformaInvoice.GrandTotalVatInclusive.
    /// </summary>
    internal sealed class ProformaPickerItem
    {
        public string ProformaId { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public string ProjectName { get; init; } = "";

        /// <summary>
        /// Maps to SavedProformaInvoice.GrandTotalVatInclusive.
        /// Named GrandTotal here for display clarity.
        /// </summary>
        public decimal GrandTotal { get; init; }

        public string Status { get; init; } = "";
        public DateTime CreatedAt { get; init; }
    }
}