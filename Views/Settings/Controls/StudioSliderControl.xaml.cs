// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Controls/StudioSliderControl.xaml.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Code-behind for the Theme Studio slider row. Pure view logic: DP
// declarations + the IsRelevant dim-lock for glass-theatre sliders on
// flat (industrial) families. No business rules — the ViewModel owns
// every value; this control only DISPLAYS and FORWARDS them.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Settings.Controls
{
    public partial class StudioSliderControl : UserControl
    {
        public StudioSliderControl()
        {
            InitializeComponent();
            ApplyRelevance(IsRelevant);
        }

        /// <summary>Localized header text (the slider's name).</summary>
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(
                nameof(Header), typeof(string), typeof(StudioSliderControl),
                new PropertyMetadata(string.Empty));

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        /// <summary>Studio axis value 0–100, two-way by default so the
        /// Slider writes straight back into the bound VM property.</summary>
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value), typeof(double), typeof(StudioSliderControl),
                new FrameworkPropertyMetadata(
                    50.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>Optional tooltip (the three hint-carrying sliders).</summary>
        public static readonly DependencyProperty ToolTipTextProperty =
            DependencyProperty.Register(
                nameof(ToolTipText), typeof(string), typeof(StudioSliderControl),
                new PropertyMetadata(null));

        public string? ToolTipText
        {
            get => (string?)GetValue(ToolTipTextProperty);
            set => SetValue(ToolTipTextProperty, value);
        }

        /// <summary>False = the active visual family does not render this
        /// axis (glass theatre on an industrial flat theme). The slider
        /// is dimmed, locked and labelled so the operator is not invited
        /// to tune a regression they cannot see.</summary>
        public static readonly DependencyProperty IsRelevantProperty =
            DependencyProperty.Register(
                nameof(IsRelevant), typeof(bool), typeof(StudioSliderControl),
                new PropertyMetadata(true, OnRelevanceChanged));

        public bool IsRelevant
        {
            get => (bool)GetValue(IsRelevantProperty);
            set => SetValue(IsRelevantProperty, value);
        }

        /// <summary>Per-slider reset command (VM: ResetStudioSliderCommand).</summary>
        public static readonly DependencyProperty ResetCommandProperty =
            DependencyProperty.Register(
                nameof(ResetCommand), typeof(ICommand),
                typeof(StudioSliderControl), new PropertyMetadata(null));

        public ICommand? ResetCommand
        {
            get => (ICommand?)GetValue(ResetCommandProperty);
            set => SetValue(ResetCommandProperty, value);
        }

        /// <summary>Studio settings key the reset command resets
        /// (e.g. "Frostness" — the VM maps it to the family's default).</summary>
        public static readonly DependencyProperty SliderKeyProperty =
            DependencyProperty.Register(
                nameof(SliderKey), typeof(string), typeof(StudioSliderControl),
                new PropertyMetadata(string.Empty));

        public string SliderKey
        {
            get => (string)GetValue(SliderKeyProperty);
            set => SetValue(SliderKeyProperty, value);
        }

        private static void OnRelevanceChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StudioSliderControl control &&
                control.IsInitialized)
            {
                control.ApplyRelevance((bool)e.NewValue);
            }
        }

        private void ApplyRelevance(bool relevant)
        {
            PART_Slider.IsEnabled = relevant;
            ResetChip.IsEnabled = relevant;
            RootPanel.Opacity = relevant ? 1.0 : 0.45;
            GlassOnlyHint.Visibility =
                relevant ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
