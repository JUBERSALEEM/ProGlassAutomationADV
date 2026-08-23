using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.ViewModels.Inquiry;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace ProGlassAutomation.Views.Inquiry
{
    /// <summary>
    /// Code-behind for the Inquiry register (clean list + slide-in rail).
    /// UI plumbing only:
    ///   - wires the editor/export/import/preview dialogs onto the ViewModel
    ///   - single-click toggles the slide-in rail (same as SheetStore pattern)
    ///   - double-click opens the selected inquiry in the workspace
    /// All business logic lives in InquiryListViewModel.
    /// </summary>
    public partial class InquiryListView : UserControl
    {
        /// <summary>Width of the slide-in rail, matching SheetStoreView's panel.</summary>
        private const double RailWidth = 376.0;

        public InquiryListView()
        {
            InitializeComponent();
            DataContextChanged += (_, __) => WireEditor();
            DataContextChanged += (_, __) => SubscribeSelection();
            Loaded += (_, __) => WireEditor();
            Loaded += (_, __) => SubscribeSelection();
        }

        private void SubscribeSelection()
        {
            if (DataContext is not InquiryListViewModel vm) return;
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (DataContext is not InquiryListViewModel vm) return;
            if (e.PropertyName == nameof(InquiryListViewModel.SelectedInquiry))
            {
                AnimateRail(vm.SelectedInquiry != null);
            }
            else if (e.PropertyName == nameof(InquiryListViewModel.IsDetailsDrawerOpen))
            {
                // The rail should also close when the user presses the
                // drawer-close button, which only flips IsDetailsDrawerOpen.
                if (!vm.IsDetailsDrawerOpen) AnimateRail(false);
            }
        }

        /// <summary>Double-click a row → open it in the workspace.</summary>
        private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is InquiryListViewModel vm
                && vm.SelectedInquiry != null
                && vm.EditInquiryCommand.CanExecute(vm.SelectedInquiry))
            {
                vm.EditInquiryCommand.Execute(vm.SelectedInquiry);
            }
        }

        /// <summary>
        /// Single-click a row → select it and slide the rail in. Clicking
        /// the same row again (or pressing the rail's close button) toggles
        /// the rail closed. Mirrors the SheetStore pattern.
        /// </summary>
        private void OnRowClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not InquiryListViewModel vm) return;
            if (sender is not FrameworkElement fe || fe.DataContext is not InquiryListRow row) return;

            if (vm.SelectedInquiry != null && ReferenceEquals(vm.SelectedInquiry, row))
            {
                // Toggle off — close the rail and clear the selection.
                vm.SelectedInquiry = null;
                return;
            }
            vm.SelectedInquiry = row;
        }

        /// <summary>
        /// Show or hide the slide-in rail with a smooth animation. The
        /// visibility flip happens once the column has expanded; we keep
        /// the panel in the visual tree while animating to avoid a layout
        /// snap.
        /// </summary>
        private void AnimateRail(bool open)
        {
            if (DetailPanel == null) return;

            if (open)
            {
                DetailPanel.Visibility = Visibility.Visible;
                DetailColumn.Width = new GridLength(RailWidth);
                var slide = new DoubleAnimation
                {
                    From = RailWidth,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                PanelSlide.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
            }
            else
            {
                var slide = new DoubleAnimation
                {
                    From = 0,
                    To = RailWidth,
                    Duration = TimeSpan.FromMilliseconds(180),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                slide.Completed += (_, __) =>
                {
                    DetailPanel.Visibility = Visibility.Collapsed;
                    DetailColumn.Width = new GridLength(0);
                    PanelSlide.X = 0;
                };
                PanelSlide.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
            }
        }

        /// <summary>Preview-an outside click on the empty list area → close rail.</summary>
        private void Root_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not InquiryListViewModel vm) return;
            // Only close if the click didn't land on a row or on the rail itself.
            var src = e.OriginalSource as DependencyObject;
            while (src != null)
            {
                if (src is ListBoxItem) return;     // row click — let it through
                if (src == DetailPanel) return;     // rail click — let it through
                src = System.Windows.Media.VisualTreeHelper.GetParent(src);
            }
            if (vm.SelectedInquiry != null)
            {
                vm.SelectedInquiry = null;
                AnimateRail(false);
            }
        }

        private void WireEditor()
        {
            if (DataContext is not InquiryListViewModel vm || vm.EditRequested != null) return;
            vm.EditRequested = async record =>
            {
                var workflow = App.ServiceProvider.GetRequiredService<IInquiryWorkflowService>();
                var attachments = App.ServiceProvider.GetRequiredService<InquiryAttachmentService>();
                var window = new InquiryEditorWindow(workflow, attachments, record)
                { Owner = Application.Current.MainWindow };
                return window.ShowDialog() == true;
            };
            vm.ExportPathRequested = () =>
            {
                var d = new SaveFileDialog
                {
                    Filter = "CSV file|*.csv",
                    FileName = $"InquiryRegister_{DateTime.Now:yyyyMMdd}.csv"
                };
                return d.ShowDialog() == true ? d.FileName : null;
            };
            vm.ImportPathRequested = () =>
            {
                var d = new OpenFileDialog { Filter = "CSV file|*.csv" };
                return d.ShowDialog() == true ? d.FileName : null;
            };
            vm.ConfirmImportRequested = count => MessageBox.Show(
                $"Create {count} validated inquiry records?", "Import inquiries",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
            vm.ConfirmDeleteAttachmentRequested = file => MessageBox.Show(
                $"Delete attachment '{file.FileName}'? The file is removed from disk as well.",
                "Delete attachment",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
            vm.PreviewAttachmentRequested = file =>
            {
                var service = App.ServiceProvider.GetService(typeof(InquiryAttachmentService))
                    as InquiryAttachmentService
                    ?? new InquiryAttachmentService(App.Inquiries ?? new InquiryRepository());
                var preview = new InquiryAttachmentPreviewWindow(file, service);
                if (Application.Current?.MainWindow is Window owner) preview.Owner = owner;
                preview.Show();
            };
        }
    }
}
