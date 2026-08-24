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

        // Senior audit (Aug 2026): test-only override so a repository test
        // can point every DbContext at an ephemeral %TEMP% folder without
        // touching the user's real %AppData% files. Production code never
        // sets this — the getter falls back to Environment.SpecialFolder.
        //
        // WHY NOT ENV VAR: an env var would leak between tests unless
        // every test remembers to clear it; a static property assigned
        // in a test fixture ctor and cleared in Dispose is race-free.
        //
        // WHY PUBLIC SET: making it internal would force InternalsVisibleTo
        // which we don't otherwise need. Documented as test-only.
        public static string? TestFolderOverride { get; set; }

        public static string CurrentFolder
        {
            get
            {
                var overrideFolder = TestFolderOverride;
                if (!string.IsNullOrWhiteSpace(overrideFolder))
                    return overrideFolder;
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    IsDemo ? DemoName : ProductionName);
            }
        }

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
