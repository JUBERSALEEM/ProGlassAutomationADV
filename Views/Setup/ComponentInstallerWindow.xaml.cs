// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Setup/ComponentInstallerWindow.xaml.cs
// Prism card for the two optional installers: FFmpeg, then the
// offline model. Neither download starts until the user clicks.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Windows;
using ProGlassAutomation.ViewModels.Setup;

namespace ProGlassAutomation.Views.Setup
{
    public partial class ComponentInstallerWindow : Window
    {
        private readonly ComponentInstallerViewModel _vm = new();

        public ComponentInstallerWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            ModelList.ItemsSource = _vm.Models;
            ModelList.SelectedItem = _vm.SelectedModel;
            ModelList.SelectionChanged += (_, _) =>
            {
                if (ModelList.SelectedItem is SetupModelRow row)
                    _vm.SelectedModel = row;
            };
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(ComponentInstallerViewModel.Page)
                    or nameof(ComponentInstallerViewModel.Status)
                    or nameof(ComponentInstallerViewModel.Percent)
                    or nameof(ComponentInstallerViewModel.FfmpegReady)
                    or nameof(ComponentInstallerViewModel.IsBusy))
                {
                    Apply();
                }
            };
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
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void Show(Window? owner)
        {
            foreach (Window open in Application.Current.Windows)
            {
                if (open is ComponentInstallerWindow existing)
                {
                    existing.Activate();
                    return;
                }
            }
            var window = new ComponentInstallerWindow();
            if (owner != null) window.Owner = owner;
            window.Show();
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

        private void Apply()
        {
            FfmpegPage.Visibility = _vm.Page == 0 ? Visibility.Visible : Visibility.Collapsed;
            ModelPage.Visibility = _vm.Page == 1 ? Visibility.Visible : Visibility.Collapsed;
            ProgressPage.Visibility = _vm.Page == 2 ? Visibility.Visible : Visibility.Collapsed;
            FfmpegBar.Value = _vm.Percent;
            ModelBar.Value = _vm.Percent;
            FfmpegState.Text = _vm.Status;
            ProgressState.Text = _vm.Status;
            StatusLine.Text = _vm.Status;

            if (_vm.Page == 0)
            {
                Heading.Text = (string)FindResource("Setup.Ffmpeg");
                Body.Text = (string)FindResource("Setup.FfmpegNeed");
                PrimaryButton.Content = FindResource("Setup.DownloadInstall");
                SecondaryButton.Content = FindResource("Setup.Recheck");
                SecondaryButton.Visibility = Visibility.Visible;
                BackButton.Visibility = Visibility.Collapsed;
                SkipButton.Visibility = Visibility.Visible;
            }
            else if (_vm.Page == 1)
            {
                Heading.Text = (string)FindResource("Setup.LocalModel");
                Body.Text = (string)FindResource("Setup.LocalModelBody");
                PrimaryButton.Content = FindResource("Setup.Install");
                SecondaryButton.Content = FindResource("Setup.License");
                SecondaryButton.Visibility = Visibility.Visible;
                BackButton.Visibility = Visibility.Visible;
                SkipButton.Visibility = Visibility.Visible;
            }
            else
            {
                Heading.Text = (string)FindResource("Setup.LocalModel");
                Body.Text = _vm.SelectedModel?.Title ?? "";
                ProgressTitle.Text = _vm.SelectedModel?.Title ?? "";
                PrimaryButton.Content = FindResource("Setup.Cancel");
                SecondaryButton.Visibility = Visibility.Collapsed;
                BackButton.Visibility = Visibility.Collapsed;
                SkipButton.Visibility = Visibility.Collapsed;
            }

            PrimaryButton.IsEnabled = !_vm.IsBusy || _vm.Page == 2;
        }

        private void Primary_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Page == 0) _vm.InstallFfmpegCommand.Execute(null);
            else if (_vm.Page == 1) _vm.InstallModelCommand.Execute(null);
            else _vm.CancelModelCommand.Execute(null);
        }

        private void Secondary_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Page == 0) _vm.RecheckFfmpegCommand.Execute(null);
            else _vm.OpenLicenseCommand.Execute(null);
        }

        private void Back_Click(object sender, RoutedEventArgs e) => _vm.ShowFfmpegCommand.Execute(null);

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Page == 0) _vm.ShowModelCommand.Execute(null);
            else Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            MarkSeen();
            base.OnClosed(e);
        }
    }
}
