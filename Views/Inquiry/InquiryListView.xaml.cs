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

        // Cached so we can unsubscribe exactly once on Unload. Avoids the
        // memory leak where a replaced DataContext VM still holds a delegate
        // pointing at this UserControl (and vice versa).
        private InquiryListViewModel? _subscribedVm;

        public InquiryListView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (_, __) => WireEditor();
            Unloaded += OnUnloaded;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from the outgoing VM BEFORE the new one is wired,
            // so we never hold a delegate into a VM that's about to be
            // collected. (C# event unsubscribers on a non-null reference
            // are idempotent and silent on no-op.)
            if (_subscribedVm != null)
            {
                _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
                _subscribedVm = null;
            }
            WireEditor();
            SubscribeSelection();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Final cleanup when the view leaves the visual tree (navigation,
            // window close, etc.). Without this, the VM keeps a strong
            // reference to this UserControl, and we keep the VM alive too —
            // classic UserControl lifetime leak.
            if (_subscribedVm != null)
            {
                _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
                _subscribedVm = null;
            }
        }

        private void SubscribeSelection()
        {
            if (DataContext is not InquiryListViewModel vm) return;
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            _subscribedVm = vm;
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
            // Cache the shell's NavigateFromAgent method via reflection so we
            // can wire it onto the editor's NavigateRequested callback every
            // time a new editor window is opened. Using reflection (instead
            // of dynamic) avoids the per-call DLR dispatch and keeps the
            // editor decoupled from MainViewModel's concrete type.
            var shellVm = Application.Current?.MainWindow?.DataContext;
            var shellNavigate = shellVm?.GetType().GetMethod(
                "NavigateFromAgent",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            void InvokeShellNavigate(string target)
            {
                if (shellNavigate == null || shellVm == null) return;
                try { shellNavigate.Invoke(shellVm, new object[] { target }); }
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[InquiryListView.xaml] WireEditor: shell method may not be available — never crash the editor: " + ex.Message); }
            }
            vm.EditRequested = async record =>
            {
                // Defensive: the list VM's ctor has a fallback for
                // App.Inquiries, but the editor's required services
                // (IInquiryWorkflowService, InquiryAttachmentService) have
                // no fallback — GetRequiredService throws when the DI
                // container never built. We resolve them via the
                // service provider when available, and fall back to
                // newing up the same instances the VM's ctor would
                // fall back to, so the editor still works in a
                // manually-constructed startup (the App.xaml.cs catch
                // branch).
                var workflow = App.ServiceProvider?.GetService(typeof(IInquiryWorkflowService))
                    as IInquiryWorkflowService
                    ?? new InquiryWorkflowService(App.Inquiries ?? new InquiryRepository());
                var attachments = App.ServiceProvider?.GetService(typeof(InquiryAttachmentService))
                    as InquiryAttachmentService
                    ?? new InquiryAttachmentService(App.Inquiries ?? new InquiryRepository());
                // Resolve master-data through DI when available, fall back
                // to the App.* static when the DI container never built
                // (manual startup). The VM already does this internally;
                // we just hand it the same instance to avoid a duplicate
                // connection.
                var masterData = App.ServiceProvider?.GetService(typeof(
                    ProGlassAutomation.Services.MasterData.Repository.IMasterDataRepository))
                    as ProGlassAutomation.Services.MasterData.Repository.IMasterDataRepository;
                var window = new InquiryEditorWindow(workflow, attachments, record, masterData)
                { Owner = Application.Current.MainWindow };
                // Wire the editor's module-navigation callback (used by the
                // right-click "Open in master data" / "View customer
                // history" menu items) to the shell's NavigateFromAgent.
                // Done AFTER the window's DataContext is set in the
                // constructor, so we read the VM from window.DataContext
                // (guaranteed non-null at this point).
                if (window.DataContext is InquiryEditorViewModel editorVm)
                {
                    editorVm.NavigateRequested = target =>
                    {
                        InvokeShellNavigate(target);
                        return Task.CompletedTask;
                    };
                }
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
            // Destructive: ask twice. The first message names the inquiry
            // and the action; the second message reminds the user that
            // this cannot be undone. The default button is No so a stray
            // Enter keypress can't drop the row.
            vm.ConfirmDeleteInquiryRequested = row => MessageBox.Show(
                $"Delete inquiry {row.InquiryNumber} ({row.CustomerName} / {row.Project})?\n\n" +
                "This permanently removes the inquiry AND its attachments " +
                "and communications. The action is logged and cannot be undone.\n\n" +
                "Tip: if the inquiry has a Proforma Invoice, Job Order, " +
                "Delivery Note, Tax Invoice or Statement, the delete will " +
                "be refused — use 'Change status → Cancel' instead.",
                "Delete inquiry",
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
