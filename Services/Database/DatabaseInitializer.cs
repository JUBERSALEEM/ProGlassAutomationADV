using System;

namespace ProGlassAutomation.Services.Database
{
    /// <summary>
    /// Handles database creation on first app run.
    /// Called from App.xaml.cs startup before UI loads.
    /// </summary>
    public static class DatabaseInitializer
    {
        public static void Initialize()
        {
            try
            {
                using var db = new AppDbContext();

                // Creates DB file + tables if they don't exist
                db.Database.EnsureCreated();

                System.Diagnostics.Debug.WriteLine($"[DB] Ready at: {AppDbContext.DatabaseFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB] Init error: {ex.Message}");
                // Silent fail — app can still run without DB (with degraded features)
            }
        }
    }
}