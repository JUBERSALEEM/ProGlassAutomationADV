using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Logging;
using System;
using System.IO;

namespace ProGlassAutomation.Services.Database
{
    /// <summary>
    /// Single folder every ERP SQLite file lives under.
    /// Supports a professional LAN/Wi-Fi shared data root so all company
    /// workstations can point at the same dataset from Settings.
    /// </summary>
    public static class AppDataRoot
    {
        public const string ProductionName = "ProGlassAutomation";
        public const string DemoName = "ProGlassAutomation.Demo";

        private static string _sharedDataRoot = "";

        public static bool IsDemo { get; private set; }
        public static bool IsSharedDataRootEnabled => !string.IsNullOrWhiteSpace(_sharedDataRoot) && !IsDemo;
        public static string SharedDataRoot => _sharedDataRoot;

        public static string? TestFolderOverride { get; set; }

        public static string CurrentFolder
        {
            get
            {
                var overrideFolder = TestFolderOverride;
                if (!string.IsNullOrWhiteSpace(overrideFolder))
                    return overrideFolder;

                if (IsDemo)
                    return DemoFolder;

                if (!string.IsNullOrWhiteSpace(_sharedDataRoot))
                    return _sharedDataRoot;

                return ProductionFolder;
            }
        }

        public static string ProductionFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ProductionName);

        public static string DemoFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            DemoName);

        public static void ConfigureFrom(AppConfig? config)
        {
            try
            {
                var data = config?.DataConnectivity;
                if (data?.UseSharedDataRoot == true
                    && TryNormalizeSharedRoot(data.SharedDataRootPath, out var normalized))
                {
                    _sharedDataRoot = normalized;
                    Directory.CreateDirectory(_sharedDataRoot);
                    AppLogService.Info(
                        $"Shared company database root enabled: {_sharedDataRoot}",
                        new LogContext { Module = "Database", Operation = "ConfigureSharedRoot" });
                }
                else
                {
                    _sharedDataRoot = "";
                }
            }
            catch (Exception ex)
            {
                _sharedDataRoot = "";
                AppLogService.Warning(
                    "Shared database root could not be enabled; falling back to local AppData. " + ex.Message,
                    new LogContext { Module = "Database", Operation = "ConfigureSharedRoot" });
            }
        }

        public static bool TryNormalizeSharedRoot(string? path, out string normalized)
        {
            normalized = "";
            if (string.IsNullOrWhiteSpace(path)) return false;

            try
            {
                var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
                var local = Path.GetFullPath(ProductionFolder);
                var demo = Path.GetFullPath(DemoFolder);

                if (full.Equals(local, StringComparison.OrdinalIgnoreCase)
                    || full.Equals(demo, StringComparison.OrdinalIgnoreCase))
                    return false;

                normalized = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void EnterDemo()
        {
            IsDemo = true;
            Directory.CreateDirectory(CurrentFolder);
        }

        public static void ExitDemo() => IsDemo = false;
    }
}
