using System.Windows.Controls;
using System.Windows.Input;
using ProGlassAutomation.ViewModels.Optimization;

namespace ProGlassAutomation.Views.Optimization.Tabs
{
    public partial class TwoDViewTabView : UserControl
    {
        public TwoDViewTabView()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => HookPreview();
            Loaded += (_, _) => ApplySheetFit();
        }

        private OptimizationViewModel? _hooked;
        private bool _fitting;

        private void HookPreview()
        {
            if (_hooked != null)
                _hooked.PropertyChanged -= OnPreviewChanged;
            _hooked = DataContext as OptimizationViewModel;
            if (_hooked != null)
                _hooked.PropertyChanged += OnPreviewChanged;
            ApplySheetFit();
        }

        private void OnPreviewChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(OptimizationViewModel.RenderWidth)
                or nameof(OptimizationViewModel.RenderHeight)
                or nameof(OptimizationViewModel.IsFitView)
                or nameof(OptimizationViewModel.HasPreview))
                ApplySheetFit();
        }

        private void OnSheetStageSizeChanged(object sender, System.Windows.SizeChangedEventArgs e) => ApplySheetFit();

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not OptimizationViewModel vm || !vm.IsSingleSheetMode
                || Keyboard.Modifiers != ModifierKeys.None)
                return;

            System.Windows.Input.ICommand? command = e.Key switch
            {
                Key.Left => vm.SelectPreviousSheetCommand,
                Key.Right => vm.SelectNextSheetCommand,
                _ => null
            };
            if (command?.CanExecute(null) != true) return;
            command.Execute(null);
            e.Handled = true;
        }

        /// <summary>
        /// Fit the plate AND its dimension callouts into the stage.
        /// Scaling only RenderWidth left the left callout hanging over the
        /// schedule. Zoom turns fit off and the scroller takes over.
        /// </summary>
        private void ApplySheetFit()
        {
            if (_fitting || SheetFitScale == null || SheetStage == null) return;
            var vm = DataContext as OptimizationViewModel;
            if (vm == null || !vm.IsFitView || vm.RenderWidth <= 1 || vm.RenderHeight <= 1
                || SheetStage.ActualWidth <= 1 || SheetStage.ActualHeight <= 1)
            {
                SheetFitScale.ScaleX = 1;
                SheetFitScale.ScaleY = 1;
                return;
            }

            _fitting = true;
            try
            {
                SheetFitScale.ScaleX = 1;
                SheetFitScale.ScaleY = 1;
                if (SheetFitHost != null)
                    SheetFitHost.UpdateLayout();

                double contentW = SheetFitHost?.DesiredSize.Width ?? 0;
                double contentH = SheetFitHost?.DesiredSize.Height ?? 0;
                // Callouts sit outside the canvas. If measure has not run yet,
                // reserve that chrome so the plate cannot paint the schedule.
                if (contentW < 8) contentW = vm.RenderWidth + 48;
                if (contentH < 8) contentH = vm.RenderHeight + 40;

                double availW = System.Math.Max(1, SheetStage.ActualWidth - 16);
                double availH = System.Math.Max(1, SheetStage.ActualHeight - 16);
                double fit = System.Math.Min(availW / contentW, availH / contentH);
                if (fit <= 0 || double.IsNaN(fit) || double.IsInfinity(fit)) fit = 1;
                SheetFitScale.ScaleX = fit;
                SheetFitScale.ScaleY = fit;
            }
            finally
            {
                _fitting = false;
            }
        }
    }
}
