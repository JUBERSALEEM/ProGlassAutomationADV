using System;
using System.IO;

namespace ProGlassAutomation.Services.Database
{
    /// <summary>
    /// Single folder every ERP SQLite file lives under.
    /// Demo Mode switches this to a sibling folder so production files
    /// are never opened, seeded or deleted.
    /// </summary>
    public static class AppDataRoot
    {
        public const string ProductionName = "ProGlassAutomation";
        public const string DemoName = "ProGlassAutomation.Demo";

        public static bool IsDemo { get; private set; }

        public static string CurrentFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            IsDemo ? DemoName : ProductionName);

        public static string ProductionFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ProductionName);

        public static string DemoFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            DemoName);

        public static void EnterDemo()
        {
            IsDemo = true;
            Directory.CreateDirectory(CurrentFolder);
        }

        public static void ExitDemo() => IsDemo = false;
    }
}
