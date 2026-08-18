using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.SampleData;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo
{
    /// <summary>
    /// Demo generation may seed ONLY the isolated demo folder, and only
    /// when that folder's databases are empty. It never calls ClearAllData.
    /// Production %AppData%/ProGlassAutomation is never opened by the director.
    /// </summary>
    public static class DemoIsolation
    {
        public static bool IsProductionPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string full;
            try { full = Path.GetFullPath(path); }
            catch { return false; }

            string prod = Path.GetFullPath(AppDataRoot.ProductionFolder);
            string demo = Path.GetFullPath(AppDataRoot.DemoFolder);
            if (full.StartsWith(demo, StringComparison.OrdinalIgnoreCase))
                return false;
            return full.StartsWith(prod, StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<(bool Allowed, string Reason)> CanSeedAsync()
        {
            if (!AppDataRoot.IsDemo)
            {
                return (false,
                    "Seeding is refused outside Demo Mode. Production files stay closed.");
            }

            if (IsProductionPath(AppDataRoot.CurrentFolder))
            {
                return (false,
                    "Current folder is the production folder. Seeding aborted.");
            }

            if (await SampleDataService.HasExistingDataAsync().ConfigureAwait(false))
            {
                return (false,
                    "The demo folder already has records. Seeding will not run again. " +
                    "Production data was not opened.");
            }

            return (true, "Demo folder is empty — sample data may be seeded.");
        }
    }
}
