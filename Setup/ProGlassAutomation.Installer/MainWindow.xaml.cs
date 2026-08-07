using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Microsoft.Win32;

namespace ProGlassAutomation.Installer
{
    public partial class MainWindow : Window
    {
        private int _currentStage = 0; // 0: Welcome, 1: Location, 2: Options, 3: Progress, 4: Success
        private string _installPath = "";
        private bool _isUninstallMode = false;
        private readonly ObservableCollection<string> _logs = new ObservableCollection<string>();

        public MainWindow()
        {
            InitializeComponent();
            LstLogs.ItemsSource = _logs;

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
                // Professional local AppData / Program Files fallback directory default
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                _installPath = Path.Combine(localAppData ?? "", "ProGlassAutomation");
                TxtPath.Text = _installPath;
                ValidateDiskSpace();
            }
        }

        private void SetupUninstallMode()
        {
            // Alter UI to show clean Uninstall setup
            StageWelcome.Visibility = Visibility.Collapsed;
            StageLocation.Visibility = Visibility.Collapsed;
            StageOptions.Visibility = Visibility.Collapsed;
            StageProgress.Visibility = Visibility.Visible;
            StageSuccess.Visibility = Visibility.Collapsed;

            _currentStage = 3; // Action stage
            BtnPrev.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;
            BtnNext.Content = "Uninstall";

            TxtProgressStatus.Text = "Ready to Uninstall ProGlass Automation";
            TxtProgressPercent.Text = "";
            Log("Uninstaller mode initialized.");
        }

        private void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string time = DateTime.Now.ToString("HH:mm:ss");
                _logs.Add($"[{time}] {message}");

                // Keep the listbox scrolled to the end
                if (LstLogs.Items.Count > 0)
                {
                    LstLogs.ScrollIntoView(LstLogs.Items[LstLogs.Items.Count - 1]);
                }
            });
        }

        private void ValidateDiskSpace()
        {
            try
            {
                string root = Path.GetPathRoot(_installPath) ?? "C:\\";
                var drive = new DriveInfo(root);
                long availableBytes = drive.AvailableFreeSpace;
                double availableMB = availableBytes / (1024.0 * 1024.0);

                TxtDiskAvailable.Text = $"{availableMB:N1} MB";

                if (availableMB < 180.0)
                {
                    TxtDiskAvailable.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
                    Log($"Warning: Insufficient disk space on drive {root}. Installation requires at least 180 MB.");
                }
                else
                {
                    TxtDiskAvailable.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
                }
            }
            catch (Exception ex)
            {
                TxtDiskAvailable.Text = "Unknown";
                Log($"Failed to validate disk space: {ex.Message}");
            }
        }

        // ── Window Actions & Drag ──
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
                "Are you sure you want to cancel the ProGlass Automation setup?",
                "Cancel Installation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Close();
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Destination Installation Directory",
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
                    ValidateDiskSpace();
                }
            }
        }

        // ── Stage State Machine Navigation ──
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
                    MessageBox.Show("Please choose a valid destination directory.", "Path Required", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                TransitionToStage(2);
            }
            else if (_currentStage == 2)
            {
                TransitionToStage(3);
                await RunInstallerAsync();
            }
            else if (_currentStage == 4)
            {
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
                        MessageBox.Show($"Could not launch ProGlass Automation: {ex.Message}", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                Close();
            }
        }

        private void TransitionToStage(int stage)
        {
            _currentStage = stage;

            StageWelcome.Visibility = stage == 0 ? Visibility.Visible : Visibility.Collapsed;
            StageLocation.Visibility = stage == 1 ? Visibility.Visible : Visibility.Collapsed;
            StageOptions.Visibility = stage == 2 ? Visibility.Visible : Visibility.Collapsed;
            StageProgress.Visibility = stage == 3 ? Visibility.Visible : Visibility.Collapsed;
            StageSuccess.Visibility = stage == 4 ? Visibility.Visible : Visibility.Collapsed;

            BtnPrev.Visibility = (stage > 0 && stage < 3 && !_isUninstallMode) ? Visibility.Visible : Visibility.Collapsed;
            BtnCancel.Visibility = (stage < 3) ? Visibility.Visible : Visibility.Collapsed;

            if (stage == 0)
            {
                BtnNext.Content = "Install";
            }
            else if (stage == 3)
            {
                BtnNext.IsEnabled = false;
                BtnNext.Content = "Installing...";
            }
            else if (stage == 4)
            {
                BtnNext.IsEnabled = true;
                BtnNext.Content = "Finish";
            }
            else
            {
                BtnNext.Content = "Next";
            }
        }

        // ── Async Installation Engine ──
        private async Task RunInstallerAsync()
        {
            try
            {
                Log("Preparing workstation target directory...");
                await Task.Delay(500);

                if (!Directory.Exists(_installPath))
                {
                    Directory.CreateDirectory(_installPath);
                    Log("Created target directory successfully.");
                }

                // Protect databases during installer upgrading
                ProtectDatabaseSafety();

                Log("Unpacking application files from setup stream...");
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = "ProGlassAutomation.Installer.payload.zip";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName) ?? Stream.Null)
                {
                    if (stream == Stream.Null)
                    {
                        throw new FileNotFoundException("Setup payload archive not found in the installer resource manifest.");
                    }

                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        int total = archive.Entries.Count;
                        int current = 0;

                        foreach (var entry in archive.Entries)
                        {
                            string destFile = Path.GetFullPath(Path.Combine(_installPath, entry.FullName));

                            // Safeguard directory traversal vulnerability (Zip Slip)
                            if (!destFile.StartsWith(_installPath, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                Directory.CreateDirectory(destFile);
                                continue;
                            }

                            string? parentDir = Path.GetDirectoryName(destFile);
                            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                            {
                                Directory.CreateDirectory(parentDir);
                            }

                            // Keep existing database files safe from overwrite during updates!
                            if (destFile.EndsWith(".db", StringComparison.OrdinalIgnoreCase) && File.Exists(destFile))
                            {
                                Log($"Preserved existing module database: {entry.Name}");
                                continue;
                            }

                            entry.ExtractToFile(destFile, true);
                            current++;

                            int percent = (int)((double)current / total * 100);
                            Dispatcher.Invoke(() =>
                            {
                                InstallProgressBar.Value = percent;
                                TxtProgressPercent.Text = $"{percent}% Completed";
                            });

                            Log($"Extracted: {entry.Name}");
                            await Task.Yield();
                        }
                    }
                }

                Log("Registering shortcuts and uninstall hooks...");
                await Task.Delay(500);

                if (ChkDesktop.IsChecked == true)
                {
                    CreateShortcuts();
                }

                RegisterUninstaller();

                Log("Setup successfully completed.");
                await Task.Delay(500);
                TransitionToStage(4);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Setup failed: {ex.Message}", "Critical Installation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        // ── Asynchronous Uninstallation Engine ──
        private async Task RunUninstallerAsync()
        {
            try
            {
                Log("Locating ProGlass Automation registry hooks...");
                await Task.Delay(500);

                string currentFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                Log("Removing system shortcuts...");
                DeleteShortcuts();
                await Task.Delay(400);

                Log("Removing Control Panel uninstall keys...");
                UnregisterUninstaller();
                await Task.Delay(400);

                MessageBoxResult preserveData = MessageBox.Show(
                    "Do you wish to preserve your local application data and database invoices safe on your system?",
                    "Preserve User Databases?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes); // Safe default preserves user data

                if (preserveData == MessageBoxResult.No)
                {
                    Log("Deleting local user databases as requested...");
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
                        Log($"Failed to clear some data folders: {ex.Message}");
                    }
                }

                Log("Finalizing uninstallation context...");
                await Task.Delay(500);

                MessageBox.Show("ProGlass Automation has been cleanly uninstalled from your workstation.", "Uninstall Completed", MessageBoxButton.OK, MessageBoxImage.Information);

                // Self delete uninstaller executable via secondary delayed process context
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
                MessageBox.Show($"Uninstall failed: {ex.Message}", "Uninstall Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void ProtectDatabaseSafety()
        {
            // Protect databases by creating a safety backup of pre-existing databases before installer proceeds
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProGlassAutomation");

                if (Directory.Exists(appDataFolder))
                {
                    string backupFolder = Path.Combine(appDataFolder, "Backup", "Update_" + timestamp);
                    Directory.CreateDirectory(backupFolder);

                    foreach (string db in Directory.GetFiles(appDataFolder, "*.db"))
                    {
                        File.Copy(db, Path.Combine(backupFolder, Path.GetFileName(db)), true);
                        Log($"Created safety backup of {Path.GetFileName(db)}.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Database safety backup failed to run: {ex.Message}");
            }
        }

        private void CreateShortcuts()
        {
            try
            {
                string targetExe = Path.Combine(_installPath, "ProGlassAutomation.exe");
                if (!File.Exists(targetExe)) return;

                // Create Desktop Shortcut
                string desktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string desktopLink = Path.Combine(desktopFolder, "ProGlass Automation.lnk");
                CreateShortcutFile(desktopLink, targetExe, "Launch ProGlass Automation ERP");

                // Create Start Menu Shortcut
                string startMenuFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
                string startMenuLink = Path.Combine(startMenuFolder, "ProGlass Automation.lnk");
                CreateShortcutFile(startMenuLink, targetExe, "Launch ProGlass Automation ERP");

                Log("Shortcuts registered successfully.");
            }
            catch (Exception ex)
            {
                Log($"Failed to write shortcuts: {ex.Message}");
            }
        }

        private void DeleteShortcuts()
        {
            try
            {
                string desktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ProGlass Automation.lnk");
                if (File.Exists(desktopLink)) File.Delete(desktopLink);

                string startMenuLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "ProGlass Automation.lnk");
                if (File.Exists(startMenuLink)) File.Delete(startMenuLink);
            }
            catch (Exception ex)
            {
                Log($"Failed to delete shortcuts: {ex.Message}");
            }
        }

        private void CreateShortcutFile(string linkPath, string targetExe, string desc)
        {
            try
            {
                Type? t = Type.GetTypeFromProgID("WScript.Shell");
                if (t != null)
                {
                    object? shell = Activator.CreateInstance(t);
                    if (shell != null)
                    {
                        object? shortcut = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
                        if (shortcut != null)
                        {
                            t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetExe });
                            t.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { desc });
                            t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetExe) ?? "" });
                            t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetExe + ",0" });
                            t.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                            return;
                        }
                    }
                }
            }
            catch
            {
                // PowerShell Fallback mechanism
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
                    Debug.WriteLine($"PowerShell shortcut failed: {ex.Message}");
                }
            }
        }

        private void RegisterUninstaller()
        {
            try
            {
                string registryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ProGlassAutomation";
                using (RegistryKey? key = Registry.CurrentUser.CreateSubKey(registryPath))
                {
                    if (key != null)
                    {
                        string exePath = Path.Combine(_installPath, "ProGlassAutomation.exe");
                        string setupPathInInstallFolder = Path.Combine(_installPath, "uninstall.exe");

                        string setupLocation = Assembly.GetExecutingAssembly().Location;
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
                            key.SetValue("EstimatedSize", 180000);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to register uninstaller: {ex.Message}");
            }
        }

        private void UnregisterUninstaller()
        {
            try
            {
                string registryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(registryPath, true))
                {
                    if (key != null)
                    {
                        key.DeleteSubKeyTree("ProGlassAutomation", false);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to unregister uninstaller: {ex.Message}");
            }
        }
    }
}
