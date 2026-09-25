// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Setup/ComponentInstallerWindow.xaml.cs
// One card, opened from Settings only. The main window does not show
// this. FFmpeg ships inside Setup.exe. Nothing starts until the user
// clicks Install.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Agent.Demo.Video;
using ProGlassAutomation.Agent.LocalAi;
using ProGlassAutomation.ViewModels.Setup;

namespace ProGlassAutomation.Views.Setup
{
    public partial class ComponentInstallerWindow : Window
    {
        private readonly ComponentInstallerViewModel _vm = new();

        public ComponentInstallerWindow(bool models = false)
        {
            InitializeComponent();
            DataContext = _vm;
            _vm.ApplyRecommendedLabel((string)FindResource("Setup.Recommended"));
            if (models)
                _vm.ShowModelCommand.Execute(null);
            _vm.PropertyChanged += OnVmChanged;
            Apply();
        }

        public static string SeenPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "component-installer.seen");

        public static bool ShouldOffer()
        {
            try
            {
                if (File.Exists(SeenPath)) return false;
                bool ffmpeg = FfmpegMp4Composer.Find() != null;
                bool model = LocalModelCatalog.All.Any(entry => LocalModelManager.IsInstalled(entry.Id, out _));
                return !ffmpeg || !model;
            }
            catch
            {
                return false;
            }
        }

        public static void Show(Window? owner, bool models = false)
        {
            foreach (Window open in Application.Current.Windows)
            {
                if (open is ComponentInstallerWindow existing)
                {
                    existing.Activate();
                    return;
                }
            }
            var window = new ComponentInstallerWindow(models);
            if (owner != null && owner.IsLoaded)
                window.Owner = owner;
            window.ShowDialog();
        }

        public static void MarkSeen()
        {
            try
            {
                string? dir = Path.GetDirectoryName(SeenPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(SeenPath, DateTime.UtcNow.ToString("o"));
            }
            catch
            {
                // Offering again is safer than blocking the shell.
            }
        }

        private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ComponentInstallerViewModel.Page)
                or nameof(ComponentInstallerViewModel.Status)
                or nameof(ComponentInstallerViewModel.StatusKey)
                or nameof(ComponentInstallerViewModel.Percent)
                or nameof(ComponentInstallerViewModel.FfmpegReady)
                or nameof(ComponentInstallerViewModel.IsBusy)
                or nameof(ComponentInstallerViewModel.IsModelBusy)
                or nameof(ComponentInstallerViewModel.IsComplete))
            {
                Apply();
            }
        }

        private string Line(string key, string fallback)
        {
            return TryFindResource(key) as string ?? fallback;
        }

        private string StatusText()
        {
            if (!string.IsNullOrEmpty(_vm.StatusKey))
                return Line(_vm.StatusKey, _vm.Status);
            return _vm.Status;
        }

        private void Apply()
        {
            FfmpegPage.Visibility = _vm.Page == 0 ? Visibility.Visible : Visibility.Collapsed;
            ModelPage.Visibility = _vm.Page == 1 ? Visibility.Visible : Visibility.Collapsed;
            ProgressPage.Visibility = _vm.Page == 2 ? Visibility.Visible : Visibility.Collapsed;
            CancelButton.Visibility = _vm.Page == 2 ? Visibility.Visible : Visibility.Collapsed;
            BackButton.Visibility = _vm.Page == 1 ? Visibility.Visible : Visibility.Collapsed;
            SkipButton.Visibility = _vm.Page == 2 ? Visibility.Collapsed : Visibility.Visible;

            FfmpegPercent.Text = _vm.PercentText;
            ModelPercent.Text = _vm.PercentText;
            FfmpegState.Text = StatusText();
            ProgressState.Text = StatusText();
            PaintFill(FfmpegTrack, FfmpegFill);
            PaintFill(ModelTrack, ModelFill);

            FfmpegInstallButton.IsEnabled = !_vm.IsBusy;
            ModelInstallButton.IsEnabled = !_vm.IsBusy && _vm.SelectedModel != null;
            SkipButton.IsEnabled = !_vm.IsBusy;
            BackButton.IsEnabled = !_vm.IsBusy;

            if (_vm.Page == 0)
            {
                Caption.Text = Line("Setup.Ffmpeg", "FFmpeg");
                Subtitle.Text = Line("Setup.FfmpegBody", "Downloads the Windows essentials zip and places both tools in the tools folder.");
                ColorIcon.Text = "\uE714";
            }
            else if (_vm.Page == 1)
            {
                Caption.Text = Line("Setup.LocalModel", "Offline AI model");
                Subtitle.Text = Line("Setup.LocalModelBody", "Install once. Answers stay on this computer.");
                ColorIcon.Text = "\uE9D9";
            }
            else
            {
                Caption.Text = Line("Setup.LocalDownload", "Local model download");
                Subtitle.Text = Line("Setup.NothingUploaded", "nothing is uploaded.");
                ColorIcon.Text = "\uE896";
                var row = _vm.SelectedModel;
                ProgressTitle.Text = Line("Setup.Installing", "Installing") + " " + (row?.ShortTitle ?? "");
                ProgressMeta.Text = row == null
                    ? ""
                    : $"{row.Entry.SizeMb} MB · {row.Entry.LicenseName} · {Line("Setup.NothingUploaded", "nothing is uploaded.")}";
                CancelButton.Content = _vm.IsModelBusy
                    ? FindResource("Setup.Cancel")
                    : FindResource("Setup.Close");
            }
        }

        private void PaintFill(FrameworkElement track, FrameworkElement fill)
        {
            double width = track.ActualWidth;
            if (width <= 0) width = 360;
            fill.Width = Math.Max(0, width * _vm.Percent / 100.0);
        }

        private void Track_SizeChanged(object sender, SizeChangedEventArgs e) => Apply();

        private void InstallFfmpeg_Click(object sender, RoutedEventArgs e) => _vm.InstallFfmpegCommand.Execute(null);

        private void Recheck_Click(object sender, RoutedEventArgs e) => _vm.RecheckFfmpegCommand.Execute(null);

        private void InstallModel_Click(object sender, RoutedEventArgs e) => _vm.InstallModelCommand.Execute(null);

        private void License_Click(object sender, RoutedEventArgs e) => _vm.OpenLicenseCommand.Execute(null);

        private void Back_Click(object sender, RoutedEventArgs e) => _vm.ShowFfmpegCommand.Execute(null);

        private void Skip_Click(object sender, RoutedEventArgs e) => Close();

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.IsModelBusy) _vm.CancelActive();
            else Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            if (_vm.IsModelBusy) _vm.CancelActive();
            else Close();
            e.Handled = true;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed || IsInteractive(e.OriginalSource as DependencyObject))
                return;
            try { DragMove(); }
            catch { /* DragMove throws if the button was already released. */ }
        }

        private static bool IsInteractive(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is System.Windows.Controls.Button
                    or System.Windows.Controls.ListBox
                    or System.Windows.Controls.ListBoxItem)
                    return true;
                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }
            return false;
        }

        protected override void OnClosed(EventArgs e)
        {
            _vm.PropertyChanged -= OnVmChanged;
            _vm.CancelActive();
            MarkSeen();
            base.OnClosed(e);
        }
    }
}
