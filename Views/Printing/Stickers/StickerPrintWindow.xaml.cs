using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services.Printing.Stickers;
using ProGlassAutomation.ViewModels.Printing.Stickers;

namespace ProGlassAutomation.Views.Printing.Stickers
{
    public partial class StickerPrintWindow : Window
    {
        private readonly StickerPrintDialogViewModel _viewModel;
        private readonly string _documentName;
        private StickerPrintPreferences _lastValidPreferences;

        public StickerPrintWindow(
            IReadOnlyList<GlassStickerLabel> labels,
            string documentName)
        {
            InitializeComponent();
            _documentName = string.IsNullOrWhiteSpace(documentName)
                ? "ProGlass Glass Stickers" : documentName;
            _lastValidPreferences = StickerPrintPreferencesStore.Load();
            _viewModel = new StickerPrintDialogViewModel(labels, _lastValidPreferences);
            DataContext = _viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            StickerItemsGrid.SelectionChanged += (_, _) => RefreshPreview();
            Loaded += (_, _) => RefreshPreview();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_viewModel.TryCreatePreferences(out var preferences, out _))
                _lastValidPreferences = preferences;
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            var label = _viewModel.SelectedLabel;
            if (label == null)
            {
                PreviewHost.Content = new TextBlock
                {
                    Text = "No printable source rows.",
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                return;
            }

            int quantity = _viewModel.OneLabelPerPiece ? 1 : label.Quantity;
            var visual = StickerLabelVisualFactory.Create(label, _lastValidPreferences, quantity);
            PreviewHost.Content = new Viewbox
            {
                Stretch = Stretch.Uniform,
                Child = visual,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.TryCreatePreferences(out var preferences, out string error))
            {
                _viewModel.StatusMessage = error;
                return;
            }

            if (StickerPrintPreferencesStore.TrySave(preferences, out string saveError))
            {
                _lastValidPreferences = preferences;
                _viewModel.StatusMessage = "Sticker layout saved on this Windows user profile.";
            }
            else
            {
                _viewModel.StatusMessage = saveError;
                MessageBox.Show(this, saveError, "Could not save sticker layout",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.TryCreatePreferences(out var preferences, out string error))
            {
                _viewModel.StatusMessage = error;
                MessageBox.Show(this, error, "Check sticker setup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            long count = _viewModel.EstimatedLabelCount;
            if (count > 500)
            {
                var confirm = MessageBox.Show(this,
                    "This will send " + count.ToString("N0") + " sticker pages to the selected printer. Continue?",
                    "Confirm sticker batch", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;
            }

            // Save the chosen stock/layout before opening the driver dialog so
            // the operator's setup survives even if a queue is unavailable.
            if (StickerPrintPreferencesStore.TrySave(preferences, out string saveError))
                _lastValidPreferences = preferences;

            var result = StickerPrintEngine.Print(_viewModel.Items.ToList(), preferences, _documentName);
            _viewModel.StatusMessage = result.Message;
            if (!result.Succeeded && !result.Cancelled)
            {
                MessageBox.Show(this, result.Message, "Sticker print failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (result.Succeeded && !string.IsNullOrWhiteSpace(saveError))
            {
                _viewModel.StatusMessage = result.Message + " Preferences could not be saved: " + saveError;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
