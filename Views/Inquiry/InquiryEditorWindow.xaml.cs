using Microsoft.Win32;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.ViewModels.Inquiry;
using System;
using ProGlassAutomation.Utilities;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Inquiry
{
    /// <summary>
    /// Inquiry workspace window. UI plumbing only:
    /// - Ctrl+S saves, Esc closes (with the VM's discard confirmation);
    /// - drag-and-drop attachment intake (A-drop zone);
    /// - file dialog + confirm callbacks;
    /// - attachment preview;
    /// - numeric input guard for the Value field (decimal only).
    ///
    /// A6 fix: the preview uses the ctor-injected InquiryAttachmentService —
    /// never the service locator (App.ServiceProvider may be null when the
    /// DI composition fell back to manual construction at startup).
    /// </summary>
    public partial class InquiryEditorWindow : Window
    {
        private readonly InquiryAttachmentService _attachmentService;
        private readonly InquiryEditorViewModel _viewModel;

        public InquiryEditorWindow(IInquiryWorkflowService workflow,
            InquiryAttachmentService attachments, InquiryRecord? record = null,
            ProGlassAutomation.Services.MasterData.Repository.IMasterDataRepository? masterData = null)
        {
            InitializeComponent();
            _attachmentService = attachments ?? throw new ArgumentNullException(nameof(attachments));
            // Master data is optional. The caller may inject a test fake
            // via the `masterData` parameter, otherwise we use the App.*
            // static singleton that's already initialised at app startup.
            // We resolve it here ONLY when the caller didn't supply one
            // — the caller may have a service-locator-aware instance
            // (e.g. resolved through App.ServiceProvider in the list view).
            if (masterData == null)
                masterData = App.ServiceProvider?.GetService(typeof(
                    ProGlassAutomation.Services.MasterData.Repository.IMasterDataRepository))
                    as ProGlassAutomation.Services.MasterData.Repository.IMasterDataRepository;
            var vm = new InquiryEditorViewModel(workflow, attachments, record, masterData);
            _viewModel = vm;
            // Subscribe to the VM's CloseRequested event. We hold the
            // handler in a field so the window's Closed handler can
            // unsubscribe it — otherwise the VM would keep a strong
            // reference to this window (and its captured `this`) past
            // the lifetime of the window. Classic event-subscription
            // leak that turns a 1KB editor into a multi-MB hold for
            // every inquiry the user opens during a long session.
            _closeRequestedHandler = saved => { DialogResult = saved; };
            vm.CloseRequested += _closeRequestedHandler;
            vm.SelectFilesRequested = SelectFiles;
            vm.ConfirmDiscardRequested = () => MessageBox.Show(
                "Discard unsaved changes to this inquiry?", "Unsaved changes",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
            vm.ConfirmDeleteRequested = file => MessageBox.Show(
                $"Delete attachment '{file.FileName}'? The file is removed from disk as well.",
                "Delete attachment",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
            vm.PreviewRequested = Preview;
            DataContext = vm;
            // Put the caret where the story starts. Cached as a field so
            // the Closed handler can unsubscribe it. The null check on
            // CustomerInput is defensive — the XAML reference always
            // resolves at runtime, but a malformed XAML or a design-time
            // host can leave the named element uninitialised, and the
            // editor should not crash on those paths.
            _focusHandler = (_, __) =>
            {
                if (CustomerInput is null) return;
                CustomerInput.Focus();
                Keyboard.Focus(CustomerInput);
            };
            Loaded += _focusHandler;
            // Closed runs after the window is hidden; this is the
            // right place to drop all VM/handler references so the VM
            // can be collected as soon as the user moves on.
            Closed += OnEditorClosed;
        }

        private readonly Action<bool> _closeRequestedHandler;
        private readonly RoutedEventHandler _focusHandler;

        private void OnEditorClosed(object? sender, EventArgs e)
        {
            // Drop every callback we own so the VM doesn't keep the
            // window (or its captured state) alive past its lifetime.
            if (_viewModel != null)
            {
                _viewModel.CloseRequested -= _closeRequestedHandler;
                // Tell the VM to stop the fire-and-forget loaders.
                // Without this, the loaders' captured `this` keeps the
                // VM alive for the lifetime of the app.
                _viewModel.CancelLoaders();
            }
            Loaded -= _focusHandler;
            Closed -= OnEditorClosed;
        }

        private static string[]? SelectFiles()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Add inquiry attachments", Multiselect = true,
                Filter = "Supported files|*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.dwg;*.dxf;*.xlsx;*.xls;*.csv|PDF|*.pdf|Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|All files|*.*"
            };
            return dialog.ShowDialog() == true ? dialog.FileNames.ToArray() : null;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                if (_viewModel.SaveCommand.CanExecute(null)) _viewModel.SaveCommand.Execute(null);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                _viewModel.CancelCommand.Execute(null);
            }
        }

        /// <summary>
        /// Block non-numeric keys in the inquiry-value field. We allow digits,
        /// the current-culture decimal separator, the current-culture group
        /// separator, and a single leading minus sign. Everything else is
        /// dropped before it can mutate the VM's ValueText.
        /// </summary>
        private void OnValuePreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox tb) { e.Handled = true; return; }
            char dec = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
            char grp = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator[0];
            foreach (char c in e.Text)
            {
                bool ok = char.IsDigit(c) || c == dec || c == grp || c == ' ' || c == '\t';
                if (!ok) { e.Handled = true; return; }
            }
            // Build the resulting string the user is about to type into and
            // see if it parses as a number. The cheapest correct check.
            string proposed = tb.Text.Remove(tb.SelectionStart, tb.SelectionLength)
                                   .Insert(tb.SelectionStart, e.Text);
            if (string.IsNullOrEmpty(proposed) || proposed == "-") return;
            if (!decimal.TryParse(proposed, NumberStyles.Number, CultureInfo.CurrentCulture, out _))
                e.Handled = true;
        }

        /// <summary>
        /// Mirror of the preview-text-input guard for pasted content. Pasting
        /// bypasses PreviewTextInput, so we strip non-numeric characters from
        /// the clipboard text before letting the paste through.
        ///
        /// WPF quirk: <c>DataObjectPastingEventArgs.SourceDataObject</c> is
        /// read-only, so we can't replace it. The settable <c>DataObject</c>
        /// is the same instance as SourceDataObject and we mutate it in place
        /// via <see cref="DataObject.SetData(string, object)"/> — WPF will
        /// then read the sanitized text when it commits the paste.
        /// </summary>
        private void OnValuePaste(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox tb) { e.CancelCommand(); return; }
            try
            {
                // Read the raw text from the read-only SourceDataObject.
                // GetDataPresent is checked first to avoid the (rare)
                // NRE when GetData returns null and we cast to string.
                if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
                { e.CancelCommand(); return; }
                object? raw = e.SourceDataObject.GetData(DataFormats.UnicodeText);
                if (raw is not string pasted)
                {
                    // Non-string clipboard content (e.g. a file drop) —
                    // we can't validate it as a decimal, so cancel.
                    e.CancelCommand();
                    return;
                }
                char dec = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
                char grp = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator[0];
                var sb = new System.Text.StringBuilder(pasted.Length);
                foreach (char c in pasted)
                    if (char.IsDigit(c) || c == dec || c == grp || c == ' ' || c == '\t')
                        sb.Append(c);
                string clean = sb.ToString();
                if (clean.Length == 0) { e.CancelCommand(); return; }
                // Reject the paste if the resulting text would not parse as a decimal
                // in the current culture. (We can also rewrite the text, but if it's
                // already numeric, no rewrite is needed — and rewriting can fight
                // other handlers in the bubbling chain.)
                string proposed = tb.Text.Remove(tb.SelectionStart, tb.SelectionLength)
                                       .Insert(tb.SelectionStart, clean);
                if (!decimal.TryParse(proposed, NumberStyles.Number, CultureInfo.CurrentCulture, out _))
                {
                    e.CancelCommand();
                    return;
                }
                // Mutate the (writable) DataObject in place so what actually lands
                // in the TextBox is the sanitized string. SourceDataObject returns
                // the same IDataObject instance as DataObject in WPF, but only
                // DataObject is the concrete class exposing SetData.
                e.DataObject.SetData(DataFormats.UnicodeText, clean);
            }
            catch (Exception ex)
            {
                // Defensive: never crash the editor on a paste. Cancel
                // the operation and log so the user can paste plain text
                // into a different field instead.
                e.CancelCommand();
                System.Diagnostics.Debug.WriteLine($"OnValuePaste failed: {ex}");
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            bool files = e.Data.GetDataPresent(DataFormats.FileDrop);
            e.Effects = files ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void OnFileDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;
            e.Handled = true;
            try
            {
                await _viewModel.AddDroppedFilesAsync(paths);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not attach the dropped files: " + ExceptionMessages.Deep(ex),
                    "Attachment", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Preview(InquiryAttachment attachment) =>
            new InquiryAttachmentPreviewWindow(attachment, _attachmentService) { Owner = this }.ShowDialog();
    }
}
