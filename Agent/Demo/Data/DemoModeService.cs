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
                // Startup only EnsureCreated the production folder.
                // After the switch these files are new and have no tables
                // until we create them — otherwise PI cards throw
                // SQLite error 1 (no such table: Companies / Clients / Projects).
                await InitializeCurrentFolderAsync(ct).ConfigureAwait(false);

                var (canSeed, reason) = await DemoIsolation.CanSeedAsync()
                    .ConfigureAwait(false);

                if (canSeed)
                {
                    ct.ThrowIfCancellationRequested();
                    string seeded = await SampleDataService.SeedAsync().ConfigureAwait(false);
                    await SampleDataService.SeedMasterProfilesAsync().ConfigureAwait(false);
                    return (true, "Demo folder active. " + seeded);
                }

                if (AppDataRoot.IsDemo
                    && !DemoIsolation.IsProductionPath(AppDataRoot.CurrentFolder))
                {
                    await SampleDataService.SeedMasterProfilesAsync().ConfigureAwait(false);
                    return (true, reason);
                }

                AppDataRoot.ExitDemo();
                return (false, reason);
            }
            catch (Exception ex)
            {
                AppDataRoot.ExitDemo();
                return (false, "Could not enter Demo Mode: " + ex.Message);
            }
        }

        /// <summary>
        /// Creates schema on whatever folder AppDataRoot currently points at.
        /// Must run after EnterDemo before any GetAll query.
        /// </summary>
        public static async Task InitializeCurrentFolderAsync(CancellationToken ct)
        {
            DatabaseInitializer.Initialize();

            if (App.Specs != null)
                await App.Specs.InitializeAsync().ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (App.Invoices != null)
                await App.Invoices.InitializeAsync().ConfigureAwait(false);
            if (App.Jobs != null)
                await App.Jobs.InitializeAsync().ConfigureAwait(false);
            if (App.DeliveryNotes != null)
                await App.DeliveryNotes.InitializeAsync().ConfigureAwait(false);
            if (App.MasterData != null)
                await App.MasterData.InitializeAsync().ConfigureAwait(false);
            if (App.TaxInvoices != null)
                await App.TaxInvoices.InitializeAsync().ConfigureAwait(false);
            if (App.Statements != null)
                await App.Statements.InitializeAsync().ConfigureAwait(false);
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
