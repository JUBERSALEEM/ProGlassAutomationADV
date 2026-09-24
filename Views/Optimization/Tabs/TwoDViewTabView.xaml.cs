using System.Windows.Controls;
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

        /// <summary>Scale the whole plate into the stage so every cut is
        /// visible. Zoom turns fit off and the scroller takes over.</summary>
        private void ApplySheetFit()
        {
            if (SheetFitScale == null || SheetStage == null) return;
            var vm = DataContext as OptimizationViewModel;
            if (vm == null || !vm.IsFitView || vm.RenderWidth <= 1 || vm.RenderHeight <= 1
                || SheetStage.ActualWidth <= 1 || SheetStage.ActualHeight <= 1)
            {
                SheetFitScale.ScaleX = 1;
                SheetFitScale.ScaleY = 1;
                return;
            }

            double sx = (SheetStage.ActualWidth - 28) / vm.RenderWidth;
            double sy = (SheetStage.ActualHeight - 36) / vm.RenderHeight;
            double fit = System.Math.Min(sx, sy);
            if (fit <= 0 || double.IsNaN(fit) || double.IsInfinity(fit)) fit = 1;
            SheetFitScale.ScaleX = fit;
            SheetFitScale.ScaleY = fit;
        }
    }
}
