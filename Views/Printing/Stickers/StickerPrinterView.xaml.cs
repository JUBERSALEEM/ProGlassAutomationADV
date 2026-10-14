using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ProGlassAutomation.Services.Printing.Stickers;
using ProGlassAutomation.ViewModels.Printing.Stickers;

namespace ProGlassAutomation.Views.Printing.Stickers
{
    public partial class StickerPrinterView : UserControl
    {
        private StickerPrintDialogViewModel _viewModel = null!;
        private string _documentName = "ProGlass Glass Stickers";
        private bool _designOnly;
        private bool _modeExplicit;
        private Canvas? _editorCanvas;
        private Border? _dragBorder;
        private StickerLayoutElement? _dragElement;
        private Point _dragStart;
        private double _dragStartLeft;
        private double _dragStartTop;
        private readonly System.Collections.Generic.List<Thumb> _resizeThumbs = new();
        private StickerLayoutElement? _resizeElement;
        private StickerLayoutElement? _resizeStartGeometry;
        private StickerResizeEdges _resizeEdges;
        private Border? _resizeTarget;
        private Point _resizeStartPoint;
        private bool _isResizing;
        private bool _showSheetPreview;
        private bool _isSelectingStudioTab;
        private long _previewSheetPageIndex;
        private long _previewSheetPageCount;
        private bool _ignoreTemplateSelectionChange = true;

        private static readonly (StickerResizeEdges Edges, Cursor Cursor, double Width, double Height)[] ResizeHandleDefinitions =
        {
            (StickerResizeEdges.Left | StickerResizeEdges.Top, Cursors.SizeNWSE, 12, 12),
            (StickerResizeEdges.Top, Cursors.SizeNS, 14, 10),
            (StickerResizeEdges.Right | StickerResizeEdges.Top, Cursors.SizeNESW, 12, 12),
            (StickerResizeEdges.Right, Cursors.SizeWE, 10, 14),
            (StickerResizeEdges.Right | StickerResizeEdges.Bottom, Cursors.SizeNWSE, 12, 12),
            (StickerResizeEdges.Bottom, Cursors.SizeNS, 14, 10),
            (StickerResizeEdges.Left | StickerResizeEdges.Bottom, Cursors.SizeNESW, 12, 12),
            (StickerResizeEdges.Left, Cursors.SizeWE, 10, 14)
        };

        public StickerPrinterView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (_, _) =>
            {
                _ignoreTemplateSelectionChange = false;
                if (_viewModel != null) RefreshPreview();
            };
        }

        /// <summary>Configure this shared editor for the source-row print dialog.</summary>
        internal void InitializeForPrint(
            System.Collections.Generic.IReadOnlyList<GlassStickerLabel> labels,
            string documentName,
            bool designOnly = false)
        {
            _modeExplicit = true;
            _designOnly = designOnly;
            _documentName = string.IsNullOrWhiteSpace(documentName)
                ? "ProGlass Glass Stickers" : documentName;
            var settings = StickerPrintPreferencesStore.Load();
            DataContext = new StickerPrintDialogViewModel(labels, settings);
            ConfigureStudioMode();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is StickerPrintDialogViewModel previous)
                previous.PropertyChanged -= OnViewModelPropertyChanged;

            if (e.NewValue is not StickerPrintDialogViewModel viewModel) return;
            _viewModel = viewModel;
            if (!_modeExplicit)
            {
                _designOnly = viewModel is StickerPrinterViewModel;
                _documentName = _designOnly ? "Sticker Template Library" : "ProGlass Glass Stickers";
            }
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ConfigureStudioMode();
            UpdateWindowTitle();
            RefreshPreview();
        }

        private void ConfigureStudioMode()
        {
            if (_viewModel == null) return;

            DesignOnlyBanner.Visibility = _designOnly ? Visibility.Visible : Visibility.Collapsed;
            PrintButton.Visibility = _designOnly ? Visibility.Collapsed : Visibility.Visible;
            PrintSampleButton.Visibility = _designOnly ? Visibility.Visible : Visibility.Collapsed;
            CloseButton.Visibility = _designOnly ? Visibility.Collapsed : Visibility.Visible;

            StudioHeaderTitle.Text = _designOnly ? "Label layout workspace" : "Production sticker run";
            StudioHeaderSubtitle.Text = _designOnly
                ? "Build reusable glass labels with test-only sample data."
                : "Review the source batch, preview the label, and print when ready.";
            SourceLabelsHeader.Text = _designOnly
                ? "Test sample only · no source rows" : "Production source rows";
            SourceEmptyState.Text = _designOnly
                ? "No production rows are loaded. This workspace previews and prints clearly marked test data without Step 5 or a PI/job."
                : "No source rows are attached. Open the standalone Sticker Printer Studio for a sample, or load real panes from Step 5 or Optimization.";

            bool hasSourceRows = _viewModel.Items.Count > 0;
            StickerItemsGrid.Visibility = hasSourceRows ? Visibility.Visible : Visibility.Collapsed;
            SourceEmptyState.Visibility = hasSourceRows ? Visibility.Collapsed : Visibility.Visible;
            if (_designOnly && string.IsNullOrWhiteSpace(_viewModel.StatusMessage))
                _viewModel.StatusMessage = "Standalone sample mode. Print sample uses test-only values; production data remains in Step 5 and Optimization.";

            UpdateA4SheetUi();
            SelectStudioTab(_viewModel.SelectedStudioTabIndex);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StickerPrintDialogViewModel.TemplateNameText))
            {
                UpdateWindowTitle();
                return;
            }
            if (e.PropertyName == nameof(StickerPrintDialogViewModel.SelectedStudioTabIndex))
            {
                SelectStudioTab(_viewModel.SelectedStudioTabIndex);
                return;
            }
            if (e.PropertyName == nameof(StickerPrintDialogViewModel.EstimatedLabelCountText)
                || e.PropertyName == nameof(StickerPrintDialogViewModel.EstimatedLabelCount))
            {
                if (!_designOnly) PreviewCountBadge.Text = _viewModel.EstimatedPrintPlanSummaryText;
                return;
            }
            if (e.PropertyName == nameof(StickerPrintDialogViewModel.IsA4SheetLayout))
            {
                UpdateA4SheetUi();
                RefreshPreview();
                return;
            }
            if (e.PropertyName == nameof(StickerPrintDialogViewModel.SheetLayoutSummaryText))
            {
                if (_showSheetPreview) RefreshPreview();
                return;
            }
            if (e.PropertyName == nameof(StickerPrintDialogViewModel.SourceRowPositionText)
                || e.PropertyName == nameof(StickerPrintDialogViewModel.CanSelectPreviousSourceRow)
                || e.PropertyName == nameof(StickerPrintDialogViewModel.CanSelectNextSourceRow))
            {
                UpdatePreviewNavigation();
                return;
            }
            if (e.PropertyName == nameof(StickerPrintDialogViewModel.EstimatedPrintPlanSummaryText))
            {
                if (!_designOnly) PreviewCountBadge.Text = _viewModel.EstimatedPrintPlanSummaryText;
                return;
            }
            if (_isResizing && e.PropertyName == nameof(StickerPrintDialogViewModel.EditorRevision))
                return;
            if (e.PropertyName == nameof(StickerPrintDialogViewModel.SelectedElement)
                || e.PropertyName == nameof(StickerPrintDialogViewModel.HasSelectedElement)
                || e.PropertyName == nameof(StickerPrintDialogViewModel.CanEditSampleDimensions))
            {
                UpdateSelectionOutlines();
                return;
            }
            if (e.PropertyName == nameof(StickerPrintDialogViewModel.StatusMessage)
                || e.PropertyName == nameof(StickerPrintDialogViewModel.IsTemplateDirty)
                || e.PropertyName == nameof(StickerPrintDialogViewModel.SampleWidthMmText)
                || e.PropertyName == nameof(StickerPrintDialogViewModel.SampleHeightMmText)
                || e.PropertyName == nameof(StickerPrintDialogViewModel.SampleDimensionsWarningText))
                return;
            RefreshPreview();
        }

        private void UpdateWindowTitle()
        {
            if (FindOwnerWindow() is not StickerPrintWindow printWindow || _viewModel == null) return;
            string templateName = string.IsNullOrWhiteSpace(_viewModel.TemplateNameText)
                ? "Untitled sticker" : _viewModel.TemplateNameText.Trim();
            printWindow.Title = _designOnly
                ? templateName + " — Sticker Template Library"
                : templateName + " — Sticker Printer Studio";
        }

        private void StudioTab_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || _isSelectingStudioTab || sender is not RadioButton selected) return;
            int index = ReferenceEquals(selected, DesignTabButton) ? 0
                : ReferenceEquals(selected, TemplatesTabButton) ? 1
                : ReferenceEquals(selected, PrinterTabButton) ? 2
                : ReferenceEquals(selected, SourceTabButton) ? 3 : -1;
            if (index < 0) return;
            _viewModel.SelectedStudioTabIndex = index;
        }

        private void SelectStudioTab(int index)
        {
            if (_viewModel == null) return;
            index = Math.Clamp(index, 0, 3);
            RadioButton target = index switch
            {
                1 => TemplatesTabButton,
                2 => PrinterTabButton,
                3 => SourceTabButton,
                _ => DesignTabButton
            };
            _isSelectingStudioTab = true;
            try
            {
                if (!target.IsChecked.GetValueOrDefault()) target.IsChecked = true;
            }
            finally
            {
                _isSelectingStudioTab = false;
            }
            UpdateStudioTabVisibility();
        }

        private void UpdateStudioTabVisibility()
        {
            if (_viewModel == null) return;
            int index = Math.Clamp(_viewModel.SelectedStudioTabIndex, 0, 3);
            bool showSources = index == 3;
            if (index != 0) CancelElementDrag();
            DesignWorkspace.Visibility = showSources ? Visibility.Collapsed : Visibility.Visible;
            SourceWorkspace.Visibility = showSources ? Visibility.Visible : Visibility.Collapsed;
            TemplateSettingsCard.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            PrinterSettingsCard.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
            DesignElementsCard.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            SelectedElementCard.Visibility = index == 0 && _viewModel.HasSelectedElement
                ? Visibility.Visible : Visibility.Collapsed;

            // Source review needs more width for the read-only table; the preview
            // remains visible and follows the currently selected source row.
            WorkspaceGrid.ColumnDefinitions[0].Width = showSources
                ? new GridLength(1.65, GridUnitType.Star)
                : new GridLength(1.15, GridUnitType.Star);
            WorkspaceGrid.ColumnDefinitions[2].Width = showSources
                ? new GridLength(0.9, GridUnitType.Star)
                : new GridLength(1, GridUnitType.Star);

            UpdateSampleSizeEditorVisibility();
            RefreshPreview();
        }

        private void UpdateA4SheetUi()
        {
            if (_viewModel == null) return;
            bool sheetMode = _viewModel.IsA4SheetLayout;
            SheetLayoutSettingsPanel.Visibility = sheetMode ? Visibility.Visible : Visibility.Collapsed;
            SheetPreviewButton.Visibility = sheetMode ? Visibility.Visible : Visibility.Collapsed;
            if (!sheetMode)
            {
                _showSheetPreview = false;
                _previewSheetPageIndex = 0;
                _previewSheetPageCount = 0;
            }
            SheetPreviewButton.Content = _showSheetPreview ? "Show label" : "Show A4 sheet";
            UpdatePreviewHeading();
            UpdatePreviewNavigation();
            if (!_designOnly) PreviewCountBadge.Text = _viewModel.EstimatedPrintPlanSummaryText;
            else PreviewCountBadge.Text = SamplePreviewBadgeText();
        }

        private void SheetPreview_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || !_viewModel.IsA4SheetLayout) return;
            _showSheetPreview = !_showSheetPreview;
            if (_showSheetPreview) _previewSheetPageIndex = 0;
            SheetPreviewButton.Content = _showSheetPreview ? "Show label" : "Show A4 sheet";
            UpdatePreviewHeading();
            RefreshPreview();
        }

        private void PreviousPreview_Click(object sender, RoutedEventArgs e)
        {
            if (_showSheetPreview && _viewModel.IsA4SheetLayout)
            {
                if (_previewSheetPageIndex <= 0) return;
                _previewSheetPageIndex--;
                RefreshPreview();
                return;
            }

            _viewModel.SelectPreviousSourceRow();
        }

        private void NextPreview_Click(object sender, RoutedEventArgs e)
        {
            if (_showSheetPreview && _viewModel.IsA4SheetLayout)
            {
                if (_previewSheetPageIndex >= _previewSheetPageCount - 1) return;
                _previewSheetPageIndex++;
                RefreshPreview();
                return;
            }

            _viewModel.SelectNextSourceRow();
        }

        private void UpdatePreviewHeading()
        {
            if (_viewModel == null) return;
            PreviewHeading.Text = _showSheetPreview && _viewModel.IsA4SheetLayout
                ? "A4 sheet preview"
                : _viewModel.IsSamplePreview ? "Sample label preview" : "Production label preview";
        }

        private void UpdatePreviewNavigation()
        {
            if (_viewModel == null) return;

            bool sheetNavigation = _showSheetPreview && _viewModel.IsA4SheetLayout;
            long itemCount = sheetNavigation ? _previewSheetPageCount : _viewModel.Items.Count;
            bool canNavigate = itemCount > 1;
            PreviewNavigationPanel.Visibility = canNavigate ? Visibility.Visible : Visibility.Collapsed;
            if (!canNavigate) return;

            if (sheetNavigation)
            {
                _previewSheetPageIndex = Math.Clamp(_previewSheetPageIndex, 0, itemCount - 1);
                PreviewPositionText.Text = "A4 sheet "
                    + (_previewSheetPageIndex + 1).ToString("N0", System.Globalization.CultureInfo.CurrentCulture)
                    + " of " + itemCount.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
                PreviousPreviewButton.IsEnabled = _previewSheetPageIndex > 0;
                NextPreviewButton.IsEnabled = _previewSheetPageIndex < itemCount - 1;
            }
            else
            {
                PreviewPositionText.Text = _viewModel.SourceRowPositionText;
                PreviousPreviewButton.IsEnabled = _viewModel.CanSelectPreviousSourceRow;
                NextPreviewButton.IsEnabled = _viewModel.CanSelectNextSourceRow;
            }
        }

        private string SamplePreviewBadgeText()
        {
            string kind = _viewModel.TemplateKind switch
            {
                StickerTemplateKind.Sgu => "SGU",
                StickerTemplateKind.Dgu => "DGU",
                StickerTemplateKind.ProformaInvoice => "PROFORMA / JOB",
                _ => "CUSTOM"
            };
            return "SAMPLE · " + kind;
        }

        private Window? FindOwnerWindow() => Window.GetWindow(this);

        private UIElement CreateA4SheetPreview()
        {
            _previewSheetPageCount = 0;
            if (!_viewModel.TryCreatePreferences(out var preferences, out string preferenceError,
                    requirePrintableRows: !_designOnly))
                return CreatePreviewMessage(preferenceError);

            if (!StickerSheetLayoutPlanner.TryCreateNominalA4Plan(
                    _viewModel.ActiveTemplate.LabelWidthMm,
                    _viewModel.ActiveTemplate.LabelHeightMm,
                    preferences.SheetMarginMm,
                    preferences.SheetGapMm,
                    out var plan,
                    out string planError))
                return CreatePreviewMessage(planError);

            var rows = _viewModel.Items.ToList();
            if (rows.Count == 0 && _viewModel.SelectedLabel is { } sample)
            {
                var sampleRow = StickerLabelSequence.CopyWithPrintedQuantity(sample, 1);
                sampleRow.Quantity = 1;
                rows.Add(sampleRow);
                preferences.OneLabelPerPiece = true;
            }

            var paginator = new StickerSheetDocumentPaginator(
                rows, preferences, _viewModel.ActiveTemplate, plan);
            _previewSheetPageCount = paginator.PageCountLong;
            if (_previewSheetPageCount <= 0)
                return CreatePreviewMessage("No valid source labels are available to preview on the A4 sheet.");

            _previewSheetPageIndex = Math.Clamp(_previewSheetPageIndex, 0, _previewSheetPageCount - 1);
            var page = paginator.GetPage(checked((int)_previewSheetPageIndex)).Visual as UIElement;
            if (page == null)
                return CreatePreviewMessage("The A4 sheet preview could not be rendered.");
            return new Viewbox
            {
                Stretch = Stretch.Uniform,
                Child = page,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
        }

        private Brush ResolveStudioBrush(string resourceKey, Brush fallback)
            => TryFindResource(resourceKey) as Brush ?? fallback;

        private void ApplyEditorOutline(Border border, bool selected)
        {
            string resourceKey = selected ? "Ov.Accent" : "Ov.Line";
            if (TryFindResource(resourceKey) is Brush)
                border.SetResourceReference(Border.BorderBrushProperty, resourceKey);
            else
                border.BorderBrush = selected ? SystemColors.HighlightBrush : SystemColors.ControlDarkBrush;
            border.BorderThickness = new Thickness(selected ? 1.5 : 0.6);
        }

        private TextBlock CreatePreviewMessage(string message) => new()
        {
            Text = message,
            Foreground = ResolveStudioBrush("Ov.Muted", SystemColors.GrayTextBrush),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20)
        };

        private void RefreshPreview()
        {
            if (_viewModel == null) return;
            UpdatePreviewHeading();
            UpdateSampleSizeEditorVisibility();
            PreviewCountBadge.Text = _designOnly
                ? SamplePreviewBadgeText()
                : _viewModel.EstimatedPrintPlanSummaryText;

            bool canEditLayout = _viewModel.SelectedStudioTabIndex == 0;
            bool restoreCanvasFocus = canEditLayout && _editorCanvas?.IsKeyboardFocusWithin == true;
            if (_showSheetPreview && _viewModel.IsA4SheetLayout)
            {
                RemoveResizeThumbs();
                _editorCanvas = null;
                PreviewExplanation.Text = _designOnly
                    ? "A4 preview of one placeholder sample. No production data is used."
                    : "Estimated A4 preview at the designed sticker size. Final sheet capacity uses the selected printer's imageable area.";
                PreviewHost.Content = CreateA4SheetPreview();
                UpdatePreviewNavigation();
                return;
            }

            UpdatePreviewNavigation();
            PreviewExplanation.Text = _designOnly
                ? canEditLayout
                    ? "Sample values only. Select the glass-size field to edit preview dimensions. Print sample sends one test label and never uses PI/job data."
                    : "Test-only sample preview. Switch to Design to edit the layout or sample dimensions; production data is never used here."
                : canEditLayout
                    ? "Select, drag, or resize an object. Arrow keys move it by 1 mm; Shift + arrows move it by 5 mm."
                    : "Read-only preview. Switch to Design to edit the layout, or open Source rows to inspect production values.";

            var label = _viewModel.SelectedLabel;
            if (label == null)
            {
                RemoveResizeThumbs();
                _editorCanvas = null;
                PreviewHost.Content = new TextBlock
                {
                    Text = "No valid source row is selected. Select a row to inspect its source values.",
                    Foreground = ResolveStudioBrush("Ov.Muted", SystemColors.GrayTextBrush),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(16)
                };
                return;
            }

            RemoveResizeThumbs();
            int quantity = _viewModel.OneLabelPerPiece ? 1 : label.Quantity;
            Brush selectionBrush = ResolveStudioBrush("Ov.Accent", SystemColors.HighlightBrush);
            Brush outlineBrush = ResolveStudioBrush("Ov.Line", SystemColors.ControlDarkBrush);
            var canvas = StickerLabelVisualFactory.CreateTemplateCanvas(
                label,
                _viewModel.ActiveTemplate,
                quantity,
                editorMode: canEditLayout,
                selectedElementId: canEditLayout ? _viewModel.SelectedElement?.Id : null,
                editorSelectionBrush: selectionBrush,
                editorOutlineBrush: outlineBrush);
            _editorCanvas = canEditLayout ? canvas : null;
            if (canEditLayout)
            {
                canvas.PreviewKeyDown += EditorCanvas_PreviewKeyDown;
                canvas.MouseLeftButtonDown += EditorCanvas_MouseLeftButtonDown;
                foreach (var border in canvas.Children.OfType<Border>())
                {
                    border.MouseLeftButtonDown += EditorElement_MouseLeftButtonDown;
                    border.MouseMove += EditorElement_MouseMove;
                    border.MouseLeftButtonUp += EditorElement_MouseLeftButtonUp;
                    border.LostMouseCapture += EditorElement_LostMouseCapture;
                    bool selected = border.Tag is StickerLayoutElement element
                        && ReferenceEquals(element, _viewModel.SelectedElement);
                    ApplyEditorOutline(border, selected);
                }
            }

            PreviewHost.Content = new Viewbox
            {
                Stretch = Stretch.Uniform,
                Child = canvas,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            if (canEditLayout) AddResizeThumbs();
            if (restoreCanvasFocus)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    if (ReferenceEquals(_editorCanvas, canvas) && IsLoaded)
                        canvas.Focus();
                }));
            }
        }

        private void UpdateSampleSizeEditorVisibility()
        {
            if (_viewModel == null) return;
            bool editableSample = _viewModel.SelectedStudioTabIndex == 0
                && _viewModel.CanEditSampleDimensions && !_showSheetPreview;
            SampleSizeEditorPanel.Visibility = editableSample ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateSelectionOutlines()
        {
            if (_viewModel == null) return;
            bool designTabActive = _viewModel.SelectedStudioTabIndex == 0;
            SelectedElementCard.Visibility = designTabActive && _viewModel.HasSelectedElement
                ? Visibility.Visible : Visibility.Collapsed;
            UpdateSampleSizeEditorVisibility();
            if (!designTabActive || _editorCanvas == null) return;

            RemoveResizeThumbs();
            foreach (var border in _editorCanvas.Children.OfType<Border>())
            {
                bool selected = border.Tag is StickerLayoutElement element
                    && ReferenceEquals(element, _viewModel.SelectedElement);
                ApplyEditorOutline(border, selected);
                if (border.Tag is StickerLayoutElement item)
                    border.Cursor = item.IsLocked ? Cursors.Arrow : Cursors.SizeAll;
            }
            AddResizeThumbs();
        }

        private void RemoveResizeThumbs()
        {
            if (_editorCanvas != null)
                foreach (var thumb in _resizeThumbs)
                    _editorCanvas.Children.Remove(thumb);
            _resizeThumbs.Clear();
        }

        private void AddResizeThumbs()
        {
            if (_editorCanvas == null || _viewModel.SelectedElement is not { IsLocked: false } element)
                return;
            if (Math.Abs(element.RotationDegrees) > 0.001) return;

            var target = _editorCanvas.Children.OfType<Border>()
                .FirstOrDefault(border => ReferenceEquals(border.Tag, element));
            if (target == null) return;

            bool compactWidth = target.Width < 28;
            bool compactHeight = target.Height < 28;
            foreach (var definition in ResizeHandleDefinitions)
            {
                var edges = definition.Edges;
                if (compactWidth && ((edges & StickerResizeEdges.Right) != 0
                        || edges == StickerResizeEdges.Top || edges == StickerResizeEdges.Bottom))
                    continue;
                if (compactHeight && ((edges & StickerResizeEdges.Bottom) != 0
                        || edges == StickerResizeEdges.Left || edges == StickerResizeEdges.Right))
                    continue;

                var thumb = new Thumb
                {
                    Style = (Style)FindResource("StickerResizeThumbStyle"),
                    Width = definition.Width,
                    Height = definition.Height,
                    Cursor = definition.Cursor,
                    Tag = definition.Edges,
                    Focusable = false,
                    IsTabStop = false,
                    ToolTip = "Drag this handle to resize the selected object"
                };
                thumb.DragStarted += ResizeThumb_DragStarted;
                thumb.DragDelta += ResizeThumb_DragDelta;
                thumb.DragCompleted += ResizeThumb_DragCompleted;
                _resizeThumbs.Add(thumb);
                _editorCanvas.Children.Add(thumb);
                Canvas.SetZIndex(thumb, int.MaxValue);
            }
            PositionResizeThumbs(target);
        }

        private void PositionResizeThumbs(Border target)
        {
            if (_editorCanvas == null) return;
            double left = Canvas.GetLeft(target);
            double top = Canvas.GetTop(target);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            double width = target.Width;
            double height = target.Height;

            foreach (var thumb in _resizeThumbs)
            {
                var edges = (StickerResizeEdges)thumb.Tag;
                double x = (edges & StickerResizeEdges.Left) != 0
                    ? left - thumb.Width / 2
                    : (edges & StickerResizeEdges.Right) != 0
                        ? left + width - thumb.Width / 2
                        : left + (width - thumb.Width) / 2;
                double y = (edges & StickerResizeEdges.Top) != 0
                    ? top - thumb.Height / 2
                    : (edges & StickerResizeEdges.Bottom) != 0
                        ? top + height - thumb.Height / 2
                        : top + (height - thumb.Height) / 2;
                x = Math.Clamp(x, 0, Math.Max(0, _editorCanvas.Width - thumb.Width));
                y = Math.Clamp(y, 0, Math.Max(0, _editorCanvas.Height - thumb.Height));
                Canvas.SetLeft(thumb, x);
                Canvas.SetTop(thumb, y);
            }
        }

        private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is not Thumb thumb || _editorCanvas == null
                || thumb.Tag is not StickerResizeEdges edges
                || _viewModel.SelectedElement is not { IsLocked: false } element)
            {
                e.Handled = true;
                return;
            }

            var target = _editorCanvas.Children.OfType<Border>()
                .FirstOrDefault(border => ReferenceEquals(border.Tag, element));
            if (target == null)
            {
                e.Handled = true;
                return;
            }

            _resizeElement = element;
            _resizeStartGeometry = element.DeepClone();
            _resizeEdges = edges;
            _resizeTarget = target;
            _resizeStartPoint = Mouse.GetPosition(_editorCanvas);
            _isResizing = true;
            e.Handled = true;
        }

        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_editorCanvas == null || _resizeTarget == null || _resizeStartGeometry == null)
                return;

            Point current = Mouse.GetPosition(_editorCanvas);
            var previewGeometry = _resizeStartGeometry.DeepClone();
            double dxMm = StickerLabelVisualFactory.DipToMm(current.X - _resizeStartPoint.X);
            double dyMm = StickerLabelVisualFactory.DipToMm(current.Y - _resizeStartPoint.Y);
            if (!StickerElementResize.TryApply(previewGeometry, _viewModel.ActiveTemplate,
                    _resizeEdges, dxMm, dyMm))
                return;

            _resizeTarget.Width = StickerLabelVisualFactory.MmToDip(previewGeometry.WidthMm);
            _resizeTarget.Height = StickerLabelVisualFactory.MmToDip(previewGeometry.HeightMm);
            Canvas.SetLeft(_resizeTarget, StickerLabelVisualFactory.MmToDip(previewGeometry.Xmm));
            Canvas.SetTop(_resizeTarget, StickerLabelVisualFactory.MmToDip(previewGeometry.Ymm));
            PositionResizeThumbs(_resizeTarget);
            e.Handled = true;
        }

        private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            try
            {
                if (!e.Canceled && _editorCanvas != null && _resizeElement != null
                    && _resizeStartGeometry != null)
                {
                    Point current = Mouse.GetPosition(_editorCanvas);
                    double dxMm = StickerLabelVisualFactory.DipToMm(current.X - _resizeStartPoint.X);
                    double dyMm = StickerLabelVisualFactory.DipToMm(current.Y - _resizeStartPoint.Y);
                    StickerElementResize.TryApply(_resizeElement, _viewModel.ActiveTemplate,
                        _resizeEdges, dxMm, dyMm);
                }
            }
            finally
            {
                _isResizing = false;
                _resizeElement = null;
                _resizeStartGeometry = null;
                _resizeTarget = null;
                _resizeEdges = StickerResizeEdges.None;
                RefreshPreview();
            }
            e.Handled = true;
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
            if (!border.CaptureMouse())
            {
                _dragElement = null;
                _dragBorder = null;
            }
            e.Handled = true;
        }

        private void CancelElementDrag()
        {
            if (_dragBorder is not { } border) return;
            _dragBorder = null;
            _dragElement = null;
            if (border.IsMouseCaptured) border.ReleaseMouseCapture();
        }

        private void EditorElement_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_dragBorder == null || !ReferenceEquals(sender, _dragBorder)) return;
            _dragBorder = null;
            _dragElement = null;
            if (IsLoaded) RefreshPreview();
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
            PositionResizeThumbs(_dragBorder);
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

        private void StickerPrinterView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel == null) return;
            if (e.Key == Key.Escape && _dragBorder != null)
            {
                CancelElementDrag();
                RefreshPreview();
                e.Handled = true;
                return;
            }
            if (e.Key != Key.Delete || _viewModel.SelectedStudioTabIndex != 0) return;
            bool canvasFocused = _editorCanvas?.IsKeyboardFocusWithin == true;
            bool layerListFocused = TemplateElementsList.IsKeyboardFocusWithin;
            if (!canvasFocused && !layerListFocused) return;

            _viewModel.DeleteSelectedElement();
            if (canvasFocused) _editorCanvas?.Focus();
            else TemplateElementsList.Focus();
            e.Handled = true;
        }

        private void EditorCanvas_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel == null || _viewModel.SelectedStudioTabIndex != 0) return;
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 5 : 1;
            switch (e.Key)
            {
                case Key.Delete:
                    _viewModel.DeleteSelectedElement();
                    // Deleting rebuilds the canvas through the editor revision
                    // event. Restore focus so repeated Delete presses keep working.
                    _editorCanvas?.Focus();
                    e.Handled = true;
                    return;
                case Key.Left: _viewModel.NudgeSelectedElement(-step, 0); break;
                case Key.Right: _viewModel.NudgeSelectedElement(step, 0); break;
                case Key.Up: _viewModel.NudgeSelectedElement(0, -step); break;
                case Key.Down: _viewModel.NudgeSelectedElement(0, step); break;
                default: return;
            }
            // Nudge notifications may redraw the view. Keep keyboard editing
            // active after every arrow key rather than requiring a new click.
            _editorCanvas?.Focus();
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
            if (dialog.ShowDialog(FindOwnerWindow()) != true || !ConfirmTemplateChange()) return;
            if (!StickerTemplateStore.TryLoadFile(dialog.FileName, out var template, out string error)
                || template == null)
            {
                MessageBox.Show(FindOwnerWindow(), error, "Could not open template",
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
                MessageBox.Show(FindOwnerWindow(), error, "Could not save template",
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
            if (dialog.ShowDialog(FindOwnerWindow()) != true) return;
            if (!_viewModel.TryPrepareTemplateForSave(out string error))
            {
                _viewModel.StatusMessage = error;
                MessageBox.Show(FindOwnerWindow(), error, "Could not export template",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!StickerTemplateStore.TrySaveFile(dialog.FileName, _viewModel.ActiveTemplate, out error))
            {
                MessageBox.Show(FindOwnerWindow(), error, "Could not export template",
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
                MessageBox.Show(FindOwnerWindow(), saveError, "Could not save printer setup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (_designOnly)
            {
                PrintSample_Click(sender, e);
                return;
            }
            if (!_viewModel.TryCreatePreferences(out var preferences, out string error))
            {
                _viewModel.StatusMessage = error;
                MessageBox.Show(FindOwnerWindow(), error, "Check sticker setup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            long count = _viewModel.EstimatedLabelCount;
            if (count > 500 && !_viewModel.IsA4SheetLayout)
            {
                var confirm = MessageBox.Show(FindOwnerWindow(),
                    "This print run is estimated as " + _viewModel.EstimatedPrintPlanSummaryText
                    + ". The exact A4 sheet count and orientation will be confirmed after selecting the printer. Continue?",
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
                _viewModel.ActiveTemplate, _documentName, FindOwnerWindow());
            _viewModel.StatusMessage = result.Message;
            if (!result.Succeeded && !result.Cancelled)
            {
                MessageBox.Show(FindOwnerWindow(), result.Message, "Sticker print failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            if (!string.IsNullOrWhiteSpace(saveError))
                _viewModel.StatusMessage = result.Message + " Setup could not be saved: " + saveError;
        }

        private void PrintSample_Click(object sender, RoutedEventArgs e)
        {
            if (!_designOnly || _viewModel.Items.Count > 0)
            {
                _viewModel.StatusMessage = "Sample printing is available only in the standalone, no-source design studio.";
                return;
            }
            if (!string.IsNullOrWhiteSpace(_viewModel.SampleDimensionsWarningText))
            {
                string dimensionError = "Correct the sample glass dimensions before printing. "
                    + _viewModel.SampleDimensionsWarningText;
                _viewModel.StatusMessage = dimensionError;
                MessageBox.Show(FindOwnerWindow(), dimensionError, "Check sample dimensions",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_viewModel.SelectedLabel is not { } sample
                || !StickerLabelSequence.IsPrintable(sample))
            {
                _viewModel.StatusMessage = "The selected template has no valid sample label to print.";
                return;
            }
            if (!_viewModel.TryCreatePreferences(out var preferences, out string validationError,
                    requirePrintableRows: false))
            {
                _viewModel.StatusMessage = validationError;
                MessageBox.Show(FindOwnerWindow(), validationError, "Check sticker setup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string confirmation = "This sends one standalone test-print job using the synthetic SAMPLE data shown in the preview. "
                + "The active template controls the label content; no PI, job, customer, Step 5 or Optimization rows are read.\n\n"
                + (preferences.OutputLayoutMode == StickerOutputLayoutMode.OneLabelPerPage
                    || (preferences.OutputLayoutMode == StickerOutputLayoutMode.PrinterDefault
                        && preferences.PrinterModel == StickerPrinterModel.ZebraZt411)
                    ? "One-label-per-page mode will send one custom-size label page."
                    : "A4 sheet mode will send one sheet with the sample in the first position.")
                + "\n\nConfirm that test stock is loaded and continue?";
            if (MessageBox.Show(FindOwnerWindow(), confirmation, "Print test sticker",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
                != MessageBoxResult.Yes)
            {
                _viewModel.StatusMessage = "Sample printing cancelled. No production records were used.";
                return;
            }

            string saveError = string.Empty;
            if (StickerPrintPreferencesStore.TrySave(preferences, out saveError))
            {
                _viewModel.MarkTemplatePersisted();
                _viewModel.StatusMessage = "Sending one test print with synthetic sample values…";
            }
            else
                _viewModel.StatusMessage = saveError;

            var sampleRow = StickerLabelSequence.CopyWithPrintedQuantity(sample, 1);
            sampleRow.Quantity = 1;
            sampleRow.PrintedQuantity = 1;
            var result = StickerPrintEngine.Print(
                new[] { sampleRow },
                preferences,
                _viewModel.ActiveTemplate,
                _documentName + " — SAMPLE TEST ONLY",
                FindOwnerWindow());
            _viewModel.StatusMessage = result.Message;
            if (!result.Succeeded && !result.Cancelled)
            {
                MessageBox.Show(FindOwnerWindow(), result.Message, "Sample sticker print failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            if (!string.IsNullOrWhiteSpace(saveError))
                _viewModel.StatusMessage = result.Message + " Setup could not be saved: " + saveError;
        }

        private static string SafeSuggestedFileName(string? value)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "Custom sticker" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(name) ? "Custom sticker" : name;
        }

        internal bool ConfirmTemplateChange()
        {
            if (!_viewModel.IsTemplateDirty) return true;
            var answer = MessageBox.Show(FindOwnerWindow(),
                "The current template has unsaved edits. Save it to the local template library before switching?\n\nYes = save and continue; No = discard edits; Cancel = stay here.",
                "Unsaved sticker template", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (answer == MessageBoxResult.Cancel) return false;
            if (answer == MessageBoxResult.No) return true;

            if (!_viewModel.TrySaveActiveTemplateToLibrary(out string path, out string error))
            {
                MessageBox.Show(FindOwnerWindow(), error, "Could not save current template",
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
                MessageBox.Show(FindOwnerWindow(),
                    "The template is available in the local library, but Step 5's last-used layout could not be updated." +
                    Environment.NewLine + Environment.NewLine + saveError,
                    "Could not save default sticker layout", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ApplyZt411FourBySix_Click(object sender, RoutedEventArgs e)
            => _viewModel.ApplyZt411FourBySixStock();

        private void AddCommonField_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { CommandParameter: string fieldKey }) return;
            _viewModel.NewFieldKey = fieldKey;
            _viewModel.AddFieldElement();
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
        private void Close_Click(object sender, RoutedEventArgs e) => FindOwnerWindow()?.Close();
    }
}
