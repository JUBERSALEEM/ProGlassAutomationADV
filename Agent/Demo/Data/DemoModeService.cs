using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.SampleData;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Data
{
    /// <summary>
    /// Switches ERP SQLite files to %AppData%/ProGlassAutomation.Demo.
    /// Production files under ProGlassAutomation/ are never opened,
    /// seeded or deleted by this service.
    /// </summary>
    public static class DemoModeService
    {
        public static bool IsActive => AppDataRoot.IsDemo;

        public static async Task<(bool Ok, string Message)> EnterAndSeedAsync(
            CancellationToken ct)
        {
            AppDataRoot.EnterDemo();
            Directory.CreateDirectory(AppDataRoot.DemoFolder);

            if (DemoIsolation.IsProductionPath(AppDataRoot.CurrentFolder))
            {
                AppDataRoot.ExitDemo();
                return (false, "Refused to seed — current folder resolved to production.");
            }

            try
            {
                var (canSeed, reason) = await DemoIsolation.CanSeedAsync()
                    .ConfigureAwait(false);
                if (!canSeed)
                {
                    // Already has demo records, or isolation refused.
                    // Still OK to record against the isolated folder.
                    if (AppDataRoot.IsDemo
                        && !DemoIsolation.IsProductionPath(AppDataRoot.CurrentFolder))
                    {
                        return (true, reason);
                    }
                    AppDataRoot.ExitDemo();
                    return (false, reason);
                }

                ct.ThrowIfCancellationRequested();
                string seeded = await SampleDataService.SeedAsync().ConfigureAwait(false);
                return (true, "Demo folder active. " + seeded);
            }
            catch (Exception ex)
            {
                AppDataRoot.ExitDemo();
                return (false, "Could not enter Demo Mode: " + ex.Message);
            }
        }

        public static void Exit() => AppDataRoot.ExitDemo();

        public static string ResetDemoFolder()
        {
            AppDataRoot.ExitDemo();
            string folder = AppDataRoot.DemoFolder;
            if (DemoIsolation.IsProductionPath(folder))
                return "Refused to reset — path is production.";

            if (Directory.Exists(folder))
            {
                try { Directory.Delete(folder, recursive: true); }
                catch (Exception ex) { return "Could not reset demo folder: " + ex.Message; }
            }
            Directory.CreateDirectory(folder);
            return "Demo folder cleared. Production data was not touched.";
        }
    }
}
