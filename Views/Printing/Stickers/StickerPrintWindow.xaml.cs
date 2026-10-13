using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ProGlassAutomation.Services.Printing.Stickers;
using ProGlassAutomation.ViewModels.Printing.Stickers;

namespace ProGlassAutomation.Views.Printing.Stickers
{
    public partial class StickerPrintWindow : Window
    {
        private readonly StickerPrintDialogViewModel _viewModel;
        private readonly string _documentName;
        private readonly bool _designOnly;
        private Canvas? _editorCanvas;
        private Border? _dragBorder;
        private StickerLayoutElement? _dragElement;
        private Point _dragStart;
        private double _dragStartLeft;
        private double _dragStartTop;
        private bool _ignoreTemplateSelectionChange = true;

        public StickerPrintWindow(
            System.Collections.Generic.IReadOnlyList<GlassStickerLabel> labels,
            string documentName,
            bool designOnly = false)
        {
            InitializeComponent();
            _designOnly = designOnly;
            _documentName = string.IsNullOrWhiteSpace(documentName)
                ? "ProGlass Glass Stickers" : documentName;
            var settings = StickerPrintPreferencesStore.Load();
            _viewModel = new StickerPrintDialogViewModel(
                labels, settings, designOnly ? CreateDesignerPreviewLabel() : null);
            DataContext = _viewModel;
            if (_designOnly)
            {
                Title = "Sticker Template Library — ProGlass";
                DesignOnlyBanner.Visibility = Visibility.Visible;
                PrintButton.IsEnabled = false;
                PreviewExplanation.Text = "Sample content is for layout positioning only. Save or export the template, then print real source rows from Step 5 or Optimization.";
                SourceLabelsHeader.Text = "NO SOURCE ROWS — this sidebar workspace edits reusable layouts only";
                _viewModel.StatusMessage = "Template library mode. Changes saved here are used by Step 5 and Optimization sticker printing.";
            }
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            StickerItemsGrid.SelectionChanged += (_, _) => RefreshPreview();
            Loaded += (_, _) =>
            {
                _ignoreTemplateSelectionChange = false;
                RefreshPreview();
            };
            Closing += (_, e) =>
            {
                if (!ConfirmTemplateChange()) e.Cancel = true;
            };
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StickerPrintDialogViewModel.SelectedElement)
                || e.PropertyName == nameof(StickerPrintDialogViewModel.HasSelectedElement))
            {
                UpdateSelectionOutlines();
                return;
            }
            if (e.PropertyName == nameof(StickerPrintDialogViewModel.StatusMessage)
                || e.PropertyName == nameof(StickerPrintDialogViewModel.IsTemplateDirty)
                || e.PropertyName == nameof(StickerPrintDialogViewModel.EstimatedLabelCountText)
                || e.PropertyName == nameof(StickerPrintDialogViewModel.EstimatedLabelCount))
                return;
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            var label = _viewModel.SelectedLabel;
            if (label == null)
            {
                _editorCanvas = null;
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
            _editorCanvas = StickerLabelVisualFactory.CreateTemplateCanvas(
                label,
                _viewModel.ActiveTemplate,
                quantity,
                editorMode: true,
                selectedElementId: _viewModel.SelectedElement?.Id);
            _editorCanvas.PreviewKeyDown += EditorCanvas_PreviewKeyDown;
            _editorCanvas.MouseLeftButtonDown += EditorCanvas_MouseLeftButtonDown;
            foreach (var border in _editorCanvas.Children.OfType<Border>())
            {
                border.MouseLeftButtonDown += EditorElement_MouseLeftButtonDown;
                border.MouseMove += EditorElement_MouseMove;
                border.MouseLeftButtonUp += EditorElement_MouseLeftButtonUp;
            }

            PreviewHost.Content = new Viewbox
            {
                Stretch = Stretch.Uniform,
                Child = _editorCanvas,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
        }

        private void UpdateSelectionOutlines()
        {
            if (_editorCanvas == null) return;
            foreach (var border in _editorCanvas.Children.OfType<Border>())
            {
                bool selected = border.Tag is StickerLayoutElement element
                    && ReferenceEquals(element, _viewModel.SelectedElement);
                border.BorderBrush = selected
                    ? Brushes.DodgerBlue
                    : new SolidColorBrush(Color.FromArgb(72, 52, 64, 84));
                border.BorderThickness = new Thickness(selected ? 1.5 : 0.6);
                if (border.Tag is StickerLayoutElement item)
                    border.Cursor = item.IsLocked ? Cursors.Arrow : Cursors.SizeAll;
            }
        }

        private void EditorCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ReferenceEquals(e.OriginalSource, _editorCanvas))
                _viewModel.SelectedElement = null;
            _editorCanvas?.Focus();
        }

        private void EditorElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.Tag is not StickerLayoutElement element)
                return;

            _viewModel.SelectedElement = element;
            _editorCanvas?.Focus();
            if (element.IsLocked)
            {
                e.Handled = true;
                return;
            }

            _dragElement = element;
            _dragBorder = border;
            _dragStart = e.GetPosition(_editorCanvas);
            _dragStartLeft = Canvas.GetLeft(border);
            _dragStartTop = Canvas.GetTop(border);
            if (double.IsNaN(_dragStartLeft)) _dragStartLeft = 0;
            if (double.IsNaN(_dragStartTop)) _dragStartTop = 0;
            border.CaptureMouse();
            e.Handled = true;
        }

        private void EditorElement_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragElement == null || _dragBorder == null || _editorCanvas == null
                || !_dragBorder.IsMouseCaptured)
                return;

            Point current = e.GetPosition(_editorCanvas);
            double nextLeft = _dragStartLeft + current.X - _dragStart.X;
            double nextTop = _dragStartTop + current.Y - _dragStart.Y;
            double maxLeft = Math.Max(0, _editorCanvas.Width - _dragBorder.Width);
            double maxTop = Math.Max(0, _editorCanvas.Height - _dragBorder.Height);
            Canvas.SetLeft(_dragBorder, Math.Clamp(nextLeft, 0, maxLeft));
            Canvas.SetTop(_dragBorder, Math.Clamp(nextTop, 0, maxTop));
        }

        private void EditorElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragElement == null || _dragBorder == null) return;
            var border = _dragBorder;
            double xMm = StickerLabelVisualFactory.DipToMm(Canvas.GetLeft(border));
            double yMm = StickerLabelVisualFactory.DipToMm(Canvas.GetTop(border));
            _dragElement = null;
            _dragBorder = null;
            border.ReleaseMouseCapture();
            _viewModel.SetSelectedElementPosition(xMm, yMm);
            e.Handled = true;
        }

        private void EditorCanvas_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 5 : 1;
            switch (e.Key)
            {
                case Key.Left: _viewModel.NudgeSelectedElement(-step, 0); break;
                case Key.Right: _viewModel.NudgeSelectedElement(step, 0); break;
                case Key.Up: _viewModel.NudgeSelectedElement(0, -step); break;
                case Key.Down: _viewModel.NudgeSelectedElement(0, step); break;
                default: return;
            }
            e.Handled = true;
        }

        private void TemplateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ignoreTemplateSelectionChange || TemplateCombo.SelectedItem is not StickerTemplateChoice choice)
                return;
            if (ReferenceEquals(choice, _viewModel.SelectedTemplateChoice)) return;
            if (!ConfirmTemplateChange())
            {
                TemplateCombo.SelectedItem = _viewModel.SelectedTemplateChoice;
                return;
            }
            _viewModel.SelectTemplateChoice(choice);
            if (_designOnly) SavePreferencesAfterTemplateOperation();
        }

        private void NewTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (ConfirmTemplateChange()) _viewModel.CreateNewTemplate();
        }

        private void OpenTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open a ProGlass sticker template",
                Filter = StickerTemplateStore.FileDialogFilter,
                DefaultExt = StickerTemplateStore.FileExtension,
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != true || !ConfirmTemplateChange()) return;
            if (!StickerTemplateStore.TryLoadFile(dialog.FileName, out var template, out string error)
                || template == null)
            {
                MessageBox.Show(this, error, "Could not open template",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _viewModel.LoadTemplateFile(template, dialog.FileName);
            if (_designOnly) SavePreferencesAfterTemplateOperation();
        }

        private void SaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.TrySaveActiveTemplateToLibrary(out string path, out string error))
            {
                _viewModel.StatusMessage = error;
                MessageBox.Show(this, error, "Could not save template",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SavePreferencesAfterTemplateOperation();
            _viewModel.SetTemplateSavedMessage("Reusable template saved to the local library: " + path);
        }

        private void ExportTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export ProGlass sticker template",
                Filter = "ProGlass sticker template (*.pgsticker)|*.pgsticker",
                DefaultExt = StickerTemplateStore.FileExtension,
                AddExtension = true,
                FileName = SafeSuggestedFileName(_viewModel.TemplateNameText)
            };
            if (dialog.ShowDialog(this) != true) return;
            if (!_viewModel.TryPrepareTemplateForSave(out string error))
            {
                _viewModel.StatusMessage = error;
                MessageBox.Show(this, error, "Could not export template",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!StickerTemplateStore.TrySaveFile(dialog.FileName, _viewModel.ActiveTemplate, out error))
            {
                MessageBox.Show(this, error, "Could not export template",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _viewModel.RegisterExportedTemplate(dialog.FileName);
            SavePreferencesAfterTemplateOperation();
            _viewModel.SetTemplateSavedMessage("Portable .pgsticker file exported: " + dialog.FileName);
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.TryCreatePreferences(out var preferences, out string error,
                    requirePrintableRows: !_designOnly))
            {
                _viewModel.StatusMessage = error;
                return;
            }
            if (StickerPrintPreferencesStore.TrySave(preferences, out string saveError))
            {
                _viewModel.MarkTemplatePersisted();
                _viewModel.StatusMessage = "Printer setup and last-used template saved for this Windows user.";
            }
            else
            {
                _viewModel.StatusMessage = saveError;
                MessageBox.Show(this, saveError, "Could not save printer setup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (_designOnly)
            {
                _viewModel.StatusMessage = "Printing is disabled in the standalone template library. Use Step 5 or Optimization to print real source rows.";
                return;
            }
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

            if (StickerPrintPreferencesStore.TrySave(preferences, out string saveError))
            {
                _viewModel.MarkTemplatePersisted();
                _viewModel.StatusMessage = "Sending the edited template to the printer…";
            }
            else
                _viewModel.StatusMessage = saveError;

            var result = StickerPrintEngine.Print(_viewModel.Items.ToList(), preferences,
                _viewModel.ActiveTemplate, _documentName);
            _viewModel.StatusMessage = result.Message;
            if (!result.Succeeded && !result.Cancelled)
            {
                MessageBox.Show(this, result.Message, "Sticker print failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            if (!string.IsNullOrWhiteSpace(saveError))
                _viewModel.StatusMessage = result.Message + " Setup could not be saved: " + saveError;
        }

        private static GlassStickerLabel CreateDesignerPreviewLabel() => new()
        {
            Company = new StickerCompanyProfile
            {
                CompanyName = "Your company name",
                Address = "Company address",
                Telephone = "Company telephone",
                Website = "company website",
                Email = "company email"
            },
            OrderNumber = "SAMPLE ORDER",
            ProformaNumber = "SAMPLE PI",
            CustomerCompanyName = "Customer company",
            GlassName = "6MM CLEAR — PREVIEW",
            GlassType = "DGU",
            GlassReference = "Glass reference",
            WidthMm = 730,
            HeightMm = 555,
            Quantity = 1,
            PrintedQuantity = 1,
            Reference = "REF",
            PanePosition = "Outer",
            Source = "Design-only sample"
        };

        private static string SafeSuggestedFileName(string? value)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "Custom sticker" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(name) ? "Custom sticker" : name;
        }

        private bool ConfirmTemplateChange()
        {
            if (!_viewModel.IsTemplateDirty) return true;
            var answer = MessageBox.Show(this,
                "The current template has unsaved edits. Save it to the local template library before switching?\n\nYes = save and continue; No = discard edits; Cancel = stay here.",
                "Unsaved sticker template", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (answer == MessageBoxResult.Cancel) return false;
            if (answer == MessageBoxResult.No) return true;

            if (!_viewModel.TrySaveActiveTemplateToLibrary(out string path, out string error))
            {
                MessageBox.Show(this, error, "Could not save current template",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            SavePreferencesAfterTemplateOperation();
            _viewModel.SetTemplateSavedMessage("Current template saved before switching: " + path);
            return true;
        }

        private void SavePreferencesAfterTemplateOperation()
        {
            if (!_viewModel.TryCreatePreferences(out var preferences, out string validationError,
                    requirePrintableRows: false))
            {
                _viewModel.StatusMessage = "The template was selected, but its default layout could not be saved: " + validationError;
                return;
            }

            if (!StickerPrintPreferencesStore.TrySave(preferences, out string saveError))
            {
                MessageBox.Show(this,
                    "The template is available in the local library, but Step 5's last-used layout could not be updated." +
                    Environment.NewLine + Environment.NewLine + saveError,
                    "Could not save default sticker layout", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AddText_Click(object sender, RoutedEventArgs e) => _viewModel.AddTextElement();
        private void AddField_Click(object sender, RoutedEventArgs e) => _viewModel.AddFieldElement();
        private void AddLogo_Click(object sender, RoutedEventArgs e) => _viewModel.AddLogoElement();
        private void AddBox_Click(object sender, RoutedEventArgs e) => _viewModel.AddRectangleElement();
        private void AddLine_Click(object sender, RoutedEventArgs e) => _viewModel.AddLineElement();
        private void DuplicateElement_Click(object sender, RoutedEventArgs e) => _viewModel.DuplicateSelectedElement();
        private void DeleteElement_Click(object sender, RoutedEventArgs e) => _viewModel.DeleteSelectedElement();
        private void BringForward_Click(object sender, RoutedEventArgs e) => _viewModel.MoveSelectedElementLayer(1);
        private void SendBackward_Click(object sender, RoutedEventArgs e) => _viewModel.MoveSelectedElementLayer(-1);
        private void MoveLeft_Click(object sender, RoutedEventArgs e) => _viewModel.NudgeSelectedElement(-1, 0);
        private void MoveRight_Click(object sender, RoutedEventArgs e) => _viewModel.NudgeSelectedElement(1, 0);
        private void MoveUp_Click(object sender, RoutedEventArgs e) => _viewModel.NudgeSelectedElement(0, -1);
        private void MoveDown_Click(object sender, RoutedEventArgs e) => _viewModel.NudgeSelectedElement(0, 1);
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
