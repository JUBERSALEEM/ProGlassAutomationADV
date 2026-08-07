using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace ProGlassSetup
{
    public partial class MainWindow : Window
    {
        private int _currentStage = 0; // 0: Welcome, 1: Mode/Selection, 2: Progress, 3: Success
        private string _installPath = "";
        private bool _isUninstallMode = false;

        public MainWindow()
        {
            InitializeComponent();

            // Check if launched with /uninstall argument
            string[] args = Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (arg.Equals("/uninstall", StringComparison.OrdinalIgnoreCase))
                {
                    _isUninstallMode = true;
                    break;
                }
            }

            if (_isUninstallMode)
            {
                SetupUninstallMode();
            }
            else
            {
                // Default install path to User's Local AppData
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                _installPath = Path.Combine(localAppData ?? "", "ProGlassAutomation");
                TxtPath.Text = _installPath;
            }
        }

        private void SetupUninstallMode()
        {
            // Alter UI to show Uninstall style
            StageWelcome.Visibility = Visibility.Collapsed;
            StageMode.Visibility = Visibility.Collapsed;
            StageProgress.Visibility = Visibility.Visible;
            StageSuccess.Visibility = Visibility.Collapsed;

            _currentStage = 2; // Directly jump to action stage
            BtnPrev.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;
            BtnNext.Content = "Uninstall";

            TxtProgressStatus.Text = "Ready to Uninstall ProGlass Automation";
            TxtProgressDetail.Text = "Click Uninstall to completely remove the application and shortcuts from this system.";
            InstallProgressBar.Value = 0;
        }

        // ── Window Movement & Interaction ──
        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to cancel the ProGlass Automation installation?",
                "Cancel Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Close();
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            // Safe fallback for folder selection using standard SaveFileDialog or OpenFileDialog if FolderBrowserDialog requires forms.
            // Using a simple OpenFileDialog with a dummy filename to browse directories elegantly
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Destination Folder",
                Filter = "Directories|FoldersOnly",
                FileName = "Select Folder",
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string fileName = openFileDialog.FileName;
                string folder = Path.GetDirectoryName(fileName) ?? "";
                if (!string.IsNullOrEmpty(folder))
                {
                    TxtPath.Text = folder;
                    _installPath = folder;
                }
            }
        }

        // ── Multi-stage Navigation State Machine ──
        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStage > 0)
            {
                TransitionToStage(_currentStage - 1);
            }
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_isUninstallMode)
            {
                BtnNext.IsEnabled = false;
                await RunUninstallerAsync();
                return;
            }

            if (_currentStage == 0)
            {
                TransitionToStage(1);
            }
            else if (_currentStage == 1)
            {
                _installPath = TxtPath.Text.Trim();
                if (string.IsNullOrEmpty(_installPath))
                {
                    MessageBox.Show("Please specify a valid installation directory.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                TransitionToStage(2);
                await RunInstallerAsync();
            }
            else if (_currentStage == 3)
            {
                // Launch app if checkmark is selected
                if (ChkLaunch.IsChecked == true)
                {
                    try
                    {
                        string exePath = Path.Combine(_installPath, "ProGlassAutomation.exe");
                        if (File.Exists(exePath))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = exePath,
                                UseShellExecute = true,
                                WorkingDirectory = _installPath
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to launch ProGlass Automation: {ex.Message}", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                Close();
            }
        }

        private void TransitionToStage(int stage)
        {
            _currentStage = stage;

            // Manage Visibilities
            StageWelcome.Visibility = stage == 0 ? Visibility.Visible : Visibility.Collapsed;
            StageMode.Visibility = stage == 1 ? Visibility.Visible : Visibility.Collapsed;
            StageProgress.Visibility = stage == 2 ? Visibility.Visible : Visibility.Collapsed;
            StageSuccess.Visibility = stage == 3 ? Visibility.Visible : Visibility.Collapsed;

            // Manage navigation buttons
            BtnPrev.Visibility = (stage == 1 && !_isUninstallMode) ? Visibility.Visible : Visibility.Collapsed;
            BtnCancel.Visibility = (stage < 2) ? Visibility.Visible : Visibility.Collapsed;

            if (stage == 2)
            {
                BtnNext.IsEnabled = false;
                BtnNext.Content = "Installing...";
            }
            else if (stage == 3)
            {
                BtnNext.IsEnabled = true;
                BtnNext.Content = "Finish";
            }
            else
            {
                BtnNext.IsEnabled = true;
                BtnNext.Content = "Next";
            }
        }

        // ── Extraction and Installation Action ──
        private async Task RunInstallerAsync()
        {
            try
            {
                TxtProgressStatus.Text = "Preparing target directory...";
                TxtProgressDetail.Text = "Creating directories and validating permissions.";
                InstallProgressBar.Value = 5;
                await Task.Delay(500);

                if (!Directory.Exists(_installPath))
                {
                    Directory.CreateDirectory(_installPath);
                }

                TxtProgressStatus.Text = "Extracting software package...";
                InstallProgressBar.Value = 15;

                // Load payload from Embedded Resource
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = "ProGlassSetup.payload.zip";
                using (Stream stream = assembly.GetManifestResourceStream(resourceName) ?? Stream.Null)
                {
                    if (stream == Stream.Null)
                    {
                        throw new FileNotFoundException("Software payload file could not be found within the installer assembly.");
                    }

                    // Extract payload zip with progress calculation
                    using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        int totalEntries = archive.Entries.Count;
                        int extractedCount = 0;

                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string destinationPath = Path.GetFullPath(Path.Combine(_installPath, entry.FullName));

                            // Check for directory traversal security vulnerabilities
                            if (!destinationPath.StartsWith(_installPath, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                Directory.CreateDirectory(destinationPath);
                                continue;
                            }

                            string parentDir = Path.GetDirectoryName(destinationPath) ?? "";
                            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                            {
                                Directory.CreateDirectory(parentDir);
                            }

                            entry.ExtractToFile(destinationPath, true);
                            extractedCount++;

                            int percent = 15 + (int)(((double)extractedCount / totalEntries) * 65);

                            // Update progress on UI safely
                            Dispatcher.Invoke(() =>
                            {
                                InstallProgressBar.Value = percent;
                                TxtProgressDetail.Text = $"Copying: {entry.FullName}";
                            });

                            // Short async context yield for responsive, smooth UI
                            await Task.Yield();
                        }
                    }
                }

                TxtProgressStatus.Text = "Registering application configurations...";
                TxtProgressDetail.Text = "Creating system shortcuts and registering the uninstaller.";
                InstallProgressBar.Value = 85;
                await Task.Delay(500);

                // Create shortcuts if option is checked
                if (ChkShortcut.IsChecked == true)
                {
                    CreateShortcuts();
                }

                // Register uninstaller cleanly in Windows Control Panel (HKCU)
                RegisterUninstaller();

                // Finish Installation Stage
                InstallProgressBar.Value = 100;
                await Task.Delay(500);
                TransitionToStage(3);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Installation failed:\n{ex.Message}\n\nStack trace: {ex.StackTrace}", "Critical Setup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        // ── Uninstallation Action ──
        private async Task RunUninstallerAsync()
        {
            try
            {
                TxtProgressStatus.Text = "Removing ProGlass Automation...";
                TxtProgressDetail.Text = "Locating and deleting shortcut paths.";
                InstallProgressBar.Value = 15;
                await Task.Delay(500);

                // Determine active install path from assembly location
                string currentFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                if (string.IsNullOrEmpty(currentFolder)) currentFolder = _installPath;

                // Remove shortcuts
                DeleteShortcuts();
                InstallProgressBar.Value = 40;
                TxtProgressDetail.Text = "Cleaning registry values.";
                await Task.Delay(400);

                // Unregister from Registry
                UnregisterUninstaller();
                InstallProgressBar.Value = 60;
                TxtProgressDetail.Text = "Deleting application binary files.";
                await Task.Delay(400);

                // Request user decision regarding local user database files
                MessageBoxResult dbResult = MessageBox.Show(
                    "Do you want to permanently delete your local database files and invoice data from your system?",
                    "Keep Data Safe?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No); // Default safe value preserves database

                if (dbResult == MessageBoxResult.Yes)
                {
                    try
                    {
                        string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProGlassAutomation");
                        if (Directory.Exists(appDataDir))
                        {
                            Directory.Delete(appDataDir, true);
                        }

                        string commonDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ProGlassAutomation");
                        if (Directory.Exists(commonDataDir))
                        {
                            Directory.Delete(commonDataDir, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error removing databases: {ex.Message}");
                    }
                }

                InstallProgressBar.Value = 85;
                TxtProgressDetail.Text = "Completing system cleanup.";
                await Task.Delay(500);

                // We must schedule self-deletion of uninstaller outside of current process context.
                // We'll instruct user that removal is finished, and we will clean up using a simple cmd/powershell script after we close.
                InstallProgressBar.Value = 100;
                TxtProgressStatus.Text = "Removal Completed Successfully!";
                TxtProgressDetail.Text = "ProGlass Automation has been cleanly uninstalled.";
                await Task.Delay(800);

                MessageBox.Show("ProGlass Automation has been cleanly uninstalled from your workstation.", "Uninstall Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                // Start cmd prompt in background to delete our installation folder self-contained binaries after exit
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c timeout /t 1 /nobreak && rd /s /q \"{currentFolder}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Uninstallation error: {ex.Message}", "Uninstall Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        // ── Professional Shortcut Creation using dynamic COM fallback ──
        private void CreateShortcuts()
        {
            try
            {
                string targetExe = Path.Combine(_installPath, "ProGlassAutomation.exe");
                if (!File.Exists(targetExe)) return;

                // 1) Desktop Shortcut
                string desktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string desktopLink = Path.Combine(desktopFolder, "ProGlass Automation.lnk");
                CreateWindowsShortcut(desktopLink, targetExe, "Launch ProGlass Automation Desktop ERP");

                // 2) Start Menu Shortcut
                string startMenuFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
                string startMenuLink = Path.Combine(startMenuFolder, "ProGlass Automation.lnk");
                CreateWindowsShortcut(startMenuLink, targetExe, "Launch ProGlass Automation Desktop ERP");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to build system shortcuts: {ex.Message}");
            }
        }

        private void DeleteShortcuts()
        {
            try
            {
                // Delete Desktop Link
                string desktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ProGlass Automation.lnk");
                if (File.Exists(desktopLink)) File.Delete(desktopLink);

                // Delete Start Menu Link
                string startMenuLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "ProGlass Automation.lnk");
                if (File.Exists(startMenuLink)) File.Delete(startMenuLink);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to remove shortcuts: {ex.Message}");
            }
        }

        private void CreateWindowsShortcut(string linkPath, string targetExe, string desc)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell") ?? typeof(object);
                if (t == typeof(object)) return;

                object shell = Activator.CreateInstance(t) ?? new object();
                if (shell.GetType() == typeof(object)) return;

                object shortcut = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath }) ?? new object();
                if (shortcut.GetType() == typeof(object)) return;

                t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetExe });
                t.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { desc });
                t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetExe) ?? "" });
                t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetExe + ",0" });

                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch
            {
                // Simple modern C# PowerShell fallback if COM fails
                try
                {
                    string psCommand = $"$s = (New-Object -COM WScript.Shell).CreateShortcut('{linkPath}'); $s.TargetPath = '{targetExe}'; $s.Description = '{desc}'; $s.WorkingDirectory = '{Path.GetDirectoryName(targetExe)}'; $s.Save()";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"PowerShell shortcut build failure: {ex.Message}");
                }
            }
        }

        // ── Clean Control Panel Registry registration ──
        private void RegisterUninstaller()
        {
            try
            {
                // We'll write to HKCU to avoid requiring elevate UAC prompts, giving a perfect lightweight user-level installation
                string registryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ProGlassAutomation";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registryPath) ?? Registry.CurrentUser)
                {
                    if (key != Registry.CurrentUser)
                    {
                        string exePath = Path.Combine(_installPath, "ProGlassAutomation.exe");
                        string setupPathInInstallFolder = Path.Combine(_installPath, "uninstall.exe");

                        // Copy setup executable to serve as uninstaller
                        string setupLocation = Assembly.GetExecutingAssembly().Location ?? "";
                        if (!string.IsNullOrEmpty(setupLocation))
                        {
                            File.Copy(setupLocation, setupPathInInstallFolder, true);
                        }

                        key.SetValue("DisplayName", "ProGlass Automation");
                        key.SetValue("DisplayVersion", "1.0.0");
                        key.SetValue("Publisher", "ProGlass Automation");
                        key.SetValue("DisplayIcon", exePath);
                        key.SetValue("UninstallString", $"\"{setupPathInInstallFolder}\" /uninstall");
                        key.SetValue("InstallLocation", _installPath);
                        key.SetValue("NoModify", 1);
                        key.SetValue("NoRepair", 1);

                        try
                        {
                            long bytes = 0;
                            foreach (string f in Directory.GetFiles(_installPath, "*.*", SearchOption.AllDirectories))
                            {
                                bytes += new FileInfo(f).Length;
                            }
                            key.SetValue("EstimatedSize", (int)(bytes / 1024));
                        }
                        catch
                        {
                            key.SetValue("EstimatedSize", 180000); // 180MB approximate default
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Uninstall registration error: {ex.Message}");
            }
        }

        private void UnregisterUninstaller()
        {
            try
            {
                string registryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath, true) ?? Registry.CurrentUser)
                {
                    if (key != Registry.CurrentUser)
                    {
                        key.DeleteSubKeyTree("ProGlassAutomation", false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Uninstall unregistration error: {ex.Message}");
            }
        }
    }
}
