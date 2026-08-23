using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.ViewModels.Inquiry;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Inquiry
{
    /// <summary>
    /// Code-behind for the Inquiry register. UI-only concerns:
    /// - wires the editor/export/import dialogs onto the ViewModel callbacks;
    /// - keeps the fixed-width column header horizontally synchronized with
    ///   the virtualized rows ListBox (one shared scroll offset).
    /// No business logic — everything else lives in the ViewModel.
    /// </summary>
    public partial class InquiryListView : UserControl
    {
        private bool _syncingScroll;

        public InquiryListView()
        {
            InitializeComponent();
            DataContextChanged += (_, __) => WireEditor();
            Loaded += (_, __) =>
            {
                WireEditor();
                HookScrollSync();
            };
        }

        /// <summary>Double-click a register row → open it in the workspace.</summary>
        private void OnRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is InquiryListViewModel vm
                && vm.SelectedInquiry != null
                && vm.EditInquiryCommand.CanExecute(vm.SelectedInquiry))
            {
                vm.EditInquiryCommand.Execute(vm.SelectedInquiry);
            }
        }

        /// <summary>Two-way offset sync: header bar follows the rows scroller.</summary>
        private void HookScrollSync()
        {
            if (RegisterList == null || HeaderScroller == null) return;
            RegisterList.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnRowsScrolled));
            HeaderScroller.ScrollChanged += OnHeaderScrolled;
        }

        private void OnRowsScrolled(object sender, ScrollChangedEventArgs e)
        {
            if (_syncingScroll || HeaderScroller == null) return;
            _syncingScroll = true;
            try
            {
                if (Math.Abs(HeaderScroller.HorizontalOffset - e.HorizontalOffset) > 0.5)
                    HeaderScroller.ScrollToHorizontalOffset(e.HorizontalOffset);
            }
            finally { _syncingScroll = false; }
        }

        private void OnHeaderScrolled(object sender, ScrollChangedEventArgs e)
        {
            if (_syncingScroll || RegisterList == null) return;
            _syncingScroll = true;
            try
            {
                var inner = GetInnerScrollViewer(RegisterList);
                if (inner != null && Math.Abs(inner.HorizontalOffset - e.HorizontalOffset) > 0.5)
                    inner.ScrollToHorizontalOffset(e.HorizontalOffset);
            }
            finally { _syncingScroll = false; }
        }

        private static ScrollViewer? GetInnerScrollViewer(DependencyObject obj)
        {
            if (obj is ScrollViewer viewer) return viewer;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                var result = GetInnerScrollViewer(child);
                if (result != null) return result;
            }
            return null;
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
